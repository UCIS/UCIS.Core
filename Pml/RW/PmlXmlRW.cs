using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.IO;
using UCIS.Pml;

namespace UCIS.Pml {
	public class PmlXmlRW : IPmlRW {
		private PmlXmlReader pReader;
		private PmlXmlWriter pWriter;

		public PmlXmlRW(Stream Stream) {
			pReader = new PmlXmlReader(Stream);
			pWriter = new PmlXmlWriter(Stream);
		}
		public PmlXmlRW(Stream Stream, Encoding Encoding) {
			pReader = new PmlXmlReader(Stream);
			pWriter = new PmlXmlWriter(Stream, Encoding);
		}

		public PmlElement ReadMessage() {
			return pReader.ReadMessage();
		}

		public void WriteMessage(PmlElement Message) {
			pWriter.WriteMessage(Message);
		}
	}

	public class PmlXmlWriter : IPmlWriter {
		private Stream pStream;
		private XmlWriterSettings pXMLConfig;

		public PmlXmlWriter(Stream Stream, Encoding Encoding) {
			pXMLConfig = CreateXMLSettings(Encoding);

			pStream = Stream;
		}
		public PmlXmlWriter(Stream Stream)
			: this(Stream, Encoding.UTF8) {
		}

		private static XmlWriterSettings CreateXMLSettings() {
			return CreateXMLSettings(null);
		}
		private static XmlWriterSettings CreateXMLSettings(Encoding Encoding) {
			XmlWriterSettings XMLConfig = new XmlWriterSettings();
			if (Encoding == null) Encoding = Encoding.UTF8;
			XMLConfig.ConformanceLevel = ConformanceLevel.Document;
			XMLConfig.NewLineHandling = NewLineHandling.Entitize;
			XMLConfig.OmitXmlDeclaration = true;
			XMLConfig.CheckCharacters = true;
			XMLConfig.Encoding = Encoding;
			XMLConfig.CloseOutput = false;
			return XMLConfig;
		}

		public void WriteMessage(PmlElement Message) {
			WriteMessageToStream(Message, pStream);
			pStream.WriteByte(0);
			pStream.Flush();
		}

		public static void WriteMessageToFile(PmlElement Message, string Filename) {
			FileStream F = File.Create(Filename);
			WriteMessageToStream(Message, F);
			F.Close();
		}

		public static void WriteMessageToStream(PmlElement Message, Stream Stream) {
			WriteMessageToStream(Message, Stream, CreateXMLSettings());
		}

		public static void WriteMessageToStream(PmlElement Message, Stream Stream, XmlWriterSettings Settings) {
			XmlWriter Writer = System.Xml.XmlWriter.Create(Stream, Settings);
			Writer.WriteStartDocument();
			Writer.WriteStartElement("msg");
			WriteElementTo(Message, Writer);
			Writer.WriteEndElement();
			Writer.WriteEndDocument();
			Writer.Flush();
			Writer.Close();
		}

		private static void WriteElementTo(PmlElement Element, System.Xml.XmlWriter Writer) {
			switch (Element.Type) {
				case PmlType.Binary:
					Writer.WriteAttributeString("type", "binary");
					byte[] Bytes = Element.ToByteArray();
					Writer.WriteBase64(Bytes, 0, Bytes.Length);
					break;
				case PmlType.Collection:
					Writer.WriteAttributeString("type", "collection");
					foreach (PmlElement Child in (PmlCollection)Element) {
						Writer.WriteStartElement("item");
						WriteElementTo(Child, Writer);
						Writer.WriteEndElement();
					}

					break;
				case PmlType.Dictionary:
					Writer.WriteAttributeString("type", "dictionary");
					foreach (KeyValuePair<string, PmlElement> Child in (PmlDictionary)Element) {
						Writer.WriteStartElement(Child.Key);
						WriteElementTo(Child.Value, Writer);
						Writer.WriteEndElement();
					}

					break;
				case PmlType.Integer:
					Writer.WriteAttributeString("type", "integer");
					Writer.WriteString(Element.ToString());
					break;
				case PmlType.Null:
					Writer.WriteAttributeString("type", "null");
					break;
				case PmlType.String:
					Writer.WriteAttributeString("type", "string");
					Writer.WriteString(Element.ToString());
					break;
			}
		}
	}

	public class PmlXmlReader : IPmlReader {
		private BinaryReader pReader;
		private System.Xml.XmlReaderSettings pXMLSettings;

