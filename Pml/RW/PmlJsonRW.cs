using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace UCIS.Pml {
	public class PmlJsonWriter : IPmlWriter {
		private TextWriter stream;

		public static String EncodeMessage(PmlElement message) {
			using (StringWriter stream = new StringWriter()) {
				WriteMessageTo(message, stream);
				return stream.ToString();
			}
		}

		public PmlJsonWriter(Stream stream) : this(stream, Encoding.UTF8) { }
		public PmlJsonWriter(Stream stream, Encoding encoding) : this(new StreamWriter(stream, encoding)) { }
		public PmlJsonWriter(TextWriter stream) {
			this.stream = stream;
		}

		public TextWriter Writer { get { return stream; } }

		public void WriteMessage(PmlElement message) {
			WriteMessageTo(message, stream);
		}

		public static void WriteMessageTo(PmlElement message, Stream stream, Encoding encoding) {
			lock (stream) {
				StreamWriter writer = new StreamWriter(stream, encoding);
				WriteElementTo(message, writer);
				writer.Flush();
				stream.Flush();
			}
		}
		public static void WriteMessageTo(PmlElement message, TextWriter stream) {
			lock (stream) {
				WriteElementTo(message, stream);
				stream.Flush();
			}
		}

		private static void WriteEscapedCharacter(TextWriter stream, Char c) {
			switch (c) {
				case '\\': stream.Write("\\\\"); break;
				case '\"': stream.Write("\\\""); break;
				case '\n': stream.Write("\\n"); break;
				case '\r': stream.Write("\\r"); break;
				case '\t': stream.Write("\\t"); break;
				case '\b': stream.Write("\\b"); break;
				case '\f': stream.Write("\\f"); break;
				default: stream.Write(c); break;
			}
		}
		private static void WriteEscapedString(TextWriter stream, String str) {
			stream.Write('"');
			foreach (Char c in str) {
				if (c < ' ' || c == '"' || c == '\\') {
					WriteEscapedCharacter(stream, c);
				} else {
					stream.Write(c);
				}
			}
			stream.Write('"');
		}

		private static void WriteElementTo(PmlElement element, TextWriter stream) {
			if (element == null) {
				stream.Write("null");
				return;
			}
			switch (element.Type) {
				case PmlType.Null:
					stream.Write("null");
					break;
				case PmlType.String:
					WriteEscapedString(stream, element.ToString());
					break;
				case PmlType.Binary:
					stream.Write('"');
					foreach (Byte b in element.ToByteArray()) {
						if (b < ' ' || b == '"' || b == '\\') {
							WriteEscapedCharacter(stream, (Char)b);
						} else {
							stream.Write((Char)b);
						}
					}
					stream.Write('"');
					break;
				case PmlType.Integer:
					stream.Write(element.ToInt64().ToString(CultureInfo.InvariantCulture));
					break;
				case PmlType.Number:
					stream.Write(element.ToDouble().ToString("f", CultureInfo.InvariantCulture));
					break;
				case PmlType.Boolean:
					stream.Write(element.ToBoolean() ? "true" : "false");
					break;
				case PmlType.Dictionary:
					IDictionary<String, PmlElement> dict = (IDictionary<String, PmlElement>)element;
					Boolean first = true;
					stream.Write('{');
					foreach (KeyValuePair<String, PmlElement> item in dict) {
						if (!first) stream.Write(',');
						first = false;
						WriteEscapedString(stream, item.Key);
						stream.Write(':');
						WriteElementTo(item.Value, stream);
					}
					stream.Write('}');
					break;
				case PmlType.Collection:
					IEnumerable<PmlElement> col = (IEnumerable<PmlElement>)element;
					first = true;
					stream.Write('[');
					foreach (PmlElement item in col) {
						if (!first) stream.Write(',');
						first = false;
						WriteElementTo(item, stream);
					}
					stream.Write(']');
					break;
				default:
					throw new InvalidOperationException("Can not encode PML type " + element.Type.ToString() + " to JSON");
			}
		}
	}

	public class JsonFormatException : Exception {
		public JsonFormatException(String message) : base(message) { }
	}

	public class PmlJsonReader : IPmlReader {
		private TextReader stream;

		public static PmlElement DecodeMessage(Byte[] buffer) {
			using (MemoryStream ms = new MemoryStream(buffer, false)) using (StreamReader sr = new StreamReader(ms, Encoding.UTF8)) return ReadMessageFrom(sr);
		}

		public static PmlElement DecodeMessage(String buffer) {
			using (StringReader sr = new StringReader(buffer)) return ReadMessageFrom(sr);
		}

		public PmlJsonReader(Stream stream) : this(stream, Encoding.UTF8) { }
		public PmlJsonReader(Stream stream, Encoding encoding) : this(new StreamReader(stream, encoding)) { }
		public PmlJsonReader(TextReader stream) {
			this.stream = stream;
		}

		public TextReader Reader { get { return stream; } }

		public PmlElement ReadMessage() {
			return ReadMessageFrom(stream);
		}

		public static PmlElement ReadMessageFrom(Stream stream, Encoding encoding) {
			lock (stream) {
				StreamReader reader = new StreamReader(stream, encoding);
				return ReadElementFrom(reader);
			}
		}

		public static PmlElement ReadMessageFrom(TextReader reader) {
			lock (reader) return ReadElementFrom(reader);
		}

		private static int FindNextToken(TextReader reader) {
			int c = reader.Peek();
			while (c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == '\f') {
				reader.Read();
				c = reader.Peek();
			}
			return c;
		}
		private static String ReadString(TextReader reader) {
			int quotes = reader.Read();
			if (quotes != '"' && quotes != '\'') throw new JsonFormatException("Expected single or double quotes (start of string), got " + (Char)quotes);
			int c = reader.Read();
			StringBuilder sb = new StringBuilder();
			while (c != quotes) {
				if (c == -1) throw new EndOfStreamException();
				if (c == '\\') {
					c = reader.Read();
					if (c == -1) throw new EndOfStreamException();
					switch (c) {
						case '"': sb.Append('"'); break;
						case '\\': sb.Append('\\'); break;
						case '/': sb.Append('/'); break;
						case 'b': sb.Append('\b'); break;
						case 'f': sb.Append('\f'); break;
						case 'n': sb.Append('\n'); break;
						case 'r': sb.Append('\r'); break;
						case 't': sb.Append('\t'); break;
						case 'u':
							Char[] hex = new Char[4];
							if (reader.ReadBlock(hex, 0, hex.Length) != hex.Length) throw new EndOfStreamException();
							UInt32 codepoint = UInt32.Parse(new String(hex), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
							if (codepoint < 0x10000) {
								sb.Append((Char)codepoint);
							} else {
								codepoint -= 0x10000;
								sb.Append((Char)((codepoint >> 10) + 0xD800));
								sb.Append((Char)(codepoint % 0x0400 + 0xDC00));
							}
							break;
						default:
							throw new JsonFormatException("Expected valid escape sequence, got \\" + (Char)c);
					}
				} else {
					sb.Append((Char)c);
				}
				c = reader.Read();
			}
			return sb.ToString();
		}
		private static void ExpectLiteral(TextReader reader, String literal) {
			foreach (Char c in literal) {
				int g = reader.Read();
				if (g == -1) throw new EndOfStreamException();
				if (g != c) throw new JsonFormatException("Expected " + c + " (" + literal + "), got " + (Char)g);
			}
		}

		private static PmlElement ReadElementFrom(TextReader reader) {
			int c = FindNextToken(reader);
			if (c == -1) return null;
			if (c == '{') {
				reader.Read();
				c = FindNextToken(reader);
				PmlDictionary d = new PmlDictionary();
				while (c != '}') {
					if (c == -1) throw new EndOfStreamException();
					String k = ReadString(reader);
					c = FindNextToken(reader);
					if (c != ':') throw new JsonFormatException("Expected colon, got " + (Char)c);
					reader.Read();
					PmlElement e = ReadElementFrom(reader);
					d.Add(k, e);
					c = FindNextToken(reader);
					if (c == '}') break;
					if (c != ',') throw new JsonFormatException("Expected comma or closing curly brace, got " + (Char)c);
					c = reader.Read();
					c = FindNextToken(reader);
				}
				reader.Read();
				return d;
			} else if (c == '[') {
				reader.Read();
				c = FindNextToken(reader);
				PmlCollection l = new PmlCollection();
				while (c != ']') {
					if (c == -1) throw new EndOfStreamException();
					PmlElement e = ReadElementFrom(reader);
					l.Add(e);
					c = FindNextToken(reader);
					if (c == ']') break;
					if (c != ',') throw new JsonFormatException("Expected comma or closing curly brace, got " + (Char)c);
					c = reader.Read();
				}
				reader.Read();
				return l;
			} else if (c == '"' || c == '\'') {
				return ReadString(reader);
			} else if (c == 'n') {
				ExpectLiteral(reader, "null");
				return new PmlNull();
			} else if (c == 't') {
				ExpectLiteral(reader, "true");
				return true;
			} else if (c == 'f') {
				ExpectLiteral(reader, "false");
				return false;
			} else if (c == '-' || (c >= '0' && c <= '9')) {
				StringBuilder sb = new StringBuilder();
				while (c == '.' || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E' || (c >= '0' && c <= '9')) {
					sb.Append((Char)c);
					reader.Read();
					c = reader.Peek();
				}
				String str = sb.ToString();
				if (str.IndexOf(".", StringComparison.OrdinalIgnoreCase) != -1 || str.IndexOf("e", StringComparison.OrdinalIgnoreCase) != -1) {
					return Double.Parse(str, NumberStyles.Any, CultureInfo.InvariantCulture);
				} else {
					return Int64.Parse(str, NumberStyles.Any, CultureInfo.InvariantCulture);
				}
			} else {
				throw new JsonFormatException("Expected JSON token, got " + (Char)c);
			}
		}
	}
}
