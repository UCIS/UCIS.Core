using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using UCIS.Net;
using UCIS.Net.HTTP;

namespace UCIS.VNCServer {
	public class VNCServerManager : IFramebuffer {
		private VNCFramebuffer fb;
		private List<VNCServerConnection> clients = new List<VNCServerConnection>();
		private VNCServer server;

		public event MouseEventHandler MouseDown;
		public event MouseEventHandler MouseMove;
		public event MouseEventHandler MouseUp;
		public event KeyEventHandler KeyDown;
		public event KeyEventHandler KeyUp;

		public VNCServerManager(int w, int h) : this(w, h, 5901) { }
		public VNCServerManager(int w, int h, int p) {
			fb = new VNCFramebuffer(w, h);
			server = new VNCServer(p);
			server.ClientConnected += delegate(object sender, VNCClientConnectedEventArgs e) {
				e.Client.Framebuffer = fb;
				e.Client.MouseDown += MouseDown;
				e.Client.MouseMove += MouseMove;
				e.Client.MouseUp += MouseUp;
				e.Client.KeyDown += KeyDown;
				e.Client.KeyUp += KeyUp;
				e.Client.Disconnected += ClientDisconnected;
				lock (clients) clients.Add(e.Client);
			};
			server.Listen();
		}

		private void ClientDisconnected(Object sender, EventArgs e) {
			lock (clients) clients.Remove((VNCServerConnection)sender);
		}

		public void Close() {
			server.Close();
			foreach (VNCServerConnection c in clients.ToArray()) c.Close();
		}

		public int Width {
			get { return fb.Width; }
		}
		public int Height {
			get { return fb.Height; }
		}
		public void Clear() {
			fb.Clear();
		}
		public void DrawImage(Image image, Rectangle srcrect, Point dest) {
			fb.DrawImage(image, srcrect, dest);
		}
		public void DrawPixels(int[] bitmap, int bmwidth, Rectangle srcrect, Point dest) {
			fb.DrawPixels(bitmap, bmwidth, srcrect, dest);
		}
		public void DrawPixels(IntPtr bitmap, int bmwidth, Rectangle srcrect, Point dest) {
			fb.DrawPixels(bitmap, bmwidth, srcrect, dest);
		}
		public void CopyRectangle(Rectangle srcrect, Point dest) {
			fb.CopyRectangle(srcrect, dest);
		}
		public void CopyRectangleTo(Rectangle srcrect, IFramebuffer destbuffer, Point destposition) {
			fb.CopyRectangleTo(srcrect, destbuffer, destposition);
		}

