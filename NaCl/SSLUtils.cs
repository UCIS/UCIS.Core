using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using UCIS.Util;

namespace UCIS.NaCl {
	public static class SSLUtils {
		public static X509Certificate2Collection DecodeCertificates(Byte[] data) {
			if (data.Length == 0) throw new EndOfStreamException();
			if (data[0] == '0') { //PFX
				X509Certificate2Collection certs = new X509Certificate2Collection();
				certs.Import(data, String.Empty, X509KeyStorageFlags.Exportable);
				return certs;
			} else if (data[0] == '-') { //PEM
				using (TextReader reader = new StreamReader(new MemoryStream(data))) return ReadPEMData(reader);
			} else {
				throw new InvalidDataException();
			}
		}

		public static Byte[] EncodeCertificates(X509Certificate2Collection certs) {
			if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
				return certs.Export(X509ContentType.Pkcs12, String.Empty);
			} else {
				using (MemoryStream ms = new MemoryStream()) {
					using (TextWriter writer = new StreamWriter(ms, Encoding.ASCII)) {
						foreach (X509Certificate2 cert in certs) {
							writer.WriteLine("-----BEGIN CERTIFICATE-----");
							writer.WriteLine(Convert.ToBase64String(cert.Export(X509ContentType.Cert, String.Empty), Base64FormattingOptions.InsertLineBreaks));
							writer.WriteLine("-----END CERTIFICATE-----");
							if (cert.HasPrivateKey) {
								RSACryptoServiceProvider rsakey = cert.PrivateKey as RSACryptoServiceProvider;
								if (rsakey != null) {
									writer.WriteLine("-----BEGIN RSA PRIVATE KEY-----");
									writer.WriteLine(Convert.ToBase64String(EncodeRSAPrivateKey(rsakey.ExportParameters(true)), Base64FormattingOptions.InsertLineBreaks));
									writer.WriteLine("-----END RSA PRIVATE KEY-----");
								}
							}
						}
					}
					return ms.ToArray();
				}
			}
		}

		public static X509Certificate2 FindServerCertificate(X509Certificate2Collection certs) {
			foreach (X509Certificate2 cert in certs) if (cert.HasPrivateKey) return cert;
			return null;
		}

		public static int ImportCACertificates(X509Certificate2Collection certs, out X509Certificate2 servercert) {
			X509Store store = new X509Store(StoreName.CertificateAuthority, StoreLocation.CurrentUser);
			store.Open(OpenFlags.ReadWrite);
			servercert = null;
			int imported = 0;
			foreach (X509Certificate2 cert in certs) {
				if (servercert == null && cert.HasPrivateKey) {
					servercert = cert;
				} else {
					store.Add(cert);
					imported++;
				}
			}
			store.Close();
			return imported;
		}

		private static X509Certificate2 FindCertificateBySubjectName(X509Certificate2Collection collection, String subjectName) {
			foreach (X509Certificate2 cert in collection) if (cert.Subject == subjectName) return cert;
			return null;
		}
		private static X509Certificate2Collection GetCertificatesFromStore(StoreName storeName, StoreLocation storeLocation) {
			X509Store store = new X509Store(storeName, storeLocation);
			X509Certificate2Collection certs = store.Certificates;
			store.Close();
			return certs;
		}

		public static X509Certificate2Collection BuildCertificateChain(X509Certificate2 cert) {
			return BuildCertificateChain(cert, null);
		}
		public static X509Certificate2Collection BuildCertificateChain(X509Certificate2Collection input) {
			X509Certificate2Collection output = new X509Certificate2Collection();
			X509Certificate2 first = input[0];
			foreach (X509Certificate2 cert in input) if (cert.HasPrivateKey) { first = cert; break; }
			return BuildCertificateChain(first, input);
		}
		public static X509Certificate2Collection BuildCertificateChain(X509Certificate2 first, X509Certificate2Collection input) {
			X509Certificate2Collection chain = new X509Certificate2Collection();
			X509Certificate2 last = first;
			chain.Add(first);
			X509Certificate2Collection userCA = null, machineCA = null;
			while (last.IssuerName != last.SubjectName && chain.Count < 10) {
				X509Certificate2 issuer = null;
				if (input != null) issuer = FindCertificateBySubjectName(input, last.Issuer);
				if (issuer == null) {
					if (userCA == null) userCA = GetCertificatesFromStore(StoreName.CertificateAuthority, StoreLocation.CurrentUser);
					issuer = FindCertificateBySubjectName(userCA, last.Issuer);
				}
				if (issuer == null) {
					if (machineCA == null) machineCA = GetCertificatesFromStore(StoreName.CertificateAuthority, StoreLocation.CurrentUser);
					issuer = FindCertificateBySubjectName(machineCA, last.Issuer);
				}
				if (issuer == null) break;
				chain.Add(issuer);
				last = issuer;
			}
			return chain;
		}

