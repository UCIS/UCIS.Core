using System;
using System.Threading;
using System.Collections.Generic;
using UCIS.Pml;

namespace UCIS.Pml {
	public class PmlCommunicator {
		private class CSyncRequest {
			internal PmlElement Reply;
		}
		private interface ISession {
			void MessageIn(PmlElement message);
			void CloseIn();
			UInt32 ID { get; }
		}
		private class PmlSubChannel : ActivePmlChannel, ISession {
			private enum ChannelState { Requesting, Acknowledged, Closed }

			private PmlCommunicator _communicator;
			private UInt32 _id;
			private ChannelState _state;

			internal PmlSubChannel(PmlCommunicator communicator, UInt32 sid, bool accepted) {
				_communicator = communicator;
				_id = sid;
				_state = accepted ? ChannelState.Acknowledged : ChannelState.Requesting;
				if (accepted) _communicator.AddSession(this);
			}

			public override bool IsOpen { get { return _state == ChannelState.Acknowledged; } }

			uint ISession.ID { get { return _id; } }
			void ISession.CloseIn() {
				_state = ChannelState.Closed;
				_communicator.RemoveSession(this);
				base.Close();
			}
			void ISession.MessageIn(PmlElement message) {
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
				return new PmlSubChannel(_communicator, _sid, true);
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

		private Dictionary<UInt32, ISession> _sessions = new Dictionary<UInt32, ISession>();
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
			while (true) {
				try {
					processMessage(_channel.ReadMessage());
				} catch (InvalidOperationException ex) {
					Console.WriteLine("InvalidOperationException in LegacyPmlCommunicator.messageReceived: " + ex.Message);
					closed();
					_channel.Close();
					return;
				} catch (Exception ex) {
					Console.WriteLine(ex.ToString());
					closed();
					_channel.Close();
					return;
				}
			}
		}
		public void Close() {
			_channel.Close();
		}
		public void WriteRawMessage(PmlElement Message) {
			_WriteMessage(Message);
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
				foreach (ISession S in _sessions.Values) {
					try {
						S.CloseIn();
					} catch (Exception ex) {
						Console.WriteLine(ex.ToString());
					}
				}
				_sessions.Clear();
			}
			lock (_invocations) {
				foreach (CSyncRequest T in _invocations.Values) lock (T) Monitor.Pulse(T);
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
				return;
			} catch (Exception ex) {
				Console.WriteLine(ex.ToString());
				closed();
				_channel.Close();
				return;
			}
		}
		private void processMessage(PmlElement Message) {
			if (Message is PmlString) {
				string Cmd = Message.ToString();
				if (Cmd.Equals("PING")) {
					_WriteMessage("PONG");
				} else if (Cmd.Equals("PONG")) {
				}
			} else if (Message is PmlDictionary) {
				string Cmd = Message.GetChild("CMD").ToString();
				if (Cmd.Equals("SES")) {
					processSessionMessage(Message);
				} else if (Cmd.Equals("RPL")) {
					UInt32 SID = Message.GetChild("SID").ToUInt32();
					CSyncRequest SRequest = null;
					lock (_invocations) {
						if (_invocations.TryGetValue(SID, out SRequest)) {
							_invocations.Remove(SID);
						} else {
							Console.WriteLine("UCIS.PML.Connection.Worker Invalid request ID in reply: " + SID.ToString());
						}
					}
					if (SRequest != null) {
						SRequest.Reply = Message.GetChild("MSG");
						lock (SRequest) Monitor.Pulse(SRequest);
					}
				} else if (Cmd.Equals("REQ") || Cmd.Equals("MSG")) {
					UThreadPool.RunCall(processCall, Message);
				} else {
					Console.WriteLine("UCIS.PML.Connection.Worker Invalid command received");
				}
			}
		}
		private void processSessionMessage(PmlElement Message) {
			UInt32 SID = Message.GetChild("SID").ToUInt32();
			byte SCMD = Message.GetChild("SCMD").ToByte();
			PmlElement InnerMsg = Message.GetChild("MSG");
			ISession Session = null;
			lock (_sessions) if (!_sessions.TryGetValue(SID, out Session)) Session = null;
			switch (SCMD) {
				case 0: //Request
					if (Session != null) {
						try {
							Session.CloseIn();
						} catch (Exception ex) {
							Console.WriteLine("UCIS.Pml.PmlCommunicator.processSessionMessage-Request: exception in session.CloseIn: " + ex.ToString());
						}
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
						try {
							Session.MessageIn(InnerMsg);
						} catch (Exception ex) {
							Console.WriteLine("UCIS.Pml.PmlCommunicator.processSessionMessage: exception in session.MessageIn: " + ex.ToString());
							WriteSessionMessage(SID, 2, null);
						}
					} else {
						WriteSessionMessage(SID, 2, null);
					}
					break;
				case 2: //Close
					if (Session != null) {
						try {
							if (InnerMsg != null && !(InnerMsg is PmlNull)) Session.MessageIn(InnerMsg);
						} catch (Exception ex) {
							Console.WriteLine("UCIS.Pml.PmlCommunicator.processSessionMessage-Close: exception in session.MessageIn: " + ex.ToString());
						} finally {
							try {
								Session.CloseIn();
							} catch (Exception ex) {
								Console.WriteLine("UCIS.Pml.PmlCommunicator.processSessionMessage: exception in session.CloseIn: " + ex.ToString());
							}
						}
					}
					break;
			}
		}
		private void processCall(object state) {
			PmlDictionary Message = (PmlDictionary)state;
			bool wantReply = Message.ContainsKey("SID");
			UInt32 SID = 0;
			if (wantReply) SID = Message.GetChild("SID").ToUInt32();
			PmlElement Reply = null;
			try {
				if (CallReceived != null) {
					PmlCallReceivedEventArgs ea = new PmlCallReceivedEventArgs(Message.GetChild("MSG"), wantReply, SID);
					CallReceived(this, ea);
					Reply = ea.Reply;
				}
			} catch (Exception ex) {
				Reply = new PmlDictionary();
				((PmlDictionary)Reply).Add("EXCEPTION", new PmlString(ex.ToString()));
				Console.WriteLine(ex.ToString());
			} finally {
				if (wantReply && Channel.IsOpen) {
					try {
						WriteSyncMessage(SID, true, Reply);
					} catch (Exception ex) {
						Console.WriteLine("UCIS.Pml.PmlCommunicator.processCall: exception: " + ex.ToString());
						closed();
						Channel.Close();
					}
				}
			}
		}

