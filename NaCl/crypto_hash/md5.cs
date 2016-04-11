using System;

namespace UCIS.NaCl.crypto_hash {
	class md5 {
		public static int BYTES = 16;

		public unsafe struct md5state {
			fixed UInt32 state[4];
			fixed Byte input[64];
			int offset;
			int length;

			public unsafe void init() {
				offset = length = 0;
				fixed (UInt32* statep = state) {
					statep[0] = 0x67452301;
					statep[1] = 0xefcdab89;
					statep[2] = 0x98badcfe;
					statep[3] = 0x10325476;
				}
			}

			private static uint F(uint x, uint y, uint z) { return ((x & y) | ((~x) & z)); }
			private static uint G(uint x, uint y, uint z) { return ((x & z) | (y & (~z))); }
			private static uint H(uint x, uint y, uint z) { return x ^ y ^ z; }
			private static uint I(uint x, uint y, uint z) { return y ^ (x | (~z)); }
			private static uint ROTATE_LEFT(uint x, int n) { return (x << n) | (x >> (32 - n)); }
			private static void FF(ref uint a, uint b, uint c, uint d, uint x, int s, uint ac) {
				(a) += F(b, c, d) + x + ac;
				(a) = ROTATE_LEFT(a, s);
				(a) += b;
			}
			private static void GG(ref uint a, uint b, uint c, uint d, uint x, int s, uint ac) {
				(a) += G(b, c, d) + x + ac;
				(a) = ROTATE_LEFT(a, s);
				(a) += b;
			}
			private static void HH(ref uint a, uint b, uint c, uint d, uint x, int s, uint ac) {
				(a) += H(b, c, d) + x + ac;
				(a) = ROTATE_LEFT(a, s);
				(a) += b;
			}
			private static void II(ref uint a, uint b, uint c, uint d, uint x, int s, uint ac) {
				(a) += I(b, c, d) + x + ac;
				(a) = ROTATE_LEFT(a, s);
				(a) += b;
			}

			public unsafe void process(Byte* inp, int inlen) {
				length += inlen;
				fixed (md5state* pthis = &this) {
					for (; offset > 0 && offset < 64 && inlen > 0; inlen--) pthis->input[offset++] = *inp++;
					if (offset == 64) {
						process_block(pthis->state, pthis->input);
						offset = 0;
					}
					while (inlen >= 64) {
						process_block(pthis->state, inp);
						inp += 64;
						inlen -= 64;
					}
					for (int i = 0; i < inlen; i++) pthis->input[offset++] = *inp++;
				}
			}

			public unsafe void finish(Byte* outp) {
				fixed (md5state* s = &this) {
					s->input[offset++] = 0x80;
					if (offset > 56) {
						for (int i = offset; i < 64; i++) s->input[i] = 0;
						process_block(s->state, s->input);
						offset = 0;
					}
					for (int i = offset; i < 56; i++) s->input[i] = 0;
					UInt64 bits = (UInt64)length << 3;
					s->input[56] = (Byte)(bits >> 0);
					s->input[57] = (Byte)(bits >> 8);
					s->input[58] = (Byte)(bits >> 16);
					s->input[59] = (Byte)(bits >> 24);
					s->input[60] = (Byte)(bits >> 32);
					s->input[61] = (Byte)(bits >> 40);
					s->input[62] = (Byte)(bits >> 48);
					s->input[63] = (Byte)(bits >> 56);
					process_block(s->state, s->input);

					for (uint i = 0, j = 0; j < 16; i++, j += 4) {
						outp[j] = (byte)s->state[i];
						outp[j + 1] = (byte)(s->state[i] >> 8);
						outp[j + 2] = (byte)(s->state[i] >> 16);
						outp[j + 3] = (byte)(s->state[i] >> 24);
					}
				}
			}

