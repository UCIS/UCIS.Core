using System;
using System.Text;
using System.IO;
using System.Xml;

namespace UCIS.Xml {
	public class XmlSocket : XmlWriter {
		Stream pStream;
		protected XmlWriter pWriter;

		public Stream BaseStream { get { return pStream; } }

		public override XmlWriterSettings Settings { get { return WriterSettings; } }
		public XmlWriterSettings WriterSettings { get; private set; }
		public XmlReaderSettings ReaderSettings { get; private set; }

		public XmlSocket(Stream Stream, Encoding Encoding) {
			WriterSettings = new XmlWriterSettings();
			WriterSettings.ConformanceLevel = ConformanceLevel.Document;
			WriterSettings.NewLineHandling = NewLineHandling.Entitize;
			WriterSettings.OmitXmlDeclaration = true;
			WriterSettings.CheckCharacters = false;
			WriterSettings.Encoding = Encoding;
			WriterSettings.CloseOutput = false;

			ReaderSettings = new XmlReaderSettings();
			ReaderSettings.ConformanceLevel = ConformanceLevel.Document;
			ReaderSettings.CloseInput = true;
			ReaderSettings.IgnoreComments = true;
			ReaderSettings.IgnoreProcessingInstructions = true;
			ReaderSettings.IgnoreWhitespace = true;
			ReaderSettings.ValidationType = ValidationType.None;
			ReaderSettings.ValidationFlags = System.Xml.Schema.XmlSchemaValidationFlags.None;
			ReaderSettings.CheckCharacters = false;

			pStream = Stream;
		}
		public XmlSocket(Stream Stream) : this(Stream, new UTF8Encoding(false)) { }

		public virtual MemoryStream ReadRawDocument() {
			MemoryStream Buffer = new MemoryStream();
			byte[] ByteBuffer = new byte[1];
			int ByteCount = 0;
			while (true) {
				ByteCount = pStream.Read(ByteBuffer, 0, 1);
				if (ByteCount == 0) {
					throw new EndOfStreamException();
				} else if (ByteBuffer[0] == 0) {
					if (Buffer.Length > 0) break;
				} else {
					Buffer.WriteByte(ByteBuffer[0]);
				}
			}
			Buffer.Flush();
			Buffer.Seek(0, SeekOrigin.Begin);

			return Buffer;
		}
		public virtual XmlDocument ReadDocument() {
			MemoryStream Buffer = ReadRawDocument();
			XmlDocument Doc = new XmlDocument();
			try {
				using (XmlReader XMLReader = XmlReader.Create(Buffer, ReaderSettings)) Doc.Load(XMLReader);
			} catch (Exception ex) {
				Buffer.Seek(0, SeekOrigin.Begin);
				throw new IOException("Could not parse XML document: \"" + Encoding.UTF8.GetString(Buffer.ToArray()) + "\"", ex);
			}
			return Doc;
		}

		protected virtual void CreateWriter() {
			pWriter = XmlWriter.Create(pStream, WriterSettings);
		}
		protected virtual void CloseWriter() {
			pWriter.Flush();
			pWriter.Close();
			pStream.WriteByte(0);
			pStream.Flush();
		}
		public override void Flush() {
			if (pWriter != null) {
				pWriter.Flush();
			} else {
				pStream.Flush();
			}
		}
		public override void Close() {
			if (pWriter != null) {
				pWriter.Close();
				pWriter = null;
			}
			pStream.Close();
		}

		public void WriteDocument(XmlDocument Document) {
			CreateWriter();
			Document.WriteTo(pWriter);
			CloseWriter();
		}

		public virtual void WriteRawDocument(string data) {
			byte[] Buffer = WriterSettings.Encoding.GetBytes(data);
			pStream.Write(Buffer, 0, Buffer.Length);
			pStream.WriteByte(0);
			pStream.Flush();
		}

		public override void WriteStartDocument() {
			CreateWriter();
			pWriter.WriteStartDocument();
		}
		public override void WriteStartDocument(bool standalone) {
			CreateWriter();
			pWriter.WriteStartDocument(standalone);
		}
		public override void WriteEndDocument() {
			pWriter.WriteEndDocument();
			CloseWriter();
		}
		public void WriteDocumentString(string localName, string value) {
			WriteStartDocument();
			WriteElementString(localName, value);
			WriteEndDocument();
		}

		public override System.Xml.WriteState WriteState {
			get {
				if (pWriter != null) {
					return pWriter.WriteState;
				} else {
					return WriteState.Start;
				}
			}
		}

		public override string LookupPrefix(string ns) {
			return pWriter.LookupPrefix(ns);
		}
		public override void WriteBase64(byte[] buffer, int index, int count) {
			pWriter.WriteBase64(buffer, index, count);
		}
		public override void WriteCData(string text) {
			pWriter.WriteCData(text);
		}
		public override void WriteCharEntity(char ch) {
			pWriter.WriteCharEntity(ch);
		}
		public override void WriteChars(char[] buffer, int index, int count) {
			pWriter.WriteChars(buffer, index, count);
		}
		public override void WriteComment(string text) {
			pWriter.WriteComment(text);
		}
		public override void WriteDocType(string name, string pubid, string sysid, string subset) {
			pWriter.WriteDocType(name, pubid, sysid, subset);
		}
		public override void WriteEndAttribute() {
			pWriter.WriteEndAttribute();
		}
		public override void WriteEndElement() {
			pWriter.WriteEndElement();
		}
		public override void WriteEntityRef(string name) {
			pWriter.WriteEntityRef(name);
		}
		public override void WriteFullEndElement() {
			pWriter.WriteFullEndElement();
		}
		public override void WriteProcessingInstruction(string name, string text) {
			pWriter.WriteProcessingInstruction(name, text);
		}
		public override void WriteRaw(char[] buffer, int index, int count) {
			pWriter.WriteRaw(buffer, index, count);
		}
		public override void WriteRaw(string data) {
			pWriter.WriteRaw(data);
		}
		public override void WriteStartAttribute(string prefix, string localName, string ns) {
			pWriter.WriteStartAttribute(prefix, localName, ns);
		}
		public override void WriteStartElement(string prefix, string localName, string ns) {
			pWriter.WriteStartElement(prefix, localName, ns);
		}
		public override void WriteString(string text) {
			pWriter.WriteString(text);
		}
		public override void WriteSurrogateCharEntity(char lowChar, char highChar) {
			pWriter.WriteSurrogateCharEntity(lowChar, highChar);
		}
		public override void WriteWhitespace(string ws) {
			pWriter.WriteWhitespace(ws);
		}
	}
}
