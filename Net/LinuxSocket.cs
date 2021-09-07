using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using UCIS.Util;

namespace UCIS.Net {
	public class PosixException : Exception {
		[DllImport("libc.so.6", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.LPStr)]
		static extern IntPtr strerror(int errno);

		public static String GetErrorDescription(int errno) {
			return Marshal.PtrToStringAnsi(strerror(errno));
		}

		public int Code { get; private set; }

		public PosixException() : this(null) { }
		public PosixException(String description) : this(Marshal.GetLastWin32Error(), description) { }
		public PosixException(int errno, String description)
			: base(description == null ? GetErrorDescription(errno) : description + ": " + GetErrorDescription(errno)) {
			this.Code = errno;
		}
	}
	public class LinuxSocket : ISocket, IDisposable {
		const String lib = "libc.so.6";
		[DllImport(lib, SetLastError = true)]
		static extern int socket(int domain, int type, int protocol);
		[DllImport(lib, SetLastError = true)]
		static extern int getsockname(int fd, [Out] Byte[] addr, ref int len);
		[DllImport(lib, SetLastError = true)]
		static extern int getpeername(int fd, [Out] Byte[] addr, ref int len);
		[DllImport(lib, SetLastError = true)]
		static extern int listen(int fd, int n);
		[DllImport(lib, SetLastError = true)]
		static extern int close(int fd);
		[DllImport(lib, SetLastError = true)]
		static extern int shutdown(int fd, int how);
		[DllImport(lib, SetLastError = true)]
		static extern int socketpair(int domain, int type, int protocol, [Out] int[] fds);
		[DllImport(lib, SetLastError = true)]
		static extern int bind(int fd, [In] Byte[] addr, int addrlen);
		[DllImport(lib, SetLastError = true)]
		static extern int connect(int fd, [In] Byte[] addr, int addrlen);
		[DllImport(lib, SetLastError = true)]
		static extern int getsockopt(int fd, int level, int optname, out int optval, ref int optlen);
		[DllImport(lib, SetLastError = true)]
		static extern int getsockopt(int fd, int level, int optname, [Out] Byte[] optval, ref int optlen);
		[DllImport(lib, SetLastError = true)]
		static extern int getsockopt(int fd, int level, int optname, out timeval optval, ref int optlen);
		[DllImport(lib, SetLastError = true)]
		static extern int setsockopt(int fd, int level, int optname, [In] ref int optval, int optlen);
		[DllImport(lib, SetLastError = true)]
		static extern int setsockopt(int fd, int level, int optname, [In] ref timeval optval, int optlen);
		[DllImport(lib, SetLastError = true)]
		static extern int send(int fd, ref Byte buf, UIntPtr n, int flags);
		[DllImport(lib, SetLastError = true)]
		static extern int sendto(int fd, ref Byte buf, UIntPtr n, int flags, [In] Byte[] addr, int addr_len);
		[DllImport(lib, SetLastError = true)]
		static extern int recv(int fd, ref Byte buf, UIntPtr n, int flags);
		[DllImport(lib, SetLastError = true)]
		static extern int recvfrom(int fd, ref Byte buf, UIntPtr n, int flags, [Out] Byte[] addr, ref int addr_len);
		[DllImport(lib, SetLastError = true)]
		static extern int accept4(int fd, IntPtr addr, IntPtr addr_len, int flags);
		unsafe struct timeval {
			public IntPtr seconds;
			public IntPtr microseconds;
		}
		[DllImport(lib, SetLastError = true)]
		static extern int epoll_create1(int flags);
		[DllImport(lib, SetLastError = true)]
		static extern int epoll_ctl(int epfd, int op, int fd, [In] ref epoll_event @event);
		[DllImport(lib, SetLastError = true)]
		static extern int epoll_wait(int epfd, [Out] epoll_event[] events, int maxevents, int timeout);
		[StructLayout(LayoutKind.Sequential, Pack = 4)]
		unsafe struct epoll_event {
			public UInt32 Events;
			public UInt64 Data;
		}
		[DllImport(lib, SetLastError = true)]
		static extern int fcntl(int fd, int cmd, int v);
		[DllImport(lib, SetLastError = true)]
		static extern int isfdtype(int __fd, int __fdtype);

