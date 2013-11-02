using System;

namespace UCIS.NaCl.crypto_hash {
	public static class sha512 {
		public static int BYTES = 64;

		public static unsafe void crypto_hash(Byte[] outv, Byte[] inv, int inlen) {
			if (outv.Length < 64) throw new ArgumentException("outv.Length < 64");
			if (inv.Length < inlen) throw new ArgumentException("inv.Length < inlen");
			fixed (Byte* outp = outv, inp = inv) crypto_hash(outp, inp, (UInt64)inlen);
		}
		public static unsafe void crypto_hash(Byte* outp, Byte* inp, UInt64 inlen) {
			sha512state state = new sha512state();
			state.init();
			state.process(inp, (int)inlen);
			state.finish(outp);
		}

		public unsafe struct sha512state {
			fixed UInt64 state[8];
			fixed Byte input[128];
			int offset;
			int length;

			public unsafe void init() {
				fixed (UInt64* s = state) {
					s[0] = 0x6a09e667f3bcc908; s[1] = 0xbb67ae8584caa73b; s[2] = 0x3c6ef372fe94f82b; s[3] = 0xa54ff53a5f1d36f1;
					s[4] = 0x510e527fade682d1; s[5] = 0x9b05688c2b3e6c1f; s[6] = 0x1f83d9abfb41bd6b; s[7] = 0x5be0cd19137e2179;
				}
				offset = 0;
				length = 0;
			}
			public unsafe void process(Byte* inp, int inlen) {
				fixed (sha512state* pthis = &this) {
					length += inlen;
					if (offset > 0) {
						int blen = 128 - offset;
						if (blen > inlen) blen = inlen;
						for (int i = 0; i < blen; i++) pthis->input[offset++] = *inp++;
						inlen -= blen;
					}
					if (offset == 128) {
						crypto_hashblocks.sha512.crypto_hashblocks(pthis->state, pthis->input, (UInt64)offset);
						offset = 0;
					}
					if (inlen >= 128) {
						crypto_hashblocks.sha512.crypto_hashblocks(pthis->state, inp, (UInt64)inlen);
						inp += inlen;
						inlen &= 127;
						inp -= inlen;
					}
					if (inlen > 0) {
						for (int i = 0; i < inlen; i++) pthis->input[offset++] = *inp++;
					}
				}
			}
			public unsafe void finish(Byte* outp) {
				fixed (sha512state* s = &this) {
					s->input[offset++] = 0x80;
					if (offset > 112) {
						for (int i = offset; i < 128; i++) s->input[i] = 0;
						crypto_hashblocks.sha512.crypto_hashblocks(s->state, s->input, 128);
						offset = 0;
					}
					for (int i = offset; i < 119; i++) s->input[i] = 0;
					UInt64 bytes = (UInt64)length;
					s->input[119] = (Byte)(bytes >> 61);
					s->input[120] = (Byte)(bytes >> 53);
					s->input[121] = (Byte)(bytes >> 45);
					s->input[122] = (Byte)(bytes >> 37);
					s->input[123] = (Byte)(bytes >> 29);
					s->input[124] = (Byte)(bytes >> 21);
					s->input[125] = (Byte)(bytes >> 13);
					s->input[126] = (Byte)(bytes >> 5);
					s->input[127] = (Byte)(bytes << 3);
					crypto_hashblocks.sha512.crypto_hashblocks(s->state, s->input, 128);
					crypto_hashblocks.sha512.crypto_hashblocks_state_pack(outp, s->state);
				}
			}
		}
	}
}
