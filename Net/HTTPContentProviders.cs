using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UCIS.Util;

namespace UCIS.Net.HTTP {
	public interface IHTTPContentProvider {
		void ServeRequest(IHTTPContext context);
	}
	public delegate void HTTPContentProviderDelegate(IHTTPContext context);
	public class HTTPContentProviderFunction : IHTTPContentProvider {
		public HTTPContentProviderDelegate Handler { get; private set; }
		public HTTPContentProviderFunction(HTTPContentProviderDelegate handler) {
			this.Handler = handler;
		}
		public void ServeRequest(IHTTPContext context) {
			Handler(context);
		}
	}
	public class HTTPPathSelector : IHTTPContentProvider, IEnumerable<KeyValuePair<String, IHTTPContentProvider>> {
		private struct PrefixInfo {
			public String Prefix;
			public IHTTPContentProvider Handler;
			public Boolean ExactMatch;
		}
		private List<PrefixInfo> Prefixes;
		private StringComparison PrefixComparison;
		public HTTPPathSelector() : this(false) { }
		public HTTPPathSelector(Boolean caseSensitive) {
			Prefixes = new List<PrefixInfo>();
			PrefixComparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
		}
		public void Add(String path, IHTTPContentProvider contentProvider) {
			AddPrefix(new PrefixInfo() { Prefix = path.TrimEnd('*'), Handler = contentProvider, ExactMatch = !path.EndsWith("*") });
		}
		public void AddPrefix(String prefix, IHTTPContentProvider contentProvider) {
			AddPrefix(new PrefixInfo() { Prefix = prefix, Handler = contentProvider, ExactMatch = false });
		}
		public void AddPath(String path, IHTTPContentProvider contentProvider) {
			AddPrefix(new PrefixInfo() { Prefix = path, Handler = contentProvider, ExactMatch = true });
		}
		private void AddPrefix(PrefixInfo item) {
			Prefixes.Add(item);
			Prefixes.Sort(delegate(PrefixInfo a, PrefixInfo b) { return -String.CompareOrdinal(a.Prefix, b.Prefix); });
		}
		public void DeletePrefix(String prefix) {
			Prefixes.RemoveAll(delegate(PrefixInfo item) { return prefix.Equals(item.Prefix, PrefixComparison); });
		}
		public void DeletePath(String prefix) {
			DeletePrefix(prefix);
		}
		public void ServeRequest(IHTTPContext context) {
			PrefixInfo c = Prefixes.Find(delegate(PrefixInfo item) {
				if (item.ExactMatch) return context.RequestPath.Equals(item.Prefix, PrefixComparison);
				else return context.RequestPath.StartsWith(item.Prefix, PrefixComparison);
			});
			if (c.Handler != null) {
				c.Handler.ServeRequest(context);
			} else {
				context.Response.SendErrorResponse(404);
			}
		}