		public void Resize(int w, int h) {
			fb = new VNCFramebuffer(w, h);
			foreach (VNCServerConnection c in clients) c.Framebuffer = fb;
		}
	}
	public class VNCFramebufferUpdateEventArgs : EventArgs {
		public VNCFramebuffer Framebuffer { get; private set; }
		public Rectangle Area { get; private set; }
		internal VNCFramebufferUpdateEventArgs(VNCFramebuffer fb, Rectangle a) {
			this.Framebuffer = fb;
			this.Area = a;
		}
	}
	public class VNCFramebuffer : IFramebuffer {
		public event EventHandler<VNCFramebufferUpdateEventArgs> Update;
		internal Int32[] Framebuffer { get; private set; }
		public int Width { get; private set; }
		public int Height { get; private set; }
		public VNCFramebuffer(int width, int height) {
			if (width <= 0) throw new ArgumentOutOfRangeException("width");
			if (height <= 0) throw new ArgumentOutOfRangeException("height");
			this.Width = width;
			this.Height = height;
			this.Framebuffer = new Int32[width * height];
		}
		public void Clear() {
			for (int i = 0; i < Width * Height; i++) Framebuffer[i] = 0;
			EventHandler<VNCFramebufferUpdateEventArgs> eh = Update;
			if (eh != null) eh(this, new VNCFramebufferUpdateEventArgs(this, new Rectangle(0, 0, Width, Height)));
		}
		public unsafe void DrawImage(Image image, Rectangle srcrect, Point dest) {
			if (srcrect.Width == 0 || srcrect.Height == 0) return;
			fixed (int* fbptr = Framebuffer) {
				using (Bitmap b = new Bitmap(Width, Height, Width * 4, PixelFormat.Format32bppRgb, (IntPtr)fbptr)) {
					using (Graphics g = Graphics.FromImage(b)) {
						g.CompositingMode = CompositingMode.SourceCopy;
						g.DrawImage(image, new Rectangle(dest, srcrect.Size), srcrect, GraphicsUnit.Pixel);
					}
				}
			}
			EventHandler<VNCFramebufferUpdateEventArgs> eh = Update;
			if (eh != null) eh(this, new VNCFramebufferUpdateEventArgs(this, new Rectangle(dest, srcrect.Size)));
		}
		public void DrawBitmap(Bitmap bitmap, Rectangle srcrect, Point dest) {
			DrawImage(bitmap, srcrect, dest);
		}
		public unsafe void DrawPixels(int[] bitmap, int bmwidth, Rectangle srcrect, Point dest) {
			fixed (int* bmp = bitmap) DrawPixels(bmp, bmwidth, srcrect, dest);
		}
		public unsafe void DrawPixels(IntPtr bitmap, int bmwidth, Rectangle srcrect, Point dest) {
			DrawPixels((int*)bitmap, bmwidth, srcrect, dest);
		}
		public unsafe void DrawPixels(int* bitmap, int bmwidth, Rectangle srcrect, Point dest) {
			if (srcrect.X < 0 || srcrect.Y < 0 || srcrect.Width < 0 || srcrect.Height < 0) throw new ArgumentOutOfRangeException("srcrect");
			if (dest.X < 0 || dest.Y < 0 || dest.X + srcrect.Width > Width || dest.Y + srcrect.Height > Height) throw new ArgumentOutOfRangeException("dest");
			if (srcrect.Width == 0 || srcrect.Height == 0) return;
			int* bmin = bitmap + srcrect.Y * bmwidth + srcrect.X;
			//Optionally detect regions that have actually changed. This produces many small regions which may slow down the Tight JPEG encoder (and ZLib based codecs)
			//DrawChangedPixels(Framebuffer, dest.Y * Width + dest.X, bmin, srcrect.Width, srcrect.Height, bmwidth, dest.X, dest.Y);
			//return;
			fixed (int* fbptr = Framebuffer) {
				int* bmout = fbptr + dest.Y * Width + dest.X;
				for (int y = 0; y < srcrect.Height; y++) {
					int* bminl = bmin + y * bmwidth;
					int* bmoutl = bmout + y * Width;
					for (int x = 0; x < srcrect.Width; x++) {
						bmoutl[x] = bminl[x];
					}
				}
			}
			EventHandler<VNCFramebufferUpdateEventArgs> eh = Update;
			if (eh != null) eh(this, new VNCFramebufferUpdateEventArgs(this, new Rectangle(dest, srcrect.Size)));
		}
		private unsafe void DrawChangedPixels(int[] shadow, int shadowoffset, int* pixels, int width, int height, int bmwidth, int xoffset, int yoffset) {
			EventHandler<VNCFramebufferUpdateEventArgs> eh = Update;
			if (eh == null) return;
			int firstx = -1, lastx = -1, firsty = -1, lasty = -1;
			for (int y = 0; y < height; y++) {
				int firstxline = -1, lastxline = -1;
				for (int x = 0; x < width; x++) {
					if (shadow[shadowoffset] != *pixels) {
						if (firstxline == -1) firstxline = x;
						lastxline = x;
						shadow[shadowoffset] = *pixels;
					}
					shadowoffset++;
					pixels++;
				}
				shadowoffset += Width - width;
				pixels += bmwidth - width;
				if (firsty != -1 && firstxline == -1) {
					eh(this, new VNCFramebufferUpdateEventArgs(this, new Rectangle(firstx + xoffset, firsty + yoffset, lastx - firstx + 1, lasty - firsty + 1)));
					firsty = lasty = -1;
				} else if (firstxline != -1) {
					if (firsty == -1) {
						firsty = y;
						firstx = firstxline;
						lastx = lastxline;
					} else {
						if (firstxline < firstx) firstx = firstxline;
						if (lastxline > lastx) lastx = lastxline;
					}
					lasty = y;
				}
			}
			if (firsty != -1) {
				eh(this, new VNCFramebufferUpdateEventArgs(this, new Rectangle(firstx + xoffset, firsty + yoffset, lastx - firstx + 1, lasty - firsty + 1)));
			}
		}
		public void CopyRectangle(Rectangle srcrect, Point dest) {
			DrawPixels(Framebuffer, Width, srcrect, dest);
		}
		public void CopyRectangleTo(Rectangle srcrect, IFramebuffer destbuffer, Point destposition) {
			destbuffer.DrawPixels(Framebuffer, Width, srcrect, destposition);
		}
	}
	struct RFBPixelFormat {
		public Byte BitsPerPixel { get; set; }
		public Byte ColorDepth { get; set; }
		public Boolean BigEndian { get; set; }
		public Boolean TrueColor { get; set; }
		public UInt16 RedMax { get; set; }
		public UInt16 GreenMax { get; set; }
		public UInt16 BlueMax { get; set; }
		public Byte RedShift { get; set; }
		public Byte GreenShift { get; set; }
		public Byte BlueShift { get; set; }
	}
	public class VNCClientConnectedEventArgs : EventArgs {
		public VNCServer Server { get; private set; }
		public EndPoint RemoteEndPoint { get; private set; }
		public VNCServerConnection Client { get; private set; }
		public Boolean Drop { get; set; }
		public Boolean AllowNoAuthentication { get; set; }
		public Boolean AllowPasswordAuthentication { get; set; }
		public VNCClientConnectedEventArgs(VNCServer serv, VNCServerConnection c, EndPoint ep) {
			this.Server = serv;
			this.RemoteEndPoint = ep;
			this.Client = c;
			this.Drop = false;
			this.AllowNoAuthentication = true;
			this.AllowPasswordAuthentication = false;
		}
	}
	public class VNCClientAuthenticationEventArgs : EventArgs {
		public VNCServerConnection Client { get; private set; }
		public Boolean Drop { get; set; }
		public Boolean UsedPasswordAuthentication { get; internal set; }
		public String DesktopName { get; set; }
		internal Byte[] VNCAuthChallenge { private get; set; }
		internal Byte[] VNCAuthResponse { private get; set; }
		internal VNCClientAuthenticationEventArgs(VNCServerConnection c) {
			this.Client = c;
			this.Drop = false;
		}
		public Boolean CheckPassword(String password) {
			return CheckPassword(Encoding.ASCII.GetBytes(password));
		}
		public Boolean CheckPassword(Byte[] password) {
			Byte[] passwordtransform = new Byte[8];
			for (int i = 0; i < 8 && i < password.Length; i++) {
				Byte a = password[i], b = 0;
				for (int j = 0; j < 8; j++) b |= (Byte)(((a >> (7 - j)) & 1) << j);
				passwordtransform[i] = b;
			}
			byte[] check;
			using (DES des = new DESCryptoServiceProvider()) {
				des.Mode = CipherMode.ECB;
				des.Padding = PaddingMode.None;
				using (ICryptoTransform transform = des.CreateEncryptor(passwordtransform, null)) {
					check = transform.TransformFinalBlock(VNCAuthChallenge, 0, 16);
				}
			}
			for (int i = 0; i < 16; i++) if (VNCAuthResponse[i] != check[i]) return false;
			return true;
		}
	}
	public class VNCServer : IHTTPContentProvider {
		public event EventHandler<VNCClientConnectedEventArgs> ClientConnected;
		public EndPoint LocalEndPoint { get; protected set; }
		public Boolean Listening { get; protected set; }
		private Socket socket = null;
		public VNCServer() : this(null) { }
		public VNCServer(int port) : this(new IPEndPoint(IPAddress.Any, port)) { }
		public VNCServer(EndPoint ep) {
			LocalEndPoint = ep;
		}
		public void Listen() {
			if (LocalEndPoint == null) throw new ArgumentNullException("LocalEndPoint");
			Listen(LocalEndPoint);
		}
		public void Listen(int port) {
			Listen(new IPEndPoint(IPAddress.Any, port));
		}
		public virtual void Listen(EndPoint ep) {
			if (Listening) throw new InvalidOperationException("The server is already listening");
			socket = new Socket(ep.AddressFamily, SocketType.Stream, ProtocolType.Unspecified);
			socket.Bind(ep);
			socket.Listen(5);
			LocalEndPoint = ep;
			Listening = true;
			socket.BeginAccept(AcceptCallback, socket);
		}
		public virtual void Close() {
			if (!Listening) throw new InvalidOperationException("The server is not listening");
			Listening = false;
			socket.Close();
		}
		private void AcceptCallback(IAsyncResult ar) {
			Socket socket = ar.AsyncState as Socket;
			try {
				Socket clientsock = socket.EndAccept(ar);
				if (clientsock.ProtocolType == ProtocolType.Tcp) clientsock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
				ClientAccepted(new VNCServerConnection(clientsock));
			} catch (SocketException ex) {
				Debug.WriteLine(ex);
			} catch (ObjectDisposedException ex) {
				Debug.WriteLine(ex);
			}
			if (Listening) socket.BeginAccept(AcceptCallback, socket);
		}
		void IHTTPContentProvider.ServeRequest(IHTTPContext context) {
			WebSocketPacketStream stream = new WebSocketPacketStream(context);
			ClientAccepted(new VNCServerConnection(stream, context.Socket));
		}
		protected void ClientAccepted(VNCServerConnection client) {
			VNCClientConnectedEventArgs clientargs = new VNCClientConnectedEventArgs(this, client, client.RemoteEndPoint);
			if (ClientConnected != null) ClientConnected(this, clientargs);
			if (clientargs.Drop) {
				client.Close();
			} else {
				client.RunAsync(clientargs);
			}
		}
	}
	public class WebVNCServer : VNCServer {
		private HTTPServer httpserver;
		public WebVNCServer() : this(null) { }
		public WebVNCServer(int port) : this(new IPEndPoint(IPAddress.Any, port)) { }
		public WebVNCServer(EndPoint ep) : base(ep) {
			httpserver = new HTTPServer();
			httpserver.ContentProvider = this;
			httpserver.ServeFlashPolicyFile = true;
		}
		public override void Listen(EndPoint ep) {
			httpserver.Listen(ep);
		}
		public override void Close() {
			httpserver.Dispose();
		}
	}
	public interface IZLibCompressor {
		byte[] Compress(Byte[] data, int offset, int count);
	}
	public delegate IZLibCompressor ZLibCompressorFactory();
	public class VNCServerConnection {
		VNCFramebuffer framebuffer = null;
		List<Rectangle> dirtyrects = new List<Rectangle>();
		int waitingforupdate = 0;
		Rectangle waitingforupdaterect = Rectangle.Empty;
		Int32[] SupportedEncodings = new Int32[] { };
		MouseButtons mousebuttons = MouseButtons.None;
		Boolean resized = false;
		Thread worker = null;
		Keys modifierKeys = Keys.None;
		MemoryStream SendBuffer = null;
		Stream socket;
		ISocket realsocket;
		Int32 protover;
		RFBPixelFormat pixelformat;
		int jpegCounter = 0;
		Rectangle blurryrect = Rectangle.Empty;
		int blurryrecoveryline = 0;
		Point mousePosition = new Point(-1, -1);
		ZLibCompressorFactory ZLibFactory = null;

		public int Width { get; private set; }
		public int Height { get; private set; }
		public Object Tag { get; set; }

