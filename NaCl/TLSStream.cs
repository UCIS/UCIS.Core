using System;
using System.IO;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using UCIS.NaCl.v2;
using UCIS.Util;

namespace UCIS.NaCl {
	public enum TLSAlertLevel : byte {
		Warning = 1,
		Fatal = 2
	}
	public enum TLSAlertDescription : byte {
		CloseNotify = 0,
		UnexpectedMessage = 10,
		BadRecordMAC = 20,
		DecryptionFailed = 21,
		RecordOverflow = 22,
		DecompressionFailure = 30,
		HandshakeFailure = 40,
		NoCertificate = 41,
		BadCertificate = 42,
		UnsupportedCertificate = 43,
		CertificateRevoked = 44,
		CertificateExpired = 45,
		CertificateUnknown = 46,
		IllegalParameter = 47,
		UnknownCA = 48,
		AccessDenied = 49,
		DecodeError = 50,
		DecryptError = 51,
		ExportRestriction = 60,
		ProtocolVersion = 70,
		InsufficientSecurity = 71,
		InternalError = 80,
		UserCanceled = 90,
		NoRenegotiation = 100,
		UnsupportedExtension = 110,
	}
	public class TLSException : Exception {
		public TLSAlertLevel Level { get; private set; }
		public TLSAlertDescription Description { get; private set; }
		public TLSException(TLSAlertLevel level, TLSAlertDescription description) : base(level.ToString() + ": " + description.ToString()) { this.Level = level; this.Description = description; }
		public TLSException(TLSAlertLevel level, TLSAlertDescription description, String message) : base(message) { this.Level = level; this.Description = description; }
		public TLSException(TLSAlertLevel level, TLSAlertDescription description, String message, Exception innerException) : base(message, innerException) { this.Level = level; this.Description = description; }
	}
	public class TLSStream : AuthenticatedStream {
		RSACryptoServiceProvider privatekey;
		Stream stream;
		Byte[] currentMACSendKey;
		Byte[] currentMACReceiveKey;
		UInt64 sendSequence = 0;
		UInt64 receiveSequence = 0;
		CipherSuite currentReceiveCipherSuite;
		CipherSuite currentSendCipherSuite;
		SymmetricAlgorithm currentReceiveCipher;
		SymmetricAlgorithm currentSendCipher;
		IHandshakeHasher handshakeHasher = new PreHandshakeHasher();
		UInt16 tlsVersion = 0x0303;
		ConnectionState state = ConnectionState.Unauthenticated;
		ConnectionState handshakeState = ConnectionState.Unauthenticated;
		Object stateLock = new Object();
		Byte[] readdata = null;
		Boolean isServer = false;
		Byte[] handshakeReceiveHeader = null;
		Byte[] handshakeReceiveBuffer = null;
		int readBusy = 0;
		Object writeLock = new Object();

		enum RecordType : byte {
			ChangeCipherSpec = 20,
			Alert = 21,
			Handshake = 22,
			ApplicationData = 23,
		}
		enum HandshakeType : short {
			HelloRequest = 0,
			ClientHello = 1,
			ServerHello = 2,
			Certificate = 11,
			ServerKeyExchange = 12,
			CertificateRequest = 13,
			ServerHelloDone = 14,
			CertificateVerify = 15,
			ClientKeyExchange = 16,
			Finished = 20,
			ChangeCipherSpec = -1, //Not a real handshake message
		}
		enum CipherSuite : ushort {
			TLS_NULL_WITH_NULL_NULL = 0,
			TLS_RSA_WITH_3DES_EDE_CBC_SHA = 0x000A,
			TLS_RSA_WITH_AES_128_CBC_SHA = 0x002F,
			TLS_RSA_WITH_AES_256_CBC_SHA = 0x0035,
			TLS_RSA_WITH_AES_128_CBC_SHA256 = 0x003C,
			TLS_RSA_WITH_AES_256_CBC_SHA256 = 0x003D,
		}
		enum ConnectionState : int {
			Unauthenticated,
			Authenticating,
			HelloReceived,
			ClientKeyReceived,
			ChangeCipherSpecReceived,
			Authenticated,
			Error,
			Closed,
		}

		static Byte[] ComputeMAC(CipherSuite suite, Byte[] key, UInt64 seqno, Byte[] header, Byte[] data, int dataoffset, int datalength) {
			if (suite == CipherSuite.TLS_NULL_WITH_NULL_NULL) {
				return new Byte[0];
			} else if (suite == CipherSuite.TLS_RSA_WITH_3DES_EDE_CBC_SHA || suite == CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA || suite == CipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA) {
				using (HMAC hasher = new HMACSHA1()) {
					hasher.Key = key;
					hasher.TransformBlock(EncodeUInt64(seqno), 0, 8, null, 0);
					hasher.TransformBlock(header, 0, 5, null, 0);
					hasher.TransformFinalBlock(data, dataoffset, datalength);
					return hasher.Hash;
				}
			} else if (suite == CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA256 || suite == CipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA256) {
				using (HMAC hasher = new HMACSHA256()) {
					hasher.Key = key;
					hasher.TransformBlock(EncodeUInt64(seqno), 0, 8, null, 0);
					hasher.TransformBlock(header, 0, 5, null, 0);
					hasher.TransformFinalBlock(data, dataoffset, datalength);
					return hasher.Hash;
				}
			} else {
				throw new ArgumentException("suite");
			}
		}

