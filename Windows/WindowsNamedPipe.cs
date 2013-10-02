using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using UCIS.Util;

namespace UCIS.Windows {
	public class WindowsNamedPipe : PacketStream {
		delegate Byte[] ReadPacketDelegate();
		ReadPacketDelegate ReadPacketDelegateInstance = null;
		SafeFileHandle PipeHandle;

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess, [MarshalAs(UnmanagedType.U4)] FileShare dwShareMode, IntPtr lpSecurityAttributes, [MarshalAs(UnmanagedType.U4)] FileMode dwCreationDisposition, UInt32 dwFlagsAndAttributes, IntPtr hTemplateFile);
		[DllImport("kernel32.dll", SetLastError = true)]
		static extern unsafe bool ReadFile(SafeFileHandle hFile, Byte* lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, NativeOverlapped* lpOverlapped);
		[DllImport("kernel32.dll", SetLastError = true)]
		static extern unsafe bool WriteFile(SafeFileHandle hFile, Byte* lpBuffer, uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, NativeOverlapped* lpOverlapped);
		[DllImport("kernel32.dll", SetLastError = true)]
		static extern unsafe bool GetOverlappedResult(SafeFileHandle hFile, NativeOverlapped* lpOverlapped, out uint lpNumberOfBytesTransferred, Boolean bWait);
		[DllImport("kernel32.dll", SetLastError = true)]
		static extern bool SetNamedPipeHandleState(SafeFileHandle hNamedPipe, ref UInt32 lpMode, IntPtr lpMaxCollectionCount, IntPtr lpCollectDataTimeout);
		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
		static extern SafeFileHandle CreateNamedPipe([MarshalAs(UnmanagedType.LPStr)] String lpName, UInt32 dwOpenMode, UInt32 dwPipeMode, UInt32 nMaxInstances, UInt32 nOutBufferSize, UInt32 nInBufferSize, UInt32 nDefaultTimeOut, IntPtr lpSecurityAttributes);
		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		static extern unsafe bool ConnectNamedPipe(SafeFileHandle hNamedPipe, NativeOverlapped* lpOverlapped);
		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		static extern unsafe bool GetNamedPipeInfo(SafeFileHandle hNamedPipe, out UInt32 lpFlags, IntPtr lpOutBufferSize, IntPtr lpInBufferSize, IntPtr lpMaxInstances);

		const UInt32 PIPE_ACCESS_DUPLEX = 0x00000003;
		const UInt32 FILE_FLAG_OVERLAPPED = 0x40000000;
		const UInt32 PIPE_TYPE_BYTE = 0x00000000;
		const UInt32 PIPE_TYPE_MESSAGE = 0x00000004;
		const UInt32 PIPE_READMODE_BYTE = 0x00000000;
		const UInt32 PIPE_READMODE_MESSAGE = 0x00000002;

		public int InitialMessageBufferSize { get; set; }