		public IEnumerator<KeyValuePair<String, IHTTPContentProvider>> GetEnumerator() {
			return Prefixes.ConvertAll(delegate(PrefixInfo item) { return new KeyValuePair<String, IHTTPContentProvider>(item.ExactMatch ? item.Prefix : item.Prefix + "*", item.Handler); }).GetEnumerator();
		}
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}
	}
	public class HTTPStaticContent : IHTTPContentProvider {
		public ArraySegment<Byte> ContentBuffer { get; set; }
		public String ContentType { get; set; }
		public HTTPStaticContent() : this(new ArraySegment<Byte>()) { }
		public HTTPStaticContent(ArraySegment<Byte> content) : this(content, "application/octet-stream") { }
		public HTTPStaticContent(String content, String contentType) : this(Encoding.UTF8.GetBytes(content), contentType) { }
		public HTTPStaticContent(String contentType) : this(new ArraySegment<Byte>(), contentType) { }
		public HTTPStaticContent(Byte[] content, String contentType) : this(new ArraySegment<Byte>(content), contentType) { }
		public HTTPStaticContent(ArraySegment<Byte> content, String contentType) {
			this.ContentBuffer = content;
			this.ContentType = contentType;
		}
		public void SetContent(Byte[] bytes) { ContentBuffer = new ArraySegment<byte>(bytes); }
		public void SetContent(Byte[] bytes, int offset, int count) { ContentBuffer = new ArraySegment<byte>(bytes, offset, count); }
		public void SetContent(String content, Encoding encoding) { SetContent(encoding.GetBytes(content)); }
		public void SetContent(String content) { SetContent(content, Encoding.UTF8); }
		public void ServeRequest(IHTTPContext context) {
			ArraySegment<Byte> content = ContentBuffer;
			if (content.Array == null) {
				context.Response.SendErrorResponse(404);
				return;
			}
			String contentType = ContentType;
			context.Response.SendStatus(200);
			if (contentType != null) context.Response.SendHeader("Content-Type", contentType);
			context.Response.WriteResponseData(content.Array, content.Offset, content.Count);
		}
	}
	public class HTTPFileProvider : IHTTPContentProvider {
		public String FileName { get; private set; }
		public String ContentType { get; private set; }
		public HTTPFileProvider(String fileName) : this(fileName, HTTPServer.GetMimeTypeForExtension(fileName) ?? "application/octet-stream") { }
		public HTTPFileProvider(String fileName, String contentType) {
			this.FileName = fileName;
			this.ContentType = contentType;
		}
		public void ServeRequest(IHTTPContext context) {
			SendFile(context, FileName, ContentType);
		}
		public static void SendFile(IHTTPContext context, String filename) {
			SendFile(context, filename, null);
		}
		public static void SendFile(IHTTPContext context, String filename, String contentType) {
			if (!File.Exists(filename)) {
				context.Response.SendErrorResponse(404);
				return;
			}
			String lastModified = File.GetLastWriteTimeUtc(filename).ToString("R");
			if (context.RequestHeaders["If-Modified-Since"] == lastModified) {
				context.Response.SendStatus(304);
				return;
			}
			if (contentType == null) contentType = HTTPServer.GetMimeTypeForExtension(Path.GetExtension(filename));
			using (FileStream fs = File.OpenRead(filename)) {
				context.Response.SendStatus(200);
				if (!String.IsNullOrEmpty(contentType)) context.Response.SendHeader("Content-Type", contentType);
				context.Response.SendHeader("Last-Modified", lastModified);
				context.Response.WriteResponseData(fs);
			}
		}
	}
	public class HTTPUnTarchiveProvider : IHTTPContentProvider {
		public String TarFileName { get; private set; }
		public HTTPUnTarchiveProvider(String tarFile) {
			this.TarFileName = tarFile;
		}
		public void ServeRequest(IHTTPContext context) {
			if (!File.Exists(TarFileName)) {
				context.Response.SendErrorResponse(404);
				return;
			}
			String reqname1 = context.RequestPath;
			if (reqname1.StartsWith("/")) reqname1 = reqname1.Substring(1);
			String reqname2 = reqname1;
			//Todo: use index.htm only if path ends in /; if path does not end in / and path is a directory, send 302 redirect.
			if (reqname2.Length > 0 && !reqname2.EndsWith("/")) reqname2 += "/";
			reqname2 += "index.htm";
			foreach (TarchiveEntry file in new TarchiveReader(TarFileName)) {
				if (!file.IsFile) continue;
				if (!reqname1.Equals(file.Name, StringComparison.OrdinalIgnoreCase) && !reqname2.Equals(file.Name, StringComparison.OrdinalIgnoreCase)) continue;
				context.Response.SendStatus(200);
				String ctype = HTTPServer.GetMimeTypeForExtension(Path.GetExtension(file.Name));
				if (ctype != null) context.Response.SendHeader("Content-Type", ctype);
				using (Stream source = file.GetStream()) context.Response.WriteResponseData(source);
				return;
			}
			context.Response.SendErrorResponse(404);
		}
	}
}
