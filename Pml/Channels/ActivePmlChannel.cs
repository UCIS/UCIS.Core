using System;
using UCIS.Pml;
using System.Collections.Generic;
using System.Threading;

namespace UCIS.Pml {
	public abstract class ActivePmlChannel : IPmlChannel {
		private AutoResetEvent _receiveEvent = new AutoResetEvent(false);
		private ReadMessageAsyncResult _asyncWait = null;
		private Queue<PmlElement> _queue = new Queue<PmlElement>();
		private bool _isOpen = true;

		public virtual bool IsOpen { get { return _isOpen; } }
		public abstract void WriteMessage(PmlElement message);

		public PmlElement ReadMessage() {
			if (!IsOpen) throw new InvalidOperationException("The channel is not open");
			if (_queue.Count == 0) {
				_receiveEvent.WaitOne();
				if (_queue.Count == 0) throw new OperationCanceledException("The operation did not complete");
			} else if (_queue.Count == 1) {
				_receiveEvent.Reset();
			}
			return _queue.Dequeue();
		}

		public IAsyncResult BeginReadMessage(AsyncCallback callback, object state) {
			ReadMessageAsyncResult ar = new ReadMessageAsyncResult(state, callback);
			if (!IsOpen) throw new InvalidOperationException("The channel is not open");
			if (_asyncWait != null) throw new InvalidOperationException("Another asynchronous operation is in progress");
			if (_queue.Count == 0) {
				_asyncWait = ar;
			} else {
				if (_queue.Count == 1) _receiveEvent.Reset();
				ar.Complete(true, _queue.Dequeue(), true);
			}
			return ar;
		}

		public PmlElement EndReadMessage(IAsyncResult asyncResult) {
			ReadMessageAsyncResult ar = (ReadMessageAsyncResult)asyncResult;
			if (ar.Error) new OperationCanceledException("The operation did not complete");
			return ar.Message;
		}

		public virtual void Close() {
			_isOpen = false;
			ReadMessageAsyncResult asyncWait = Interlocked.Exchange<ReadMessageAsyncResult>(ref _asyncWait, null);
			if (asyncWait != null) asyncWait.Complete(false, null, false);
			_receiveEvent.Set();
			_receiveEvent.Close();
		}

		public void Dispose() {
			Close();
		}

		protected void PushReceivedMessage(PmlElement message) {
			ReadMessageAsyncResult asyncWait = Interlocked.Exchange<ReadMessageAsyncResult>(ref _asyncWait, null);
			if (asyncWait != null) {
				asyncWait.Complete(true, message, false);
			} else {
				_queue.Enqueue(message);
				_receiveEvent.Set();
			}
		}

		private class ReadMessageAsyncResult : IAsyncResult {
			private object state;
			private AsyncCallback callback;
			private bool completed;
			private bool synchronously;
			private ManualResetEvent waitHandle;

			internal PmlElement Message;
			internal bool Error;

			public bool CompletedSynchronously { get { return synchronously; } }
			public object AsyncState { get { return state; } }
			public WaitHandle AsyncWaitHandle {
				get {
					if (waitHandle == null) waitHandle = new ManualResetEvent(completed);
					return waitHandle;
				}
			}
			public bool IsCompleted { get { return completed; } }

			internal ReadMessageAsyncResult(object state, AsyncCallback callback) {
				this.state = state;
				this.callback = callback;
				this.completed = false;
				this.synchronously = false;
				this.Message = null;
				this.Error = false;
				this.waitHandle = null;
			}
			internal void Complete(bool success, PmlElement message, bool synchronously) {
				this.Message = message;
				this.Error = !success;
				this.synchronously = synchronously;
				this.completed = true;
				if (waitHandle != null) waitHandle.Set();
				if (this.callback != null) this.callback.Invoke(this);
			}
		}
	}
}