		public static Boolean AddKeyToCertificates(X509Certificate2Collection certs, RSAParameters rsakey) {
			if (rsakey.Exponent == null) return false;
			int count = 0;
			foreach (X509Certificate2 cert in certs) {
				RSACryptoServiceProvider pubkey = cert.PublicKey.Key as RSACryptoServiceProvider;
				if (pubkey != null) {
					RSAParameters p2 = pubkey.ExportParameters(false);
					if (CompareBytes(rsakey.Exponent, p2.Exponent) && CompareBytes(rsakey.Modulus, p2.Modulus)) {
						RSA rsa = RSA.Create();
						rsa.ImportParameters(rsakey);
						cert.PrivateKey = rsa;
						count++;
					}
				}
			}
			return count != 0;
		}

		static Boolean CompareBytes(Byte[] a, Byte[] b) {
			if (a.Length != b.Length) return false;
			for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
			return true;
		}

		static X509Certificate2Collection ReadPEMData(TextReader reader) {
			X509Certificate2Collection certs = new X509Certificate2Collection();
			RSAParameters rsakey = default(RSAParameters);
			String block = null;
			StringBuilder sb = new StringBuilder();
			while (true) {
				String line = reader.ReadLine();
				if (line == null) break;
				line = line.Trim();
				if (line.Length == 0) continue;
				if (line.StartsWith("-----BEGIN ")) {
					block = line.Substring(11).TrimEnd('-');
					sb = new StringBuilder();
				} else if (line.StartsWith("-----END ")) {
					if (block == line.Substring(9).TrimEnd('-')) {
						Byte[] bytes = Convert.FromBase64String(sb.ToString());
						if (block == "RSA PRIVATE KEY") {
							rsakey = DecodeRSAPrivateKey(bytes);
						} else if (block == "CERTIFICATE") {
							certs.Add(new X509Certificate2(bytes, String.Empty, X509KeyStorageFlags.Exportable));
						}
					}
				} else if (line[0] == '-') {
					block = null;
				} else {
					sb.Append(line);
				}
			}

			AddKeyToCertificates(certs, rsakey);

			return certs;
		}

		static RSAParameters DecodeRSAPrivateKey(byte[] privkey) {
			using (BinaryReader r = new BinaryReader(new MemoryStream(privkey))) {
				UInt16 twobytes = r.ReadUInt16();
				if (twobytes == 0x8130) r.ReadByte();
				else if (twobytes == 0x8230) r.ReadInt16();
				else return default(RSAParameters);

				twobytes = r.ReadUInt16();
				if (twobytes != 0x0102) return default(RSAParameters); //version number
				Byte bt = r.ReadByte();
				if (bt != 0x00) return default(RSAParameters);

				RSAParameters p = new RSAParameters();

				p.Modulus = r.ReadBytes(GetIntegerSize(r));
				p.Exponent = r.ReadBytes(GetIntegerSize(r));
				p.D = r.ReadBytes(GetIntegerSize(r));
				p.P = r.ReadBytes(GetIntegerSize(r));
				p.Q = r.ReadBytes(GetIntegerSize(r));
				p.DP = r.ReadBytes(GetIntegerSize(r));
				p.DQ = r.ReadBytes(GetIntegerSize(r));
				p.InverseQ = r.ReadBytes(GetIntegerSize(r));

				return p;
			}
		}

