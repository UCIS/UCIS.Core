using System;
using System.Threading;

namespace UCIS.Util {
	public abstract class AsyncResultBase : IAsyncResult {
		[ThreadStatic]
		static Boolean ThreadInCallback = false;
		ManualResetEvent WaitEvent = null;
		AsyncCallback Callback = null;
		Object MonitorWaitHandle = new Object();
		public object AsyncState { get; private set; }
		public bool CompletedSynchronously { get; private set; }
		public bool IsCompleted { get; private set; }
		public Exception Error { get; private set; }
		public WaitHandle AsyncWaitHandle {
			get {
				lock (MonitorWaitHandle) {
					if (WaitEvent == null) WaitEvent = new ManualResetEvent(IsCompleted);
					return WaitEvent;
				}
			}
		}

		public AsyncResultBase(AsyncCallback callback, Object state) {
			this.Callback = callback;
			this.AsyncState = state;
		}

		private void CallCallback(Object state) {
			if (Callback != null) Callback(this);
		}

		protected void SetCompleted(Boolean synchronously, Exception error) {
			this.CompletedSynchronously = synchronously;
			this.Error = error;
			lock (MonitorWaitHandle) {
				IsCompleted = true;
				if (WaitEvent != null) WaitEvent.Set();
				Monitor.PulseAll(MonitorWaitHandle);
			}
			if (Callback != null) {
				if (synchronously && !ThreadInCallback) {
					try {
						ThreadInCallback = true;
						Callback(this);
					} finally {
						ThreadInCallback = false;
					}
				} else {
					ThreadPool.QueueUserWorkItem(CallCallback);
				}
			}
		}

		public void WaitForCompletion() {
			lock (MonitorWaitHandle) if (!IsCompleted) Monitor.Wait(MonitorWaitHandle);
		}
		public Boolean WaitForCompletion(int millisecondsTimeout) {
			lock (MonitorWaitHandle) if (!IsCompleted) Monitor.Wait(MonitorWaitHandle, millisecondsTimeout);
			return IsCompleted;
		}

		protected void ThrowError() {
			if (Error != null) throw Error;
		}
	}
}
