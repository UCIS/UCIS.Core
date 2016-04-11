using System;
using System.Globalization;
using System.IO;
using UCIS.Util;
using curve25519xsalsa20poly1305impl = UCIS.NaCl.crypto_box.curve25519xsalsa20poly1305;
using ed25519impl = UCIS.NaCl.crypto_sign.ed25519;
using edwards25519sha512batchimpl = UCIS.NaCl.crypto_sign.edwards25519sha512batch;
using md5impl = UCIS.NaCl.crypto_hash.md5;
using sha1impl = UCIS.NaCl.crypto_hash.sha1;
using sha256impl = UCIS.NaCl.crypto_hash.sha256;
using sha512impl = UCIS.NaCl.crypto_hash.sha512;
using xsalsa20poly1305impl = UCIS.NaCl.crypto_secretbox.xsalsa20poly1305;

namespace UCIS.NaCl.v2 {
	public class curve25519keypair {
		private Byte[] secretkey;
		private Byte[] publickey = null;

		public curve25519keypair() {
			curve25519xsalsa20poly1305impl.crypto_box_keypair(out publickey, out secretkey);
		}
		public curve25519keypair(Byte[] secretkey) {
			this.secretkey = secretkey;
		}
		public curve25519keypair(String secretkey) {
			this.secretkey = DecodeHexString(secretkey, curve25519xsalsa20poly1305impl.SECRETKEYBYTES);
		}
		public curve25519keypair(Byte[] secretkey, Byte[] publickey) {
			if (publickey.Length != curve25519xsalsa20poly1305impl.PUBLICKEYBYTES) throw new ArgumentOutOfRangeException("publickey");
			if (secretkey.Length != curve25519xsalsa20poly1305impl.SECRETKEYBYTES) throw new ArgumentOutOfRangeException("secretkey");
			this.secretkey = secretkey;
			this.publickey = publickey;
		}
		public Byte[] PublicKey {
			get {
				if (publickey == null) publickey = curve25519xsalsa20poly1305impl.crypto_box_getpublickey(secretkey);
				return publickey;
			}
		}
		public Byte[] SecretKey { get { return secretkey; } }
		internal static Byte[] DecodeHexString(String str, int length) {
			if (str.Length != length * 2) throw new ArgumentException("str", "Incorrect key length");
			Byte[] bytes = new Byte[length];
			for (int i = 0; i < length; i++) bytes[i] = Byte.Parse(str.Substring(i * 2, 2), NumberStyles.HexNumber);
			return bytes;
		}
	}
	public class curve25519xsalsa20poly1305 : xsalsa20poly1305 {
		public Byte[] PublicKey { get; private set; }
		public curve25519keypair SecretKey { get; private set; }
		public curve25519xsalsa20poly1305(String publickey, String secretkey)
			: this(curve25519keypair.DecodeHexString(publickey, curve25519xsalsa20poly1305impl.PUBLICKEYBYTES), new curve25519keypair(secretkey)) {
		}
		public curve25519xsalsa20poly1305(Byte[] publickey, Byte[] secretkey)
			: this(publickey, new curve25519keypair(secretkey)) {
		}
		public curve25519xsalsa20poly1305(Byte[] publickey, curve25519keypair secretkey)
			: base(curve25519xsalsa20poly1305impl.crypto_box_beforenm(publickey, secretkey.SecretKey)) {
			this.PublicKey = publickey;
			this.SecretKey = secretkey;
		}
	}
	public class xsalsa20poly1305 {
		protected Byte[] sharedkey = new Byte[xsalsa20poly1305impl.KEYBYTES];
		Byte[] nonce = new Byte[xsalsa20poly1305impl.NONCEBYTES];

		public int SharedKeySize { get { return xsalsa20poly1305impl.KEYBYTES; } }

