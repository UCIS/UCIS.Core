using System;
using System.Collections.Generic;
using System.Text;
using UCIS.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;

namespace UCIS.Pml {
	/*public class PmlConnection : LegacyPmlCommunicator {
		public PmlConnection(Socket Socket) : this(new TCPPmlChannel(Socket)) { }
		public PmlConnection(TCPStream Stream) : this(new TCPPmlChannel(Stream)) { }
		public PmlConnection(Stream Stream) : this(new PmlBinaryRW(Stream)) {}
		public PmlConnection(IPmlRW RW) : this(new PmlChannel(RW)) { }
		public PmlConnection(IPmlChannel CH) : base(CH) { }
	}*/
	public class PmlConnection {
		private class CSyncRequest {
			internal PmlElement Reply;
			internal ManualResetEvent ResetEvent = new ManualResetEvent(false);
		}
		public abstract class SessionBase {
			private bool pActive;
			private PmlConnection pConnection;
			private UInt32 pSID;

			protected SessionBase(PmlConnection Connection) {
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
				if (!pActive) throw new InvalidOperationException("Session is not active");
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

			public Session(PmlConnection Connection) : base(Connection) { }

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

		private Stream pStream;

		public event MessageReceivedEventHandler MessageReceived;
		public delegate void MessageReceivedEventHandler(PmlElement Message);
		public event RequestReceivedEventHandler RequestReceived;
		public delegate void RequestReceivedEventHandler(PmlElement Request, ref PmlElement Reply);
		public event SessionRequestReceivedEventHandler SessionRequestReceived;
		public delegate void SessionRequestReceivedEventHandler(PmlElement Request, uint SID);

		private IPmlWriter _writer;
		private IPmlReader _reader;

		public PmlConnection(Socket Socket) : this(new TCPStream(Socket)) { }
		public PmlConnection(Stream Stream) : this(new PmlBinaryRW(Stream)) {
			pStream = Stream;
		}
		public PmlConnection(IPmlRW RMRW) : this(RMRW, RMRW) { }
		public PmlConnection(IPmlReader Reader, IPmlWriter Writer) {
			_reader = Reader;
			_writer = Writer;
		}

		public void Close() {
			if (pStream != null) pStream.Close();
		}

		public IPmlReader Reader {
			get { return _reader; }
		}
		public IPmlWriter Writer {
			get { return _writer; }
		}
		private PmlElement _ReadMessage() {
			PmlElement Message = _reader.ReadMessage();
			return Message;  //Warning: Can't lock reader because it can be the same as the Writer (possible deadlock)
		}
		private void _WriteMessage(PmlElement Message) {
			lock (_writer) _writer.WriteMessage(Message);
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

		public void Worker() {
			try {
				PmlElement Message = null;
				int Ping = 0;
				while (true) {
					try {
						Message = _ReadMessage();
						if (Message == null) Console.WriteLine("UCIS.PML.Connection: Message is just null?");
					} catch (EndOfStreamException) {
						Console.WriteLine("UCIS.PML.Connection: End of stream");
						return;
					} catch (SocketException ex) {
						if (ex.ErrorCode == (int)SocketError.TimedOut) {
							Console.WriteLine("UCIS.PML.Connection: SocketException/TimedOut");
							Message = null;
						} else if (ex.ErrorCode == (int)SocketError.ConnectionReset) {
							Console.WriteLine("UCIS.PML.Connection: Connection reset by peer");
							return;
						} else {
							throw new Exception("Exception while reading message", ex);
						}
					} catch (IOException ex) {
						Console.WriteLine("UCIS.PML.Connection: IOException: " + ex.Message);
						Message = null;
					} catch (TimeoutException) {
						Message = null;
					}
					if (Message == null) {
						if (Ping > 2) {
							Console.WriteLine("UCIS.PML.Connection: Connection timed out");
							break;
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
										SessionRequestReceived(InnerMsg, SID);
									}
								} else {
									Session.ClosedA();
									Session.ClosedB(null);
									WriteSessionMessage(SID, 2, null);
								}
							} else if (SCMD == 1) {
								if (Session == null) {
									WriteSessionMessage(SID, 2, null);
								} else {
									Session.MessageIn(InnerMsg);
								}
							} else if (SCMD == 2) {
								if (Session != null) {
									Session.ClosedA();
									Session.ClosedB(InnerMsg);
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
							System.Threading.ThreadPool.QueueUserWorkItem(new System.Threading.WaitCallback(SyncRequestHandler), Message);
						} else if (Cmd.Equals("MSG")) {
							PmlElement InnerMsg = Message.GetChild("MSG");
							if (MessageReceived != null) MessageReceived(InnerMsg);
						} else {
							throw new InvalidOperationException("Invalid operation");
						}
					}
				}
			} catch (System.Threading.ThreadAbortException ex) {
				throw ex;
			} catch (Exception ex) {
				Console.WriteLine(ex.ToString());
			} finally {
				Console.WriteLine("UCIS.PML.Connection: Connection closed");
				try {
					foreach (SessionBase S in pSessions.Values) {
						try {
							S.ClosedB(null);
						} catch (Exception ex) {
							Console.WriteLine(ex.ToString());
						}
					}
					pSessions.Clear();
					foreach (CSyncRequest T in pSyncRequests.Values) {
						T.ResetEvent.Set();
					}
				} catch (Exception ex) {
					Console.WriteLine(ex.ToString());
				}
			}
		}

		private void SyncRequestHandler(object state) {
			PmlDictionary Message = (PmlDictionary)state;
			UInt32 SID = default(UInt32);
			PmlElement InnerMsg = default(PmlElement);
			PmlElement Reply = default(PmlElement);
			Reply = null;
			SID = Message.GetChild("SID").ToUInt32();
			InnerMsg = Message.GetChild("MSG");
			try {
				if (RequestReceived != null) {
					RequestReceived(InnerMsg, ref Reply);
				}
			} catch (Exception ex) {
				Reply = new PmlDictionary();
				((PmlDictionary)Reply).Add("EXCEPTION", new PmlString(ex.ToString()));
				Console.WriteLine(ex.ToString());
			}
			WriteSyncMessage(SID, true, Reply);
		}

		public PmlElement SyncRequest(PmlElement Request) {
			return SyncRequest(Request, 30000);
		}
		public PmlElement SyncRequest(PmlElement Request, int Timeout) {
			UInt32 SID = default(UInt32);
			CSyncRequest SyncEvent = new CSyncRequest();
			SID = GetNextSessionId(false);
			lock (pSyncRequests) pSyncRequests.Add(SID, SyncEvent);
			WriteSyncMessage(SID, false, Request);
			if (!SyncEvent.ResetEvent.WaitOne(Timeout, false)) {
				Console.WriteLine("UCIS.PML.Connection.SyncRequest Timeout: " + SID.ToString());
				lock (pSyncRequests) pSyncRequests.Remove(SID);
				throw new TimeoutException();
			}
			return SyncEvent.Reply;
		}

		public void SendMessage(PmlElement Message) {
			PmlDictionary Msg = new PmlDictionary();
			Msg.Add("CMD", new PmlString("MSG"));
			Msg.Add("MSG", Message);
			_WriteMessage(Msg);
		}

		public PmlElement ReadMessage() {
			return _ReadMessage();
		}
		public void SendRawMessage(PmlElement Message) {
			_WriteMessage(Message);
		}
	}
}
