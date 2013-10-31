using System;

namespace UCIS.NaCl.crypto_sign {
	public static class edwards25519sha512batch {
		public const int SECRETKEYBYTES = 64;
		public const int PUBLICKEYBYTES = 32;
		public const int CRYPTO_BYTES = 64;

		/*Arithmetic modulo the group order n = 2^252 +  27742317777372353535851937790883648493 = 7237005577332262213973186563042994240857116359379907606001950938285454250989 */

		unsafe struct sc25519 {
			public fixed UInt32 v[32]; //crypto_uint32 v[32];

			static UInt32[] m = new UInt32[32] {0xED, 0xD3, 0xF5, 0x5C, 0x1A, 0x63, 0x12, 0x58, 0xD6, 0x9C, 0xF7, 0xA2, 0xDE, 0xF9, 0xDE, 0x14, 
                                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x10};

			static UInt32[] mu = new UInt32[33] {0x1B, 0x13, 0x2C, 0x0A, 0xA3, 0xE5, 0x9C, 0xED, 0xA7, 0x29, 0x63, 0x08, 0x5D, 0x21, 0x06, 0x21, 
                                     0xEB, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F};

			/* Reduce coefficients of r before calling reduce_add_sub */
			static unsafe void reduce_add_sub(sc25519* r) {
				int i, b = 0, pb = 0, nb;
				Byte* t = stackalloc Byte[32];

				for (i = 0; i < 32; i++) {
					b = (r->v[i] < pb + m[i]) ? 1 : 0;
					t[i] = (Byte)(r->v[i] - pb - m[i] + b * 256);
					pb = b;
				}
				nb = 1 - b;
				for (i = 0; i < 32; i++) r->v[i] = (uint)(r->v[i] * b + t[i] * nb);
			}

			/* Reduce coefficients of x before calling barrett_reduce */
			static unsafe void barrett_reduce(sc25519* r, UInt32* x) { // const crypto_uint32 x[64]
				/* See HAC, Alg. 14.42 */
				UInt32* q2 = stackalloc UInt32[66]; // { 0 };
				for (int z = 0; z < 66; z++) q2[z] = 0;
				UInt32* q3 = q2 + 33;
				UInt32* r1 = stackalloc UInt32[33];
				UInt32* r2 = stackalloc UInt32[33]; // { 0 };
				for (int z = 0; z < 33; z++) r2[z] = 0;
				UInt32 carry;
				int b, pb = 0;

				for (int i = 0; i < 33; i++)
					for (int j = 0; j < 33; j++)
						if (i + j >= 31) q2[i + j] += mu[i] * x[j + 31];
				carry = q2[31] >> 8;
				q2[32] += carry;
				carry = q2[32] >> 8;
				q2[33] += carry;

				for (int i = 0; i < 33; i++) r1[i] = x[i];
				for (int i = 0; i < 32; i++)
					for (int j = 0; j < 33; j++)
						if (i + j < 33) r2[i + j] += m[i] * q3[j];

				for (int i = 0; i < 32; i++) {
					carry = r2[i] >> 8;
					r2[i + 1] += carry;
					r2[i] &= 0xff;
				}

				for (int i = 0; i < 32; i++) {
					b = (r1[i] < pb + r2[i]) ? 1 : 0;
					r->v[i] = (uint)(r1[i] - pb - r2[i] + b * 256);
					pb = b;
				}

				/* XXX: Can it really happen that r<0?, See HAC, Alg 14.42, Step 3 
				 * If so: Handle  it here!
				 */

				reduce_add_sub(r);
				reduce_add_sub(r);
			}

			/*
			static int iszero(const sc25519 *x)
			{
			  // Implement
			  return 0;
			}
			*/

			public static unsafe void sc25519_from32bytes(sc25519* r, Byte* x) { //const unsigned char x[32]
				UInt32* t = stackalloc UInt32[64]; // { 0 };
				for (int i = 0; i < 32; i++) t[i] = x[i];
				for (int i = 32; i < 64; i++) t[i] = 0;
				barrett_reduce(r, t);
			}

			public static unsafe void sc25519_from64bytes(sc25519* r, Byte* x) { //const unsigned char x[64]
				UInt32* t = stackalloc UInt32[64]; // { 0 };
				for (int i = 0; i < 64; i++) t[i] = x[i];
				barrett_reduce(r, t);
			}

			/* XXX: What we actually want for crypto_group is probably just something like
			 * void sc25519_frombytes(sc25519 *r, const unsigned char *x, size_t xlen)
			 */

			public static unsafe void sc25519_to32bytes(Byte* r, sc25519* x) { //unsigned char r[32]
				for (int i = 0; i < 32; i++) r[i] = (Byte)x->v[i];
			}