		void WriteRecord(RecordType type, Byte[] buffer, int offset, int length) {
			lock (writeLock) {
				while (length > 32 * 1024) {
					int part = Math.Min(length, 32 * 1024);
					WriteRecord(type, buffer, offset, part);
					offset += part;
					length -= part;
				}
				int extra;
				if (currentSendCipherSuite == CipherSuite.TLS_NULL_WITH_NULL_NULL) {
					extra = 0;
				} else if (currentSendCipherSuite == CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA || currentSendCipherSuite == CipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA) {
					extra = 20 + 1; //MAC + Padding length
					int rem = (length + extra) % (currentSendCipher.BlockSize / 8);
					extra += (currentSendCipher.BlockSize / 8) - rem;
					extra += 16; //IV
				} else if (currentSendCipherSuite == CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA256 || currentSendCipherSuite == CipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA256) {
					extra = 32 + 1; //MAC + Padding length
					int rem = (length + extra) % (currentSendCipher.BlockSize / 8);
					extra += (currentSendCipher.BlockSize / 8) - rem;
					extra += 16; //IV
				} else if (currentSendCipherSuite == CipherSuite.TLS_RSA_WITH_3DES_EDE_CBC_SHA) {
					extra = 20 + 1; //MAC + padding length
					int rem = (length + extra) % (currentSendCipher.BlockSize / 8);
					extra += (currentSendCipher.BlockSize / 8) - rem;
					if (tlsVersion >= 0x0302) extra += 8; //IV
				} else {
					throw new TLSException(TLSAlertLevel.Fatal, TLSAlertDescription.InternalError, "An invalid cipher suite has been selected");
				}
				Byte[] packet = new Byte[2 + 2 + 1 + length + extra];
				packet[0] = (Byte)type;
				packet[1] = (Byte)(tlsVersion >> 8);
				packet[2] = (Byte)(tlsVersion >> 0);
				packet[3] = (Byte)(length >> 8);
				packet[4] = (Byte)(length >> 0);
				if (currentSendCipherSuite == CipherSuite.TLS_NULL_WITH_NULL_NULL) {
					Buffer.BlockCopy(buffer, offset, packet, 5, length);
				} else if (currentSendCipherSuite == CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA || currentSendCipherSuite == CipherSuite.TLS_RSA_WITH_3DES_EDE_CBC_SHA || currentSendCipherSuite == CipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA || currentSendCipherSuite == CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA256 || currentSendCipherSuite == CipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA256) {
					Byte[] mac = ComputeMAC(currentSendCipherSuite, currentMACSendKey, sendSequence, packet, buffer, offset, length);

					int iv_offset = 0;
					if (tlsVersion >= 0x0302) {
						currentSendCipher.GenerateIV();
						currentSendCipher.IV.CopyTo(packet, 5);
						iv_offset = currentSendCipher.BlockSize / 8;
					}

					Buffer.BlockCopy(buffer, offset, packet, 5 + iv_offset, length);
					mac.CopyTo(packet, 5 + iv_offset + length);

					int padding = packet.Length - (5 + iv_offset + length + mac.Length) - 1;
					for (int i = 5 + iv_offset + length + mac.Length; i < packet.Length; i++) packet[i] = (Byte)padding;

					using (ICryptoTransform transform = currentSendCipher.CreateEncryptor()) {
						transform.TransformFinalBlock(packet, 5 + iv_offset, packet.Length - 5 - iv_offset).CopyTo(packet, 5 + iv_offset);
					}

					if (tlsVersion < 0x0302) currentSendCipher.IV = ArrayUtil.Slice(packet, -(currentSendCipher.BlockSize / 8));

					length += extra;
					packet[3] = (Byte)(length >> 8);
					packet[4] = (Byte)(length >> 0);
				} else {
					throw new TLSException(TLSAlertLevel.Fatal, TLSAlertDescription.InternalError, "An invalid cipher suite has been selected");
				}
				sendSequence++;
				stream.Write(packet, 0, packet.Length);
			}
		}

		class ReadRecordAsyncResult : AsyncResultBase {
			public Byte[] Header { get; set; }
			public Byte[] Payload { get; set; }
			public ReadRecordAsyncResult(AsyncCallback callback, Object state) : base(callback, state) { }
			public new void SetCompleted(Boolean synchronously, Exception error) {
				base.SetCompleted(synchronously, error);
			}
			public new void WaitForCompletion() {
				base.WaitForCompletion();
				ThrowError();
			}
		}
		IAsyncResult BeginReadRecord(AsyncCallback callback, Object state) {
			Byte[] header = new Byte[5];
			ReadRecordAsyncResult ar = new ReadRecordAsyncResult(callback, state) { Header = header };
			StreamUtil.BeginReadAll(stream, header, 0, 5, AsyncReadRecordCallback1, ar);
			return ar;
		}
		void AsyncReadRecordCallback1(IAsyncResult asyncResult) {
			ReadRecordAsyncResult ar = (ReadRecordAsyncResult)asyncResult.AsyncState;
			try {
				int hlen = StreamUtil.EndReadAll(asyncResult);
				Byte[] header = ar.Header;
				if (hlen != header.Length) throw new EndOfStreamException();
				if (header[1] != 0x03) throw new TLSException(TLSAlertLevel.Fatal, TLSAlertDescription.ProtocolVersion, "Invalid protocol version");
				if (header[2] < 0x01) throw new TLSException(TLSAlertLevel.Fatal, TLSAlertDescription.ProtocolVersion, "Invalid protocol version");
				int length = (header[3] << 8) | header[4];
				if (length > 34 * 1024) throw new TLSException(TLSAlertLevel.Fatal, TLSAlertDescription.RecordOverflow, "Packet too large");
				Byte[] buffer = new Byte[length];
				ar.Payload = buffer;
				StreamUtil.BeginReadAll(stream, buffer, 0, length, AsyncReadRecordCallback2, ar);
			} catch (Exception ex) {
				ar.SetCompleted(false, ex);
			}
		}
		void AsyncReadRecordCallback2(IAsyncResult asyncResult) {
			ReadRecordAsyncResult ar = (ReadRecordAsyncResult)asyncResult.AsyncState;
			try {
				int dlen = StreamUtil.EndReadAll(asyncResult);
				if (ar.Payload.Length != dlen) throw new EndOfStreamException();
				ar.SetCompleted(false, null);
			} catch (Exception ex) {
				ar.SetCompleted(false, ex);
			}
		}
		Byte[] EndReadRecord(IAsyncResult asyncResult, out RecordType type) {
			ReadRecordAsyncResult ar = (ReadRecordAsyncResult)asyncResult;
			ar.WaitForCompletion();
			return ReadRecordDecode(ar.Header, ar.Payload, out type);
		}
		Byte[] ReadRecord(out RecordType type) {
			Byte[] header = StreamUtil.ReadAll(stream, 5);
			if (header[1] != 0x03) throw new TLSException(TLSAlertLevel.Fatal, TLSAlertDescription.ProtocolVersion, "Invalid protocol version");
			if (header[2] < 0x01) throw new TLSException(TLSAlertLevel.Fatal, TLSAlertDescription.ProtocolVersion, "Invalid protocol version");
			int length = (header[3] << 8) | header[4];
			if (length > 34 * 1024) throw new TLSException(TLSAlertLevel.Fatal, TLSAlertDescription.RecordOverflow, "Packet too large");
			Byte[] data = StreamUtil.ReadAll(stream, length);
			return ReadRecordDecode(header, data, out type);
		}
		Byte[] ReadRecordDecode(Byte[] header, Byte[] data, out RecordType type) {
			type = (RecordType)header[0];
			if (currentReceiveCipherSuite == CipherSuite.TLS_NULL_WITH_NULL_NULL) {
			} else if (currentReceiveCipherSuite == CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA || currentReceiveCipherSuite == CipherSuite.TLS_RSA_WITH_3DES_EDE_CBC_SHA || currentReceiveCipherSuite == CipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA || currentReceiveCipherSuite == CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA256 || currentReceiveCipherSuite == CipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA256) {
				Byte[] next_iv = null;
				if (tlsVersion >= 0x0302) {
					currentReceiveCipher.IV = ArrayUtil.Slice(data, 0, currentReceiveCipher.BlockSize / 8);
					data = ArrayUtil.Slice(data, currentReceiveCipher.BlockSize / 8);
				} else {
					next_iv = ArrayUtil.Slice(data, -(currentReceiveCipher.BlockSize / 8));
				}
				using (ICryptoTransform transform = currentReceiveCipher.CreateDecryptor()) {
					data = transform.TransformFinalBlock(data, 0, data.Length);
				}
				int mac_length = 0;
				if (currentReceiveCipherSuite == CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA || currentReceiveCipherSuite == CipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA || currentReceiveCipherSuite == CipherSuite.TLS_RSA_WITH_3DES_EDE_CBC_SHA) {
					mac_length = 20;
				} else if (currentReceiveCipherSuite == CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA256 || currentReceiveCipherSuite == CipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA256) {
					mac_length = 32;
				}
				Byte padding_length = data[data.Length - 1];
				Byte[] mac = ArrayUtil.Slice(data, data.Length - 1 - padding_length - mac_length, mac_length);
				for (int i = data.Length - padding_length - 1; i < data.Length; i++) if (data[i] != padding_length) throw new TLSException(TLSAlertLevel.Fatal, TLSAlertDescription.BadRecordMAC, "Invalid padding data");
				data = ArrayUtil.Slice(data, 0, data.Length - 1 - padding_length - mac_length);
				header[3] = (Byte)(data.Length >> 8);
				header[4] = (Byte)(data.Length >> 0);
				Byte[] hash = ComputeMAC(currentReceiveCipherSuite, currentMACReceiveKey, receiveSequence, header, data, 0, data.Length);
				if (!ArrayUtil.Equal(mac, hash)) throw new TLSException(TLSAlertLevel.Fatal, TLSAlertDescription.BadRecordMAC, "Incorrect MAC");
				if (next_iv != null) currentReceiveCipher.IV = next_iv;
			} else {
				throw new TLSException(TLSAlertLevel.Fatal, TLSAlertDescription.InternalError, "Unsupported cipher suite selected");
			}
			receiveSequence++;
			return data;
		}

