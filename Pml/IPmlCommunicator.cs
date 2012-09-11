using System;
using System.Collections.Generic;
using System.Text;

namespace UCIS.Pml {
	public class PmlCallReceivedEventArgs : EventArgs {
		private PmlElement _request;
		private PmlElement _reply;
		private UInt32 _sid;
		private bool _wantReply;

		internal PmlCallReceivedEventArgs(PmlElement request, bool wantReply, UInt32 sid) {
			_request = request;
			_wantReply = wantReply;
			_sid = sid;
			_reply = null;
		}
		public bool WantReply {
			get { return _wantReply; }
		}
		internal UInt32 SID {
			get { return _sid; }
		}
		public PmlElement Reply {
			get { return _reply; }
			set { _reply = value; }
		}
		public PmlElement Request {
			get { return _request; }
		}
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
