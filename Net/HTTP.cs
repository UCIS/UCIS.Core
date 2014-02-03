using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using UCIS.Net;
using UCIS.Util;
using HTTPHeader = System.Collections.Generic.KeyValuePair<string, string>;

namespace UCIS.Net.HTTP {
	public class HTTPServer : TCPServer.IModule, IDisposable {
		public IHTTPContentProvider ContentProvider { get; set; }
		public Boolean ServeFlashPolicyFile { get; set; }
		public X509Certificate SSLCertificate { get; set; }
		private Socket Listener = null;

		public HTTPServer() { }

		public void Listen(int port) {
			Listen(new IPEndPoint(IPAddress.Any, port));
		}

		public void Listen(EndPoint localep) {
			if (Listener != null) throw new InvalidOperationException("A listener exists");
			Listener = new Socket(localep.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			Listener.Bind(localep);
			Listener.Listen(5);
			Listener.BeginAccept(AcceptCallback, null);
		}

		private void AcceptCallback(IAsyncResult ar) {
			try {
				Socket socket = Listener.EndAccept(ar);
				if (SSLCertificate != null) {
					SslStream ssl = new SslStream(new NetworkStream(socket, true));
					ssl.BeginAuthenticateAsServer(SSLCertificate, SslAuthenticationCallback, new Object[] { socket, ssl });
				} else {
					new HTTPContext(this, socket);
				}
			} catch (Exception) { }
			try {
				Listener.BeginAccept(AcceptCallback, null);
			} catch (Exception) { }
		}

		private void SslAuthenticationCallback(IAsyncResult ar) {
			Object[] args = (Object[])ar.AsyncState;
			Socket socket = (Socket)args[0];
			SslStream ssl = (SslStream)args[1];
			try {
				ssl.EndAuthenticateAsServer(ar);
				new HTTPContext(this, ssl, socket);
			} catch (Exception) { }
		}

		public void Dispose() {
			if (Listener != null) Listener.Close();
		}

		public void HandleClient(Socket client) {
			new HTTPContext(this, client);
		}

		bool TCPServer.IModule.Accept(TCPStream stream) {
			new HTTPContext(this, stream);
			return false;
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
		public Boolean SuppressStandardHeaders { get; set; }
		public TCPStream TCPStream { get; private set; }

		private StreamWriter Writer;
		private PrebufferingStream Reader;
		private List<HTTPHeader> RequestHeaders;
		private HTTPConnectionState State = HTTPConnectionState.Starting;
		private KeyValuePair<String, String>[] QueryParameters = null, PostParameters = null;
		private HTTPOutputStream ResponseStream = null;
		private HTTPInputStream RequestStream = null;

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
						if (Context.State != HTTPConnectionState.ProcessingRequest && Context.State != HTTPConnectionState.SendingHeaders) throw new InvalidOperationException("The response stream can not be created in the current state");
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
				oldbuffer.WriteTo(this);
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
							/*if (MaxLength != -1 && MaxLength > BytesWritten) {
								long left = MaxLength - BytesWritten;
								Byte[] dummy = new Byte[Math.Min(left, 1024)];
								for (; left > 0; left -= dummy.Length) OutputStream.Write(dummy, 0, (int)Math.Min(left, dummy.Length));
							}*/
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
					if (MaxLength != -1 && MaxLength > BytesWritten) Context.Close();
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

		public HTTPContext(HTTPServer server, TCPStream stream) : this(server, stream, stream.Socket) { }
		public HTTPContext(HTTPServer server, Socket socket) : this(server, null, socket) { }
		public HTTPContext(HTTPServer server, Stream stream, Socket socket) {
			this.Server = server;
			this.Socket = socket;
			if (socket != null) {
				this.LocalEndPoint = socket.LocalEndPoint;
				this.RemoteEndPoint = socket.RemoteEndPoint;
				if (socket.ProtocolType == ProtocolType.Tcp) socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
				if (stream == null) stream = new NetworkStream(socket, true);
			}
			Init(stream);
		}

		private void Init(Stream Stream) {
			Writer = new StreamWriter(Stream, Encoding.ASCII);
			Writer.NewLine = "\r\n";
			Writer.AutoFlush = true;
			Reader = new PrebufferingStream(Stream);
			Reader.BeginPrebuffering(PrebufferCallback, null);
		}

		private static String ReadLine(Stream stream) {
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
					Writer.WriteLine("<cross-domain-policy><allow-access-from domain=\"*\" to-ports=\"*\" /></cross-domain-policy>");
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
				while (true) {
					line = ReadLine();
					if (line == null) goto SendError400AndClose;
					if (line.Length == 0) break;
					request = line.Split(new String[] { ": " }, 2, StringSplitOptions.None);
					if (request.Length != 2) goto SendError400AndClose;
					RequestHeaders.Add(new HTTPHeader(request[0], request[1]));
				}
				IHTTPContentProvider content = Server.ContentProvider;
				if (content == null) goto SendError500AndClose;
				State = HTTPConnectionState.ProcessingRequest;
				content.ServeRequest(this);
				Close();
			} catch (Exception ex) {
				Console.Error.WriteLine(ex);
				switch (State) {
					case HTTPConnectionState.ProcessingRequest: goto SendError500AndClose;
					default:
						Close();
						break;
				}
			}
			return;

		SendError400AndClose:
			State = HTTPConnectionState.ProcessingRequest;
			SendErrorAndClose(400);
			return;
		SendError500AndClose:
			State = HTTPConnectionState.ProcessingRequest;
			SendErrorAndClose(500);
			return;
		SendError505AndClose:
			State = HTTPConnectionState.ProcessingRequest;
			SendErrorAndClose(400);
			return;
		}

		public String GetRequestHeader(String name) {
			if (State != HTTPConnectionState.ProcessingRequest && State != HTTPConnectionState.SendingHeaders && State != HTTPConnectionState.SendingContent) throw new InvalidOperationException();
			foreach (HTTPHeader h in RequestHeaders) {
				if (name.Equals(h.Key, StringComparison.OrdinalIgnoreCase)) return h.Value;
			}
			return null;
		}
		public String[] GetRequestHeaders(String name) {
			if (State != HTTPConnectionState.ProcessingRequest && State != HTTPConnectionState.SendingHeaders && State != HTTPConnectionState.SendingContent) throw new InvalidOperationException();
			String[] items = new String[0];
			foreach (HTTPHeader h in RequestHeaders) {
				if (name.Equals(h.Key, StringComparison.OrdinalIgnoreCase)) ArrayUtil.Add(ref items, h.Value);
			}
			return items;
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
			StringBuilder sb = new StringBuilder();
			sb.Append("HTTP/");
			switch (HTTPVersion) {
				case 10: sb.Append("1.0"); break;
				case 11: sb.Append("1.1"); break;
				default: sb.Append("1.0"); break;
			}
			sb.Append(" ");
			sb.Append(code);
			sb.Append(" ");
			sb.Append(message);
			Writer.WriteLine(sb.ToString());
			State = HTTPConnectionState.SendingHeaders;
			if (!SuppressStandardHeaders) {
				SendHeader("Expires", "Expires: Sun, 1 Jan 2000 00:00:00 GMT");
				SendHeader("Cache-Control", "no-store, no-cache, must-revalidate");
				SendHeader("Cache-Control", "post-check=0, pre-check=0");
				SendHeader("Pragma", "no-cache");
				SendHeader("Server", "UCIS Embedded Webserver");
				SendHeader("Connection", "Close");
			}
		}
		public void SendHeader(String name, String value) {
			if (State == HTTPConnectionState.ProcessingRequest) SendStatus(200);
			if (State != HTTPConnectionState.SendingHeaders) throw new InvalidOperationException();
			Writer.WriteLine(name + ": " + value);
		}
		public void SendErrorResponse(int state) {
			String message = GetMessageForStatus(state);
			try {
				SendStatus(state, message);
				SendHeader("Content-Type", "text/plain");
				WriteResponseData(Encoding.ASCII.GetBytes(String.Format("Error {0}: {1}", state, message)));
			} catch (Exception ex) {
				Console.Error.WriteLine(ex);
			}
		}
		public Stream OpenResponseStream(HTTPResponseStreamMode mode) {
			if (ResponseStream != null) throw new InvalidOperationException("The response stream has already been opened");
			return ResponseStream = new HTTPOutputStream(this, mode);
		}
		public Stream OpenResponseStream(long length) {
			if (ResponseStream != null) throw new InvalidOperationException("The response stream has already been opened");
			if (length < 0) throw new ArgumentException("Response length can not be negative", "length");
			return ResponseStream = new HTTPOutputStream(this, HTTPResponseStreamMode.Direct, length);
		}
		public void WriteResponseData(Byte[] buffer) {
			WriteResponseData(buffer, 0, buffer.Length);
		}
		public void WriteResponseData(Byte[] buffer, int offset, int count) {
			if (offset < 0 || count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException("buffer", "Offset and count arguments exceed the buffer dimensions");
			SendHeader("Content-Length", count.ToString());
			Stream stream = BeginResponseData();
			stream.Write(buffer, offset, count);
			EndResponseData();
		}
		private Stream BeginResponseData() {
			if (State == HTTPConnectionState.ProcessingRequest) SendStatus(200);
			if (State == HTTPConnectionState.SendingHeaders) {
				Writer.WriteLine();
				State = HTTPConnectionState.SendingContent;
			}
			if (State != HTTPConnectionState.SendingContent) throw new InvalidOperationException("The response stream can not be opened in the current state");
			return Reader;
		}
		private void EndResponseData() {
			if (State == HTTPConnectionState.Completed || State == HTTPConnectionState.Closed) return;
			OpenRequestStream().Close();
			if (State != HTTPConnectionState.SendingContent) WriteResponseData(new Byte[0]);
			State = HTTPConnectionState.Completed;
		}

		public Stream GetDirectStream() {
			if (State == HTTPConnectionState.Closed) throw new InvalidOperationException("The context has been closed");
			BeginResponseData();
			State = HTTPConnectionState.Closed;
			return Reader;
		}

		private void SendErrorAndClose(int code) {
			SendErrorResponse(code);
			Close();
		}
		private void Close() {
			if (State == HTTPConnectionState.Closed) return;
			Reader.Close();
			State = HTTPConnectionState.Closed;
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
		public HTTPFileProvider(String fileName) : this(fileName, "application/octet-stream") { }
		public HTTPFileProvider(String fileName, String contentType) {
			this.FileName = fileName;
			this.ContentType = contentType;
		}
		public void ServeRequest(HTTPContext context) {
			if (File.Exists(FileName)) {
				using (FileStream fs = File.OpenRead(FileName)) {
					context.SendStatus(200);
					context.SendHeader("Content-Type", ContentType);
					long left = fs.Length;
					Stream response = context.OpenResponseStream(fs.Length);
					byte[] buffer = new byte[1024 * 10];
					while (fs.CanRead) {
						int len = fs.Read(buffer, 0, buffer.Length);
						if (len <= 0) break;
						left -= len;
						response.Write(buffer, 0, len);
					}
					response.Close();
				}
			} else {
				context.SendErrorResponse(404);
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
				String ctype = null;
				switch (Path.GetExtension(file.Name).ToLowerInvariant()) {
					case ".txt": ctype = "text/plain"; break;
					case ".htm":
					case ".html": ctype = "text/html"; break;
					case ".css": ctype = "text/css"; break;
					case ".js": ctype = "application/x-javascript"; break;
					case ".png": ctype = "image/png"; break;
					case ".jpg":
					case ".jpeg": ctype = "image/jpeg"; break;
					case ".gif": ctype = "image/gif"; break;
					case ".ico": ctype = "image/x-icon"; break;
				}
				if (ctype != null) context.SendHeader("Content-Type", ctype);
				using (Stream response = context.OpenResponseStream(file.Size), source = file.GetStream()) {
					byte[] buffer = new byte[Math.Min(source.Length, 1024 * 10)];
					while (source.CanRead) {
						int len = source.Read(buffer, 0, buffer.Length);
						if (len <= 0) break;
						response.Write(buffer, 0, len);
					}
				}
				return;
			}
			context.SendErrorResponse(404);
		}
	}
}
