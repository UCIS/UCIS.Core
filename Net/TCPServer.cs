using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;

namespace UCIS.Net {
	public class TCPServer {
		public class ModuleCollection : Dictionary<byte, IModule> {
			public void Add(char Key, IModule Value) {
				base.Add((byte)Key, Value);
			}
			public void Remove(char Key) {
				base.Remove((byte)Key);
			}
		}

		public class ClientAcceptedEventArgs : EventArgs {
			private TCPStream _client;
			internal ClientAcceptedEventArgs(TCPStream client) {
				_client = client;
			}
			public TCPStream Client { get { return _client; } }
		}

		public event EventHandler<ClientAcceptedEventArgs> ClientAccepted;

		private Socket _Listener;
		private UCIS.ThreadPool _ThreadPool;
		private NetworkConnectionList _Clients = new NetworkConnectionList();
		private ModuleCollection _Modules = new ModuleCollection();
		private IModule _CatchAllModule = null;
		private Int32 _ThrottleCounter;
		private Int32 _ThrottleBurst = 10;
		private Int32 _ThrottleRate = 0;
		private Timer _ThrottleTimer = null;

		public TCPServer() {
			_ThreadPool = UCIS.ThreadPool.DefaultPool;
		}

		public NetworkConnectionList Clients {
			get { return _Clients; }
		}

		public ModuleCollection Modules {
			get { return _Modules; }
		}

		public IModule CatchAllModule {
			get {
				return _CatchAllModule;
			}
			set {
				_CatchAllModule = value;
			}
		}

		public Int32 ThrottleRate {
			get { return _ThrottleRate; }
			set {
				if (value < 0) throw new ArgumentOutOfRangeException("value");
				_ThrottleRate = value;
				if (_Listener == null) return;
				if (value == 0 && _ThrottleTimer != null) {
					_ThrottleTimer.Dispose();
					_ThrottleTimer = null;
				} else if (value > 0 && _ThrottleTimer == null) {
					_ThrottleTimer = new Timer(ThrottleCallback, null, 1000, 1000);
				}
			}
		}
		public Int32 ThrottleBurst {
			get { return _ThrottleBurst; }
			set {
				if (value < 1) throw new ArgumentOutOfRangeException("value");
				_ThrottleBurst = value;
			}
		}

		public EndPoint LocalEndPoint { get { return _Listener.LocalEndPoint; } }

		public void Listen(int Port) {
			Listen(AddressFamily.InterNetwork, Port);
		}
		public void Listen(AddressFamily af, int Port) {
			Stop();

			_Listener = new Socket(af, SocketType.Stream, ProtocolType.Tcp);

			_Listener.Bind(new IPEndPoint(af == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, Port));
			_Listener.Listen(25);
			_ThrottleCounter = _ThrottleBurst;
			_Listener.BeginAccept(AcceptCallback, null);

			if (_ThrottleRate > 0) {
				_ThrottleTimer = new Timer(ThrottleCallback, null, 1000, 1000);
			}
		}

		public void Stop() {
			if (_ThrottleTimer != null) _ThrottleTimer.Dispose();
			if (_Listener != null && _Listener.IsBound) _Listener.Close();
			_Listener = null;
		}

		public void Close() {
			Close(true);
		}
		public void Close(bool CloseClients) {
			Stop();
			if (CloseClients) {
				_Clients.CloseAll();
				if (_Clients.Count > 0) Console.WriteLine("TCPServer.Close: Warning: " + _Clients.Count.ToString() + " connections were not properly terminated.");
			} else {
				_Clients.Clear();
			}
		}

		private void AcceptCallback(System.IAsyncResult ar) {
			Client Client = null;
			Socket Socket = null;
			if (_Listener == null) return;
			try {
				Socket = _Listener.EndAccept(ar);
			} catch (ObjectDisposedException) {
				Console.WriteLine("TCPServer.AcceptCallback Listener object has been disposed. Aborting.");
				return;
			} catch (SocketException ex) {
				Console.WriteLine("TCPServer.AcceptCallback SocketException: " + ex.Message);
				Socket = null;
			}
			if (Socket != null) {
				try {
					Client = new Client(Socket, this);
					_Clients.Add(Client);
					if (ClientAccepted != null) ClientAccepted(this, new ClientAcceptedEventArgs(Client));
					Client.Start(_ThreadPool);
				} catch (Exception ex) {
					Console.WriteLine(ex.ToString());
				}
			}
			if (_ThrottleCounter > 0 || _ThrottleRate == 0) {
				_ThrottleCounter--;
				_Listener.BeginAccept(AcceptCallback, null);
			}
		}

		private void ThrottleCallback(Object state) {
			if (_Listener == null) return;
			if (_ThrottleRate == 0) return;
			if (_ThrottleCounter >= _ThrottleBurst) return;
			if (_ThrottleCounter <= 0) {
				_Listener.BeginAccept(AcceptCallback, null);
			}
			_ThrottleRate += _ThrottleRate;
		}

		public interface IModule {
			// Return value: True = Close connection, False = keep alive
			bool Accept(TCPStream Stream);
		}

		private class Client : TCPStream {
			private TCPServer _Server;
			private UCIS.ThreadPool.WorkItem _WorkItem;
			private IModule _Module;
			private byte _MagicNumber;

			public TCPServer Server {
				get { return _Server; }
			}
			public IModule Module {
				get { return _Module; }
			}

			private void _Stream_Closed(object sender, EventArgs e) {
				_WorkItem = null;
				_Module = null;
				_Server = null;
				base.Closed -= _Stream_Closed;
			}

			internal Client(Socket Socket, TCPServer Server) : base(Socket) {
				_Server = Server;
				base.Closed += _Stream_Closed;
				this.Tag = Server;
			}

			internal void Start(UCIS.ThreadPool Pool) {
				_WorkItem = Pool.QueueWorkItem(WorkerProc, null);
			}

			private void WorkerProc(object state) {
				bool CloseSocket = true;
				try {
					try {
						//base.NoDelay = true;
						base.ReadTimeout = 5000;
						//Console.WriteLine("TCPServer: Accepted connection from " + base.Socket.RemoteEndPoint.ToString());
						_MagicNumber = (byte)base.PeekByte();
					} catch (TimeoutException ex) {
						Console.WriteLine("TCPServer: Caught TimeoutException while reading magic number: " + ex.Message);
						return;
					}
					if (_Server._Modules.TryGetValue(_MagicNumber, out _Module)) {
						this.Tag = _Module;
						CloseSocket = _Module.Accept(this);
					} else if (_Server._CatchAllModule != null) {
						this.Tag = _Server._CatchAllModule;
						CloseSocket = _Server._CatchAllModule.Accept(this);
					} else {
						this.Tag = this;
						Console.WriteLine("TCPServer: Unknown magic number: " + _MagicNumber.ToString());
					}
				} catch (ThreadAbortException) {
					Console.WriteLine("TCPServer: Caught ThreadAbortException");
				} catch (SocketException ex) {
					Console.WriteLine("TCPServer: Caught SocketException: " + ex.Message);
				} catch (Exception ex) {
					Console.WriteLine("TCPServer: Caught Exception: " + ex.ToString());
				} finally {
					try {
						if (CloseSocket) base.Close();
					} catch { }
				}
			}
		}
	}
}