		public xsalsa20poly1305(Byte[] sharedkey) : this(sharedkey, null) { }
		public xsalsa20poly1305(Byte[] sharedkey, Byte[] nonce) {
			if (sharedkey == null) throw new ArgumentNullException("secretkey");
			if (sharedkey.Length != xsalsa20poly1305impl.KEYBYTES) throw new ArgumentOutOfRangeException("secretkey", "The key size does not match the expected key length");
			sharedkey.CopyTo(this.sharedkey, 0);
			if (nonce != null) this.Nonce = nonce;
		}

		public Byte[] Nonce {
			get { return this.nonce; }
			set { NonceValue = value; }
		}
		public Byte[] NonceValue {
			get { return ArrayUtil.ToArray(nonce); }
			set {
				if (ReferenceEquals(value, null)) throw new ArgumentNullException("value");
				if (value.Length > xsalsa20poly1305impl.NONCEBYTES) throw new ArgumentOutOfRangeException("value", "Nonce is too big");
				value.CopyTo(nonce, 0);
				Array.Clear(this.nonce, value.Length, this.nonce.Length - value.Length);
			}
		}
		public Byte[] NonceBuffer {
			get { return this.nonce; }
			set {
				if (ReferenceEquals(value, null)) throw new ArgumentNullException("value");
				if (value.Length > xsalsa20poly1305impl.NONCEBYTES) throw new ArgumentOutOfRangeException("value", "Incorrect nonce length");
				this.nonce = value;
			}
		}

		public Byte[] SharedKey {
			get { return sharedkey; }
		}

		public unsafe Byte[] Encrypt(Byte[] data) {
			return Encrypt(data, 0, data.Length);
		}
		public unsafe Byte[] Encrypt(Byte[] data, int offset, int count) {
			if (ReferenceEquals(data, null)) throw new ArgumentNullException("data");
			if (offset < 0) throw new ArgumentOutOfRangeException("offset", "Offset can not be negative");
			if (data.Length < offset + count) throw new ArgumentOutOfRangeException("count", "The specified range is outside of the array");
			Byte[] ret = new Byte[GetEncryptedSize(count)];
			fixed (Byte* mp = data, cp = ret, np = nonce, kp = sharedkey) {
				if (xsalsa20poly1305impl.crypto_secretbox_nopad(cp, mp + offset, (ulong)count, np, kp) != 0) throw new InvalidOperationException("Encryption failed");
			}
			return ret;
		}
		public unsafe int EncryptTo(Byte[] data, int offset, int count, Byte[] outdata, int outoffset, int outcount) {
			if (ReferenceEquals(data, null)) throw new ArgumentNullException("data");
			if (offset < 0) throw new ArgumentOutOfRangeException("offset", "Offset can not be negative");
			if (data.Length < offset + count) throw new ArgumentOutOfRangeException("count", "The specified range is outside of the array");
			if (ReferenceEquals(outdata, null)) throw new ArgumentNullException("outdata");
			if (outoffset < 0) throw new ArgumentOutOfRangeException("outoffset", "Offset can not be negative");
			if (outdata.Length < outoffset + outcount) throw new ArgumentOutOfRangeException("outcount", "The specified range is outside of the array");
			int retcount = GetEncryptedSize(count);
			if (outcount < retcount) throw new ArgumentOutOfRangeException("outcount", "The output buffer is too small");
			fixed (Byte* mp = data, cp = outdata, np = nonce, kp = sharedkey) {
				if (xsalsa20poly1305impl.crypto_secretbox_nopad(cp + outoffset, mp + offset, (ulong)count, np, kp) != 0) throw new InvalidOperationException("Encryption failed");
			}
			return outcount;
		}
		/*public unsafe void EncryptInplace(Byte[] data, int offset, int count) {
			if (ReferenceEquals(data, null)) throw new ArgumentNullException("data");
			if (offset < 0) throw new ArgumentOutOfRangeException("offset", "Offset can not be negative");
			if (data.Length < offset + count) throw new ArgumentOutOfRangeException("count", "The specified range is outside of the array");
			if (count < 16) throw new ArgumentOutOfRangeException("count", "count should be at least 16");
			fixed (Byte* mp = data, np = nonce, kp = sharedkey) {
				if (xsalsa20poly1305impl.crypto_secretbox_inplace_nopad(mp + offset, (ulong)count, np, kp) != 0) throw new InvalidOperationException("Encryption failed");
			}
		}*/
		public int GetEncryptedSize(int size) {
			return size + 16;
		}

