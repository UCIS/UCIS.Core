using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UCIS.Util;

namespace UCIS.Net.HTTP {
	public class WebSocketPacketStream : PacketStream {
		PrebufferingStream baseStream;
		Boolean closed = false;
		Boolean binaryProtocol = false;
		int wsProtocol = -1;

		public WebSocketPacketStream(HTTPContext context) {
			try {
				String ConnectionHeader = context.GetRequestHeader("Connection"); //can be comma-separated list
				Boolean ConnectionUpgrade = ConnectionHeader != null && ConnectionHeader.Contains("Upgrade");
				Boolean UpgradeWebsocket = "WebSocket".Equals(context.GetRequestHeader("Upgrade"), StringComparison.OrdinalIgnoreCase);
				String SecWebSocketKey = context.GetRequestHeader("Sec-WebSocket-Key");
				String SecWebSocketKey1 = context.GetRequestHeader("Sec-WebSocket-Key1");
				String SecWebSocketKey2 = context.GetRequestHeader("Sec-WebSocket-Key2");
				String SecWebSocketProtocol = context.GetRequestHeader("Sec-WebSocket-Protocol");
				String[] SecWebSocketProtocols = SecWebSocketProtocol == null ? null : SecWebSocketProtocol.Split(new String[] { ", " }, StringSplitOptions.None);
				if (!ConnectionUpgrade || !UpgradeWebsocket) throw new InvalidOperationException("The HTTP context does not contain a WebSocket request");
				binaryProtocol = SecWebSocketProtocols != null && Array.IndexOf(SecWebSocketProtocols, "binary") != -1;
				if (SecWebSocketKey != null) {
					wsProtocol = 13;
					String hashedKey;
					using (SHA1 sha1 = SHA1Managed.Create()) {
						Byte[] hashable = Encoding.ASCII.GetBytes(SecWebSocketKey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11");
						Byte[] hash = sha1.ComputeHash(hashable);
						hashedKey = Convert.ToBase64String(hash, Base64FormattingOptions.None);
					}
					context.SendStatus(101);
					context.SetResponseHeader("Connection", "Upgrade");
					context.SetResponseHeader("Upgrade", "websocket");
					context.SetResponseHeader("Sec-WebSocket-Accept", hashedKey);
					if (SecWebSocketProtocols != null) context.SetResponseHeader("Sec-WebSocket-Protocol", binaryProtocol ? "binary" : "base64");
					Stream rawstream = context.GetDirectStream();
					baseStream = rawstream as PrebufferingStream ?? new PrebufferingStream(rawstream);
				} else if (SecWebSocketKey1 != null && SecWebSocketKey2 != null) {
					wsProtocol = 100;
					Byte[] key = new Byte[4 + 4 + 8];
					CalculateHybi00MagicNumber(SecWebSocketKey1, key, 0);
					CalculateHybi00MagicNumber(SecWebSocketKey2, key, 4);
					context.SendStatus(101);
					context.SetResponseHeader("Connection", "Upgrade");
					context.SetResponseHeader("Upgrade", "websocket");
					if (SecWebSocketProtocols != null) context.SetResponseHeader("Sec-WebSocket-Protocol", binaryProtocol ? "binary" : "base64");
					context.SendHeader("Sec-WebSocket-Origin", context.GetRequestHeader("Origin"));
					context.SendHeader("Sec-WebSocket-Location", (context.IsSecure ? "wss://" : "ws://") + context.GetRequestHeader("Host") + context.RequestPath);
					Stream rawstream = context.GetDirectStream();
					baseStream = rawstream as PrebufferingStream ?? new PrebufferingStream(rawstream);
					baseStream.ReadAll(key, 8, 8);
					using (MD5 md5 = MD5.Create()) key = md5.ComputeHash(key);
					baseStream.Write(key, 0, key.Length);
				} else {
					throw new InvalidOperationException("Unsupported WebSocket request");
				}
			} catch (Exception) {
				closed = true;
				if (baseStream != null) baseStream.Close();
				throw;
			}
		}

		private void CalculateHybi00MagicNumber(String s, Byte[] obuf, int opos) {
			long number = 0;
			long spaces = 0;
			foreach (Char c in s) {
				if (c == ' ') {
					spaces++;
				} else if (c >= '0' && c <= '9') {
					number = number * 10 + (c - '0');
				}
			}
			number /= spaces;
			obuf[opos++] = (Byte)(number >> 24);
			obuf[opos++] = (Byte)(number >> 16);
			obuf[opos++] = (Byte)(number >> 8);
			obuf[opos++] = (Byte)(number >> 0);
		}

		public override bool CanRead { get { return !closed && baseStream.CanRead; } }
		public override bool CanSeek { get { return false; } }
		public override bool CanWrite { get { return !closed && baseStream.CanWrite; } }
		public override void Flush() { }

		public override void Close() {
			closed = true;
			base.Close();
			if (baseStream != null) baseStream.Close();
		}
		public override bool CanTimeout { get { return baseStream.CanTimeout; } }
		public override int ReadTimeout {
			get { return baseStream.ReadTimeout; }
			set { baseStream.ReadTimeout = value; }
		}
		public override int WriteTimeout {
			get { return baseStream.WriteTimeout; }
			set { baseStream.WriteTimeout = value; }
		}

		public override long Length { get { throw new NotSupportedException(); } }
		public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
		public override void SetLength(long value) { throw new NotSupportedException(); }
		public override long Position {
			get { throw new NotSupportedException(); }
			set { throw new NotSupportedException(); }
		}

		Byte[] leftOver = null;
		public override int Read(byte[] buffer, int offset, int count) {
			Byte[] packet = leftOver;
			leftOver = null;
			if (packet == null) packet = ReadPacket();
			if (packet == null) return 0;
			if (count > packet.Length) count = packet.Length;
			Buffer.BlockCopy(packet, 0, buffer, offset, count);
			if (packet.Length > count) leftOver = ArrayUtil.Slice(packet, count);
			return count;
		}
		private int ReadRawMessage(out Byte[] payloadret) {
			if (closed) throw new ObjectDisposedException("WebSocketPacketStream");
			if (wsProtocol == 13) {
				Byte[] multipartbuffer = null;
				int multipartopcode = -1;
				while (true) {
					int flags = baseStream.ReadByte();
					if (flags == -1) throw new EndOfStreamException();
					UInt64 pllen = (byte)baseStream.ReadByte();
					Boolean masked = (pllen & 128) != 0;
					pllen &= 127;
					if (pllen == 126) {
						pllen = (uint)baseStream.ReadByte() << 8;
						pllen |= (uint)baseStream.ReadByte();
					} else if (pllen == 127) {
						pllen = (ulong)baseStream.ReadByte() << 56;
						pllen |= (ulong)baseStream.ReadByte() << 48;
						pllen |= (ulong)baseStream.ReadByte() << 40;
						pllen |= (ulong)baseStream.ReadByte() << 32;
						pllen |= (uint)baseStream.ReadByte() << 24;
						pllen |= (uint)baseStream.ReadByte() << 16;
						pllen |= (uint)baseStream.ReadByte() << 8;
						pllen |= (uint)baseStream.ReadByte();
					}
					Byte[] mask = new Byte[4];
					if (masked) baseStream.ReadAll(mask, 0, mask.Length);
					//Console.WriteLine("Read flags={0} masked={1} mask={2} len={3}", flags, masked, mask, pllen);
					Byte[] payload = new Byte[pllen]; // + (4 - (pllen % 4))];
					baseStream.ReadAll(payload, 0, (int)pllen);
					if (masked) for (int i = 0; i < (int)pllen; i++) payload[i] ^= mask[i % 4];
					int opcode = flags & 0x0f;
					Boolean fin = (flags & 0x80) != 0;
					if (opcode == 0) {
						//Console.WriteLine("WebSocket received continuation frame type {0}!", multipartopcode);
						Array.Resize(ref multipartbuffer, multipartbuffer.Length + payload.Length);
						payload.CopyTo(multipartbuffer, multipartbuffer.Length - payload.Length);
						opcode = -1;
						if (fin) {
							payload = multipartbuffer;
							opcode = multipartopcode;
							multipartbuffer = null;
						}
					} else if (!fin) {
						//Console.WriteLine("WebSocket received non-fin frame type {0}!", opcode);
						multipartbuffer = payload;
						multipartopcode = opcode;
						opcode = -1;
					}
					if (opcode == -1) {
					} else if (opcode == 0) {
						throw new NotSupportedException("WebSocket opcode 0 is not supported");
					} else if (opcode == 1) {
						payloadret = payload;
						return 1; //text frame
					} else if (opcode == 2) {
						payloadret = payload;
						return 2; //binary frame
					} else if (opcode == 8) {
						payloadret = null;
						return 0; //end of stream
					} else if (opcode == 9) {
						//Console.WriteLine("WebSocket PING");
						WriteProtocol13Frame(10, payload, 0, (int)pllen);
					} else if (opcode == 10) { //PONG
					} else {
						//Console.WriteLine("WebSocket UNKNOWN OPCODE {0}", opcode);
					}
				}
			} else if (wsProtocol == 100) {
				int frameType = baseStream.ReadByte();
				if (frameType == -1) throw new EndOfStreamException();
				if ((frameType & 0x80) != 0) {
					int length = 0;
					while (true) {
						int b = baseStream.ReadByte();
						if (b == -1) throw new EndOfStreamException();
						length = (length << 7) | (b & 0x7f);
						if ((b & 0x80) == 0) break;
					}
					Byte[] buffer = new Byte[length];
					baseStream.ReadAll(buffer, 0, length);
					if (frameType == 0xff && length == 0) {
						payloadret = null;
						return 0;
					} else {
						throw new InvalidOperationException();
					}
				} else {
					using (MemoryStream ms = new MemoryStream()) {
						while (true) {
							int b = baseStream.ReadByte();
							if (b == -1) throw new EndOfStreamException();
							if (b == 0xff) break;
							ms.WriteByte((Byte)b);
						}
						if (frameType == 0x00) {
							ms.Seek(0, SeekOrigin.Begin);
							payloadret = ms.ToArray();
							return 1; //text frame
						} else {
							throw new InvalidOperationException();
						}
					}
				}
			} else {
				throw new InvalidOperationException();
			}
		}
		public override byte[] ReadPacket() {
			if (leftOver != null) throw new InvalidOperationException("There is remaining data from a partial read");
			Byte[] payload;
			int opcode = ReadRawMessage(out payload);
			switch (opcode) {
				case 0: return null;
				case 1: return Convert.FromBase64String(Encoding.UTF8.GetString(payload));
				case 2: return payload;
				default: throw new InvalidOperationException("Internal error: unexpected frame type");
			}
		}
		public override void Write(byte[] buffer, int offset, int count) {
			if (!binaryProtocol) {
				String encoded = Convert.ToBase64String(buffer, offset, count, Base64FormattingOptions.None);
				buffer = Encoding.ASCII.GetBytes(encoded);
				offset = 0;
				count = buffer.Length;
			}
			WriteRawMessage(buffer, offset, count, binaryProtocol);
		}
		private void WriteRawMessage(Byte[] buffer, int offset, int count, Boolean binary) {
			if (closed) throw new ObjectDisposedException("WebSocketPacketStream");
			if (wsProtocol == 13) {
				WriteProtocol13Frame(binary ? (Byte)0x2 : (Byte)0x1, buffer, offset, count);
			} else if (wsProtocol == 100) {
				Byte[] bytes = new Byte[2 + count];
				bytes[0] = 0x00;
				Buffer.BlockCopy(buffer, offset, bytes, 1, count);
				bytes[1 + count] = 0xff;
				baseStream.Write(bytes, 0, bytes.Length);
			} else {
				throw new InvalidOperationException();
			}
		}
		private void WriteProtocol13Frame(Byte opcode, Byte[] buffer, int offset, int count) {
			int pllen = count;
			int hlen = 2;
			if (pllen > 0xffff) hlen += 8;
			else if (pllen > 125) hlen += 2;
			Byte[] wbuf = new Byte[count + hlen];
			wbuf[0] = (Byte)(opcode | 0x80);
			if (pllen > 0xffff) {
				wbuf[1] = 127;
				wbuf[2] = 0;
				wbuf[3] = 0;
				wbuf[4] = 0;
				wbuf[5] = 0;
				wbuf[6] = (Byte)(pllen >> 24);
				wbuf[7] = (Byte)(pllen >> 16);
				wbuf[8] = (Byte)(pllen >> 8);
				wbuf[9] = (Byte)(pllen >> 0);
			} else if (pllen > 125) {
				wbuf[1] = 126;
				wbuf[2] = (Byte)(pllen >> 8);
				wbuf[3] = (Byte)(pllen >> 0);
			} else {
				wbuf[1] = (Byte)pllen;
			}
			Buffer.BlockCopy(buffer, offset, wbuf, hlen, count);
			baseStream.Write(wbuf, 0, wbuf.Length);
		}

		public String ReadTextMessage() {
			if (leftOver != null) throw new InvalidOperationException("There is remaining data from a partial read");
			Byte[] payload;
			int opcode = ReadRawMessage(out payload);
			switch (opcode) {
				case 0: return null;
				case 1:
				case 2: return Encoding.UTF8.GetString(payload);
				default: throw new InvalidOperationException("Internal error: unexpected frame type");
			}
		}
		public void WriteTextMessage(String message) {
			Byte[] packet = Encoding.UTF8.GetBytes(message);
			WriteRawMessage(packet, 0, packet.Length, false);
		}

		class AsyncResult : AsyncResultBase {
			public Byte[] Buffer { get; private set; }
			public int Opcode { get; private set; }
			public AsyncResult(AsyncCallback callback, Object state) : base(callback, state) { }
			public void SetCompleted(Boolean synchronously, Byte[] buffer, int opcode, Exception error) {
				this.Buffer = buffer;
				this.Opcode = opcode;
				base.SetCompleted(synchronously, error);
			}
			public new void WaitForCompletion() {
				base.WaitForCompletion();
				ThrowError();
			}
		}
		private void AsyncReadReady(IAsyncResult ar) {
			AsyncResult myar = (AsyncResult)ar.AsyncState;
			try {
				baseStream.EndPrebuffering(ar);
				Byte[] payload;
				int opcode = ReadRawMessage(out payload);
				myar.SetCompleted(ar.CompletedSynchronously, payload, opcode, null);
			} catch (Exception ex) {
				myar.SetCompleted(ar.CompletedSynchronously, null, 0, ex);
			}
		}
		public override IAsyncResult BeginReadPacket(AsyncCallback callback, object state) {
			if (leftOver != null) throw new InvalidOperationException("There is remaining data from a partial read");
			AsyncResult ar = new AsyncResult(callback, state);
			baseStream.BeginPrebuffering(AsyncReadReady, ar);
			return ar;
		}
		public override byte[] EndReadPacket(IAsyncResult asyncResult) {
			AsyncResult ar = (AsyncResult)asyncResult;
			switch (ar.Opcode) {
				case 0: return null;
				case 1: return Convert.FromBase64String(Encoding.UTF8.GetString(ar.Buffer));
				case 2: return ar.Buffer;
				default: throw new InvalidOperationException("Internal error: unexpected frame type");
			}
		}
		public IAsyncResult BeginReadTextMessage(AsyncCallback callback, object state) {
			return BeginReadPacket(callback, state);
		}
		public String EndReadTextMessage(IAsyncResult asyncResult) {
			AsyncResult ar = (AsyncResult)asyncResult;
			switch (ar.Opcode) {
				case 0: return null;
				case 1:
				case 2: return Encoding.UTF8.GetString(ar.Buffer);
				default: throw new InvalidOperationException("Internal error: unexpected frame type");
			}
		}
	}
}
