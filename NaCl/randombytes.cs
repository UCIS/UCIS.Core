using System;
using System.Security.Cryptography;

namespace UCIS.NaCl {
	public static class randombytes {
		public static void generate(Byte[] x) {
			RNGCryptoServiceProvider rnd = new RNGCryptoServiceProvider();
			rnd.GetBytes(x);
		}
		public static Byte[] generate(int count) {
			Byte[] bytes = new Byte[count];
			generate(bytes);
			return bytes;
		}
	}
}