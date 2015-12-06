using System;
using UCIS.Pml;
using UCIS.Util;
using System.Collections.Generic;
using System.Threading;

namespace UCIS.Pml {
	public abstract class ActivePmlChannel : IPmlChannel {
		private ReadMessageAsyncResult _asyncWait = null;
		private Queue<PmlElement> _queue = new Queue<PmlElement>();
		private bool _isOpen = true;

		public virtual bool IsOpen { get { return _isOpen; } }
		public abstract void WriteMessage(PmlElement message);

		public PmlElement ReadMessage() {
			lock (_queue) {
				if (!IsOpen) throw new InvalidOperationException("The channel is not open");
				while (_queue.Count == 0) {
					if (!IsOpen) throw new OperationCanceledException("The operation did not complete");
					Monitor.Wait(_queue);
				}
				return _queue.Dequeue();
			}
		}

		public IAsyncResult BeginReadMessage(AsyncCallback callback, object state) {
			ReadMessageAsyncResult ar;
			Boolean completed = false;
			lock (_queue) {
				if (!IsOpen) throw new InvalidOperationException("The channel is not open");
				if (_asyncWait != null) throw new InvalidOperationException("Another asynchronous operation is in progress");
				ar = new ReadMessageAsyncResult(callback, state);
				if (_queue.Count == 0) {
					_asyncWait = ar;
				} else {
					ar.Message = _queue.Dequeue();
					completed = true;
				}
			}
			if (completed) ar.SetCompleted(true, null);
			return ar;
		}

		public PmlElement EndReadMessage(IAsyncResult asyncResult) {
			ReadMessageAsyncResult ar = (ReadMessageAsyncResult)asyncResult;
			ar.WaitForCompletion();
			if (ar.Error != null) throw new OperationCanceledException("The asynchronous operation failed", ar.Error);
			return ar.Message;
		}

		public virtual void Close() {
			ReadMessageAsyncResult asyncWait;
			lock (_queue) {
				_isOpen = false;
				asyncWait = Interlocked.Exchange<ReadMessageAsyncResult>(ref _asyncWait, null);
				Monitor.PulseAll(_queue);
			}
			if (asyncWait != null) asyncWait.SetCompleted(false, new ObjectDisposedException("ActivePmlChannel"));
		}

		public void Dispose() {
			Close();
		}

		protected void PushReceivedMessage(PmlElement message) {
			ReadMessageAsyncResult asyncWait;
			lock (_queue) {
				asyncWait = Interlocked.Exchange<ReadMessageAsyncResult>(ref _asyncWait, null);
				if (asyncWait == null) {
					_queue.Enqueue(message);
					Monitor.Pulse(_queue);
				}
			}
			if (asyncWait != null) {
				asyncWait.Message = message;
				asyncWait.SetCompleted(false, null);
			}
		}

		class ReadMessageAsyncResult : AsyncResultBase {
			internal PmlElement Message;
			public ReadMessageAsyncResult(AsyncCallback callback, Object state) : base(callback, state) { }
			public new void SetCompleted(Boolean synchronously, Exception error) {
				base.SetCompleted(synchronously, error);
			}
		}
	}
}
