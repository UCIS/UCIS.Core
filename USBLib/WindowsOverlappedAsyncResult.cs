using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace UCIS.USBLib {
	unsafe class WindowsOverlappedAsyncResult : IAsyncResult {
		public Object AsyncState { get; private set; }
		public WaitHandle AsyncWaitHandle { get; private set; }
		public bool CompletedSynchronously { get { return false; } }
		public bool IsCompleted { get; private set; }
		int ErrorCode;
		int Result;
		AsyncCallback Callback;
		NativeOverlapped* pOverlapped = null;
		public WindowsOverlappedAsyncResult(AsyncCallback callback, Object state) {
			this.Callback = callback;
			this.AsyncState = state;
			IsCompleted = false;
			AsyncWaitHandle = new ManualResetEvent(false);
		}
		public NativeOverlapped* PackOverlapped(Object userData) {
			if (pOverlapped != null) throw new InvalidOperationException();
			Overlapped overlapped = new Overlapped(0, 0, AsyncWaitHandle.SafeWaitHandle.DangerousGetHandle(), this);
			return pOverlapped = overlapped.Pack(CompletionCallback, userData);
		}
		static void CompletionCallback(UInt32 errorCode, UInt32 numBytes, NativeOverlapped* poverlapped) {
			Overlapped overlapped = Overlapped.Unpack(poverlapped);
			WindowsOverlappedAsyncResult ar = (WindowsOverlappedAsyncResult)overlapped.AsyncResult;
			Overlapped.Free(poverlapped);
			ar.ErrorCode = (int)errorCode;
			ar.Result = (int)numBytes;
			ar.IsCompleted = true;
			if (ar.Callback != null) ar.Callback(ar);
		}
		internal void SyncResult(Boolean success, int length) {
			if (success) return;
			int err = Marshal.GetLastWin32Error();
			if (err == 997) return;
			ErrorCleanup();
			throw new Win32Exception(err);
		}
		internal void ErrorCleanup() {
			if (pOverlapped != null) {
				Overlapped.Unpack(pOverlapped);
				Overlapped.Free(pOverlapped);
				pOverlapped = null;
			}
			AsyncWaitHandle.Close();
		}
		internal int Complete() {
			if (!IsCompleted) AsyncWaitHandle.WaitOne();
			if (ErrorCode != 0) throw new Win32Exception(ErrorCode);
			AsyncWaitHandle.Close();
			return Result;
		}
	}
}
