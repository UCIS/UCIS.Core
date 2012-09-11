using System;
using System.Threading;
using System.Collections.Generic;
using UCIS.Pml;

namespace UCIS.Pml {
	public class LegacyPmlCommunicator {
		private class CSyncRequest {
			internal PmlElement Reply;
			internal ManualResetEvent ResetEvent = new ManualResetEvent(false);
		}
		public abstract class SessionBase {
			private bool pActive;
			private LegacyPmlCommunicator pConnection;
			private UInt32 pSID;

			public uint SID { get { return pSID; } }
			public bool Active { get { return pActive; } }
			public LegacyPmlCommunicator Communicator { get { return pConnection; } }

			protected SessionBase(LegacyPmlCommunicator Connection) {
				pConnection = Connection;
			}

			protected void Accept(UInt32 SID) {
				if (pActive) throw new InvalidOperationException("Session is active");
				pSID = SID;
				lock (pConnection.pSessions) pConnection.pSessions.Add(pSID, this);
				pActive = true;
			}
			protected void Request() {
				Request(null);
			}
			protected void Request(PmlElement Message) {
				if (pActive) throw new InvalidOperationException("Session is active");
				pSID = pConnection.GetNextSessionId(true);
				lock (pConnection.pSessions) pConnection.pSessions.Add(pSID, this);
				pConnection.WriteSessionMessage(pSID, 0, Message);
				pActive = true;
			}

			protected internal abstract void MessageIn(PmlElement Message);

			protected void SendMessage(PmlElement Message) {
				if (!pActive) throw new InvalidOperationException("Session is not active");
				pConnection.WriteSessionMessage(pSID, 1, Message);
			}

			public void Close() {
				Close(null);
			}
			public void Close(PmlElement Message) {
				if (!pActive) return; // throw new InvalidOperationException("Session is not active");
				pConnection.WriteSessionMessage(pSID, 2, Message);
				ClosedA();
			}

			internal void ClosedA() {
				pActive = false;
				lock (pConnection.pSessions) pConnection.pSessions.Remove(pSID);
			}

			internal void ClosedB(PmlElement Message) {
				pActive = false;
				Closed(Message);
			}

			protected virtual void Closed(PmlElement Message) {
			}
		}
		public class Session : SessionBase {
			public event MessageReceivedEventHandler MessageReceived;
			public delegate void MessageReceivedEventHandler(PmlElement Message);
			public event SessionClosedEventHandler SessionClosed;
			public delegate void SessionClosedEventHandler(PmlElement Message);

			public Session(LegacyPmlCommunicator Connection) : base(Connection) { }

			public new void Accept(UInt32 SID) {
				base.Accept(SID);
			}
			public new void Request() {
				Request(null);
			}
			public new void Request(PmlElement Message) {
				base.Request(Message);
			}

			protected internal override void MessageIn(PmlElement Message) {
				if (MessageReceived != null) MessageReceived(Message);
			}

			public new void SendMessage(PmlElement Message) {
				base.SendMessage(Message);
			}

			protected override void Closed(PmlElement Message) {
				if (SessionClosed != null) SessionClosed(Message);
			}
		}

		private Dictionary<UInt32, SessionBase> pSessions = new Dictionary<UInt32, SessionBase>();
		private UInt32 pNextSession;
		private Dictionary<UInt32, CSyncRequest> pSyncRequests = new Dictionary<UInt32, CSyncRequest>();
		private UInt32 pNextSyncRequest;

		private IPmlChannel _channel;

		public event MessageReceivedEventHandler MessageReceived;
		public delegate void MessageReceivedEventHandler(PmlElement Message);
		public event RequestReceivedEventHandler RequestReceived;
		public delegate void RequestReceivedEventHandler(PmlElement Request, ref PmlElement Reply);
		public event SessionRequestReceivedEventHandler SessionRequestReceived;
		public delegate void SessionRequestReceivedEventHandler(PmlElement Request, uint SID);
		public event EventHandler Closed;

		public ICollection<SessionBase> Sessions { get { return (ICollection<SessionBase>)pSessions.Values; } }
		public int SyncRequests { get { return pSyncRequests.Count; } }

		public LegacyPmlCommunicator(IPmlChannel channel, bool autoStart) {
			_channel = channel;
			if (autoStart) Start();
			//_channel.BeginReadMessage(messageReceived, null);
			//_channel.MessageReceived += messageReceived;
			//_channel.Closed += closed;
		}
		public void Start() {
			_channel.BeginReadMessage(messageReceived, null);
		}

		public IPmlChannel Channel { get { return _channel; } }

