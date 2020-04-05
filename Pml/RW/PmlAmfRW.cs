using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UCIS.Pml;

namespace UCIS.Pml {
	internal enum AmfDataType : byte {
		Number = 0,
		Boolean = 1,
		String = 2,
		UntypedObject = 3,
		MovieClip = 4,
		Null = 5,
		Undefined = 6,
		ReferencedObject = 7,
		MixedArray = 8,
		End = 9,
		Array = 10,
		Date = 11,
		LongString = 12,
		TypeAsObject = 13,
		Recordset = 14,
		Xml = 15,
		TypedObject = 16
	}
	public class PmlAmfWriter : IPmlWriter {
		private BinaryWriter pWriter;

		public PmlAmfWriter(BinaryWriter Writer) {
			pWriter = Writer;
		}
		public PmlAmfWriter(Stream Stream) {
			pWriter = new BinaryWriter(Stream, Encoding.UTF8);
		}

		public BinaryWriter BaseWriter {
			get { return pWriter; }
			set { pWriter = value; }
		}

		public void WriteMessage(PmlElement Message) {
			WriteMessageTo(Message, pWriter);
		}

		public static void WriteMessageTo(PmlElement Message, BinaryWriter Writer) {
			lock (Writer) {
				WriteElementTo(Message, Writer);
				Writer.Flush();
			}
		}

		private static void WriteElementTo(PmlElement Element, BinaryWriter Writer) {
			if (Element == null) {
				Writer.Write((byte)AmfDataType.Null);
				return;
			}
			switch (Element.Type) {
				case PmlType.Null:
					Writer.Write((byte)AmfDataType.Null);
					break;
				case PmlType.Dictionary:
					Writer.Write((byte)AmfDataType.UntypedObject);
					//WriteDictionary(Writer, (PmlDictionary)Element);
					WriteUntypedObject(Writer, (PmlDictionary)Element);
					break;
				case PmlType.Collection:
					Writer.Write((byte)AmfDataType.Array);
					WriteCollection(Writer, (PmlCollection)Element);
					break;
				case PmlType.Binary:
					Writer.Write((byte)AmfDataType.String);
					byte[] bytes = Element.ToByteArray();
					if (bytes.Length > UInt16.MaxValue) {
						Writer.Write((byte)AmfDataType.String);
						WriteString(Writer, bytes);
					} else {
						Writer.Write((byte)AmfDataType.LongString);
						WriteLongString(Writer, bytes);
					}
					break;
				case PmlType.String:
					string str = Element.ToString();
					if (str.Length < UInt16.MaxValue) {
						Writer.Write((byte)AmfDataType.String);
						WriteString(Writer, str);
					} else {
						Writer.Write((byte)AmfDataType.LongString);
						WriteLongString(Writer, str);
					}
					break;
				case PmlType.Integer:
				case PmlType.Number:
					Writer.Write((byte)AmfDataType.Number);
					WriteDouble(Writer, (Element as PmlInteger).ToDouble());
					break;
				case PmlType.Boolean:
					Writer.Write((byte)AmfDataType.Boolean);
					Writer.Write((byte)(Element.ToBoolean() ? 1 : 0));
					break;
			}
		}
		private static void WriteEnd(BinaryWriter w) {
			WriteUInt16(w, 0);
			w.Write((byte)AmfDataType.End);
		}
		private static void WriteUntypedObject(BinaryWriter w, PmlDictionary value) {
			foreach (KeyValuePair<String, PmlElement> kvp in value) {
				WriteString(w, kvp.Key);
				WriteElementTo(kvp.Value, w);
			}
			WriteEnd(w);
		}
		private static void WriteDictionary(BinaryWriter w, PmlDictionary value) {
			WriteUInt32(w, (UInt32)value.Count);
			foreach (KeyValuePair<String, PmlElement> kvp in value) {
				WriteString(w, kvp.Key);
				WriteElementTo(kvp.Value, w);
			}
		}
		private static void WriteCollection(BinaryWriter w, PmlCollection value) {
			WriteUInt32(w,(UInt32)value.Count);
			foreach (PmlElement o in value) {
				WriteElementTo(o, w);
			}
		}
		private static void WriteDouble(BinaryWriter w, double value) {
			WriteReverse(w, BitConverter.GetBytes(value));
		}
		private static void WriteUInt32(BinaryWriter w, UInt32 value) {
			WriteReverse(w, BitConverter.GetBytes(value));
		}
		private static void WriteUInt16(BinaryWriter w, UInt16 value) {
			WriteReverse(w, BitConverter.GetBytes(value));
		}
		private static void WriteString(BinaryWriter w, string value) {
			WriteString(w, Encoding.UTF8.GetBytes(value));
		}
		private static void WriteLongString(BinaryWriter w, string value) {
			WriteLongString(w, Encoding.UTF8.GetBytes(value));
		}
		private static void WriteString(BinaryWriter w, byte[] buffer) {
			WriteUInt16(w, (UInt16)buffer.Length);
			w.Write(buffer, 0, buffer.Length);
		}
		private static void WriteLongString(BinaryWriter w, byte[] buffer) {
			WriteUInt32(w, (UInt32)buffer.Length);
			w.Write(buffer, 0, buffer.Length);
		}
		private static void WriteReverse(BinaryWriter w, byte[] value) {
			Array.Reverse(value);
			w.Write(value);
		}
	}