		public EndPoint RemoteEndPoint { get { return realsocket == null ? null : realsocket.RemoteEndPoint; } }
		public VNCFramebuffer Framebuffer {
			get { return framebuffer; }
			set {
				if (framebuffer != null) {
					framebuffer.Update -= FBUpdate;
				}
				framebuffer = value;
				if (value == null) return;
				lock (dirtyrects) {
					resized = true;
					dirtyrects.Clear();
					framebuffer.Update += FBUpdate;
				}
				FBUpdate(this, new VNCFramebufferUpdateEventArgs(value, new Rectangle(0, 0, framebuffer.Width, framebuffer.Height)));
			}
		}

		public event MouseEventHandler MouseDown;
		public event MouseEventHandler MouseMove;
		public event MouseEventHandler MouseUp;
		public event KeyEventHandler KeyDown;
		public event KeyEventHandler KeyUp;

		public event EventHandler Disconnected;

		public event EventHandler UpdateRequested;
		public event EventHandler WaitingForUpdate;

		public event EventHandler<VNCClientAuthenticationEventArgs> ClientAuthentication;
		public event EventHandler ConnectionComplete;

		public VNCServerConnection(Socket client) : this(new NetworkStream(client, true), new FWSocketWrapper(client)) {
			realsocket.SendBufferSize = 1024 * 1024;
		}
		public VNCServerConnection(ISocket client) : this(new SocketStream(client, true), client) {
			realsocket.SendBufferSize = 1024 * 1024;
		}
		public VNCServerConnection(Stream client) : this(client, null) { }
		public VNCServerConnection(Stream client, ISocket socket) {
			this.socket = client;
			this.realsocket = socket;
			pixelformat = new RFBPixelFormat() {
				BigEndian = false, BitsPerPixel = 32, ColorDepth = 24, TrueColor = true,
				BlueMax = 255, BlueShift = 0, GreenMax = 255, GreenShift = 8, RedMax = 255, RedShift = 16
			};
		}
		
		public void Close() {
			socket.Close();
		}
		
		public void RunAsync(VNCClientConnectedEventArgs args) {
			worker = new Thread(RunSafe);
			worker.Start(args);
		}
		private void RunSafe(Object state) {
			try {
				RunProc((VNCClientConnectedEventArgs)state);
			} catch (Exception ex) {
				Console.Error.WriteLine(ex);
			} finally {
				try { socket.Close(); } catch (Exception ex) { Console.Error.WriteLine(ex); }
				if (Disconnected != null) Disconnected(this, new EventArgs());
			}
		}
		private void RunProc(VNCClientConnectedEventArgs connargs) {
			Initialisation(connargs);
			ReceiveLoop();
		}

		private void Initialisation(VNCClientConnectedEventArgs connargs) {
			{
				Byte[] protovbuf = Encoding.ASCII.GetBytes("RFB 003.008\n");
				SendAll(protovbuf);
				FlushSendBuffer();
				protovbuf = ReceiveAll(12);
				String protovs = Encoding.ASCII.GetString(protovbuf);
				protover = int.Parse(protovs.Substring(4, 3)) * 1000 + int.Parse(protovs.Substring(8, 3));
			}
			//Console.WriteLine("Client protocol is {0} = {1}", protovs.TrimEnd('\n'), protover);
			if (protover < 3003) throw new InvalidOperationException("Unsupported protocol version");
			VNCClientAuthenticationEventArgs authargs = new VNCClientAuthenticationEventArgs(this);
			if (protover >= 3007) {
				if (connargs.AllowNoAuthentication && connargs.AllowPasswordAuthentication) {
					SendAll(new Byte[] { 2, 1, 2 }); //2 security types, no security, VNC security
				} else if (connargs.AllowNoAuthentication) {
					SendAll(new Byte[] { 1, 1 }); //1 security type, none security
				} else if (connargs.AllowPasswordAuthentication) {
					SendAll(new Byte[] { 1, 2 }); //1 security type, VNC security
				} else {
					SendAll(new Byte[] { 0 }); //no security types, drop connection
					throw new InvalidOperationException("No security types allowed");
				}
				FlushSendBuffer();
				Byte[] sectype = ReceiveAll(1);
				if (sectype[0] == 1 && connargs.AllowNoAuthentication) {
					authargs.UsedPasswordAuthentication = false;
				} else if (sectype[0] == 2 && connargs.AllowPasswordAuthentication) {
					authargs.UsedPasswordAuthentication = true;
				} else throw new InvalidOperationException("Unsupported security type");
			} else if (protover == 3003) {
				if (connargs.AllowNoAuthentication) {
					SendUInt32(1); //Security type is none
					authargs.UsedPasswordAuthentication = false;
				} else if (connargs.AllowPasswordAuthentication) {
					SendUInt32(2); //Security type is VNC security
					authargs.UsedPasswordAuthentication = true;
				} else {
					SendUInt32(0); //no security types, drop connection
					throw new InvalidOperationException("No security types allowed");
				}
				FlushSendBuffer();
			} else {
				throw new InvalidOperationException("Unsupported protocol version");
			}
			if (authargs.UsedPasswordAuthentication) {
				Byte[] challenge = new Byte[16];
				(new RNGCryptoServiceProvider()).GetBytes(challenge);
				SendAll(challenge);
				FlushSendBuffer();
				authargs.VNCAuthChallenge = challenge;
				authargs.VNCAuthResponse = ReceiveAll(16);
			}
			if (ClientAuthentication != null) ClientAuthentication(this, authargs);
			if (authargs.Drop) {
				SendUInt32(1); //Security not OK
				FlushSendBuffer();
				throw new Exception("Authentication rejected");
			}
			if (authargs.UsedPasswordAuthentication || protover >= 3008) SendUInt32(0); //Security OK
			FlushSendBuffer();
			Byte[] clientinit = ReceiveAll(1);
			//Console.WriteLine("Shared = {0}", clientinit[0]);
			{
				VNCFramebuffer fb = framebuffer;
				if (fb != null) {
					Width = fb.Width;
					Height = fb.Height;
				} else {
					Width = 1024;
					Height = 768;
				}
			}
			resized = false;
			SendUInt16((UInt16)Width);
			SendUInt16((UInt16)Height);
			SendPixelFormat(pixelformat);
			{
				Byte[] desknamestr;
				if (authargs.DesktopName == null) desknamestr = new Byte[0];
				else desknamestr = Encoding.ASCII.GetBytes(authargs.DesktopName);
				SendUInt32((UInt32)desknamestr.Length);
				SendAll(desknamestr);
			}
			FlushSendBuffer();
			if (ConnectionComplete != null) ConnectionComplete(this, new EventArgs());
		}

