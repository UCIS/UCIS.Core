using System;

namespace UCIS.Pml {
	/*public class PmlMessageReceivedEventArgs : EventArgs {
		private PmlElement _message;
		public PmlMessageReceivedEventArgs(PmlElement message) {
			_message = message;
		}
		public PmlElement Message { get { return _message; } }
	}*/
	public interface IPmlChannel : IDisposable {
		//event EventHandler MessageReceived;
		//event EventHandler Closed;
		bool IsOpen { get; }
		void WriteMessage(PmlElement message);
		void Close();

		PmlElement ReadMessage();
		IAsyncResult BeginReadMessage(AsyncCallback callback, object state);
		PmlElement EndReadMessage(IAsyncResult asyncResult);
	}
}