		public void Call(PmlElement message) {
			PmlDictionary Msg = new PmlDictionary() { { "CMD", "MSG" }, { "MSG", message } };
			_WriteMessage(Msg);
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
			try {
				WriteSyncMessage(SID, false, message);
				Boolean success;
				lock (SyncEvent) success = Monitor.Wait(SyncEvent, timeout);
				if (!success) throw new TimeoutException("The SyncRequest timed out (SID=" + SID.ToString() + ")");
			} finally {
				lock (_invocations) _invocations.Remove(SID);
			}
			return SyncEvent.Reply;
		}

		public IPmlChannel CreateChannel(PmlElement data) {
			UInt32 sid = GetNextSessionId(ref pNextSession, _sessions);
			PmlSubChannel ch = new PmlSubChannel(this, sid, true);
			WriteSessionMessage(sid, 0, data);
			if (!ch.IsOpen) return null;
			return ch;
		}

		private void AddSession(ISession session) {
			if (_closed) return;
			lock (_sessions) _sessions.Add(session.ID, session);
		}
		private void RemoveSession(UInt32 session) {
			if (_closed) return;
			lock (_sessions) _sessions.Remove(session);
		}
		private void RemoveSession(ISession session) {
			RemoveSession(session.ID);
		}

		private static UInt32 GetNextSessionId<T>(ref UInt32 id, IDictionary<UInt32, T> dictionary) {
			lock (dictionary) {
				do {
					id++;
				} while (dictionary.ContainsKey(id));
				return id;
			}
		}

		protected void WriteSyncMessage(UInt32 SID, bool RPL, PmlElement MSG) {
			PmlDictionary Msg2 = new PmlDictionary() {
				{ "CMD", RPL ? "RPL" : "REQ" },
				{ "SID", SID },
				{ "MSG", MSG },
			};
			_WriteMessage(Msg2);
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
		public void SendMessage(PmlElement Message) {
			Call(Message);
		}
	}
}