		private WindowsNamedPipe(SafeFileHandle handle) {
			InitialMessageBufferSize = 1024;
			this.PipeHandle = handle;
		}
		public static WindowsNamedPipe Create(String name, Boolean messageMode, uint maxClients, uint readBuffer, uint writeBuffer, uint defaultTimeout) {
			SafeFileHandle handle = CreateNamedPipe(name, PIPE_ACCESS_DUPLEX | FILE_FLAG_OVERLAPPED, 
				messageMode ? (PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE) : (PIPE_TYPE_BYTE | PIPE_READMODE_BYTE),
				maxClients, writeBuffer, readBuffer, defaultTimeout, IntPtr.Zero);
			if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
			return new WindowsNamedPipe(handle);
		}
		public static WindowsNamedPipe Connect(String name) {
			SafeFileHandle handle = CreateFile(name, 0x40000000 | 0x80000000, FileShare.None, IntPtr.Zero, FileMode.Open, 0x40000000, IntPtr.Zero);
			if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
			UInt32 flags;
			if (!GetNamedPipeInfo(handle, out flags, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
			UInt32 lpMode = (flags & PIPE_TYPE_MESSAGE) != 0 ? PIPE_READMODE_MESSAGE : PIPE_READMODE_BYTE;
			if (!SetNamedPipeHandleState(handle, ref lpMode, IntPtr.Zero, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
			return new WindowsNamedPipe(handle);
		}

		public unsafe void WaitForClient() {
			uint nread;
			using (ManualResetEvent evt = new ManualResetEvent(false)) {
				NativeOverlapped overlapped = new NativeOverlapped();
				overlapped.EventHandle = evt.SafeWaitHandle.DangerousGetHandle();
				if (!ConnectNamedPipe(PipeHandle, &overlapped)) {
					int err = Marshal.GetLastWin32Error();
					if (err != 997) throw new Win32Exception(err);
					evt.WaitOne();
					if (!GetOverlappedResult(PipeHandle, &overlapped, out nread, false)) throw new Win32Exception(Marshal.GetLastWin32Error());
				}
			}
		}

		private unsafe int ReadInternal(Byte[] buffer, int offset, int count, out int error) {
			if (offset < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException("offset", "Specified buffer is outside of array bounds");
			uint nread;
			using (ManualResetEvent evt = new ManualResetEvent(false)) {
				NativeOverlapped overlapped = new NativeOverlapped();
				overlapped.EventHandle = evt.SafeWaitHandle.DangerousGetHandle();
				fixed (Byte* bufferptr = buffer) {
					if (ReadFile(PipeHandle, bufferptr + offset, (uint)count, out nread, &overlapped)) {
						error = 0;
					} else {
						error = Marshal.GetLastWin32Error();
						if (error == 997) {
							evt.WaitOne();
							if (GetOverlappedResult(PipeHandle, &overlapped, out nread, false)) {
								error = 0;
							} else {
								error = Marshal.GetLastWin32Error();
							}
						}
					}
					return (int)nread;
				}
			}
		}
		public override unsafe int Read(byte[] buffer, int offset, int count) {
			int error;
			int nread = ReadInternal(buffer, offset, count, out error);
			if (error == 109) return 0;
			if (error == 0) return nread;
			throw new Win32Exception(error);
		}
		public override unsafe void Write(byte[] buffer, int offset, int count) {
			if (offset < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException("offset", "Specified buffer is outside of array bounds");
			uint nread;
			using (ManualResetEvent evt = new ManualResetEvent(false)) {
				NativeOverlapped overlapped = new NativeOverlapped();
				overlapped.EventHandle = evt.SafeWaitHandle.DangerousGetHandle();
				fixed (Byte* bufferptr = buffer) {
					if (!WriteFile(PipeHandle, bufferptr + offset, (uint)count, out nread, &overlapped)) {
						int err = Marshal.GetLastWin32Error();
						if (err != 997) throw new Win32Exception(err);
						evt.WaitOne();
						if (!GetOverlappedResult(PipeHandle, &overlapped, out nread, false)) throw new Win32Exception(Marshal.GetLastWin32Error());
					}
				}
			}
			if (nread != count) throw new IOException("Not all data could be written");
		}
		public override void Close() {
			base.Close();
			PipeHandle.Close();
		}

		public unsafe override byte[] ReadPacket() {
			int offset = 0;
			Byte[] buffer = new Byte[InitialMessageBufferSize];
			while (true) {
				int error;
				int nread = ReadInternal(buffer, offset, buffer.Length - offset, out error);
				offset += nread;
				if (error == 109) {
					return null;
				} else if (error == 0) {
					if (offset != buffer.Length) Array.Resize(ref buffer, offset);
					return buffer;
				} else if (error == 234) {
					offset = buffer.Length;
					Array.Resize(ref buffer, buffer.Length * 2);
				} else {
					throw new Win32Exception(error);
				}
			}
		}
		public override IAsyncResult BeginReadPacket(AsyncCallback callback, object state) {
			if (ReadPacketDelegateInstance == null) ReadPacketDelegateInstance = (ReadPacketDelegate)ReadPacket;
			return ReadPacketDelegateInstance.BeginInvoke(callback, state);
		}
		public override byte[] EndReadPacket(IAsyncResult asyncResult) {
			return ReadPacketDelegateInstance.EndInvoke(asyncResult);
		}

		public override bool CanRead {
			get { return !PipeHandle.IsClosed; }
		}
		public override bool CanWrite {
			get { return !PipeHandle.IsClosed; }
		}

		public override void Flush() { }
		public override bool CanSeek { get { return false; } }

		public override long Length { get { throw new NotSupportedException(); } }
		public override long Position {
			get { throw new NotSupportedException(); }
			set { throw new NotSupportedException(); }
		}

		public override long Seek(long offset, System.IO.SeekOrigin origin) { throw new NotSupportedException(); }

		public override void SetLength(long value) { throw new NotSupportedException(); }

	}
}
