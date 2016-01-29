using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace UCIS.USBLib {
	unsafe class WindowsOverlappedAsyncResult : IAsyncResult {
		public Object AsyncState { get; private set; }
		public bool CompletedSynchronously { get; private set; }
		public bool IsCompleted { get; private set; }
		int ErrorCode;
		int Result;
		AsyncCallback Callback;
		NativeOverlapped* pOverlapped = null;
		ManualResetEvent WaitEvent = null;
		Object MonitorWaitHandle = new Object();
		//Note that the file handle must be registered with the ThreadPool for the callback to work correctly!
		public WindowsOverlappedAsyncResult(AsyncCallback callback, Object state) {
			this.Callback = callback;
			this.AsyncState = state;
			IsCompleted = false;
		}
		public NativeOverlapped* PackOverlapped(Object userData) {
			if (pOverlapped != null) throw new InvalidOperationException();
			//Passing a real WaitHandle to the Overlapped constructor results in a race condition between CompletionCallback / Complete because the event may be signalled before the callback completes
			Overlapped overlapped = new Overlapped(0, 0, IntPtr.Zero, this);
			return pOverlapped = overlapped.Pack(CompletionCallback, userData);
		}
		static void CompletionCallback(UInt32 errorCode, UInt32 numBytes, NativeOverlapped* poverlapped) {
			Overlapped overlapped = Overlapped.Unpack(poverlapped);
			WindowsOverlappedAsyncResult ar = (WindowsOverlappedAsyncResult)overlapped.AsyncResult;
			Overlapped.Free(poverlapped);
			lock (ar.MonitorWaitHandle) {
				ar.ErrorCode = (int)errorCode;
				ar.Result = (int)numBytes;
				ar.IsCompleted = true;
				if (ar.WaitEvent != null) ar.WaitEvent.Set();
				Monitor.PulseAll(ar.MonitorWaitHandle);
			}
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
			lock (MonitorWaitHandle) if (!IsCompleted) Monitor.Wait(MonitorWaitHandle);
			if (ErrorCode != 0) throw new Win32Exception(ErrorCode);
			AsyncWaitHandle.Close();
			return Result;
		}
		public WaitHandle AsyncWaitHandle {
			get {
				lock (MonitorWaitHandle) {
					if (WaitEvent == null) WaitEvent = new ManualResetEvent(IsCompleted);
					return WaitEvent;
				}
			}
		}
	}
}
