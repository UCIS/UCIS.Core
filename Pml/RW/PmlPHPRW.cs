using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace UCIS.Pml {
	public class PmlPHPWriter : IPmlWriter {
		private Stream stream;
		private Encoding encoding;

		public static Byte[] EncodeMessage(PmlElement message) {
			using (MemoryStream stream = new MemoryStream()) {
				WriteMessageTo(message, stream, Encoding.UTF8);
				return stream.ToArray();
			}
		}

		public PmlPHPWriter(Stream stream) {
			this.stream = stream;
			this.encoding = Encoding.UTF8;
		}
		public PmlPHPWriter(Stream stream, Encoding encoding) {
			this.stream = stream;
			this.encoding = encoding;
		}

		public Stream Stream { get { return stream; } }

		public void WriteMessage(PmlElement message) {
			WriteMessageTo(message, stream, encoding);
		}

		public static void WriteMessageTo(PmlElement message, Stream stream, Encoding encoding) {
			lock (stream) {
				WriteElementTo(message, stream, encoding);
				stream.Flush();
			}
		}

		private static void WriteString(Stream stream, Encoding encoding, String str) {
			Byte[] bytes = encoding.GetBytes(str);
			stream.Write(bytes, 0, bytes.Length);
		}

		private static void WriteElementTo(PmlElement element, Stream stream, Encoding encoding) {
			if (element == null) {
				WriteString(stream, encoding, "N;");
				return;
			}
			switch (element.Type) {
				case PmlType.Null:
					WriteString(stream, encoding, "N;");
					break;
				case PmlType.String:
					Byte[] bytes = encoding.GetBytes(element.ToString());
					WriteString(stream, encoding, "s:");
					WriteString(stream, encoding, bytes.Length.ToString());
					WriteString(stream, encoding, ":\"");
					stream.Write(bytes, 0, bytes.Length);
					WriteString(stream, encoding, "\";");
					break;
				case PmlType.Binary:
					bytes = element.ToByteArray();
					WriteString(stream, encoding, "s:");
					WriteString(stream, encoding, bytes.Length.ToString());
					WriteString(stream, encoding, ":\"");
					stream.Write(bytes, 0, bytes.Length);
					WriteString(stream, encoding, "\";");
					break;
				case PmlType.Integer:
					WriteString(stream, encoding, "i:");
					WriteString(stream, encoding, element.ToString());
					WriteString(stream, encoding, ";");
					break;
				case PmlType.Dictionary:
					IDictionary<String, PmlElement> dict = (IDictionary<String, PmlElement>)element;
					WriteString(stream, encoding, "a:");
					WriteString(stream, encoding, dict.Count.ToString());
					WriteString(stream, encoding, ":{");
					foreach (KeyValuePair<String, PmlElement> item in dict) {
						bytes = encoding.GetBytes(item.Key);
						WriteString(stream, encoding, "s:");
						WriteString(stream, encoding, bytes.Length.ToString());
						WriteString(stream, encoding, ":\"");
						stream.Write(bytes, 0, bytes.Length);
						WriteString(stream, encoding, "\";");
						WriteElementTo(item.Value, stream, encoding);
					}
					WriteString(stream, encoding, "}");
					break;
				case PmlType.Collection:
					ICollection<PmlElement> col = (ICollection<PmlElement>)element;
					WriteString(stream, encoding, "a:");
					WriteString(stream, encoding, col.Count.ToString());
					WriteString(stream, encoding, ":{");
					int i = 0;
					foreach (PmlElement item in col) {
						WriteString(stream, encoding, "i:");
						WriteString(stream, encoding, i.ToString());
						WriteString(stream, encoding, ";");
						WriteElementTo(item, stream, encoding);
						i += 1;
					}
					WriteString(stream, encoding, "}");
					break;
				default:
					WriteString(stream, encoding, "N;");
					break;
			}
		}
	}
	public class PmlPHPReader : IPmlReader {
		private Stream stream;
		private Encoding encoding;

		public static PmlElement DecodeMessage(Byte[] buffer) {
			using (MemoryStream ms = new MemoryStream(buffer, false)) return ReadMessageFrom(ms, Encoding.UTF8);
		}

		public PmlPHPReader(Stream stream) {
			this.stream = stream;
			this.encoding = Encoding.UTF8;
		}
		public PmlPHPReader(Stream stream, Encoding encoding) {
			this.stream = stream;
			this.encoding = encoding;
		}

		public Stream Stream { get { return stream; } }

		public PmlElement ReadMessage() {
			return ReadMessageFrom(stream, encoding);
		}

		public static PmlElement ReadMessageFrom(Stream stream, Encoding encoding) {
			lock (stream) return ReadElementFrom(stream, encoding);
		}

		public class PhpSerializerException : Exception {
			public PhpSerializerException(int Position, char Read, char Expected) : base("At position " + Position.ToString() + " expected " + Expected + " but got " + Read) { }
		}

		private static Char ReadChar(Stream stream) {
			int value = stream.ReadByte();
			if (value < 0) throw new EndOfStreamException();
			return (Char)value;
		}
		private static string ReadNumber(Stream stream, Char terminatedBy) {
			String str = "";
			while (true) {
				Char c = ReadChar(stream);
				if (c == terminatedBy) break;
				if (c < '0' && c > '9' && c != '.' && c != ',' && c != '-' && c != 'e') throw new PhpSerializerException(0, c, terminatedBy);
				str += c;
			}
			return str;
		}
		private static void ReadExpect(Stream stream, Char expect) {
			Char c = ReadChar(stream);
			if (c != expect) throw new PhpSerializerException(0, c, expect);
		}
		private static String ReadString(Stream stream, Encoding encoding, int length) {
			Byte[] bytes = new Byte[length];
			while (length > 0) {
				int read = stream.Read(bytes, bytes.Length - length, length);
				if (read <= 0) throw new EndOfStreamException();
				length -= read;
			}
			return encoding.GetString(bytes);
		}

		private static PmlElement ReadElementFrom(Stream stream, Encoding encoding) {
			Char type = ReadChar(stream);
			switch (type) {
				case 'N':
					ReadExpect(stream, ';');
					return new PmlNull();
				case 'i':
					ReadExpect(stream, ':');
					return new PmlInteger(ReadNumber(stream, ';'));
				case 'd':
					ReadExpect(stream, ':');
					//Return New PML.PMLNumber(ReadNumber(Reader, ";"c))
					return new PmlString(ReadNumber(stream, ';'));
				case 's':
					ReadExpect(stream, ':');
					int strlen = int.Parse(ReadNumber(stream, ':'));
					ReadExpect(stream, '"');
					String str = ReadString(stream, encoding, strlen);
					ReadExpect(stream, '"');
					ReadExpect(stream, ';');
					return new PmlString(str);
				case 'a':
					PmlDictionary dict = new PmlDictionary();
					ReadExpect(stream, ':');
					int count = int.Parse(ReadNumber(stream, ':'));
					ReadExpect(stream, '{');
					for (int i = 0; i < count; i++) {
						Char read = ReadChar(stream);
						String key;
						switch (read) {
							case 'i':
								ReadExpect(stream, ':');
								key = ReadNumber(stream, ';');
								break;
							case 's':
								ReadExpect(stream, ':');
								strlen = int.Parse(ReadNumber(stream, ':'));
								ReadExpect(stream, '"');
								key = ReadString(stream, encoding, strlen);
								ReadExpect(stream, '"');
								ReadExpect(stream, ';');
								break;
							case 'd':
								ReadExpect(stream, ':');
								key = ReadNumber(stream, ';');
								break;
							default:
								throw new NotSupportedException("Only integer and string keys are supported, got: " + read);
						}
						dict.Add(key, ReadElementFrom(stream, encoding));
					}
					ReadExpect(stream, '}');
					return dict;
				default:
					throw new NotSupportedException("Unknown type: " + type);
			}
		}
	}
}