		private void ReceiveLoop() {
			while (true) {
				Byte mtype = ReceiveByte();
				switch (mtype) {
					case 0: //SetPixelFormat
						ReceiveMessageSetPixelFormat();
						break;
					case 2: //SetEncodings
						Byte[] b = ReceiveAll(3);
						b = ReceiveAll(4 * ((b[1] << 8) | b[2]));
						SupportedEncodings = new Int32[b.Length / 4];
						for (int i = 0; i < b.Length; i += 4) SupportedEncodings[i / 4] = (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3];
						break;
					case 3: //FramebufferUpdateRequest
						b = ReceiveAll(9);
						Rectangle r = new Rectangle((b[1] << 8) | b[2], (b[3] << 8) | b[4], (b[5] << 8) | b[6], (b[7] << 8) | b[8]);
						SendQueuedRectangles(r, b[0] != 0);
						break;
					case 4: //KeyEvent
						ReceiveMessageKeyboard();
						break;
					case 5: //PointerEvent
						ReceiveMessageMouse();
						break;
					case 6: //ClientCutText
						b = ReceiveAll(7);
						ReceiveAll((b[3] << 24) | (b[4] << 16) | (b[5] << 8) | b[6]);
						break;
					default:
						throw new InvalidOperationException("Received unknown message type, synchronization is lost");
				}
			}
		}
		private void ReceiveMessageSetPixelFormat() {
			Byte[] b = ReceiveAll(3);
			pixelformat = ReceivePixelFormat();
			if (!pixelformat.TrueColor) {
				//I don't want to use a pallette, so I cheat by sending a 8bpp "true color" pallette
				pixelformat.TrueColor = true;
				pixelformat.BigEndian = false;
				pixelformat.RedShift = 0;
				pixelformat.RedMax = 3;
				pixelformat.GreenShift = 2;
				pixelformat.GreenMax = 7;
				pixelformat.BlueShift = 5;
				pixelformat.BlueMax = 7;
				lock (socket) {
					SendUInt8(1); //SetColourMapEntries
					SendUInt8(0); //Padding
					SendUInt16(0); //First colour
					SendUInt16(256); //Number of colours
					for (UInt16 blue = 0; blue < 256; blue += 32) {
						for (UInt16 green = 0; green < 256; green += 32) {
							for (UInt16 red = 0; red < 256; red += 64) {
								SendUInt16((UInt16)(red << 8));
								SendUInt16((UInt16)(green << 8));
								SendUInt16((UInt16)(blue << 8));
							}
						}
					}
					FlushSendBuffer();
				}
			}
		}
		private void ReceiveMessageKeyboard() {
			Byte[] b = ReceiveAll(7);
			Boolean down = b[0] != 0;
			uint key = (uint)((b[3] << 24) | (b[4] << 16) | (b[5] << 8) | b[6]);
			//Console.WriteLine("KeyEvent code=0x{0:x} down={1}", key, down);
			Keys keyval = Keys.None;
			//see: http://cgit.freedesktop.org/xorg/proto/x11proto/plain/keysymdef.h
			if (key >= 'A' && key <= 'Z') keyval = (Keys)(key + (int)Keys.A - 'A') | Keys.Shift;
			else if (key >= 'a' && key <= 'z') keyval = (Keys)(key + (int)Keys.A - 'a');
			else if (key >= '0' && key <= '9') keyval = (Keys)(key + (int)Keys.D0 - '0');
			else if (key >= 0xffb0 && key <= 0xffb9) keyval = (Keys)(key + (int)Keys.NumPad0 - 0xffb0);
			else if (key >= 0xffbe && key <= 0xffd5) keyval = (Keys)(key + (int)Keys.F1 - 0xffbe); //all the way to F35...
			else switch (key) {
					case 0xff08: keyval = Keys.Back; break;
					case 0xff09: keyval = Keys.Tab; break;
					case 0xff0a: keyval = Keys.LineFeed; break;
					case 0xff0b: keyval = Keys.Clear; break;
					case 0xff0d: keyval = Keys.Return; break;
					case 0xff13: keyval = Keys.Pause; break;
					case 0xff14: keyval = Keys.Scroll; break;
					case 0xff15: keyval = Keys.None; break; //Sys req
					case 0xff1b: keyval = Keys.Escape; break;
					case 0xffff: keyval = Keys.Delete; break;
					case 0xff50: keyval = Keys.Home; break;
					case 0xff51: keyval = Keys.Left; break;
					case 0xff52: keyval = Keys.Up; break;
					case 0xff53: keyval = Keys.Right; break;
					case 0xff54: keyval = Keys.Down; break;
					case 0xff55: keyval = Keys.PageUp; break;
					case 0xff56: keyval = Keys.PageDown; break;
					case 0xff57: keyval = Keys.End; break;
					case 0xff58: keyval = Keys.Home; break;
					case 0xff60: keyval = Keys.Select; break;
					case 0xff61: keyval = Keys.Print; break;
					case 0xff62: keyval = Keys.Execute; break;
					case 0xff63: keyval = Keys.Insert; break;
					case 0xff65: keyval = Keys.None; break; //Undo
					case 0xff66: keyval = Keys.None; break; //Redo
					case 0xff67: keyval = Keys.Apps; break;
					case 0xff68: keyval = Keys.BrowserSearch; break;
					case 0xff69: keyval = Keys.Cancel; break;
					case 0xff6a: keyval = Keys.Help; break;
					case 0xff6b: keyval = Keys.Pause; break;
					case 0xff7e: keyval = Keys.None; break; //Character set switch
					case 0xff7f: keyval = Keys.NumLock; break;
					case 0xff80: keyval = Keys.Space; break;
					case 0xff89: keyval = Keys.Tab; break;
					case 0xff8d: keyval = Keys.Enter; break;
					case 0xff91: keyval = Keys.F1; break;
					case 0xff92: keyval = Keys.F2; break;
					case 0xff93: keyval = Keys.F3; break;
					case 0xff94: keyval = Keys.F4; break;
					case 0xff95: keyval = Keys.Home; break;
					case 0xff96: keyval = Keys.Left; break;
					case 0xff97: keyval = Keys.Up; break;
					case 0xff98: keyval = Keys.Right; break;
					case 0xff99: keyval = Keys.Down; break;
					case 0xff9a: keyval = Keys.PageUp; break;
					case 0xff9b: keyval = Keys.PageDown; break;
					case 0xff9c: keyval = Keys.End; break;
					case 0xff9d: keyval = Keys.Home; break;
					case 0xff9e: keyval = Keys.Insert; break;
					case 0xff9f: keyval = Keys.Delete; break;
					case 0xffbd: keyval = Keys.None; break; //keypad equals
					case 0xffaa: keyval = Keys.Multiply; break;
					case 0xffab: keyval = Keys.Add; break;
					case 0xffac: keyval = Keys.Separator; break;
					case 0xffad: keyval = Keys.Subtract; break;
					case 0xffae: keyval = Keys.Decimal; break;
					case 0xffaf: keyval = Keys.Divide; break;
					case 0xffe1: keyval = Keys.LShiftKey; break;
					case 0xffe2: keyval = Keys.RShiftKey; break;
					case 0xffe3: keyval = Keys.LControlKey; break;
					case 0xffe4: keyval = Keys.RControlKey; break;
					case 0xffe5: keyval = Keys.CapsLock; break;
					case 0xffe6: keyval = Keys.CapsLock; break; //shift lock!?
					case 0xffe7: keyval = Keys.None; break; //Left meta!?
					case 0xffe8: keyval = Keys.None; break; //Right meta!?
					case 0xffe9: keyval = Keys.LMenu; break;
					case 0xffea: keyval = Keys.Menu; break; //right alt
					case 0xffeb: keyval = Keys.LWin; break;
					case 0xffec: keyval = Keys.RWin; break;
					case 0xffed: keyval = Keys.None; break; //Left hyper
					case 0xffee: keyval = Keys.None; break; //Right hyper
					//Some X11 specific stuff
					case 0x20: keyval = Keys.Space; break;
					case 0x21: keyval = Keys.D1 | Keys.Shift; break; //!
					case 0x22: keyval = Keys.OemQuotes | Keys.Shift; break; //double quotes
					case 0x23: keyval = Keys.D3 | Keys.Shift; break; //number sign? #
					case 0x24: keyval = Keys.D4 | Keys.Shift; break; //dollar
					case 0x25: keyval = Keys.D5 | Keys.Shift; break; //percent
					case 0x26: keyval = Keys.D7 | Keys.Shift; break; //ampersand
					case 0x27: keyval = Keys.OemQuotes; break; //apostrophe
					case 0x28: keyval = Keys.D9 | Keys.Shift; break; //parenleft
					case 0x29: keyval = Keys.D0 | Keys.Shift; break; //parenright
					case 0x2a: keyval = Keys.D8 | Keys.Shift; break; //askerisk
					case 0x2b: keyval = Keys.Oemplus | Keys.Shift; break; //plus
					case 0x2c: keyval = Keys.Oemcomma; break; //comma
					case 0x2d: keyval = Keys.OemMinus; break; //minus
					case 0x2e: keyval = Keys.OemPeriod; break; //period
					case 0x2f: keyval = Keys.OemQuestion; break; //slash
					//digits handled above
					case 0x3a: keyval = Keys.OemSemicolon | Keys.Shift; break; //colon
					case 0x3b: keyval = Keys.OemSemicolon; break; //semicolon
					case 0x3c: keyval = Keys.Oemcomma | Keys.Shift; break; //less than
					case 0x3d: keyval = Keys.Oemplus; break; //equals
					case 0x3e: keyval = Keys.OemPeriod | Keys.Shift; break; //greater than
					case 0x3f: keyval = Keys.OemQuestion | Keys.Shift; break; //question mark
					case 0x40: keyval = Keys.D2 | Keys.Shift; break; //commercial at
					//capital letters handled above
					case 0x5b: keyval = Keys.OemOpenBrackets; break; //left square bracker
					case 0x5c: keyval = Keys.OemBackslash; break; //backslash
					case 0x5d: keyval = Keys.OemCloseBrackets; break; //right square bracker
					case 0x5e: keyval = Keys.D6 | Keys.Shift; break; //CIRCUMFLEX ACCENT
					case 0x5f: keyval = Keys.OemMinus | Keys.Shift; break; //underscore
					case 0x60: keyval = Keys.Oemtilde; break; //grave accent
					//small letters handled above
					case 0x7b: keyval = Keys.OemOpenBrackets | Keys.Shift; break; //left curly bracket
					case 0x7c: keyval = Keys.OemBackslash | Keys.Shift; break; //vertical line
					case 0x7d: keyval = Keys.OemCloseBrackets | Keys.Shift; break; //right curly bracket
					case 0x7e: keyval = Keys.Oemtilde | Keys.Shift; break; //tilde
					//blah blah
					//experimental:
					case 0xfe03: keyval = Keys.RMenu; break; //Alt gr or XK_ISO_Level3_Shift
				}
			switch (keyval) {
				case Keys.LShiftKey:
				case Keys.RShiftKey:
				case Keys.ShiftKey:
					if (down) modifierKeys |= Keys.Shift; else modifierKeys &= ~Keys.Shift;
					break;
				case Keys.LControlKey:
				case Keys.RControlKey:
				case Keys.ControlKey:
					if (down) modifierKeys |= Keys.Control; else modifierKeys &= ~Keys.Control;
					break;
				case Keys.LMenu:
				case Keys.RMenu:
				case Keys.Menu:
					if (down) modifierKeys |= Keys.Alt; else modifierKeys &= ~Keys.Alt;
					break;
			}
			keyval |= modifierKeys;
			if (down && KeyDown != null) KeyDown(this, new KeyEventArgs(keyval));
			else if (!down && KeyUp != null) KeyUp(this, new KeyEventArgs(keyval));
		}
		private void ReceiveMessageMouse() {
			Byte[] b = ReceiveAll(5);
			Point p = new Point((b[1] << 8) | b[2], (b[3] << 8) | b[4]);
			MouseButtons mb = MouseButtons.None;
			if ((b[0] & 1) != 0) mb |= MouseButtons.Left;
			if ((b[0] & 2) != 0) mb |= MouseButtons.Middle;
			if ((b[0] & 4) != 0) mb |= MouseButtons.Right;
			if ((b[0] & 32) != 0) mb |= MouseButtons.XButton1;
			if ((b[0] & 64) != 0) mb |= MouseButtons.XButton2;
			int dd = 0;
			if ((b[0] & 8) != 0) dd = 120;
			if ((b[0] & 16) != 0) dd = -120;
			//Console.WriteLine("PointerEvent x={0} y={1} buttons={2} delta={3}", p.X, p.Y, mb, dd);
			foreach (MouseButtons mbi in Enum.GetValues(typeof(MouseButtons))) if ((mousebuttons & mbi) != 0 && (mb & mbi) == 0) if (MouseUp != null) MouseUp(this, new MouseEventArgs(mbi, 0, p.X, p.Y, 0));
			if ((dd != 0 || mousePosition != p) && MouseMove != null) MouseMove(this, new MouseEventArgs(mb & mousebuttons, 0, p.X, p.Y, dd));
			foreach (MouseButtons mbi in Enum.GetValues(typeof(MouseButtons))) if ((mousebuttons & mbi) == 0 && (mb & mbi) != 0) if (MouseDown != null) MouseDown(this, new MouseEventArgs(mbi, 0, p.X, p.Y, 0));
			mousePosition = p;
			mousebuttons = mb;
		}