			private static void process_block(uint* state, byte* block) {
				uint a = state[0], b = state[1], c = state[2], d = state[3];
				uint* x = stackalloc uint[16];

				for (uint i = 0, j = 0; j < 64; i++, j += 4) x[i] = (uint)(block[j] | (block[j + 1] << 8) | (block[j + 2] << 16) | (block[j + 3] << 24));

				const int S11 = 7, S12 = 12, S13 = 17, S14 = 22, S21 = 5, S22 = 9, S23 = 14, S24 = 20, S31 = 4, S32 = 11, S33 = 16, S34 = 23, S41 = 6, S42 = 10, S43 = 15, S44 = 21;

				/* Round 1 */
				FF(ref a, b, c, d, x[0], S11, 0xd76aa478); /* 1 */
				FF(ref d, a, b, c, x[1], S12, 0xe8c7b756); /* 2 */
				FF(ref c, d, a, b, x[2], S13, 0x242070db); /* 3 */
				FF(ref b, c, d, a, x[3], S14, 0xc1bdceee); /* 4 */
				FF(ref a, b, c, d, x[4], S11, 0xf57c0faf); /* 5 */
				FF(ref d, a, b, c, x[5], S12, 0x4787c62a); /* 6 */
				FF(ref c, d, a, b, x[6], S13, 0xa8304613); /* 7 */
				FF(ref b, c, d, a, x[7], S14, 0xfd469501); /* 8 */
				FF(ref a, b, c, d, x[8], S11, 0x698098d8); /* 9 */
				FF(ref d, a, b, c, x[9], S12, 0x8b44f7af); /* 10 */
				FF(ref c, d, a, b, x[10], S13, 0xffff5bb1); /* 11 */
				FF(ref b, c, d, a, x[11], S14, 0x895cd7be); /* 12 */
				FF(ref a, b, c, d, x[12], S11, 0x6b901122); /* 13 */
				FF(ref d, a, b, c, x[13], S12, 0xfd987193); /* 14 */
				FF(ref c, d, a, b, x[14], S13, 0xa679438e); /* 15 */
				FF(ref b, c, d, a, x[15], S14, 0x49b40821); /* 16 */

				/* Round 2 */
				GG(ref a, b, c, d, x[1], S21, 0xf61e2562); /* 17 */
				GG(ref d, a, b, c, x[6], S22, 0xc040b340); /* 18 */
				GG(ref c, d, a, b, x[11], S23, 0x265e5a51); /* 19 */
				GG(ref b, c, d, a, x[0], S24, 0xe9b6c7aa); /* 20 */
				GG(ref a, b, c, d, x[5], S21, 0xd62f105d); /* 21 */
				GG(ref d, a, b, c, x[10], S22, 0x02441453); /* 22 */
				GG(ref c, d, a, b, x[15], S23, 0xd8a1e681); /* 23 */
				GG(ref b, c, d, a, x[4], S24, 0xe7d3fbc8); /* 24 */
				GG(ref a, b, c, d, x[9], S21, 0x21e1cde6); /* 25 */
				GG(ref d, a, b, c, x[14], S22, 0xc33707d6); /* 26 */
				GG(ref c, d, a, b, x[3], S23, 0xf4d50d87); /* 27 */
				GG(ref b, c, d, a, x[8], S24, 0x455a14ed); /* 28 */
				GG(ref a, b, c, d, x[13], S21, 0xa9e3e905); /* 29 */
				GG(ref d, a, b, c, x[2], S22, 0xfcefa3f8); /* 30 */
				GG(ref c, d, a, b, x[7], S23, 0x676f02d9); /* 31 */
				GG(ref b, c, d, a, x[12], S24, 0x8d2a4c8a); /* 32 */

				/* Round 3 */
				HH(ref a, b, c, d, x[5], S31, 0xfffa3942); /* 33 */
				HH(ref d, a, b, c, x[8], S32, 0x8771f681); /* 34 */
				HH(ref c, d, a, b, x[11], S33, 0x6d9d6122); /* 35 */
				HH(ref b, c, d, a, x[14], S34, 0xfde5380c); /* 36 */
				HH(ref a, b, c, d, x[1], S31, 0xa4beea44); /* 37 */
				HH(ref d, a, b, c, x[4], S32, 0x4bdecfa9); /* 38 */
				HH(ref c, d, a, b, x[7], S33, 0xf6bb4b60); /* 39 */
				HH(ref b, c, d, a, x[10], S34, 0xbebfbc70); /* 40 */
				HH(ref a, b, c, d, x[13], S31, 0x289b7ec6); /* 41 */
				HH(ref d, a, b, c, x[0], S32, 0xeaa127fa); /* 42 */
				HH(ref c, d, a, b, x[3], S33, 0xd4ef3085); /* 43 */
				HH(ref b, c, d, a, x[6], S34, 0x04881d05); /* 44 */
				HH(ref a, b, c, d, x[9], S31, 0xd9d4d039); /* 45 */
				HH(ref d, a, b, c, x[12], S32, 0xe6db99e5); /* 46 */
				HH(ref c, d, a, b, x[15], S33, 0x1fa27cf8); /* 47 */
				HH(ref b, c, d, a, x[2], S34, 0xc4ac5665); /* 48 */

				/* Round 4 */
				II(ref a, b, c, d, x[0], S41, 0xf4292244); /* 49 */
				II(ref d, a, b, c, x[7], S42, 0x432aff97); /* 50 */
				II(ref c, d, a, b, x[14], S43, 0xab9423a7); /* 51 */
				II(ref b, c, d, a, x[5], S44, 0xfc93a039); /* 52 */
				II(ref a, b, c, d, x[12], S41, 0x655b59c3); /* 53 */
				II(ref d, a, b, c, x[3], S42, 0x8f0ccc92); /* 54 */
				II(ref c, d, a, b, x[10], S43, 0xffeff47d); /* 55 */
				II(ref b, c, d, a, x[1], S44, 0x85845dd1); /* 56 */
				II(ref a, b, c, d, x[8], S41, 0x6fa87e4f); /* 57 */
				II(ref d, a, b, c, x[15], S42, 0xfe2ce6e0); /* 58 */
				II(ref c, d, a, b, x[6], S43, 0xa3014314); /* 59 */
				II(ref b, c, d, a, x[13], S44, 0x4e0811a1); /* 60 */
				II(ref a, b, c, d, x[4], S41, 0xf7537e82); /* 61 */
				II(ref d, a, b, c, x[11], S42, 0xbd3af235); /* 62 */
				II(ref c, d, a, b, x[2], S43, 0x2ad7d2bb); /* 63 */
				II(ref b, c, d, a, x[9], S44, 0xeb86d391); /* 64 */

				state[0] += a;
				state[1] += b;
				state[2] += c;
				state[3] += d;
			}
		}
	}
}
