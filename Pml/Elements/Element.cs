using System;
using System.Collections.Generic;
using System.Text;

namespace UCIS.Pml {
	/*public enum PmlElementType : byte {
		Null = 0,

		Dictionary = 1,
		Collection = 2,

		Binary = 10,
		String = 11,

		Integer = 20
	}*/
	public enum PmlType {
		Null,
		Dictionary,
		Collection,
		Binary,
		String,
		Integer,
		//Number,
	}
	public abstract class PmlElement {
		public abstract PmlType Type { get; }

		public virtual PmlElement GetChild(string name) { return null; }
		public virtual PmlElement GetChild(int index) { return null; }
		public virtual IEnumerable<PmlElement> GetChildren() { return null; }
		public virtual IEnumerable<KeyValuePair<String, PmlElement>> GetNamedChildren() { return null; }
		public virtual int GetChildCount() { return 0; }
		public virtual void AddChild(string name, PmlElement value) { throw new NotSupportedException(); }
		public virtual void AddChild(PmlElement value) { throw new NotSupportedException(); }

		public abstract object ToObject();
		public abstract override string ToString();
		public abstract bool ToBoolean();
		public abstract byte ToByte();
		public abstract decimal ToDecimal();
		public abstract double ToDouble();
		public abstract short ToInt16();
		public abstract int ToInt32();
		public abstract long ToInt64();
		public abstract sbyte ToSByte();
		public abstract float ToSingle();
		public abstract ushort ToUInt16();
		public abstract uint ToUInt32();
		public abstract ulong ToUInt64();
		public abstract char ToChar();
		public abstract byte[] ToByteArray();

		public static explicit operator string(PmlElement e) { return e == null ? null : e.ToString(); }
		public static explicit operator bool(PmlElement e) { return e == null ? false : e.ToBoolean(); }
		public static explicit operator byte(PmlElement e) { return e == null ? (byte)0 : e.ToByte(); }
		public static explicit operator decimal(PmlElement e) { return e == null ? (decimal)0 : e.ToDecimal(); }
		public static explicit operator double(PmlElement e) { return e == null ? (double)0 : e.ToDouble(); }
		public static explicit operator short(PmlElement e) { return e == null ? (short)0 : e.ToInt16(); }
		public static explicit operator int(PmlElement e) { return e == null ? (int)0 : e.ToInt32(); }
		public static explicit operator long(PmlElement e) { return e == null ? (long)0 : e.ToInt64(); }
		public static explicit operator sbyte(PmlElement e) { return e == null ? (sbyte)0 : e.ToSByte(); }
		public static explicit operator float(PmlElement e) { return e == null ? (float)0 : e.ToSingle(); }
		public static explicit operator ushort(PmlElement e) { return e == null ? (ushort)0 : e.ToUInt16(); }
		public static explicit operator uint(PmlElement e) { return e == null ? (uint)0 : e.ToUInt32(); }
		public static explicit operator ulong(PmlElement e) { return e == null ? (ulong)0 : e.ToUInt64(); }
		public static explicit operator char(PmlElement e) { return e == null ? '\0' : e.ToChar(); }
		public static explicit operator byte[](PmlElement e) { return e == null ? null : e.ToByteArray(); }

		public static implicit operator PmlElement(String str) { return new PmlString(str); }
		public static implicit operator PmlElement(UInt64 number) { return new PmlInteger(number); }
		public static implicit operator PmlElement(UInt32 number) { return new PmlInteger(number); }
		public static implicit operator PmlElement(Int64 number) { return new PmlInteger(number); }
		public static implicit operator PmlElement(Int32 number) { return new PmlInteger(number); }
		public static implicit operator PmlElement(Int16 number) { return new PmlInteger(number); }
		public static implicit operator PmlElement(UInt16 number) { return new PmlInteger(number); }
		public static implicit operator PmlElement(Byte number) { return new PmlInteger(number); }
		//public static implicit operator PmlElement(Double number) { return new PmlNumber(number); }
	}
}
