using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace UCIS.Net {
	public interface ISocket : IDisposable {
		AddressFamily AddressFamily { get; }
		//int Available { get; }
		bool Blocking { get; set; }
		//bool Connected { get; }
		//bool DontFragment { get; set; }
		//bool EnableBroadcast { get; set; }
		//bool ExclusiveAddressUse { get; set; }
		//IntPtr Handle { get; }
		//bool IsBound { get; }
		//LingerOption LingerState { get; set; }
		EndPoint LocalEndPoint { get; }
		//bool MulticastLoopback { get; set; }
		bool NoDelay { get; set; }
		ProtocolType ProtocolType { get; }
		int ReceiveBufferSize { get; set; }
		int ReceiveTimeout { get; set; }
		EndPoint RemoteEndPoint { get; }
		int SendBufferSize { get; set; }
		int SendTimeout { get; set; }
		//SocketType SocketType { get; }
		//short Ttl { get; set; }
		//bool UseOnlyOverlappedIO { get; set; }
		ISocket Accept();
		//bool AcceptAsync(SocketAsyncEventArgs e);
		IAsyncResult BeginAccept(AsyncCallback callback, object state);
		//IAsyncResult BeginAccept(int receiveSize, AsyncCallback callback, object state);
		//IAsyncResult BeginAccept(Socket acceptSocket, int receiveSize, AsyncCallback callback, object state);
		//IAsyncResult BeginConnect(EndPoint remoteEP, AsyncCallback callback, object state);
		//IAsyncResult BeginConnect(IPAddress address, int port, AsyncCallback requestCallback, object state);
		//IAsyncResult BeginConnect(IPAddress[] addresses, int port, AsyncCallback requestCallback, object state);
		//IAsyncResult BeginConnect(string host, int port, AsyncCallback requestCallback, object state);
		//IAsyncResult BeginDisconnect(bool reuseSocket, AsyncCallback callback, object state);
		//IAsyncResult BeginReceive(IList<ArraySegment<byte>> buffers, SocketFlags socketFlags, AsyncCallback callback, object state);
		//IAsyncResult BeginReceive(IList<ArraySegment<byte>> buffers, SocketFlags socketFlags, out SocketError errorCode, AsyncCallback callback, object state);
		IAsyncResult BeginReceive(byte[] buffer, int offset, int size, SocketFlags socketFlags, AsyncCallback callback, object state);
		//IAsyncResult BeginReceive(byte[] buffer, int offset, int size, SocketFlags socketFlags, out SocketError errorCode, AsyncCallback callback, object state);
		//IAsyncResult BeginReceiveFrom(byte[] buffer, int offset, int size, SocketFlags socketFlags, ref EndPoint remoteEP, AsyncCallback callback, object state);
		//IAsyncResult BeginReceiveMessageFrom(byte[] buffer, int offset, int size, SocketFlags socketFlags, ref EndPoint remoteEP, AsyncCallback callback, object state);
		//IAsyncResult BeginSend(IList<ArraySegment<byte>> buffers, SocketFlags socketFlags, AsyncCallback callback, object state);
		//IAsyncResult BeginSend(IList<ArraySegment<byte>> buffers, SocketFlags socketFlags, out SocketError errorCode, AsyncCallback callback, object state);
		IAsyncResult BeginSend(byte[] buffer, int offset, int size, SocketFlags socketFlags, AsyncCallback callback, object state);
		//IAsyncResult BeginSend(byte[] buffer, int offset, int size, SocketFlags socketFlags, out SocketError errorCode, AsyncCallback callback, object state);
		//IAsyncResult BeginSendFile(string fileName, AsyncCallback callback, object state);
		//IAsyncResult BeginSendFile(string fileName, byte[] preBuffer, byte[] postBuffer, TransmitFileOptions flags, AsyncCallback callback, object state);
		//IAsyncResult BeginSendTo(byte[] buffer, int offset, int size, SocketFlags socketFlags, EndPoint remoteEP, AsyncCallback callback, object state);
		void Bind(EndPoint localEP);
		void Close();
		//void Close(int timeout);
		//void Connect(EndPoint remoteEP);
		//void Connect(IPAddress address, int port);
		//void Connect(IPAddress[] addresses, int port);
		//void Connect(string host, int port);
		//bool ConnectAsync(SocketAsyncEventArgs e);
		//void Disconnect(bool reuseSocket);
		//bool DisconnectAsync(SocketAsyncEventArgs e);
		//SocketInformation DuplicateAndClose(int targetProcessId);
		ISocket EndAccept(IAsyncResult asyncResult);
		//Socket EndAccept(out byte[] buffer, IAsyncResult asyncResult);
		//Socket EndAccept(out byte[] buffer, out int bytesTransferred, IAsyncResult asyncResult);
		//void EndConnect(IAsyncResult asyncResult);
		//void EndDisconnect(IAsyncResult asyncResult);
		int EndReceive(IAsyncResult asyncResult);
		//int EndReceive(IAsyncResult asyncResult, out SocketError errorCode);
		//int EndReceiveFrom(IAsyncResult asyncResult, ref EndPoint endPoint);
		//int EndReceiveMessageFrom(IAsyncResult asyncResult, ref SocketFlags socketFlags, ref EndPoint endPoint, out IPPacketInformation ipPacketInformation);
		int EndSend(IAsyncResult asyncResult);
		//int EndSend(IAsyncResult asyncResult, out SocketError errorCode);
		//void EndSendFile(IAsyncResult asyncResult);
		//int EndSendTo(IAsyncResult asyncResult);
		//object GetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName);
		//void GetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, byte[] optionValue);
		//byte[] GetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, int optionLength);
		//int IOControl(int ioControlCode, byte[] optionInValue, byte[] optionOutValue);
		//int IOControl(IOControlCode ioControlCode, byte[] optionInValue, byte[] optionOutValue);
		void Listen(int backlog);
		//bool Poll(int microSeconds, SelectMode mode);
		//int Receive(byte[] buffer);
		//int Receive(IList<ArraySegment<byte>> buffers);
		//int Receive(byte[] buffer, SocketFlags socketFlags);
		//int Receive(IList<ArraySegment<byte>> buffers, SocketFlags socketFlags);
		//int Receive(byte[] buffer, int size, SocketFlags socketFlags);
		//int Receive(IList<ArraySegment<byte>> buffers, SocketFlags socketFlags, out SocketError errorCode);
		int Receive(byte[] buffer, int offset, int size, SocketFlags socketFlags);
		//int Receive(byte[] buffer, int offset, int size, SocketFlags socketFlags, out SocketError errorCode);
		//bool ReceiveAsync(SocketAsyncEventArgs e);
		//int ReceiveFrom(byte[] buffer, ref EndPoint remoteEP);
		//int ReceiveFrom(byte[] buffer, SocketFlags socketFlags, ref EndPoint remoteEP);
		//int ReceiveFrom(byte[] buffer, int size, SocketFlags socketFlags, ref EndPoint remoteEP);
		//int ReceiveFrom(byte[] buffer, int offset, int size, SocketFlags socketFlags, ref EndPoint remoteEP);
		//bool ReceiveFromAsync(SocketAsyncEventArgs e);
		//int ReceiveMessageFrom(byte[] buffer, int offset, int size, ref SocketFlags socketFlags, ref EndPoint remoteEP, out IPPacketInformation ipPacketInformation);
		//bool ReceiveMessageFromAsync(SocketAsyncEventArgs e);
		//int Send(byte[] buffer);
		//int Send(IList<ArraySegment<byte>> buffers);
		//int Send(byte[] buffer, SocketFlags socketFlags);
		//int Send(IList<ArraySegment<byte>> buffers, SocketFlags socketFlags);
		//int Send(byte[] buffer, int size, SocketFlags socketFlags);
		//int Send(IList<ArraySegment<byte>> buffers, SocketFlags socketFlags, out SocketError errorCode);
		int Send(byte[] buffer, int offset, int size, SocketFlags socketFlags);
		//int Send(byte[] buffer, int offset, int size, SocketFlags socketFlags, out SocketError errorCode);
		//bool SendAsync(SocketAsyncEventArgs e);
		//void SendFile(string fileName);
		//void SendFile(string fileName, byte[] preBuffer, byte[] postBuffer, TransmitFileOptions flags);
		//bool SendPacketsAsync(SocketAsyncEventArgs e);
		//int SendTo(byte[] buffer, EndPoint remoteEP);
		//int SendTo(byte[] buffer, SocketFlags socketFlags, EndPoint remoteEP);
		//int SendTo(byte[] buffer, int size, SocketFlags socketFlags, EndPoint remoteEP);
		int SendTo(byte[] buffer, int offset, int size, SocketFlags socketFlags, EndPoint remoteEP);
		//bool SendToAsync(SocketAsyncEventArgs e);
		void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, bool optionValue);
		void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, byte[] optionValue);
		void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, int optionValue);
		void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, object optionValue);
		//void Shutdown(SocketShutdown how);
	}
	public class FWSocket : Socket, ISocket {
		public FWSocket(SocketInformation socketInformation) : base(socketInformation) { }
		public FWSocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType) : base(addressFamily, socketType, protocolType) { }
		public new ISocket Accept() {
			return new FWSocketWrapper(base.Accept());
		}
		public new ISocket EndAccept(IAsyncResult asyncResult) {
			return new FWSocketWrapper(base.EndAccept(asyncResult));
		}
	}
	public class FWSocketWrapper : ISocket {
		public Socket Socket { get; private set; }
		public FWSocketWrapper(Socket socket) {
			this.Socket = socket;
		}
		public AddressFamily AddressFamily {
			get { return Socket.AddressFamily; }
		}
		public bool Blocking {
			get { return Socket.Blocking; }
			set { Socket.Blocking = value; }
		}
		public EndPoint LocalEndPoint {
			get { return Socket.LocalEndPoint; }
		}
		public bool NoDelay {
			get { return Socket.NoDelay; }
			set { Socket.NoDelay = value; }
		}
		public ProtocolType ProtocolType {
			get { return Socket.ProtocolType; }
		}
		public int ReceiveBufferSize {
			get { return Socket.ReceiveBufferSize; }
			set { Socket.ReceiveBufferSize = value; }
		}
		public int ReceiveTimeout {
			get { return Socket.ReceiveTimeout; }
			set { Socket.ReceiveTimeout = value; }
		}
		public EndPoint RemoteEndPoint {
			get { return Socket.RemoteEndPoint; }
		}
		public int SendTimeout {
			get { return Socket.SendTimeout; }
			set { Socket.SendTimeout = value; }
		}
		public int SendBufferSize {
			get { return Socket.SendBufferSize; }
			set { Socket.SendBufferSize = value; }
		}
		public ISocket Accept() {
			return new FWSocketWrapper(Socket.Accept());
		}
		public IAsyncResult BeginAccept(AsyncCallback callback, object state) {
			return Socket.BeginAccept(callback, state);
		}
		public IAsyncResult BeginReceive(byte[] buffer, int offset, int size, SocketFlags socketFlags, AsyncCallback callback, object state) {
			return Socket.BeginReceive(buffer, offset, size, socketFlags, callback, state);
		}
		public IAsyncResult BeginSend(byte[] buffer, int offset, int size, SocketFlags socketFlags, AsyncCallback callback, object state) {
			return Socket.BeginSend(buffer, offset, size, socketFlags, callback, state);
		}
		public void Bind(EndPoint localEP) {
			Socket.Bind(localEP);
		}
		public void Close() {
			Socket.Close();
		}
		public ISocket EndAccept(IAsyncResult asyncResult) {
			return new FWSocketWrapper(Socket.EndAccept(asyncResult));
		}
		public int EndReceive(IAsyncResult asyncResult) {
			return Socket.EndReceive(asyncResult);
		}
		public int EndSend(IAsyncResult asyncResult) {
			return Socket.EndSend(asyncResult);
		}
		public void Listen(int backlog) {
			Socket.Listen(backlog);
		}
		public int Receive(byte[] buffer, int offset, int size, SocketFlags socketFlags) {
			return Socket.Receive(buffer, offset, size, socketFlags);
		}
		public int Send(byte[] buffer, int offset, int size, SocketFlags socketFlags) {
			return Socket.Send(buffer, offset, size, socketFlags);
		}
		public int SendTo(byte[] buffer, int offset, int size, SocketFlags socketFlags, EndPoint remoteEP) {
			return Socket.SendTo(buffer, offset, size, socketFlags, remoteEP);
		}
		public void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, Boolean optionValue) {
			Socket.SetSocketOption(optionLevel, optionName, optionValue);
		}
		public void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, byte[] optionValue) {
			Socket.SetSocketOption(optionLevel, optionName, optionValue);
		}
		public void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, int optionValue) {
			Socket.SetSocketOption(optionLevel, optionName, optionValue);
		}
		public void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, object optionValue) {
			Socket.SetSocketOption(optionLevel, optionName, optionValue);
		}
		public void Dispose() {
			((IDisposable)Socket).Dispose();
		}
	}
	public class SocketStream : Stream {
		ISocket socket;
		Boolean readable, writeable, ownsSocket;
		public SocketStream(ISocket socket) : this(socket, true) { }
		public SocketStream(ISocket socket, bool ownsSocket) {
			if (socket == null) throw new ArgumentNullException("socket");
			this.readable = this.writeable = true;
			this.ownsSocket = ownsSocket;
			this.socket = socket;
		}
		public override bool CanRead { get { return readable; } }
		public override bool CanSeek { get { return false; } }
		public override bool CanTimeout { get { return true; } }
		public override bool CanWrite { get { return writeable; } }
		//public virtual bool DataAvailable { get { return socket.Available != 0; } }
		public override long Length { get { throw new NotSupportedException(); } }
		public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
		public override int ReadTimeout { get { return socket.ReceiveTimeout; } set { socket.ReceiveTimeout = value; } }
		public override int WriteTimeout { get { return socket.SendTimeout; } set { socket.SendTimeout = value; } }

		public override IAsyncResult BeginRead(byte[] buffer, int offset, int size, AsyncCallback callback, object state) {
			try {
				return socket.BeginReceive(buffer, offset, size, SocketFlags.None, callback, state);
			} catch (Exception ex) {
				if (ex is ThreadAbortException || ex is StackOverflowException || ex is OutOfMemoryException) throw;
				throw new IOException(ex.Message, ex);
			}
		}
		public override IAsyncResult BeginWrite(byte[] buffer, int offset, int size, AsyncCallback callback, object state) {
			try {
				return socket.BeginSend(buffer, offset, size, SocketFlags.None, callback, state);
			} catch (Exception ex) {
				if (ex is ThreadAbortException || ex is StackOverflowException || ex is OutOfMemoryException) throw;
				throw new IOException(ex.Message, ex);
			}
		}
		//public void Close(int timeout);
		protected override void Dispose(bool disposing) {
			if (disposing) {
				readable = writeable = false;
				if (socket != null) {
					if (ownsSocket) {
						//socket.InternalShutdown(SocketShutdown.Both);
						//socket.Close(m_CloseTimeout);
						socket.Close();
					}
				}
			}
			base.Dispose(disposing);
		}
		public override int EndRead(IAsyncResult asyncResult) {
			try {
				return socket.EndReceive(asyncResult);
			} catch (Exception ex) {
				if (ex is ThreadAbortException || ex is StackOverflowException || ex is OutOfMemoryException) throw;
				throw new IOException(ex.Message, ex);
			}
		}
		public override void EndWrite(IAsyncResult asyncResult) {
			try {
				socket.EndSend(asyncResult);
			} catch (Exception ex) {
				if (ex is ThreadAbortException || ex is StackOverflowException || ex is OutOfMemoryException) throw;
				throw new IOException(ex.Message, ex);
			}
		}
		public override void Flush() { }
		public override int Read(byte[] buffer, int offset, int size) {
			try {
				return socket.Receive(buffer, offset, size, SocketFlags.None);
			} catch (Exception ex) {
				if (ex is ThreadAbortException || ex is StackOverflowException || ex is OutOfMemoryException) throw;
				throw new IOException(ex.Message, ex);
			}
		}
		public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
		public override void SetLength(long value) { throw new NotSupportedException(); }
		public override void Write(byte[] buffer, int offset, int size) {
			try {
				//Assume the socket is in blocking mode and all data will be sent before returning
				int sent = socket.Send(buffer, offset, size, SocketFlags.None);
				if (sent < size) throw new EndOfStreamException();
			} catch (Exception ex) {
				if (ex is ThreadAbortException || ex is StackOverflowException || ex is OutOfMemoryException) throw;
				throw new IOException(ex.Message, ex);
			}
		}
	}
}