		public void Close() {
			//_channel.MessageReceived -= messageReceived;
			//_channel.Closed -= closed;
			_channel.Close();
		}

		private void _WriteMessage(PmlElement Message) {
			lock (_channel) {
				if (_channel.IsOpen) {
					_channel.WriteMessage(Message);
				} else {
					throw new InvalidOperationException("Could not write message: the channel is not open");
				}
			}
		}

		private UInt32 GetNextSessionId(bool IsSession) {
			if (IsSession) {
				lock (pSessions) {
					do {
						if (pNextSession == UInt32.MaxValue) {
							pNextSession = 0;
						} else {
							pNextSession += (uint)1;
						}
					}
					while (pSessions.ContainsKey(pNextSession));
					return pNextSession;
				}
			} else {
				lock (pSyncRequests) {
					do {
						if (pNextSyncRequest == UInt32.MaxValue) {
							pNextSyncRequest = 0;
						} else {
							pNextSyncRequest += (uint)1;
						}
					}
					while (pSyncRequests.ContainsKey(pNextSyncRequest));
					return pNextSyncRequest;
				}
			}
		}

		protected void WriteSessionMessage(UInt32 SID, byte CMD, PmlElement MSG) {
			PmlDictionary Msg2 = new PmlDictionary();
			Msg2.Add("CMD", new PmlString("SES"));
			Msg2.Add("SID", new PmlInteger(SID));
			Msg2.Add("SCMD", new PmlInteger(CMD));
			Msg2.Add("MSG", MSG);
			_WriteMessage(Msg2);
		}

		protected void WriteSyncMessage(UInt32 SID, bool RPL, PmlElement MSG) {
			PmlDictionary Msg2 = new PmlDictionary();
			if (RPL) {
				Msg2.Add("CMD", new PmlString("RPL"));
			} else {
				Msg2.Add("CMD", new PmlString("REQ"));
			}
			Msg2.Add("SID", new PmlInteger(SID));
			Msg2.Add("MSG", MSG);
			_WriteMessage(Msg2);
		}

