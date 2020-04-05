using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace UCIS.Pml {
	public class PmlBinaryRW : IPmlRW {
		private PmlBinaryReader pReader;
		private PmlBinaryWriter pWriter;

		public PmlBinaryRW(Stream Stream) {
			pReader = new PmlBinaryReader(Stream);
			pWriter = new PmlBinaryWriter(Stream);
		}
		public PmlBinaryRW(Stream Stream, Encoding Encoding) {
			pReader = new PmlBinaryReader(Stream, Encoding);
			pWriter = new PmlBinaryWriter(Stream, Encoding);
		}

		public PmlElement ReadMessage() {
			return pReader.ReadMessage();
		}

		public void WriteMessage(PmlElement Message) {
			pWriter.WriteMessage(Message);
		}

		public PmlBinaryReader Reader {
			get { return pReader; }
		}
		public PmlBinaryWriter Writer {
			get { return pWriter; }
		}
	}

	public class PmlBinaryWriter : IPmlWriter {
		private Stream pStream;
		private Encoding pEncoding;

		public PmlBinaryWriter(Stream Stream) {
			pStream = Stream;
			pEncoding = Encoding.UTF8;
		}
		public PmlBinaryWriter(Stream Stream, Encoding Encoding) {
			pStream = Stream;
			pEncoding = Encoding;
		}

		public void WriteMessage(PmlElement Message) {
			MemoryStream stream = new MemoryStream();
			BinaryWriter writer = new BinaryWriter(stream, pEncoding);
			WriteMessageTo(Message, writer);
			lock (pStream) {
				stream.WriteTo(pStream);
				pStream.Flush();
			}
		}

		public static Byte[] EncodeMessage(PmlElement message) {
			MemoryStream stream = new MemoryStream();
			BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8);
			WriteMessageTo(message, writer);
			return stream.ToArray();
		}

		public static void WriteMessageTo(PmlElement Message, BinaryWriter Writer) {
			lock (Writer) {
				Writer.Write((byte)255);
				WriteElementTo(Message, Writer);
				Writer.Write((byte)255);
				Writer.Flush();
			}
		}

		private static void WriteElementTo(PmlElement Element, BinaryWriter Writer) {
			if (Element == null) {
				Writer.Write((byte)0);
				return;
			}
			switch (Element.Type) {
				case PmlType.Null:
					Writer.Write((byte)0);
					break;
				case PmlType.Dictionary:
					Writer.Write((byte)1);
					foreach (KeyValuePair<string, PmlElement> Item in (PmlDictionary)Element) {
						Writer.Write((byte)1);
						Writer.Write(Item.Key);
						WriteElementTo(Item.Value, Writer);
					}
					Writer.Write((byte)0);
					break;
				case PmlType.Collection:
					Writer.Write((byte)2);
					foreach (PmlElement Item in (PmlCollection)Element) {
						Writer.Write((byte)1);
						WriteElementTo(Item, Writer);
					}
					Writer.Write((byte)0);
					break;
				case PmlType.Binary: {
						Writer.Write((byte)10);
						Byte[] Buffer = Element.ToByteArray();
						if (Buffer == null) {
							Writer.Write((int)0);
						} else {
							Writer.Write((int)Buffer.Length);
							Writer.Write(Buffer);
						}
					} break;
				case PmlType.String:
					Writer.Write((byte)11);
					string Str = Element.ToString();
					if (Str == null) {
						Writer.Write(String.Empty);
					} else {
						Writer.Write(Str);
					}

					break;
				case PmlType.Integer:
					Writer.Write((byte)20);
					PmlInteger RMInt = (PmlInteger)Element;
					if (RMInt.IsSigned) {
						Writer.Write((byte)1);
						Writer.Write((long)RMInt);
					} else {
						Writer.Write((byte)0);
						Writer.Write((ulong)RMInt);
					}
					break;
				case PmlType.Boolean:
					Writer.Write((byte)21);
					Writer.Write(Element.ToBoolean());
					break;
				case PmlType.Number:
					Writer.Write((byte)22);
					Writer.Write(Element.ToDouble());
					break;
				default:
					Writer.Write((byte)0);
					Console.WriteLine("PmlBinaryRW: Can not encode PML type {0}", Element.Type);
					break;
			}
		}
	}

	public class PmlBinaryReader : IPmlReader {
		private BinaryReader pReader;

		public PmlBinaryReader(BinaryReader Reader) {
			pReader = Reader;
		}
		public PmlBinaryReader(Stream Stream) {
			pReader = new BinaryReader(Stream);
		}
		public PmlBinaryReader(Stream Stream, Encoding Encoding) {
			pReader = new BinaryReader(Stream, Encoding);
		}

		public BinaryReader BaseReader {
			get { return pReader; }
			set { pReader = value; }
		}

		public PmlElement ReadMessage() {
			return ReadMessageFrom(pReader);
		}

		public static PmlElement DecodeMessage(Byte[] message) {
			using (MemoryStream ms = new MemoryStream(message)) {
				using (BinaryReader reader = new BinaryReader(ms, Encoding.UTF8)) {
					return ReadMessageFrom(reader);
				}
			}
		}

		public static PmlElement ReadMessageFrom(BinaryReader Reader) {
			PmlElement Element = null;
			lock (Reader) {
				if (Reader.ReadByte() != 255) {
					return null;
				}
				Element = ReadElementFrom(Reader);
				if (Reader.ReadByte() != 255) {
					return null;
				}
			}
			return Element;
		}

		private static PmlElement ReadElementFrom(BinaryReader Reader) {
			Byte EType = Reader.ReadByte();
			switch (EType) {
				case 0: return new PmlNull();
				case 1:
					PmlDictionary ElementD = new PmlDictionary();
					do {
						byte B = Reader.ReadByte();
						if (B == 0) return ElementD;
						else if (B == 1) ElementD.Add(Reader.ReadString(), ReadElementFrom(Reader));
						else return null;
					}
					while (true);
				case 2:
					PmlCollection ElementC = new PmlCollection();
					do {
						byte B = Reader.ReadByte();
						if (B == 0) return ElementC;
						else if (B == 1) ElementC.Add(ReadElementFrom(Reader));
						else return null;
					}
					while (true);
				case 10:
					int Len = 0;
					Len = Reader.ReadInt32();
					return new PmlBinary(Reader.ReadBytes(Len));
				case 11:
					return new PmlString(Reader.ReadString());
				case 20: {
						byte B = Reader.ReadByte();
						if (B == 0) return new PmlInteger(Reader.ReadUInt64());
						else if (B == 1) return new PmlInteger(Reader.ReadInt64());
						else return null;
					}
				case 21: return Reader.ReadBoolean();
				case 22: return Reader.ReadDouble();
				default:
					throw new Exception("Unknown PML type code " + EType.ToString());
			}
		}
	}
}
