using System;

namespace UCIS.NaCl.crypto_hash {
	public static class sha256 {
		public static int BYTES = 32;

		public static unsafe void crypto_hash(Byte[] outv, Byte[] inv, int inlen) {
			if (outv.Length < 32) throw new ArgumentException("outv.Length < 32");
			if (inv.Length < inlen) throw new ArgumentException("inv.Length < inlen");
			fixed (Byte* outp = outv, inp = inv) crypto_hash(outp, inp, (UInt64)inlen);
		}
		public static unsafe void crypto_hash(Byte* outp, Byte* inp, UInt64 inlen) {
			sha256state state = new sha256state();
			state.init();
			state.process(inp, (int)inlen);
			state.finish(outp);
		}

		public unsafe struct sha256state {
			fixed UInt32 state[8];
			fixed Byte input[64];
			int offset;
			int length;

			public unsafe void init() {
				fixed (UInt32* s = state) {
					s[0] = 0x6a09e667; s[1] = 0xbb67ae85; s[2] = 0x3c6ef372; s[3] = 0xa54ff53a;
					s[4] = 0x510e527f; s[5] = 0x9b05688c; s[6] = 0x1f83d9ab; s[7] = 0x5be0cd19;
				}
				offset = 0;
				length = 0;
			}
			public unsafe void process(Byte* inp, int inlen) {
				fixed (sha256state* pthis = &this) {
					length += inlen;
					if (offset > 0) {
						int blen = 64 - offset;
						if (blen > inlen) blen = inlen;
						for (int i = 0; i < blen; i++) pthis->input[offset++] = *inp++;
						inlen -= blen;
					}
					if (offset == 64) {
						crypto_hashblocks.sha256.crypto_hashblocks(pthis->state, pthis->input, (UInt64)offset);
						offset = 0;
					}
					if (inlen >= 64) {
						crypto_hashblocks.sha256.crypto_hashblocks(pthis->state, inp, (UInt64)inlen);
						inp += inlen;
						inlen &= 63;
						inp -= inlen;
					}
					if (inlen > 0) {
						for (int i = 0; i < inlen; i++) pthis->input[offset++] = *inp++;
					}
				}
			}
			public unsafe void finish(Byte* outp) {
				fixed (sha256state* s = &this) {
					s->input[offset++] = 0x80;
					if (offset > 56) {
						for (int i = offset; i < 64; i++) s->input[i] = 0;
						crypto_hashblocks.sha256.crypto_hashblocks(s->state, s->input, 64);
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
					crypto_hashblocks.sha256.crypto_hashblocks(s->state, s->input, 64);
					crypto_hashblocks.sha256.crypto_hashblocks_state_pack(outp, s->state);
				}
			}
		}
	}
}
