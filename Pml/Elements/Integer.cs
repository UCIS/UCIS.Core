using System;

namespace UCIS.Pml {
	public class PmlInteger : PmlElement {
		private Int64 value;
		private Boolean signed;

		public PmlInteger() {
			value = 0;
			signed = true;
		}
		public PmlInteger(UInt64 value) {
			this.value = (Int64)value;
			signed = false;
		}
		public PmlInteger(Int64 value) {
			this.value = value;
			signed = true;
		}
		public PmlInteger(String value) {
			UInt64 uvalue;
			if (Int64.TryParse(value, out this.value)) {
				signed = true;
			} else if (UInt64.TryParse(value, out uvalue)) {
				this.value = (Int64)uvalue;
				signed = false;
			} else {
				throw new FormatException();
			}
		}

		public Boolean IsSigned { get { return signed; } }

		public override PmlType Type { get { return PmlType.Integer; } }

		public override object ToObject() { return signed ? (Object)value : (Object)ToUInt64(); }
		public override string ToString() { return signed ? value.ToString() : ToUInt64().ToString(); }
		public override bool ToBoolean() { return value != 0; }
		public override byte ToByte() { return (Byte)value; }
		public override decimal ToDecimal() { return signed ? (Decimal)value : (Decimal)ToUInt64(); }
		public override double ToDouble() { return signed ? (Double)value : (Double)ToUInt64(); }
		public override short ToInt16() { return (Int16)value; }
		public override int ToInt32() { return (Int32)value; }
		public override long ToInt64() { return value; }
		public override sbyte ToSByte() { return (SByte)value; }
		public override float ToSingle() { return signed ? (Single)value : (Single)ToUInt64(); }
		public override ushort ToUInt16() { return (UInt16)value; }
		public override uint ToUInt32() { return (UInt32)value; }
		public override ulong ToUInt64() { return (UInt64)value; }
		public override char ToChar() { return (Char)value; }
		public override byte[] ToByteArray() { return signed ? BitConverter.GetBytes(value) : BitConverter.GetBytes(ToUInt64()); }
	}
}
