using System;

namespace UCIS.NaCl.crypto_core {
	static unsafe class salsa20 {
		static Boolean UseNativeFunctions = false;
		static unsafe internal Boolean EnableNativeImplementation() {
			UseNativeFunctions = false;
			Byte* dummy = stackalloc Byte[64];
			try {
				if (Native.crypto_core_salsa20(dummy, dummy, dummy, dummy) != 0) return false;
			} catch (Exception) {
				return false;
			}
			return UseNativeFunctions = true;
		}

		public const int OUTPUTBYTES = 64;
		public const int INPUTBYTES = 16;
		public const int KEYBYTES = 32;
		public const int CONSTBYTES = 16;

		public const int ROUNDS = 20;

		static UInt32 load_littleendian(Byte* x) {
			return (UInt32)(x[0] | (x[1] << 8) | (x[2] << 16) | (x[3] << 24));
		}

		static void store_littleendian(Byte* x, UInt32 u) {
			x[0] = (Byte)u; u >>= 8;
			x[1] = (Byte)u; u >>= 8;
			x[2] = (Byte)u; u >>= 8;
			x[3] = (Byte)u;
		}

		public static void crypto_core(Byte* outv, Byte* inv, Byte* k, Byte[] c) {
			fixed (Byte* cp = c) crypto_core(outv, inv, k, cp);
		}

		public static void crypto_core(Byte* outv, Byte* inv, Byte* k, Byte* c) {
			if (UseNativeFunctions) {
				Native.crypto_core_salsa20(outv, inv, k, c);
				return;
			}

			UInt32 x0, x1, x2, x3, x4, x5, x6, x7, x8, x9, x10, x11, x12, x13, x14, x15;
			UInt32 j0, j1, j2, j3, j4, j5, j6, j7, j8, j9, j10, j11, j12, j13, j14, j15;

			j0 = x0 = load_littleendian(c + 0);
			j1 = x1 = load_littleendian(k + 0);
			j2 = x2 = load_littleendian(k + 4);
			j3 = x3 = load_littleendian(k + 8);
			j4 = x4 = load_littleendian(k + 12);
			j5 = x5 = load_littleendian(c + 4);
			j6 = x6 = load_littleendian(inv + 0);
			j7 = x7 = load_littleendian(inv + 4);
			j8 = x8 = load_littleendian(inv + 8);
			j9 = x9 = load_littleendian(inv + 12);
			j10 = x10 = load_littleendian(c + 8);
			j11 = x11 = load_littleendian(k + 16);
			j12 = x12 = load_littleendian(k + 20);
			j13 = x13 = load_littleendian(k + 24);
			j14 = x14 = load_littleendian(k + 28);
			j15 = x15 = load_littleendian(c + 12);

			for (int i = ROUNDS; i > 0; i -= 2) {
				UInt32 tsum;
				tsum = x0 + x12; x4 ^= (tsum << 7) | (tsum >> (32 - 7));
				tsum = x4 + x0; x8 ^= (tsum << 9) | (tsum >> (32 - 9));
				tsum = x8 + x4; x12 ^= (tsum << 13) | (tsum >> (32 - 13));
				tsum = x12 + x8; x0 ^= (tsum << 18) | (tsum >> (32 - 18));
				tsum = x5 + x1; x9 ^= (tsum << 7) | (tsum >> (32 - 7));
				tsum = x9 + x5; x13 ^= (tsum << 9) | (tsum >> (32 - 9));
				tsum = x13 + x9; x1 ^= (tsum << 13) | (tsum >> (32 - 13));
				tsum = x1 + x13; x5 ^= (tsum << 18) | (tsum >> (32 - 18));
				tsum = x10 + x6; x14 ^= (tsum << 7) | (tsum >> (32 - 7));
				tsum = x14 + x10; x2 ^= (tsum << 9) | (tsum >> (32 - 9));
				tsum = x2 + x14; x6 ^= (tsum << 13) | (tsum >> (32 - 13));
				tsum = x6 + x2; x10 ^= (tsum << 18) | (tsum >> (32 - 18));
				tsum = x15 + x11; x3 ^= (tsum << 7) | (tsum >> (32 - 7));
				tsum = x3 + x15; x7 ^= (tsum << 9) | (tsum >> (32 - 9));
				tsum = x7 + x3; x11 ^= (tsum << 13) | (tsum >> (32 - 13));
				tsum = x11 + x7; x15 ^= (tsum << 18) | (tsum >> (32 - 18));
				tsum = x0 + x3; x1 ^= (tsum << 7) | (tsum >> (32 - 7));
				tsum = x1 + x0; x2 ^= (tsum << 9) | (tsum >> (32 - 9));
				tsum = x2 + x1; x3 ^= (tsum << 13) | (tsum >> (32 - 13));
				tsum = x3 + x2; x0 ^= (tsum << 18) | (tsum >> (32 - 18));
				tsum = x5 + x4; x6 ^= (tsum << 7) | (tsum >> (32 - 7));
				tsum = x6 + x5; x7 ^= (tsum << 9) | (tsum >> (32 - 9));
				tsum = x7 + x6; x4 ^= (tsum << 13) | (tsum >> (32 - 13));
				tsum = x4 + x7; x5 ^= (tsum << 18) | (tsum >> (32 - 18));
				tsum = x10 + x9; x11 ^= (tsum << 7) | (tsum >> (32 - 7));
				tsum = x11 + x10; x8 ^= (tsum << 9) | (tsum >> (32 - 9));
				tsum = x8 + x11; x9 ^= (tsum << 13) | (tsum >> (32 - 13));
				tsum = x9 + x8; x10 ^= (tsum << 18) | (tsum >> (32 - 18));
				tsum = x15 + x14; x12 ^= (tsum << 7) | (tsum >> (32 - 7));
				tsum = x12 + x15; x13 ^= (tsum << 9) | (tsum >> (32 - 9));
				tsum = x13 + x12; x14 ^= (tsum << 13) | (tsum >> (32 - 13));
				tsum = x14 + x13; x15 ^= (tsum << 18) | (tsum >> (32 - 18));
			}

			x0 += j0;
			x1 += j1;
			x2 += j2;
			x3 += j3;
			x4 += j4;
			x5 += j5;
			x6 += j6;
			x7 += j7;
			x8 += j8;
			x9 += j9;
			x10 += j10;
			x11 += j11;
			x12 += j12;
			x13 += j13;
			x14 += j14;
			x15 += j15;

			store_littleendian(outv + 0, x0);
			store_littleendian(outv + 4, x1);
			store_littleendian(outv + 8, x2);
			store_littleendian(outv + 12, x3);
			store_littleendian(outv + 16, x4);
			store_littleendian(outv + 20, x5);
			store_littleendian(outv + 24, x6);
			store_littleendian(outv + 28, x7);
			store_littleendian(outv + 32, x8);
			store_littleendian(outv + 36, x9);
			store_littleendian(outv + 40, x10);
			store_littleendian(outv + 44, x11);
			store_littleendian(outv + 48, x12);
			store_littleendian(outv + 52, x13);
			store_littleendian(outv + 56, x14);
			store_littleendian(outv + 60, x15);
		}
	}
}