		private void FBUpdate(Object sender, VNCFramebufferUpdateEventArgs e) {
			if (e.Framebuffer != framebuffer) return;
			Rectangle r = e.Area;
			r.Intersect(new Rectangle(0, 0, e.Framebuffer.Width, e.Framebuffer.Height));
			if (r.Width == 0 || r.Height == 0) return;
			Boolean send;
			lock (dirtyrects) {
				for (int i = 0; i < dirtyrects.Count; i++) {
					Rectangle r2 = dirtyrects[i];
					if (r.IntersectsWith(r2)) {
						dirtyrects.RemoveAt(i);
						i = -1;
						r = Rectangle.Union(r, r2);
					}
				}
				dirtyrects.Add(r);
				send = waitingforupdate > 0;
			}
			if (send) {
				Interlocked.Decrement(ref waitingforupdate);
				SendQueuedRectangles(waitingforupdaterect, false);
			}
		}

		private void ClearIntersectingQueuedRectangles(Rectangle r) {
			lock (dirtyrects) {
				for (int i = 0; i < dirtyrects.Count; i++) {
					Rectangle r2 = dirtyrects[i];
					if (!r2.IntersectsWith(r)) continue;
					dirtyrects.RemoveAt(i);
					i--;
				}
				dirtyrects.TrimExcess();
			}
		}
		private void SendQueuedRectangles(Rectangle r, Boolean incremental) {
			lock (socket) {
				if (resized) {
					resized = false;
					jpegCounter = 0;
					VNCFramebuffer fb = framebuffer;
					SendUInt8(0); //FramebufferUpdate
					SendUInt8(0); //Padding
					if (Array.IndexOf(SupportedEncodings, -223) == -1) {
						Console.Error.WriteLine("VNC: Desktop size update not supported");
						SendUInt16(fb == null ? (UInt16)1 : (UInt16)2); //Number of rectangles
						int[] empty = new int[r.Width * r.Height];
						SendFBRectangle(empty, new Rectangle(0, 0, r.Width, r.Height), r.Width);
						ClearIntersectingQueuedRectangles(r);
						blurryrect = Rectangle.Empty;
						if (fb != null) SendFBRectangle(fb.Framebuffer, r, fb.Width);
					} else {
						if (fb != null) {
							Width = fb.Width;
							Height = fb.Height;
						}
						Console.Error.WriteLine("VNC: Sending desktop size update {0}x{1}", Width, Height);
						SendUInt16(1); //Number of rectangles
						SendUInt16(0);
						SendUInt16(0);
						SendUInt16((UInt16)Width);
						SendUInt16((UInt16)Height);
						SendUInt32(unchecked((UInt32)(-223))); //Encoding type
					}
					FlushSendBuffer();
				} else {
					if (UpdateRequested != null) UpdateRequested(this, new EventArgs());
					VNCFramebuffer fb = framebuffer;
					if (!incremental) {
						jpegCounter = 0;
						ClearIntersectingQueuedRectangles(r);
						blurryrect = Rectangle.Empty;
						SendUInt8(0); //FramebufferUpdate
						SendUInt8(0); //Padding
						SendUInt16(1); //Number of rectangles
						SendFBRectangle(fb.Framebuffer, r, fb.Width);
					} else { //incremental
						List<Rectangle> sending = new List<Rectangle>();
						lock (dirtyrects) {
							if (dirtyrects.Count == 0) {
								if (jpegCounter > 0) jpegCounter--;
							} else {
								if (jpegCounter < 10) jpegCounter++;
							}
							for (int i = 0; i < dirtyrects.Count; i++) {
								Rectangle r2 = dirtyrects[i];
								if (!r2.IntersectsWith(r)) continue;
								dirtyrects.RemoveAt(i);
								i--;
								r2.Intersect(r);
								sending.Add(r2);
							}
							dirtyrects.TrimExcess();
							if (sending.Count == 0 && blurryrect.IsEmpty) {
								Interlocked.Increment(ref waitingforupdate);
								waitingforupdaterect = r;
							}
						}
						if (sending.Count > 0 || !blurryrect.IsEmpty) {
							SendUInt8(0); //FramebufferUpdate
							SendUInt8(0); //Padding
							SendUInt16((UInt16)(sending.Count + (blurryrect.IsEmpty ? 0 : 1))); //Number of rectangles
							if (!blurryrect.IsEmpty) {
								//The idea here is to use a lossless compression for a small area to "recover" textual/static content from the JPEG artifacts
								//Only a small area is updated here each time because compressing a full frame takes too much CPU time
								Rectangle fixrect = blurryrect;
								if (blurryrecoveryline < fixrect.Top) blurryrecoveryline = fixrect.Top;
								else if (blurryrecoveryline >= fixrect.Bottom) blurryrecoveryline = fixrect.Top;
								fixrect.Intersect(new Rectangle(0, blurryrecoveryline, Int16.MaxValue, 10));
								if (fixrect.IsEmpty) fixrect = blurryrect;
								int oldjpeg = jpegCounter;
								jpegCounter = 0;
								SendFBRectangle(fb.Framebuffer, Rectangle.Intersect(fixrect, r), fb.Width);
								jpegCounter = oldjpeg;
								blurryrecoveryline = fixrect.Bottom;
								if (fixrect.Top <= blurryrect.Top && fixrect.Bottom >= blurryrect.Top) {
									blurryrect.Intersect(new Rectangle(0, fixrect.Bottom, Int16.MaxValue, Int16.MaxValue));
								} else if (fixrect.Top <= blurryrect.Bottom && fixrect.Bottom >= blurryrect.Bottom) {
									blurryrect.Intersect(new Rectangle(0, 0, fixrect.Top, Int16.MaxValue));
								}
								if (blurryrect.Height == 0) blurryrect = Rectangle.Empty;
							}
							foreach (Rectangle r2 in sending) {
								SendFBRectangle(fb.Framebuffer, r2, fb.Width);
							}
						} else {
							if (WaitingForUpdate != null) WaitingForUpdate(this, new EventArgs());
						}
					}
				}
				FlushSendBuffer();
			}
		}