			public static unsafe void sc25519_add(sc25519* r, sc25519* x, sc25519* y) {
				for (int i = 0; i < 32; i++) r->v[i] = x->v[i] + y->v[i];
				for (int i = 0; i < 31; i++) {
					uint carry = r->v[i] >> 8;
					r->v[i + 1] += carry;
					r->v[i] &= 0xff;
				}
				reduce_add_sub(r);
			}

			public static unsafe void sc25519_mul(sc25519* r, sc25519* x, sc25519* y) {
				UInt32* t = stackalloc UInt32[64];
				for (int i = 0; i < 64; i++) t[i] = 0;

				for (int i = 0; i < 32; i++)
					for (int j = 0; j < 32; j++)
						t[i + j] += x->v[i] * y->v[j];

				/* Reduce coefficients */
				for (int i = 0; i < 63; i++) {
					uint carry = t[i] >> 8;
					t[i + 1] += carry;
					t[i] &= 0xff;
				}

				barrett_reduce(r, t);
			}

			public static unsafe void sc25519_square(sc25519* r, sc25519* x) {
				sc25519_mul(r, x, x);
			}
		}
		struct ge25519 {
			public fe25519 x;
			public fe25519 y;
			public fe25519 z;
			public fe25519 t;

			struct ge25519_p1p1 {
				public fe25519 x;
				public fe25519 z;
				public fe25519 y;
				public fe25519 t;
			}
			struct ge25519_p2 {
				public fe25519 x;
				public fe25519 y;
				public fe25519 z;
			}

			/* Windowsize for fixed-window scalar multiplication */
			const int WINDOWSIZE = 2; //#define WINDOWSIZE 2                      /* Should be 1,2, or 4 */
			const int WINDOWMASK = ((1 << WINDOWSIZE) - 1); //#define WINDOWMASK ((1<<WINDOWSIZE)-1)

			/* packed parameter d in the Edwards curve equation */
			static Byte[] ecd = new Byte[32] {0xA3, 0x78, 0x59, 0x13, 0xCA, 0x4D, 0xEB, 0x75, 0xAB, 0xD8, 0x41, 0x41, 0x4D, 0x0A, 0x70, 0x00, 
                                      0x98, 0xE8, 0x79, 0x77, 0x79, 0x40, 0xC7, 0x8C, 0x73, 0xFE, 0x6F, 0x2B, 0xEE, 0x6C, 0x03, 0x52};

			/* Packed coordinates of the base point */
			static Byte[] ge25519_base_x = new Byte[32]  {0x1A, 0xD5, 0x25, 0x8F, 0x60, 0x2D, 0x56, 0xC9, 0xB2, 0xA7, 0x25, 0x95, 0x60, 0xC7, 0x2C, 0x69, 
                                                 0x5C, 0xDC, 0xD6, 0xFD, 0x31, 0xE2, 0xA4, 0xC0, 0xFE, 0x53, 0x6E, 0xCD, 0xD3, 0x36, 0x69, 0x21};
			static Byte[] ge25519_base_y = new Byte[32]  {0x58, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 
                                                 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66};
			static Byte[] ge25519_base_z = new Byte[32] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
			static Byte[] ge25519_base_t = new Byte[32]  {0xA3, 0xDD, 0xB7, 0xA5, 0xB3, 0x8A, 0xDE, 0x6D, 0xF5, 0x52, 0x51, 0x77, 0x80, 0x9F, 0xF0, 0x20, 
                                                 0x7D, 0xE3, 0xAB, 0x64, 0x8E, 0x4E, 0xEA, 0x66, 0x65, 0x76, 0x8B, 0xD7, 0x0F, 0x5F, 0x87, 0x67};

			/* Packed coordinates of the neutral element */
			static Byte[] ge25519_neutral_x = new Byte[32]; // { 0 };
			static Byte[] ge25519_neutral_y = new Byte[32] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
			static Byte[] ge25519_neutral_z = new Byte[32] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
			static Byte[] ge25519_neutral_t = new Byte[32]; // { 0 };

			static unsafe void p1p1_to_p2(ge25519_p2* r, ge25519_p1p1* p) {
				fe25519.fe25519_mul(&r->x, &p->x, &p->t);
				fe25519.fe25519_mul(&r->y, &p->y, &p->z);
				fe25519.fe25519_mul(&r->z, &p->z, &p->t);
			}

			static unsafe void p1p1_to_p3(ge25519* r, ge25519_p1p1* p) {
				p1p1_to_p2((ge25519_p2*)r, p);
				fe25519.fe25519_mul(&r->t, &p->x, &p->y);
			}