		public unsafe Byte[] Decrypt(Byte[] data) {
			return Decrypt(data, 0, data.Length);
		}
		public unsafe Byte[] Decrypt(Byte[] data, int offset, int count) {
			if (ReferenceEquals(data, null)) throw new ArgumentNullException("data");
			if (offset < 0) throw new ArgumentOutOfRangeException("offset", "Offset can not be negative");
			if (data.Length < offset + count) throw new ArgumentOutOfRangeException("count", "The specified range is outside of the array");
			if (count < 16) return null;
			Byte[] ret = new Byte[GetDecryptedSize(count)];
			fixed (Byte* cp = data, mp = ret, np = nonce, kp = sharedkey) {
				if (xsalsa20poly1305impl.crypto_secretbox_open_nopad(mp, cp + offset, (ulong)count, np, kp) != 0) return null;
			}
			return ret;
		}
		public unsafe int? DecryptTo(Byte[] data, int offset, int count, Byte[] outdata, int outoffset, int outcount) {
			if (ReferenceEquals(data, null)) throw new ArgumentNullException("data");
			if (offset < 0) throw new ArgumentOutOfRangeException("offset", "Offset can not be negative");
			if (data.Length < offset + count) throw new ArgumentOutOfRangeException("count", "The specified range is outside of the array");
			if (count < 16) return null;
			if (ReferenceEquals(outdata, null)) throw new ArgumentNullException("outdata");
			if (outoffset < 0) throw new ArgumentOutOfRangeException("outoffset", "Offset can not be negative");
			if (outdata.Length < outoffset + outcount) throw new ArgumentOutOfRangeException("outcount", "The specified range is outside of the array");
			int retcount = GetDecryptedSize(count);
			if (outcount < retcount) throw new ArgumentOutOfRangeException("outcount", "The output buffer is too small");
			fixed (Byte* cp = data, mp = outdata, np = nonce, kp = sharedkey) {
				if (xsalsa20poly1305impl.crypto_secretbox_open_nopad(mp + outoffset, cp + offset, (ulong)count, np, kp) != 0) return null;
			}
			return retcount;
		}
		public unsafe ArraySegment<Byte>? DecryptInplace(Byte[] data, int offset, int count) {
			if (ReferenceEquals(data, null)) throw new ArgumentNullException("data");
			if (offset < 0) throw new ArgumentOutOfRangeException("offset", "Offset can not be negative");
			if (data.Length < offset + count) throw new ArgumentOutOfRangeException("count", "The specified range is outside of the array");
			if (count < 16) return null;
			fixed (Byte* cp = data, np = nonce, kp = sharedkey) {
				if (xsalsa20poly1305impl.crypto_secretbox_open_inplace_nopad(cp + offset, (ulong)count, np, kp) != 0) return null;
			}
			return new ArraySegment<byte>(data, offset + 16, count - 16);
		}
		public int GetDecryptedSize(int size) {
			if (size < 16) return -1;
			return size - 16;
		}

		public Boolean Verify(Byte[] data) {
			return Verify(data, 0, data.Length);
		}
		public unsafe Boolean Verify(Byte[] data, int offset, int count) {
			if (ReferenceEquals(data, null)) throw new ArgumentNullException("data");
			if (offset < 0) throw new ArgumentOutOfRangeException("offset", "Offset can not be negative");
			if (data.Length < offset + count) throw new ArgumentOutOfRangeException("count", "The specified range is outside of the array");
			if (count < 16) return false;
			Byte[] ret = new Byte[GetDecryptedSize(count)];
			fixed (Byte* cp = data, np = nonce, kp = sharedkey) {
				return xsalsa20poly1305impl.crypto_secretbox_verify(cp + offset, (ulong)count, np, kp);
			}
		}