		Byte[] ReadHandshake(out HandshakeType type) {
			RecordType rtype;
			Byte[] data = ReadRecord(out rtype);
			//Console.WriteLine("record: {0}", BitConverter.ToString(data).Replace('-', ' '));
			if (rtype == RecordType.Alert) {
				throw new TLSException((TLSAlertLevel)data[0], (TLSAlertDescription)data[1]);
			} else if (rtype == RecordType.ChangeCipherSpec) {
				type = HandshakeType.ChangeCipherSpec;
				return data;
			} else if (rtype == RecordType.Handshake) {
				type = (HandshakeType)data[0];
			} else {
				throw new TLSException(TLSAlertLevel.Fatal, TLSAlertDescription.UnexpectedMessage, "Unexpected record type");
			}
			if (type != HandshakeType.HelloRequest) handshakeHasher.Process(data, 0, data.Length);
			int length = (data[1] << 16) | (data[2] << 8) | data[3];
			data = ArrayUtil.Slice(data, 4);
			if (data.Length < length) {
				if (length > 0x100000) throw new TLSException(TLSAlertLevel.Fatal, TLSAlertDescription.RecordOverflow, "Handshake message is too long"); //1MB should be enough...
				int offset = data.Length;
				Array.Resize(ref data, length);
				while (offset < length) {
					Byte[] next = ReadRecord(out rtype);
					if (rtype != RecordType.Handshake) throw new TLSException(TLSAlertLevel.Fatal, TLSAlertDescription.UnexpectedMessage, "Unexpected record type");
					next.CopyTo(data, offset);
					if (type != HandshakeType.HelloRequest) handshakeHasher.Process(next, 0, next.Length);
					offset += next.Length;
				}
			}
			return data;
		}
		IAsyncResult BeginReadHandshake(AsyncCallback callback, Object state) {
			ReadRecordAsyncResult ar = new ReadRecordAsyncResult(callback, state) { Header = null, Payload = null };
			BeginReadRecord(AsyncReadHandshakeCallback, ar);
			return ar;
		}
		void AsyncReadHandshakeCallback(IAsyncResult asyncResult) {
			ReadRecordAsyncResult ar = (ReadRecordAsyncResult)asyncResult.AsyncState;
			try {
				RecordType rtype;
				Byte[] data = EndReadRecord(asyncResult, out rtype);
				if (ar.Header == null) {
					if (rtype == RecordType.Alert) {
						throw new TLSException((TLSAlertLevel)data[0], (TLSAlertDescription)data[1]);
					} else if (rtype == RecordType.ChangeCipherSpec) {
						ar.Header = new Byte[] { (Byte)rtype, 0 };
						ar.Payload = data;
						ar.SetCompleted(false, null);
						return;
					} else if (rtype == RecordType.Handshake) {
						ar.Header = new Byte[] { (Byte)rtype, data[0], data[1], data[2], data[3] };
						ar.Payload = ArrayUtil.Slice(data, 4);
						if (data[0] != (Byte)HandshakeType.HelloRequest) handshakeHasher.Process(data, 0, data.Length);
					} else {
						throw new TLSException(TLSAlertLevel.Fatal, TLSAlertDescription.UnexpectedMessage, "Unexpected record type");
					}
				} else {
					if (rtype != RecordType.Handshake) throw new TLSException(TLSAlertLevel.Fatal, TLSAlertDescription.UnexpectedMessage, "Unexpected record type");
					if (ar.Header[1] != (Byte)HandshakeType.HelloRequest) handshakeHasher.Process(data, 0, data.Length);
					ar.Payload = ArrayUtil.Merge(ar.Payload, data);
				}
				int length = (ar.Header[2] << 16) | (ar.Header[3] << 8) | ar.Header[4];
				if (length == ar.Payload.Length) {
					ar.SetCompleted(false, null);
				} else if (length > 0x100000) {
					throw new TLSException(TLSAlertLevel.Fatal, TLSAlertDescription.RecordOverflow, "Handshake message is too long"); //1MB should be enough...
				} else {
					BeginReadRecord(AsyncReadHandshakeCallback, ar);
				}
			} catch (Exception ex) {
				ar.SetCompleted(false, ex);
			}
		}
		Byte[] EndReadHandshake(IAsyncResult asyncResult, out HandshakeType type) {
			ReadRecordAsyncResult ar = (ReadRecordAsyncResult)asyncResult;
			ar.WaitForCompletion();
			if (ar.Header[0] == (Byte)RecordType.ChangeCipherSpec) {
				type = HandshakeType.ChangeCipherSpec;
				return ar.Payload;
			} else if (ar.Header[0] == (Byte)RecordType.Handshake) {
				type = (HandshakeType)ar.Header[1];
				return ar.Payload;
			} else {
				throw new TLSException(TLSAlertLevel.Fatal, TLSAlertDescription.UnexpectedMessage, "Unexpected record type");
			}
		}

