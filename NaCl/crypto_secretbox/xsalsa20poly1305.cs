using System;

namespace UCIS.NaCl.crypto_secretbox {
	unsafe static class xsalsa20poly1305 {
		public const int KEYBYTES = 32;
		public const int NONCEBYTES = 24;
		public const int ZEROBYTES = 32;
		public const int BOXZEROBYTES = 16;

		static public int crypto_secretbox(Byte* c, Byte* m, UInt64 mlen, Byte* n, Byte* k) {
			if (mlen < 32) return -1;
			crypto_stream.xsalsa20.crypto_stream_xor(c, m, mlen, n, k);
			crypto_onetimeauth.poly1305.crypto_onetimeauth(c + 16, c + 32, mlen - 32, c);
			for (int i = 0; i < 16; ++i) c[i] = 0;
			return 0;
		}

		static public int crypto_secretbox_open(Byte* m, Byte* c, UInt64 clen, Byte* n, Byte* k) {
			if (clen < 32) return -1;
			Byte[] subkey = new Byte[32];
			fixed (Byte* subkeyp = subkey) {
				crypto_stream.xsalsa20.crypto_stream(subkeyp, 32, n, k);
				if (crypto_onetimeauth.poly1305.crypto_onetimeauth_verify(c + 16, c + 32, clen - 32, subkeyp) != 0) return -1;
			}
			crypto_stream.xsalsa20.crypto_stream_xor(m, c, clen, n, k);
			for (int i = 0; i < 32; ++i) m[i] = 0;
			return 0;
		}

		static internal int crypto_secretbox_nopad(Byte* c, Byte* m, UInt64 mlen, Byte* n, Byte* k) {
			if (mlen < 0) return -1;
			Byte* mc32 = stackalloc Byte[32];
			for (int i = 0; i < 32; i += 4) *(int*)(mc32 + i) = 0;
			crypto_stream.xsalsa20.crypto_stream_xor_split(mc32, 32, c + 16, m, mlen, n, k);
			crypto_onetimeauth.poly1305.crypto_onetimeauth(mc32 + 16, c + 16, mlen, mc32);
			for (int i = 0; i < 16; ++i) c[i] = mc32[i + 16];
			return 0;
		}

		static internal Boolean crypto_secretbox_verify(Byte* c, UInt64 clen, Byte* n, Byte* k) {
			if (clen < 16) return false;
			Byte* subkey = stackalloc Byte[32];
			for (int i = 0; i < 32; i += 4) *(int*)(subkey + i) = 0;
			crypto_stream.xsalsa20.crypto_stream(subkey, 32, n, k);
			return crypto_onetimeauth.poly1305.crypto_onetimeauth_verify(c, c + 16, clen - 16, subkey) == 0;
		}

		static internal int crypto_secretbox_open_nopad(Byte* m, Byte* c, UInt64 clen, Byte* n, Byte* k) {
			if (!crypto_secretbox_verify(c, clen, n, k)) return -1;
			if (clen < 16) return -1;
			Byte* mc32 = stackalloc Byte[32];
			for (int i = 0; i < 16; i += 4) *(int*)(mc32 + i) = 0;
			for (int i = 0; i < 16; i += 4) *(int*)(mc32 + i + 16) = *(int*)(c + i);
			crypto_stream.xsalsa20.crypto_stream_xor_split(mc32, 32, m, c + 16, clen - 16, n, k);
			return 0;
		}

		static internal int crypto_secretbox_inplace_nopad(Byte* c, UInt64 mlen, Byte* n, Byte* k) {
			if (mlen < 0) return -1;
			Byte* mc16 = stackalloc Byte[16];
			for (int i = 0; i < 16; i += 4) *(int*)(mc16 + i) = 0;
			crypto_stream.xsalsa20.crypto_stream_xor_split(mc16, 16, c, c, mlen, n, k);
			crypto_onetimeauth.poly1305.crypto_onetimeauth(c, c + 16, mlen, mc16);
			return 0;
		}

		static internal int crypto_secretbox_open_inplace_nopad(Byte* c, UInt64 clen, Byte* n, Byte* k) {
			if (clen < 16) return -1;
			Byte* subkey = stackalloc Byte[32];
			for (int i = 0; i < 32; i += 4) *(int*)(subkey + i) = 0;
			crypto_stream.xsalsa20.crypto_stream(subkey, 32, n, k);
			if (crypto_onetimeauth.poly1305.crypto_onetimeauth_verify(c, c + 16, clen - 16, subkey) != 0) return -1;
			crypto_stream.xsalsa20.crypto_stream_xor_split(null, 16, c, c, clen, n, k);
			return 0;
		}
	}
}