		public PmlXmlReader(Stream Stream)
			: this(new BinaryReader(Stream)) {
		}

		public PmlXmlReader(BinaryReader Reader) {
			pReader = Reader;
			pXMLSettings = new System.Xml.XmlReaderSettings();
			pXMLSettings.ConformanceLevel = System.Xml.ConformanceLevel.Document;
			pXMLSettings.CloseInput = true;
			pXMLSettings.IgnoreComments = true;
			pXMLSettings.IgnoreProcessingInstructions = true;
			pXMLSettings.IgnoreWhitespace = true;
			pXMLSettings.ValidationType = System.Xml.ValidationType.None;
			pXMLSettings.ValidationFlags = System.Xml.Schema.XmlSchemaValidationFlags.None;
			pXMLSettings.CheckCharacters = true;
		}

		public BinaryReader BaseReader {
			get { return pReader; }
			set { pReader = value; }
		}

		private XmlDocument ReadXMLDocument() {
			System.Xml.XmlDocument Doc = new System.Xml.XmlDocument();
			MemoryStream Buffer = default(MemoryStream);
			System.Xml.XmlReader XMLReader = default(System.Xml.XmlReader);
			byte B = 0;
			Buffer = new MemoryStream();
			do {
				B = pReader.ReadByte();
				if (B == 0) break;
				Buffer.WriteByte(B);
			}
			while (true);
			Buffer.Flush();
			Buffer.Seek(0, SeekOrigin.Begin);

			XMLReader = System.Xml.XmlReader.Create(Buffer, pXMLSettings);
			Doc.Load(XMLReader);
			XMLReader.Close();
			return Doc;
		}

		public PmlElement ReadMessage() {
			System.Xml.XmlDocument Doc = default(System.Xml.XmlDocument);
			Doc = ReadXMLDocument();
			if (Doc == null) return null;
			return ReadElement(Doc.FirstChild);
		}

		public static PmlElement ReadElement(System.Xml.XmlNode X) {
			PmlType pType;
			bool pTypeFound = false;
			pType = PmlType.Null;
			pTypeFound = true;
			if (X.Attributes != null && X.Attributes.Count > 0 && X.Attributes["type"] != null) {
				switch (X.Attributes["type"].Value.ToLowerInvariant()) {
					case "binary":
						pType = PmlType.Binary;
						break;
					case "collection":
						pType = PmlType.Collection;
						break;
					case "dictionary":
						pType = PmlType.Dictionary;
						break;
					case "string":
						pType = PmlType.String;
						break;
					case "null":
						pType = PmlType.Null;
						break;
					case "integer":
						pType = PmlType.Integer;
						break;
					default:
						pTypeFound = false;
						break;
				}
			} else {
				pTypeFound = false;
			}

			if (!pTypeFound) {
				if (X.HasChildNodes) {
					if (X.ChildNodes.Count == 1 && X.FirstChild.NodeType == System.Xml.XmlNodeType.Text) {
						Int64 dummy;
						UInt64 dummyu;
						if (Int64.TryParse(X.FirstChild.Value, out dummy) || UInt64.TryParse(X.FirstChild.Value, out dummyu)) {
							pType = PmlType.Integer;
						} else {
							pType = PmlType.String;
						}
					} else if (X.FirstChild.Name == "item") {
						pType = PmlType.Collection;
					} else {
						pType = PmlType.Dictionary;
					}
				} else {
					pType = PmlType.Null;
				}
			}

			switch (pType) {
				case PmlType.Null:
					return new PmlNull();
				case PmlType.Binary:
					if (X.FirstChild == null) {
						return new PmlBinary(new byte[0]);
					} else {
						return new PmlBinary(Convert.FromBase64String(X.FirstChild.Value));
					}
				case PmlType.Integer:
					return new PmlInteger(X.FirstChild.Value);
				case PmlType.String:
					if (X.FirstChild == null) {
						return new PmlString("");
					} else {
						return new PmlString(X.FirstChild.Value);
					}
				case PmlType.Collection:
					PmlCollection C = new PmlCollection();
					foreach (XmlNode N in X.ChildNodes) {
						C.Add(ReadElement(N));
					}

					return C;
				case PmlType.Dictionary:
					PmlDictionary D = new PmlDictionary();
					foreach (XmlNode N in X.ChildNodes) {
						D.Add(N.Name, ReadElement(N));
					}

					return D;
				default:
					return null;
			}
		}
	}
}