		void WriteHandshake(HandshakeType type, params Byte[][] args) {
			Byte[] arg = ArrayUtil.Merge(args);
			Byte[] record = new Byte[4 + arg.Length];
			record[0] = (Byte)type;
			record[1] = (Byte)(arg.Length >> 16);
			record[2] = (Byte)(arg.Length >> 8);
			record[3] = (Byte)(arg.Length >> 0);
			arg.CopyTo(record, 4);
			if (type != HandshakeType.HelloRequest) handshakeHasher.Process(record, 0, record.Length);
			WriteRecord(RecordType.Handshake, record, 0, record.Length);
		}

		public TLSStream(Stream stream) : base(stream, false) {
			this.stream = stream;
		}

		static UInt16 DecodeUInt16BE(Byte[] buffer, int offset) {
			return (UInt16)((buffer[offset + 0] << 8) | buffer[offset + 1]);
		}
		static UInt32 DecodeUInt32BE(Byte[] buffer, int offset) {
			return (UInt32)((buffer[offset + 0] << 24) | (buffer[offset + 1] << 16) | (buffer[offset + 2] << 8) | buffer[offset + 3]);
		}
		static UInt32 DecodeUInt32LE(Byte[] buffer, int offset) {
			return (UInt32)((buffer[offset + 3] << 24) | (buffer[offset + 2] << 16) | (buffer[offset + 1] << 8) | buffer[offset + 0]);
		}

		static Byte[] EncodeUInt64(UInt64 value) {
			return new Byte[8] { (Byte)(value >> 56), (Byte)(value >> 48), (Byte)(value >> 40), (Byte)(value >> 32), (Byte)(value >> 24), (Byte)(value >> 16), (Byte)(value >> 8), (Byte)value };
		}
		static Byte[] EncodeUInt32(UInt32 value) {
			return new Byte[4] { (Byte)(value >> 24), (Byte)(value >> 16), (Byte)(value >> 8), (Byte)value };
		}
		static Byte[] EncodeUInt24(UInt32 value) {
			if (value > 0xFFFFFF) throw new OverflowException();
			return new Byte[3] { (Byte)(value >> 16), (Byte)(value >> 8), (Byte)value };
		}
		static Byte[] EncodeUInt16(UInt16 value) {
			return new Byte[2] { (Byte)(value >> 8), (Byte)value };
		}
		static Byte[] EncodeByte(Byte value) {
			return new Byte[1] { value };
		}

		/*static String ToHexString(Byte[] bytes) {
			return BitConverter.ToString(bytes).Replace('-', ' ');
		}
		static String ToHexString(UInt16[] values) {
			return String.Join(" ", Array.ConvertAll(values, v => v.ToString("X4")));
		}
		static String ImplodeString<T>(T[] values) {
			return String.Join(" ", Array.ConvertAll(values, v => v.ToString()));
		}*/

