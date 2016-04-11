using System;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using UCIS.Net.HTTP;
using UCIS.Pml;

namespace UCIS.NaCl {
	public class ACMEClient {
		private X509Certificate2 ca_cert = null;
		private RSACryptoServiceProvider account_key = null;
		private String replay_nonce = null;
		//const String acme_url = "https://acme-staging.api.letsencrypt.org";
		const String acme_url = "https://acme-v01.api.letsencrypt.org";
		const String ca_cert_url = "https://letsencrypt.org/certs/lets-encrypt-x3-cross-signed.der";
		const String agreement_url = "https://letsencrypt.org/documents/LE-SA-v1.0.1-July-27-2015.pdf";

		public delegate void CreateHTTPChallengeCallback(String path, String value);

		private static String urlbase64(Byte[] bytes) {
			return Convert.ToBase64String(bytes, Base64FormattingOptions.None).Replace('+', '-').Replace('/', '_').TrimEnd('=');
		}

		private Byte[] signed_request(String url, PmlDictionary payload) {
			RegisterKey();
			String payload64 = urlbase64(Encoding.UTF8.GetBytes(PmlJsonWriter.EncodeMessage(payload)));
			using (WebClient wc = new WebClient()) {
				String nonce = Interlocked.Exchange(ref replay_nonce, null);
				if (nonce == null) {
					wc.DownloadString(acme_url + "/directory");
					nonce = wc.ResponseHeaders["Replay-Nonce"];
				}
				RSAParameters account_key_params = account_key.ExportParameters(false);
				String pubExponent64 = urlbase64(account_key_params.Exponent);
				String pubMod64 = urlbase64(account_key_params.Modulus);
				PmlDictionary header = new PmlDictionary() { { "alg", "RS256" }, { "jwk", new PmlDictionary() { { "e", pubExponent64 }, { "kty", "RSA" }, { "n", pubMod64 } } } };
				PmlDictionary prot = new PmlDictionary() { { "alg", "RS256" }, { "jwk", new PmlDictionary() { { "e", pubExponent64 }, { "kty", "RSA" }, { "n", pubMod64 } } }, { "nonce", nonce } };
				String protected64 = urlbase64(Encoding.ASCII.GetBytes(PmlJsonWriter.EncodeMessage(prot)));
				String signed64;
				using (SHA256 sha = SHA256.Create()) signed64 = urlbase64(account_key.SignData(Encoding.ASCII.GetBytes(protected64 + "." + payload64), sha));
				PmlDictionary data = new PmlDictionary() { { "header", header }, { "protected", protected64 }, { "payload", payload64 }, { "signature", signed64 } };
				Byte[] ret = wc.UploadData(url, Encoding.UTF8.GetBytes(PmlJsonWriter.EncodeMessage(data)));
				if (!String.IsNullOrEmpty(wc.ResponseHeaders["Replay-Nonce"])) replay_nonce = wc.ResponseHeaders["Replay-Nonce"];
				return ret;
			}
		}

		private void RegisterKey() {
			if (account_key == null) {
				account_key = new RSACryptoServiceProvider(4096);
				signed_request(acme_url + "/acme/new-reg", new PmlDictionary() { { "resource", "new-reg" }, { "agreement", agreement_url } });
			}
		}

		public void AuthorizeDomains(String[] domains) {
			using (HTTPServer httpserver = new HTTPServer()) {
				httpserver.Listen(80);
				HTTPPathSelector httprouter = new HTTPPathSelector();
				httpserver.ContentProvider = httprouter;
				AuthorizeDomains(domains, httprouter);
			}
		}
		public void AuthorizeDomains(String[] domains, HTTPPathSelector httprouter) {
			AuthorizeDomains(domains, (String path, String value) => {
				if (value == null) httprouter.DeletePath(path);
				else httprouter.AddPath(path, new HTTPStaticContent(value, "/text/plain"));
			});
		}
		public void AuthorizeDomains(String[] domains, CreateHTTPChallengeCallback challenge_callback) {
			RegisterKey();
			RSAParameters account_key_params = account_key.ExportParameters(false);
			String thumbprint;
			using (SHA256 sha = SHA256.Create()) thumbprint = urlbase64(sha.ComputeHash(Encoding.UTF8.GetBytes(PmlJsonWriter.EncodeMessage(new PmlDictionary() {
				{ "e", urlbase64(account_key_params.Exponent) },
				{ "kty", "RSA" },
				{ "n", urlbase64(account_key_params.Modulus) }
			}))));
			foreach (String altname in domains) {
				Byte[] response_string = signed_request(acme_url + "/acme/new-authz", new PmlDictionary() { { "resource", "new-authz" }, { "identifier", new PmlDictionary() { { "type", "dns" }, { "value", altname } } } });
				PmlDictionary response = (PmlDictionary)PmlJsonReader.DecodeMessage(response_string);
				PmlCollection challenges = (PmlCollection)response["challenges"];
				PmlDictionary challenge = null;
				foreach (PmlDictionary item in challenges) if ((String)item["type"] == "http-01") challenge = item;
				String challenge_token = (String)challenge["token"];
				String challenge_uri = (String)challenge["uri"];
				String keyauth = challenge_token + "." + thumbprint;
				challenge_callback("/.well-known/acme-challenge/" + challenge_token, keyauth);
				try {
					response_string = signed_request(challenge_uri, new PmlDictionary() { { "resource", "challenge" }, { "keyAuthorization", keyauth } });
					response = (PmlDictionary)PmlJsonReader.DecodeMessage(response_string);
					while ((String)response["status"] == "pending") {
						Thread.Sleep(1000);
						using (WebClient wc = new WebClient()) response_string = wc.DownloadData(challenge_uri);
						response = (PmlDictionary)PmlJsonReader.DecodeMessage(response_string);
					}
				} finally {
					challenge_callback("/.well-known/acme-challenge/" + challenge_token, null);
				}
				if ((String)response["status"] != "valid") throw new InvalidOperationException("Challenge rejected for domain " + altname + " (" + response["status"] + ")");
			}
		}

		public X509Certificate2 GetCertificate(params String[] domains) {
			return GetCertificate(new RSACryptoServiceProvider(2048), domains);
		}
		public X509Certificate2 GetCertificate(RSACryptoServiceProvider key, params String[] domains) {
			AuthorizeDomains(domains);
			Byte[] csr = SSLUtils.GenerateCertificateSigningRequest(key, domains);
			Byte[] cert = signed_request(acme_url + "/acme/new-cert", new PmlDictionary() { { "resource", "new-cert" }, { "csr", urlbase64(csr) } });
			X509Certificate2 c = new X509Certificate2(cert);
			c.PrivateKey = key;
			return c;
		}
		public X509Certificate2 GetIssuerCertificate() {
			if (ca_cert != null) return ca_cert;
			Byte[] cert;
			using (WebClient wc = new WebClient()) cert = wc.DownloadData(ca_cert_url);
			return ca_cert = new X509Certificate2(cert);
		}
	}
}
