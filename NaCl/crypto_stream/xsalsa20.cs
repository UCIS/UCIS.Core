using System;

namespace UCIS.NaCl.crypto_stream {
	static unsafe class xsalsa20 {
		public const int KEYBYTES = 32;
		public const int NONCEBYTES = 24;

		//Never written to
		static Byte[] sigma = new Byte[16] {(Byte)'e', (Byte)'x', (Byte)'p', (Byte)'a', //[16] = "expand 32-byte k";
											(Byte)'n', (Byte)'d', (Byte)' ', (Byte)'3',
											(Byte)'2', (Byte)'-', (Byte)'b', (Byte)'y',
											(Byte)'t', (Byte)'e', (Byte)' ', (Byte)'k', };

		public static void crypto_stream(Byte* c, int clen, Byte* n, Byte* k) {
			Byte* subkey = stackalloc Byte[32];
			crypto_core.hsalsa20.crypto_core(subkey, n, k, sigma);
			salsa20.crypto_stream(c, clen, n + 16, subkey);
		}

		public static void crypto_stream_xor(Byte* c, Byte* m, UInt64 mlen, Byte* n, Byte* k) {
			Byte* subkey = stackalloc Byte[32];
			crypto_core.hsalsa20.crypto_core(subkey, n, k, sigma);
			salsa20.crypto_stream_xor(c, m, (int)mlen, n + 16, subkey);
		}

		internal static void crypto_stream_xor_split(Byte* mcpad, int padbytes, Byte* c, Byte* m, UInt64 mlen, Byte* n, Byte* k) {
			Byte* subkey = stackalloc Byte[32];
			crypto_core.hsalsa20.crypto_core(subkey, n, k, sigma);
			salsa20.crypto_stream_xor_split(mcpad, padbytes, c, m, (int)mlen, n + 16, subkey);
		}
	}
}