		X509Certificate2Collection handshake_server_certificates;
		Byte[] handshake_client_random, handshake_server_random;
		CipherSuite handshake_cipher_suite;
		Byte[] handshake_master_secret, handshake_key_block;
		Byte[] handshake_hash_state;
		void SetupCipherSuite(CipherSuite suite, Boolean sending, out Byte[] macKey, out SymmetricAlgorithm cipher) {
			//key_block: client_write_MAC_key, server_write_MAC_key, client_write_key, server_write_key, client_write_IV, server_write_IV
			int offset = 0;
			int mac_key_length = 0;
			if (suite == CipherSuite.TLS_RSA_WITH_3DES_EDE_CBC_SHA || suite == CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA || suite == CipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA) {
				mac_key_length = 20;
			} else if (handshake_cipher_suite == CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA256 || handshake_cipher_suite == CipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA256) {
				mac_key_length = 32;
			}
			if (sending) offset += mac_key_length;
			macKey = ArrayUtil.Slice(handshake_key_block, offset, mac_key_length);
			offset += mac_key_length;
			if (!sending) offset += mac_key_length;
			if (handshake_cipher_suite == CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA || handshake_cipher_suite == CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA256) {
				cipher = new RijndaelManaged();
				cipher.BlockSize = 128;
			} else if (handshake_cipher_suite == CipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA || handshake_cipher_suite == CipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA256) {
				cipher = new RijndaelManaged();
				cipher.BlockSize = 128;
			} else if (handshake_cipher_suite == CipherSuite.TLS_RSA_WITH_3DES_EDE_CBC_SHA) {
				cipher = new TripleDESCryptoServiceProvider();
			} else {
				cipher = null;
			}
			if (cipher != null) {
				cipher.Mode = CipherMode.CBC;
				cipher.Padding = PaddingMode.None;
				if (sending) offset += cipher.KeySize / 8;
				cipher.Key = ArrayUtil.Slice(handshake_key_block, offset, cipher.KeySize / 8);
				offset += cipher.KeySize / 8;
				if (!sending) offset += cipher.KeySize / 8;
				if (sending) offset += cipher.BlockSize / 8;
				cipher.IV = ArrayUtil.Slice(handshake_key_block, offset, cipher.BlockSize / 8);
				offset += cipher.BlockSize / 8;
				if (!sending) offset += cipher.BlockSize / 8;
			}
		}
		void ProcessHandshake(HandshakeType htype, Byte[] record) {
			if (htype == HandshakeType.ClientHello) {
				if (handshakeState != ConnectionState.Unauthenticated && handshakeState != ConnectionState.Authenticated) throw new TLSException(TLSAlertLevel.Fatal, TLSAlertDescription.UnexpectedMessage, "Unexpected record received");
				UInt16 client_version = DecodeUInt16BE(record, 0);
				if (client_version < 0x0301) throw new TLSException(TLSAlertLevel.Fatal, TLSAlertDescription.ProtocolVersion, "Unsupported TLS version");
				//Console.WriteLine("client_version: {0:X4}", client_version);
				UInt32 gmt_unix_time = DecodeUInt32BE(record, 2);
				//Console.WriteLine("gmt_unix_time: {0} ({1})", gmt_unix_time, (new DateTime(1970, 01, 01, 00, 00, 00, DateTimeKind.Utc)).AddSeconds(gmt_unix_time).ToString());
				handshake_client_random = ArrayUtil.Slice(record, 2, 32);
				//Console.WriteLine("random_bytes: {0}", ToHexString(handshake_client_random));
				int session_id_length = record[34];
				Byte[] session_id = ArrayUtil.Slice(record, 35, session_id_length);
				//Console.WriteLine("session_id: {0}", ToHexString(session_id));
				int cipher_suites_length = DecodeUInt16BE(record, 35 + session_id_length);
				CipherSuite[] cipher_suites = new CipherSuite[cipher_suites_length / 2];
				for (int i = 0; i < cipher_suites.Length; i++) cipher_suites[i] = (CipherSuite)DecodeUInt16BE(record, 35 + session_id_length + 2 + i * 2);
				//Console.WriteLine("cipher_suites: {0}", ImplodeString(cipher_suites));
				int compression_methods_length = record[35 + session_id_length + 2 + cipher_suites_length];
				Byte[] compression_methods = ArrayUtil.Slice(record, 35 + session_id_length + 2 + cipher_suites_length + 1, compression_methods_length);
				//Console.WriteLine("compression_methods: {0}", ToHexString(compression_methods));
				Byte[] extensions = ArrayUtil.Slice(record, 35 + session_id_length + 2 + cipher_suites_length + 1 + compression_methods_length);
				//Console.WriteLine("extensions: {0}", ToHexString(extensions));

				if (tlsVersion > client_version) tlsVersion = client_version;
				if (tlsVersion > 0x0303) tlsVersion = 0x0303;
				if (tlsVersion < 0x0301) throw new TLSException(TLSAlertLevel.Fatal, TLSAlertDescription.ProtocolVersion, "The requested TLS version is not supported");

				if (Array.IndexOf(cipher_suites, CipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA256) != -1) handshake_cipher_suite = CipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA256;
				else if (Array.IndexOf(cipher_suites, CipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA) != -1) handshake_cipher_suite = CipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA;
				else if (Array.IndexOf(cipher_suites, CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA256) != -1) handshake_cipher_suite = CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA256;
				else if (Array.IndexOf(cipher_suites, CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA) != -1) handshake_cipher_suite = CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA;
				else if (Array.IndexOf(cipher_suites, CipherSuite.TLS_RSA_WITH_3DES_EDE_CBC_SHA) != -1) handshake_cipher_suite = CipherSuite.TLS_RSA_WITH_3DES_EDE_CBC_SHA;
				//else if (Array.IndexOf(cipher_suites, CipherSuite.TLS_NULL_WITH_NULL_NULL) != -1) handshake_cipher_suite = CipherSuite.TLS_NULL_WITH_NULL_NULL;
				else throw new TLSException(TLSAlertLevel.Fatal, TLSAlertDescription.HandshakeFailure, "No common cipher suite found");

				//Console.WriteLine("Selected cipher suite: {0}", handshake_cipher_suite);

				if (handshakeHasher is PreHandshakeHasher) {
					if (tlsVersion >= 0x0303) handshakeHasher = ((PreHandshakeHasher)handshakeHasher).Replace(new SHA256Hasher());
					else handshakeHasher = ((PreHandshakeHasher)handshakeHasher).Replace(new MD5AndSHA1Hasher());
				}

				//send ServerHello
				handshake_server_random = UCIS.NaCl.randombytes.generate(32);
				WriteHandshake(HandshakeType.ServerHello,
					EncodeUInt16(tlsVersion),
					handshake_server_random,
					EncodeByte(0),
					EncodeUInt16((UInt16)handshake_cipher_suite),
					EncodeByte(0)
				);

				if (handshake_server_certificates != null && state != ConnectionState.Authenticated) {
					//optionally send ServerCertificate
					Byte[] certsbytes = new Byte[0];
					foreach (X509Certificate cert in handshake_server_certificates) {
						Byte[] certbytes = cert.Export(X509ContentType.Cert, String.Empty);
						certsbytes = ArrayUtil.Merge(certsbytes, EncodeUInt24((UInt32)certbytes.Length), certbytes);
					}
					WriteHandshake(HandshakeType.Certificate, EncodeUInt24((UInt32)certsbytes.Length), certsbytes);
					handshake_server_certificates = null;
				}

				//optionally send ServerKeyExchange
				//optionally send CertificateRequest

				//send ServerHelloDone
				WriteHandshake(HandshakeType.ServerHelloDone);
				handshakeState = ConnectionState.HelloReceived;
			} else if (htype == HandshakeType.ClientKeyExchange) {
				if (handshakeState != ConnectionState.HelloReceived) throw new TLSException(TLSAlertLevel.Fatal, TLSAlertDescription.UnexpectedMessage, "Unexpected record received");
				UInt16 premastersecret_length = DecodeUInt16BE(record, 0);
				record = ArrayUtil.Slice(record, 2, premastersecret_length);
				record = privatekey.Decrypt(record, false);

				UInt16 premastersecret_version = DecodeUInt16BE(record, 0);
				//Console.WriteLine("premastersecret: {0:X4}", premastersecret_version);

				//Console.WriteLine("premastersecret: {0}", ToHexString(record));
				handshake_master_secret = tls_prf(record, "master secret", 48, handshake_client_random, handshake_server_random);
				//Console.WriteLine("master_secret: {0}", ToHexString(handshake_master_secret));
				handshake_key_block = tls_prf(handshake_master_secret, "key expansion", 256, handshake_server_random, handshake_client_random);

				handshakeState = ConnectionState.ClientKeyReceived;
			} else if (htype == HandshakeType.ChangeCipherSpec) { //It's not really a handshake message, but for convenience we pretend it's one.
				if (handshakeState != ConnectionState.ClientKeyReceived) throw new TLSException(TLSAlertLevel.Fatal, TLSAlertDescription.UnexpectedMessage, "Unexpected handshake message");
				if (record.Length < 1 || record[0] != 1) throw new TLSException(TLSAlertLevel.Fatal, TLSAlertDescription.UnexpectedMessage, "Unexpected record received");
				currentReceiveCipherSuite = handshake_cipher_suite;
				SetupCipherSuite(handshake_cipher_suite, false, out currentMACReceiveKey, out currentReceiveCipher);
				receiveSequence = 0;
				handshake_hash_state = handshakeHasher.GetHash();
				handshakeState = ConnectionState.ChangeCipherSpecReceived;
			} else if (htype == HandshakeType.Finished) {
				if (handshakeState != ConnectionState.ChangeCipherSpecReceived) throw new TLSException(TLSAlertLevel.Fatal, TLSAlertDescription.UnexpectedMessage, "Unexpected handshake message");
				Byte[] verify = tls_prf(handshake_master_secret, "client finished", record.Length, handshake_hash_state);
				//Console.WriteLine("verify {0}", ToHexString(verify));
				if (!ArrayUtil.Equal(verify, record)) throw new TLSException(TLSAlertLevel.Fatal, TLSAlertDescription.DecryptError, "Handshake verification failed");

				//send ChangeCipherSpec
				lock (writeLock) {
					WriteRecord(RecordType.ChangeCipherSpec, new Byte[1] { 1 }, 0, 1);
					currentSendCipherSuite = handshake_cipher_suite;
					SetupCipherSuite(handshake_cipher_suite, true, out currentMACSendKey, out currentSendCipher);
					sendSequence = 0;

					//send Finished
					Byte[] handshakeHash = handshakeHasher.GetHash();
					verify = tls_prf(handshake_master_secret, "server finished", 12, handshakeHash);
					WriteHandshake(HandshakeType.Finished, verify);

					handshakeState = ConnectionState.Authenticated;
				}

				lock (stateLock) {
					state = ConnectionState.Authenticated;
					Monitor.PulseAll(stateLock);
				}
			} else if (htype == HandshakeType.Certificate || htype == HandshakeType.CertificateVerify) {
				//Ignore
			} else {
				throw new TLSException(TLSAlertLevel.Fatal, TLSAlertDescription.UnexpectedMessage, "Unexpected handshake message");
			}
		}

