using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UCIS.Util;
using SysThreadPool = System.Threading.ThreadPool;

namespace UCIS.Net {
	public class TCPStream : Stream, INetworkConnection {
		private static long _totalBytesRead = 0;
		private static long _totalBytesWritten = 0;
		public static ulong TotalBytesWritten { get { return (ulong)_totalBytesWritten; } set { _totalBytesWritten = (long)value; } }
		public static ulong TotalBytesRead { get { return (ulong)_totalBytesRead; } set { _totalBytesRead = (long)value; } }

		private Socket _Socket;
		private byte _PeekByte;
		private bool _HasPeekByte;
		private ulong _BytesWritten;
		private ulong _BytesRead;
		private DateTime _StartTime;
		private bool _Blocking;

		public event EventHandler Closed;

		public TCPStream(Socket Socket) {
			_Socket = Socket;
			_HasPeekByte = false;
			_StartTime = DateTime.Now;
			_Blocking = _Socket.Blocking;
		}

		public Socket Socket {
			get { return _Socket; }
		}

		public DateTime CreationTime {
			get { return _StartTime; }
		}

		public bool Blocking {
			get { return _Blocking; }
			set {
				Socket.Blocking = value;
				_Blocking = value;
			}
		}

		public bool NoDelay {
			get { return Socket.NoDelay; }
			set { Socket.NoDelay = value; }
		}

		public override bool CanTimeout {
			get { return true; }
		}

		public override int ReadTimeout {
			get { return Socket.ReceiveTimeout; }
			set { Socket.ReceiveTimeout = value; }
		}

		public override int WriteTimeout {
			get { return Socket.SendTimeout; }
			set { Socket.SendTimeout = value; }
		}

		public override int ReadByte() {
			if (_HasPeekByte) {
				_HasPeekByte = false;
				return _PeekByte;
			} else {
				byte[] Buffer = new byte[1];
				if (Read(Buffer, 0, 1) != 1) return -1;
				return Buffer[0];
			}
		}

		public override int Read(byte[] buffer, int offset, int size) {
			if (size < 1) return 0;
			int Count = 0;
			if (_HasPeekByte) {
				buffer[offset] = _PeekByte;
				_HasPeekByte = false;
				Count = 1;
				offset += 1;
				size = 0;
			}
			try {
				if (size > 0) Count += Socket.Receive(buffer, offset, size, SocketFlags.None);
			} catch (SocketException ex) {
				switch (ex.SocketErrorCode) {
					case SocketError.WouldBlock:
						_Socket.Blocking = _Blocking;
						throw new TimeoutException("The receive operation would block", ex);
					case SocketError.TimedOut:
						throw new TimeoutException("The receive operation timed out", ex);
					case SocketError.ConnectionReset:
					case SocketError.Disconnecting:
					case SocketError.NetworkDown:
					case SocketError.NetworkReset:
					case SocketError.NetworkUnreachable:
					case SocketError.NotConnected:
						Close();
						throw new SocketException((int)ex.SocketErrorCode);
					default:
						throw new SocketException((int)ex.SocketErrorCode);
				}
			}

			_BytesRead += (ulong)Count;
			Interlocked.Add(ref _totalBytesRead, (long)Count);
			return Count;
		}

		class AsyncResult : AsyncResultBase {
			public int Count { get; private set; }
			public AsyncResult(AsyncCallback callback, Object state) : base(callback, state) { }
			public void SetCompleted(Boolean synchronously, int cnt) {
				Count = cnt;
				base.SetCompleted(synchronously, null);
			}
		}
		public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state) {
			if (count < 0) {
				AsyncResult ar = new AsyncResult(callback, state);
				ar.SetCompleted(true, 0);
				return ar;
			} else if (_HasPeekByte) {
				buffer[offset] = _PeekByte;
				_HasPeekByte = false;
				AsyncResult ar = new AsyncResult(callback, state);
				ar.SetCompleted(true, 1);
				return ar;
			} else {
				return Socket.BeginReceive(buffer, offset, count, SocketFlags.None, callback, state);
			}
		}
		public override int EndRead(IAsyncResult asyncResult) {
			if (asyncResult is AsyncResult) {
				return ((AsyncResult)asyncResult).Count;
			} else {
				return Socket.EndReceive(asyncResult);
			}
		}

