using System;
using System.Runtime.InteropServices;

namespace UCIS.NaCl {
	public static class Native {
		[DllImport("nacl")] public static unsafe extern int crypto_onetimeauth_poly1305(Byte* outv, Byte* inv, UInt64 inlen, Byte* k);
		[DllImport("nacl")] public static unsafe extern int crypto_core_salsa20(Byte* outv, Byte* inv, Byte* k, Byte* c);
		[DllImport("nacl")] public static unsafe extern int crypto_core_hsalsa20(Byte* outv, Byte* inv, Byte* k, Byte* c);
		[DllImport("nacl")] public static unsafe extern int crypto_box_curve25519xsalsa20poly1305_afternm(Byte* c, Byte* m, UInt64 mlen, Byte* n, Byte* k);
		[DllImport("nacl")] public static unsafe extern int crypto_box_curve25519xsalsa20poly1305_afternm(Byte[] c, Byte[] m, UInt64 mlen, Byte[] n, Byte[] k);
		[DllImport("nacl")] public static unsafe extern int crypto_box_curve25519xsalsa20poly1305_open_afternm(Byte* m, Byte* c, UInt64 clen, Byte* n, Byte* k);
		[DllImport("nacl")] public static unsafe extern int crypto_box_curve25519xsalsa20poly1305_open_afternm(Byte[] m, Byte[] c, UInt64 clen, Byte[] n, Byte[] k);

		public static Boolean EnableNativeImplementation() {
			//Todo: check if the library exists at all before probing for functions
			return
				UCIS.NaCl.crypto_onetimeauth.poly1305.EnableNativeImplementation() |
				UCIS.NaCl.crypto_core.salsa20.EnableNativeImplementation() |
				UCIS.NaCl.crypto_core.hsalsa20.EnableNativeImplementation() |
				false;
		}
	}
}