	public class PmlAmfReader : IPmlReader {
		private BinaryReader pReader;

		public PmlAmfReader(BinaryReader Reader) {
			pReader = Reader;
		}
		public PmlAmfReader(Stream Stream) {
			pReader = new BinaryReader(Stream, Encoding.UTF8);
		}

		public BinaryReader BaseReader {
			get { return pReader; }
			set { pReader = value; }
		}

		public PmlElement ReadMessage() {
			return ReadMessageFrom(pReader);
		}

		public static PmlElement ReadMessageFrom(BinaryReader Reader) {
			PmlElement Element = null;
			lock (Reader) {
				Element = ReadElementFrom(Reader);
			}
			return Element;
		}


		private static PmlElement ReadElementFrom(BinaryReader Reader) {
			AmfDataType EType = (AmfDataType)Reader.ReadByte();
			return ReadData(Reader, EType);
		}
		private static PmlElement ReadData(BinaryReader Reader, AmfDataType EType) {
			switch (EType) {
				case AmfDataType.Number:
					Double d = ReadDouble(Reader);
					if (d == (double)(Int64)d) {
						return new PmlInteger((Int64)d);
					} else if (d == (double)(UInt64)d) {
						return new PmlInteger((UInt64)d);
					} else {
						return new PmlNumber(d);
					}
				case AmfDataType.Boolean:
					return new PmlBoolean(Reader.ReadByte() != 0);
				case AmfDataType.String:
					return new PmlString(ReadShortString(Reader));
				case AmfDataType.Array:
					PmlCollection ElementC = new PmlCollection();
					int size = ReadInt32(Reader);
					for (int i = 0; i < size; ++i) {
						ElementC.Add(ReadElementFrom(Reader));
					}
					return ElementC;
				case AmfDataType.Date:
					return new PmlString(ReadDate(Reader).ToString());
				case AmfDataType.LongString:
					return new PmlString(ReadLongString(Reader));
				case AmfDataType.TypedObject:
					ReadShortString(Reader);
					return ReadUntypedObject(Reader);
				case AmfDataType.MixedArray:
					return ReadDictionary(Reader);
				case AmfDataType.Null:
				case AmfDataType.Undefined:
				case AmfDataType.End:
					return new PmlNull();
				case AmfDataType.UntypedObject:
					return ReadUntypedObject(Reader);
				case AmfDataType.Xml:
					return new PmlString(ReadLongString(Reader));
				case AmfDataType.MovieClip:
				case AmfDataType.ReferencedObject:
				case AmfDataType.TypeAsObject:
				case AmfDataType.Recordset:
				default:
					throw new NotSupportedException("The AMF type is not supported: " + EType.ToString());
			}
		}

		private static PmlDictionary ReadUntypedObject(BinaryReader Reader) {
			PmlDictionary ElementD = new PmlDictionary();
			string key = ReadShortString(Reader);
			for (byte type = Reader.ReadByte(); type != 9; type = Reader.ReadByte()) {
				ElementD.Add(key, ReadData(Reader, (AmfDataType)type));
				key = ReadShortString(Reader);
			}
			return ElementD;
		}
		private static PmlDictionary ReadDictionary(BinaryReader Reader) {
			PmlDictionary ElementD = new PmlDictionary();
			int size = ReadInt32(Reader);
			return ReadUntypedObject(Reader);
		}

		private static DateTime ReadDate(BinaryReader r) {
			double ms = ReadDouble(r);
			DateTime date = (new DateTime(1970, 1, 1)).AddMilliseconds(ms);
			ReadUInt16(r); //gets the timezone
			return date;
		}
		private static double ReadDouble(BinaryReader r) {
			return BitConverter.ToDouble(ReadReverse(r, 8), 0);
		}

		private static int ReadInt32(BinaryReader r) {
			return BitConverter.ToInt32(ReadReverse(r, 4), 0);
		}

		private static ushort ReadUInt16(BinaryReader r) {
			return BitConverter.ToUInt16(ReadReverse(r, 2), 0);
		}

		private static byte[] ReadReverse(BinaryReader r, int size) {
			byte[] buffer = r.ReadBytes(size);
			Array.Reverse(buffer);
			return buffer;
		}
		private static string ReadLongString(BinaryReader r) {
			int length = ReadInt32(r);
			return ReadString(r, length);
		}
		private static string ReadShortString(BinaryReader r) {
			int length = ReadUInt16(r);
			return ReadString(r, length);
		}
		private static string ReadString(BinaryReader r, int length) {
			byte[] buffer = r.ReadBytes(length);
			return Encoding.UTF8.GetString(buffer);
		}
	}
}