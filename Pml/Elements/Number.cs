using System;
using System.Globalization;

namespace UCIS.Pml {
	public class PmlNumber : PmlElement {
		private double _Value;

		public PmlNumber() {
			_Value = 0;
		}
		public PmlNumber(double Value) {
			_Value = Value;
		}
		public PmlNumber(string Value) {
			_Value = double.Parse(Value);
		}

		public override PmlType Type { get { return PmlType.Number; } }

		public override object ToObject() { return _Value; }
		public override string ToString() { return _Value.ToString("#,#", CultureInfo.InvariantCulture); }
		public override bool ToBoolean() { return _Value != 0; }
		public override byte ToByte() { return (Byte)_Value; }
		public override decimal ToDecimal() { return (Decimal)_Value; }
		public override double ToDouble() { return (Double)_Value; }
		public override short ToInt16() { return (Int16)_Value; }
		public override int ToInt32() { return (Int32)_Value; }
		public override long ToInt64() { return (Int64)_Value; }
		public override sbyte ToSByte() { return (SByte)_Value; }
		public override float ToSingle() { return (Single)_Value; }
		public override ushort ToUInt16() { return (UInt16)_Value; }
		public override uint ToUInt32() { return (UInt32)_Value; }
		public override ulong ToUInt64() { return (UInt64)_Value; }
		public override char ToChar() { return (Char)_Value; }
		public override byte[] ToByteArray() { return BitConverter.GetBytes(_Value); }
	}
}
