using System;
using System.Collections.Generic;
using System.Text;

namespace UCIS.Pml {
	public class PmlCallReceivedEventArgs : EventArgs {
		internal PmlCallReceivedEventArgs(PmlElement request, bool wantReply, UInt32 sid) {
			this.Request = request;
			this.WantReply = wantReply;
			this.SID = sid;
			this.Reply = null;
		}
		public bool WantReply { get; private set; }
		internal UInt32 SID { get; private set; }
		public PmlElement Request { get; private set; }
		public PmlElement Reply { get; set; }
	}
	public abstract class PmlChannelRequestReceivedEventArgs : EventArgs {
		public abstract IPmlChannel Accept();
		public abstract void Reject();
		public abstract PmlElement Data { get; }
	}
	public interface IPmlCommunicator {
		event EventHandler<PmlCallReceivedEventArgs> CallReceived;
		event EventHandler<PmlChannelRequestReceivedEventArgs> ChannelRequestReceived;

		void Call(PmlElement message);
		PmlElement Invoke(PmlElement message);

		IAsyncResult BeginInvoke(PmlElement message, AsyncCallback callback, Object state);
		PmlElement EndInvoke(IAsyncResult result);

		IPmlChannel CreateChannel(PmlElement data);
		IAsyncResult BeginCreateChannel(PmlElement data, AsyncCallback callback, Object state);
		IPmlChannel EndCreateChannel(IAsyncResult result);
	}
}
