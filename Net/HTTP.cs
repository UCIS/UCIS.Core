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
		private ISocket[] Listeners = new ISocket[0];
		public event EventHandler<HTTPServerEventArgs> ConnectionAccepted;
		public event EventHandler<HTTPServerEventArgs> ConnectionClosed;
		public event EventHandler<HTTPServerEventArgs> RequestStarted;
		public event EventHandler<HTTPServerEventArgs> RequestReceived;
		public event EventHandler<HTTPServerEventArgs> RequestFinished;
		private volatile int openConnections = 0;
		public int ActiveConnections { get { return openConnections; } }
		public int MaximumConnections { get; set; }
		public Boolean UseSynchronousAccept { get; set; }

		public HTTPServer() {
			DefaultHeaders = new List<KeyValuePair<String, String>>() {
				new KeyValuePair<String, String>("Server", "UCIS Embedded Webserver"),
			};
			AllowGzipCompression = true;
			RequestTimeout = 30;
		}

		public void Listen(int port) {
			Listen(new IPEndPoint(IPAddress.Any, port));
		}

		public void Listen(EndPoint localep) {
			ISocket listener = new FWSocket(localep.AddressFamily, SocketType.Stream, ProtocolType.Unspecified);
			Listen(listener, localep);
		}

		public void Listen(ISocket socket, EndPoint localep) {
			socket.Bind(localep);
			socket.Listen(128);
			ArrayUtil.Add(ref Listeners, socket);
			if (UseSynchronousAccept) {
				socket.Blocking = true;
				(new Thread(AcceptWorker)).Start(socket);
			} else {
				socket.Blocking = false;
				socket.BeginAccept(AcceptCallback, socket);
			}
		}

		private void AcceptCallback(IAsyncResult ar) {
			ISocket listener = (ISocket)ar.AsyncState;
			ISocket socket = null;
			try {
				socket = listener.EndAccept(ar);
				if (MaximumConnections > 0 && openConnections >= MaximumConnections) {
					socket.Close();
				} else {
					HandleClient(socket);
				}
			} catch (Exception ex) {
				RaiseOnError(this, ex);
				if (socket != null) socket.Close();
			}
			try {
				listener.BeginAccept(AcceptCallback, listener);
			} catch (Exception ex) {
				RaiseOnError(this, ex);
			}
		}

		private void AcceptWorker(Object state) {
			ISocket listener = (ISocket)state;
			while (true) {
				ISocket socket;
				while (MaximumConnections > 0 && openConnections >= MaximumConnections) Thread.Sleep(10);
				try {
					socket = listener.Accept();
				} catch (ObjectDisposedException) {
					break;
				} catch (Exception ex) {
					RaiseOnError(this, ex);
					continue;
				}
				try {
					HandleClient(socket);
				} catch (Exception ex) {
					RaiseOnError(this, ex);
					socket.Close();
				}
			}
		}

		private void SslAuthenticationCallback(IAsyncResult ar) {
			Object[] args = (Object[])ar.AsyncState;
			ISocket socket = (ISocket)args[0];
			SslStream ssl = (SslStream)args[1];
			Stream streamwrapper = (Stream)args[2];
			IDisposable timeout = (IDisposable)args[3];
			try {
				ssl.EndAuthenticateAsServer(ar);
				timeout.Dispose();
				new HTTPContext(this, ssl, socket, -1, true);
			} catch (Exception ex) {
				RaiseOnError(this, ex);
				if (socket != null) RaiseEvent(ConnectionClosed, null, socket);
				streamwrapper.Close();
				if (socket != null) socket.Close();
			}
		}

		public void Dispose() {
			foreach (ISocket socket in Listeners) socket.Close();
			Listeners = new ISocket[0];
		}

		public void HandleClient(Socket socket, Stream streamwrapper) {
			HandleClient((ISocket)new FWSocketWrapper(socket), streamwrapper);
		}

		public void HandleClient(ISocket socket, Stream streamwrapper) {
			RaiseEvent(ConnectionAccepted, null, socket);
			Interlocked.Increment(ref openConnections);
			ThreadPool.QueueUserWorkItem(HandleClientInternal, new Object[] { socket, streamwrapper });
		}

		private void HandleClientInternal(Object state) {
			Object[] args = (Object[])state;
			ISocket socket = (ISocket)args[0];
			Stream streamwrapper = args[1] as Stream;
			try {
				if (socket.AddressFamily == AddressFamily.InterNetwork || socket.AddressFamily == AddressFamily.InterNetworkV6) {
					socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
				}
				if (streamwrapper == null) {
					socket.Blocking = true;
					if (socket is FWSocket) streamwrapper = new NetworkStream((FWSocket)socket, true);
					else if (socket is FWSocketWrapper) streamwrapper = new NetworkStream(((FWSocketWrapper)socket).Socket, true);
					else streamwrapper = new SocketStream(socket);
				}
				try {
					if (SSLCertificate != null) {
						SslStream ssl = new SslStream(streamwrapper);
						ssl.BeginAuthenticateAsServer(SSLCertificate, SslAuthenticationCallback, new Object[] { socket, ssl, streamwrapper, new TimedDisposer(ssl, 10000) });
					} else {
						new HTTPContext(this, streamwrapper, socket, -1, false);
					}
				} catch {
					streamwrapper.Close();
					throw;
				}
			} catch (Exception ex) {
				RaiseOnError(this, ex);
				socket.Close();
				Interlocked.Decrement(ref openConnections);
				RaiseEvent(ConnectionClosed, null, socket);
			}
		}

		public void HandleClient(Socket client) {
			HandleClient(new FWSocketWrapper(client));
		}

		public void HandleClient(ISocket client) {
			HandleClient(client, null);
		}

		bool TCPServer.IModule.Accept(TCPStream stream) {
			HandleClient(new FWSocketWrapper(stream.Socket), stream);
			return false;
		}

		internal void RaiseOnError(Object sender, Exception error) {
			System.Diagnostics.Debug.WriteLine(error);
			ErrorEventHandler eh = OnError;
			if (eh != null) eh(sender, new ErrorEventArgs(error));
		}

		public static String GetMimeTypeForExtension(String extension) {
			if (String.IsNullOrEmpty(extension)) return null;
			int i = extension.LastIndexOf('.');
			if (i != -1) extension = extension.Substring(i + 1);
			switch (extension.ToLowerInvariant()) {
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

		private void RaiseEvent(EventHandler<HTTPServerEventArgs> eh, HTTPContext ctx, ISocket socket) {
			if (eh != null) eh((Object)ctx ?? this, new HTTPServerEventArgs(this, ctx, socket));
		}

		internal void RaiseRequestStarted(HTTPContext ctx) {
			RaiseEvent(RequestStarted, ctx, ctx.Socket);
		}

		internal void RaiseRequestReceived(HTTPContext ctx) {
			RaiseEvent(RequestReceived, ctx, ctx.Socket);
		}

		internal void RaiseRequestFinished(HTTPContext ctx) {
			RaiseEvent(RequestFinished, ctx, ctx.Socket);
		}

		internal void RaiseConnectionClosed(HTTPContext ctx) {
			Interlocked.Decrement(ref openConnections);
			RaiseEvent(ConnectionClosed, ctx, ctx.Socket);
		}
	}

	public class HTTPServerEventArgs : EventArgs {
		public HTTPServer Server { get; private set; }
		public ISocket Socket { get; private set; }
		public IHTTPContext Context { get; private set; }

		public HTTPServerEventArgs(HTTPServer server, IHTTPContext context, ISocket socket) {
			this.Server = server;
			this.Socket = socket;
			this.Context = context;
		}
	}

	public enum HTTPResponseStreamMode {
		None = -1,
		Direct = 0,
		Buffered = 1,
		Chunked = 2,
		Hybrid = 3,
	}

	public class HTTPContext : IHTTPContext {
		public HTTPServer Server { get; private set; }
		public EndPoint LocalEndPoint { get; private set; }
		public EndPoint RemoteEndPoint { get; private set; }

		public String RequestMethod { get; private set; }
		public String RequestPath { get; private set; }
		public String RequestQuery { get; private set; }
		public int HTTPVersion { get; set; }

		public ISocket Socket { get; private set; }
		public Boolean AsynchronousCompletion { get; set; }
		public Boolean KeepAlive { get; set; }
		public TCPStream TCPStream { get { return Reader.BaseStream as TCPStream; } }
		public Boolean IsSecure { get; private set; }

		private PrebufferingStream Reader;
		private List<HTTPHeader> ResponseHeaders = null;
		private HTTPConnectionState State = HTTPConnectionState.Starting;
		private Stream ResponseStream = null;
		private Boolean AcceptGzipCompression = false;
		private int KeepAliveMaxRequests = 100;
		private Timer TimeoutTimer = null;
		public Boolean AllowGzipCompression { get; set; }
		public int ResponseStatusCode { get; private set; }
		private String ResponseStatusInfo;
		public HTTPRequestEnvironment Environment { get; private set; }
		public HTTPRequestHeaderCollection RequestHeaders { get; private set; }
		public HTTPRequestBody RequestBody { get; private set; }
		public HTTPResponse Response { get; private set; }
		private HTTPRequestCookieCollection cookies = null;
		private HTTPQueryParametersCollection queryparameters = null;

		private enum HTTPConnectionState {
			Starting = 0,
			ReceivingRequest = 1,
			ProcessingRequest = 2,
			SendingHeaders = 3,
			SendingContent = 4,
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
						if (MaxLength != -1) Context.SetResponseHeader("Content-Length", MaxLength.ToString());
						OutputStream = Context.BeginResponseData();
						break;
					case HTTPResponseStreamMode.Chunked:
						Context.SetResponseHeader("Transfer-Encoding", "chunked");
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
				public CompletedAsyncResult(AsyncCallback callback, Object state)
					: base(callback, state) {
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
				get { return Context.State != HTTPConnectionState.Closed && (OutputStream == null || OutputStream.CanWrite); }
			}
			protected override void Dispose(bool disposing) {
				base.Dispose(disposing);
				if (disposing) {
					try {
						switch (Mode) {
							case HTTPResponseStreamMode.Direct:
								if (MaxLength == -1 || (MaxLength != -1 && MaxLength > BytesWritten)) Context.KeepAlive = false;
								Mode = HTTPResponseStreamMode.None;
								break;
							case HTTPResponseStreamMode.Chunked:
								WriteChunked(null, 0, 0);
								Mode = HTTPResponseStreamMode.None;
								break;
							case HTTPResponseStreamMode.Buffered:
							case HTTPResponseStreamMode.Hybrid:
								long length = (Buffer == null) ? 0 : Buffer.Length;
								Context.SetResponseHeader("Content-Length", length.ToString());
								OutputStream = Context.BeginResponseData();
								if (Buffer != null) Buffer.WriteTo(OutputStream);
								Buffer = null;
								Mode = HTTPResponseStreamMode.None;
								break;
						}
						Context.EndResponseData();
					} catch {
						if (Context.AsynchronousCompletion) Context.Close();
						throw;
					}
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
				String TransferEncoding = context.RequestHeaders["Transfer-Encoding"];
				String ContentLength = context.RequestHeaders["Content-Length"];
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
				if (read > 0) {
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
							if (length.Length == 0) length = ReadLine(InputStream);
							if (!long.TryParse(length, System.Globalization.NumberStyles.HexNumber, null, out BytesLeft)) BytesLeft = 0;
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
					int read = InputStream.EndRead(asyncResult);
					if (read > 0) {
						BytesRead += read;
						BytesLeft -= read;
					}
					return read;
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
				//get { return (BytesLeft > 0 || Mode == HTTPResponseStreamMode.Chunked) && InputStream.CanRead; }
				get { return InputStream.CanRead; }
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

		internal HTTPContext(HTTPServer server, Stream stream, ISocket socket, int maxrequests, Boolean issecure) {
			if (ReferenceEquals(server, null)) throw new ArgumentNullException("server");
			if (ReferenceEquals(socket, null) && ReferenceEquals(stream, null)) throw new ArgumentNullException("stream");
			this.Server = server;
			this.Socket = socket;
			this.IsSecure = issecure;
			if (maxrequests != -1) this.KeepAliveMaxRequests = maxrequests;
			this.AllowGzipCompression = true;
			if (socket != null) {
				this.LocalEndPoint = socket.LocalEndPoint;
				if (socket.AddressFamily == AddressFamily.InterNetwork) this.RemoteEndPoint = socket.RemoteEndPoint;
				if (stream == null) {
					socket.Blocking = true;
					if (socket is Socket) stream = new NetworkStream((Socket)socket, true);
					else if (socket is FWSocketWrapper) stream = new NetworkStream(((FWSocketWrapper)socket).Socket, true);
					else if (stream == null) stream = new SocketStream(socket, true);
				}
			}
			Reader = stream as PrebufferingStream ?? new PrebufferingStream(stream);
			this.HTTPVersion = 10;
			if (server.RequestTimeout > 0) TimeoutTimer = new Timer(TimeoutCallback, null, server.RequestTimeout * 1000 + 1000, Timeout.Infinite);
			Environment = new HTTPRequestEnvironment();
			ResponseHeaders = new List<HTTPHeader>(Server.DefaultHeaders);
			server.RaiseRequestStarted(this);
			BeginReadLine(Reader).AddCallback(InvokeReadRequestLineCallback);
		}

		private void InvokeReadRequestLineCallback(AsyncResult<String> ar) {
			//Break a possibly recursive callback
			if (ar.CompletedSynchronously) ThreadPool.QueueUserWorkItem((s) => ReadRequestLineCallback(ar));
			else ReadRequestLineCallback(ar);
		}

		public override string ToString() {
			String s = LocalEndPoint.ToString() + " <=> " + RemoteEndPoint.ToString() + " - " + State.ToString();
			if (State > HTTPConnectionState.ReceivingRequest) {
				s += " - " + RequestMethod + " " + RequestPath;
				if (RequestQuery != null) s += "?" + RequestQuery;
			}
			return s;
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
				if (s.Length > 16384) throw new InvalidDataException("Request line too long");
			}
			return s.ToString();
		}
		private String ReadLine() {
			return ReadLine(Reader);
		}

		public static AsyncResult<String> BeginReadLine(PrebufferingStream stream) {
			String ret;
			if (TryReadString(stream, out ret)) return ret;
			AsyncResultSource<String> ar = new AsyncResultSource<String>();
			stream.BeginPrebuffering(stream.Buffered + 1, ReadLineCallback, new Object[] { stream, ar, stream.Buffered });
			return ar;
		}
		private static void ReadLineCallback(IAsyncResult ar) {
			Object[] state = (Object[])ar.AsyncState;
			PrebufferingStream stream = (PrebufferingStream)state[0];
			AsyncResultSource<String> rar = (AsyncResultSource<String>)state[1];
			int last = (int)state[2];
			try {
				int read = stream.EndPrebuffering(ar);
				String ret;
				if (TryReadString(stream, out ret)) rar.SetCompleted(false, ret);
				else if (last == read) rar.SetCompleted(false, (String)null);
				else stream.BeginPrebuffering(read + 1, ReadLineCallback, new Object[] { stream, rar, read });
			} catch (Exception ex) {
				rar.SetCompleted(false, ex);
			}
		}
		public static AsyncResult BeginReadLines(PrebufferingStream stream, Predicate<String> callback) {
			String str;
			while (TryReadString(stream, out str)) if (!callback(str)) return AsyncResult.CreateCompleted();
			AsyncResultSource ar = new AsyncResultSource();
			stream.BeginPrebuffering(stream.Buffered + 1, ReadLinesCallback, new Object[] { stream, ar, stream.Buffered, callback });
			return ar;
		}
		private static void ReadLinesCallback(IAsyncResult ar) {
			Object[] state = (Object[])ar.AsyncState;
			PrebufferingStream stream = (PrebufferingStream)state[0];
			AsyncResultSource rar = (AsyncResultSource)state[1];
			int last = (int)state[2];
			Predicate<String> callback = (Predicate<String>)state[3];
			try {
				int read = stream.EndPrebuffering(ar);
				String str;
				while (TryReadString(stream, out str)) if (!callback(str)) { rar.SetCompleted(false, null); return; }
				if (last == read) rar.SetCompleted(false, null);
				else stream.BeginPrebuffering(read + 1, ReadLineCallback, new Object[] { stream, rar, read, callback });
			} catch (Exception ex) {
				rar.SetCompleted(false, ex);
			}
		}
		private static Boolean TryReadString(PrebufferingStream stream, out String ret) {
			Byte[] buffer = new Byte[16384];
			int len = stream.TryPeek(buffer, 0, buffer.Length);
			for (int i = 0; i < len; i++) {
				if (buffer[i] == 10 || buffer[i] == 0) {
					int skip = i;
					if (i > 0 && buffer[i - 1] == 13) i--;
					ret = Encoding.ASCII.GetString(buffer, 0, i);
					stream.Skip(skip + 1);
					return true;
				}
			}
			if (len == buffer.Length) throw new InvalidDataException("Request line too long");
			ret = null;
			return false;
		}

		private void TimeoutCallback(Object state) {
			if (State == HTTPConnectionState.Starting || State == HTTPConnectionState.ReceivingRequest) Close();
		}

		private void ReadRequestLineCallback(AsyncResult<String> ar) {
			lock (this) {
				if (State == HTTPConnectionState.Closed) return;
				if (State != HTTPConnectionState.Starting) return; //this should not happen
				State = HTTPConnectionState.ReceivingRequest;
			}
			try {
				String line = ar.Result;
				if (line == null) {
					Close();
					return;
				}
				if (Server.ServeFlashPolicyFile && line.Length > 0 && line[0] == '<') { //<policy-file-request/>
					StreamWriter writer = new StreamWriter(Reader, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = false };
					writer.WriteLine("<cross-domain-policy><allow-access-from domain=\"*\" to-ports=\"*\" /></cross-domain-policy>");
					writer.Flush();
					Reader.WriteByte(0);
					Close();
					return;
				}
				String[] request = line.Split(' ');
				if (request.Length != 3) { SendErrorAndClose(400); return; }
				RequestMethod = request[0].ToUpperInvariant();
				String RequestAddress = request[1];
				switch (request[2]) {
					case "HTTP/1.0": HTTPVersion = 10; break;
					case "HTTP/1.1": HTTPVersion = 11; break;
					default: SendErrorAndClose(400); return;
				}
				request = RequestAddress.Split(new Char[] { '?' });
				RequestPath = Uri.UnescapeDataString(request[0]);
				RequestQuery = request.Length > 1 ? request[1] : null;
				HTTPRequestHeaderCollection.BeginReadFromStream(Reader).AddCallback(InvokeReadRequestHeadersCallback);
			} catch (Exception ex) {
				Server.RaiseOnError(this, ex);
				switch (State) {
					case HTTPConnectionState.ProcessingRequest:
						SendErrorAndClose(500);
						return;
					default:
						Close();
						break;
				}
			}
		}

		private void InvokeReadRequestHeadersCallback(AsyncResult<HTTPRequestHeaderCollection> ar) {
			ThreadPool.QueueUserWorkItem((s) => ReadRequestHeadersCallback(ar), null);
		}

		private void ReadRequestHeadersCallback(AsyncResult<HTTPRequestHeaderCollection> ar) {
			try {
				RequestHeaders = ar.Result;
				if (RequestHeaders == null) { SendErrorAndClose(400); return; }
				String connectionHeader = RequestHeaders["Connection"];
				if (HTTPVersion == 10) {
					KeepAlive = "Keep-Alive".Equals(connectionHeader, StringComparison.InvariantCultureIgnoreCase);
				} else {
					KeepAlive = String.IsNullOrEmpty(connectionHeader) || "Keep-Alive".Equals(connectionHeader, StringComparison.InvariantCultureIgnoreCase);
				}
				String acceptEncodingHeader = RequestHeaders["Accept-Encoding"];
				if (Server.AllowGzipCompression && acceptEncodingHeader != null) {
					String[] acceptEncodings = acceptEncodingHeader.Split(',');
					foreach (String encoding in acceptEncodings) if (encoding.Trim().Equals("gzip", StringComparison.InvariantCultureIgnoreCase)) AcceptGzipCompression = true;
				}
				if (TimeoutTimer != null) TimeoutTimer.Dispose();
				lock (this) {
					if (State != HTTPConnectionState.ReceivingRequest) throw new InvalidOperationException("Unexpected request state");
					State = HTTPConnectionState.ProcessingRequest;
				}
				Response = new HTTPResponse(this);
				RequestBody = new HTTPRequestBody(this, new HTTPInputStream(this));
				Response.SendStatus(200);
				SetResponseHeader("Date", DateTime.UtcNow.ToString("R"));
				if (KeepAlive && KeepAliveMaxRequests > 1) {
					SetResponseHeader("Connection", "Keep-Alive");
					if (Server.RequestTimeout > 0) SetResponseHeader("Keep-Alive", String.Format("timeout={0}, max={1}", Server.RequestTimeout, KeepAliveMaxRequests));
				} else {
					SetResponseHeader("Connection", "Close");
				}
				Server.RaiseRequestReceived(this);
				IHTTPContentProvider content = Server.ContentProvider;
				if (content == null) {
					SendErrorResponse(404);
					return;
				}
				content.ServeRequest(this);
				if (!AsynchronousCompletion) {
					if (ResponseStream != null) ResponseStream.Close();
					EndResponseData();
				}
			} catch (Exception ex) {
				Server.RaiseOnError(this, ex);
				switch (State) {
					case HTTPConnectionState.ProcessingRequest:
						SendErrorAndClose(500);
						return;
					default:
						Close();
						break;
				}
			}
		}

		private static String UnescapeUrlDataString(String text) {
			return Uri.UnescapeDataString(text.Replace('+', ' '));
		}
		internal static KeyValuePair<String, String>[] DecodeUrlEncodedFields(String data) {
			if (data == null) return new KeyValuePair<string, string>[0];
			List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
			foreach (String arg in data.Split('&')) {
				String[] parts = arg.Split(new Char[] { '=' }, 2);
				String key = UnescapeUrlDataString(parts[0]);
				String value = (parts.Length > 1) ? UnescapeUrlDataString(parts[1]) : String.Empty;
				list.Add(new KeyValuePair<string, string>(key, value));
			}
			return list.ToArray();
		}

		public HTTPRequestCookieCollection Cookies {
			get {
				if (cookies == null) cookies = new HTTPRequestCookieCollection(RequestHeaders.GetAll("Cookie"));
				return cookies;
			}
		}
		public HTTPQueryParametersCollection QueryParameters {
			get {
				if (queryparameters == null) queryparameters = new HTTPQueryParametersCollection(RequestQuery);
				return queryparameters;
			}
		}

		internal static String GetMessageForStatus(int code) {
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

		internal void SendStatus(int code, String message) {
			if (State != HTTPConnectionState.ProcessingRequest) throw new InvalidOperationException();
			ResponseStatusCode = code;
			ResponseStatusInfo = message;
		}
		internal void SendHeader(String name, String value) {
			if (State != HTTPConnectionState.ProcessingRequest) throw new InvalidOperationException();
			ResponseHeaders.Add(new HTTPHeader(name, value));
		}
		internal void SetResponseHeader(String name, String value) {
			if (State != HTTPConnectionState.ProcessingRequest) throw new InvalidOperationException();
			ResponseHeaders.RemoveAll(delegate(HTTPHeader header) { return header.Key.Equals(name, StringComparison.OrdinalIgnoreCase); });
			if (value != null) ResponseHeaders.Add(new HTTPHeader(name, value));
		}
		private void SendErrorResponse(int state) {
			try {
				if (Response != null) Response.SendErrorResponse(state);
			} catch (Exception ex) {
				Server.RaiseOnError(this, ex);
			}
		}
		internal Stream OpenResponseStream(HTTPResponseStreamMode mode) {
			if (ResponseStream != null) throw new InvalidOperationException("The response stream has already been opened");
			if (AcceptGzipCompression && AllowGzipCompression && (mode == HTTPResponseStreamMode.Buffered || mode == HTTPResponseStreamMode.Chunked || mode == HTTPResponseStreamMode.Hybrid)) {
				SetResponseHeader("Content-Encoding", "gzip");
				return ResponseStream = new GZipStream(new HTTPOutputStream(this, mode), CompressionMode.Compress);
			}
			return ResponseStream = new HTTPOutputStream(this, mode);
		}
		internal Stream OpenResponseStream(long length) {
			if (ResponseStream != null) throw new InvalidOperationException("The response stream has already been opened");
			if (length < 0) throw new ArgumentException("Response length can not be negative", "length");
			if (AcceptGzipCompression && AllowGzipCompression && length > 100 && length < 1024 * 256) return OpenResponseStream(HTTPResponseStreamMode.Buffered);
			return ResponseStream = new HTTPOutputStream(this, HTTPResponseStreamMode.Direct, length);
		}
		private Stream BeginResponseData() {
			Boolean sendHeaders = false;
			lock (this) {
				if (State == HTTPConnectionState.ProcessingRequest) {
					sendHeaders = true;
					State = HTTPConnectionState.SendingHeaders;
				}
			}
			if (sendHeaders) {
				StreamWriter writer = new StreamWriter(Reader, Encoding.ASCII) { AutoFlush = false, NewLine = "\r\n" };
				writer.WriteLine("HTTP/{0}.{1} {2} {3}", HTTPVersion / 10, HTTPVersion % 10, ResponseStatusCode, ResponseStatusInfo);
				foreach (HTTPHeader header in ResponseHeaders) writer.WriteLine(header.Key + ": " + header.Value);
				writer.WriteLine();
				writer.Flush();
				lock (this) {
					if (State != HTTPConnectionState.SendingHeaders) throw new InvalidOperationException("Unexpected request state");
					State = HTTPConnectionState.SendingContent;
				}
			}
			if (State != HTTPConnectionState.SendingContent) throw new InvalidOperationException("The response stream can not be opened in the current state");
			//TODO: disallow response stream if HTTP status does not allow content, or if HEAD request
			return Reader;
		}
		private void EndResponseData() {
			if (State == HTTPConnectionState.Closed) return;
			if (RequestBody != null) RequestBody.Dispose();
			if (State != HTTPConnectionState.SendingContent) {
				if ((ResponseStatusCode >= 100 && ResponseStatusCode <= 199) || ResponseStatusCode == 204 || ResponseStatusCode == 304) {
					BeginResponseData();
				} else {
					SetResponseHeader("Content-Length", "0");
					BeginResponseData();
				}
			}
			//If WriteResponseData is called above, it will call EndResponseData which will close the connection, so check state again.
			Boolean keepAlive = KeepAlive && KeepAliveMaxRequests > 1;
			lock (this) {
				if (State != HTTPConnectionState.SendingContent) keepAlive = false;
				if (keepAlive) State = HTTPConnectionState.Closed;
			}
			if (keepAlive) {
				try {
					if (RequestBody != null) RequestBody.Dispose();
					new HTTPContext(Server, Reader, Socket, KeepAliveMaxRequests - 1, IsSecure);
				} catch (Exception ex) {
					Server.RaiseOnError(this, ex);
					Reader.Close();
					Server.RaiseConnectionClosed(this);
				}
				Server.RaiseRequestFinished(this);
			} else {
				Close();
			}
		}

		public Stream GetDirectStream() {
			if (State == HTTPConnectionState.Closed) throw new InvalidOperationException("The context has been closed");
			KeepAlive = false;
			BeginResponseData();
			lock (this) {
				if (State != HTTPConnectionState.SendingContent) throw new InvalidOperationException("Unexpected request state");
				State = HTTPConnectionState.Closed;
			}
			Server.RaiseRequestFinished(this);
			Server.RaiseConnectionClosed(this);
			return Reader.Buffered > 0 ? Reader : Reader.BaseStream;
		}

		private void SendErrorAndClose(int code) {
			try {
				lock (this) {
					if (State == HTTPConnectionState.Starting || State == HTTPConnectionState.ReceivingRequest) State = HTTPConnectionState.ProcessingRequest;
				}
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
			lock (this) {
				if (State == HTTPConnectionState.Closed) return;
				State = HTTPConnectionState.Closed;
			}
			Server.RaiseRequestFinished(this);
			Server.RaiseConnectionClosed(this);
			try { Reader.Close(); } catch { }
			try { if (RequestBody != null) RequestBody.Dispose(); } catch { }
		}
		public void Close(int errorCode) {
			if (errorCode == 0) Close();
			else SendErrorAndClose(errorCode);
		}

		public static long CopyStream(Stream input, Stream output, long length) {
			if (length == 0) return 0;
			int buffersize = 4096 * 10;
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

	public class HTTPRequestEnvironment : Dictionary<String, Object> {
		public new Object this[String key] {
			get {
				Object value;
				if (TryGetValue(key, out value)) return value;
				return null;
			}
			set {
				base[key] = value;
			}
		}
		public T Get<T>(String key) {
			Object value;
			if (TryGetValue(key, out value)) return (T)value;
			return default(T);
		}
	}
	public class HTTPRequestHeaderCollection : IEnumerable<HTTPHeader> {
		HTTPHeader[] headers;
		public HTTPRequestHeaderCollection(ICollection<HTTPHeader> headers) {
			this.headers = ArrayUtil.ToArray(headers);
		}
		public static HTTPRequestHeaderCollection FromStream(Stream stream) {
			List<HTTPHeader> RequestHeaders = new List<HTTPHeader>();
			String headerName = null, headerValue = null;
			while (true) {
				String line = HTTPContext.ReadLine(stream);
				if (line == null) return null;
				if (line.Length == 0) break;
				if (line[0] == ' ' || line[0] == '\t') {
					headerValue += line;
				} else {
					if (headerName != null) RequestHeaders.Add(new HTTPHeader(headerName, (headerValue ?? String.Empty).Trim()));
					String[] request = line.Split(new Char[] { ':' }, 2, StringSplitOptions.None);
					if (request.Length != 2) return null;
					headerName = request[0];
					headerValue = request[1];
				}
			}
			if (headerName != null) RequestHeaders.Add(new HTTPHeader(headerName, (headerValue ?? String.Empty).Trim()));
			return new HTTPRequestHeaderCollection(RequestHeaders);
		}
		public static AsyncResult<HTTPRequestHeaderCollection> BeginReadFromStream(PrebufferingStream stream) {
			AsyncResultSource<HTTPRequestHeaderCollection> source = new AsyncResultSource<HTTPRequestHeaderCollection>();
			List<HTTPHeader> RequestHeaders = new List<HTTPHeader>();
			String headerName = null, headerValue = null;
			AsyncResult result = HTTPContext.BeginReadLines(stream, (line) => {
				if (line.Length == 0) {
					if (headerName != null) RequestHeaders.Add(new HTTPHeader(headerName, (headerValue ?? String.Empty).Trim()));
					return false;
				} else if (line[0] == ' ' || line[0] == '\t') {
					headerValue += line;
					return true;
				} else {
					if (headerName != null) RequestHeaders.Add(new HTTPHeader(headerName, (headerValue ?? String.Empty).Trim()));
					String[] request = line.Split(new Char[] { ':' }, 2, StringSplitOptions.None);
					if (request.Length != 2) {
						RequestHeaders = null;
						return false;
					} else {
						headerName = request[0];
						headerValue = request[1];
						return true;
					}
				}
			});
			result.AddSuccessCallback((r) => {
				source.SetCompleted(false, new HTTPRequestHeaderCollection(RequestHeaders));
			});
			result.AddErrorCallback((ex) => {
				source.SetCompleted(false, ex);
			});
			return source;
		}
		public String Get(String name) {
			foreach (HTTPHeader h in headers) {
				if (name.Equals(h.Key, StringComparison.OrdinalIgnoreCase)) return h.Value;
			}
			return null;
		}
		public String this[String name] { get { return Get(name); } }
		public String[] GetAll(String name) {
			String[] items = new String[0];
			foreach (HTTPHeader h in headers) {
				if (name.Equals(h.Key, StringComparison.OrdinalIgnoreCase)) ArrayUtil.Add(ref items, h.Value);
			}
			return items;
		}

		public IEnumerator<HTTPHeader> GetEnumerator() {
			return ((IEnumerable<HTTPHeader>)headers).GetEnumerator();
		}
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
			return headers.GetEnumerator();
		}
	}
	public class HTTPRequestCookieCollection : IEnumerable<KeyValuePair<String, String>> {
		KeyValuePair<String, String>[] cookies;
		public HTTPRequestCookieCollection(ICollection<KeyValuePair<String, String>> cookies) {
			this.cookies = ArrayUtil.ToArray(cookies);
		}
		public HTTPRequestCookieCollection(String[] cookieheaders) {
			List<KeyValuePair<String, String>> list = new List<KeyValuePair<String, String>>();
			foreach (String cookie in cookieheaders) {
				foreach (String part in cookie.Split(';', ',')) {
					String[] subparts = part.Split('=');
					String key = subparts[0].Trim(' ', '\t', '"');
					String value = (subparts.Length < 2) ? null : subparts[1].Trim(' ', '\t', '"');
					list.Add(new KeyValuePair<string, string>(key, value));
				}
			}
			cookies = list.ToArray();
		}
		public String Get(String name) {
			foreach (HTTPHeader h in cookies) {
				if (name.Equals(h.Key, StringComparison.OrdinalIgnoreCase)) return h.Value;
			}
			return null;
		}
		public String this[String name] { get { return Get(name); } }
		public String[] GetAll(String name) {
			String[] items = new String[0];
			foreach (HTTPHeader h in cookies) {
				if (name.Equals(h.Key, StringComparison.OrdinalIgnoreCase)) ArrayUtil.Add(ref items, h.Value);
			}
			return items;
		}

		public IEnumerator<HTTPHeader> GetEnumerator() {
			return ((IEnumerable<HTTPHeader>)cookies).GetEnumerator();
		}
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
			return cookies.GetEnumerator();
		}
	}
	public class HTTPQueryParametersCollection : IEnumerable<KeyValuePair<String, String>> {
		KeyValuePair<String, String>[] parameters;
		public HTTPQueryParametersCollection(ICollection<KeyValuePair<String, String>> cookies) {
			this.parameters = ArrayUtil.ToArray(cookies);
		}
		public HTTPQueryParametersCollection(String querystring) {
			this.parameters = HTTPContext.DecodeUrlEncodedFields(querystring);
		}
		public String Get(String name) {
			foreach (HTTPHeader h in parameters) {
				if (name.Equals(h.Key, StringComparison.OrdinalIgnoreCase)) return h.Value;
			}
			return null;
		}
		public String this[String name] { get { return Get(name); } }
		public String[] GetAll(String name) {
			String[] items = new String[0];
			foreach (HTTPHeader h in parameters) {
				if (name.Equals(h.Key, StringComparison.OrdinalIgnoreCase)) ArrayUtil.Add(ref items, h.Value);
			}
			return items;
		}

		public IEnumerator<HTTPHeader> GetEnumerator() {
			return ((IEnumerable<HTTPHeader>)parameters).GetEnumerator();
		}
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
			return parameters.GetEnumerator();
		}
	}
	public class HTTPRequestBody : IDisposable {
		public String ContentType { get; private set; }
		public Int64 ContentLength { get { return source.Length; } }
		private Boolean processed = false;
		private Stream source;
		private KeyValuePair<String, String>[] parameters = null;
		private HTTPUploadedFile[] files = null;
		public HTTPRequestBody(HTTPContext context, Stream input) {
			this.source = input;
			ContentType = context.RequestHeaders["Content-Type"];
		}
		public void Cache() {
			if (!(source is CachedVolatileStream)) {
				if (processed) throw new InvalidOperationException("The input stream has already been used");
				source = new CachedVolatileStream(source);
			}
		}
		public Stream GetStream() {
			processed = true;
			return source;
		}
		public String GetPostParameter(String name) {
			foreach (KeyValuePair<String, String> kvp in GetPostParameters()) if (kvp.Key == name) return kvp.Value;
			return null;
		}
		public String this[String name] { get { return GetPostParameter(name); } }
		public String[] GetPostParameters(String name) {
			List<String> list = new List<string>();
			foreach (KeyValuePair<String, String> kvp in GetPostParameters()) if (kvp.Key == name) list.Add(kvp.Value);
			return list.ToArray();
		}
		public KeyValuePair<String, String>[] GetPostParameters() {
			if (parameters != null) return parameters;
			if (processed && !(source is CachedVolatileStream)) throw new InvalidOperationException("The input stream has already been used");
			StructuredHeaderValue ctype = new StructuredHeaderValue(ContentType);
			if (String.IsNullOrEmpty(ctype.Value)) return parameters = new KeyValuePair<String, String>[0];
			if ("application/x-www-form-urlencoded".Equals(ctype.Value, StringComparison.InvariantCultureIgnoreCase)) {
				String data;
				using (StreamReader reader = new StreamReader(GetStream(), Encoding.UTF8)) data = reader.ReadToEnd();
				return parameters = HTTPContext.DecodeUrlEncodedFields(data);
			} else if ("multipart/form-data".Equals(ctype.Value, StringComparison.InvariantCultureIgnoreCase)) {
				String boundary = ctype.GetParameter("boundary");
				IEnumerable<MimePartStream> parts = MimeMultipartEnumerator.Create(GetStream(), boundary);
				List<KeyValuePair<String, String>> parameters_list = new List<KeyValuePair<String, String>>();
				List<HTTPUploadedFile> files_list = new List<HTTPUploadedFile>();
				foreach (MimePartStream part in parts) {
					StructuredHeaderValue disposition = part.ContentDisposition;
					if (!"form-data".Equals(disposition.Value, StringComparison.InvariantCultureIgnoreCase)) continue;
					if (disposition.GetParameter("filename") != null) {
						files_list.Add(new HTTPUploadedFile(disposition.GetParameter("name"), disposition.GetParameter("filename"), part.Headers["Content-Type"], part));
					} else {
						String data;
						using (StreamReader reader = new StreamReader(part, Encoding.UTF8)) data = reader.ReadToEnd();
						parameters_list.Add(new KeyValuePair<String, String>(disposition.GetParameter("name"), data));
					}
				}
				files = files_list.ToArray();
				return parameters = parameters_list.ToArray();
			} else {
				return parameters = new KeyValuePair<String, String>[0];
			}
		}

		public IEnumerable<HTTPUploadedFile> GetFiles() {
			GetPostParameters();
			return files;
		}
		public IEnumerable<HTTPUploadedFile> GetFiles(String fieldName) {
			GetPostParameters();
			return Array.FindAll(files, f => f.FieldName == fieldName);
		}
		public HTTPUploadedFile GetFile(String fieldName) {
			GetPostParameters();
			return Array.Find(files, f => f.FieldName == fieldName);
		}

		public void Dispose() {
			if (files != null) foreach (HTTPUploadedFile d in files) d.Dispose();
			source.Dispose();
		}

		struct StructuredHeaderValue {
			public String Value { get; private set; }
			public String Comment { get; private set; }
			public KeyValuePair<String, String>[] Parameters { get; private set; }
			public StructuredHeaderValue(String value)
				: this() {
				if (value == null) value = String.Empty;
				int i = 0;
				int state = 0;
				String key = null;
				List<KeyValuePair<String, String>> parameters = new List<KeyValuePair<String, String>>();
				while (i < value.Length) {
					Char c = value[i];
					String token = String.Empty;
					int type = -1;
					if (Char.IsWhiteSpace(c)) {
					} else if (c == '(') {
						for (i++; i < value.Length && value[i] != ')'; i++) token += value[i];
						type = 2;
					} else if (c == '"') {
						for (i++; i < value.Length && value[i] != '"'; i++) {
							if (value[i] == '\\' && i + 1 < value.Length) i++;
							token += value[i];
						}
						type = 1;
					} else if (c == ';') {
						if (state == 1) parameters.Add(new KeyValuePair<String, String>(key, String.Empty));
						state = 1;
					} else if (c == '=' && state == 1) {
						state = 2;
					} else {
						for (; i < value.Length && !Char.IsWhiteSpace(value[i]) && value[i] != '(' && value[i] != '"' && value[i] != ';' && (state != 1 || value[i] != '='); i++) token += value[i];
						type = 1;
						i--;
					}
					i++;
					if (type == 1 && state == 0) {
						if (!String.IsNullOrEmpty(Value)) Value += ' ';
						Value += token;
					} else if (type == 1 && state == 1) {
						key = token;
					} else if (type == 1 && state == 2) {
						parameters.Add(new KeyValuePair<String, String>(key, token));
						state = 0;
					} else if (type == 2) {
						if (!String.IsNullOrEmpty(Comment)) Comment += ' ';
						Comment += token;
					}
				}
				if (state == 1) parameters.Add(new KeyValuePair<String, String>(key, String.Empty));
				this.Parameters = parameters.ToArray();
			}
			public String GetParameter(String name) {
				foreach (KeyValuePair<String, String> item in Parameters) if (item.Key.Equals(name, StringComparison.InvariantCultureIgnoreCase)) return item.Value;
				return null;
			}
			public static explicit operator StructuredHeaderValue(String value) {
				return new StructuredHeaderValue(value);
			}
			public override string ToString() {
				StringBuilder sb = new StringBuilder();
				sb.Append('"' + Value.Replace("\"", "\\\"") + '"');
				foreach (KeyValuePair<String, String> item in Parameters) sb.Append("; " + item.Key + "=\"" + item.Value.Replace("\"", "\\\"") + "\"");
				if (!String.IsNullOrEmpty(Comment)) sb.Append("(" + Comment.Replace(')', ' ') + ")");
				return sb.ToString();
			}
		}
		class MimePartStream : Stream {
			PrebufferingStream source;
			Byte[] boundary;
			Boolean ended = false;
			Boolean first;
			internal Boolean IsFinal { get; private set; }
			public HTTPRequestHeaderCollection Headers { get; private set; }
			internal MimePartStream(PrebufferingStream source, Byte[] boundary, Boolean first) {
				this.source = source;
				this.boundary = boundary;
				this.first = first;
				Headers = HTTPRequestHeaderCollection.FromStream(this);
			}
			public StructuredHeaderValue ContentType { get { return (StructuredHeaderValue)Headers["Content-Type"]; } }
			public StructuredHeaderValue ContentDisposition { get { return (StructuredHeaderValue)Headers["Content-Disposition"]; } }
			private Boolean IsBoundary() {
				Byte c;
				int x = 0;
				c = source.Peek(x);
				if (c == '\r' || c == '\n') x++;
				else if (!first) return false;
				first = false;
				if (c == '\r' && source.Peek(x) == '\n') x++;
				if (source.Peek(x) != '-') return false;
				x++;
				if (source.Peek(x) != '-') return false;
				x++;
				for (int j = 0; j < boundary.Length; j++, x++) if (source.Peek(x) != boundary[j]) return false;
				c = source.Peek(x++);
				if (c == '\n' || c == '\r') {
					if (c == '\r' && source.Peek(x) == '\n') x++;
				} else if (c == '-' && source.Peek(x) == '-') {
					x++;
					c = source.Peek(x++);
					if (c != '\n' && c != '\r') return false;
					if (c == '\r' && source.Peek(x) == '\n') x++;
					IsFinal = true;
				} else {
					return false;
				}
				source.Skip(x);
				ended = true;
				return true;
			}
			public override int Read(byte[] buffer, int offset, int count) {
				if (offset < 0 || count < 0) throw new ArgumentOutOfRangeException("count");
				if (ended) return 0;
				source.Prebuffer();
				int read = 0;
				while (read < count) {
					if (IsBoundary()) return read;
					buffer[offset++] = (Byte)source.ReadByte();
					read++;
				}
				return read;
			}
			internal void Skip() {
				while (!ended && !IsBoundary()) source.ReadByte();
			}
			public override bool CanRead { get { return !ended && source.CanRead; } }
			public override bool CanSeek { get { return false; } }
			public override bool CanWrite { get { return false; } }
			public override void Flush() { }
			public override long Length { get { throw new NotSupportedException(); } }
			public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
			public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
			public override void SetLength(long value) { throw new NotSupportedException(); }
			public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
		}
		class MimeMultipartEnumerator : IEnumerator<MimePartStream> {
			PrebufferingStream source;
			Byte[] boundary;
			internal MimeMultipartEnumerator(PrebufferingStream source, Byte[] boundary) {
				this.source = source;
				this.boundary = boundary;
				Current = new MimePartStream(source, boundary, true);
			}
			public MimePartStream Current { get; private set; }
			public void Dispose() {
				source.Dispose();
			}
			object System.Collections.IEnumerator.Current { get { return Current; } }
			public bool MoveNext() {
				Current.Skip();
				if (Current.IsFinal) return false;
				Current = new MimePartStream(source, boundary, false);
				return true;
			}
			public void Reset() { throw new NotSupportedException(); }

			class Enumerable : IEnumerable<MimePartStream> {
				internal Stream source;
				internal String boundary;
				public IEnumerator<MimePartStream> GetEnumerator() {
					return new MimeMultipartEnumerator(source as PrebufferingStream ?? new PrebufferingStream(source), Encoding.ASCII.GetBytes(boundary));
				}
				System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
					return GetEnumerator();
				}
			}

			public static IEnumerable<MimePartStream> Create(Stream source, String boundary) {
				return new Enumerable() { source = source, boundary = boundary };
			}
		}
	}
	public class HTTPUploadedFile : CachedVolatileStream {
		public String FieldName { get; private set; }
		public String FileName { get; private set; }
		public String ContentType { get; private set; }
		public HTTPUploadedFile(String fieldName, String fileName, String contentType, Stream source) : base(source) {
			this.FieldName = FieldName;
			this.FileName = fileName;
			this.ContentType = ContentType;
			this.Seek(0, SeekOrigin.End); //force to cache all source data
			this.Seek(0, SeekOrigin.Begin);
		}
	}
	public class CachedVolatileStream : Stream {
		int threshold = 1024 * 100;
		Stream cache;
		Stream source;
		public override bool CanRead { get { return true; } }
		public override bool CanSeek { get { return true; } }
		public override bool CanWrite { get { return false; } }
		public override void Flush() { }
		public override void SetLength(long value) { throw new NotSupportedException(); }
		public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
		public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state) { throw new NotSupportedException(); }

		public CachedVolatileStream(Stream source) {
			this.source = source;
			this.cache = new MemoryStream();
		}

		public override long Length {
			get {
				if (source != null) CacheUntil(-1, true);
				return cache.Length;
			}
		}

		public override long Position {
			get { return cache.Position; }
			set { Seek(value, SeekOrigin.Begin); }
		}

		public override int Read(byte[] buffer, int offset, int count) {
			if (source != null && cache.Position == cache.Length) CacheUntil(cache.Position + count, true);
			return cache.Read(buffer, offset, count);
		}

		public override long Seek(long offset, SeekOrigin origin) {
			if (source == null) return cache.Seek(offset, origin);
			if (origin == SeekOrigin.End) {
				CacheUntil(-1, false);
				return cache.Seek(offset, origin);
			}
			if (origin == SeekOrigin.Current) offset += cache.Position;
			if (offset > cache.Length) CacheUntil(offset, false);
			return cache.Seek(offset, SeekOrigin.Begin);
		}

		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);
			if (disposing) {
				if (cache != null) cache.Close();
				if (source != null) source.Close();
			}
		}

		void MakeFileCache(Boolean preservePosition) {
			if (cache is MemoryStream) {
				String cacheFile = Path.GetRandomFileName();
				Stream newCache = new FileStream(cacheFile, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.DeleteOnClose | FileOptions.RandomAccess);
				long position = preservePosition ? cache.Position : -1;
				cache.Seek(0, SeekOrigin.Begin);
				((MemoryStream)cache).WriteTo(newCache);
				cache.Dispose();
				cache = newCache;
				if (preservePosition) cache.Position = position;
			}
		}
		void CacheUntil(long amount, Boolean preservePosition) {
			if (source == null || (amount != -1 && amount < cache.Length)) return;
			long position = preservePosition ? cache.Position : -1;
			cache.Seek(0, SeekOrigin.End);
			if (amount != -1) amount -= cache.Length;
			Byte[] buffer = new Byte[amount == -1 ? 4096 : Math.Min(amount, 4096)];
			while (amount == -1 || amount > 0) {
				int read = source.Read(buffer, 0, amount == -1 ? buffer.Length : (int)Math.Min(amount, buffer.Length));
				if (read == 0) {
					source.Close();
					source = null;
					break;
				}
				if (read < 0 || read > amount) throw new IOException();
				if ((cache is MemoryStream) && cache.Length + read > threshold) MakeFileCache(false);
				cache.Write(buffer, 0, read);
				if (amount != -1) amount -= read;
			}
			if (preservePosition) cache.Position = position;
		}
	}
	public class HTTPResponse {
		HTTPContext context;
		public HTTPResponse(HTTPContext context) {
			this.context = context;
		}
		public void SendStatus(int code) {
			String message = HTTPContext.GetMessageForStatus(code);
			context.SendStatus(code, message);
		}
		public void SendStatus(int code, String message) {
			context.SendStatus(code, message);
		}
		public void SendHeader(String name, String value) {
			context.SendHeader(name, value);
		}
		public void SetResponseHeader(String name, String value) {
			context.SetResponseHeader(name, value);
		}
		public void SendErrorResponse(int state) {
			String message = HTTPContext.GetMessageForStatus(state);
			SendStatus(state, message);
			SetResponseHeader("Content-Type", "text/plain");
			WriteResponseData(Encoding.ASCII.GetBytes(String.Format("Error {0}: {1}", state, message)));
		}
		public Stream OpenResponseStream(HTTPResponseStreamMode mode) {
			return context.OpenResponseStream(mode);
		}
		public Stream OpenResponseStream(long length) {
			return context.OpenResponseStream(length);
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
			HTTPContext.CopyStream(stream, output, length);
			output.Close();
		}
	}
}