			/* Constant-time version of: if(b) r = p */
			static unsafe void cmov_p3(ge25519* r, ge25519* p, Byte b) {
				fe25519.fe25519_cmov(&r->x, &p->x, b);
				fe25519.fe25519_cmov(&r->y, &p->y, b);
				fe25519.fe25519_cmov(&r->z, &p->z, b);
				fe25519.fe25519_cmov(&r->t, &p->t, b);
			}

			/* See http://www.hyperelliptic.org/EFD/g1p/auto-twisted-extended-1.html#doubling-dbl-2008-hwcd */
			static unsafe void dbl_p1p1(ge25519_p1p1* r, ge25519_p2* p) {
				fe25519 a, b, c, d;
				fe25519.fe25519_square(&a, &p->x);
				fe25519.fe25519_square(&b, &p->y);
				fe25519.fe25519_square(&c, &p->z);
				fe25519.fe25519_add(&c, &c, &c);
				fe25519.fe25519_neg(&d, &a);

				fe25519.fe25519_add(&r->x, &p->x, &p->y);
				fe25519.fe25519_square(&r->x, &r->x);
				fe25519.fe25519_sub(&r->x, &r->x, &a);
				fe25519.fe25519_sub(&r->x, &r->x, &b);
				fe25519.fe25519_add(&r->z, &d, &b);
				fe25519.fe25519_sub(&r->t, &r->z, &c);
				fe25519.fe25519_sub(&r->y, &d, &b);
			}

			static unsafe void add_p1p1(ge25519_p1p1* r, ge25519* p, ge25519* q) {
				fe25519 a, b, c, d, t, fd;
				fixed (Byte* ecdp = ecd) fe25519.fe25519_unpack(&fd, ecdp);

				fe25519.fe25519_sub(&a, &p->y, &p->x); // A = (Y1-X1)*(Y2-X2)
				fe25519.fe25519_sub(&t, &q->y, &q->x);
				fe25519.fe25519_mul(&a, &a, &t);
				fe25519.fe25519_add(&b, &p->x, &p->y); // B = (Y1+X1)*(Y2+X2)
				fe25519.fe25519_add(&t, &q->x, &q->y);
				fe25519.fe25519_mul(&b, &b, &t);
				fe25519.fe25519_mul(&c, &p->t, &q->t); //C = T1*k*T2
				fe25519.fe25519_mul(&c, &c, &fd);
				fe25519.fe25519_add(&c, &c, &c);       //XXX: Can save this addition by precomputing 2*ecd
				fe25519.fe25519_mul(&d, &p->z, &q->z); //D = Z1*2*Z2
				fe25519.fe25519_add(&d, &d, &d);
				fe25519.fe25519_sub(&r->x, &b, &a); // E = B-A
				fe25519.fe25519_sub(&r->t, &d, &c); // F = D-C
				fe25519.fe25519_add(&r->z, &d, &c); // G = D+C
				fe25519.fe25519_add(&r->y, &b, &a); // H = B+A
			}

			/* ********************************************************************
			 *                    EXPORTED FUNCTIONS
			 ******************************************************************** */

			/* return 0 on success, -1 otherwise */
			public unsafe static Boolean ge25519_unpack_vartime(ge25519* r, Byte* p) { //const unsigned char p[32]
				Boolean ret;
				fe25519 t, fd;
				fe25519.fe25519_setone(&r->z);
				fixed (Byte* ecdp = ecd) fe25519.fe25519_unpack(&fd, ecdp);
				Byte par = (Byte)(p[31] >> 7);
				fe25519.fe25519_unpack(&r->y, p);
				fe25519.fe25519_square(&r->x, &r->y);
				fe25519.fe25519_mul(&t, &r->x, &fd);
				fe25519.fe25519_sub(&r->x, &r->x, &r->z);
				fe25519.fe25519_add(&t, &r->z, &t);
				fe25519.fe25519_invert(&t, &t);
				fe25519.fe25519_mul(&r->x, &r->x, &t);
				ret = fe25519.fe25519_sqrt_vartime(&r->x, &r->x, par);
				fe25519.fe25519_mul(&r->t, &r->x, &r->y);
				return ret;
			}

			public static unsafe void ge25519_pack(Byte* r, ge25519* p) { //unsigned char r[32]
				fe25519 tx, ty, zi;
				fe25519.fe25519_invert(&zi, &p->z);
				fe25519.fe25519_mul(&tx, &p->x, &zi);
				fe25519.fe25519_mul(&ty, &p->y, &zi);
				fe25519.fe25519_pack(r, &ty);
				r[31] ^= (Byte)(fe25519.fe25519_getparity(&tx) << 7);
			}

