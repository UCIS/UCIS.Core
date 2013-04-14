using System;

namespace UCIS.NaCl.crypto_scalarmult {
	unsafe public static class curve25519 {
		const int CRYPTO_BYTES = 32;
		const int CRYPTO_SCALARBYTES = 32;

		//Never written to (both)
		static Byte[] basev = new Byte[32] { 9, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }; //[32] = {9};
		static UInt32[] minusp = new UInt32[32] { 19, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 128 };

		public static void crypto_scalarmult_base(Byte* q, Byte* n) {
			fixed (Byte* basevp = basev) crypto_scalarmult(q, n, basevp);
		}
		public static void crypto_scalarmult_base(Byte[] q, Byte[] n) {
			fixed (Byte* basevp = basev, qp = q, np = n) crypto_scalarmult(qp, np, basevp);
		}

		static void add(UInt32[] outv, UInt32[] a, UInt32[] b) { //outv[32],a[32],b[32]
			fixed (UInt32* outvp = outv, ap = a, bp = b) add(outvp, ap, bp);
		}
		static void add(UInt32[] outv, UInt32[] a, UInt32* b) {
			fixed (UInt32* outvp = outv, ap = a) add(outvp, ap, b);
		}
		static void add(UInt32* outv, UInt32* a, UInt32* b) {
			UInt32 u = 0;
			for (int j = 0; j < 31; ++j) { u += a[j] + b[j]; outv[j] = u & 255; u >>= 8; }
			u += a[31] + b[31]; outv[31] = u;
		}

		static void sub(UInt32* outv, UInt32[] a, UInt32* b) {//outv[32], a[32], b[32]
			UInt32 u = 218;
			for (int j = 0; j < 31; ++j) {
				u += a[j] + 65280 - b[j];
				outv[j] = u & 255;
				u >>= 8;
			}
			u += a[31] - b[31];
			outv[31] = u;
		}

		static void squeeze(UInt32* a) { //a[32]
			UInt32 u = 0;
			for (int j = 0; j < 31; ++j) { u += a[j]; a[j] = u & 255; u >>= 8; }
			u += a[31]; a[31] = u & 127;
			u = 19 * (u >> 7);
			for (int j = 0; j < 31; ++j) { u += a[j]; a[j] = u & 255; u >>= 8; }
			u += a[31]; a[31] = u;
		}

		static void freeze(UInt32* a) { //a[32]
			UInt32[] aorig = new UInt32[32];
			for (int j = 0; j < 32; ++j) aorig[j] = a[j];
			fixed (UInt32* minuspp = minusp) add(a, a, minuspp);
			UInt32 negative = (UInt32)(-((a[31] >> 7) & 1));
			for (int j = 0; j < 32; ++j) a[j] ^= negative & (aorig[j] ^ a[j]);
		}

		static void mult(UInt32[] outv, UInt32[] a, UInt32[] b) { //outv[32], a[32], b[32]
			fixed (UInt32* outvp = outv, ap = a, bp = b) mult(outvp, ap, bp);
		}
		static void mult(UInt32* outv, UInt32* a, UInt32* b) {
			UInt32 j;
			for (uint i = 0; i < 32; ++i) {
				UInt32 u = 0;
				for (j = 0; j <= i; ++j) u += a[j] * b[i - j];
				for (j = i + 1; j < 32; ++j) u += 38 * a[j] * b[i + 32 - j];
				outv[i] = u;
			}
			squeeze(outv);
		}

		static void mult121665(UInt32[] outv, UInt32[] a) { //outv[32], a[32]
			UInt32 j;
			UInt32 u = 0;
			for (j = 0; j < 31; ++j) { u += 121665 * a[j]; outv[j] = u & 255; u >>= 8; }
			u += 121665 * a[31]; outv[31] = u & 127;
			u = 19 * (u >> 7);
			for (j = 0; j < 31; ++j) { u += outv[j]; outv[j] = u & 255; u >>= 8; }
			u += outv[j]; outv[j] = u;
		}

