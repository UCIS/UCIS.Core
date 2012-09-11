using System;
using UCIS.Pml;
using System.Collections.Generic;
using System.Threading;

namespace UCIS.Pml {
	public abstract class PassivePmlChannel : IPmlChannel {
		private bool _isOpen = true;

		public virtual bool IsOpen { get { return _isOpen; } }
		public abstract void WriteMessage(PmlElement message);

		public void Dispose() {
			Close();
		}
		public virtual void Close() {
			_isOpen = false;
		}

		public abstract PmlElement ReadMessage();

		public IAsyncResult BeginReadMessage(AsyncCallback callback, object state) {
			ReadMessageAsyncResult ar = new ReadMessageAsyncResult();
			ar.Callback = callback;
			ar.State = state;
			UCIS.ThreadPool.RunCall(AsyncReadMessage, ar);
			return ar;
		}
		public PmlElement EndReadMessage(IAsyncResult asyncResult) {
			ReadMessageAsyncResult ar = (ReadMessageAsyncResult)asyncResult;
			if (ar.Error != null) throw new Exception("The asynchronous operation could not be completed", ar.Error);
			return ar.Message;
		}

		private struct ReadMessageAsyncResult : IAsyncResult {
			internal object State;
			internal PmlElement Message;
			internal AsyncCallback Callback;
			internal Exception Error;
			internal bool Completed;

			public bool CompletedSynchronously { get { return false; } }
			public object AsyncState { get { return State; } }
			public WaitHandle AsyncWaitHandle { get { return null; } }
			public bool IsCompleted { get { return Completed; } }
		}
		private void AsyncReadMessage(object state) {
			ReadMessageAsyncResult ar = (ReadMessageAsyncResult)state;
			try {
				ar.Message = ReadMessage();
				ar.Error = null;
			} catch (Exception ex) {
				ar.Error = ex;
			}
			ar.Completed = true;
			ar.Callback.Invoke(ar);
		}
	}
}
