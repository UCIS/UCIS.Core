using System;
using System.Threading;
using System.Collections.Generic;
using UCIS.Pml;

namespace UCIS.Pml {
	public class PmlCommunicator {
		[Serializable]
		class PmlRemoteException : Exception {
			public String ExceptionText { get; private set; }
			public PmlRemoteException(String message, String text) : base(message) {
				this.ExceptionText = text;
			}
			public override string ToString() {
				if (ExceptionText == null) return base.ToString();
				return base.ToString() + Environment.NewLine + "Original exception text:" + Environment.NewLine + ExceptionText;
			}
		}
		class CSyncRequest {
			internal PmlDictionary Reply = null;
			internal void Completed(PmlDictionary reply) {
				lock (this) {
					this.Reply = reply;
					Monitor.PulseAll(this);
				}
			}
		}
		class SubChannel : ActivePmlChannel {
			private enum ChannelState { Requesting, Acknowledged, Closed }

			private PmlCommunicator _communicator;
			private UInt32 _id;
			private ChannelState _state;

			internal SubChannel(PmlCommunicator communicator, UInt32 sid, bool accepted) {
				_communicator = communicator;
				_id = sid;
				_state = accepted ? ChannelState.Acknowledged : ChannelState.Requesting;
				if (accepted) _communicator.AddSession(this);
			}

			public override bool IsOpen { get { return _state == ChannelState.Acknowledged; } }

			internal uint ID { get { return _id; } }
			internal void CloseIn() {
				_state = ChannelState.Closed;
				base.Close();
			}
			internal void MessageIn(PmlElement message) {
				base.PushReceivedMessage(message);
			}

			public override void WriteMessage(PmlElement message) {
				if (_state != ChannelState.Acknowledged) throw new InvalidOperationException("The subchannel is not open");
				_communicator.WriteSessionMessage(_id, 1, message);
			}
			public override void Close() {
				if (_state != ChannelState.Acknowledged) return;
				_state = ChannelState.Closed;
				_communicator.WriteSessionMessage(_id, 2, null);
				_communicator.RemoveSession(this); 
				base.Close();
			}
		}
		private class PmlChannelRequestReceivedEventArgsA : PmlChannelRequestReceivedEventArgs {
			private PmlCommunicator _communicator;
			private PmlElement _data;
			private bool _accepted, _rejected;
			private UInt32 _sid;
			internal PmlChannelRequestReceivedEventArgsA(PmlCommunicator communicator, UInt32 sid, PmlElement message) {
				_communicator = communicator;
				_data = message;
				_sid = sid;
				_accepted = _rejected = false;
			}
			public override IPmlChannel Accept() {
				if (_accepted || _rejected) throw new InvalidOperationException("The channel has already been accepted or rejected");
				_accepted = true;
				return new SubChannel(_communicator, _sid, true);
			}
			public override void Reject() {
				if (_accepted) throw new InvalidOperationException("The channel has already been accepted");
				if (_rejected) return;
				_rejected = true;
				_communicator.WriteSessionMessage(_sid, 2, null);
			}
			internal void RejectIfNotAccepted() {
				if (!_accepted) Reject();
			}
			public override PmlElement Data { get { return _data; } }
		}

		public event EventHandler<PmlCallReceivedEventArgs> CallReceived;
		public event EventHandler<PmlChannelRequestReceivedEventArgs> ChannelRequestReceived;
		public event EventHandler Closed;

		private Dictionary<UInt32, SubChannel> _sessions = new Dictionary<UInt32, SubChannel>();
		private Dictionary<UInt32, CSyncRequest> _invocations = new Dictionary<UInt32, CSyncRequest>();
		private UInt32 pNextSession;
		private UInt32 pNextSyncRequest;

		private bool _closed;
		private IPmlChannel _channel;

		public IPmlChannel Channel { get { return _channel; } }

