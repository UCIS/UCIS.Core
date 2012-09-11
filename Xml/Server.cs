using System;
using System.Collections.Generic;
using System.Xml;
using UCIS.Net;

namespace UCIS.Xml {
	public class XmlServer : TCPServer.IModule {
		public Dictionary<string, IModule> Modules = new Dictionary<string, IModule>(StringComparer.OrdinalIgnoreCase);

		public bool Accept(TCPStream Stream) {
			XmlSocket XSocket = new XmlSocket(Stream);
			Stream.ReadTimeout = 5000;
			XmlDocument FirstMessage = XSocket.ReadDocument();
			if (FirstMessage == null) return true;
			IModule module;
			if (Modules.TryGetValue(FirstMessage.FirstChild.Name, out module)) {
				module.Accept(XSocket, FirstMessage);
			} else {
				Console.WriteLine("XMLServer.Accept: Module not found: " + FirstMessage.FirstChild.Name);
			}
			return true;
		}

		public interface IModule {
			void Accept(XmlSocket Socket, XmlDocument FirstMessage);
		}
	}
}