		public int PeekByte() {
			if (_HasPeekByte) {
				return _PeekByte;
			} else {
				int Result = ReadByte();
				if (Result >= 0 && Result <= 255) {
					_PeekByte = (byte)Result;
					_HasPeekByte = true;
				}
				return Result;
			}
		}

		public int WriteBufferSize {
			get { return Socket.SendBufferSize; }
			set { Socket.SendBufferSize = value; }
		}

		public int ReadBufferSize {
			get { return Socket.ReceiveBufferSize; }
			set { Socket.ReceiveBufferSize = value; }
		}

		public override bool CanRead {
			get { return Socket.Connected && (Blocking || (Socket.Available > 0)); }
		}

		public override bool CanSeek {
			get { return false; }
		}

		public override bool CanWrite {
			get { return Socket.Connected; }
		}

		public override void Flush() {
			//Do nothing
			//_Socket.NoDelay = true;
		}

		public override long Length {
			get { throw new NotSupportedException(); }
		}
		public override long Position {
			get { throw new NotSupportedException(); }
			set { throw new NotSupportedException(); }
		}

		public override long Seek(long offset, SeekOrigin origin) {
			throw new NotSupportedException();
		}

		public override void SetLength(long value) {
			throw new NotSupportedException();
		}

		public override void Write(byte[] buffer, int offset, int size) {
			int left = size;
			if (_Socket == null) throw new ObjectDisposedException("socket");
			try {
				while (left > 0) {
					int sent = _Socket.Send(buffer, offset, left, 0);
					left -= sent;
					offset += sent;
				}
			} catch (SocketException ex) {
				switch (ex.SocketErrorCode) {
					case SocketError.WouldBlock:
						_Socket.Blocking = _Blocking;
						throw new TimeoutException("The send operation would block", ex);
					case SocketError.TimedOut:
						throw new TimeoutException("The send operation timed out", ex);
					case SocketError.ConnectionReset:
					case SocketError.Disconnecting:
					case SocketError.NetworkDown:
					case SocketError.NetworkReset:
					case SocketError.NetworkUnreachable:
					case SocketError.NotConnected:
						Close();
						throw new SocketException((int)ex.SocketErrorCode);
					default:
						throw new SocketException((int)ex.SocketErrorCode);
				}
			}
			_BytesWritten += (ulong)size;
			Interlocked.Add(ref _totalBytesWritten, (long)size);
		}

		public override void Close() {
			Socket s = Interlocked.Exchange(ref _Socket, null);
			try {
				if (s != null) {
					try {
						if (s.Connected) s.Shutdown(SocketShutdown.Both);
					} catch { }
					s.Close();
				}
			} finally {
				base.Close();
				if (Closed != null) Closed(this, new EventArgs());
			}
		}

		public bool Connected {
			get {
				if (Socket == null) return false;
				if (Socket.Connected) return true;
				Close();
				return false;
			}
		}

		public object Tag { get; set; }

		public ulong BytesWritten { get { return _BytesWritten; } }
		public ulong BytesRead { get { return _BytesRead; } }
		public TimeSpan Age { get { return DateTime.Now.Subtract(_StartTime); } }
		public EndPoint RemoteEndPoint {
			get {
				if (_Socket == null || !_Socket.Connected) return null;
				try {
					return _Socket.RemoteEndPoint;
				} catch (SocketException) {
					return null;
				}
			}
		}
		//public Object Proxy { get { return null; } }
		Object INetworkConnection.Handler { get { return Tag; } }
	}
}