		public PmlCommunicator(IPmlChannel channel, bool autoStart) {
			_channel = channel;
			if (autoStart) Start();
		}
		public void Start() {
			_channel.BeginReadMessage(messageReceived, null);
		}
		public void StartSync() {
			try {
				while (true) {
					processMessage(_channel.ReadMessage());
				}
			} catch (InvalidOperationException ex) {
				Console.WriteLine("InvalidOperationException in LegacyPmlCommunicator.messageReceived: " + ex.Message);
			} catch (Exception ex) {
				Console.WriteLine(ex.ToString());
			} finally {
				closed();
				_channel.Close();
			}
		}
		public void Close() {
			_channel.Close();
		}

		private void _WriteMessage(PmlElement Message) {
			lock (_channel) {
				if (!_channel.IsOpen) throw new InvalidOperationException("Could not write message: the channel is not open");
				_channel.WriteMessage(Message);
			}
		}
		private void closed() {
			_closed = true;
			lock (_sessions) {
				foreach (SubChannel item in _sessions.Values) item.CloseIn();
				_sessions.Clear();
			}
			lock (_invocations) {
				foreach (CSyncRequest item in _invocations.Values) item.Completed(null);
				_invocations.Clear();
			}
			if (Closed != null) Closed(this, new EventArgs());
		}

		private void messageReceived(IAsyncResult ar) {
			try {
				PmlElement Message = _channel.EndReadMessage(ar);
				processMessage(Message);
				_channel.BeginReadMessage(messageReceived, null);
			} catch (InvalidOperationException ex) {
				Console.WriteLine("InvalidOperationException in LegacyPmlCommunicator.messageReceived: " + ex.Message);
				closed();
				_channel.Close();
			} catch (Exception ex) {
				Console.WriteLine(ex.ToString());
				closed();
				_channel.Close();
			}
		}
		private void processMessage(PmlElement Message) {
			if (Message is PmlString) {
				String cmd = Message.ToString();
				if (cmd == "PING") {
					_WriteMessage("PONG");
				} else if (cmd == "PONG") {
				}
			} else if (Message is PmlDictionary) {
				String cmd = Message.GetChild("CMD").ToString();
				if (cmd == "SES") {
					processSessionMessage(Message);
				} else if (cmd == "RPL") {
					UInt32 SID = Message.GetChild("SID").ToUInt32();
					CSyncRequest SRequest;
					lock (_invocations) {
						if (!_invocations.TryGetValue(SID, out SRequest)) SRequest = null;
						_invocations.Remove(SID);
					}
					if (SRequest != null) SRequest.Completed((PmlDictionary)Message);
				} else if (cmd == "REQ" || cmd == "MSG") {
					UThreadPool.RunCall(processCall, Message);
				}
			}
		}
		private void processSessionMessage(PmlElement Message) {
			UInt32 SID = Message.GetChild("SID").ToUInt32();
			byte SCMD = Message.GetChild("SCMD").ToByte();
			PmlElement InnerMsg = Message.GetChild("MSG");
			SubChannel Session;
			lock (_sessions) if (!_sessions.TryGetValue(SID, out Session)) Session = null;
			switch (SCMD) {
				case 0: //Request
					if (Session != null) {
						RemoveSession(Session);
						Session.CloseIn();
						WriteSessionMessage(SID, 2, null);
					} else if (ChannelRequestReceived != null) {
						try {
							PmlChannelRequestReceivedEventArgsA ea = new PmlChannelRequestReceivedEventArgsA(this, SID, InnerMsg);
							ChannelRequestReceived(this, ea);
							ea.RejectIfNotAccepted();
						} catch (Exception ex) {
							Console.WriteLine("UCIS.Pml.PmlCommunicator.processSessionMessage: exception in ChannelRequestReceived: " + ex.ToString());
							WriteSessionMessage(SID, 2, null);
						}
					} else {
						WriteSessionMessage(SID, 2, null);
					}
					break;
				case 1: //Message
					if (Session != null) {
						Session.MessageIn(InnerMsg);
					} else {
						WriteSessionMessage(SID, 2, null);
					}
					break;
				case 2: //Close
					if (Session != null) {
						if (InnerMsg != null && !(InnerMsg is PmlNull)) Session.MessageIn(InnerMsg);
						RemoveSession(Session);
						Session.CloseIn();
					}
					break;
			}
		}
		private void processCall(object state) {
			PmlDictionary Message = (PmlDictionary)state;
			bool wantReply = Message.ContainsKey("SID");
			UInt32 SID = 0;
			if (wantReply) SID = Message.GetChild("SID").ToUInt32();
			PmlDictionary reply = new PmlDictionary() { { "CMD", "RPL" }, { "SID", SID } };
			try {
				if (CallReceived != null) {
					PmlCallReceivedEventArgs ea = new PmlCallReceivedEventArgs(Message.GetChild("MSG"), wantReply, SID);
					CallReceived(this, ea);
					reply.Add("MSG", ea.Reply);
				}
			} catch (Exception ex) {
				reply.Add("ERRMSG", ex.Message);
				reply.Add("ERRTXT", ex.ToString());
			}
			if (wantReply && Channel.IsOpen) {
				try {
					_WriteMessage(reply);
				} catch { }
			}
		}

