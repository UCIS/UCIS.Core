using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using UCIS.Util;
using HTTPHeader = System.Collections.Generic.KeyValuePair<string, string>;

namespace UCIS.Net.HTTP {
	public class HTTPServer : TCPServer.IModule, IDisposable {
		public IHTTPContentProvider ContentProvider { get; set; }
		public Boolean ServeFlashPolicyFile { get; set; }
		public X509Certificate SSLCertificate { get; set; }
		public Boolean AllowGzipCompression { get; set; }
		public int RequestTimeout { get; set; }
		public IList<KeyValuePair<String, String>> DefaultHeaders { get; private set; }
		public ErrorEventHandler OnError;
		private Socket Listener = null;

		public HTTPServer() {
			DefaultHeaders = new List<KeyValuePair<String, String>>() {
				new KeyValuePair<String, String>("Server", "UCIS Embedded Webserver"),
			};
			AllowGzipCompression = true;
			RequestTimeout = 5;
		}

		public void Listen(int port) {
			Listen(new IPEndPoint(IPAddress.Any, port));
		}

		public void Listen(EndPoint localep) {
			if (Listener != null) throw new InvalidOperationException("A listener exists");
			Listener = new Socket(localep.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			Listener.Bind(localep);
			Listener.Listen(10);
			Listener.BeginAccept(AcceptCallback, null);
		}

		private void AcceptCallback(IAsyncResult ar) {
			Socket socket = null;
			try {
				socket = Listener.EndAccept(ar);
				HandleClient(socket);
			} catch (Exception ex) {
				RaiseOnError(this, ex);
				if (socket != null) socket.Close();
			}
			try {
				Listener.BeginAccept(AcceptCallback, null);
			} catch (Exception ex) {
				RaiseOnError(this, ex);
			}
		}

		private void SslAuthenticationCallback(IAsyncResult ar) {
			Object[] args = (Object[])ar.AsyncState;
			Socket socket = (Socket)args[0];
			SslStream ssl = (SslStream)args[1];
			Stream streamwrapper = (Stream)args[2];
			try {
				ssl.EndAuthenticateAsServer(ar);
				new HTTPContext(this, ssl, socket, -1, true);
			} catch (Exception ex) {
				RaiseOnError(this, ex);
				streamwrapper.Close();
				if (socket != null) socket.Close();
			}
		}

		public void Dispose() {
			if (Listener != null) Listener.Close();
		}

		public void HandleClient(Socket socket, Stream streamwrapper) {
			if (streamwrapper == null) streamwrapper = new NetworkStream(socket, true);
			try {
				if (SSLCertificate != null) {
					SslStream ssl = new SslStream(streamwrapper);
					ssl.BeginAuthenticateAsServer(SSLCertificate, SslAuthenticationCallback, new Object[] { socket, ssl, streamwrapper });
				} else {
					new HTTPContext(this, streamwrapper, socket, -1, false);
				}
			} catch {
				streamwrapper.Close();
				throw;
			}
		}

		public void HandleClient(Socket client) {
			HandleClient(client, null);
		}

		bool TCPServer.IModule.Accept(TCPStream stream) {
			HandleClient(stream.Socket, stream);
			return false;
		}

		internal void RaiseOnError(Object sender, Exception error) {
			System.Diagnostics.Debug.WriteLine(error);
			ErrorEventHandler eh = OnError;
			if (eh != null) eh(sender, new ErrorEventArgs(error));
		}

		public static String GetMimeTypeForExtension(String extension) {
			switch (extension.TrimStart('.').ToLowerInvariant()) {
				case "txt": return "text/plain";
				case "htm":
				case "html": return "text/html";
				case "css": return "text/css";
				case "js": return "application/javascript";
				case "png": return "image/png";
				case "jpg":
				case "jpeg": return "image/jpeg";
				case "gif": return "image/gif";
				case "ico": return "image/x-icon";
				default: return null;
			}
		}
	}

	public enum HTTPResponseStreamMode {
		None = -1,
		Direct = 0,
		Buffered = 1,
		Chunked = 2,
		Hybrid = 3,
	}

	public class HTTPContext {
		public HTTPServer Server { get; private set; }
		public EndPoint LocalEndPoint { get; private set; }
		public EndPoint RemoteEndPoint { get; private set; }

		public String RequestMethod { get; private set; }
		public String RequestPath { get; private set; }
		public String RequestQuery { get; private set; }
		public int HTTPVersion { get; set; }

		public Socket Socket { get; private set; }
		public Boolean AsynchronousCompletion { get; set; }
		public Boolean KeepAlive { get; set; }
		public TCPStream TCPStream { get { return Reader.BaseStream as TCPStream; } }
		public Boolean IsSecure { get; private set; }

		private PrebufferingStream Reader;
		private List<HTTPHeader> RequestHeaders = null, ResponseHeaders = null;
		private HTTPConnectionState State = HTTPConnectionState.Starting;
		private KeyValuePair<String, String>[] QueryParameters = null, PostParameters = null, Cookies = null;
		private Stream ResponseStream = null;
		private Stream RequestStream = null;
		private Boolean AcceptGzipCompression = false;
		private int KeepAliveMaxRequests = 20;
		private Timer TimeoutTimer = null;
		public Boolean AllowGzipCompression { get; set; }
		private int ResponseStatusCode;
		private String ResponseStatusInfo;

