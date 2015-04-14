using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

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
		private UThreadPool _ThreadPool;

		public NetworkConnectionList Clients { get; private set; }
		public ModuleCollection Modules { get; private set; }
		public IModule DefaultModule { get; set; }

		public TCPServer() {
			_ThreadPool = UThreadPool.DefaultPool;
			Clients = new NetworkConnectionList();
			Modules = new ModuleCollection();
			DefaultModule = null;
		}

		public void Listen(int port) {
			Listen(AddressFamily.InterNetwork, port);
		}
		public void Listen(AddressFamily af, int port) {
			Socket listener = new Socket(af, SocketType.Stream, ProtocolType.Tcp);
			try {
				listener.Bind(new IPEndPoint(af == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, port));
				listener.Listen(25);
			} catch {
				listener.Close();
				throw;
			}
			lock (listeners) listeners.Add(listener);
			try {
				listener.BeginAccept(AcceptCallback, listener);
			} catch {
				listener.Close();
				lock (listeners) listeners.Remove(listener);
				throw;
			}
		}

		public void Stop() {
			lock (listeners) foreach (Socket listener in listeners) listener.Close();
		}

		public void Close() {
			Close(true);
		}
		public void Close(bool closeClients) {
			Stop();
			if (closeClients) Clients.CloseAll();
		}

		private void AcceptCallback(IAsyncResult ar) {
			Socket listener = (Socket)ar.AsyncState;
			Socket socket = null;
			try {
				socket = listener.EndAccept(ar);
			} catch (ObjectDisposedException) {
				lock (listeners) listeners.Remove(listener);
				return;
			} catch (SocketException ex) {
				Console.WriteLine("TCPServer.AcceptCallback SocketException: " + ex.Message);
				socket = null;
			}
			if (socket != null) {
				try {
					Client client = new Client(socket, this);
					Clients.Add(client);
					if (ClientAccepted != null) ClientAccepted(this, new ClientAcceptedEventArgs(client));
					client.Start(_ThreadPool);
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
			TCPServer server;

			internal Client(Socket Socket, TCPServer Server) : base(Socket) {
				this.server = Server;
				this.Tag = Server;
			}

			internal void Start(UThreadPool Pool) {
				Pool.QueueWorkItem(WorkerProc, null);
			}

			private void WorkerProc(Object state) {
				bool closesocket = true;
				try {
					int magicnumber = -2;
					try {
						ReadTimeout = 5000;
						magicnumber = base.PeekByte();
						if (magicnumber == -1) return;
					} catch (TimeoutException ex) {
						Console.WriteLine("TCPServer: Caught TimeoutException while reading magic number: " + ex.Message);
						return;
					}
					IModule handler;
					if (!server.Modules.TryGetValue((Byte)magicnumber, out handler)) handler = server.DefaultModule;
					if (handler != null) {
						this.Tag = handler;
						closesocket = handler.Accept(this);
					} else {
						Console.WriteLine("TCPServer: Unknown magic number: " + magicnumber.ToString());
					}
				} catch (SocketException ex) {
					Console.WriteLine("TCPServer: Caught SocketException: " + ex.Message);
				} catch (Exception ex) {
					Console.WriteLine("TCPServer: Caught Exception: " + ex.ToString());
				} finally {
					try {
						if (closesocket) Close();
					} catch { }
				}
			}
		}
	}
}