		private void SendFBRectangle(Int32[] fb, Rectangle r, Int32 fbwidth) {
			r.Intersect(new Rectangle(0, 0, fbwidth, fb.Length / fbwidth));
			r.Intersect(new Rectangle(0, 0, Width, Height));
			Boolean sent = false;
			foreach (Int32 enc in SupportedEncodings) {
				switch (enc) {
					case 0:
						SendFBRectangleRaw(fb, r, fbwidth); sent = true; break;
					case 2:
						if (r.Height < 8) break;
						SendFBRectangleRRE(fb, r, fbwidth); sent = true; break;
					case 5:
						if (r.Width < 16 || r.Height < 16) break;
						SendFBRectangleHextile(fb, r, fbwidth); sent = true; break;
					case 6:
						sent = SendFBRectangleZLib(fb, r, fbwidth);
						break;
					case 7:
						sent = SendFBRectangleTight(fb, r, fbwidth);
						break;
				}
				if (sent) break;
			}
			if (!sent) SendFBRectangleRaw(fb, r, fbwidth);
		}
		private void SendFBRectangleHeader(Rectangle r, UInt32 encoding) {
			SendUInt16((UInt16)r.X);
			SendUInt16((UInt16)r.Y);
			SendUInt16((UInt16)r.Width);
			SendUInt16((UInt16)r.Height);
			SendUInt32(encoding);
		}
		IZLibCompressor TightZLib = null;
		private unsafe Boolean SendFBRectangleTight(Int32[] framebuffer, Rectangle r, Int32 fbwidth) {
			if (jpegCounter > 3 && r.Width * r.Height > 100 * 100) {
				SendFBRectangleHeader(r, 7);
				SendUInt8(0x90); //Jpeg encoding
				MemoryStream jpeg = new MemoryStream();
				if (r.Width > 0 && r.Height > 0) {
					fixed (int* fbptr = framebuffer) {
						using (Bitmap bmp = new Bitmap(r.Width, r.Height, fbwidth * 4, PixelFormat.Format32bppRgb, (IntPtr)(fbptr + (r.Top * fbwidth + r.Left)))) {
							ImageCodecInfo ici = Array.Find(ImageCodecInfo.GetImageEncoders(), delegate(ImageCodecInfo item) { return item.FormatID == ImageFormat.Jpeg.Guid; });
							EncoderParameters ep = new EncoderParameters(1);
							ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 50L);
							bmp.Save(jpeg, ici, ep);
							//bmp.Save(jpeg, ImageFormat.Jpeg);
						}
					}
				}
				jpeg.Seek(0, SeekOrigin.Begin);
				int length = (int)jpeg.Length;
				Byte b1 = (Byte)(length & 0x7F);
				Byte b2 = (Byte)((length >> 7) & 0x7F);
				Byte b3 = (Byte)((length >> 14) & 0x7F);
				if (b3 != 0) b2 |= 0x80;
				if (b2 != 0) b1 |= 0x80;
				SendUInt8(b1);
				if (b2 != 0) SendUInt8(b2);
				if (b3 != 0) SendUInt8(b3);
				SendAll(jpeg.ToArray());
				blurryrect = blurryrect.IsEmpty ? r : Rectangle.Union(blurryrect, r);
			} else {
				if (TightZLib == null && ZLibFactory == null) return false;
				SendFBRectangleHeader(r, 7);
				SendUInt8(0x00); //Basic encoding, copy filter (raw), zlib stream 0
				Byte[] row;
				int i = 0;
				if (pixelformat.BitsPerPixel == 32 && pixelformat.ColorDepth == 24) {
					row = new Byte[r.Width * r.Height * 3];
					for (int y = r.Top; y < r.Bottom; y++) {
						for (int x = r.Left; x < r.Right; x++) {
							UInt32 pixel = (UInt32)framebuffer[y * fbwidth + x];
							row[i++] = (Byte)(pixel >> 16);
							row[i++] = (Byte)(pixel >> 8);
							row[i++] = (Byte)(pixel >> 0);
						}
					}
				} else {
					row = new Byte[r.Width * r.Height * pixelformat.BitsPerPixel / 8];
					for (int y = r.Top; y < r.Bottom; y++) {
						for (int x = r.Left; x < r.Right; x++) {
							UInt32 pixel = (UInt32)framebuffer[y * fbwidth + x];
							UInt32 encoded = 0;
							encoded |= ((((pixel >> 16) & 0xff) * (pixelformat.RedMax + 1u) / 256) & pixelformat.RedMax) << pixelformat.RedShift;
							encoded |= ((((pixel >> 8) & 0xff) * (pixelformat.GreenMax + 1u) / 256) & pixelformat.GreenMax) << pixelformat.GreenShift;
							encoded |= ((((pixel >> 0) & 0xff) * (pixelformat.BlueMax + 1u) / 256) & pixelformat.BlueMax) << pixelformat.BlueShift;
							if (pixelformat.BigEndian) {
								for (int b = pixelformat.BitsPerPixel - 8; b >= 0; b -= 8) row[i++] = (Byte)(encoded >> b);
							} else {
								for (int b = 0; b < pixelformat.BitsPerPixel; b += 8) row[i++] = (Byte)(encoded >> b);
							}
						}
					}
				}
				if (i >= 12) {
					if (TightZLib == null) TightZLib = ZLibFactory();
					Byte[] compressed = TightZLib.Compress(row, 0, i);
					int length = compressed.Length;
					Byte b1 = (Byte)(length & 0x7F);
					Byte b2 = (Byte)((length >> 7) & 0x7F);
					Byte b3 = (Byte)((length >> 14) & 0x7F);
					if (b3 != 0) b2 |= 0x80;
					if (b2 != 0) b1 |= 0x80;
					SendUInt8(b1);
					if (b2 != 0) SendUInt8(b2);
					if (b3 != 0) SendUInt8(b3);
					SendAll(compressed);
				} else {
					SendAll(row, 0, i);
				}
			}
			return true;
		}
		private void SendFBRectangleRaw(Int32[] framebuffer, Rectangle r, int fbwidth) {
			SendFBRectangleHeader(r, 0);
			for (int y = r.Top; y < r.Bottom; y++) {
				Byte[] row = new Byte[r.Width * pixelformat.BitsPerPixel / 8];
				int i = 0;
				for (int x = r.Left; x < r.Right; x++) {
					UInt32 pixel = (UInt32)framebuffer[y * fbwidth + x];
					UInt32 encoded = 0;
					encoded |= ((((pixel >> 16) & 0xff) * (pixelformat.RedMax + 1u) / 256) & pixelformat.RedMax) << pixelformat.RedShift;
					encoded |= ((((pixel >> 8) & 0xff) * (pixelformat.GreenMax + 1u) / 256) & pixelformat.GreenMax) << pixelformat.GreenShift;
					encoded |= ((((pixel >> 0) & 0xff) * (pixelformat.BlueMax + 1u) / 256) & pixelformat.BlueMax) << pixelformat.BlueShift;
					if (pixelformat.BigEndian) {
						for (int b = pixelformat.BitsPerPixel - 8; b >= 0; b -= 8) row[i++] = (Byte)(encoded >> b);
					} else {
						for (int b = 0; b < pixelformat.BitsPerPixel; b += 8) row[i++] = (Byte)(encoded >> b);
					}
				}
				SendAll(row);
			}
		}

