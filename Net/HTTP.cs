using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UCIS.Util;
using HTTPHeader = System.Collections.Generic.KeyValuePair<string, string>;

namespace UCIS.Net.HTTP {
	public class HTTPServer : TCPServer.IModule, IDisposable {
		public IHTTPContentProvider ContentProvider { get; set; }
		public Boolean ServeFlashPolicyFile { get; set; }
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
				new HTTPContext(this, socket);
			} catch (Exception) { }
			try {
				Listener.BeginAccept(AcceptCallback, null);
			} catch (Exception) { }
		}

		public void Dispose() {
			if (Listener != null) Listener.Close();
		}

		bool TCPServer.IModule.Accept(TCPStream stream) {
			new HTTPContext(this, stream);
			return false;
		}
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

		private Stream Stream;
		private StreamWriter Writer;
		private List<HTTPHeader> RequestHeaders;
		private HTTPConnectionState State = HTTPConnectionState.Starting;

		private enum HTTPConnectionState {
			Starting = 0,
			ReceivingRequest = 1,
			ProcessingRequest = 2,
			SendingHeaders = 3,
			SendingContent = 4,
			Closed = 5,
		}

		public HTTPContext(HTTPServer server, TCPStream stream) {
			this.Server = server;
			this.Socket = stream.Socket;
			this.LocalEndPoint = Socket.LocalEndPoint;
			this.RemoteEndPoint = Socket.RemoteEndPoint;
			this.Stream = stream;
			Init();
		}

		public HTTPContext(HTTPServer server, Socket socket) {
			this.Server = server;
			this.Socket = socket;
			this.LocalEndPoint = socket.LocalEndPoint;
			this.RemoteEndPoint = socket.RemoteEndPoint;
			this.Stream = new NetworkStream(socket, true);
			Init();
		}

		private void Init() {
			Writer = new StreamWriter(Stream, Encoding.ASCII);
			Writer.NewLine = "\r\n";
			Writer.AutoFlush = true;
			UCIS.ThreadPool.RunTask(ReceiveOperation, null);
		}

		private String ReadLine() {
			StringBuilder s = new StringBuilder();
			while (true) {
				int b = Stream.ReadByte();
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

		private void ReceiveOperation(Object state) {
			State = HTTPConnectionState.ReceivingRequest;
			try {
				String line = ReadLine();
				if (line == null) {
					Close();
					return;
				}
				if (Server.ServeFlashPolicyFile && line[0] == '<') { //<policy-file-request/>
					Writer.WriteLine("<cross-domain-policy><allow-access-from domain=\"*\" to-ports=\"*\" /></cross-domain-policy>");
					Stream.WriteByte(0);
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
					default: goto SendError400AndClose;
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
			SendErrorAndClose(400);
			return;
SendError500AndClose:
			SendErrorAndClose(500);
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

		public void SendErrorAndClose(int state) {
			try {
				SendStatus(state);
				GetResponseStream();
			} catch (Exception ex) {
				Console.Error.WriteLine(ex);
			}
			Close();
		}

		public void SendStatus(int code) {
			String message;
			switch (code) {
				case 101: message = "Switching Protocols"; break;
				case 200: message = "OK"; break;
				case 400: message = "Bad Request"; break;
				case 404: message = "Not Found"; break;
				case 500: message = "Internal Server Error"; break;
				default: message = "Unknown Status"; break;
			}
			SendStatus(code, message);
		}
		public void SendStatus(int code, String message) {
			if (State != HTTPConnectionState.ProcessingRequest) throw new InvalidOperationException();
			StringBuilder sb = new StringBuilder();
			sb.Append("HTTP/");
			switch (HTTPVersion) {
				case 10: sb.Append("1.0"); break;
				case 11: sb.Append("1.1"); break;
				default: throw new ArgumentException("The HTTP version is not supported", "HTTPVersion");
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
				SendHeader("Server", "UCIS Webserver");
				SendHeader("Connection", "Close");
			}
		}
		public void SendHeader(String name, String value) {
			if (State == HTTPConnectionState.ProcessingRequest) SendStatus(200);
			if (State != HTTPConnectionState.SendingHeaders) throw new InvalidOperationException();
			Writer.WriteLine(name + ": " + value);
		}
		public Stream GetResponseStream() {
			if (State == HTTPConnectionState.ProcessingRequest) SendStatus(200);
			if (State == HTTPConnectionState.SendingHeaders) {
				Writer.WriteLine();
				State = HTTPConnectionState.SendingContent;
			}
			if (State != HTTPConnectionState.SendingContent) throw new InvalidOperationException();
			return Stream;
		}

		public void Close() {
			if (State == HTTPConnectionState.Closed) return;
			Stream.Close();
			State = HTTPConnectionState.Closed;
		}
	}

	public interface IHTTPContentProvider {
		void ServeRequest(HTTPContext context);
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
				context.SendErrorAndClose(404);
			}
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
					context.SendHeader("Content-Length", fs.Length.ToString());
					long left = fs.Length;
					Stream response = context.GetResponseStream();
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
				context.SendErrorAndClose(404);
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
				context.SendErrorAndClose(404);
				return;
			}
			String reqname1 = context.RequestPath;
			if (reqname1.Length > 0 && reqname1[0] == '/') reqname1 = reqname1.Substring(1);
			String reqname2 = reqname1;
			if (reqname2.Length > 0 && !reqname2.EndsWith("/")) reqname2 += "/";
			reqname2 += "index.htm";
			using (FileStream fs = File.OpenRead(TarFileName)) {
				while (true) {
					Byte[] header = new Byte[512];
					if (fs.Read(header, 0, 512) != 512) break;
					int flen = Array.IndexOf<Byte>(header, 0, 0, 100);
					if (flen == 0) continue;
					if (flen == -1) flen = 100;
					String fname = Encoding.ASCII.GetString(header, 0, flen);
					String fsize = Encoding.ASCII.GetString(header, 124, 11);
					int fsizei = Convert.ToInt32(fsize, 8);
					if (reqname1.Equals(fname, StringComparison.OrdinalIgnoreCase) || reqname2.Equals(fname)) {
						context.SendStatus(200);
						context.SendHeader("Content-Length", fsizei.ToString());
						String ctype = null;
						switch (Path.GetExtension(fname).ToUpperInvariant()) {
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
						Stream response = context.GetResponseStream();
						int left = fsizei;
						while (left > 0) {
							byte[] buffer = new byte[1024 * 10];
							int len = fs.Read(buffer, 0, buffer.Length);
							if (len <= 0) break;
							left -= len;
							response.Write(buffer, 0, len);
						}
						response.Close();
						return;
					} else {
						fs.Seek(fsizei, SeekOrigin.Current);
					}
					int padding = fsizei % 512;
					if (padding != 0) padding = 512 - padding;
					fs.Seek(padding, SeekOrigin.Current);
				}
			}
			context.SendErrorAndClose(404);
		}
	}
}
