using System;
using System.IO;
using System.Text;

namespace UCIS.ProtocolBuffers {
	public interface IPBReader {
		void Reset();
		Boolean NextField();
		int FieldNumber { get; }
		int WireType { get; }
		Int64 GetVarint();
		Int64 GetFixed64();
		Byte[] GetBytes();
		String GetString();
		IPBReader GetMessageReader();
		Int32 GetFixed32();
	}
	public interface IPBWriter {
		void WriteVarint(int fieldNumber, Int64 value);
		void WriteFixed64(int fieldNumber, Int64 value);
		void WriteBytes(int fieldNumber, Byte[] bytes);
		void WriteBytes(int fieldNumber, Byte[] bytes, int offset, int count);
		void WriteFixed32(int fieldNumber, Int32 value);
	}
	public interface IPBMessage {
		void Decode(IPBReader r);
		void Encode(IPBWriter w);
	}
	public class ArrayPBReader : IPBReader {
		Byte[] buffer;
		int offset, length, index;
		int currentField;
		Boolean hasCurrentField;
		int currentFieldLength;

		public ArrayPBReader(Byte[] buffer) : this(buffer, 0, buffer.Length) { }
		public ArrayPBReader(Byte[] buffer, int offset, int length) {
			this.buffer = buffer;
			this.offset = offset;
			this.length = length;
			Reset();
		}

		public void Reset() {
			index = 0;
			hasCurrentField = false;
		}

		public bool NextField() {
			if (hasCurrentField) {
				index += currentFieldLength;
				hasCurrentField = false;
			}
			if (index >= length) return false;
			index += ReadVarIntTo(out currentField);
			int dummy;
			switch (WireType) {
				case 0: currentFieldLength = ReadVarIntTo(out dummy); ; break; //Varint
				case 1: currentFieldLength = 8; break; //64bit
				case 2: index += ReadVarIntTo(out currentFieldLength); break; //Bytes
				case 5: currentFieldLength = 4; break; //32bit
				default: throw new InvalidDataException();
			}
			if (index + currentFieldLength > length) throw new InvalidDataException();
			hasCurrentField = true;
			return true;
		}

		private int ReadVarIntTo(out int v) {
			long vl;
			int l = ReadVarIntTo(out vl);
			v = (int)vl;
			return l;
		}
		private int ReadVarIntTo(out long v) {
			v = 0;
			int h = 0;
			int b = 0x80;
			int l = 0;
			while ((b & 0x80) != 0) {
				b = buffer[offset + index + l];
				v |= ((long)b & 0x7FL) << h;
				h += 7;
				l++;
			}
			return l;
		}

		public int FieldNumber { get { return (int)((UInt32)currentField >> 3); } }
		public int WireType { get { return currentField & 7; } }

		public long GetVarint() {
			if (!hasCurrentField || WireType != 0) throw new InvalidOperationException();
			long v;
			ReadVarIntTo(out v);
			return v;
		}

		public long GetFixed64() {
			if (!hasCurrentField || WireType != 1) throw new InvalidOperationException();
			int i = offset + index;
			return ((long)buffer[i + 0] << 0) | ((long)buffer[i + 1] << 8) | ((long)buffer[i + 2] << 16) | ((long)buffer[i + 3] << 24) |
				((long)buffer[i + 4] << 32) | ((long)buffer[i + 5] << 40) | ((long)buffer[i + 6] << 48) | ((long)buffer[i + 7] << 56);
		}

		public byte[] GetBytes() {
			if (!hasCurrentField || WireType != 2) throw new InvalidOperationException();
			Byte[] bytes = new Byte[currentFieldLength];
			Buffer.BlockCopy(buffer, offset + index, bytes, 0, currentFieldLength);
			return bytes;
		}

		public string GetString() {
			if (!hasCurrentField || WireType != 2) throw new InvalidOperationException();
			return Encoding.UTF8.GetString(buffer, offset + index, currentFieldLength);
		}

		public IPBReader GetMessageReader() {
			if (!hasCurrentField || WireType != 2) throw new InvalidOperationException();
			return new ArrayPBReader(buffer, offset + index, currentFieldLength);
		}

		public int GetFixed32() {
			if (!hasCurrentField || WireType != 5) throw new InvalidOperationException();
			int i = offset + index;
			return ((int)buffer[i + 0] << 0) | ((int)buffer[i + 1] << 8) | ((int)buffer[i + 2] << 16) | ((int)buffer[i + 3] << 24);
		}
	}
	public class StreamPBWriter : IPBWriter {
		Stream stream;

		public StreamPBWriter(Stream stream) {
			this.stream = stream;
		}

		public static Byte[] Encode(IPBMessage message) {
			using (MemoryStream ms = new MemoryStream()) {
				message.Encode(new StreamPBWriter(ms));
				return ms.ToArray();
			}
		}

		public static int EncodeToStreamBuffered(Stream stream, IPBMessage message) {
			using (MemoryStream ms = new MemoryStream()) {
				message.Encode(new StreamPBWriter(ms));
				ms.WriteTo(stream);
				return (int)ms.Length;
			}
		}

		private void WriteVarint(long value) {
			while ((value & ~0x7fL) != 0) {
				stream.WriteByte((Byte)(0x80 | value));
				value = (long)((ulong)value >> 7);
			}
			stream.WriteByte((Byte)value);
		}
		private void WriteTag(int fieldNumber, int wireType) {
			WriteVarint((fieldNumber << 3) | wireType);
		}

		public void WriteVarint(int fieldNumber, long value) {
			WriteTag(fieldNumber, 0);
			WriteVarint(value);
		}

		public void WriteFixed64(int fieldNumber, long value) {
			WriteTag(fieldNumber, 1);
			for (int i = 0; i < 8; i++) {
				stream.WriteByte((Byte)value);
				value >>= 8;
			}
		}

		public void WriteBytes(int fieldNumber, byte[] bytes) {
			WriteBytes(fieldNumber, bytes, 0, bytes.Length);
		}

		public void WriteBytes(int fieldNumber, byte[] bytes, int offset, int count) {
			WriteTag(fieldNumber, 2);
			WriteVarint(count);
			stream.Write(bytes, offset, count);
		}

		public void WriteFixed32(int fieldNumber, int value) {
			WriteTag(fieldNumber, 5);
			for (int i = 0; i < 4; i++) {
				stream.WriteByte((Byte)value);
				value >>= 8;
			}
		}
	}
}