		private enum HTTPConnectionState {
			Starting = 0,
			ReceivingRequest = 1,
			ProcessingRequest = 2,
			SendingHeaders = 3,
			SendingContent = 4,
			Completed = 5,
			Closed = 6,
		}

		private class HTTPOutputStream : Stream {
			public HTTPResponseStreamMode Mode { get; private set; }
			public HTTPContext Context { get; private set; }
			private Stream OutputStream = null;
			private MemoryStream Buffer = null;
			private long BytesWritten = 0;
			private long MaxLength;

			public HTTPOutputStream(HTTPContext context, HTTPResponseStreamMode mode) : this(context, mode, -1) { }
			public HTTPOutputStream(HTTPContext context, HTTPResponseStreamMode mode, long length) {
				this.Context = context;
				this.Mode = mode;
				this.MaxLength = length;
				switch (Mode) {
					case HTTPResponseStreamMode.Direct:
						if (MaxLength != -1) Context.SendHeader("Content-Length", MaxLength.ToString());
						OutputStream = Context.BeginResponseData();
						break;
					case HTTPResponseStreamMode.Chunked:
						Context.SendHeader("Transfer-Encoding", "chunked");
						OutputStream = Context.BeginResponseData();
						break;
					case HTTPResponseStreamMode.Buffered:
					case HTTPResponseStreamMode.Hybrid:
						if (Context.State != HTTPConnectionState.ProcessingRequest) throw new InvalidOperationException("The response stream can not be created in the current state");
						break;
					default: throw new InvalidOperationException("Response stream mode is not supported");
				}
			}

			private void WriteBuffered(byte[] buffer, int offset, int count) {
				if (Buffer == null) Buffer = new MemoryStream();
				Buffer.Write(buffer, offset, count);
			}
			private void WriteChunked(byte[] buffer, int offset, int count) {
				Byte[] lb = Encoding.ASCII.GetBytes(count.ToString("X") + "\r\n");
				OutputStream.Write(lb, 0, lb.Length);
				if (count != 0) OutputStream.Write(buffer, offset, count);
				OutputStream.Write(new Byte[] { (Byte)'\r', (Byte)'\n' }, 0, 2);
			}
			private void HybridSwitchToChunked() {
				MemoryStream oldbuffer = Buffer;
				Buffer = null;
				Context.SendHeader("Transfer-Encoding", "chunked");
				OutputStream = Context.BeginResponseData();
				Mode = HTTPResponseStreamMode.Chunked;
				if (oldbuffer != null) oldbuffer.WriteTo(this);
			}