		IZLibCompressor ZLibStream = null;
		private Boolean SendFBRectangleZLib(Int32[] framebuffer, Rectangle r, int fbwidth) {
			if (ZLibStream == null && ZLibFactory == null) return false;
			SendFBRectangleHeader(r, 6);
			Byte[] row = new Byte[r.Width * r.Height * pixelformat.BitsPerPixel / 8];
			int i = 0;
			for (int y = r.Top; y < r.Bottom; y++) {
				for (int x = r.Left; x < r.Right; x++) {
					UInt32 pixel = (UInt32)framebuffer[y * fbwidth + x];
					UInt32 encoded = 0;
					encoded |= ((((pixel >> 16) & 0xff) * (pixelformat.RedMax + 1u) / 256) & pixelformat.RedMax) << pixelformat.RedShift;
					encoded |= ((((pixel >> 8) & 0xff) * (pixelformat.GreenMax + 1u) / 256) & pixelformat.GreenMax) << pixelformat.GreenShift;
					encoded |= ((((pixel >> 0) & 0xff) * (pixelformat.BlueMax + 1u) / 256) & pixelformat.BlueMax) << pixelformat.BlueShift;
					if (pixelformat.BigEndian) {
						for (int b = pixelformat.BitsPerPixel - 8; b >= 0; b -= 8) row[i++] = (Byte)(encoded >> b);
					} else {
						for (int b = 0; b < pixelformat.BitsPerPixel; b += 8) row[i++] = (Byte)(encoded >> b);
					}
				}
			}
			if (ZLibStream == null) ZLibStream = ZLibFactory();
			Byte[] compressed = ZLibStream.Compress(row, 0, i);
			SendUInt32((UInt32)compressed.Length);
			SendAll(compressed);
			return true;
		}


		private void SendFBRectangleRRE(Int32[] framebuffer, Rectangle r, int fbwidth) {
			SendFBRectangleHeader(r, 2);
			int basecolor = framebuffer[r.Y * fbwidth + r.X];
			List<Rectangle> subrects = new List<Rectangle>();
			if (false) {
				for (int y = r.Top; y < r.Bottom; y++) {
					int color = basecolor;
					int runstart = r.Left;
					int runlength = 0;
					for (int x = r.Left; x < r.Right; x++) {
						int newcolor = framebuffer[y * fbwidth + x];
						if (color != newcolor) {
							if (color != basecolor && runlength > 0) {
								Rectangle r2 = new Rectangle(runstart, y, runlength, 1);
								if (r2.Y > 0 && framebuffer[(r2.Y - 1) * fbwidth + r2.X] == color) {
									Boolean hasadjacent = false;
									Rectangle adjacent = r2;
									foreach (Rectangle r3 in subrects) {
										if (r3.Left == r2.Left && r3.Width == r2.Width && r3.Top + r3.Height == r2.Top) {
											adjacent = r3;
											hasadjacent = true;
											break;
										}
									}
									if (hasadjacent) {
										subrects.Remove(adjacent);
										r2 = Rectangle.Union(r2, adjacent);
									}
								}
								subrects.Add(r2);
							}
							runstart = x;
							runlength = 0;
							color = newcolor;
						}
						runlength++;
					}
					if (color != basecolor && runlength > 0) subrects.Add(new Rectangle(runstart, y, runlength, 1));
				}
			} else {
				Queue<Rectangle> remaining = new Queue<Rectangle>();
				remaining.Enqueue(r);
				while (remaining.Count > 0) {
					Rectangle r2 = remaining.Dequeue();
					int color = framebuffer[r2.Y * fbwidth + r2.X];
					int rw = -1, rh = -1;
					for (int x = r2.Left; x < r2.Right && rw == -1; x++) {
						if (color != framebuffer[r2.Y * fbwidth + x]) rw = x - r2.Left;
					}
					if (rw == -1) rw = r2.Width;
					for (int y = r2.Top + 1; y < r2.Bottom && rh == -1; y++) {
						Boolean success = true;
						for (int x = r2.Left; x < r2.Left + rw && success; x++) {
							if (color != framebuffer[y * fbwidth + x]) success = false;
						}
						if (!success) rh = y - r2.Top;
					}
					if (rh == -1) rh = r2.Height;
					if (rw != 0 && rh != 0) subrects.Add(new Rectangle(r2.X, r2.Y, rw, rh));
					//if (r2.Width - rw > 0 && rh != 0) remaining.Enqueue(new Rectangle(r2.X + rw, r2.Y, r2.Width - rw, rh));
					if (r2.Height - rh > 0 && rw != 0) remaining.Enqueue(new Rectangle(r2.X, r2.Y + rh, rw, r2.Height - rh));
					//if (r2.Width - rw > 0 && r2.Height - rh > 0) remaining.Enqueue(new Rectangle(r2.X + rw, r2.Y + rh, r2.Width - rw, r2.Height - rh));
					//if (r2.Height - rh > 0) remaining.Enqueue(new Rectangle(r2.X, r2.Y + rh, r2.Width, r2.Height - rh));
					if (r2.Width - rw > 0) remaining.Enqueue(new Rectangle(r2.X + rw, r2.Y, r2.Width - rw, r2.Height));
				}
			}
			SendUInt32((UInt32)subrects.Count);
			SendPixel(basecolor);
			foreach (Rectangle r2 in subrects) {
				SendPixel(framebuffer[r2.Y * fbwidth + r2.X]);
				SendUInt16((UInt16)(r2.Left - r.Left));
				SendUInt16((UInt16)(r2.Top - r.Top));
				SendUInt16((UInt16)r2.Width);
				SendUInt16((UInt16)r2.Height);
			}
		}
		private void SendFBRectangleHextile(Int32[] framebuffer, Rectangle r, int fbwidth) {
			SendFBRectangleHeader(r, 5);
			const int hextileRaw = 1;
			const int hextileBgSpecified = 2;
			const int hextileFgSpecified = 4;
			const int hextileAnySubrects = 8;
			const int hextileSubrectsColoured = 16;
			int oldBg = 0, oldFg = 0;
			bool oldBgValid = false;
			bool oldFgValid = false;
			Rectangle t = new Rectangle();
			for (t.Y = r.Top; t.Top < r.Bottom; t.Y += 16) {
				t.Height = Math.Min(r.Bottom, t.Top + 16) - t.Top;
				for (t.X = r.Left; t.Left < r.Right; t.X += 16) {
					t.Width = Math.Min(r.Right, t.Left + 16) - t.Left;
					int tileType = 0;
					int bg = framebuffer[t.Y * fbwidth + t.X];
					int fg = 0;
					if (!oldBgValid || oldBg != bg) {
						tileType |= hextileBgSpecified;
						oldBg = bg;
						oldBgValid = true;
					}
					Boolean foundfg = false;
					Boolean foundcol = false;
					int subrects = 0;
					for (int y = t.Top; y < t.Bottom; y++) {
						int color = bg;
						int length = 0;
						for (int x = t.Left; x < t.Right; x++) {
							int pixel = framebuffer[y * fbwidth + x];
							if (pixel == bg) {
							} else if (!foundfg) {
								fg = pixel;
								foundfg = true;
							} else if (pixel == fg) {
							} else {
								foundcol = true;
							}
							if (color != pixel && length > 0) {
								if (color != bg) subrects++;
								length = 0;
							}
							length++;
							color = pixel;
						}
						if (length > 0 && color != bg) subrects++;
					}
					if (foundcol) {
						tileType |= hextileSubrectsColoured | hextileAnySubrects;
						oldFgValid = false;
					} else if (foundfg) {
						tileType |= hextileAnySubrects;
						if (!oldFgValid || oldFg != fg) {
							tileType |= hextileFgSpecified;
							oldFg = fg;
							oldFgValid = true;
						}
					}
					int encbytes = 0;
					if ((tileType & hextileBgSpecified) != 0) encbytes += pixelformat.BitsPerPixel / 8;
					if ((tileType & hextileFgSpecified) != 0) encbytes += pixelformat.BitsPerPixel / 8;
					if ((tileType & hextileAnySubrects) != 0) {
						int pertile = 2;
						if ((tileType & hextileSubrectsColoured) != 0) pertile += pixelformat.BitsPerPixel / 8;
						encbytes += pertile * subrects;
					}
					if (t.Width * t.Height * pixelformat.BitsPerPixel / 8 <= encbytes) {
						SendUInt8(hextileRaw);
						for (int y = t.Top; y < t.Bottom; y++) for (int x = t.Left; x < t.Right; x++) SendPixel(framebuffer[y * fbwidth + x]);
						oldBgValid = oldFgValid = false;
						continue;
					}
					SendUInt8((Byte)tileType);
					if ((tileType & hextileBgSpecified) != 0) SendPixel(bg);
					if ((tileType & hextileFgSpecified) != 0) SendPixel(fg);
					if ((tileType & hextileAnySubrects) != 0) {
						SendUInt8((Byte)subrects);
						int subrectsa = 0;
						for (int y = t.Top; y < t.Bottom; y++) {
							int color = bg;
							int length = 0;
							int start = 0;
							for (int x = t.Left; x < t.Right; x++) {
								int newcolor = framebuffer[y * fbwidth + x];
								if (color != newcolor && length > 0) {
									if (color != bg && subrectsa < subrects) {
										if ((tileType & hextileSubrectsColoured) != 0) SendPixel(color);
										SendUInt8((Byte)(((start & 0xF) << 4) | ((y - t.Top) & 0xF)));
										SendUInt8((Byte)((((length - 1) & 0xF) << 4) | (0 & 0xF)));
										subrectsa++;
									}
									length = 0;
									start = x - t.Left;
								}
								length++;
								color = newcolor;
							}
							if (length > 0 && color != bg && subrectsa < subrects) {
								if ((tileType & hextileSubrectsColoured) != 0) SendPixel(color);
								SendUInt8((Byte)(((start & 0xF) << 4) | ((y - t.Top) & 0xF)));
								SendUInt8((Byte)((((length - 1) & 0xF) << 4) | (0 & 0xF)));
								subrectsa++;
							}
						}
						for (int i = subrectsa; i < subrects; i++) {
							if ((tileType & hextileSubrectsColoured) != 0) SendPixel(0);
							SendUInt16(0);
							subrectsa++;
						}
						if (subrects != subrectsa) throw new Exception("subrects != subrectsa");
					}
				}
				//Flush();
			}
		}

