using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UCIS.Util;

namespace UCIS.Net.HTTP {
	public class WebSocketPacketStream : PacketStream {
		Stream baseStream;
		Boolean negotiationDone = false;
		Boolean closed = false;
		ManualResetEvent negotiationEvent = new ManualResetEvent(false);
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
				if (!ConnectionUpgrade || !UpgradeWebsocket || SecWebSocketProtocols == null || SecWebSocketProtocols.Length == 0) goto Failure;
				binaryProtocol = SecWebSocketProtocol != null && Array.IndexOf(SecWebSocketProtocols, "binary") != -1;
				if (SecWebSocketKey != null) {
					wsProtocol = 13;
					String hashedKey;
					using (SHA1 sha1 = new SHA1Managed()) {
						Byte[] hashable = Encoding.ASCII.GetBytes(SecWebSocketKey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11");
						Byte[] hash = sha1.ComputeHash(hashable);
						hashedKey = Convert.ToBase64String(hash, Base64FormattingOptions.None);
					}
					context.SuppressStandardHeaders = true;
					context.SendStatus(101);
					context.SendHeader("Connection", "Upgrade");
					context.SendHeader("Upgrade", "websocket");
					context.SendHeader("Sec-WebSocket-Accept", hashedKey);
					context.SendHeader("Sec-WebSocket-Protocol", binaryProtocol ? "binary" : "base64");
					baseStream = context.GetDirectStream();
				} else if (SecWebSocketKey1 != null && SecWebSocketKey2 != null) {
					wsProtocol = 100;
					Byte[] key = new Byte[4 + 4 + 8];
					CalculateHybi00MagicNumber(SecWebSocketKey1, key, 0);
					CalculateHybi00MagicNumber(SecWebSocketKey2, key, 4);
					context.SuppressStandardHeaders = true;
					context.SendStatus(101);
					context.SendHeader("Connection", "Upgrade");
					context.SendHeader("Upgrade", "websocket");
					context.SendHeader("Sec-WebSocket-Protocol", binaryProtocol ? "binary" : "base64");
					context.SendHeader("Sec-WebSocket-Origin", context.GetRequestHeader("Origin"));
					context.SendHeader("Sec-WebSocket-Location", "ws://" + context.GetRequestHeader("Host") + context.RequestPath);
					baseStream = context.GetDirectStream();
					ReadAllBytes(key, 8, 8);
					using (MD5 md5 = new MD5CryptoServiceProvider()) key = md5.ComputeHash(key);
					baseStream.Write(key, 0, key.Length);
				} else {
					goto Failure;
				}
				if (closed) baseStream.Close();
			} catch (Exception) {
				closed = true;
				if (baseStream != null) baseStream.Close();
			} finally {
				negotiationDone = true;
				negotiationEvent.Set();
			}
			return;
Failure:
			closed = true;
			context.SendErrorResponse(400);
			return;
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

		public override bool CanRead { get { return negotiationDone && !closed; } }
		public override bool CanSeek { get { return false; } }
		public override bool CanWrite { get { return negotiationDone && !closed; } }
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

		private void ReadAllBytes(Byte[] buffer, int offset, int count) {
			while (count > 0) {
				int l = baseStream.Read(buffer, offset, count);
				if (l <= 0) throw new EndOfStreamException();
				offset += l;
				count -= l;
			}
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
		public override byte[] ReadPacket() {
			if (leftOver != null) throw new InvalidOperationException("There is remaining data from a partial read");
			negotiationEvent.WaitOne();
			if (closed) throw new ObjectDisposedException("WebSocketPacketStream");
			try {
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
						if (masked) ReadAllBytes(mask, 0, mask.Length);
						//Console.WriteLine("Read flags={0} masked={1} mask={2} len={3}", flags, masked, mask, pllen);
						Byte[] payload = new Byte[pllen]; // + (4 - (pllen % 4))];
						ReadAllBytes(payload, 0, (int)pllen);
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
							String text = Encoding.UTF8.GetString(payload); //, 0, pllen);
							return Convert.FromBase64String(text);
						} else if (opcode == 2) {
							return payload; // ArrayUtil.Slice(payload, 0, pllen);
						} else if (opcode == 8) {
							return null;
						} else if (opcode == 9) {
							//Console.WriteLine("WebSocket PING");
							WriteFrame(10, payload, 0, (int)pllen);
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
						ReadAllBytes(buffer, 0, length);
						if (frameType == 0xff && length == 0) {
							return null;
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
								StreamReader reader = new StreamReader(ms, Encoding.UTF8, false);
								return Convert.FromBase64String(reader.ReadToEnd());
							} else {
								throw new InvalidOperationException();
							}
						}
					}
				} else {
					throw new InvalidOperationException();
				}
			} catch (Exception ex) {
				Console.WriteLine(ex);
				throw;
			}
		}
		private delegate Byte[] ReadPacketDelegate();
		ReadPacketDelegate readPacketDelegate;
		public override IAsyncResult BeginReadPacket(AsyncCallback callback, object state) {
			if (readPacketDelegate == null) readPacketDelegate = ReadPacket;
			return readPacketDelegate.BeginInvoke(callback, state);
		}
		public override byte[] EndReadPacket(IAsyncResult asyncResult) {
			return readPacketDelegate.EndInvoke(asyncResult);
		}
		public override void Write(byte[] buffer, int offset, int count) {
			negotiationEvent.WaitOne();
			if (closed) throw new ObjectDisposedException("WebSocketPacketStream");
			if (!binaryProtocol) {
				String encoded = Convert.ToBase64String(buffer, offset, count, Base64FormattingOptions.None);
				buffer = Encoding.ASCII.GetBytes(encoded);
				offset = 0;
				count = buffer.Length;
			}
			if (wsProtocol == 13) {
				WriteFrame(binaryProtocol ? (Byte)0x2 : (Byte)0x1, buffer, offset, count);
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
		private void WriteFrame(Byte opcode, Byte[] buffer, int offset, int count) {
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
	}
}