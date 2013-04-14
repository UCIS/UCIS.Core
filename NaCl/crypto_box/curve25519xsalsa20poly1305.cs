using System;
using UCIS.NaCl;
using System.Runtime.InteropServices;

namespace UCIS.NaCl.crypto_box {
	public static class curve25519xsalsa20poly1305 {
		/* constants */
		public const int PUBLICKEYBYTES = 32;
		public const int SECRETKEYBYTES = 32;
		public const int BEFORENMBYTES = 32;
		public const int NONCEBYTES = 24;
		public const int ZEROBYTES = 32;
		public const int BOXZEROBYTES = 16;

		//Never written to
		static Byte[] sigma = new Byte[16] {(Byte)'e', (Byte)'x', (Byte)'p', (Byte)'a', //[16] = "expand 32-byte k";
											(Byte)'n', (Byte)'d', (Byte)' ', (Byte)'3',
											(Byte)'2', (Byte)'-', (Byte)'b', (Byte)'y',
											(Byte)'t', (Byte)'e', (Byte)' ', (Byte)'k', };

		/* static pointer based methods */
		static unsafe public void crypto_box_getpublickey(Byte* pk, Byte* sk) {
			crypto_scalarmult.curve25519.crypto_scalarmult_base(pk, sk);
		}
		static unsafe public int crypto_box_afternm(Byte* c, Byte* m, UInt64 mlen, Byte* n, Byte* k) {
			return crypto_secretbox.xsalsa20poly1305.crypto_secretbox(c, m, mlen, n, k);
		}
		static unsafe public int crypto_box_open_afternm(Byte* m, Byte* c, UInt64 clen, Byte* n, Byte* k) {
			return crypto_secretbox.xsalsa20poly1305.crypto_secretbox_open(m, c, clen, n, k);
		}
		static unsafe public void crypto_box_beforenm(Byte* k, Byte* pk, Byte* sk) {
			Byte[] s = new Byte[32];
			fixed (Byte* sp = s, sigmap = sigma) { //, np = n
				crypto_scalarmult.curve25519.crypto_scalarmult(sp, sk, pk);
				crypto_core.hsalsa20.crypto_core(k, null, sp, sigmap); //k, np, sp, sigmap
			}
		}
		static unsafe public int crypto_box(Byte* c, Byte* m, UInt64 mlen, Byte* n, Byte* pk, Byte* sk) {
			Byte[] k = new Byte[BEFORENMBYTES];
			fixed (Byte* kp = k) {
				crypto_box_beforenm(kp, pk, sk);
				return crypto_box_afternm(c, m, mlen, n, kp);
			}
		}
		static unsafe public int crypto_box_open(Byte* m, Byte* c, UInt64 clen, Byte* n, Byte* pk, Byte* sk) {
			Byte[] k = new Byte[BEFORENMBYTES];
			fixed (Byte* kp = k) {
				crypto_box_beforenm(kp, pk, sk);
				return crypto_box_open_afternm(m, c, clen, n, kp);
			}
		}

		/* static array based methods */
		static unsafe public void crypto_box_keypair(out Byte[] pk, out Byte[] sk) {
			sk = new Byte[32];
			pk = new Byte[32];
			randombytes.generate(sk); //randombytes(sk, 32);
			fixed (Byte* skp = sk, pkp = pk) crypto_scalarmult.curve25519.crypto_scalarmult_base(pkp, skp);
		}
		static unsafe public Byte[] crypto_box_getpublickey(Byte[] sk) {
			Byte[] pk;
			crypto_box_getpublickey(out pk, sk);
			return pk;
		}
		static unsafe public void crypto_box_getpublickey(out Byte[] pk, Byte[] sk) {
			if (sk.Length != SECRETKEYBYTES) throw new ArgumentOutOfRangeException("sk");
			pk = new Byte[32];
			fixed (Byte* skp = sk, pkp = pk) crypto_box_getpublickey(pkp, skp);
		}
		static unsafe public void crypto_box_beforenm(Byte[] k, Byte[] pk, Byte[] sk) {
			fixed (Byte* kp = k, pkp = pk, skp = sk) crypto_box_beforenm(kp, pkp, skp);
		}
		static unsafe public Byte[] crypto_box_beforenm(Byte[] pk, Byte[] sk) {
			if (pk.Length != PUBLICKEYBYTES) throw new ArgumentOutOfRangeException("pk");
			if (sk.Length != SECRETKEYBYTES) throw new ArgumentOutOfRangeException("sk");
			Byte[] k = new Byte[BEFORENMBYTES];
			fixed (Byte* kp = k, pkp = pk, skp = sk) crypto_box_beforenm(kp, pkp, skp);
			return k;
		}
		static unsafe public int crypto_box_afternm(Byte[] c, Byte[] m, Byte[] n, Byte[] k) {
			fixed (Byte* cp = c, mp = m, np = n, kp = k) return crypto_box_afternm(cp, mp, (ulong)m.Length, np, kp);
		}
		static unsafe public int crypto_box_open_afternm(Byte[] m, Byte[] c, Byte[] n, Byte[] k) {
			fixed (Byte* cp = c, mp = m, np = n, kp = k) return crypto_box_open_afternm(mp, cp, (ulong)c.Length, np, kp);
		}
		static unsafe public int crypto_box(Byte[] c, Byte[] m, Byte[] n, Byte[] pk, Byte[] sk) {
			fixed (Byte* cp = c, mp = m, np = n, pkp = pk, skp = sk) return crypto_box(cp, mp, (ulong)m.Length, np, pkp, skp);
		}
		static unsafe public int crypto_box_open(Byte[] m, Byte[] c, Byte[] n, Byte[] pk, Byte[] sk) {
			fixed (Byte* cp = c, mp = m, np = n, pkp = pk, skp = sk) return crypto_box_open(mp, cp, (ulong)c.Length, np, pkp, skp);
		}

		static unsafe public int crypto_box_afternm(Byte[] c, int coffset, Byte[] m, int moffset, int mlen, Byte[] n, Byte[] k) {
			fixed (Byte* cp = c, mp = m, np = n, kp = k) return crypto_box_afternm(cp + coffset, mp + moffset, (ulong)mlen, np, kp);
		}
		static unsafe public int crypto_box_open_afternm(Byte[] m, int moffset, Byte[] c, int coffset, int clen, Byte[] n, Byte[] k) {
			fixed (Byte* cp = c, mp = m, np = n, kp = k) return crypto_box_open_afternm(mp + moffset, cp + coffset, (ulong)clen, np, kp);
		}
		static unsafe public int crypto_box(Byte[] c, int coffset, Byte[] m, int moffset, int mlen, Byte[] n, Byte[] pk, Byte[] sk) {
			fixed (Byte* cp = c, mp = m, np = n, pkp = pk, skp = sk) return crypto_box(cp + coffset, mp + moffset, (ulong)mlen, np, pkp, skp);
		}
		static unsafe public int crypto_box_open(Byte[] m, int moffset, Byte[] c, int coffset, int clen, Byte[] n, Byte[] pk, Byte[] sk) {
			fixed (Byte* cp = c, mp = m, np = n, pkp = pk, skp = sk) return crypto_box_open(mp + moffset, cp + coffset, (ulong)clen, np, pkp, skp);
		}
	}
}