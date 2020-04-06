using System;
using System.Threading;
using System.Reflection;

namespace UCIS.Util {
	public abstract class AsyncResultBase : IAsyncResult {
		[ThreadStatic]
		static Boolean ThreadInCallback = false;
		ManualResetEvent WaitEvent = null;
		Object MonitorWaitHandle = new Object();
		AsyncCallback callbacks;
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
			if (callback != null) callbacks += callback;
			this.AsyncState = state;
		}

		private void CallCallback(Object state) {
			AsyncCallback callbacks = this.callbacks;
			if (callbacks != null) callbacks(this);
		}

		protected void SetCompleted(Boolean synchronously, Exception error) {
			AsyncCallback callbacks;
			lock (MonitorWaitHandle) {
				if (IsCompleted) return;
				callbacks = this.callbacks;
				this.CompletedSynchronously = synchronously;
				this.Error = error;
				IsCompleted = true;
				if (WaitEvent != null) WaitEvent.Set();
				Monitor.PulseAll(MonitorWaitHandle);
			}
			if (callbacks != null) {
				if (synchronously && !ThreadInCallback) {
					try {
						ThreadInCallback = true;
						callbacks(this);
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
			if (Error != null) {
				MethodInfo preserveStackTrace = typeof(Exception).GetMethod("InternalPreserveStackTrace", BindingFlags.Instance | BindingFlags.NonPublic);
				if (preserveStackTrace != null) preserveStackTrace.Invoke(Error, new Object[0]);
				throw Error;
			}
		}

		public void AddCallback(AsyncCallback callback, Boolean callifcompleted) {
			Boolean callnow = false;
			lock (MonitorWaitHandle) {
				if (!IsCompleted) callbacks += callback;
				else if (callifcompleted) callnow = true;
			}
			if (callnow) callback(this);
		}
		public void AddCallback(AsyncCallback callback) {
			AddCallback(callback, true);
		}
		public void AddSuccessCallback(AsyncCallback callback) {
			AddCallback((ar) => { if (((AsyncResultBase)ar).Error == null) callback(ar); });
		}
		public void AddErrorCallback(Action<Exception> callback) {
			AddCallback((ar) => { Exception error = ((AsyncResultBase)ar).Error; if (error != null) callback(error); });
		}
	}

	public class AsyncResult : AsyncResultBase {
		protected AsyncResult() : base(null, null) {
		}
		public static AsyncResult CreateCompleted() {
			AsyncResult ar = new AsyncResult();
			ar.SetCompleted(true, null);
			return ar;
		}
		public static AsyncResult CreateCompleted(Exception ex) {
			AsyncResult ar = new AsyncResult();
			ar.SetCompleted(true, ex);
			return ar;
		}
	}

	public abstract class AsyncResult<TResult> : AsyncResult {
		public abstract TResult Result { get; }
		class InternalAsyncResult : AsyncResult<TResult> {
			public TResult result;
			public override TResult Result {
				get {
					WaitForCompletion();
					ThrowError();
					return result;
				}
			}
			public InternalAsyncResult() {
			}
			public InternalAsyncResult(TResult result, Exception error) {
				this.result = result;
				SetCompleted(true, error);
			}
		}
		public static AsyncResult<TResult> Create(TResult result) {
			return new InternalAsyncResult(result, null);
		}
		public static AsyncResult<TResult> Create(Exception exception) {
			return new InternalAsyncResult(default(TResult), exception);
		}
		public static implicit operator TResult(AsyncResult<TResult> r) {
			return r.Result;
		}
		public static implicit operator AsyncResult<TResult>(Exception ex) {
			return Create(ex);
		}
		public static implicit operator AsyncResult<TResult>(TResult r) {
			return Create(r);
		}
		public void AddCallback(Action<AsyncResult<TResult>> callback) {
			base.AddCallback((ar) => callback((AsyncResult<TResult>)ar));
		}
		public void AddUICallback(Action<AsyncResult<TResult>> callback) {
			SynchronizationContext context = SynchronizationContext.Current;
			base.AddCallback((ar) => context.Post((s) => callback((AsyncResult<TResult>)s), ar));
		}
		public void AddSuccessCallback(Action<TResult> callback) {
			base.AddSuccessCallback((ar) => callback(((AsyncResult<TResult>)ar).Result));
		}

		public delegate TNext Func<TNext>(TResult arg);
		public AsyncResult<TNext> Then<TNext>(Func<TNext> callback) {
			AsyncResult<TNext>.InternalAsyncResult nextar = new AsyncResult<TNext>.InternalAsyncResult();
			base.AddCallback((IAsyncResult ar) => {
				AsyncResult<TResult> arr = (AsyncResult<TResult>)ar;
				if (arr.Error != null) {
					nextar.SetCompleted(arr.CompletedSynchronously, arr.Error);
				} else {
					try {
						nextar.result = callback(arr.Result);
						nextar.SetCompleted(arr.CompletedSynchronously, null);
					} catch (Exception ex) {
						nextar.SetCompleted(arr.CompletedSynchronously, ex);
					}
				}
			});
			return nextar;
		}
	}

	public class AsyncResultSource {
		class AsyncResultImpl : AsyncResult {
			public AsyncResultImpl() { }
			public new void SetCompleted(Boolean synchronously, Exception ex) {
				base.SetCompleted(synchronously, ex);
			}
		}
		private AsyncResultImpl result = new AsyncResultImpl();
		public AsyncResult AsyncResult { get { return result; } }
		public void SetCompleted(Boolean synchronously, Exception exception) {
			result.SetCompleted(synchronously, exception);
		}
		public static implicit operator AsyncResult(AsyncResultSource source) { return source.AsyncResult; }
	}

	public class AsyncResultSource<TResult> {
		class AsyncResultImpl : AsyncResult<TResult> {
			public AsyncResultImpl() { }
			private TResult result;
			public new void SetCompleted(Boolean synchronously, Exception ex) {
				base.SetCompleted(synchronously, ex);
			}
			public void SetCompleted(Boolean synchronously, TResult result) {
				this.result = result;
				base.SetCompleted(synchronously, null);
			}
			override public TResult Result {
				get {
					WaitForCompletion();
					ThrowError();
					return result;
				}
			}
		}
		private AsyncResultImpl result = new AsyncResultImpl();
		public AsyncResult<TResult> AsyncResult { get { return result; } }
		public void SetCompleted(Boolean synchronously, TResult result) {
			this.result.SetCompleted(synchronously, null);
		}
		public void SetCompleted(Boolean synchronously, Exception exception) {
			result.SetCompleted(synchronously, exception);
		}
		public static implicit operator AsyncResult<TResult>(AsyncResultSource<TResult> source) { return source.AsyncResult; }
	}
}