			public static unsafe void ge25519_add(ge25519* r, ge25519* p, ge25519* q) {
				ge25519_p1p1 grp1p1;
				add_p1p1(&grp1p1, p, q);
				p1p1_to_p3(r, &grp1p1);
			}

			public static unsafe void ge25519_double(ge25519* r, ge25519* p) {
				ge25519_p1p1 grp1p1;
				dbl_p1p1(&grp1p1, (ge25519_p2*)p);
				p1p1_to_p3(r, &grp1p1);
			}

			public static unsafe void ge25519_scalarmult(ge25519* r, ge25519* p, sc25519* s) {
				int i, j, k;
				ge25519 g;
				fixed (Byte* ge25519_neutral_xp = ge25519_neutral_x) fe25519.fe25519_unpack(&g.x, ge25519_neutral_xp);
				fixed (Byte* ge25519_neutral_yp = ge25519_neutral_y) fe25519.fe25519_unpack(&g.y, ge25519_neutral_yp);
				fixed (Byte* ge25519_neutral_zp = ge25519_neutral_z) fe25519.fe25519_unpack(&g.z, ge25519_neutral_zp);
				fixed (Byte* ge25519_neutral_tp = ge25519_neutral_t) fe25519.fe25519_unpack(&g.t, ge25519_neutral_tp);

				ge25519[] pre = new ge25519[(1 << WINDOWSIZE)];
				ge25519 t;
				ge25519_p1p1 tp1p1;
				Byte w;
				Byte* sb = stackalloc Byte[32];
				sc25519.sc25519_to32bytes(sb, s);

				// Precomputation
				pre[0] = g;
				pre[1] = *p;
				for (i = 2; i < (1 << WINDOWSIZE); i += 2) {
					fixed (ge25519* prep = pre) {
						dbl_p1p1(&tp1p1, (ge25519_p2*)(prep + i / 2));
						p1p1_to_p3(prep + i, &tp1p1);
						add_p1p1(&tp1p1, prep + i, prep + 1);
						p1p1_to_p3(prep + i + 1, &tp1p1);
					}
				}

				// Fixed-window scalar multiplication
				for (i = 32; i > 0; i--) {
					for (j = 8 - WINDOWSIZE; j >= 0; j -= WINDOWSIZE) {
						for (k = 0; k < WINDOWSIZE - 1; k++) {
							dbl_p1p1(&tp1p1, (ge25519_p2*)&g);
							p1p1_to_p2((ge25519_p2*)&g, &tp1p1);
						}
						dbl_p1p1(&tp1p1, (ge25519_p2*)&g);
						p1p1_to_p3(&g, &tp1p1);
						// Cache-timing resistant loading of precomputed value:
						w = (Byte)((sb[i - 1] >> j) & WINDOWMASK);
						t = pre[0];
						for (k = 1; k < (1 << WINDOWSIZE); k++)
							fixed (ge25519* prekp = &pre[k]) cmov_p3(&t, prekp, (k == w) ? (Byte)1 : (Byte)0);

						add_p1p1(&tp1p1, &g, &t);
						if (j != 0) p1p1_to_p2((ge25519_p2*)&g, &tp1p1);
						else p1p1_to_p3(&g, &tp1p1); /* convert to p3 representation at the end */
					}
				}
				r->x = g.x;
				r->y = g.y;
				r->z = g.z;
				r->t = g.t;
			}

			public unsafe static void ge25519_scalarmult_base(ge25519* r, sc25519* s) {
				/* XXX: Better algorithm for known-base-point scalar multiplication */
				ge25519 t;
				fixed (Byte* ge25519_base_xp = ge25519_base_x) fe25519.fe25519_unpack(&t.x, ge25519_base_xp);
				fixed (Byte* ge25519_base_yp = ge25519_base_y) fe25519.fe25519_unpack(&t.y, ge25519_base_yp);
				fixed (Byte* ge25519_base_zp = ge25519_base_z) fe25519.fe25519_unpack(&t.z, ge25519_base_zp);
				fixed (Byte* ge25519_base_tp = ge25519_base_t) fe25519.fe25519_unpack(&t.t, ge25519_base_tp);
				ge25519_scalarmult(r, &t, s);
			}
		}
		unsafe struct fe25519 {
			public fixed UInt32 v[32]; // crypto_uint32 v[32];

			const int WINDOWSIZE = 4; //#define WINDOWSIZE 4 /* Should be 1,2, or 4 */
			const int WINDOWMASK = ((1 << WINDOWSIZE) - 1); //#define WINDOWMASK ((1<<WINDOWSIZE)-1)

