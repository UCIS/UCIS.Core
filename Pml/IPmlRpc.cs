using System;
using System.Collections.Generic;

namespace UCIS.Pml {
	interface IPmlRpcServer {
		IDictionary<string, Delegate> ExportedMethods { get; }
		IDictionary<string, Object> ExportedObjects { get; }
	}
	interface IPmlRpcClient {
		void Call(String method, params Object[] args);
		void Invoke(String method, params Object[] args);
		void BeginInvoke(String method, Object[] args, AsyncCallback callback, Object state);
		Object EndInvoke(IAsyncResult result);
	}
}