		private void SendPixel(int pixeli) {
			UInt32 encoded = 0;
			UInt32 pixel = (UInt32)pixeli;
			encoded |= ((((pixel >> 16) & 0xff) * (pixelformat.RedMax + 1u) / 256) & pixelformat.RedMax) << pixelformat.RedShift;
			encoded |= ((((pixel >> 8) & 0xff) * (pixelformat.GreenMax + 1u) / 256) & pixelformat.GreenMax) << pixelformat.GreenShift;
			encoded |= ((((pixel >> 0) & 0xff) * (pixelformat.BlueMax + 1u) / 256) & pixelformat.BlueMax) << pixelformat.BlueShift;
			byte[] row = new Byte[pixelformat.BitsPerPixel / 8];
			int i = 0;
			if (pixelformat.BigEndian) {
				for (int b = pixelformat.BitsPerPixel - 8; b >= 0; b -= 8) row[i++] = (Byte)(encoded >> b);
			} else {
				for (int b = 0; b < pixelformat.BitsPerPixel; b += 8) row[i++] = (Byte)(encoded >> b);
			}
			SendAll(row);
		}

		private void SendPixelFormat(RFBPixelFormat pf) {
			SendUInt8(pf.BitsPerPixel); //bits per pixel
			SendUInt8(pf.ColorDepth); //depth
			SendUInt8(pf.BigEndian ? (Byte)1 : (Byte)0); //big endian
			SendUInt8(pf.TrueColor ? (Byte)1 : (Byte)0); //true color
			SendUInt16(pf.RedMax); //red max
			SendUInt16(pf.GreenMax); //green max
			SendUInt16(pf.BlueMax); //blue max
			SendUInt8(pf.RedShift); //red shift
			SendUInt8(pf.GreenShift); //green shift
			SendUInt8(pf.BlueShift); //blue shift
			SendUInt8(0); //padding
			SendUInt8(0); //padding
			SendUInt8(0); //padding
		}
		private RFBPixelFormat ReceivePixelFormat() {
			Byte[] b = ReceiveAll(16);
			RFBPixelFormat pf = new RFBPixelFormat();
			pf.BitsPerPixel = b[0];
			pf.ColorDepth = b[1];
			pf.BigEndian = b[2] != 0;
			pf.TrueColor = b[3] != 0;
			pf.RedMax = (UInt16)((b[4] << 8) | b[5]);
			pf.GreenMax = (UInt16)((b[6] << 8) | b[7]);
			pf.BlueMax = (UInt16)((b[8] << 8) | b[9]);
			pf.RedShift = b[10];
			pf.GreenShift = b[11];
			pf.BlueShift = b[12];
			return pf;
		}

		private void SendUInt32(UInt32 value) {
			SendAll(new Byte[] { (Byte)(value >> 24), (Byte)(value >> 16), (Byte)(value >> 8), (Byte)value });
		}
		private void SendUInt16(UInt16 value) {
			SendAll(new Byte[] { (Byte)(value >> 8), (Byte)value });
		}
		private void SendUInt8(Byte value) {
			SendAll(new Byte[] { value });
		}
		private void SendAll(Byte[] buffer) {
			SendAll(buffer, 0, buffer.Length);
		}

		private void SendAll(Byte[] buffer, int off, int len) {
			if (SendBuffer == null) SendBuffer = new MemoryStream();
			SendBuffer.Write(buffer, off, len);
			if (SendBuffer.Length > 102400) FlushSendBuffer();
		}
		private void FlushSendBuffer() {
			if (SendBuffer == null) return;
			SendBuffer.Seek(0, SeekOrigin.Begin);
			SendBuffer.WriteTo(socket);
			SendBuffer.SetLength(0);
		}

		private Byte ReceiveByte() {
			Byte[] buffer = new Byte[1];
			ReceiveAll(buffer, 0, 1);
			return buffer[0];
		}
		private Byte[] ReceiveAll(int len) {
			Byte[] buffer = new Byte[len];
			ReceiveAll(buffer, 0, len);
			return buffer;
		}
		private void ReceiveAll(Byte[] buffer, int off, int len) {
			while (len > 0) {
				int sent = socket.Read(buffer, off, len);
				if (sent == 0) throw new EndOfStreamException();
				len -= sent;
				off += sent;
			}
		}
	}
}
