using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace UCIS.Net.HTTP {
	public interface IHTTPContext {
		Boolean AllowGzipCompression { get; set; }
		Boolean AsynchronousCompletion { get; set; }
		Stream GetDirectStream();
		int HTTPVersion { get; set; }
		Boolean IsSecure { get; }
		Boolean KeepAlive { get; set; }
		EndPoint LocalEndPoint { get; }
		EndPoint RemoteEndPoint { get; }
		String RequestMethod { get; }
		String RequestPath { get; }
		String RequestQuery { get; }
		HTTPServer Server { get; }
		ISocket Socket { get; }
		TCPStream TCPStream { get; }
		HTTPResponse Response { get; }
		HTTPRequestHeaderCollection RequestHeaders { get; }
		HTTPRequestBody RequestBody { get; }
		HTTPQueryParametersCollection QueryParameters { get; }
		void Close(int errorCode);
	}
	public class HTTPContextWrapper : IHTTPContext {
		IHTTPContext inner;
		public HTTPContextWrapper(IHTTPContext inner) { this.inner = inner; }
		public virtual IHTTPContext PreviousContext { get { return inner; } }
		public virtual bool AllowGzipCompression { get { return inner.AllowGzipCompression; } set { inner.AllowGzipCompression = value; } }
		public virtual bool AsynchronousCompletion { get { return inner.AsynchronousCompletion; } set { inner.AsynchronousCompletion = value; } }
		public virtual Stream GetDirectStream() { return inner.GetDirectStream(); }
		public virtual int HTTPVersion { get { return inner.HTTPVersion; } set { inner.HTTPVersion = value; } }
		public virtual bool IsSecure { get { return inner.IsSecure; } }
		public virtual bool KeepAlive { get { return inner.KeepAlive; } set { inner.KeepAlive = value; } }
		public virtual EndPoint LocalEndPoint { get { return inner.LocalEndPoint; } }
		public virtual EndPoint RemoteEndPoint { get { return inner.RemoteEndPoint; } }
		public virtual string RequestMethod { get { return inner.RequestMethod; } }
		public virtual string RequestPath { get { return inner.RequestPath; } }
		public virtual string RequestQuery { get { return inner.RequestQuery; } }
		public virtual HTTPServer Server { get { return inner.Server; } }
		public virtual ISocket Socket { get { return inner.Socket; } }
		public virtual TCPStream TCPStream { get { return inner.TCPStream; } }
		public virtual HTTPRequestHeaderCollection RequestHeaders { get { return inner.RequestHeaders; } }
		public virtual HTTPRequestBody RequestBody { get { return inner.RequestBody; } }
		public virtual HTTPResponse Response { get { return inner.Response; } }
		public virtual HTTPQueryParametersCollection QueryParameters { get { return inner.QueryParameters; } }
		public virtual void Close(int errorCode) { inner.Close(errorCode); }
	}
}
