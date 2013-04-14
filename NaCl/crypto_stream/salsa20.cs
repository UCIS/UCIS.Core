using System;

namespace UCIS.NaCl.crypto_stream {
	static unsafe class salsa20 {
		public const int KEYBYTES = 32;
		public const int NONCEBYTES = 8;

		static Byte[] sigma = new Byte[16] {(Byte)'e', (Byte)'x', (Byte)'p', (Byte)'a', //[16] = "expand 32-byte k";
											(Byte)'n', (Byte)'d', (Byte)' ', (Byte)'3',
											(Byte)'2', (Byte)'-', (Byte)'b', (Byte)'y',
											(Byte)'t', (Byte)'e', (Byte)' ', (Byte)'k', }; 

		public static void crypto_stream(Byte* c, int clen, Byte* n, Byte* k) {
			Byte[] inv = new Byte[16];
			Byte[] block = new Byte[64];
			if (clen == 0) return;

			for (int i = 0; i < 8; ++i) inv[i] = n[i];
			for (int i = 8; i < 16; ++i) inv[i] = 0;

			while (clen >= 64) {
				fixed (Byte* invp = inv, sigmap = sigma) crypto_core.salsa20.crypto_core(c, invp, k, sigmap);

				UInt32 u = 1;
				for (int i = 8; i < 16; ++i) {
					u += inv[i];
					inv[i] = (Byte)u;
					u >>= 8;
				}

				clen -= 64;
				c += 64;
			}

			if (clen != 0) {
				fixed (Byte* invp = inv, sigmap = sigma, blockp = block) crypto_core.salsa20.crypto_core(blockp, invp, k, sigmap);
				for (int i = 0; i < clen; ++i) c[i] = block[i];
			}
		}

		public static void crypto_stream_xor(Byte* c, Byte* m, int mlen, Byte* n, Byte* k) {
			Byte[] inv = new Byte[16];
			Byte[] block = new Byte[64];
			if (mlen == 0) return;

			for (int i = 0; i < 8; ++i) inv[i] = n[i];
			for (int i = 8; i < 16; ++i) inv[i] = 0;

			while (mlen >= 64) {
				fixed (Byte* invp = inv, sigmap = sigma, blockp = block) crypto_core.salsa20.crypto_core(blockp, invp, k, sigmap);
				for (int i = 0; i < 64; ++i) c[i] = (Byte)(m[i] ^ block[i]);

				UInt32 u = 1;
				for (int i = 8; i < 16; ++i) {
					u += inv[i];
					inv[i] = (Byte)u;
					u >>= 8;
				}

				mlen -= 64;
				c += 64;
				m += 64;
			}

			if (mlen != 0) {
				fixed (Byte* invp = inv, sigmap = sigma, blockp = block) crypto_core.salsa20.crypto_core(blockp, invp, k, sigmap);
				for (int i = 0; i < mlen; ++i) c[i] = (Byte)(m[i] ^ block[i]);
			}
		}

		internal static void crypto_stream_xor_split(Byte* mcpad, int padbytes, Byte* c, Byte* m, int mlen, Byte* n, Byte* k) {
			Byte* inv = stackalloc Byte[16];
			Byte* block = stackalloc Byte[64];

			for (int i = 0; i < 8; ++i) inv[i] = n[i];
			for (int i = 8; i < 16; ++i) inv[i] = 0;

			if (padbytes > 0) {
				if (padbytes > 64) throw new ArgumentOutOfRangeException("padbytes");
				crypto_core.salsa20.crypto_core(block, inv, k, sigma);
				if (mcpad != null) for (int i = 0; i < padbytes; ++i) mcpad[i] ^= block[i];
				if (mlen > 0) {
					int bleft = 64 - padbytes;
					if (bleft > mlen) bleft = mlen;
					for (int i = 0; i < bleft; ++i) c[i] = (Byte)(m[i] ^ block[i + padbytes]);
					c += bleft;
					m += bleft;
					mlen -= bleft;
					if (mlen >= 0) {
						UInt32 u = 1;
						for (int i = 8; i < 16; ++i) {
							u += inv[i];
							inv[i] = (Byte)u;
							u >>= 8;
						}
					}
				}
			}

			while (mlen >= 64) {
				crypto_core.salsa20.crypto_core(block, inv, k, sigma);
				//for (int i = 0; i < 64; ++i) c[i] = (Byte)(m[i] ^ block[i]);
				for (int i = 0; i < 64; i += 4) *(int*)(c + i) = *(int*)(m + i) ^ *(int*)(block + i);

				UInt32 u = 1;
				for (int i = 8; i < 16; ++i) {
					u += inv[i];
					inv[i] = (Byte)u;
					u >>= 8;
				}

				mlen -= 64;
				c += 64;
				m += 64;
			}

			if (mlen > 0) {
				crypto_core.salsa20.crypto_core(block, inv, k, sigma);
				for (int i = 0; i < mlen; ++i) c[i] = (Byte)(m[i] ^ block[i]);
			}
		}
	}
}