		public void AuthenticateAsServer(X509Certificate2Collection chain) {
			lock (stateLock) {
				if (state != ConnectionState.Unauthenticated) throw new InvalidOperationException("The session is not in the Unauthenticated state");
				state = ConnectionState.Authenticating;
				isServer = true;
				Monitor.PulseAll(stateLock);
			}
			try {
				handshake_server_certificates = new X509Certificate2Collection(chain);
				privatekey = (RSACryptoServiceProvider)chain[0].PrivateKey;
				while (state != ConnectionState.Authenticated) {
					if (state == ConnectionState.Closed) throw new ObjectDisposedException("TLSStream");
					if (state == ConnectionState.Error) throw new TLSException(TLSAlertLevel.Fatal, TLSAlertDescription.InternalError);
					HandshakeType htype;
					Byte[] record = ReadHandshake(out htype);
					ProcessHandshake(htype, record);
				}
			} catch {
				lock (stateLock) {
					state = ConnectionState.Error;
					Monitor.PulseAll(stateLock);
				}
				throw;
			}
		}
		public IAsyncResult BeginAuthenticateAsServer(X509Certificate2Collection chain, AsyncCallback callback, Object callbackState) {
			lock (stateLock) {
				if (state != ConnectionState.Unauthenticated) throw new InvalidOperationException("The session is not in the Unauthenticated state");
				state = ConnectionState.Authenticating;
				isServer = true;
				Monitor.PulseAll(stateLock);
			}
			ReadRecordAsyncResult ar = new ReadRecordAsyncResult(callback, callbackState);
			try {
				handshake_server_certificates = new X509Certificate2Collection(chain);
				privatekey = (RSACryptoServiceProvider)chain[0].PrivateKey;
				BeginReadHandshake(AsyncAuthenticateAsServerCallback, ar);
			} catch {
				lock (stateLock) {
					state = ConnectionState.Error;
					Monitor.PulseAll(stateLock);
				}
				throw;
			}
			return ar;
		}
		void AsyncAuthenticateAsServerCallback(IAsyncResult asyncResult) {
			ReadRecordAsyncResult ar = (ReadRecordAsyncResult)asyncResult.AsyncState;
			try {
				HandshakeType htype;
				Byte[] record = EndReadHandshake(asyncResult, out htype);
				ProcessHandshake(htype, record);
				if (state == ConnectionState.Closed) {
					ar.SetCompleted(false, new ObjectDisposedException("TLSStream"));
				} else if (state == ConnectionState.Error) {
					ar.SetCompleted(false, new TLSException(TLSAlertLevel.Fatal, TLSAlertDescription.InternalError));
				} else if (state == ConnectionState.Authenticated) {
					ar.SetCompleted(false, null);
				} else {
					BeginReadHandshake(AsyncAuthenticateAsServerCallback, ar);
				}
			} catch (Exception ex) {
				lock (stateLock) {
					state = ConnectionState.Error;
					Monitor.PulseAll(stateLock);
				}
				ar.SetCompleted(false, ex);
			}
		}
		public void EndAuthenticateAsServer(IAsyncResult asyncResult) {
			ReadRecordAsyncResult ar = (ReadRecordAsyncResult)asyncResult;
			ar.WaitForCompletion();
		}

		void ProcessControlRecord(RecordType rtype, Byte[] record) {
			if (rtype == RecordType.ChangeCipherSpec) {
				ProcessHandshake(HandshakeType.ChangeCipherSpec, record);
			} else if (rtype == RecordType.Handshake) {
				if (handshakeReceiveHeader == null) {
					handshakeReceiveHeader = ArrayUtil.Slice(record, 0, 4);
					handshakeReceiveBuffer = ArrayUtil.Slice(record, 4);
				} else {
					handshakeReceiveBuffer = ArrayUtil.Merge(handshakeReceiveBuffer, record);
				}
				if (handshakeReceiveHeader[0] != (Byte)HandshakeType.HelloRequest) handshakeHasher.Process(record, 0, record.Length);
				int length = (handshakeReceiveHeader[1] << 16) | (handshakeReceiveHeader[2] << 8) | handshakeReceiveHeader[3];
				if (length == handshakeReceiveBuffer.Length) {
					ProcessHandshake((HandshakeType)handshakeReceiveHeader[0], handshakeReceiveBuffer);
					handshakeReceiveBuffer = handshakeReceiveHeader = null;
				} else if (length > 0x100000) {
					throw new TLSException(TLSAlertLevel.Fatal, TLSAlertDescription.RecordOverflow, "Handshake message is too long"); //1MB should be enough...
				}
			} else if (rtype == RecordType.Alert) {
				TLSAlertLevel level = (TLSAlertLevel)record[0];
				TLSAlertDescription description = (TLSAlertDescription)record[1];
				if (level == TLSAlertLevel.Fatal) {
					state = ConnectionState.Error;
					throw new TLSException(level, description);
				} else if (level == TLSAlertLevel.Warning && description == TLSAlertDescription.CloseNotify) {
					state = ConnectionState.Closed;
				}
			} else {
				throw new TLSException(TLSAlertLevel.Fatal, TLSAlertDescription.UnexpectedMessage);
			}
		}

