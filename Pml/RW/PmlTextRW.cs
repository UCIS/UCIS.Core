using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace UCIS.Pml {
	public class PmlTextWriter : IPmlWriter {
		private TextWriter pWriter;

		public static string GetMessageString(PmlElement Message) {
			StringWriter Buffer = new StringWriter();
			WriteMessageTo(Message, Buffer);
			return Buffer.ToString();
		}

		public PmlTextWriter(TextWriter Writer) {
			pWriter = Writer;
		}
		public PmlTextWriter(Stream Writer, Encoding Encoding) {
			pWriter = new StreamWriter(Writer, Encoding);
		}
		public PmlTextWriter(StringBuilder StringBuilder) {
			pWriter = new StringWriter(StringBuilder);
		}

		public TextWriter BaseWriter {
			get { return pWriter; }
			set { pWriter = value; }
		}

		public void WriteMessage(PmlElement Message) {
			WriteElementTo(Message, "", pWriter);
		}

		public static void WriteMessageTo(PmlElement Message, TextWriter Writer) {
			lock (Writer) {
				WriteElementTo(Message, "", Writer);
				Writer.Flush();
			}
		}

		private static void WriteElementTo(PmlElement Element, string Indent, TextWriter Writer) {
			if (Element == null) {
				Console.WriteLine("NULL");
				return;
			}
			switch (Element.Type) {
				case PmlType.Null:
					Writer.WriteLine("NULL");
					break;
				case PmlType.Collection:
					Writer.WriteLine("COLLECTION {");
					foreach (PmlElement Child in (PmlCollection)Element) {
						Writer.Write(Indent + " ");
						WriteElementTo(Child, Indent + " ", Writer);
					}

					Writer.WriteLine(Indent + "}");
					break;
				case PmlType.Dictionary:
					Writer.WriteLine("DICTIONARY {");
					foreach (KeyValuePair<string, PmlElement> Child in (PmlDictionary)Element) {
						Writer.Write(Indent + " " + Uri.EscapeDataString(Child.Key) + " ");
						WriteElementTo(Child.Value, Indent + " ", Writer);
					}

					Writer.WriteLine(Indent + "}");
					break;
				case PmlType.Binary:
					Writer.WriteLine("BINARY " + Convert.ToBase64String(Element.ToByteArray()));
					break;
				case PmlType.Integer:
					Writer.WriteLine("INT " + Element.ToString());
					break;
				case PmlType.String:
					Writer.WriteLine("STRING " + Uri.EscapeDataString(Element.ToString()));
					break;
			}
		}
	}
}