			public override void Write(byte[] buffer, int offset, int count) {
				if (offset < 0 || count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException("buffer", "Offset and count arguments exceed the buffer dimensions");
				switch (Mode) {
					case HTTPResponseStreamMode.Direct:
						if (MaxLength != -1 && BytesWritten + count > MaxLength) throw new InvalidOperationException("The write operation exceeds the transfer length");
						OutputStream.Write(buffer, offset, count);
						BytesWritten += count;
						break;
					case HTTPResponseStreamMode.Buffered:
						WriteBuffered(buffer, offset, count);
						break;
					case HTTPResponseStreamMode.Chunked:
						if (count != 0) WriteChunked(buffer, offset, count);
						BytesWritten += count;
						break;
					case HTTPResponseStreamMode.Hybrid:
						if (count > 1024 || (Buffer != null && Buffer.Length + count > 1024)) {
							HybridSwitchToChunked();
							if (count != 0) WriteChunked(buffer, offset, count);
						} else {
							WriteBuffered(buffer, offset, count);
						}
						break;
				}
			}
			class CompletedAsyncResult : AsyncResultBase {
				public CompletedAsyncResult(AsyncCallback callback, Object state) : base(callback, state) {
					SetCompleted(true, null);
				}
			}
			public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state) {
				if (offset < 0 || count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException("buffer", "Offset and count arguments exceed the buffer dimensions");
				switch (Mode) {
					case HTTPResponseStreamMode.Direct:
						if (MaxLength != -1 && BytesWritten + count > MaxLength) throw new InvalidOperationException("The write operation exceeds the transfer length");
						BytesWritten += count;
						return OutputStream.BeginWrite(buffer, offset, count, callback, state);
					case HTTPResponseStreamMode.Buffered:
					case HTTPResponseStreamMode.Chunked:
					case HTTPResponseStreamMode.Hybrid:
						Write(buffer, offset, count);
						return new CompletedAsyncResult(callback, state);
					default: return null;
				}
			}
			public override void EndWrite(IAsyncResult asyncResult) {
				switch (Mode) {
					case HTTPResponseStreamMode.Direct:
						OutputStream.EndWrite(asyncResult);
						break;
				}
			}
			public override void SetLength(long value) {
				switch (Mode) {
					case HTTPResponseStreamMode.Buffered:
					case HTTPResponseStreamMode.Hybrid:
						if (value != 0) throw new InvalidOperationException("The length can only be set to zero using this method");
						Buffer = null;
						break;
					default: throw new InvalidOperationException("The operation is not supported in the current mode");
				}
			}
			public override long Length {
				get { return MaxLength == -1 ? Position : MaxLength; }
			}
			public override long Position {
				get { return (Buffer == null) ? BytesWritten : BytesWritten + Buffer.Length; }
				set { throw new NotSupportedException(); }
			}
			public override void Flush() {
				switch (Mode) {
					case HTTPResponseStreamMode.Hybrid:
						HybridSwitchToChunked();
						break;
				}
			}
			public override bool CanWrite {
				get { return Context.State != HTTPConnectionState.Completed && Context.State != HTTPConnectionState.Closed && (OutputStream == null || OutputStream.CanWrite); }
			}
			protected override void Dispose(bool disposing) {
				base.Dispose(disposing);
				if (disposing) {
					switch (Mode) {
						case HTTPResponseStreamMode.Direct:
							if (MaxLength == -1 || (MaxLength != -1 && MaxLength > BytesWritten)) Context.KeepAlive = false;
							break;
						case HTTPResponseStreamMode.Chunked:
							WriteChunked(null, 0, 0);
							Mode = HTTPResponseStreamMode.None;
							break;
						case HTTPResponseStreamMode.Buffered:
						case HTTPResponseStreamMode.Hybrid:
							long length = (Buffer == null) ? 0 : Buffer.Length;
							Context.SendHeader("Content-Length", length.ToString());
							OutputStream = Context.BeginResponseData();
							if (Buffer != null) Buffer.WriteTo(OutputStream);
							Buffer = null;
							Mode = HTTPResponseStreamMode.None;
							break;
					}
					Context.EndResponseData();
				}
			}
			public override bool CanTimeout {
				get { return OutputStream == null ? true : OutputStream.CanTimeout; }
			}
			public override int WriteTimeout {
				get { return OutputStream == null ? 0 : OutputStream.WriteTimeout; }
				set { if (OutputStream != null) OutputStream.WriteTimeout = value; }
			}

			public override bool CanRead { get { return false; } }
			public override bool CanSeek { get { return false; } }

			public override int Read(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
			public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
		}
		private class HTTPInputStream : Stream {
			public HTTPResponseStreamMode Mode { get; private set; }
			private Stream InputStream = null;
			private long BytesRead = 0;
			private long BytesLeft = 0;
			public HTTPInputStream(HTTPContext context) {
				String TransferEncoding = context.GetRequestHeader("Transfer-Encoding");
				String ContentLength = context.GetRequestHeader("Content-Length");
				InputStream = context.Reader;
				if (TransferEncoding != null && TransferEncoding.StartsWith("chunked", StringComparison.InvariantCultureIgnoreCase)) {
					Mode = HTTPResponseStreamMode.Chunked;
				} else if (ContentLength != null) {
					Mode = HTTPResponseStreamMode.Direct;
					if (!long.TryParse(ContentLength, out BytesLeft)) BytesLeft = 0;
				} else {
					Mode = HTTPResponseStreamMode.None;
				}
			}
			private int ReadDirect(Byte[] buffer, int offset, int count) {
				if (count > BytesLeft) count = (int)BytesLeft;
				if (count == 0) return 0;
				int read = InputStream.Read(buffer, offset, count);
				if (read >= 0) {
					BytesRead += read;
					BytesLeft -= read;
				}
				return read;
			}
			public override int Read(byte[] buffer, int offset, int count) {
				if (offset < 0 || count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException("buffer", "Offset and count arguments exceed the buffer dimensions");
				switch (Mode) {
					case HTTPResponseStreamMode.None: 
						return 0;
					case HTTPResponseStreamMode.Direct:
						return ReadDirect(buffer, offset, count);
					case HTTPResponseStreamMode.Chunked:
						if (BytesLeft == 0) {
							String length = ReadLine(InputStream);
							if (!long.TryParse(length, out BytesLeft)) BytesLeft = 0;
							if (BytesLeft == 0) {
								while (true) {
									String line = ReadLine(InputStream);
									if (line == null || line.Length == 0) break;
								}
								Mode = HTTPResponseStreamMode.None;
								return 0;
							}
						}
						return ReadDirect(buffer, offset, count);
					default:
						return 0;
				}
			}
			class CompletedAsyncResult : AsyncResultBase {
				public int Count { get; private set; }
				public CompletedAsyncResult(AsyncCallback callback, Object state, int count)
					: base(callback, state) {
					this.Count = count;
					SetCompleted(true, null);
				}
			}
			public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state) {
				if (BytesLeft > 0) {
					if (count > BytesLeft) count = (int)BytesLeft;
					if (count == 0) return new CompletedAsyncResult(callback, state, 0);
					return InputStream.BeginRead(buffer, offset, count, callback, state);
				} else {
					int ret = Read(buffer, offset, count);
					return new CompletedAsyncResult(callback, state, ret);
				}
			}
			public override int EndRead(IAsyncResult asyncResult) {
				CompletedAsyncResult car = asyncResult as CompletedAsyncResult;
				if (car != null) {
					return car.Count;
				} else {
					return InputStream.EndRead(asyncResult);
				}
			}
			public override long Length {
				get { return BytesRead + BytesLeft; }
			}
			public override long Position {
				get { return BytesRead; }
				set { throw new NotSupportedException(); }
			}
			public override bool CanRead {
				get { return (BytesLeft > 0 || Mode == HTTPResponseStreamMode.Chunked) && InputStream.CanRead; }
			}
			public override bool CanTimeout {
				get { return InputStream.CanTimeout; }
			}
			public override int ReadTimeout {
				get { return InputStream.ReadTimeout; }
				set { InputStream.ReadTimeout = value; }
			}
			protected override void Dispose(bool disposing) {
				base.Dispose(disposing);
				Byte[] dummy = new Byte[1024];
				while (Read(dummy, 0, dummy.Length) != 0) ;
			}

			public override bool CanSeek { get { return false; } }
			public override bool CanWrite { get { return false; } }
			public override void Flush() { }
			public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
			public override void SetLength(long value) { throw new NotSupportedException(); }
			public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
		}

		internal HTTPContext(HTTPServer server, Stream stream, Socket socket, int maxrequests, Boolean issecure) {
			if (ReferenceEquals(server, null)) throw new ArgumentNullException("server");
			if (ReferenceEquals(socket, null) && ReferenceEquals(stream, null)) throw new ArgumentNullException("stream");
			this.Server = server;
			this.Socket = socket;
			this.IsSecure = issecure;
			if (maxrequests != -1) this.KeepAliveMaxRequests = maxrequests;
			this.AllowGzipCompression = true;
			if (socket != null) {
				this.LocalEndPoint = socket.LocalEndPoint;
				this.RemoteEndPoint = socket.RemoteEndPoint;
				if (stream == null) stream = new NetworkStream(socket, true);
			}
			Reader = stream as PrebufferingStream ?? new PrebufferingStream(stream);
			if (server.RequestTimeout > 0) TimeoutTimer = new Timer(TimeoutCallback, null, server.RequestTimeout * 1000 + 1000, Timeout.Infinite);
			Reader.BeginPrebuffering(PrebufferCallback, null);
		}

		public static String ReadLine(Stream stream) {
			StringBuilder s = new StringBuilder();
			while (true) {
				int b = stream.ReadByte();
				if (b == -1) {
					if (s.Length == 0) return null;
					break;
				} else if (b == 13) {
				} else if (b == 10 || b == 0) {
					break;
				} else {
					s.Append((Char)b);
				}
			}
			return s.ToString();
		}
		private String ReadLine() {
			return ReadLine(Reader);
		}

		private void TimeoutCallback(Object state) {
			if (State == HTTPConnectionState.Starting || State == HTTPConnectionState.ReceivingRequest) Close();
		}

		private void PrebufferCallback(IAsyncResult ar) {
			State = HTTPConnectionState.ReceivingRequest;
			try {
				Reader.EndPrebuffering(ar);
				String line = ReadLine();
				if (line == null) {
					Close();
					return;
				}
				if (Server.ServeFlashPolicyFile && line[0] == '<') { //<policy-file-request/>
					StreamWriter writer = new StreamWriter(Reader, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = false };
					writer.WriteLine("<cross-domain-policy><allow-access-from domain=\"*\" to-ports=\"*\" /></cross-domain-policy>");
					writer.Flush();
					Reader.WriteByte(0);
					Close();
					return;
				}
				String[] request = line.Split(' ');
				if (request.Length != 3) goto SendError400AndClose;
				RequestMethod = request[0];
				String RequestAddress = request[1];
				switch (request[2]) {
					case "HTTP/1.0": HTTPVersion = 10; break;
					case "HTTP/1.1": HTTPVersion = 11; break;
					default: goto SendError505AndClose;
				}
				request = RequestAddress.Split(new Char[] { '?' });
				RequestPath = Uri.UnescapeDataString(request[0]);
				RequestQuery = request.Length > 1 ? request[1] : null;
				RequestHeaders = new List<HTTPHeader>();
				String headerName = null, headerValue = null;
				while (true) {
					line = ReadLine();
					if (line == null) goto SendError400AndClose;
					if (line.Length == 0) break;
					if (line[0] == ' ' || line[0] == '\t') {
						headerValue += line;
					} else {
						if (headerName != null) RequestHeaders.Add(new HTTPHeader(headerName, (headerValue ?? String.Empty).Trim()));
						request = line.Split(new Char[] { ':' }, 2, StringSplitOptions.None);
						if (request.Length != 2) goto SendError400AndClose;
						headerName = request[0];
						headerValue = request[1];
					}
				}
				if (headerName != null) RequestHeaders.Add(new HTTPHeader(headerName, (headerValue ?? String.Empty).Trim()));
				String connectionHeader = GetRequestHeader("Connection");
				if (HTTPVersion == 10) {
					KeepAlive = "Keep-Alive".Equals(connectionHeader, StringComparison.InvariantCultureIgnoreCase);
				} else {
					KeepAlive = String.IsNullOrEmpty(connectionHeader) || "Keep-Alive".Equals(connectionHeader, StringComparison.InvariantCultureIgnoreCase);
				}
				String acceptEncodingHeader = GetRequestHeader("Accept-Encoding");
				if (Server.AllowGzipCompression && acceptEncodingHeader != null) {
					String[] acceptEncodings = acceptEncodingHeader.Split(',');
					foreach (String encoding in acceptEncodings) if (encoding.Trim().Equals("gzip", StringComparison.InvariantCultureIgnoreCase)) AcceptGzipCompression = true;
				}
				if (TimeoutTimer != null) TimeoutTimer.Dispose();
				ResponseHeaders = new List<HTTPHeader>(Server.DefaultHeaders);
				State = HTTPConnectionState.ProcessingRequest;
				SendStatus(200);
				SendHeader("Date", DateTime.UtcNow.ToString("R"));
				if (KeepAlive && KeepAliveMaxRequests > 1) {
					SendHeader("Connection", "Keep-Alive");
					if (Server.RequestTimeout > 0) SendHeader("Keep-Alive", String.Format("timeout={0}, max={1}", Server.RequestTimeout, KeepAliveMaxRequests));
				} else {
					SendHeader("Connection", "Close");
				}
				IHTTPContentProvider content = Server.ContentProvider;
				if (content == null) {
					SendErrorResponse(404);
					return;
				}
				content.ServeRequest(this);
				if (!AsynchronousCompletion) EndResponseData();
			} catch (Exception ex) {
				Server.RaiseOnError(this, ex);
				switch (State) {
					case HTTPConnectionState.ProcessingRequest: goto SendError500AndClose;
					default:
						Close();
						break;
				}
			}
			return;

		SendError400AndClose:
			SendErrorAndClose(400);
			return;
		SendError500AndClose:
			SendErrorAndClose(500);
			return;
		SendError505AndClose:
			SendErrorAndClose(400);
			return;
		}

		public String GetRequestHeader(String name) {
			if (RequestHeaders == null) return null;
			foreach (HTTPHeader h in RequestHeaders) {
				if (name.Equals(h.Key, StringComparison.OrdinalIgnoreCase)) return h.Value;
			}
			return null;
		}
		public String[] GetRequestHeaders(String name) {
			if (RequestHeaders == null) return null;
			String[] items = new String[0];
			foreach (HTTPHeader h in RequestHeaders) {
				if (name.Equals(h.Key, StringComparison.OrdinalIgnoreCase)) ArrayUtil.Add(ref items, h.Value);
			}
			return items;
		}
		public IEnumerable<KeyValuePair<String, String>> GetRequestHeaders() {
			if (RequestHeaders == null) return null;
			return RequestHeaders;
		}

		private static String UnescapeUrlDataString(String text) {
			return Uri.UnescapeDataString(text.Replace('+', ' '));
		}
		private static KeyValuePair<String, String>[] DecodeUrlEncodedFields(String data) {
			List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
			foreach (String arg in data.Split('&')) {
				String[] parts = arg.Split(new Char[] { '=' }, 2);
				String key = UnescapeUrlDataString(parts[0]);
				String value = (parts.Length > 1) ? UnescapeUrlDataString(parts[1]) : String.Empty;
				list.Add(new KeyValuePair<string, string>(key, value));
			}
			return list.ToArray();
		}

		public String GetQueryParameter(String name) {
			foreach (KeyValuePair<String, String> kvp in GetQueryParameters()) if (kvp.Key == name) return kvp.Value;
			return null;
		}
		public String[] GetQueryParameters(String name) {
			List<String> list = new List<string>();
			foreach (KeyValuePair<String, String> kvp in GetQueryParameters()) if (kvp.Key == name) list.Add(kvp.Value);
			return list.ToArray();
		}
		public KeyValuePair<String, String>[] GetQueryParameters() {
			if (RequestQuery == null) return new KeyValuePair<String, String>[0];
			if (QueryParameters == null) QueryParameters = DecodeUrlEncodedFields(RequestQuery);
			return QueryParameters;
		}

		public String GetPostParameter(String name) {
			foreach (KeyValuePair<String, String> kvp in GetPostParameters()) if (kvp.Key == name) return kvp.Value;
			return null;
		}
		public String[] GetPostParameters(String name) {
			List<String> list = new List<string>();
			foreach (KeyValuePair<String, String> kvp in GetPostParameters()) if (kvp.Key == name) list.Add(kvp.Value);
			return list.ToArray();
		}
		public KeyValuePair<String, String>[] GetPostParameters() {
			if (PostParameters == null) {
				if (RequestMethod == "POST" && GetRequestHeader("Content-Type") == "application/x-www-form-urlencoded") {
					String data;
					using (StreamReader reader = new StreamReader(OpenRequestStream(), Encoding.UTF8)) data = reader.ReadToEnd();
					PostParameters = DecodeUrlEncodedFields(data);
				} else {
					PostParameters = new KeyValuePair<string, string>[0];
				}
			}
			return PostParameters;
		}

		public String GetCookie(String name) {
			foreach (KeyValuePair<String, String> kvp in GetCookies()) if (kvp.Key == name) return kvp.Value;
			return null;
		}
		public String[] GetCookies(String name) {
			List<String> list = new List<string>();
			foreach (KeyValuePair<String, String> kvp in GetCookies()) if (kvp.Key == name) list.Add(kvp.Value);
			return list.ToArray();
		}
		public KeyValuePair<String, String>[] GetCookies() {
			if (Cookies == null) {
				String cookie = GetRequestHeader("Cookie");
				List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
				if (cookie != null) {
					foreach (String part in cookie.Split(';', ',')) {
						String[] subparts = part.Split('=');
						String key = subparts[0].Trim(' ', '\t', '"');
						String value = (subparts.Length < 2) ? null : subparts[1].Trim(' ', '\t', '"');
						list.Add(new KeyValuePair<string, string>(key, value));
					}
				}
				Cookies = list.ToArray();
			}
			return Cookies;
		}

		public void SetCookie(String name, String value) {
			SendHeader("Set-Cookie", String.Format("{0}={1}", name, value));
		}
		public void SetCookie(String name, String value, DateTime expire) {
			SendHeader("Set-Cookie", String.Format("{0}={1}; Expires={2:R}", name, value, expire));
		}
		public void SetCookie(String name, String value, DateTime? expire, String path, String domain, Boolean secure, Boolean httponly) {
			StringBuilder sb = new StringBuilder();
			sb.Append(name);
			sb.Append("=");
			sb.Append(value);
			if (expire != null) sb.AppendFormat("; Expires={0:R}", expire.Value.ToUniversalTime());
			if (path != null) sb.AppendFormat("; Path={0}", path);
			if (domain != null) sb.AppendFormat("; Domain={0}", domain);
			if (secure) sb.Append("; Secure");
			if (httponly) sb.Append("; HttpOnly");
			SendHeader("Set-Cookie", sb.ToString());
		}

		public Stream OpenRequestStream() {
			if (RequestStream == null) RequestStream = new HTTPInputStream(this);
			return RequestStream;
		}

		private static String GetMessageForStatus(int code) {
			switch (code) {
				case 101: return "Switching Protocols";
				case 200: return "OK";
				case 301: return "Moved Permanently";
				case 302: return "Found";
				case 303: return "See Other";
				case 304: return "Not Modified";
				case 307: return "Temporary Redirect";
				case 400: return "Bad Request";
				case 401: return "Access denied";
				case 403: return "Forbidden";
				case 404: return "Not Found";
				case 500: return "Internal Server Error";
				case 505: return "HTTP Version Not Supported";
				default: return "Unknown Status";
			}
		}

		public void SendStatus(int code) {
			String message = GetMessageForStatus(code);
			SendStatus(code, message);
		}
		public void SendStatus(int code, String message) {
			if (State != HTTPConnectionState.ProcessingRequest) throw new InvalidOperationException();
			ResponseStatusCode = code;
			ResponseStatusInfo = message;
		}
		public void SendHeader(String name, String value) {
			if (State != HTTPConnectionState.ProcessingRequest) throw new InvalidOperationException();
			ResponseHeaders.Add(new HTTPHeader(name, value));
		}
		public void SetResponseHeader(String name, String value) {
			if (State != HTTPConnectionState.ProcessingRequest) throw new InvalidOperationException();
			ResponseHeaders.RemoveAll(delegate(HTTPHeader header) { return header.Key.Equals(name, StringComparison.OrdinalIgnoreCase); });
			if (value != null) ResponseHeaders.Add(new HTTPHeader(name, value));
		}
		public void SendErrorResponse(int state) {
			String message = GetMessageForStatus(state);
			try {
				SendStatus(state, message);
				SendHeader("Content-Type", "text/plain");
				WriteResponseData(Encoding.ASCII.GetBytes(String.Format("Error {0}: {1}", state, message)));
			} catch (Exception ex) {
				Server.RaiseOnError(this, ex);
			}
		}
		public Stream OpenResponseStream(HTTPResponseStreamMode mode) {
			if (ResponseStream != null) throw new InvalidOperationException("The response stream has already been opened");
			if (AcceptGzipCompression && AllowGzipCompression && (mode == HTTPResponseStreamMode.Buffered || mode == HTTPResponseStreamMode.Chunked || mode == HTTPResponseStreamMode.Hybrid)) {
				SendHeader("Content-Encoding", "gzip");
				return ResponseStream = new GZipStream(new HTTPOutputStream(this, mode), CompressionMode.Compress);
			}
			return ResponseStream = new HTTPOutputStream(this, mode);
		}
		public Stream OpenResponseStream(long length) {
			if (ResponseStream != null) throw new InvalidOperationException("The response stream has already been opened");
			if (length < 0) throw new ArgumentException("Response length can not be negative", "length");
			if (AcceptGzipCompression && AllowGzipCompression && length > 100 && length < 1024 * 256) return OpenResponseStream(HTTPResponseStreamMode.Buffered);
			return ResponseStream = new HTTPOutputStream(this, HTTPResponseStreamMode.Direct, length);
		}
		public void WriteResponseData(Byte[] buffer) {
			WriteResponseData(buffer, 0, buffer.Length);
		}
		public void WriteResponseData(Byte[] buffer, int offset, int count) {
			Stream stream = OpenResponseStream(count);
			stream.Write(buffer, offset, count);
			stream.Close();
		}
		public void WriteResponseData(Stream stream) {
			WriteResponseData(stream, stream.CanSeek ? stream.Length - stream.Position : -1);
		}
		public void WriteResponseData(Stream stream, long length) {
			Stream output;
			if (length == -1) output = OpenResponseStream(HTTPResponseStreamMode.Hybrid);
			else output = OpenResponseStream(length);
			CopyStream(stream, output, length, 0);
			output.Close();
		}
		private Stream BeginResponseData() {
			if (State == HTTPConnectionState.ProcessingRequest) {
				State = HTTPConnectionState.SendingHeaders;
				StreamWriter writer = new StreamWriter(Reader, Encoding.ASCII) { AutoFlush = false, NewLine = "\r\n" };
				writer.WriteLine("HTTP/{0}.{1} {2} {3}", HTTPVersion / 10, HTTPVersion % 10, ResponseStatusCode, ResponseStatusInfo);
				foreach (HTTPHeader header in ResponseHeaders) writer.WriteLine(header.Key + ": " + header.Value);
				writer.WriteLine();
				writer.Flush();
				State = HTTPConnectionState.SendingContent;
			}
			if (State != HTTPConnectionState.SendingContent) throw new InvalidOperationException("The response stream can not be opened in the current state");
			return Reader;
		}
		private void EndResponseData() {
			if (State == HTTPConnectionState.Completed || State == HTTPConnectionState.Closed) return;
			OpenRequestStream().Close();
			if (State != HTTPConnectionState.SendingContent) {
				if ((ResponseStatusCode >= 100 && ResponseStatusCode <= 199) || ResponseStatusCode == 204 || ResponseStatusCode == 304) {
					BeginResponseData();
				} else {
					WriteResponseData(new Byte[0]);
				}
			}
			State = HTTPConnectionState.Completed;
			if (KeepAlive && KeepAliveMaxRequests > 1) {
				State = HTTPConnectionState.Closed;
				new HTTPContext(Server, Reader, Socket, KeepAliveMaxRequests - 1, IsSecure);
			} else {
				Close();
			}
		}

		public Stream GetDirectStream() {
			if (State == HTTPConnectionState.Closed) throw new InvalidOperationException("The context has been closed");
			KeepAlive = false;
			BeginResponseData();
			State = HTTPConnectionState.Closed;
			return Reader.Buffered > 0 ? Reader : Reader.BaseStream;
		}

		private void SendErrorAndClose(int code) {
			try {
				if (State == HTTPConnectionState.Starting || State == HTTPConnectionState.ReceivingRequest) State = HTTPConnectionState.ProcessingRequest;
				if (State == HTTPConnectionState.ProcessingRequest) SendErrorResponse(code);
			} catch (IOException) {
			} catch (SocketException) {
			} catch (ObjectDisposedException) {
			} catch (Exception ex) {
				Server.RaiseOnError(this, ex);
			} finally {
				Close();
			}
		}
		private void Close() {
			if (State == HTTPConnectionState.Closed) return;
			State = HTTPConnectionState.Closed;
			Reader.Close();
		}

		public static long CopyStream(Stream input, Stream output, long length, int buffersize) {
			if (length == 0) return 0;
			if (buffersize <= 0) buffersize = 1024 * 10;
			if (length >= 0 && length < buffersize) buffersize = (int)length;
			Byte[] buffer = new Byte[buffersize];
			long total = 0;
			while (length != 0 && input.CanRead) {
				if (length > 0 && buffersize > length) buffersize = (int)length;
				int read = input.Read(buffer, 0, buffersize);
				if (read < 0) throw new EndOfStreamException("Read error on input stream");
				if (read == 0) break;
				output.Write(buffer, 0, read);
				total += read;
				if (length > 0) length -= read;
			}
			return total;
		}
	}