		static void square(UInt32[] outv, UInt32[] a) { //outv[32], a[32]
			fixed (UInt32* outvp = outv, ap = a) square(outvp, ap);
		}
		static void square(UInt32* outv, UInt32* a) {
			UInt32 j;
			for (uint i = 0; i < 32; ++i) {
				UInt32 u = 0;
				for (j = 0; j < i - j; ++j) u += a[j] * a[i - j];
				for (j = i + 1; j < i + 32 - j; ++j) u += 38 * a[j] * a[i + 32 - j];
				u *= 2;
				if ((i & 1) == 0) {
					u += a[i / 2] * a[i / 2];
					u += 38 * a[i / 2 + 16] * a[i / 2 + 16];
				}
				outv[i] = u;
			}
			squeeze(outv);
		}

		static void select(UInt32[] p, UInt32[] q, UInt32[] r, UInt32[] s, UInt32 b) { //p[64], q[64], r[64], s[64]
			UInt32 bminus1 = b - 1;
			for (int j = 0; j < 64; ++j) {
				UInt32 t = bminus1 & (r[j] ^ s[j]);
				p[j] = s[j] ^ t;
				q[j] = r[j] ^ t;
			}
		}

		static void mainloop(UInt32[] work, Byte[] e) { //work[64], e[32]
			UInt32[] xzm1 = new UInt32[64];
			UInt32[] xzm = new UInt32[64];
			UInt32[] xzmb = new UInt32[64];
			UInt32[] xzm1b = new UInt32[64];
			UInt32[] xznb = new UInt32[64];
			UInt32[] xzn1b = new UInt32[64];
			UInt32[] a0 = new UInt32[64];
			UInt32[] a1 = new UInt32[64];
			UInt32[] b0 = new UInt32[64];
			UInt32[] b1 = new UInt32[64];
			UInt32[] c1 = new UInt32[64];
			UInt32[] r = new UInt32[32];
			UInt32[] s = new UInt32[32];
			UInt32[] t = new UInt32[32];
			UInt32[] u = new UInt32[32];

			for (int j = 0; j < 32; ++j) xzm1[j] = work[j];
			xzm1[32] = 1;
			for (int j = 33; j < 64; ++j) xzm1[j] = 0;

			xzm[0] = 1;
			for (int j = 1; j < 64; ++j) xzm[j] = 0;

			fixed (UInt32* xzmbp = xzmb, a0p = a0, xzm1bp = xzm1b, a1p = a1, b0p = b0, b1p = b1, c1p = c1, xznbp = xznb, up = u, xzn1bp = xzn1b, workp = work, sp = s, rp = r) {
				for (int pos = 254; pos >= 0; --pos) {
					UInt32 b = (UInt32)(e[pos / 8] >> (pos & 7));
					b &= 1;
					select(xzmb, xzm1b, xzm, xzm1, b);
					add(a0, xzmb, xzmbp + 32);
					sub(a0p + 32, xzmb, xzmbp + 32);
					add(a1, xzm1b, xzm1bp + 32);
					sub(a1p + 32, xzm1b, xzm1bp + 32);
					square(b0p, a0p);
					square(b0p + 32, a0p + 32);
					mult(b1p, a1p, a0p + 32);
					mult(b1p + 32, a1p + 32, a0p);
					add(c1, b1, b1p + 32);
					sub(c1p + 32, b1, b1p + 32);
					square(rp, c1p + 32);
					sub(sp, b0, b0p + 32);
					mult121665(t, s);
					add(u, t, b0p);
					mult(xznbp, b0p, b0p + 32);
					mult(xznbp + 32, sp, up);
					square(xzn1bp, c1p);
					mult(xzn1bp + 32, rp, workp);
					select(xzm, xzm1, xznb, xzn1b, b);
				}
			}

			for (int j = 0; j < 64; ++j) work[j] = xzm[j];
		}