			static unsafe void reduce_add_sub(fe25519* r) {
				for (int rep = 0; rep < 4; rep++) {
					UInt32 t = r->v[31] >> 7;
					r->v[31] &= 127;
					t *= 19;
					r->v[0] += t;
					for (int i = 0; i < 31; i++) {
						t = r->v[i] >> 8;
						r->v[i + 1] += t;
						r->v[i] &= 255;
					}
				}
			}

			unsafe static void reduce_mul(fe25519* r) {
				for (int rep = 0; rep < 2; rep++) {
					UInt32 t = r->v[31] >> 7;
					r->v[31] &= 127;
					t *= 19;
					r->v[0] += t;
					for (int i = 0; i < 31; i++) {
						t = r->v[i] >> 8;
						r->v[i + 1] += t;
						r->v[i] &= 255;
					}
				}
			}

			/* reduction modulo 2^255-19 */
			unsafe static void freeze(fe25519* r) {
				UInt32 m = (r->v[31] == 127) ? 1u : 0;
				for (int i = 30; i > 1; i--)
					m *= (r->v[i] == 255) ? 1u : 0;
				m *= (r->v[0] >= 237) ? 1u : 0;

				r->v[31] -= m * 127;
				for (int i = 30; i > 0; i--)
					r->v[i] -= m * 255;
				r->v[0] -= m * 237;
			}

			/*freeze input before calling isone*/
			unsafe static Boolean isone(fe25519* x) {
				bool r = x->v[0] == 1;
				for (int i = 1; i < 32; i++) r &= (x->v[i] == 0);
				return r;
			}

			/*freeze input before calling iszero*/
			unsafe static Boolean iszero(fe25519* x) {
				bool r = (x->v[0] == 0);
				for (int i = 1; i < 32; i++) r &= (x->v[i] == 0);
				return r;
			}


			unsafe static Boolean issquare(fe25519* x) {
				Byte[] e = new Byte[32] { 0xf6, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x3f }; /* (p-1)/2 */
				fe25519 t;

				fixed (Byte* ep = e) fe25519_pow(&t, x, ep);
				freeze(&t);
				return isone(&t) || iszero(&t);
			}

			public static unsafe void fe25519_unpack(fe25519* r, Byte* x) { //const unsigned char x[32]
				for (int i = 0; i < 32; i++) r->v[i] = x[i];
				r->v[31] &= 127;
			}

			/* Assumes input x being reduced mod 2^255 */
			public static unsafe void fe25519_pack(Byte* r, fe25519* x) { //unsigned char r[32]
				for (int i = 0; i < 32; i++)
					r[i] = (byte)x->v[i];

				/* freeze byte array */
				UInt32 m = (r[31] == 127) ? 1u : 0; /* XXX: some compilers might use branches; fix */
				for (int i = 30; i > 1; i--)
					m *= (r[i] == 255) ? 1u : 0;
				m *= (r[0] >= 237) ? 1u : 0;
				r[31] -= (byte)(m * 127);
				for (int i = 30; i > 0; i--)
					r[i] -= (byte)(m * 255);
				r[0] -= (byte)(m * 237);
			}

			public static unsafe void fe25519_cmov(fe25519* r, fe25519* x, Byte b) {
				Byte nb = (Byte)(1 - b);
				for (int i = 0; i < 32; i++) r->v[i] = nb * r->v[i] + b * x->v[i];
			}

			public static unsafe Byte fe25519_getparity(fe25519* x) {
				fe25519 t = new fe25519();
				for (int i = 0; i < 32; i++) t.v[i] = x->v[i];
				freeze(&t);
				return (Byte)(t.v[0] & 1);
			}

			public static unsafe void fe25519_setone(fe25519* r) {
				r->v[0] = 1;
				for (int i = 1; i < 32; i++) r->v[i] = 0;
			}

			static unsafe void fe25519_setzero(fe25519* r) {
				for (int i = 0; i < 32; i++) r->v[i] = 0;
			}

			public static unsafe void fe25519_neg(fe25519* r, fe25519* x) {
				fe25519 t = new fe25519();
				for (int i = 0; i < 32; i++) t.v[i] = x->v[i];
				fe25519_setzero(r);
				fe25519_sub(r, r, &t);
			}

			public static unsafe void fe25519_add(fe25519* r, fe25519* x, fe25519* y) {
				for (int i = 0; i < 32; i++) r->v[i] = x->v[i] + y->v[i];
				reduce_add_sub(r);
			}

