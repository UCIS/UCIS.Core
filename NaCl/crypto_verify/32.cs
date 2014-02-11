using System;

namespace UCIS.NaCl.crypto_verify {
	unsafe static class _32 {
		const int BYTES = 32;
		public static int crypto_verify(Byte* x, Byte* y) {
			Int32 differentbits = 0;
			for (int i = 0; i < 32; i++) differentbits |= x[i] ^ y[i];
			return (1 & ((differentbits - 1) >> 8)) - 1;
		}
	}
}