		static int GetIntegerSize(BinaryReader r) {
			Byte bt = r.ReadByte();
			if (bt != 0x02) return 0;
			bt = r.ReadByte();
			int count;
			if (bt == 0x81) {
				count = r.ReadByte();
			} else if (bt == 0x82) {
				Byte highbyte = r.ReadByte();
				Byte lowbyte = r.ReadByte();
				count = (highbyte << 8) | lowbyte;
			} else {
				count = bt;
			}
			while (r.ReadByte() == 0x00) count--;
			r.BaseStream.Seek(-1, SeekOrigin.Current);
			return count;
		}

		static Byte[] EncodeRSAPrivateKey(RSAParameters rsakey) {
			using (MemoryStream buffer = new MemoryStream()) {
				using (BinaryWriter w = new BinaryWriter(buffer)) {
					w.Write((UInt16)0x8130);
					w.Write((Byte)0);
					w.Write((UInt16)0x0102);
					w.Write((Byte)0);
					WritePemBytes(w, rsakey.Modulus);
					WritePemBytes(w, rsakey.Exponent);
					WritePemBytes(w, rsakey.D);
					WritePemBytes(w, rsakey.P);
					WritePemBytes(w, rsakey.Q);
					WritePemBytes(w, rsakey.DP);
					WritePemBytes(w, rsakey.DQ);
					WritePemBytes(w, rsakey.InverseQ);
					return buffer.ToArray();
				}
			}
		}

		static void WritePemBytes(BinaryWriter w, Byte[] bytes) {
			w.Write((Byte)2);
			if (bytes.Length < 0x80) {
				w.Write((Byte)bytes.Length);
			} else if (bytes.Length <= 0xFF) {
				w.Write((Byte)0x81);
				w.Write((Byte)bytes.Length);
			} else {
				w.Write((Byte)0x82);
				w.Write((Byte)(bytes.Length >> 8));
				w.Write((Byte)bytes.Length);
			}
			w.Write(bytes);
		}