			public static unsafe void fe25519_sub(fe25519* r, fe25519* x, fe25519* y) {
				UInt32* t = stackalloc UInt32[32];
				t[0] = x->v[0] + 0x1da;
				t[31] = x->v[31] + 0xfe;
				for (int i = 1; i < 31; i++) t[i] = x->v[i] + 0x1fe;
				for (int i = 0; i < 32; i++) r->v[i] = t[i] - y->v[i];
				reduce_add_sub(r);
			}

			public static unsafe void fe25519_mul(fe25519* r, fe25519* x, fe25519* y) {
				UInt32* t = stackalloc UInt32[63];
				for (int i = 0; i < 63; i++) t[i] = 0;
				for (int i = 0; i < 32; i++)
					for (int j = 0; j < 32; j++)
						t[i + j] += x->v[i] * y->v[j];

				for (int i = 32; i < 63; i++)
					r->v[i - 32] = t[i - 32] + 38 * t[i];
				r->v[31] = t[31]; /* result now in r[0]...r[31] */

				reduce_mul(r);
			}

			public static unsafe void fe25519_square(fe25519* r, fe25519* x) {
				fe25519_mul(r, x, x);
			}

			/*XXX: Make constant time! */
			public static unsafe void fe25519_pow(fe25519* r, fe25519* x, Byte* e) {
				/*
				fe25519 g;
				fe25519_setone(&g);
				int i;
				unsigned char j;
				for(i=32;i>0;i--)
				{
				  for(j=128;j>0;j>>=1)
				  {
					fe25519_square(&g,&g);
					if(e[i-1] & j) 
					  fe25519_mul(&g,&g,x);
				  }
				}
				for(i=0;i<32;i++) r->v[i] = g.v[i];
				*/
				fe25519 g;
				fe25519_setone(&g);
				fe25519[] pre = new fe25519[(1 << WINDOWSIZE)];
				fe25519 t;
				Byte w;

				// Precomputation
				fixed (fe25519* prep = pre) fe25519_setone(prep);
				pre[1] = *x;
				for (int i = 2; i < (1 << WINDOWSIZE); i += 2) {
					fixed (fe25519* prep = pre) {
						fe25519_square(prep + i, prep + i / 2);
						fe25519_mul(prep + i + 1, prep + i, prep + 1);
					}
				}

				// Fixed-window scalar multiplication
				for (int i = 32; i > 0; i--) {
					for (int j = 8 - WINDOWSIZE; j >= 0; j -= WINDOWSIZE) {
						for (int k = 0; k < WINDOWSIZE; k++)
							fe25519_square(&g, &g);
						// Cache-timing resistant loading of precomputed value:
						w = (Byte)((e[i - 1] >> j) & WINDOWMASK);
						t = pre[0];
						for (int k = 1; k < (1 << WINDOWSIZE); k++) fixed (fe25519* prekp = &pre[k]) fe25519_cmov(&t, prekp, (k == w) ? (Byte)1 : (Byte)0);
						fe25519_mul(&g, &g, &t);
					}
				}
				*r = g;
			}

			/* Return 0 on success, 1 otherwise */
			public static unsafe Boolean fe25519_sqrt_vartime(fe25519* r, fe25519* x, Byte parity) {
				/* See HAC, Alg. 3.37 */
				if (!issquare(x)) return true;
				Byte[] e = new Byte[32] { 0xfb, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x1f }; /* (p-1)/4 */
				Byte[] e2 = new Byte[32] { 0xfe, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x0f }; /* (p+3)/8 */
				Byte[] e3 = new Byte[32] { 0xfd, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x0f }; /* (p-5)/8 */
				fe25519 p = new fe25519(); // { { 0 } };
				fe25519 d;
				fixed (Byte* ep = e) fe25519.fe25519_pow(&d, x, ep);
				freeze(&d);
				if (isone(&d))
					fixed (Byte* e2p = e2) fe25519.fe25519_pow(r, x, e2p);
				else {
					for (int i = 0; i < 32; i++)
						d.v[i] = 4 * x->v[i];
					fixed (Byte* e3p = e3) fe25519.fe25519_pow(&d, &d, e3p);
					for (int i = 0; i < 32; i++)
						r->v[i] = 2 * x->v[i];
					fe25519_mul(r, r, &d);
				}
				freeze(r);
				if ((r->v[0] & 1) != (parity & 1)) {
					fe25519_sub(r, &p, r);
				}
				return false;
			}

