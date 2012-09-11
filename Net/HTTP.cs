using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace UCIS.Net.HTTP {
	public class HTTPServer : TCPServer.IModule {
		public Dictionary<string, HTTPContent> Content = new Dictionary<string, HTTPContent>(StringComparer.InvariantCultureIgnoreCase);
		public HTTPContent DefaultContent = null;

		public bool Accept(TCPStream Stream) {
			StreamReader StreamReader = new StreamReader(Stream, Encoding.ASCII);
			String Line = StreamReader.ReadLine();
			String[] Request = Line.Split(' ');

			//Console.WriteLine("HTTP.Server.Accept Request: " + Line);

			if (Request == null || Request.Length < 2 || Request[0] != "GET" || Request[1][0] != '/') {
				//Console.WriteLine("HTTP.Server.Start Bad request");
				SendError(Stream, 400);
				return true;
			}

			Request = Request[1].Split(new Char[] { '?' }, 2);
			HTTPContent content;
			if (Content.TryGetValue(Request[0], out content)) {
			} else if (DefaultContent != null) {
				content = DefaultContent;
			} else {
				SendError(Stream, 404);
				return true;
			}
			HTTPContext Context = new HTTPContext();
			Context.Stream = Stream;
			Context.Request = new HTTPRequest();
			Context.Request.Method = Method.GET;
			Context.Request.Path = Request[0];
			if (Request.Length == 2) {
				Context.Request.Query = Request[1];
			} else {
				Context.Request.Query = null;
			}
			HTTPContent.Result ServeResult = content.Serve(Context);

			if (ServeResult == HTTPContent.Result.OK_KEEPALIVE) {
				return false;
			} else if (!(ServeResult == HTTPContent.Result.OK_CLOSE)) {
				SendError(Stream, (int)ServeResult);
			}
			return true;
		}

		public static void SendError(Stream Stream, int ErrorCode) {
			string ErrorText = null;
			switch (ErrorCode) {
				case 400:
					ErrorText = "The request could not be understood by the server due to malformed syntax.";
					break;
				case 404:
					ErrorText = "The requested file can not be found.";
					break;
				default:
					ErrorText = "Unknown error code: " + ErrorCode.ToString() + ".";
					break;
			}
			SendHeader(Stream, ErrorCode, "text/plain", ErrorText.Length);
			WriteLine(Stream, ErrorText);
			Thread.Sleep(100);
			return;
		}

		public static void SendHeader(Stream Stream, int ResultCode, string ContentType) {
			SendHeader(Stream, ResultCode, ContentType, -1);
		}
		public static void SendHeader(Stream Stream, int ResultCode, string ContentType, int ContentLength) {
			//ResultCode = 200, ContentType = null, ContentLength = -1
			string ResultString;
			switch (ResultCode) {
				case 200: ResultString = "OK"; break;
				case 400: ResultString = "Bad Request"; break;
				case 404: ResultString = "Not found"; break;
				default: ResultString = "Unknown"; break;
			}
			WriteLine(Stream, "HTTP/1.1 " + ResultCode.ToString() + " " + ResultString);
			WriteLine(Stream, "Expires: Mon, 26 Jul 1990 05:00:00 GMT");
			WriteLine(Stream, "Cache-Control: no-store, no-cache, must-revalidate");
			WriteLine(Stream, "Cache-Control: post-check=0, pre-check=0");
			WriteLine(Stream, "Pragma: no-cache");
			WriteLine(Stream, "Server: UCIS Simple Webserver");
			WriteLine(Stream, "Connection: Close");
			if ((ContentType != null)) WriteLine(Stream, "Content-Type: " + ContentType);
			if (ContentLength != -1) WriteLine(Stream, "Content-Length: " + ContentLength.ToString());
			WriteLine(Stream, "");
		}

		public static void WriteLine(Stream Stream, string Line) {
			byte[] Buffer = null;
			Buffer = Encoding.ASCII.GetBytes(Line);
			Stream.Write(Buffer, 0, Buffer.Length);
			Stream.WriteByte(13);
			Stream.WriteByte(10);
		}
	}

	public abstract class HTTPContent {
		public abstract Result Serve(HTTPContext Context);
		public enum Result : int {
			OK_CLOSE = -2,
			OK_KEEPALIVE = -1,
			ERR_NOTFOUND = 404
		}
	}

	public class HTTPFileContent : HTTPContent {
		public string Filename { get; private set; }
		public string ContentType { get; private set; }

		public HTTPFileContent(string Filename) : this(Filename, "application/octet-stream") { }
		public HTTPFileContent(string Filename, string ContentType) {
			this.Filename = Filename;
			this.ContentType = ContentType;
		}

		public override Result Serve(HTTPContext Context) {
			if (!File.Exists(Filename)) return Result.ERR_NOTFOUND;

			using (FileStream FileStream = File.OpenRead(Filename)) {
				HTTPServer.SendHeader(Context.Stream, 200, ContentType, (int)FileStream.Length);
				byte[] Buffer = new byte[1025];
				while (FileStream.CanRead) {
					int Length = FileStream.Read(Buffer, 0, Buffer.Length);
					if (Length <= 0) break;
					Context.Stream.Write(Buffer, 0, Length);
				}
			}
			return Result.OK_CLOSE;
		}
	}

	public class HTTPContext {
		public HTTPRequest Request { get; internal set; }
		public TCPStream Stream { get; internal set; }
	}

	public enum Method {
		GET = 0,
		HEAD = 1,
		POST = 2,
		PUT = 3
	}

	public class HTTPRequest {
		public HTTP.Method Method { get; internal set; }
		public string Path { get; internal set; }
		public string Query { get; internal set; }
	}
}