		private void messageReceived(IAsyncResult ar) {
			PmlElement Message;
			try {
				Message = _channel.EndReadMessage(ar);
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
			int Ping = 0;
			if (Message == null) {
				if (Ping > 2) {
					_channel.Close();
					return;
				} else {
					_WriteMessage(new PmlString("PING"));
				}
				Ping += 1;
			} else if (Message is PmlString) {
				string Cmd = Message.ToString();
				if (Cmd.Equals("PING")) {
					_WriteMessage(new PmlString("PONG"));
				} else if (Cmd.Equals("PONG")) {
					Ping = 0;
				}
			} else if (Message is PmlDictionary) {
				string Cmd = null;
				Cmd = Message.GetChild("CMD").ToString();
				if (Cmd.Equals("SES")) {
					UInt32 SID = default(UInt32);
					byte SCMD = 0;
					SessionBase Session = default(SessionBase);
					PmlElement InnerMsg = default(PmlElement);
					SID = Message.GetChild("SID").ToUInt32();
					SCMD = Message.GetChild("SCMD").ToByte();
					InnerMsg = Message.GetChild("MSG");
					lock (pSessions) {
						if (pSessions.ContainsKey(SID)) {
							Session = pSessions[SID];
						} else {
							Session = null;
						}
					}
					if (SCMD == 0) {
						if (Session == null) {
							if (SessionRequestReceived != null) {
								try {
									SessionRequestReceived(InnerMsg, SID);
								} catch (Exception ex) {
									Console.WriteLine("Exception in LegacyPmlCommnuicator.messageReceived->SessionRequestReceived: " + ex.ToString());
									WriteSessionMessage(SID, 2, null);
								}
							}
						} else {
							try {
								Session.ClosedA();
								Session.ClosedB(null);
							} catch (Exception ex) {
								Console.WriteLine("Exception in LegacyPmlCommnuicator.messageReceived->Session.ClosedA/B: " + ex.ToString());
							}
							WriteSessionMessage(SID, 2, null);
						}
					} else if (SCMD == 1) {
						if (Session == null) {
							WriteSessionMessage(SID, 2, null);
						} else {
							try {
								Session.MessageIn(InnerMsg);
							} catch (Exception ex) {
								Console.WriteLine("Exception in LegacyPmlCommnuicator.messageReceived->Session.MessageIn: " + ex.ToString());
								WriteSessionMessage(SID, 2, null);
							}
						}
					} else if (SCMD == 2) {
						if (Session != null) {
							try {
								Session.ClosedA();
								Session.ClosedB(InnerMsg);
							} catch (Exception ex) {
								Console.WriteLine("Exception in LegacyPmlCommnuicator.messageReceived->Session.ClosedA/B: " + ex.ToString());
							}
						}
					}
				} else if (Cmd.Equals("RPL")) {
					UInt32 SID = default(UInt32);
					CSyncRequest SRequest = null;
					SID = Message.GetChild("SID").ToUInt32();
					lock (pSyncRequests) {
						if (pSyncRequests.TryGetValue(SID, out SRequest)) {
							pSyncRequests.Remove(SID);
						} else {
							Console.WriteLine("UCIS.PML.Connection.Worker Invalid request ID in reply: " + SID.ToString());
						}
					}
					if (SRequest != null) {
						SRequest.Reply = Message.GetChild("MSG");
						SRequest.ResetEvent.Set();
					}
				} else if (Cmd.Equals("REQ")) {
					UCIS.ThreadPool.RunCall(SyncRequestHandler, Message);
					//System.Threading.ThreadPool.QueueUserWorkItem(new System.Threading.WaitCallback(SyncRequestHandler), Message);
				} else if (Cmd.Equals("MSG")) {
					PmlElement InnerMsg = Message.GetChild("MSG");
					if (MessageReceived != null) MessageReceived(InnerMsg);
				} else {
					throw new InvalidOperationException("Invalid operation");
				}
			}
		}
		private void closed() {
			//_channel.MessageReceived -= messageReceived;
			//_channel.Closed -= closed;
			Console.WriteLine("UCIS.PML.Connection: Connection closed");
			try {
				SessionBase[] sessions;
				lock (pSessions) {
					sessions = new SessionBase[pSessions.Count];
					pSessions.Values.CopyTo(sessions, 0);
				}
				foreach (SessionBase S in sessions) {
					try {
						S.ClosedB(null);
					} catch (Exception ex) {
						Console.WriteLine(ex.ToString());
					}
				}
			} catch (Exception ex) {
				Console.WriteLine(ex.ToString());
			}
			lock (pSessions) pSessions.Clear();
			try {
				CSyncRequest[] reqs;
				lock (pSyncRequests) {
					reqs = new CSyncRequest[pSyncRequests.Count];
					pSyncRequests.Values.CopyTo(reqs, 0);
				}
				foreach (CSyncRequest T in reqs) {
					T.ResetEvent.Set();
				}
			} catch (Exception ex) {
				Console.WriteLine(ex.ToString());
			}
			lock (pSyncRequests) pSyncRequests.Clear();
			if (Closed != null) Closed(this, new EventArgs());
		}

		private void SyncRequestHandler(object state) {
			PmlDictionary Message = (PmlDictionary)state;
			PmlElement Reply = null;
			UInt32 SID = 0;
			try {
				SID = Message.GetChild("SID").ToUInt32();
				PmlElement InnerMsg = Message.GetChild("MSG");
				if (RequestReceived != null) {
					RequestReceived(InnerMsg, ref Reply);
				}
			} catch (Exception ex) {
				Reply = new PmlDictionary();
				((PmlDictionary)Reply).Add("EXCEPTION", new PmlString(ex.ToString()));
				Console.WriteLine(ex.ToString());
			}
			try {
				WriteSyncMessage(SID, true, Reply);
			} catch (Exception ex) {
				Console.WriteLine("Exception: " + ex.ToString());
			}
		}

		public PmlElement SyncRequest(PmlElement Request) {
			return SyncRequest(Request, 30000);
		}
		public PmlElement SyncRequest(PmlElement Request, int Timeout) {
			CSyncRequest SyncEvent = new CSyncRequest();
			UInt32 SID = GetNextSessionId(false);
			lock (pSyncRequests) pSyncRequests.Add(SID, SyncEvent);
			try {
				WriteSyncMessage(SID, false, Request);
				if (!SyncEvent.ResetEvent.WaitOne(Timeout, false)) {
					lock (pSyncRequests) pSyncRequests.Remove(SID);
					throw new TimeoutException("The SyncRequest timed out (SID=" + SID.ToString() + ")");
				}
			} finally {
				lock (pSyncRequests) pSyncRequests.Remove(SID);
			}
			return SyncEvent.Reply;
		}

		public void SendMessage(PmlElement Message) {
			PmlDictionary Msg = new PmlDictionary();
			Msg.Add("CMD", new PmlString("MSG"));
			Msg.Add("MSG", Message);
			_WriteMessage(Msg);
		}

		public void SendRawMessage(PmlElement Message) {
			_WriteMessage(Message);
		}
	}
}
