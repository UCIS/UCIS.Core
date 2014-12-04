using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

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

		private List<Socket> listeners = new List<Socket>();
		private UCIS.ThreadPool _ThreadPool;
		private NetworkConnectionList _Clients = new NetworkConnectionList();
		private ModuleCollection _Modules = new ModuleCollection();
		private IModule _CatchAllModule = null;

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

		public void Listen(int Port) {
			Listen(AddressFamily.InterNetwork, Port);
		}
		public void Listen(AddressFamily af, int Port) {
			Socket listener = new Socket(af, SocketType.Stream, ProtocolType.Tcp);
			try {
				listener.Bind(new IPEndPoint(af == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, Port));
				listener.Listen(25);
			} catch {
				listener.Close();
				throw;
			}
			lock (listeners) listeners.Add(listener);
			listener.BeginAccept(AcceptCallback, listener);
		}

		public void Stop() {
			lock (listeners) foreach (Socket listener in listeners) listener.Close();
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

		private void AcceptCallback(IAsyncResult ar) {
			Socket listener = (Socket)ar.AsyncState;
			Socket Socket = null;
			try {
				Socket = listener.EndAccept(ar);
			} catch (ObjectDisposedException) {
				lock (listeners) listeners.Remove(listener);
				return;
			} catch (SocketException ex) {
				Console.WriteLine("TCPServer.AcceptCallback SocketException: " + ex.Message);
				Socket = null;
			}
			if (Socket != null) {
				try {
					Client Client = new Client(Socket, this);
					_Clients.Add(Client);
					if (ClientAccepted != null) ClientAccepted(this, new ClientAcceptedEventArgs(Client));
					Client.Start(_ThreadPool);
				} catch (Exception ex) {
					Console.WriteLine(ex.ToString());
				}
			}
			try {
				listener.BeginAccept(AcceptCallback, listener);
			} catch (ObjectDisposedException) {
				lock (listeners) listeners.Remove(listener);
			}
		}

		public interface IModule {
			// Return value: True = Close connection, False = keep alive
			bool Accept(TCPStream Stream);
		}

		private class Client : TCPStream {
			private TCPServer _Server;
			private IModule _Module;
			private byte _MagicNumber;

			public TCPServer Server {
				get { return _Server; }
			}
			public IModule Module {
				get { return _Module; }
			}

			private void _Stream_Closed(object sender, EventArgs e) {
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
				Pool.QueueWorkItem(WorkerProc, null);
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