		public Byte[] GenerateRandomNonce() {
			randombytes.generate(nonce);
			return nonce;
		}
		public void IncrementNonceLE() {
			for (int i = 0; i < nonce.Length && ++nonce[i] == 0; i++) ;
		}
		public void IncrementNonceBE() {
			for (int i = nonce.Length - 1; i >= 0 && ++nonce[i] == 0; i--) ;
		}

		public xsalsa20poly1305 Clone() {
			return new xsalsa20poly1305(sharedkey, nonce);
		}
	}
	public class edwards25519sha512batch {
		public static Byte[] Sign(Byte[] message, Byte[] secretkey) {
			return edwards25519sha512batchimpl.crypto_sign(message, secretkey);
		}
		public static int GetSignedSize(int size) {
			return size + 64;
		}
		public static Byte[] Open(Byte[] signed, Byte[] publickey) {
			return edwards25519sha512batchimpl.crypto_sign_open(signed, publickey);
		}
		public static unsafe Boolean Verify(Byte[] signed, Byte[] publickey) {
			if (publickey.Length != edwards25519sha512batchimpl.PUBLICKEYBYTES) throw new ArgumentException("publickey.Length != PUBLICKEYBYTES");
			UInt64 mlen;
			fixed (Byte* smp = signed, pkp = publickey) return edwards25519sha512batchimpl.crypto_sign_open(null, out mlen, smp, (ulong)signed.Length, pkp) == 0;
		}
		public static Byte[] Extract(Byte[] signed) {
			if (signed.Length < 64) return null;
			Byte[] ret = new Byte[signed.Length - 64];
			Buffer.BlockCopy(signed, 32, ret, 0, ret.Length);
			return ret;
		}
		public static int GetExtractedSize(int size) {
			if (size < 64) return -1;
			return size - 64;
		}
	}
	public class md5 {
		md5impl.md5state state = new md5impl.md5state();
		public md5() {
			state.init();
		}
		private md5(md5impl.md5state state) {
			this.state = state;
		}
		public unsafe void Process(Byte[] buffer, int offset, int count) {
			if (offset < 0 || count < 0 || offset + count > buffer.Length) throw new ArgumentException("buffer");
			fixed (Byte* p = buffer) state.process(p + offset, count);
		}
		public unsafe void Process(Byte[] buffer) {
			Process(buffer, 0, buffer.Length);
		}
		public unsafe void ProcessStream(Stream stream) {
			Byte[] buffer = new Byte[1024];
			while (true) {
				int read = stream.Read(buffer, 0, buffer.Length);
				if (read == 0) break;
				if (read < 0) throw new EndOfStreamException();
				Process(buffer, 0, read);
			}
		}
		public unsafe void GetHash(Byte[] hash, int offset) {
			if (offset < 0 || offset + 16 > hash.Length) throw new ArgumentException("hash");
			fixed (Byte* p = hash) state.finish(p + offset);
		}
		public unsafe Byte[] GetHash() {
			Byte[] hash = new Byte[16];
			GetHash(hash, 0);
			return hash;
		}
		public md5 Clone() {
			return new md5(state);
		}
		public static unsafe void GetHash(Byte[] buffer, int offset, int count, Byte[] hash, int hashoffset) {
			md5 sha = new md5();
			sha.Process(buffer, offset, count);
			sha.GetHash(hash, hashoffset);
		}
		public static unsafe Byte[] GetHash(Byte[] buffer, int offset, int count) {
			md5 sha = new md5();
			sha.Process(buffer, offset, count);
			return sha.GetHash();
		}
		public static unsafe Byte[] GetHash(Byte[] buffer) {
			return GetHash(buffer, 0, buffer.Length);
		}
		public static unsafe Byte[] HashStream(Stream stream) {
			md5 sha = new md5();
			sha.ProcessStream(stream);
			return sha.GetHash();
		}
		public static unsafe Byte[] HashFile(String filename) {
			using (FileStream stream = File.OpenRead(filename)) return HashStream(stream);
		}
	}
	public class sha1 {
		sha1impl.sha1state state = new sha1impl.sha1state();
		public sha1() {
			state.init();
		}
		private sha1(sha1impl.sha1state state) {
			this.state = state;
		}
		public unsafe void Process(Byte[] buffer, int offset, int count) {
			if (offset < 0 || count < 0 || offset + count > buffer.Length) throw new ArgumentException("buffer");
			fixed (Byte* p = buffer) state.process(p + offset, count);
		}
		public unsafe void Process(Byte[] buffer) {
			Process(buffer, 0, buffer.Length);
		}
		public unsafe void ProcessStream(Stream stream) {
			Byte[] buffer = new Byte[1024];
			while (true) {
				int read = stream.Read(buffer, 0, buffer.Length);
				if (read == 0) break;
				if (read < 0) throw new EndOfStreamException();
				Process(buffer, 0, read);
			}
		}
		public unsafe void GetHash(Byte[] hash, int offset) {
			if (offset < 0 || offset + 20 > hash.Length) throw new ArgumentException("hash");
			fixed (Byte* p = hash) state.finish(p + offset);
		}
		public unsafe Byte[] GetHash() {
			Byte[] hash = new Byte[20];
			GetHash(hash, 0);
			return hash;
		}
		public sha1 Clone() {
			return new sha1(state);
		}
		public static unsafe void GetHash(Byte[] buffer, int offset, int count, Byte[] hash, int hashoffset) {
			sha1 sha = new sha1();
			sha.Process(buffer, offset, count);
			sha.GetHash(hash, hashoffset);
		}
		public static unsafe Byte[] GetHash(Byte[] buffer, int offset, int count) {
			sha1 sha = new sha1();
			sha.Process(buffer, offset, count);
			return sha.GetHash();
		}
		public static unsafe Byte[] GetHash(Byte[] buffer) {
			return GetHash(buffer, 0, buffer.Length);
		}
		public static unsafe Byte[] HashStream(Stream stream) {
			sha1 sha = new sha1();
			sha.ProcessStream(stream);
			return sha.GetHash();
		}
		public static unsafe Byte[] HashFile(String filename) {
			using (FileStream stream = File.OpenRead(filename)) return HashStream(stream);
		}
	}
	public class sha256 {
		sha256impl.sha256state state = new sha256impl.sha256state();
		public sha256() {
			state.init();
		}
		private sha256(sha256impl.sha256state state) {
			this.state = state;
		}
		public unsafe void Process(Byte[] buffer, int offset, int count) {
			if (offset < 0 || count < 0 || offset + count > buffer.Length) throw new ArgumentException("buffer");
			fixed (Byte* p = buffer) state.process(p + offset, count);
		}
		public unsafe void Process(Byte[] buffer) {
			Process(buffer, 0, buffer.Length);
		}
		public unsafe void ProcessStream(Stream stream) {
			Byte[] buffer = new Byte[1024];
			while (true) {
				int read = stream.Read(buffer, 0, buffer.Length);
				if (read == 0) break;
				if (read < 0) throw new EndOfStreamException();
				Process(buffer, 0, read);
			}
		}
		public unsafe void GetHash(Byte[] hash, int offset) {
			if (offset < 0 || offset + 32 > hash.Length) throw new ArgumentException("hash");
			fixed (Byte* p = hash) state.finish(p + offset);
		}
		public unsafe Byte[] GetHash() {
			Byte[] hash = new Byte[32];
			GetHash(hash, 0);
			return hash;
		}
		public sha256 Clone() {
			return new sha256(state);
		}
		public static unsafe void GetHash(Byte[] buffer, int offset, int count, Byte[] hash, int hashoffset) {
			sha256 sha = new sha256();
			sha.Process(buffer, offset, count);
			sha.GetHash(hash, hashoffset);
		}
		public static unsafe Byte[] GetHash(Byte[] buffer, int offset, int count) {
			sha256 sha = new sha256();
			sha.Process(buffer, offset, count);
			return sha.GetHash();
		}
		public static unsafe Byte[] GetHash(Byte[] buffer) {
			return GetHash(buffer, 0, buffer.Length);
		}
		public static unsafe Byte[] HashStream(Stream stream) {
			sha256 sha = new sha256();
			sha.ProcessStream(stream);
			return sha.GetHash();
		}
		public static unsafe Byte[] HashFile(String filename) {
			using (FileStream stream = File.OpenRead(filename)) return HashStream(stream);
		}
	}
	public class sha512 {
		sha512impl.sha512state state = new sha512impl.sha512state();
		public sha512() {
			state.init();
		}
		private sha512(sha512impl.sha512state state) {
			this.state = state;
		}
		public unsafe void Process(Byte[] buffer, int offset, int count) {
			if (offset < 0 || count < 0 || offset + count > buffer.Length) throw new ArgumentException("buffer");
			fixed (Byte* p = buffer) state.process(p + offset, count);
		}
		public unsafe void Process(Byte[] buffer) {
			Process(buffer, 0, buffer.Length);
		}
		public unsafe void ProcessStream(Stream stream) {
			Byte[] buffer = new Byte[1024];
			while (true) {
				int read = stream.Read(buffer, 0, buffer.Length);
				if (read == 0) break;
				if (read < 0) throw new EndOfStreamException();
				Process(buffer, 0, read);
			}
		}
		public unsafe void GetHash(Byte[] hash, int offset) {
			if (offset < 0 || offset + 64 > hash.Length) throw new ArgumentException("hash");
			fixed (Byte* p = hash) state.finish(p + offset);
		}
		public unsafe Byte[] GetHash() {
			Byte[] hash = new Byte[64];
			GetHash(hash, 0);
			return hash;
		}
		public sha512 Clone() {
			return new sha512(state);
		}
		public static unsafe void GetHash(Byte[] buffer, int offset, int count, Byte[] hash, int hashoffset) {
			sha512 sha = new sha512();
			sha.Process(buffer, offset, count);
			sha.GetHash(hash, hashoffset);
		}
		public static unsafe Byte[] GetHash(Byte[] buffer, int offset, int count) {
			sha512 sha = new sha512();
			sha.Process(buffer, offset, count);
			return sha.GetHash();
		}
		public static unsafe Byte[] GetHash(Byte[] buffer) {
			return GetHash(buffer, 0, buffer.Length);
		}
		public static unsafe Byte[] HashStream(Stream stream) {
			sha512 sha = new sha512();
			sha.ProcessStream(stream);
			return sha.GetHash();
		}
		public static unsafe Byte[] HashFile(String filename) {
			using (FileStream stream = File.OpenRead(filename)) return HashStream(stream);
		}
	}
	public class ed25519keypair {
		internal Byte[] key;

