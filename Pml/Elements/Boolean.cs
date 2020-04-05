using System;

namespace UCIS.Pml {
	public class PmlBoolean: PmlElement {
		private Boolean value;

		public PmlBoolean() {
			value = false;
		}
		public PmlBoolean(Boolean value) {
			this.value = value;
		}

		public override PmlType Type { get { return PmlType.Boolean; } }

		public override object ToObject() { return value; }
		public override string ToString() { return value.ToString(); }
		public override bool ToBoolean() { return value; }
		public override byte ToByte() { return (Byte)ToInt32(); }
		public override decimal ToDecimal() { return ToInt32(); }
		public override double ToDouble() { return ToInt32(); }
		public override short ToInt16() { return (Int16)ToInt32(); }
		public override int ToInt32() { return value ? 1 : 0; }
		public override long ToInt64() { return ToInt32(); }
		public override sbyte ToSByte() { return (SByte)ToInt32(); }
		public override float ToSingle() { return ToInt32(); }
		public override ushort ToUInt16() { return (UInt16)ToInt32(); }
		public override uint ToUInt32() { return (UInt32)ToInt32(); }
		public override ulong ToUInt64() { return (UInt64)ToInt32(); }
		public override char ToChar() { return (Char)ToInt32(); }
		public override byte[] ToByteArray() { return new Byte[1] { (Byte)ToInt32() }; }
	}
}
