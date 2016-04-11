using System;

namespace UCIS.NaCl.crypto_hash {
	class sha1 {
		public static int BYTES = 20;

		public unsafe struct sha1state {
			fixed UInt32 state[5];
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
					statep[4] = 0xc3d2e1f0;
				}
			}

			public unsafe void process(Byte* inp, int inlen) {
				length += inlen;
				fixed (sha1state* pthis = &this) {
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
				fixed (sha1state* s = &this) {
					s->input[offset++] = 0x80;
					if (offset > 56) {
						for (int i = offset; i < 64; i++) s->input[i] = 0;
						process_block(s->state, s->input);
						offset = 0;
					}
					for (int i = offset; i < 56; i++) s->input[i] = 0;
					UInt64 bits = (UInt64)length << 3;
					s->input[56] = (Byte)(bits >> 56);
					s->input[57] = (Byte)(bits >> 48);
					s->input[58] = (Byte)(bits >> 40);
					s->input[59] = (Byte)(bits >> 32);
					s->input[60] = (Byte)(bits >> 24);
					s->input[61] = (Byte)(bits >> 16);
					s->input[62] = (Byte)(bits >> 8);
					s->input[63] = (Byte)bits;
					process_block(s->state, s->input);

					for (uint i = 0, j = 0; j < 20; i++, j += 4) {
						outp[j] = (byte)(s->state[i] >> 24);
						outp[j + 1] = (byte)(s->state[i] >> 16);
						outp[j + 2] = (byte)(s->state[i] >> 8);
						outp[j + 3] = (byte)(s->state[i] >> 0);
					}
				}
			}

			private static void process_block(uint* state, byte* block) {
				uint a = state[0], b = state[1], c = state[2], d = state[3], e = state[4];

				uint* expandedBuffer = stackalloc uint[80];
				for (uint k = 0, j = 0; j < 64; k++, j += 4) expandedBuffer[k] = (uint)((block[j] << 24) | (block[j + 1] << 16) | (block[j + 2] << 8) | (block[j + 3] << 0));

				int i;

				for (i = 16; i < 80; i++) {
					uint tmp = expandedBuffer[i - 3] ^ expandedBuffer[i - 8] ^ expandedBuffer[i - 14] ^ expandedBuffer[i - 16];
					expandedBuffer[i] = (tmp << 1) | (tmp >> 31);
				}

				/* Round 1 */
				for (i = 0; i < 20; i += 5) {
	                e += ((a << 5) | (a >> 27)) + (d ^ (b & (c ^ d))) + expandedBuffer[i + 0] + 0x5a827999; b = ((b << 30) | (b >> 2));
					d += ((e << 5) | (e >> 27)) + (c ^ (a & (b ^ c))) + expandedBuffer[i + 1] + 0x5a827999; a = ((a << 30) | (a >> 2));
					c += ((d << 5) | (d >> 27)) + (b ^ (e & (a ^ b))) + expandedBuffer[i + 2] + 0x5a827999; e = ((e << 30) | (e >> 2));
					b += ((c << 5) | (c >> 27)) + (a ^ (d & (e ^ a))) + expandedBuffer[i + 3] + 0x5a827999; d = ((d << 30) | (d >> 2));
					a += ((b << 5) | (b >> 27)) + (e ^ (c & (d ^ e))) + expandedBuffer[i + 4] + 0x5a827999; c = ((c << 30) | (c >> 2));
				}

				/* Round 2 */
				for (; i < 40; i += 5) {
				    e += ((a << 5) | (a >> 27)) + (b ^ c ^ d) + expandedBuffer[i + 0] + 0x6ed9eba1; b = ((b << 30) | (b >> 2));
					d += ((e << 5) | (e >> 27)) + (a ^ b ^ c) + expandedBuffer[i + 1] + 0x6ed9eba1; a = ((a << 30) | (a >> 2));
					c += ((d << 5) | (d >> 27)) + (e ^ a ^ b) + expandedBuffer[i + 2] + 0x6ed9eba1; e = ((e << 30) | (e >> 2));
					b += ((c << 5) | (c >> 27)) + (d ^ e ^ a) + expandedBuffer[i + 3] + 0x6ed9eba1; d = ((d << 30) | (d >> 2));
					a += ((b << 5) | (b >> 27)) + (c ^ d ^ e) + expandedBuffer[i + 4] + 0x6ed9eba1; c = ((c << 30) | (c >> 2));
				}

				/* Round 3 */
				for (; i < 60; i += 5) {
					e += ((a << 5) | (a >> 27)) + ((b & c) | (d & (b | c))) + expandedBuffer[i + 0] + 0x8f1bbcdc; b = (b << 30) | (b >> 2);
					d += ((e << 5) | (e >> 27)) + ((a & b) | (c & (a | b))) + expandedBuffer[i + 1] + 0x8f1bbcdc; a = (a << 30) | (a >> 2);
					c += ((d << 5) | (d >> 27)) + ((e & a) | (b & (e | a))) + expandedBuffer[i + 2] + 0x8f1bbcdc; e = (e << 30) | (e >> 2);
					b += ((c << 5) | (c >> 27)) + ((d & e) | (a & (d | e))) + expandedBuffer[i + 3] + 0x8f1bbcdc; d = (d << 30) | (d >> 2);
					a += ((b << 5) | (b >> 27)) + ((c & d) | (e & (c | d))) + expandedBuffer[i + 4] + 0x8f1bbcdc; c = (c << 30) | (c >> 2);
				}

				/* Round 4 */
				for (; i < 80; i += 5) {
					e += ((a << 5) | (a >> 27)) + (b ^ c ^ d) + expandedBuffer[i + 0] + 0xca62c1d6; b = (b << 30) | (b >> 2);
					d += ((e << 5) | (e >> 27)) + (a ^ b ^ c) + expandedBuffer[i + 1] + 0xca62c1d6; a = (a << 30) | (a >> 2);
					c += ((d << 5) | (d >> 27)) + (e ^ a ^ b) + expandedBuffer[i + 2] + 0xca62c1d6; e = (e << 30) | (e >> 2);
					b += ((c << 5) | (c >> 27)) + (d ^ e ^ a) + expandedBuffer[i + 3] + 0xca62c1d6; d = (d << 30) | (d >> 2);
					a += ((b << 5) | (b >> 27)) + (c ^ d ^ e) + expandedBuffer[i + 4] + 0xca62c1d6; c = (c << 30) | (c >> 2);
				}

				state[0] += a;
				state[1] += b;
				state[2] += c;
				state[3] += d;
				state[4] += e;
			}
		}
	}
}