		public ed25519keypair() {
			Byte[] pk;
			ed25519impl.crypto_sign_keypair(out pk, out key);
		}
		public ed25519keypair(Byte[] key) {
			if (key.Length == 64) {
				this.key = ArrayUtil.ToArray(key);
			} else {
				Byte[] pk;
				ed25519impl.crypto_sign_seed_keypair(out pk, out this.key, key);
			}
		}
		public ed25519keypair(String key) : this(curve25519keypair.DecodeHexString(key, key.Length / 2)) { }
		public Byte[] PublicKey { get { return ArrayUtil.Slice(key, 32, 32); } }
		public Byte[] SecretKey { get { return ArrayUtil.Slice(key, 0, 32); } }
		public Byte[] ExpandedKey { get { return ArrayUtil.ToArray(key); } }

		public Byte[] GetSignature(Byte[] message) {
			return ed25519.GetSignature(message, key);
		}
		public Byte[] GetSignature(Byte[] message, int offset, int count) {
			return ed25519.GetSignature(new ArraySegment<Byte>(message, offset, count), key);
		}
		public Byte[] SignMessage(Byte[] message) {
			return ed25519.SignMessage(message, key);
		}
	}
	public class ed25519 {
		public static unsafe Byte[] GetSignature(Byte[] message, Byte[] key) {
			if (message == null) throw new ArgumentNullException("message");
			if (key.Length != 64) throw new ArgumentException("key");
			Byte[] sig = new Byte[64];
			fixed (Byte* sigp = sig, msgp = message, kp = key) ed25519impl.crypto_getsignature(sigp, msgp, message.Length, kp);
			return sig;
		}
		public static unsafe Byte[] GetSignature(ArraySegment<Byte> message, Byte[] key) {
			if (message == null) throw new ArgumentNullException("message");
			if (key.Length != 64) throw new ArgumentException("key");
			if (message.Offset < 0 || message.Count < 0 || message.Offset + message.Count > message.Array.Length) throw new ArgumentException("message");
			Byte[] sig = new Byte[64];
			fixed (Byte* sigp = sig, msgp = message.Array, kp = key) ed25519impl.crypto_getsignature(sigp, msgp + message.Offset, message.Count, kp);
			return sig;
		}
		public static unsafe Byte[] SignMessage(Byte[] message, Byte[] key) {
			if (key.Length != 64) throw new ArgumentException("key");
			Byte[] ret = new Byte[message.Length + 64];
			int smlen;
			fixed (Byte* sm = ret, msgp = message, kp = key) ed25519impl.crypto_sign(sm, out smlen, msgp, message.Length, kp);
			return ret;
		}
		public static unsafe Boolean VerifySignature(Byte[] message, Byte[] signature, Byte[] pk) {
			if (signature.Length < 64) throw new ArgumentException("signature");
			if (pk.Length < 32) throw new ArgumentException("pk");
			fixed (Byte* sp = signature, mp = message, kp = pk) return ed25519impl.crypto_sign_verify(sp, mp, message.Length, kp);
		}
		public static unsafe Boolean VerifySignature(ArraySegment<Byte> message, ArraySegment<Byte> signature, Byte[] pk) {
			if (signature.Offset < 0 || signature.Count < 64 || signature.Offset + signature.Count > signature.Array.Length) throw new ArgumentException("signature");
			if (message.Offset < 0 || message.Count < 0 || message.Offset + message.Count > message.Array.Length) throw new ArgumentException("message");
			if (pk.Length < 32) throw new ArgumentException("pk");
			fixed (Byte* sp = signature.Array, mp = message.Array, kp = pk) return ed25519impl.crypto_sign_verify(sp + signature.Offset, mp + message.Offset, message.Count, kp);
		}
		public static unsafe Boolean VerifySignedMessage(Byte[] signedmessage, Byte[] pk) {
			if (signedmessage.Length < 64) throw new ArgumentException("signedmessage");
			if (pk.Length < 32) throw new ArgumentException("pk");
			fixed (Byte* mp = signedmessage, kp = pk) return ed25519impl.crypto_sign_verify(mp, mp + 64, signedmessage.Length - 64, kp);
		}
		public static Byte[] ExtractSignedMessage(Byte[] signedmessage) {
			return ArrayUtil.Slice(signedmessage, 64);
		}
		public static Byte[] ExtractSignedMessage(ArraySegment<Byte> signedmessage) {
			return ArrayUtil.Slice(signedmessage.Array, signedmessage.Offset + 64, signedmessage.Count - 64);
		}
		public static ArraySegment<Byte> ExtractSignedMessageFast(Byte[] signedmessage) {
			return new ArraySegment<Byte>(signedmessage, 64, signedmessage.Length - 64);
		}
		public static ArraySegment<Byte> ExtractSignedMessageFast(ArraySegment<Byte> signedmessage) {
			return new ArraySegment<Byte>(signedmessage.Array, signedmessage.Offset + 64, signedmessage.Count - 64);
		}
		public static Byte[] OpenSignedMessage(Byte[] signedmessage, Byte[] pk) {
			if (!VerifySignedMessage(signedmessage, pk)) return null;
			return ExtractSignedMessage(signedmessage);
		}
	}
}
