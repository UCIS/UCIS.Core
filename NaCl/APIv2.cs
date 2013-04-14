using System;
using curve25519xsalsa20poly1305impl = UCIS.NaCl.crypto_box.curve25519xsalsa20poly1305;
using edwards25519sha512batchimpl = UCIS.NaCl.crypto_sign.edwards25519sha512batch;
using xsalsa20poly1305impl = UCIS.NaCl.crypto_secretbox.xsalsa20poly1305;

namespace UCIS.NaCl.v2 {
	public class curve25519keypair {
		private Byte[] publickey, secretkey;

		public curve25519keypair() {
			curve25519xsalsa20poly1305impl.crypto_box_keypair(out publickey, out secretkey);
		}
		public curve25519keypair(Byte[] secretkey) {
			this.publickey = curve25519xsalsa20poly1305impl.crypto_box_getpublickey(secretkey);
			this.secretkey = secretkey;
		}
		public curve25519keypair(Byte[] secretkey, Byte[] publickey) {
			if (publickey.Length != curve25519xsalsa20poly1305impl.PUBLICKEYBYTES) throw new ArgumentOutOfRangeException("publickey");
			if (secretkey.Length != curve25519xsalsa20poly1305impl.SECRETKEYBYTES) throw new ArgumentOutOfRangeException("secretkey");
			this.secretkey = secretkey;
			this.publickey = publickey;
		}
		public Byte[] PublicKey { get { return publickey; } }
		public Byte[] SecretKey { get { return secretkey; } }
	}
	public class curve25519xsalsa20poly1305 : xsalsa20poly1305 {
		public curve25519xsalsa20poly1305(Byte[] publickey, curve25519keypair secretkey)
			: this(publickey, secretkey.SecretKey) {
		}
		public curve25519xsalsa20poly1305(Byte[] publickey, Byte[] secretkey)
			: base(curve25519xsalsa20poly1305impl.crypto_box_beforenm(publickey, secretkey)) {
		}
	}
	public class xsalsa20poly1305 {
		Byte[] sharedkey;
		Byte[] nonce;

		public int SharedKeySize { get { return xsalsa20poly1305impl.KEYBYTES; } }

		public xsalsa20poly1305(Byte[] sharedkey) : this(sharedkey, null) { }
		public xsalsa20poly1305(Byte[] sharedkey, Byte[] nonce) {
			if (ReferenceEquals(sharedkey, null)) throw new ArgumentNullException("secretkey");
			if (sharedkey.Length != xsalsa20poly1305impl.KEYBYTES) throw new ArgumentOutOfRangeException("secretkey", "The key size does not match the expected key length");
			this.sharedkey = sharedkey;
			this.nonce = new Byte[xsalsa20poly1305impl.NONCEBYTES];
			if (!ReferenceEquals(nonce, null)) this.Nonce = nonce;
		}

		public Byte[] Nonce {
			get { return this.nonce; }
			set {
				if (ReferenceEquals(value, null)) throw new ArgumentNullException("value");
				if (value.Length > xsalsa20poly1305impl.NONCEBYTES) throw new ArgumentOutOfRangeException("value", "Nonce is too big");
				value.CopyTo(nonce, 0);
				Array.Clear(this.nonce, value.Length, this.nonce.Length - value.Length);
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
		public Byte[] Sign(Byte[] message, Byte[] secretkey) {
			return edwards25519sha512batchimpl.crypto_sign(message, secretkey);
		}
		public int GetSignedSize(int size) {
			return size + 64;
		}
		public Byte[] Open(Byte[] signed, Byte[] publickey) {
			return edwards25519sha512batchimpl.crypto_sign_open(signed, publickey);
		}
		public unsafe Boolean Verify(Byte[] signed, Byte[] publickey) {
			if (publickey.Length != edwards25519sha512batchimpl.PUBLICKEYBYTES) throw new ArgumentException("publickey.Length != PUBLICKEYBYTES");
			UInt64 mlen;
			fixed (Byte* smp = signed, pkp = publickey) return edwards25519sha512batchimpl.crypto_sign_open(null, out mlen, smp, (ulong)signed.Length, pkp) == 0;
		}
		public Byte[] Extract(Byte[] signed) {
			if (signed.Length < 64) return null;
			Byte[] ret = new Byte[signed.Length - 64];
			Buffer.BlockCopy(signed, 32, ret, 0, ret.Length);
			return ret;
		}
		public int GetExtractedSize(int size) {
			if (size < 64) return -1;
			return size - 64;
		}
	}
}