			public static unsafe void fe25519_invert(fe25519* r, fe25519* x) {
				fe25519 z2;
				fe25519 z9;
				fe25519 z11;
				fe25519 z2_5_0;
				fe25519 z2_10_0;
				fe25519 z2_20_0;
				fe25519 z2_50_0;
				fe25519 z2_100_0;
				fe25519 t0;
				fe25519 t1;

				/* 2 */
				fe25519_square(&z2, x);
				/* 4 */
				fe25519_square(&t1, &z2);
				/* 8 */
				fe25519_square(&t0, &t1);
				/* 9 */
				fe25519_mul(&z9, &t0, x);
				/* 11 */
				fe25519_mul(&z11, &z9, &z2);
				/* 22 */
				fe25519_square(&t0, &z11);
				/* 2^5 - 2^0 = 31 */
				fe25519_mul(&z2_5_0, &t0, &z9);

				/* 2^6 - 2^1 */
				fe25519_square(&t0, &z2_5_0);
				/* 2^7 - 2^2 */
				fe25519_square(&t1, &t0);
				/* 2^8 - 2^3 */
				fe25519_square(&t0, &t1);
				/* 2^9 - 2^4 */
				fe25519_square(&t1, &t0);
				/* 2^10 - 2^5 */
				fe25519_square(&t0, &t1);
				/* 2^10 - 2^0 */
				fe25519_mul(&z2_10_0, &t0, &z2_5_0);

				/* 2^11 - 2^1 */
				fe25519_square(&t0, &z2_10_0);
				/* 2^12 - 2^2 */
				fe25519_square(&t1, &t0);
				/* 2^20 - 2^10 */
				for (int i = 2; i < 10; i += 2) { fe25519_square(&t0, &t1); fe25519_square(&t1, &t0); }
				/* 2^20 - 2^0 */
				fe25519_mul(&z2_20_0, &t1, &z2_10_0);

				/* 2^21 - 2^1 */
				fe25519_square(&t0, &z2_20_0);
				/* 2^22 - 2^2 */
				fe25519_square(&t1, &t0);
				/* 2^40 - 2^20 */
				for (int i = 2; i < 20; i += 2) { fe25519_square(&t0, &t1); fe25519_square(&t1, &t0); }
				/* 2^40 - 2^0 */
				fe25519_mul(&t0, &t1, &z2_20_0);

				/* 2^41 - 2^1 */
				fe25519_square(&t1, &t0);
				/* 2^42 - 2^2 */
				fe25519_square(&t0, &t1);
				/* 2^50 - 2^10 */
				for (int i = 2; i < 10; i += 2) { fe25519_square(&t1, &t0); fe25519_square(&t0, &t1); }
				/* 2^50 - 2^0 */
				fe25519_mul(&z2_50_0, &t0, &z2_10_0);

				/* 2^51 - 2^1 */
				fe25519_square(&t0, &z2_50_0);
				/* 2^52 - 2^2 */
				fe25519_square(&t1, &t0);
				/* 2^100 - 2^50 */
				for (int i = 2; i < 50; i += 2) { fe25519_square(&t0, &t1); fe25519_square(&t1, &t0); }
				/* 2^100 - 2^0 */
				fe25519_mul(&z2_100_0, &t1, &z2_50_0);

				/* 2^101 - 2^1 */
				fe25519_square(&t1, &z2_100_0);
				/* 2^102 - 2^2 */
				fe25519_square(&t0, &t1);
				/* 2^200 - 2^100 */
				for (int i = 2; i < 100; i += 2) { fe25519_square(&t1, &t0); fe25519_square(&t0, &t1); }
				/* 2^200 - 2^0 */
				fe25519_mul(&t1, &t0, &z2_100_0);

				/* 2^201 - 2^1 */
				fe25519_square(&t0, &t1);
				/* 2^202 - 2^2 */
				fe25519_square(&t1, &t0);
				/* 2^250 - 2^50 */
				for (int i = 2; i < 50; i += 2) { fe25519_square(&t0, &t1); fe25519_square(&t1, &t0); }
				/* 2^250 - 2^0 */
				fe25519_mul(&t0, &t1, &z2_50_0);

				/* 2^251 - 2^1 */
				fe25519_square(&t1, &t0);
				/* 2^252 - 2^2 */
				fe25519_square(&t0, &t1);
				/* 2^253 - 2^3 */
				fe25519_square(&t1, &t0);
				/* 2^254 - 2^4 */
				fe25519_square(&t0, &t1);
				/* 2^255 - 2^5 */
				fe25519_square(&t1, &t0);
				/* 2^255 - 21 */
				fe25519_mul(r, &t1, &z11);
			}
		}

