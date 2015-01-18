using System;
using System.Net;

namespace UCIS.Net {
	public interface INetworkConnection {
		event EventHandler Closed;
		void Close();
		bool Connected { get; }
		ulong BytesWritten { get; }
		ulong BytesRead { get; }
		TimeSpan Age { get; }
		EndPoint RemoteEndPoint { get; }
		Object Handler { get; }
	}
}