		interface IHandshakeHasher {
			void Process(Byte[] buffer, int offset, int length);
			Byte[] GetHash();
		}
		class MD5AndSHA1Hasher : IHandshakeHasher {
			md5 md5 = new md5();
			sha1 sha1 = new sha1();
			public void Process(Byte[] buffer, int offset, int length) {
				md5.Process(buffer, offset, length);
				sha1.Process(buffer, offset, length);
			}
			public Byte[] GetHash() {
				return ArrayUtil.Merge(md5.Clone().GetHash(), sha1.Clone().GetHash());
			}
		}
		class SHA256Hasher : IHandshakeHasher {
			sha256 sha256 = new sha256();
			public void Process(Byte[] buffer, int offset, int length) {
				sha256.Process(buffer, offset, length);
			}
			public Byte[] GetHash() {
				return sha256.Clone().GetHash();
			}
		}
		class PreHandshakeHasher : IHandshakeHasher {
			Byte[] buffer = null;
			public void Process(Byte[] buffer, int offset, int length) {
				buffer = ArrayUtil.Slice(buffer, offset, length);
				this.buffer = (this.buffer == null) ? buffer : ArrayUtil.Merge(this.buffer, buffer);
			}
			public Byte[] GetHash() {
				throw new NotSupportedException();
			}
			public IHandshakeHasher Replace(IHandshakeHasher replacement) {
				if (buffer != null) replacement.Process(buffer, 0, buffer.Length);
				return replacement;
			}
		}

		static Byte[] md5(Byte[] d1, Byte[] d2) {
			md5 hash = new md5();
			hash.Process(d1);
			if (d2 != null) hash.Process(d2);
			return hash.GetHash();
		}
		static Byte[] sha1(Byte[] d1, Byte[] d2) {
			sha1 hash = new sha1();
			hash.Process(d1);
			if (d2 != null) hash.Process(d2);
			return hash.GetHash();
		}
		static Byte[] sha256(Byte[] d1, Byte[] d2) {
			sha256 hash = new sha256();
			hash.Process(d1);
			if (d2 != null) hash.Process(d2);
			return hash.GetHash();
		}

		static Byte[] hmac_md5(Byte[] secret, Byte[] data) {
			Byte[] k_pad = new Byte[64];

			if (secret.Length > 64) secret = md5(secret, null);
			secret.CopyTo(k_pad, 0);
			for (int i = 0; i < 64; i++) k_pad[i] ^= 0x36;

			Byte[] mac = md5(k_pad, data);

			k_pad = new Byte[64];
			secret.CopyTo(k_pad, 0);
			for (int i = 0; i < 64; i++) k_pad[i] ^= 0x5C;

			return md5(k_pad, mac);
		}
		static Byte[] hmac_sha1(Byte[] secret, Byte[] data) {
			Byte[] k_pad = new Byte[64];

			if (secret.Length > 64) secret = sha1(secret, null);
			secret.CopyTo(k_pad, 0);
			for (int i = 0; i < 64; i++) k_pad[i] ^= 0x36;

			Byte[] mac = sha1(k_pad, data);

			k_pad = new Byte[64];
			secret.CopyTo(k_pad, 0);
			for (int i = 0; i < 64; i++) k_pad[i] ^= 0x5C;

			return sha1(k_pad, mac);
		}
		static Byte[] hmac_sha256(Byte[] secret, Byte[] data) {
			Byte[] k_pad = new Byte[64];

			if (secret.Length > 64) secret = sha256(secret, null);
			secret.CopyTo(k_pad, 0);
			for (int i = 0; i < 64; i++) k_pad[i] ^= 0x36;

			Byte[] mac = sha256(k_pad, data);

			k_pad = new Byte[64];
			secret.CopyTo(k_pad, 0);
			for (int i = 0; i < 64; i++) k_pad[i] ^= 0x5C;

			return sha256(k_pad, mac);
		}

		Byte[] tls_prf(Byte[] secret, String label, int out_length, params Byte[][] input) {
			if (tlsVersion >= 0x0303) {
				return tls_prf_sha256(secret, label, ArrayUtil.Merge(input), out_length);
			} else {
				return tls_prf_md5sha1(secret, label, ArrayUtil.Merge(input), out_length);
			}
		}
		static Byte[] tls_prf_sha256(Byte[] secret, String label, Byte[] seed, int outlen) {
			seed = ArrayUtil.Merge(Encoding.ASCII.GetBytes(label), seed);
			Byte[] a = seed;
			int pos = 0;
			Byte[] ret = new Byte[outlen];
			while (pos < outlen) {
				a = hmac_sha256(secret, a);
				Byte[] b = hmac_sha256(secret, ArrayUtil.Merge(a, seed));
				int clen = Math.Min(outlen - pos, b.Length);
				Buffer.BlockCopy(b, 0, ret, pos, clen);
				pos += clen;
			}
			return ret;
		}
		static Byte[] tls_prf_md5sha1(Byte[] secret, String label, Byte[] seed, int outlen) {
			seed = ArrayUtil.Merge(Encoding.ASCII.GetBytes(label), seed);
			Byte[] S1 = ArrayUtil.Slice(secret, 0, (secret.Length + 1) / 2);
			Byte[] S2 = ArrayUtil.Slice(secret, secret.Length - (secret.Length + 1) / 2, (secret.Length + 1) / 2);

			Byte[] ret = new Byte[outlen];
			int pos = 0;
			Byte[] a = seed;
			while (pos < outlen) {
				a = hmac_md5(S1, a);
				Byte[] b = hmac_md5(S1, ArrayUtil.Merge(a, seed));
				int clen = Math.Min(outlen - pos, b.Length);
				Buffer.BlockCopy(b, 0, ret, pos, clen);
				pos += clen;
			}
			pos = 0;
			a = seed;
			while (pos < outlen) {
				a = hmac_sha1(S2, a);
				Byte[] b = hmac_sha1(S2, ArrayUtil.Merge(a, seed));
				int clen = Math.Min(outlen - pos, b.Length);
				for (int i = 0; i < clen; i++) ret[pos + i] ^= b[i];
				pos += clen;
			}
			return ret;
		}

