using System;
using System.Globalization;
using System.Text;

namespace UCIS.Pml {
	public class PmlString : PmlElement {
		private string _Value;

		public PmlString(string Value) {
			_Value = Value == null ? String.Empty : Value;
		}

		public override PmlType Type { get { return PmlType.String; } }

		public override object ToObject() { return _Value; }
		public override string ToString() { return _Value; }
		public override bool ToBoolean() { return Boolean.Parse(_Value); }
		public override byte ToByte() { return Byte.Parse(_Value, CultureInfo.InvariantCulture); }
		public override decimal ToDecimal() { return Decimal.Parse(_Value, CultureInfo.InvariantCulture); }
		public override double ToDouble() { return Double.Parse(_Value, CultureInfo.InvariantCulture); }
		public override short ToInt16() { return Int16.Parse(_Value, CultureInfo.InvariantCulture); }
		public override int ToInt32() { return Int32.Parse(_Value, CultureInfo.InvariantCulture); }
		public override long ToInt64() { return Int64.Parse(_Value, CultureInfo.InvariantCulture); }
		public override sbyte ToSByte() { return SByte.Parse(_Value, CultureInfo.InvariantCulture); }
		public override float ToSingle() { return Single.Parse(_Value, CultureInfo.InvariantCulture); }
		public override ushort ToUInt16() { return UInt16.Parse(_Value, CultureInfo.InvariantCulture); }
		public override uint ToUInt32() { return UInt32.Parse(_Value, CultureInfo.InvariantCulture); }
		public override ulong ToUInt64() { return UInt64.Parse(_Value, CultureInfo.InvariantCulture); }
		public override char ToChar() { return _Value[0]; }
		public override byte[] ToByteArray() { return Encoding.UTF8.GetBytes(_Value); }
	}
}

