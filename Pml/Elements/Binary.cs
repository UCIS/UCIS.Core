using System;
using System.Text;

namespace UCIS.Pml {
	public class PmlBinary : PmlElement {
		private byte[] _Value;

		public PmlBinary(byte[] Value) {
			_Value = Value;
		}

		public override PmlType Type { get { return PmlType.Binary; } }

		public override object ToObject() { return _Value; }
		public override string ToString() { return Encoding.UTF8.GetString(_Value); }
		public override bool ToBoolean() { return BitConverter.ToBoolean(_Value, 0); }
		public override byte ToByte() { return _Value[0]; }
		public override decimal ToDecimal() { return _Value.Length == 4 ? (Decimal)BitConverter.ToSingle(_Value, 0) : (Decimal)BitConverter.ToDouble(_Value, 0); }
		public override double ToDouble() { return BitConverter.ToDouble(_Value, 0); }
		public override short ToInt16() { return BitConverter.ToInt16(_Value, 0); }
		public override int ToInt32() { return BitConverter.ToInt32(_Value, 0); }
		public override long ToInt64() { return BitConverter.ToInt64(_Value, 0); }
		public override sbyte ToSByte() { return (SByte)_Value[0]; }
		public override float ToSingle() { return BitConverter.ToSingle(_Value, 0); }
		public override ushort ToUInt16() { return BitConverter.ToUInt16(_Value, 0); }
		public override uint ToUInt32() { return BitConverter.ToUInt32(_Value, 0); }
		public override ulong ToUInt64() { return BitConverter.ToUInt64(_Value, 0); }
		public override char ToChar() { return BitConverter.ToChar(_Value, 0); }
		public override byte[] ToByteArray() { return _Value; }
	}
}