		public override bool IsAuthenticated { get { return state == ConnectionState.Authenticated; } }
		public override bool IsEncrypted { get { return currentSendCipherSuite != CipherSuite.TLS_NULL_WITH_NULL_NULL; } }
		public override bool IsMutuallyAuthenticated { get { return false; } }
		public override bool IsServer { get { return isServer; } }
		public override bool IsSigned { get { return false; } }
		public override bool CanRead { get { return stream.CanRead; } }
		public override bool CanSeek { get { return false; } }
		public override bool CanWrite { get { return stream.CanWrite; } }
		public override bool CanTimeout { get { return base.CanTimeout; } }
		public override int ReadTimeout {
			get { return base.ReadTimeout; }
			set { base.ReadTimeout = value; }
		}
		public override int WriteTimeout {
			get { return base.WriteTimeout; }
			set { base.WriteTimeout = value; }
		}

		public override void Flush() {
			stream.Flush();
		}

		public override long Length { get { throw new NotSupportedException(); } }
		public override long Position {
			get { throw new NotSupportedException(); }
			set { throw new NotSupportedException(); }
		}
		public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
		public override void SetLength(long value) { throw new NotSupportedException(); }

		int ReadDataFromBuffer(byte[] buffer, int offset, int count) {
			count = Math.Min(readdata.Length, count);
			Buffer.BlockCopy(readdata, 0, buffer, offset, readdata.Length);
			if (readdata.Length <= count) {
				readdata = null;
			} else {
				readdata = ArrayUtil.Slice(readdata, count);
			}
			return count;
		}
		public override int Read(byte[] buffer, int offset, int count) {
			if (Interlocked.CompareExchange(ref readBusy, 1, 0) != 0) throw new InvalidOperationException("Another read operation is in progress");
			try {
				if (readdata == null) {
					lock (stateLock) while (state != ConnectionState.Authenticated && state != ConnectionState.Error && state != ConnectionState.Closed) Monitor.Wait(stateLock);
					while (readdata == null) {
						if (state == ConnectionState.Closed) throw new ObjectDisposedException("TLSStream");
						if (state == ConnectionState.Error) throw new InvalidOperationException("The session encountered an error and can no longer be used");
						RecordType rtype;
						Byte[] record = ReadRecord(out rtype);
						if (rtype == RecordType.ApplicationData) {
							readdata = record;
							break;
						}
						ProcessControlRecord(rtype, record);
					}
				}
				return ReadDataFromBuffer(buffer, offset, count);
			} finally {
				readBusy = 0;
			}
		}
		class IOAsyncResult : AsyncResultBase {
			public Byte[] Buffer { get; set; }
			public int Offset { get; set; }
			public int Count { get; set; }
			public IOAsyncResult(AsyncCallback callback, Object state) : base(callback, state) { }
			public new void SetCompleted(Boolean synchronously, Exception error) {
				base.SetCompleted(synchronously, error);
			}
			public new void WaitForCompletion() {
				base.WaitForCompletion();
				ThrowError();
			}
		}
		public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object callbackState) {
			lock (stateLock) while (state != ConnectionState.Authenticated && state != ConnectionState.Error && state != ConnectionState.Closed) Monitor.Wait(stateLock);
			if (state == ConnectionState.Closed) throw new ObjectDisposedException("TLSStream");
			if (state == ConnectionState.Error) throw new InvalidOperationException("The session encountered an error and can no longer be used");
			IOAsyncResult ar = new IOAsyncResult(callback, callbackState) { Buffer = buffer, Offset = offset, Count = count };
			if (readdata != null) {
				ar.SetCompleted(true, null);
			} else {
				if (Interlocked.CompareExchange(ref readBusy, 1, 0) != 0) throw new InvalidOperationException("Another read operation is in progress");
				try {
					BeginReadRecord(AsyncReadCallback, ar);
				} catch {
					readBusy = 0;
					throw;
				}
			}
			return ar;
		}
		void AsyncReadCallback(IAsyncResult asyncResult) {
			IOAsyncResult ar = (IOAsyncResult)asyncResult.AsyncState;
			try {
				RecordType rtype;
				Byte[] record = EndReadRecord(asyncResult, out rtype);
				if (rtype == RecordType.ApplicationData) {
					readdata = record;
					readBusy = 0;
					ar.SetCompleted(false, null);
				} else {
					ProcessControlRecord(rtype, record);
					BeginReadRecord(AsyncReadCallback, ar);
				}
			} catch (Exception ex) {
				readBusy = 0;
				ar.SetCompleted(false, ex);
			}
		}
		public override int EndRead(IAsyncResult asyncResult) {
			IOAsyncResult ar = (IOAsyncResult)asyncResult;
			ar.WaitForCompletion();
			if (Interlocked.CompareExchange(ref readBusy, 1, 0) != 0) throw new InvalidOperationException("Another read operation is in progress");
			try {
				return ReadDataFromBuffer(ar.Buffer, ar.Offset, ar.Count);
			} finally {
				readBusy = 0;
			}
		}

		public override void Write(byte[] buffer, int offset, int count) {
			lock (stateLock) {
				while (state != ConnectionState.Authenticated && state != ConnectionState.Error && state != ConnectionState.Closed) Monitor.Wait(stateLock);
				if (state == ConnectionState.Closed) throw new ObjectDisposedException("TLSStream");
				if (state == ConnectionState.Error) throw new InvalidOperationException("The session encountered an error and can no longer be used");
			}
			WriteRecord(RecordType.ApplicationData, buffer, offset, count);
		}

		public override void Close() {
			if (state == ConnectionState.Authenticated) {
				try { WriteRecord(RecordType.Alert, new Byte[] { (Byte)TLSAlertLevel.Warning, (Byte)TLSAlertDescription.CloseNotify }, 0, 2); } catch { }
				state = ConnectionState.Closed;
			}
			base.Close();
			stream.Dispose();
		}

		public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state) {
			return base.BeginWrite(buffer, offset, count, callback, state);
		}
		public override void EndWrite(IAsyncResult asyncResult) {
			base.EndWrite(asyncResult);
		}
	}
}