		const String objectname = "LinuxSocket";
		int handle;
		AsyncIOResult recvqhead = null, recvqtail = null;
		AsyncIOResult sendqhead = null, sendqtail = null;
		int asyncreadbusy = 0, asyncwritebusy = 0;
		static int epollfds = 0;
		static Thread epollthread = null;
		static Dictionary<int, LinuxSocket> epollsockets = null;
		static int epollfd = -1;

		private static int ConvertAddressFamily(AddressFamily addressFamily) {
			switch (addressFamily) {
				case AddressFamily.Unspecified: return 0;
				case AddressFamily.InterNetwork: return 2;
				case AddressFamily.InterNetworkV6: return 10;
				case AddressFamily.Unix: return 1;
				default: throw new ArgumentOutOfRangeException("addressFamily");
			}
		}
		private static int ConvertSocketType(SocketType socketType) {
			switch (socketType) {
				case SocketType.Stream: return 1;
				case SocketType.Dgram: return 2;
				case SocketType.Raw: return 3;
				case SocketType.Seqpacket: return 5;
				default: throw new ArgumentOutOfRangeException("socketType");
			}
		}
		private static int ConvertProtocolType(ProtocolType protocolType) {
			switch (protocolType) {
				case ProtocolType.Unspecified: return 0;
				case ProtocolType.Tcp: return 6;
				case ProtocolType.Udp: return 17;
				default: throw new ArgumentOutOfRangeException("protocolType");
			}
		}
		private static Byte[] ConvertSocketAddress(EndPoint ep) {
			if (ep.AddressFamily == AddressFamily.InterNetwork) {
				IPEndPoint ipep = (IPEndPoint)ep;
				Byte[] ret = new Byte[2 + 2 + 4 + 8];
				ret[0] = 0x02; //AF_INET
				ret[1] = 0x00; //AF_INET
				ret[2] = (Byte)(ipep.Port >> 8);
				ret[3] = (Byte)(ipep.Port >> 0);
				ipep.Address.GetAddressBytes().CopyTo(ret, 4);
				return ret;
			} else if (ep.AddressFamily == AddressFamily.InterNetworkV6) {
				IPEndPoint ipep = (IPEndPoint)ep;
				Byte[] ret = new Byte[2 + 2 + 4 + 16 + 4];
				ret[0] = 0x0A; //AF_INET
				ret[1] = 0x00; //AF_INET
				ret[2] = (Byte)(ipep.Port >> 8);
				ret[3] = (Byte)(ipep.Port >> 0);
				ipep.Address.GetAddressBytes().CopyTo(ret, 8);
				BitConverter.GetBytes((Int32)ipep.Address.ScopeId).CopyTo(ret, 24);
				return ret;
			} else {
				throw new NotImplementedException("Address family " + ep.AddressFamily.ToString() + " is not supported.");
			}
		}
		private static EndPoint ConvertSocketAddress(Byte[] addr, int len) {
			if (len < 2) throw new ArgumentException("Address length is too small", "len");
			if (addr[0] == 0x02 && addr[1] == 0x00) {
				if (len < 8) throw new ArgumentException("Address length is too small", "len");
				return new IPEndPoint(new IPAddress(ArrayUtil.Slice(addr, 4, 4)), (UInt16)((addr[2] << 8) | addr[3]));
			} else if (addr[0] == 0x0A && addr[1] == 0x00) {
				if (len < 28) throw new ArgumentException("Address length is too small", "len");
				return new IPEndPoint(new IPAddress(ArrayUtil.Slice(addr, 8, 16), BitConverter.ToInt32(addr, 24)), (UInt16)((addr[2] << 8) | addr[3]));
			} else {
				throw new NotImplementedException("Address family " + BitConverter.ToInt16(addr, 0).ToString() + " is not supported.");
			}
		}
		private static int ConvertFlags(SocketFlags socketFlags) {
			int flags = 0x4000; //NOSIGNAL
			if ((socketFlags & SocketFlags.DontRoute) != 0) flags |= 0x04;
			if ((socketFlags & SocketFlags.OutOfBand) != 0) flags |= 0x01;
			if ((socketFlags & SocketFlags.Partial) != 0) flags |= 0x8000;
			if ((socketFlags & SocketFlags.Peek) != 0) flags |= 0x02;
			if ((socketFlags & SocketFlags.Truncated) != 0) flags |= 0x20;
			return flags;
		}
		public LinuxSocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType) {
			int domain = ConvertAddressFamily(addressFamily);
			int type = ConvertSocketType(socketType);
			int protocol = ConvertProtocolType(protocolType);
			type |= 0x80000; //CLOSE ON EXEC
			handle = socket(domain, type, protocol);
			if (handle == -1) throw new PosixException("socket");
		}
		public LinuxSocket(int handle) {
			if (handle == -1) throw new ObjectDisposedException(objectname);
			if (!IsSocketHandle(handle)) throw new ArgumentException("The handle does not represent an open socket", "handle");
			this.handle = handle;
		}
		public static Boolean IsSocketHandle(int handle) {
			return isfdtype(handle, 0xC000) == 1;
		}
		public static LinuxSocket[] SocketPair(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType) {
			int domain = ConvertAddressFamily(addressFamily);
			int type = ConvertSocketType(socketType);
			int protocol = ConvertProtocolType(protocolType);
			type |= 0x80000; //CLOSE ON EXEC
			int[] fds = new int[2];
			if (socketpair(domain, type, protocol, fds) == -1) throw new PosixException("socketpair");
			return new LinuxSocket[] { new LinuxSocket(fds[0]), new LinuxSocket(fds[1]) };
		}
		public bool CloseOnExec {
			get {
				if (handle == -1) throw new ObjectDisposedException(objectname);
				int flags = fcntl(handle, 1, 0);
				return (flags & 1) == 0;
			}
			set {
				if (handle == -1) throw new ObjectDisposedException(objectname);
				int flags = fcntl(handle, 1, 0);
				if (flags == -1) throw new PosixException("fcntl");
				flags = value ? (flags | 1) : (flags & ~1);
				flags = fcntl(handle, 2, flags);
				if (flags == -1) throw new PosixException("fcntl");
			}
		}
		public AddressFamily AddressFamily {
			get {
				if (handle == -1) throw new ObjectDisposedException(objectname);
				int value;
				int len = 4;
				if (getsockopt(handle, 1, 39, out value, ref len) == -1) throw new PosixException("getsockopt"); //SOL_SOCKET, SO_DOMAIN
				if (len != 4) return AddressFamily.Unknown;
				switch (value) {
					case 2: return AddressFamily.InterNetwork;
					case 10: return AddressFamily.InterNetworkV6;
					case 1: return AddressFamily.Unix;
					default: return AddressFamily.Unknown;
				}
			}
		}
		public EndPoint LocalEndPoint {
			get {
				if (handle == -1) throw new ObjectDisposedException(objectname);
				Byte[] buffer = new Byte[32];
				int len = buffer.Length;
				if (getsockname(handle, buffer, ref len) == -1) throw new PosixException("getsockname");
				if (len > buffer.Length) {
					buffer = new Byte[len];
					if (getsockname(handle, buffer, ref len) == -1) throw new PosixException("getsockname");
				}
				return ConvertSocketAddress(buffer, len);
			}
		}
		public bool NoDelay {
			get {
				if (handle == -1) throw new ObjectDisposedException(objectname);
				int value;
				int len = 4;
				if (getsockopt(handle, 6, 1, out value, ref len) == -1) throw new PosixException("getsockopt"); //IPPROTO_TCP, TCP_NODELAY
				return len > 0 && value != 0;
			}
			set {
				SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, value ? 1 : 0);
			}
		}
		public bool Blocking {
			get {
				if (handle == -1) throw new ObjectDisposedException(objectname);
				int flags = fcntl(handle, 3, 0);
				return (flags & 0x800) == 0;
			}
			set {
				if (handle == -1) throw new ObjectDisposedException(objectname);
				int flags = fcntl(handle, 3, 0);
				if (flags == -1) throw new PosixException("fcntl");
				flags = value ? (flags & ~0x800) : (flags | 0x800);
				flags = fcntl(handle, 4, flags);
				if (flags == -1) throw new PosixException("fcntl");
			}
		}
		public ProtocolType ProtocolType {
			get {
				if (handle == -1) throw new ObjectDisposedException(objectname);
				int value;
				int len = 4;
				if (getsockopt(handle, 1, 38, out value, ref len) == -1) throw new PosixException("getsockopt"); //SOL_SOCKET, SO_PROTOCOL
				if (len != 4) return ProtocolType.Unknown;
				switch (value) {
					case 6: return ProtocolType.Tcp;
					case 17: return ProtocolType.Udp;
					default: return ProtocolType.Unknown;
				}
			}
		}
		public EndPoint RemoteEndPoint {
			get {
				if (handle == -1) throw new ObjectDisposedException(objectname);
				Byte[] buffer = new Byte[32];
				int len = buffer.Length;
				if (getpeername(handle, buffer, ref len) == -1) throw new PosixException("getpeername");
				if (len > buffer.Length) {
					buffer = new Byte[len];
					if (getpeername(handle, buffer, ref len) == -1) throw new PosixException("getpeername");
				}
				return ConvertSocketAddress(buffer, len);
			}
		}
		public SocketType SocketType {
			get {
				if (handle == -1) throw new ObjectDisposedException(objectname);
				int value;
				int len = 4;
				if (getsockopt(handle, 1, 3, out value, ref len) == -1) throw new PosixException("getsockopt"); //SOL_SOCKET, SO_TYPE
				if (len != 4) return SocketType.Unknown;
				switch (value) {
					case 1: return SocketType.Stream;
					case 2: return SocketType.Dgram;
					case 3: return SocketType.Raw;
					case 5: return SocketType.Seqpacket;
					default: return SocketType.Unknown;
				}
			}
		}
		public ISocket Accept() {
			if (this.handle == -1) throw new ObjectDisposedException(objectname);
			int handle = accept4(this.handle, IntPtr.Zero, IntPtr.Zero, 0x80000);
			if (handle == -1) throw new PosixException("accept4");
			return new LinuxSocket(handle);
		}
		public void Bind(EndPoint localEP) {
			if (handle == -1) throw new ObjectDisposedException(objectname);
			Byte[] addr = ConvertSocketAddress(localEP);
			SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
			if (bind(handle, addr, addr.Length) == -1) throw new PosixException("bind");
		}
		public void Close() {
			Dispose();
		}
		public void Connect(EndPoint remoteEP) {
			Byte[] addr = ConvertSocketAddress(remoteEP);
			if (handle == -1) throw new ObjectDisposedException(objectname);
			if (connect(handle, addr, addr.Length) == -1) throw new PosixException("connect");
		}
		public void Connect(IPAddress address, int port) {
			Connect(new IPEndPoint(address, port));
		}
		//public void Connect(IPAddress[] addresses, int port);
		//public void Connect(string host, int port);
		protected virtual void Dispose(bool disposing) {
			int fd = handle;
			handle = -1;
			if (fd != -1) {
				Dictionary<int, LinuxSocket> sockmap;
				lock (typeof(LinuxSocket)) {
					if (epollfd != -1) {
						epoll_event evt = new epoll_event();
						epoll_ctl(epollfd, 2, fd, ref evt);
					}
					sockmap = epollsockets;
				}
				if (sockmap != null) lock (sockmap) sockmap.Remove(fd);
				close(fd);
			}
		}
		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		~LinuxSocket() {
			Dispose(false);
		}
		public void Listen(int backlog) {
			if (handle == -1) throw new ObjectDisposedException(objectname);
			if (listen(handle, backlog) == -1) throw new PosixException("listen");
		}
		public int Receive(byte[] buffer) {
			return Receive(buffer, 0, buffer.Length, SocketFlags.None);
		}
		public int Receive(byte[] buffer, SocketFlags socketFlags) {
			return Receive(buffer, 0, buffer.Length, socketFlags);
		}
		public int Receive(byte[] buffer, int size, SocketFlags socketFlags) {
			return Receive(buffer, 0, size, socketFlags);
		}
		public int Receive(byte[] buffer, int offset, int size, SocketFlags socketFlags) {
			if (buffer == null) throw new ArgumentNullException("buffer");
			if (offset < 0 || size < 0 || offset + size > buffer.Length) throw new ArgumentOutOfRangeException("offset");
			int flags = ConvertFlags(socketFlags);
			if (handle == -1) throw new ObjectDisposedException(objectname);
			int ret = recv(handle, ref buffer[offset], (UIntPtr)size, flags);
			if (ret == -1) throw new PosixException("recv");
			return ret;
		}
		public int ReceiveFrom(byte[] buffer, ref EndPoint remoteEP) {
			return ReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref remoteEP);
		}
		public int ReceiveFrom(byte[] buffer, SocketFlags socketFlags, ref EndPoint remoteEP) {
			return ReceiveFrom(buffer, 0, buffer.Length, socketFlags, ref remoteEP);
		}
		public int ReceiveFrom(byte[] buffer, int size, SocketFlags socketFlags, ref EndPoint remoteEP) {
			return ReceiveFrom(buffer, 0, size, socketFlags, ref remoteEP);
		}
		public int ReceiveFrom(byte[] buffer, int offset, int size, SocketFlags socketFlags, ref EndPoint remoteEP) {
			if (buffer == null) throw new ArgumentNullException("buffer");
			if (offset < 0 || size < 0 || offset + size > buffer.Length) throw new ArgumentOutOfRangeException("offset");
			int flags = ConvertFlags(socketFlags);
			if (handle == -1) throw new ObjectDisposedException(objectname);
			Byte[] addr = new Byte[128];
			int addr_len = addr.Length;
			int ret = recvfrom(handle, ref buffer[offset], (UIntPtr)size, flags, addr, ref addr_len);
			if (ret == -1) throw new PosixException("recvfrom");
			if (addr_len < addr.Length) remoteEP = ConvertSocketAddress(addr, addr_len);
			return ret;
		}
		public int Send(byte[] buffer) {
			return Send(buffer, 0, buffer.Length, SocketFlags.None);
		}
		public int Send(byte[] buffer, SocketFlags socketFlags) {
			return Send(buffer, 0, buffer.Length, socketFlags);
		}
		public int Send(byte[] buffer, int size, SocketFlags socketFlags) {
			return Send(buffer, 0, size, socketFlags);
		}
		public int Send(byte[] buffer, int offset, int size, SocketFlags socketFlags) {
			if (buffer == null) throw new ArgumentNullException("buffer");
			if (offset < 0 || size < 0 || offset + size > buffer.Length) throw new ArgumentOutOfRangeException("offset");
			int flags = ConvertFlags(socketFlags);
			if (handle == -1) throw new ObjectDisposedException(objectname);
			int ret = send(handle, ref buffer[offset], (UIntPtr)size, flags);
			if (ret == -1) throw new PosixException("send");
			return ret;
		}
		public int SendTo(byte[] buffer, EndPoint remoteEP) {
			return SendTo(buffer, 0, buffer.Length, SocketFlags.None, remoteEP);
		}
		public int SendTo(byte[] buffer, SocketFlags socketFlags, EndPoint remoteEP) {
			return SendTo(buffer, 0, buffer.Length, socketFlags, remoteEP);
		}
		public int SendTo(byte[] buffer, int size, SocketFlags socketFlags, EndPoint remoteEP) {
			return SendTo(buffer, 0, size, socketFlags, remoteEP);
		}
		public int SendTo(byte[] buffer, int offset, int size, SocketFlags socketFlags, EndPoint remoteEP) {
			if (buffer == null) throw new ArgumentNullException("buffer");
			if (offset < 0 || size < 0 || offset + size > buffer.Length) throw new ArgumentOutOfRangeException("offset");
			if (handle == -1) throw new ObjectDisposedException(objectname);
			Byte[] addr = ConvertSocketAddress(remoteEP);
			int flags = ConvertFlags(socketFlags);
			int ret = sendto(handle, ref buffer[offset], (UIntPtr)size, flags, addr, addr.Length);
			if (ret == -1) throw new PosixException("sendto");
			return ret;
		}
		public void Shutdown(SocketShutdown how) {
			if (handle == -1) throw new ObjectDisposedException(objectname);
			if (shutdown(handle, (int)how) == -1) throw new PosixException("shutdown");
		}
		public int ReceiveBufferSize {
			get { return GetSocketOptionInt(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer); }
			set { SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, value); }
		}
		public int ReceiveTimeout {
			get {
				if (handle == -1) throw new ObjectDisposedException(objectname);
				timeval tv;
				int tvlen = Marshal.SizeOf(typeof(timeval));
				getsockopt(handle, 1, 20, out tv, ref tvlen);
				return (int)tv.seconds * 1000 + (int)tv.microseconds / 1000;
			}
			set {
				if (handle == -1) throw new ObjectDisposedException(objectname);
				timeval tv = new timeval() { seconds = (IntPtr)(value / 1000), microseconds = (IntPtr)((value % 1000) * 1000) };
				setsockopt(handle, 1, 20, ref tv, Marshal.SizeOf(tv));
			}
		}
		public int SendTimeout {
			get {
				if (handle == -1) throw new ObjectDisposedException(objectname);
				timeval tv;
				int tvlen = Marshal.SizeOf(typeof(timeval));
				getsockopt(handle, 1, 21, out tv, ref tvlen);
				return (int)tv.seconds * 1000 + (int)tv.microseconds / 1000;
			}
			set {
				if (handle == -1) throw new ObjectDisposedException(objectname);
				timeval tv = new timeval() { seconds = (IntPtr)(value / 1000), microseconds = (IntPtr)((value % 1000) * 1000) };
				setsockopt(handle, 1, 21, ref tv, Marshal.SizeOf(tv));
			}
		}
		public int SendBufferSize {
			get { return GetSocketOptionInt(SocketOptionLevel.Socket, SocketOptionName.SendBuffer); }
			set { SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, value); }
		}

		private static void ConvertSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, out int level, out int optname) {
			//https://github.com/torvalds/linux/blob/5bfc75d92efd494db37f5c4c173d3639d4772966/tools/include/uapi/asm-generic/socket.h
			switch (optionLevel) {
				case SocketOptionLevel.Socket:
					level = 1;
					switch (optionName) {
						case SocketOptionName.ReuseAddress: optname = 2; break; //SO_REUSEADDR
						case SocketOptionName.DontRoute: optname = 5; break; //SO_DONTROUTE
						case SocketOptionName.Broadcast: optname = 6; break; //SO_BROADCAST
						case SocketOptionName.SendBuffer: optname = 7; break; //SO_SNDBUF
						case SocketOptionName.ReceiveBuffer: optname = 8; break; //SO_RCVBUF
						case SocketOptionName.KeepAlive: optname = 9; break; //SO_KEEPALIVE
						case SocketOptionName.Linger: optname = 13; break; //SO_LINGER
						case SocketOptionName.ReceiveTimeout: optname = 21; break; //SO_RCVTIMEO_OLD	
						case SocketOptionName.SendTimeout: optname = 21; break; //SO_SNDTIMEO_OLD
						default: throw new NotImplementedException();
					}
					break;
				case SocketOptionLevel.Tcp:
					level = 6;
					switch (optionName) {
						case SocketOptionName.NoDelay: optname = 1; break; //TCP_NODELAY
						default: throw new NotImplementedException();
					}
					break;
				default: throw new NotImplementedException();
			}
		}
		public void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, Boolean optionValue) {
			SetSocketOption(optionLevel, optionName, optionValue ? 1 : 0);
		}
		public void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, int optionValue) {
			int level, optname;
			ConvertSocketOption(optionLevel, optionName, out level, out optname);
			if (handle == -1) throw new ObjectDisposedException(objectname);
			if (setsockopt(handle, level, optname, ref optionValue, 4) == -1) throw new PosixException("setsockopt(" + optionLevel + ", " + optionName + ")");
		}
		private int GetSocketOptionInt(SocketOptionLevel optionLevel, SocketOptionName optionName) {
			int level, optname;
			ConvertSocketOption(optionLevel, optionName, out level, out optname);
			int value, len = 4;
			if (getsockopt(handle, level, optname, out value, ref len) == -1) throw new PosixException("getsockopt(" + optionLevel + ", " + optionName + ")");
			return value;
		}

		class AsyncIOResult : AsyncResultBase {
			public LinuxSocket Socket;
			public AsyncIOResult Next = null;
			public Byte[] Buffer;
			public int Offset;
			public int Length;
			public int Transferred = 0;
			public int Flags;
			public int Operation;
			public AsyncIOResult(AsyncCallback callback, Object state) : base(callback, state) { }
			public new void SetCompleted(Boolean synchronously, Exception error) {
				base.SetCompleted(synchronously, error);
			}
			public void Complete() {
				base.WaitForCompletion();
				base.ThrowError();
			}
		}

		public IAsyncResult BeginReceive(byte[] buffer, int offset, int size, SocketFlags socketFlags, AsyncCallback callback, object state) {
			return BeginAsyncIO(0, buffer, offset, size, socketFlags, callback, state);
		}
		public int EndReceive(IAsyncResult asyncResult) {
			return EndAsyncIO(asyncResult);
		}
		public IAsyncResult BeginSend(byte[] buffer, int offset, int size, SocketFlags socketFlags, AsyncCallback callback, object state) {
			return BeginAsyncIO(1, buffer, offset, size, socketFlags, callback, state);
		}
		public int EndSend(IAsyncResult asyncResult) {
			return EndAsyncIO(asyncResult);
		}
		public IAsyncResult BeginAccept(AsyncCallback callback, object state) {
			return BeginAsyncIO(2, new Byte[0], 0, 0, SocketFlags.None, callback, state);
		}
		public ISocket EndAccept(IAsyncResult asyncResult) {
			int ret = EndAsyncIO(asyncResult); ;
			return new LinuxSocket(ret);
		}
		IAsyncResult BeginAsyncIO(int op, byte[] buffer, int offset, int size, SocketFlags socketFlags, AsyncCallback callback, object state) {
			if (buffer == null) throw new ArgumentNullException("buffer");
			if (offset < 0 || size < 0 || offset + size > buffer.Length) throw new ArgumentOutOfRangeException("offset");
			if (handle == -1) throw new ObjectDisposedException(objectname);
			AsyncIOResult ar = new AsyncIOResult(callback, state) {
				Socket = this,
				Buffer = buffer,
				Offset = offset,
				Length = size,
				Flags = ConvertFlags(socketFlags) | 0x40, //flags | MSG_DONTWAIT
				Operation = op,
			};
			Boolean first = false;
			lock (this) {
				if (op == 1) {
					if (sendqtail == null) {
						first = true;
						sendqhead = ar;
						asyncwritebusy = 1;
					} else {
						sendqtail.Next = ar;
					}
					sendqtail = ar;
				} else {
					if (recvqtail == null) {
						first = true;
						recvqhead = ar;
						asyncreadbusy = 1;
					} else {
						recvqtail.Next = ar;
					}
					recvqtail = ar;
				}
			}
			if (first) {
				TryAsyncIO(ar, true);
				if (op == 1) asyncwritebusy = 0; else asyncreadbusy = 0;
				if (!ar.IsCompleted || ar.Next != null) EpollWait();
			}
			return ar;
		}
		int EndAsyncIO(IAsyncResult asyncResult) {
			((AsyncIOResult)asyncResult).Complete();
			return ((AsyncIOResult)asyncResult).Transferred;
		}
		void TryAsyncIO(AsyncIOResult ar, Boolean sync) {
			int read;
			if (handle == -1) {
				AsyncIOSetCompleted(ar, sync, new ObjectDisposedException(objectname));
				return;
			}
			if (ar.Operation == 1) {
				read = send(handle, ref ar.Buffer[ar.Offset], (UIntPtr)ar.Length, ar.Flags);
			} else if (ar.Operation == 2) {
				Boolean wasBlocking = Blocking;
				Blocking = false;
				read = accept4(handle, IntPtr.Zero, IntPtr.Zero, 0x80000);
				var err = read == -1 ? Marshal.GetLastWin32Error() : 0;
				Blocking = wasBlocking;
				if (read != -1) {
					ar.Transferred = read;
					AsyncIOSetCompleted(ar, sync, null);
				} else if (err != 11) {
					AsyncIOSetCompleted(ar, sync, new PosixException(err, "accept4"));
				}
				return;
			} else {
				read = recv(handle, ref ar.Buffer[ar.Offset], (UIntPtr)ar.Length, ar.Flags);
				if (read == 0 && ar.Operation == 0) {
					AsyncIOSetCompleted(ar, sync, null);
					return;
				}
			}
			if (read == -1 && Marshal.GetLastWin32Error() == 11) read = 0;
			if (read < 0) {
				AsyncIOSetCompleted(ar, sync, new PosixException(ar.Operation == 1 ? "send" : "recv"));
			} else {
				ar.Offset += read;
				ar.Length -= read;
				ar.Transferred += read;
				if (ar.Length <= 0 || (ar.Transferred > 0 && ar.Operation == 0)) AsyncIOSetCompleted(ar, sync, null);
			}
		}
		void AsyncIOSetCompleted(AsyncIOResult ar, Boolean synchronous, Exception error) {
			AsyncIOResult call = null;
			lock (this) {
				if (ar.Operation == 1) {
					if (sendqhead == ar) sendqhead = call = ar.Next;
					if (sendqtail == ar) sendqtail = null;
				} else {
					if (recvqhead == ar) recvqhead = call = ar.Next;
					if (recvqtail == ar) recvqtail = null;
				}
				ar.Next = null;
			}
			ar.SetCompleted(synchronous, error);
			if (call != null) {
				TryAsyncIO(call, false);
				if (!call.IsCompleted) EpollWait(); //request was queued while this one was completing (synchronously, possibly chained)
			}
		}

		void EpollWait() {
			Dictionary<int, LinuxSocket> sockmap;
			lock (typeof(LinuxSocket)) {
				if (epollfd == -1) {
					epollfd = epoll_create1(0x80000);
					if (epollfd == -1) throw new PosixException("epoll_create1");
					epollfds = 0;
					epollsockets = new Dictionary<int, LinuxSocket>();
				}
				sockmap = epollsockets;
				if (epollthread == null) {
					epollthread = new Thread(EpollLoop);
					epollthread.IsBackground = true;
					epollthread.Start();
				}
			}
			lock (sockmap) sockmap[handle] = this;
			lock (this) {
				uint mode = (1 << 30); //ONESHOT
				if (recvqhead != null) mode |= 0x001;
				if (sendqhead != null) mode |= 0x004;
				if ((mode & 0x005) == 0) return;
				epoll_event evt = new epoll_event() { Events = mode, Data = (UInt64)handle };
				int ret = epoll_ctl(epollfd, 1, handle, ref evt); //ADD
				if (ret != -1) Interlocked.Increment(ref epollfds);
				if (ret == -1 && Marshal.GetLastWin32Error() == 17) ret = epoll_ctl(epollfd, 3, handle, ref evt); //MODIFY
				if (ret == -1) throw new PosixException("epoll_ctl");
			}
		}
		void EpollEvent(Boolean read, Boolean write, Boolean error, Boolean closed) {
			if (closed || error) read = write = true;
			if (read && recvqhead != null && Interlocked.CompareExchange(ref asyncreadbusy, 1, 0) == 0) {
				if (recvqhead != null) TryAsyncIO(recvqhead, false);
				asyncreadbusy = 0;
			}
			if (write && sendqhead != null && Interlocked.CompareExchange(ref asyncwritebusy, 1, 0) == 0) {
				if (sendqhead != null) TryAsyncIO(sendqhead, false);
				asyncwritebusy = 0;
			}
			EpollWait();
		}
		static void EpollLoop() {
			epoll_event[] events = new epoll_event[4];
			try {
				Dictionary<int, LinuxSocket> sockmap = epollsockets;
				if (sockmap == null) return;
				while (true) {
					int ret = epoll_wait(epollfd, events, events.Length, 1000);
					if (ret == -1 && Marshal.GetLastWin32Error() == 4) continue; //Interrupted by signal
					if (ret == -1) throw new PosixException("epoll_wait");
					for (int i = 0; i < ret; i++) {
						Interlocked.Decrement(ref epollfds);
						LinuxSocket sock;
						lock (sockmap) if (!sockmap.TryGetValue((int)events[i].Data, out sock)) sock = null;
						UInt32 evt = events[i].Events;
						if (sock != null) sock.EpollEvent((evt & 0x001) != 0, (evt & 0x004) != 0, (evt & 0x008) != 0, (evt & 0x010) != 0);
					}
					if (sockmap.Count == 0) {
						lock (typeof(LinuxSocket)) {
							if (sockmap.Count != 0) continue;
							close(epollfd);
							epollfd = -1;
							epollthread = null;
							epollsockets = null;
							break;
						}
					}
				}
			} catch {
				lock (typeof(LinuxSocket)) {
					if (epollfd != -1) close(epollfd);
					epollfd = -1;
					epollthread = null;
					epollsockets = null;
				}
				throw;
			}
		}
	}
}