		public static unsafe void crypto_sign_keypair(out Byte[] pk, out Byte[] sk) {
			sc25519 scsk;
			ge25519 gepk;

			sk = new Byte[SECRETKEYBYTES];
			pk = new Byte[PUBLICKEYBYTES];
			randombytes.generate(sk);
			fixed (Byte* skp = sk) crypto_hash.sha512.crypto_hash(skp, skp, 32);
			sk[0] &= 248;
			sk[31] &= 127;
			sk[31] |= 64;

			fixed (Byte* skp = sk) sc25519.sc25519_from32bytes(&scsk, skp);

			ge25519.ge25519_scalarmult_base(&gepk, &scsk);
			fixed (Byte* pkp = pk) ge25519.ge25519_pack(pkp, &gepk);
		}

		public static unsafe Byte[] crypto_sign(Byte[] m, Byte[] sk) {
			if (sk.Length != SECRETKEYBYTES) throw new ArgumentException("sk.Length != SECRETKEYBYTES");
			Byte[] sm = new Byte[m.Length + 64];
			UInt64 smlen;
			fixed (Byte* smp = sm, mp = m, skp = sk) crypto_sign(smp, out smlen, mp, (ulong)m.Length, skp);
			return sm;
		}
		public static unsafe void crypto_sign(Byte* sm, out UInt64 smlen, Byte* m, UInt64 mlen, Byte* sk) {
			sc25519 sck, scs, scsk;
			ge25519 ger;
			Byte* r = stackalloc Byte[32];
			Byte* s = stackalloc Byte[32];
			Byte* hmg = stackalloc Byte[crypto_hash.sha512.BYTES];
			Byte* hmr = stackalloc Byte[crypto_hash.sha512.BYTES];

			smlen = mlen + 64;
			for (UInt64 i = 0; i < mlen; i++) sm[32 + i] = m[i];
			for (int i = 0; i < 32; i++) sm[i] = sk[32 + i];
			crypto_hash.sha512.crypto_hash(hmg, sm, mlen + 32); /* Generate k as h(m,sk[32],...,sk[63]) */

			sc25519.sc25519_from64bytes(&sck, hmg);
			ge25519.ge25519_scalarmult_base(&ger, &sck);
			ge25519.ge25519_pack(r, &ger);

			for (int i = 0; i < 32; i++) sm[i] = r[i];

			crypto_hash.sha512.crypto_hash(hmr, sm, mlen + 32); /* Compute h(m,r) */
			sc25519.sc25519_from64bytes(&scs, hmr);
			sc25519.sc25519_mul(&scs, &scs, &sck);

			sc25519.sc25519_from32bytes(&scsk, sk);
			sc25519.sc25519_add(&scs, &scs, &scsk);

			sc25519.sc25519_to32bytes(s, &scs); /* cat s */
			for (UInt64 i = 0; i < 32; i++)
				sm[mlen + 32 + i] = s[i];
		}

		public static unsafe Byte[] crypto_sign_open(Byte[] sm, Byte[] pk) {
			if (pk.Length != PUBLICKEYBYTES) throw new ArgumentException("pk.Length != PUBLICKEYBYTES");
			Byte[] m = new Byte[sm.Length - 64];
			UInt64 mlen;
			fixed (Byte* smp = sm, mp = m, pkp = pk) {
				if (crypto_sign_open(mp, out mlen, smp, (ulong)sm.Length, pkp) != 0) return null;
			}
			return m;
		}
		public static unsafe int crypto_sign_open(Byte* m, out UInt64 mlen, Byte* sm, UInt64 smlen, Byte* pk) {
			mlen = 0;
			if (smlen < 64) return -1;

			Byte* t1 = stackalloc Byte[32], t2 = stackalloc Byte[32];
			ge25519 get1, get2, gepk;
			sc25519 schmr, scs;
			Byte* hmr = stackalloc Byte[crypto_hash.sha512.BYTES];

			if (ge25519.ge25519_unpack_vartime(&get1, sm)) return -1;
			if (ge25519.ge25519_unpack_vartime(&gepk, pk)) return -1;

			crypto_hash.sha512.crypto_hash(hmr, sm, smlen - 32);

			sc25519.sc25519_from64bytes(&schmr, hmr);
			ge25519.ge25519_scalarmult(&get1, &get1, &schmr);
			ge25519.ge25519_add(&get1, &get1, &gepk);
			ge25519.ge25519_pack(t1, &get1);

			sc25519.sc25519_from32bytes(&scs, &sm[smlen - 32]);
			ge25519.ge25519_scalarmult_base(&get2, &scs);
			ge25519.ge25519_pack(t2, &get2);

			if (m != null) for (UInt64 i = 0; i < smlen - 64; i++) m[i] = sm[i + 32];
			mlen = smlen - 64;

			return crypto_verify._32.crypto_verify(t1, t2);
		}
	}
}