		public void SendMessage(PmlElement message) {
			_WriteMessage(new PmlDictionary() { { "CMD", "MSG" }, { "MSG", message } });
		}
		public PmlElement Invoke(PmlElement message) {
			return Invoke(message, 60000);
		}
		public PmlElement Invoke(PmlElement message, int timeout) {
			if (_closed) throw new InvalidOperationException("Sorry, we're closed.");
			CSyncRequest SyncEvent = new CSyncRequest();
			UInt32 SID;
			lock (_invocations) {
				SID = GetNextSessionId(ref pNextSyncRequest, _invocations);
				_invocations.Add(SID, SyncEvent);
			}
			Boolean waitSuccess = false;
			try {
				_WriteMessage(new PmlDictionary() { { "CMD", "REQ" }, { "SID", SID }, { "MSG", message } });
				lock (SyncEvent) waitSuccess = SyncEvent.Reply != null || Monitor.Wait(SyncEvent, timeout);
			} finally {
				lock (_invocations) _invocations.Remove(SID);
			}
			if (!waitSuccess) throw new TimeoutException("The SyncRequest timed out (SID=" + SID.ToString() + ")");
			if (SyncEvent.Reply == null) throw new OperationCanceledException("The operation was aborted");
			PmlElement errmsg = SyncEvent.Reply.GetChild("ERRMSG");
			if (errmsg != null) {
				PmlElement errtxt = SyncEvent.Reply.GetChild("ERRTXT");
				throw new PmlRemoteException(errmsg.ToString(), errtxt == null ? null : errtxt.ToString());
			}
			return SyncEvent.Reply.GetChild("MSG");
		}

		public IPmlChannel CreateChannel(PmlElement data) {
			UInt32 sid;
			SubChannel ch;
			lock (_sessions) {
				sid = GetNextSessionId(ref pNextSession, _sessions);
				ch = new SubChannel(this, sid, true);
			}
			WriteSessionMessage(sid, 0, data);
			return ch;
		}

		private void AddSession(SubChannel session) {
			if (_closed) return;
			lock (_sessions) _sessions.Add(session.ID, session);
		}
		private void RemoveSession(SubChannel session) {
			if (_closed) return;
			lock (_sessions) _sessions.Remove(session.ID);
		}

		private static UInt32 GetNextSessionId<T>(ref UInt32 id, IDictionary<UInt32, T> dictionary) {
			lock (dictionary) {
				while (dictionary.ContainsKey(++id));
				return id;
			}
		}

		protected void WriteSessionMessage(UInt32 SID, byte CMD, PmlElement MSG) {
			PmlDictionary Msg2 = new PmlDictionary() {
				{ "CMD", "SES" },
				{ "SID", SID },
				{ "SCMD", CMD },
			};
			if (MSG != null) Msg2.Add("MSG", MSG);
			_WriteMessage(Msg2);
		}

		/* LegacyPmlCommunicator compatibility */
		public PmlElement SyncRequest(PmlElement Request) {
			return Invoke(Request);
		}
		public PmlElement SyncRequest(PmlElement Request, int Timeout) {
			return Invoke(Request, Timeout);
		}
	}
}