		static void recip(UInt32* outv, UInt32* z) { //outv[32], z[32]
			UInt32[] z2 = new UInt32[32];
			UInt32[] z9 = new UInt32[32];
			UInt32[] z11 = new UInt32[32];
			UInt32[] z2_5_0 = new UInt32[32];
			UInt32[] z2_10_0 = new UInt32[32];
			UInt32[] z2_20_0 = new UInt32[32];
			UInt32[] z2_50_0 = new UInt32[32];
			UInt32[] z2_100_0 = new UInt32[32];
			UInt32[] t0 = new UInt32[32];
			UInt32[] t1 = new UInt32[32];

			/* 2 */
			fixed (UInt32* z2p = z2) square(z2p, z);
			/* 4 */
			square(t1, z2);
			/* 8 */
			square(t0, t1);
			/* 9 */
			fixed (UInt32* z9p = z9, t0p = t0) mult(z9p, t0p, z);
			/* 11 */
			mult(z11, z9, z2);
			/* 22 */
			square(t0, z11);
			/* 2^5 - 2^0 = 31 */
			mult(z2_5_0, t0, z9);

			/* 2^6 - 2^1 */
			square(t0, z2_5_0);
			/* 2^7 - 2^2 */
			square(t1, t0);
			/* 2^8 - 2^3 */
			square(t0, t1);
			/* 2^9 - 2^4 */
			square(t1, t0);
			/* 2^10 - 2^5 */
			square(t0, t1);
			/* 2^10 - 2^0 */
			mult(z2_10_0, t0, z2_5_0);

			/* 2^11 - 2^1 */
			square(t0, z2_10_0);
			/* 2^12 - 2^2 */
			square(t1, t0);
			/* 2^20 - 2^10 */
			for (int i = 2; i < 10; i += 2) { square(t0, t1); square(t1, t0); }
			/* 2^20 - 2^0 */
			mult(z2_20_0, t1, z2_10_0);

			/* 2^21 - 2^1 */
			square(t0, z2_20_0);
			/* 2^22 - 2^2 */
			square(t1, t0);
			/* 2^40 - 2^20 */
			for (int i = 2; i < 20; i += 2) { square(t0, t1); square(t1, t0); }
			/* 2^40 - 2^0 */
			mult(t0, t1, z2_20_0);

			/* 2^41 - 2^1 */
			square(t1, t0);
			/* 2^42 - 2^2 */
			square(t0, t1);
			/* 2^50 - 2^10 */
			for (int i = 2; i < 10; i += 2) { square(t1, t0); square(t0, t1); }
			/* 2^50 - 2^0 */
			mult(z2_50_0, t0, z2_10_0);

			/* 2^51 - 2^1 */
			square(t0, z2_50_0);
			/* 2^52 - 2^2 */
			square(t1, t0);
			/* 2^100 - 2^50 */
			for (int i = 2; i < 50; i += 2) { square(t0, t1); square(t1, t0); }
			/* 2^100 - 2^0 */
			mult(z2_100_0, t1, z2_50_0);

			/* 2^101 - 2^1 */
			square(t1, z2_100_0);
			/* 2^102 - 2^2 */
			square(t0, t1);
			/* 2^200 - 2^100 */
			for (int i = 2; i < 100; i += 2) { square(t1, t0); square(t0, t1); }
			/* 2^200 - 2^0 */
			mult(t1, t0, z2_100_0);

			/* 2^201 - 2^1 */
			square(t0, t1);
			/* 2^202 - 2^2 */
			square(t1, t0);
			/* 2^250 - 2^50 */
			for (int i = 2; i < 50; i += 2) { square(t0, t1); square(t1, t0); }
			/* 2^250 - 2^0 */
			mult(t0, t1, z2_50_0);

			/* 2^251 - 2^1 */
			square(t1, t0);
			/* 2^252 - 2^2 */
			square(t0, t1);
			/* 2^253 - 2^3 */
			square(t1, t0);
			/* 2^254 - 2^4 */
			square(t0, t1);
			/* 2^255 - 2^5 */
			square(t1, t0);
			/* 2^255 - 21 */
			fixed (UInt32* t1p = t1, z11p = z11) mult(outv, t1p, z11p);
		}

		public static void crypto_scalarmult(Byte* q, Byte* n, Byte* p) {
			UInt32[] work = new UInt32[96];
			Byte[] e = new Byte[32];
			for (int i = 0; i < 32; ++i) e[i] = n[i];
			e[0] &= 248;
			e[31] &= 127;
			e[31] |= 64;
			for (int i = 0; i < 32; ++i) work[i] = p[i];
			mainloop(work, e);
			fixed (UInt32* workp = work) {
				recip(workp + 32, workp + 32);
				mult(workp + 64, workp, workp + 32);
				freeze(workp + 64);
			}
			for (int i = 0; i < 32; ++i) q[i] = (Byte)work[64 + i];
		}
	}
}