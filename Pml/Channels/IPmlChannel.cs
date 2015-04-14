using System;

namespace UCIS.Pml {
	public interface IPmlChannel : IDisposable {
		bool IsOpen { get; }
		void WriteMessage(PmlElement message);
		void Close();

		PmlElement ReadMessage();
		IAsyncResult BeginReadMessage(AsyncCallback callback, object state);
		PmlElement EndReadMessage(IAsyncResult asyncResult);
	}
}