		private static Byte[] EncodeDerTag(Byte tclass, Boolean tconstructed, Int32 tnumber, params Byte[][] values) {
			Byte[] value = values == null ? new Byte[0] : ArrayUtil.Merge(values);
			using (MemoryStream ms = new MemoryStream()) {
				if (tnumber <= 30) {
					ms.WriteByte((Byte)(((tclass & 3) << 6) | (tconstructed ? 0x20 : 0x00) | (tnumber & 0x1F)));
				} else {
					ms.WriteByte((Byte)(((tclass & 3) << 6) | (tconstructed ? 0x20 : 0x00) | 0x31));
					if (tnumber > 0xFE00000) ms.WriteByte((Byte)(0x80 | ((tnumber >> 28) & 0x7F)));
					if (tnumber > 0x1FC000) ms.WriteByte((Byte)(0x80 | ((tnumber >> 21) & 0x7F)));
					if (tnumber > 0x3F80) ms.WriteByte((Byte)(0x80 | ((tnumber >> 14) & 0x7F)));
					if (tnumber > 0x7F) ms.WriteByte((Byte)(0x80 | ((tnumber >> 7) & 0x7F)));
					ms.WriteByte((Byte)(tnumber & 0x7F));
				}
				if (value.Length < 0x80) {
					ms.WriteByte((Byte)value.Length);
				} else if (value.Length < 0x100) {
					ms.WriteByte(0x81);
					ms.WriteByte((Byte)value.Length);
				} else if (value.Length < 0x10000) {
					ms.WriteByte(0x82);
					ms.WriteByte((Byte)(value.Length >> 8));
					ms.WriteByte((Byte)value.Length);
				} else if (value.Length < 0x1000000) {
					ms.WriteByte(0x83);
					ms.WriteByte((Byte)(value.Length >> 16));
					ms.WriteByte((Byte)(value.Length >> 8));
					ms.WriteByte((Byte)value.Length);
				} else {
					ms.WriteByte(0x84);
					ms.WriteByte((Byte)(value.Length >> 24));
					ms.WriteByte((Byte)(value.Length >> 16));
					ms.WriteByte((Byte)(value.Length >> 8));
					ms.WriteByte((Byte)value.Length);
				}
				ms.Write(value, 0, value.Length);
				return ms.ToArray();
			}
		}
		public static Byte[] GenerateCertificateSigningRequest(RSACryptoServiceProvider key, params String[] domains) {
			RSAParameters key_params = key.ExportParameters(false);
			Byte[] csr =
				EncodeDerTag(0, true, 0x10, //SEQUENCE
					EncodeDerTag(0, false, 0x02, new Byte[] { 0x00 }), //INTEGER version: 0
					EncodeDerTag(0, true, 0x10, //SEQUENCE
						EncodeDerTag(0, true, 0x11, //SET
							EncodeDerTag(0, true, 0x10, //SEQUENCE
								EncodeDerTag(0, false, 0x06, new Byte[] { 0x55, 0x04, 0x03 }), //OBJECT: commonName
								EncodeDerTag(0, false, 0x0C, Encoding.UTF8.GetBytes(domains[0])) //UTF8STRING
							)
						)
					),
					EncodeDerTag(0, true, 0x10, //SEQUENCE
						EncodeDerTag(0, true, 0x10, //SEQUENCE
							EncodeDerTag(0, false, 0x06, new Byte[] { 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x01 }), //OBJECT: rsaEncryption
							EncodeDerTag(0, false, 0x05) //NULL
						),
						EncodeDerTag(0, false, 0x03, //BIT STRING
							new Byte[1] { 0 }, //Number of unused bits in final octet
							EncodeDerTag(0, true, 0x10, //SEQUENCE
								EncodeDerTag(0, false, 0x02, ArrayUtil.Merge(new Byte[] { 0 }, key_params.Modulus)), //INTEGER - Don't know why we need the extra zero, but it seems necessary for the CSR to be accepted.
								EncodeDerTag(0, false, 0x02, key_params.Exponent) //INTEGER
							)
						)
					),
					EncodeDerTag(2, true, 0x00, //cont[0]
						EncodeDerTag(0, true, 0x10, //SEQUENCE
							EncodeDerTag(0, false, 0x06, new Byte[] { 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x09, 0x0E }), //OBJECT: Extension Request
							EncodeDerTag(0, true, 0x11, //SET
								EncodeDerTag(0, true, 0x10, //SEQUENCE
									EncodeDerTag(0, true, 0x10, //SEQUENCE
										EncodeDerTag(0, false, 0x06, new Byte[] { 0x55, 0x1D, 0x11 }), //OBJECT: X509v3 Subject Alternative Name
										EncodeDerTag(0, false, 0x04, //OCTET STRING
											EncodeDerTag(0, true, 0x10, //SEQUENCE
												Array.ConvertAll(domains, domain => EncodeDerTag(2, false, 0x02, Encoding.UTF8.GetBytes(domain))) //DNS name
											)
										)
									)
								)
							)
						)
					)
				);
			Byte[] signature;
			using (SHA256 sha = SHA256.Create()) signature = key.SignData(csr, sha);
			csr = EncodeDerTag(0, true, 0x10, //SEQUENCE
				csr,
				EncodeDerTag(0, true, 0x10, //SEQUENCE
					EncodeDerTag(0, false, 0x06, new Byte[] { 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x0B }), //OBJECT: sha256WithRSAEncryption
					EncodeDerTag(0, false, 0x05) //NULL
				),
				EncodeDerTag(0, false, 0x03, //BIT STRING
					new Byte[1] { 0 }, //Number of unused bits in final octet
					signature
				)
			);
			return csr;
		}
		public static X509Certificate2 GenerateSelfSignedCertificate(params String[] domains) {
			return GenerateSelfSignedCertificate(new RSACryptoServiceProvider(2048), domains);
		}
		public static X509Certificate2 GenerateSelfSignedCertificate(RSACryptoServiceProvider key, params String[] domains) {
			RSAParameters key_params = key.ExportParameters(false);
			DateTime notBefore = DateTime.Now.AddMonths(-1);
			DateTime notAfter = DateTime.Now.AddYears(10);
			Byte[] cert = EncodeDerTag(0, true, 0x10, //SEQUENCE
				EncodeDerTag(2, true, 0x00, //cont[0]
					EncodeDerTag(0, false, 0x02, new Byte[] { 2 }) //INTEGER version: 2
				),
				EncodeDerTag(0, false, 0x02, new Byte[] { 1 }, UCIS.NaCl.randombytes.generate(16)), //INTEGER serial
				EncodeDerTag(0, true, 0x10, //SEQUENCE
					EncodeDerTag(0, false, 0x06, new Byte[] { 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x0B }), //OBJECT: sha256WithRSAEncryption
					EncodeDerTag(0, false, 0x05) //NULL
				),
				EncodeDerTag(0, true, 0x10, //SEQUENCE
					EncodeDerTag(0, true, 0x11, //SET
						EncodeDerTag(0, true, 0x10, //SEQUENCE
							EncodeDerTag(0, false, 0x06, new Byte[] { 0x55, 0x04, 0x03 }),
							EncodeDerTag(0, false, 0x0C, Encoding.UTF8.GetBytes(domains[0]))
						)
					)
				),
				EncodeDerTag(0, true, 0x10, //SEQUENCE
					EncodeDerTag(0, false, 0x17, Encoding.ASCII.GetBytes(notBefore.ToString("yyMMddHHmmss\\Z"))), //UTCTIME notbefore
					EncodeDerTag(0, false, 0x17, Encoding.ASCII.GetBytes(notAfter.ToString("yyMMddHHmmss\\Z"))) //UTCTIME notafter
				),
				EncodeDerTag(0, true, 0x10, //SEQUENCE
					EncodeDerTag(0, true, 0x11, //SET
						EncodeDerTag(0, true, 0x10, //SEQUENCE
							EncodeDerTag(0, false, 0x06, new Byte[] { 0x55, 0x04, 0x03 }), //OBJECT: commonName
							EncodeDerTag(0, false, 0x0C, Encoding.UTF8.GetBytes(domains[0])) //UTF8STRING
						)
					)
				),
				EncodeDerTag(0, true, 0x10, //SEQUENCE
					EncodeDerTag(0, true, 0x10, //SEQUENCE
						EncodeDerTag(0, false, 0x06, new Byte[] { 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x01 }),
						EncodeDerTag(0, false, 0x05) //NULL
					),
					EncodeDerTag(0, false, 0x03, //BIT STRING
						new Byte[1] { 0 }, //Number of unused bits in final octet
						EncodeDerTag(0, true, 0x10, //SEQUENCE
							EncodeDerTag(0, false, 0x02, ArrayUtil.Merge(new Byte[] { 0 }, key_params.Modulus)), //INTEGER - Don't know why we need the extra zero, but it seems necessary for the CSR to be accepted.
							EncodeDerTag(0, false, 0x02, key_params.Exponent) //INTEGER
						)
					)
				),
				EncodeDerTag(2, true, 0x03, //cont[0]
					EncodeDerTag(0, true, 0x10, //SEQUENCE
						EncodeDerTag(0, true, 0x10, //SEQUENCE
							EncodeDerTag(0, false, 0x06, new Byte[] { 0x55, 0x1D, 0x11 }), //OBJECT: X509v3 Subject Alternative Name
							EncodeDerTag(0, false, 0x04, //OCTET STRING
								EncodeDerTag(0, true, 0x10, //SEQUENCE
									Array.ConvertAll(domains, domain => EncodeDerTag(2, false, 0x02, Encoding.UTF8.GetBytes(domain))) //DNS name
								)
							)
						)
					)
				)
			);
			Byte[] signature;
			using (SHA256 sha = SHA256.Create()) signature = key.SignData(cert, sha);
			cert = EncodeDerTag(0, true, 0x10, //SEQUENCE
				cert,
				EncodeDerTag(0, true, 0x10, //SEQUENCE
					EncodeDerTag(0, false, 0x06, new Byte[] { 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x0B }), //OBJECT: sha256WithRSAEncryption
					EncodeDerTag(0, false, 0x05) //NULL
				),
				EncodeDerTag(0, false, 0x03, //BIT STRING
					new Byte[1] { 0 }, //Number of unused bits in final octet
					signature
				)
			);

			X509Certificate2 c = new X509Certificate2(cert);
			c.PrivateKey = key;
			return c;
		}
	}
}
