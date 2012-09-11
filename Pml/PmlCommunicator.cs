using System;
using System.Threading;
using System.Collections.Generic;

namespace UCIS.Pml {
/*	class PmlCommunicator : IPmlCommunicator, IDisposable {
		private IPmlChannel _channel;
		private Dictionary<UInt32, IPmlSubChannel> _subchannels = new Dictionary<uint,IPmlSubChannel>();
		private Random _random = new Random();

		private enum CommandCode : int {
			CallWithoutReply = 0,
			CallWithReply = 1,
			Message = 2,
			ChannelRequest = 3,
			ChannelAcknowledge = 4,
			ChannelClose = 5,
			Error = 6
		}

		private interface IPmlSubChannel {
			void CloseIn();
			void ErrorIn(PmlElement message);
			void MessageIn(PmlElement message);
		}

		private class ChannelRequestWaitHandler : IAsyncResult {
			internal AsyncCallback Callback = null;
			internal Object CallbackState = null;
			internal ManualResetEvent Event = null;
			internal PmlSubChannel Channel = null;
			internal bool Completed = false;

			internal ChannelRequestWaitHandler(PmlSubChannel channel) {
				Channel = channel;
			}

			internal void Complete() {
				Completed = true;
				if (Event != null) Event.Set();
				if (Callback != null) Callback.Invoke(this);
			}

			public object AsyncState { get { return CallbackState; } }
			public WaitHandle AsyncWaitHandle { get { return null; } }
			public bool CompletedSynchronously { get { return false; } }
			public bool IsCompleted { get { return Completed; } }
		}
		private class PmlSubChannel : ActivePmlChannel, IPmlSubChannel {
			private enum ChannelState { Requesting, Acknowledged, Closed }

			private PmlCommunicator _communicator;
			private UInt32 _id;
			private ChannelState _state;

			internal PmlSubChannel(PmlCommunicator communicator, UInt32 sid) {
				_communicator = communicator;
				_id = sid;
				_state = ChannelState.Requesting;
			}

			public override bool IsOpen { get { return _state == ChannelState.Acknowledged; } }

			internal void AcknowledgeIn() {
				if (_state != 0) throw new InvalidOperationException("The subchannel is not awaiting an acknowledgement");
				_state = ChannelState.Acknowledged;
			}
			void IPmlSubChannel.CloseIn() {
				_state = ChannelState.Closed;
				_communicator._subchannels.Remove(_id);
				base.Close();
			}
			void IPmlSubChannel.ErrorIn(PmlElement message) {
				(this as IPmlSubChannel).CloseIn();
			}
			void IPmlSubChannel.MessageIn(PmlElement message) {
				base.PushReceivedMessage(message);
			}

			internal void AcknowledgeOut() {
				if (_state != 0) throw new InvalidOperationException("The subchannel is not awaiting an acknowledgement");
				_state = ChannelState.Acknowledged;
				_communicator.sendMessage(CommandCode.ChannelAcknowledge, _id, null);
			}
			internal void RejectOut() {
				if (_state != 0) throw new InvalidOperationException("The subchannel is not awaiting an acknowledgement");
				_state = ChannelState.Closed;
				_communicator.sendMessage(CommandCode.ChannelClose, _id, null);
			}

			public override void SendMessage(PmlElement message) {
				if (_state != ChannelState.Acknowledged) throw new InvalidOperationException("The subchannel is not open");
				_communicator.sendMessage(CommandCode.Message, _id, message);
			}
			public override void Close() {
				if (_state != ChannelState.Acknowledged) return;
				_state = ChannelState.Closed;
				_communicator.sendMessage(CommandCode.ChannelClose, _id, null);
				_communicator._subchannels.Remove(_id);
				base.Close();
			}
		}
		private class PmlChannelRequestReceivedEventArgsA : PmlChannelRequestReceivedEventArgs {
			private PmlCommunicator _communicator;
			private PmlElement _data;
			private PmlSubChannel _channel;
			private bool _accepted;
			private bool _rejected;
			internal PmlChannelRequestReceivedEventArgsA(PmlCommunicator communicator, UInt32 sid, PmlElement message) {
				_communicator = communicator;
				_channel = new PmlSubChannel(communicator, sid);
				_data = message;
			}
			public override IPmlChannel Accept() {
				if (_accepted || _rejected) throw new InvalidOperationException("The channel has already been accepted or rejected");
				_accepted = true;
				_channel.AcknowledgeOut();
				return _channel;
			}
			public override void Reject() {
				if (_accepted) throw new InvalidOperationException("The channel has already been accepted");
				if (_rejected) return;
				_rejected = true;
				_channel.RejectOut();
			}
			internal void RejectIfNotAccepted() {
				if (!_accepted) Reject();
			}
			public override PmlElement Data {
				get {
					return _data;
				}
			}
		}

		private class PmlInvocation : IAsyncResult, IPmlSubChannel {
			internal PmlCommunicator Communicator = null;
			internal AsyncCallback Callback = null;
			internal Object CallbackState = null;
			internal bool Error = false;
			internal bool Completed = false;
			internal PmlElement Message = null;
			internal ManualResetEvent Event = null;
			internal UInt32 ID;

			internal PmlInvocation(PmlCommunicator communicator, UInt32 id) {
				Communicator = communicator;
				ID = id;
			}

			void IPmlSubChannel.CloseIn() {
				(this as IPmlSubChannel).ErrorIn(null);
			}
			void IPmlSubChannel.ErrorIn(PmlElement message) {
				Error = true;
				Communicator._subchannels.Remove(ID);
				(this as IPmlSubChannel).MessageIn(message);
			}
			void IPmlSubChannel.MessageIn(PmlElement message) {
				Message = message;
				Completed = true;
				if (Event != null) Event.Set();
				if (Callback != null) Callback.Invoke(this);
			}

			public object AsyncState { get { return CallbackState; } }
			public WaitHandle AsyncWaitHandle { get { return null; } }
			public bool CompletedSynchronously { get { return false; } }
			public bool IsCompleted { get { return Completed; } }
		}

		public event EventHandler<PmlCallReceivedEventArgs> CallReceived;
		public event EventHandler<PmlChannelRequestReceivedEventArgs> ChannelRequestReceived;

		public PmlCommunicator(IPmlChannel channel) {
			_channel = channel;
			_channel.Closed += channelClosed;
		}

		public void Dispose() {
			_channel.Close();
			_channel = null;
			IPmlSubChannel[] A = new IPmlSubChannel[_subchannels.Count];
			_subchannels.Values.CopyTo(A, 0);
			foreach (IPmlSubChannel S in A) S.CloseIn();
			_subchannels.Clear();
			_subchannels = null;
			_random = null;
		}

		private void channelClosed(Object sender, EventArgs e) {
			Dispose();
		}

		public IPmlChannel Channel { get { return _channel; } }

		public void Call(PmlElement message) {
			sendMessage(0, 0, message); //Call without reply
		}
		public PmlElement Invoke(PmlElement message) {
			return Invoke(message, 60000);
		}
		public PmlElement Invoke(PmlElement message, int timeout) {
			UInt32 sid = getSessionID();
			PmlInvocation inv = new PmlInvocation(this, sid);
			inv.Event = new ManualResetEvent(false);
			_subchannels.Add(sid, inv);
			sendMessage(CommandCode.CallWithReply, sid, message);
			inv.Event.WaitOne(timeout);
			if (inv.Error) throw new Exception(message.ToString());
			return inv.Message;
		}

		public IAsyncResult BeginInvoke(PmlElement message, AsyncCallback callback, Object state) {
			UInt32 sid = getSessionID();
			PmlInvocation inv = new PmlInvocation(this, sid);
			inv.Callback = callback;
			inv.CallbackState = state;
			_subchannels.Add(sid, inv);
			sendMessage(CommandCode.CallWithReply, sid, message);
			return inv;
		}
		public PmlElement EndInvoke(IAsyncResult result) {
			PmlInvocation ar = (PmlInvocation)result;
			if (!ar.Completed) {
				(_subchannels as IList<IPmlSubChannel>).Remove(ar);
				throw new InvalidOperationException("The asynchronous operation has not completed");
			} else if (ar.Error) {
				throw new Exception(ar.Message.ToString());
			} else {
				return ar.Message;
			}
		}

		public IPmlChannel CreateChannel(PmlElement data) {
			UInt32 sid = getSessionID();
			PmlSubChannel ch = new PmlSubChannel(this, sid);
			ChannelRequestWaitHandler wh = new ChannelRequestWaitHandler(ch);
			wh.Event = new ManualResetEvent(false);
			_subchannels.Add(sid, ch);
			sendMessage(CommandCode.ChannelRequest, sid, data);
			wh.Event.WaitOne();
			if (!ch.IsOpen) return null;
			return ch;
		}
		public IAsyncResult BeginCreateChannel(PmlElement data, AsyncCallback callback, Object state) {
			UInt32 sid = getSessionID();
			PmlSubChannel ch = new PmlSubChannel(this, sid);
			ChannelRequestWaitHandler wh = new ChannelRequestWaitHandler(ch);
			wh.Callback = callback;
			wh.CallbackState = state;
			_subchannels.Add(sid, ch);
			sendMessage(CommandCode.ChannelRequest, sid, data);
			if (!ch.IsOpen) return null;
			return wh;
		}
		public IPmlChannel EndCreateChannel(IAsyncResult result) {
			ChannelRequestWaitHandler ar = (ChannelRequestWaitHandler)result;
			if (!ar.Channel.IsOpen) return null;
			return ar.Channel;
		}

		private UInt32 getSessionID() {
			return (uint)_random.Next();
		}

		private void sendMessage(CommandCode cmd, uint sid, PmlElement message) {
			PmlDictionary msg = new PmlDictionary();
			msg.Add("c", (int)cmd);
			if (cmd > 0) msg.Add("s", sid);
			if (message != null) msg.Add("m", message);
			_channel.SendMessage(msg);
		}

		private void invokeCallReceived(Object state) {
			PmlCallReceivedEventArgs e = (PmlCallReceivedEventArgs)state;
			try {
				if (CallReceived != null) CallReceived(this, e);
				if (e.WantReply) sendMessage(CommandCode.Message, e.SID, e.Reply);
			} catch (Exception ex) {
				if (e.WantReply) sendMessage(CommandCode.Error, e.SID, new PmlString(ex.ToString()));
			}
		}
		private void invokeChannelRequestReceived(Object state) {
			PmlChannelRequestReceivedEventArgsA e = (PmlChannelRequestReceivedEventArgsA)state;
			if (ChannelRequestReceived != null) ChannelRequestReceived(this, e);
			e.RejectIfNotAccepted();
		}

		private void messageReceived(Object sender, EventArgs e) {
			IPmlSubChannel subChannel = null;
			UInt32 sid = 0;
			bool subChannelExists = false;
			if (!(e.Message is PmlDictionary)) return;
			PmlDictionary msg = (PmlDictionary)e.Message;
			PmlElement cmdElement = msg.GetChild("c");
			PmlElement sidElement = msg.GetChild("i");
			PmlElement msgElement = msg.GetChild("m");
			if (cmdElement == null) return;
			if (sidElement != null) sid = sidElement.ToUInt32();
			if (sidElement != null) subChannelExists = _subchannels.TryGetValue(sid, out subChannel);
			if (!subChannelExists) subChannel = null;
			switch ((CommandCode)cmdElement.ToInt32()) {
				case CommandCode.CallWithoutReply:
					if (CallReceived != null) ThreadPool.RunCall(invokeCallReceived, new PmlCallReceivedEventArgs(msgElement, false, 0));
					break;
				case CommandCode.CallWithReply:
					if (CallReceived != null) ThreadPool.RunCall(invokeCallReceived, new PmlCallReceivedEventArgs(msgElement, true, sid));
					else sendMessage(CommandCode.Error, sid, null);
					break;
				case CommandCode.Message: //Reply to call | subchannel message
					if (subChannelExists) subChannel.MessageIn(msgElement);
					else sendMessage(CommandCode.Error, sid, null);
					break;
				case CommandCode.ChannelRequest:
					if (subChannelExists) {
						sendMessage(CommandCode.Error, sid, null);
						subChannel.CloseIn();
					} else {
						if (ChannelRequestReceived == null) sendMessage(CommandCode.ChannelClose, sid, null);
						else ThreadPool.RunCall(invokeChannelRequestReceived, new PmlChannelRequestReceivedEventArgsA(this, sid, msgElement));
					}
					break;
				case CommandCode.ChannelAcknowledge:
					if (subChannelExists) {
						if (subChannel is PmlSubChannel) (subChannel as PmlSubChannel).AcknowledgeIn();
						else {
							sendMessage(CommandCode.Error, sid, null); //Error
							subChannel.CloseIn();
						}
					} else sendMessage(CommandCode.Error, sid, null); //Error
					break;
				case CommandCode.ChannelClose:
					if (subChannelExists) subChannel.CloseIn();
					break;
				case CommandCode.Error:
					if (subChannelExists) subChannel.ErrorIn(msgElement);
					break;
			}
		}
	}*/
}
