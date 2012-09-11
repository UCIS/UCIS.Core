using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace UCIS.Pml {
	public class PmlPHPWriter : IPmlWriter {
		private TextWriter pWriter;

		public static string GetMessageString(PmlElement Message) {
			StringWriter Buffer = new StringWriter();
			WriteMessageTo(Message, Buffer);
			return Buffer.ToString();
		}

		public PmlPHPWriter(TextWriter Writer) {
			pWriter = Writer;
		}
		public PmlPHPWriter(Stream Writer, Encoding Encoding) {
			pWriter = new StreamWriter(Writer, Encoding);
		}
		public PmlPHPWriter(StringBuilder StringBuilder) {
			pWriter = new StringWriter(StringBuilder);
		}

		public TextWriter BaseWriter {
			get { return pWriter; }
			set { pWriter = value; }
		}

		public void WriteMessage(PmlElement Message) {
			WriteMessageTo(Message, pWriter);
		}

		public static void WriteMessageTo(PmlElement Message, TextWriter Writer) {
			lock (Writer) {
				WriteElementTo(Message, Writer);
				Writer.Flush();
			}
		}

		private static void WriteElementTo(PmlElement Element, TextWriter Writer) {
			string Str;
			if (Element == null) {
				Writer.Write("N;");
				return;
			}
			switch (Element.Type) {
				case PmlType.Null:
					Writer.Write("N;");
					break;
				case PmlType.String:
				case PmlType.Binary:
					Str = Element.ToString();
					Writer.Write("s:");
					Writer.Write(Encoding.UTF8.GetByteCount(Str).ToString());
					Writer.Write(":\"");
					Writer.Write(Str);
					Writer.Write("\";");
					break;
				case PmlType.Integer:
					Writer.Write("i:");
					Writer.Write(Element.ToString());
					Writer.Write(";");
					break;
				case PmlType.Dictionary:
					Writer.Write("a:");
					Writer.Write(((IDictionary<string, PmlElement>)Element).Count.ToString());
					Writer.Write(":{");
					foreach (KeyValuePair<string, PmlElement> Child in (IDictionary<string, PmlElement>)Element) {
						Str = Child.Key;
						Writer.Write("s:");
						Writer.Write(Encoding.UTF8.GetByteCount(Str).ToString());
						Writer.Write(":\"");
						Writer.Write(Str);
						Writer.Write("\";");
						WriteElementTo(Child.Value, Writer);
					}

					Writer.Write("}");
					break;
				case PmlType.Collection:
					int I = 0;
					Writer.Write("a:");
					Writer.Write(((ICollection<PmlElement>)Element).Count.ToString());
					Writer.Write(":{");
					foreach (PmlElement Child in (ICollection<PmlElement>)Element) {
						Writer.Write("i:");
						Writer.Write(I.ToString());
						Writer.Write(";");
						WriteElementTo(Child, Writer);
						I += 1;
					}

					Writer.Write("}");
					break;
				default:
					Writer.Write("N;");
					break;
			}
		}
	}
	public class PmlPHPReader : IPmlReader {
		private TextReader pReader;

		public static PmlElement GetMessageFromString(string Data) {
			StringReader Buffer = new StringReader(Data);
			return ReadMessageFrom(Buffer);
		}

		public PmlPHPReader(TextReader Reader) {
			pReader = Reader;
		}
		public PmlPHPReader(Stream Reader, Encoding Encoding) {
			pReader = new StreamReader(Reader, Encoding);
		}
		public PmlPHPReader(string Data) {
			pReader = new StringReader(Data);
		}

		public TextReader BaseReader {
			get { return pReader; }
			set { pReader = value; }
		}

		public PmlElement ReadMessage() {
			return ReadMessageFrom(pReader);
		}

		public static PmlElement ReadMessageFrom(TextReader Reader) {
			lock (Reader) {
				return ReadElementFrom(Reader);
			}
		}

		public class PhpSerializerException : Exception {

			public PhpSerializerException(int Position, char Read, char Expected)
				: base("At position " + Position.ToString() + " expected " + Expected + " but got " + Read) {
			}
		}

		private static char ReadChar(TextReader Reader) {
			char[] Buffer = new char[1];
			if (Reader.Read(Buffer, 0, 1) != 1) throw new EndOfStreamException();
			return Buffer[0];
		}

		private static string ReadNumber(TextReader Reader, char TerminatedBy) {
			char Buffer = '\0';
			string S = "";
			do {
				Buffer = ReadChar(Reader);
				if (Buffer == TerminatedBy) break;
				if (Buffer < '0' && Buffer > '9' && Buffer != '.' && Buffer != ',' && Buffer != '-' && Buffer != 'e')
					throw new PhpSerializerException(0, Buffer, TerminatedBy);
				S += Buffer;
			}
			while (true);
			return S;
		}

		private static void ReadExpect(TextReader Reader, char Expect) {
			char Read = '\0';
			Read = ReadChar(Reader);
			if (Read != Expect) throw new PhpSerializerException(0, Read, Expect);
		}

		private static PmlElement ReadElementFrom(TextReader Reader) {
			char Type = '\0';
			char Read = '\0';
			int KL;
			Type = ReadChar(Reader);
			switch (Type) {
				case 'N':
					ReadExpect(Reader, ';');
					return new PmlNull();
				case 'i':
					ReadExpect(Reader, ':');
					return new PmlInteger(ReadNumber(Reader, ';'));
				case 'd':
					ReadExpect(Reader, ':');
					return new PmlString(ReadNumber(Reader, ';'));
				//Return New PML.PMLNumber(ReadNumber(Reader, ";"c))
				case 's':
					KL = 0;
					char[] Buffer = null;
					ReadExpect(Reader, ':');
					KL = int.Parse(ReadNumber(Reader, ':'));
					Buffer = new Char[KL];
					ReadExpect(Reader, '"');
					Reader.ReadBlock(Buffer, 0, KL);
					ReadExpect(Reader, '"');
					ReadExpect(Reader, ';');
					return new PmlString(new String(Buffer));
				case 'a':
					PmlDictionary D = new PmlDictionary();
					int I = 0;
					int L = 0;
					KL = 0;
					char[] K = null;
					ReadExpect(Reader, ':');
					L = int.Parse(ReadNumber(Reader, ':'));
					ReadExpect(Reader, '{');
					for (I = 1; I <= L; I++) {
						Read = ReadChar(Reader);
						switch (Read) {
							case 'i':
								ReadExpect(Reader, ':');
								K = ReadNumber(Reader, ';').ToCharArray();
								break;
							case 's':
								ReadExpect(Reader, ':');
								KL = int.Parse(ReadNumber(Reader, ':'));
								K = new char[KL];
								ReadExpect(Reader, '"');
								Reader.ReadBlock(K, 0, KL);
								ReadExpect(Reader, '"');
								ReadExpect(Reader, ';');
								break;
							case 'd':
								ReadExpect(Reader, ':');
								K = ReadNumber(Reader, ';').ToCharArray();
								break;
							default:
								throw new NotSupportedException("Only integer and string keys are supported, got: " + Read);
						}
						D.Add(new String(K), ReadElementFrom(Reader));
					}

					ReadExpect(Reader, '}');
					return D;
				default:
					throw new NotSupportedException("Unknown type: " + Type);
			}
		}
	}
}