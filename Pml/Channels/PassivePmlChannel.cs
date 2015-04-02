using System;
using UCIS.Pml;
using UCIS.Util;
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
			ReadMessageAsyncResult ar = new ReadMessageAsyncResult(callback, state);
			UThreadPool.RunCall(AsyncReadMessage, ar);
			return ar;
		}
		public PmlElement EndReadMessage(IAsyncResult asyncResult) {
			ReadMessageAsyncResult ar = (ReadMessageAsyncResult)asyncResult;
			ar.WaitForCompletion();
			if (ar.Error != null) throw new Exception("The asynchronous operation failed", ar.Error);
			return ar.Message;
		}

		class ReadMessageAsyncResult : AsyncResultBase {
			internal PmlElement Message;
			public ReadMessageAsyncResult(AsyncCallback callback, Object state) : base(callback, state) { }
			public void SetCompleted(Boolean synchronously, Exception error, PmlElement message) {
				this.Message = message;
				base.SetCompleted(synchronously, error);
			}
		}
		private void AsyncReadMessage(object state) {
			ReadMessageAsyncResult ar = (ReadMessageAsyncResult)state;
			try {
				PmlElement message = ReadMessage();
				ar.SetCompleted(false, null, message);
			} catch (Exception ex) {
				ar.SetCompleted(false, ex, null);
			}
		}
	}
}
