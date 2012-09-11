using System;
using System.Collections.Generic;
using System.Text;
using UCIS.Net;

namespace UCIS.Xml {
	public class XmlPolicyFile : XmlServer.IModule {
		private string[] pHosts;
		private string pPorts;

		public XmlPolicyFile(string[] Hosts) : this(Hosts, null) { }
		public XmlPolicyFile(string[] Hosts, int[] Ports) {
			pHosts = Hosts;
			if (Ports != null) {
				pPorts = "";
				foreach (int Port in Ports) {
					pPorts += "," + Port.ToString();
				}
				pPorts = pPorts.Substring(1);
			} else {
				pPorts = null;
			}
		}

		public void Accept(XmlSocket Socket, System.Xml.XmlDocument FirstMessage) {
			Socket.WriterSettings.OmitXmlDeclaration = false;

			Socket.WriteStartDocument();
			Socket.WriteDocType("cross-domain-policy", null, "http://www.macromedia.com/xml/dtds/cross-domain-policy.dtd", null);
			Socket.WriteStartElement("cross-domain-policy");

			foreach (string Host in pHosts) {
				Socket.WriteStartElement("allow-access-from");
				Socket.WriteAttributeString("domain", Host);
				if (pPorts != null) Socket.WriteAttributeString("to-ports", pPorts);
				Socket.WriteEndElement();
			}

			Socket.WriteEndElement();
			Socket.WriteEndDocument();
		}
	}
}