	public interface IHTTPContentProvider {
		void ServeRequest(HTTPContext context);
	}
	public delegate void HTTPContentProviderDelegate(HTTPContext context);
	public class HTTPContentProviderFunction : IHTTPContentProvider {
		public HTTPContentProviderDelegate Handler { get; private set; }
		public HTTPContentProviderFunction(HTTPContentProviderDelegate handler) {
			this.Handler = handler;
		}
		public void ServeRequest(HTTPContext context) {
			Handler(context);
		}
	}
	public class HTTPPathSelector : IHTTPContentProvider {
		private List<KeyValuePair<String, IHTTPContentProvider>> Prefixes;
		private StringComparison PrefixComparison;
		public HTTPPathSelector() : this(false) { }
		public HTTPPathSelector(Boolean caseSensitive) {
			Prefixes = new List<KeyValuePair<string, IHTTPContentProvider>>();
			PrefixComparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
		}
		public void AddPrefix(String prefix, IHTTPContentProvider contentProvider) {
			Prefixes.Add(new KeyValuePair<string, IHTTPContentProvider>(prefix, contentProvider));
			Prefixes.Sort(delegate(KeyValuePair<String, IHTTPContentProvider> a, KeyValuePair<String, IHTTPContentProvider> b) {
				return -String.CompareOrdinal(a.Key, b.Key);
			});
		}
		public void DeletePrefix(String prefix) {
			Prefixes.RemoveAll(delegate(KeyValuePair<string, IHTTPContentProvider> item) { return prefix.Equals(item.Key, PrefixComparison); });
		}
		public void ServeRequest(HTTPContext context) {
			KeyValuePair<string, IHTTPContentProvider> c = Prefixes.Find(delegate(KeyValuePair<string, IHTTPContentProvider> item) { return context.RequestPath.StartsWith(item.Key, PrefixComparison); });
			if (c.Value != null) {
				c.Value.ServeRequest(context);
			} else {
				context.SendErrorResponse(404);
			}
		}
	}
	public class HTTPStaticContent : IHTTPContentProvider {
		public ArraySegment<Byte> ContentBuffer { get; set; }
		public String ContentType { get; set; }
		public HTTPStaticContent() : this(new ArraySegment<Byte>()) { }
		public HTTPStaticContent(ArraySegment<Byte> content) : this(content, "application/octet-stream") { }
		public HTTPStaticContent(String content, String contentType) : this(Encoding.UTF8.GetBytes(content), contentType) { }
		public HTTPStaticContent(String contentType) : this(new ArraySegment<Byte>(), contentType) { }
		public HTTPStaticContent(Byte[] content, String contentType) : this(new ArraySegment<Byte>(content), contentType) { }
		public HTTPStaticContent(ArraySegment<Byte> content, String contentType) {
			this.ContentBuffer = content;
			this.ContentType = contentType;
		}
		public void SetContent(Byte[] bytes) { ContentBuffer = new ArraySegment<byte>(bytes); }
		public void SetContent(Byte[] bytes, int offset, int count) { ContentBuffer = new ArraySegment<byte>(bytes, offset, count); }
		public void SetContent(String content, Encoding encoding) { SetContent(encoding.GetBytes(content)); }
		public void SetContent(String content) { SetContent(content, Encoding.UTF8); }
		public void ServeRequest(HTTPContext context) {
			ArraySegment<Byte> content = ContentBuffer;
			if (content.Array == null) {
				context.SendErrorResponse(404);
				return;
			}
			String contentType = ContentType;
			context.SendStatus(200);
			if (contentType != null) context.SendHeader("Content-Type", contentType);
			context.WriteResponseData(content.Array, content.Offset, content.Count);
		}
	}
	public class HTTPFileProvider : IHTTPContentProvider {
		public String FileName { get; private set; }
		public String ContentType { get; private set; }
		public HTTPFileProvider(String fileName) : this(fileName, HTTPServer.GetMimeTypeForExtension(fileName) ?? "application/octet-stream") { }
		public HTTPFileProvider(String fileName, String contentType) {
			this.FileName = fileName;
			this.ContentType = contentType;
		}
		public void ServeRequest(HTTPContext context) {
			SendFile(context, FileName, ContentType);
		}
		public static void SendFile(HTTPContext context, String filename) {
			SendFile(context, filename, null);
		}
		public static void SendFile(HTTPContext context, String filename, String contentType) {
			if (!File.Exists(filename)) {
				context.SendErrorResponse(404);
				return;
			}
			String lastModified = File.GetLastWriteTimeUtc(filename).ToString("R");
			if (context.GetRequestHeader("If-Modified-Since") == lastModified) {
				context.SendStatus(304);
				return;
			}
			if (contentType == null) contentType = HTTPServer.GetMimeTypeForExtension(Path.GetExtension(filename));
			using (FileStream fs = File.OpenRead(filename)) {
				context.SendStatus(200);
				if (!String.IsNullOrEmpty(contentType)) context.SendHeader("Content-Type", contentType);
				context.SendHeader("Last-Modified", lastModified);
				context.WriteResponseData(fs);
			}
		}
	}
	public class HTTPUnTarchiveProvider : IHTTPContentProvider {
		public String TarFileName { get; private set; }
		public HTTPUnTarchiveProvider(String tarFile) {
			this.TarFileName = tarFile;
		}
		public void ServeRequest(HTTPContext context) {
			if (!File.Exists(TarFileName)) {
				context.SendErrorResponse(404);
				return;
			}
			String reqname1 = context.RequestPath;
			if (reqname1.StartsWith("/")) reqname1 = reqname1.Substring(1);
			String reqname2 = reqname1;
			if (reqname2.Length > 0 && !reqname2.EndsWith("/")) reqname2 += "/";
			reqname2 += "index.htm";
			foreach (TarchiveEntry file in new TarchiveReader(TarFileName)) {
				if (!file.IsFile) continue;
				if (!reqname1.Equals(file.Name, StringComparison.OrdinalIgnoreCase) && !reqname2.Equals(file.Name, StringComparison.OrdinalIgnoreCase)) continue;
				context.SendStatus(200);
				String ctype = HTTPServer.GetMimeTypeForExtension(Path.GetExtension(file.Name));
				if (ctype != null) context.SendHeader("Content-Type", ctype);
				using (Stream source = file.GetStream()) context.WriteResponseData(source);
				return;
			}
			context.SendErrorResponse(404);
		}
	}
}
