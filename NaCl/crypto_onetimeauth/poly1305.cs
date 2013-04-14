using System;

namespace UCIS.NaCl.crypto_onetimeauth {
	unsafe static class poly1305 {
		static Boolean UseNativeFunctions = false;
		static unsafe internal Boolean EnableNativeImplementation() {
			UseNativeFunctions = false;
			Byte* dummy = stackalloc Byte[32];
			try {
				if (Native.crypto_onetimeauth_poly1305(dummy, dummy, 0, dummy) != 0) return false;
			} catch (Exception) {
				return false;
			}
			return UseNativeFunctions = true;
		}

		const int CRYPTO_BYTES = 16;
		const int CRYPTO_KEYBYTES = 32;

		//Never written to
		static UInt32[] minusp = new UInt32[17] { 5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 252 };

		public static int crypto_onetimeauth_verify(Byte* h, Byte* inv, UInt64 inlen, Byte* k) {
			Byte* correct = stackalloc Byte[16];
			crypto_onetimeauth(correct, inv, inlen, k);
			return crypto_verify._16.crypto_verify(h, correct);
		}

		static void add(UInt32* h, UInt32* c) { //h[17], c[17]
			UInt32 u = 0;
			for (int j = 0; j < 17; ++j) { u += h[j] + c[j]; h[j] = u & 255; u >>= 8; }
		}

		static void squeeze(UInt32* h) { //h[17]
			UInt32 u = 0;
			for (int j = 0; j < 16; ++j) { u += h[j]; h[j] = u & 255; u >>= 8; }
			u += h[16]; h[16] = u & 3;
			u = 5 * (u >> 2);
			for (int j = 0; j < 16; ++j) { u += h[j]; h[j] = u & 255; u >>= 8; }
			u += h[16]; h[16] = u;
		}

		static void freeze(UInt32* h) { //h[17]
			UInt32* horig = stackalloc UInt32[17];
			for (int j = 0; j < 17; ++j) horig[j] = h[j];
			fixed (uint* minuspp = minusp) add(h, minuspp);
			UInt32 negative = (UInt32)(-(h[16] >> 7));
			for (int j = 0; j < 17; ++j) h[j] ^= negative & (horig[j] ^ h[j]);
		}

		static void mulmod(UInt32* h, UInt32* r) { //h[17], r[17]
			UInt32* hr = stackalloc UInt32[17];
			for (uint i = 0; i < 17; ++i) {
				UInt32 u = 0;
				for (uint j = i + 1; j < 17; ++j) u += h[j] * r[i + 17 - j];
				u *= 320;
				for (uint j = 0; j <= i; ++j) u += h[j] * r[i - j];
				hr[i] = u;
			}
			for (int i = 0; i < 17; ++i) h[i] = hr[i];
			squeeze(h);
		}

		public static void crypto_onetimeauth(Byte* outv, Byte* inv, UInt64 inlen, Byte* k) {
			if (UseNativeFunctions) {
				Native.crypto_onetimeauth_poly1305(outv, inv, inlen, k);
				return;
			}

			UInt32* r = stackalloc UInt32[17];
			UInt32* h = stackalloc UInt32[17];
			UInt32* c = stackalloc UInt32[17];

			r[0] = k[0];
			r[1] = k[1];
			r[2] = k[2];
			r[3] = (UInt32)(k[3] & 15);
			r[4] = (UInt32)(k[4] & 252);
			r[5] = k[5];
			r[6] = k[6];
			r[7] = (UInt32)(k[7] & 15);
			r[8] = (UInt32)(k[8] & 252);
			r[9] = k[9];
			r[10] = k[10];
			r[11] = (UInt32)(k[11] & 15);
			r[12] = (UInt32)(k[12] & 252);
			r[13] = k[13];
			r[14] = k[14];
			r[15] = (UInt32)(k[15] & 15);
			r[16] = 0;

			for (int j = 0; j < 17; ++j) h[j] = 0;

			while (inlen > 0) {
				int m = (int)Math.Min(16, inlen);
				for (int j = 0; j < m; ++j) c[j] = inv[j];
				c[m] = 1;
				inv += m;
				inlen -= (ulong)m;
				for (int j = m + 1; j < 17; ++j) c[j] = 0;
				add(h, c);
				mulmod(h, r);
			}

			freeze(h);

			for (int j = 0; j < 16; ++j) c[j] = k[j + 16];
			c[16] = 0;
			add(h, c);
			for (int j = 0; j < 16; ++j) outv[j] = (Byte)h[j];
		}
	}
}