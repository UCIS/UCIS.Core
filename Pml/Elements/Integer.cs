using System;

namespace UCIS.Pml {
	public class PmlInteger : PmlElement {
		private UInt64 pUValue;
		private Int64 pSValue;
		private bool pSigned;

		public PmlInteger() {
			pSValue = 0;
			pSigned = true;
		}
		public PmlInteger(UInt64 Value) {
			pUValue = Value;
			pSigned = false;
		}
		public PmlInteger(Int64 Value) {
			pSValue = Value;
			pSigned = true;
		}
		public PmlInteger(string Value) {
			if (Int64.TryParse(Value, out pSValue)) {
				pSigned = true;
			} else if (UInt64.TryParse(Value, out pUValue)) {
				pSigned = false;
			} else {
				throw new FormatException();
			}
		}

		public Boolean IsSigned { get { return pSigned; } }

		public override PmlType Type { get { return PmlType.Integer; } }

		public override object ToObject() { return pSigned ? (Object)pSValue : (Object)pUValue; }
		public override string ToString() { return pSigned ? pSValue.ToString() : pUValue.ToString(); }
		public override bool ToBoolean() { return pSigned ? (pSValue != 0) : (pUValue != 0); }
		public override byte ToByte() { return pSigned ? (Byte)pSValue : (Byte)pUValue; }
		public override decimal ToDecimal() { return pSigned ? (Decimal)pSValue : (Decimal)pUValue; }
		public override double ToDouble() { return pSigned ? (Double)pSValue : (Double)pUValue; }
		public override short ToInt16() { return pSigned ? (Int16)pSValue : (Int16)pUValue; }
		public override int ToInt32() { return pSigned ? (Int32)pSValue : (Int32)pUValue; }
		public override long ToInt64() { return pSigned ? pSValue : (Int64)pUValue; }
		public override sbyte ToSByte() { return pSigned ? (SByte)pSValue : (SByte)pUValue; }
		public override float ToSingle() { return pSigned ? (Single)pSValue : (Single)pUValue; }
		public override ushort ToUInt16() { return pSigned ? (UInt16)pSValue : (UInt16)pUValue; }
		public override uint ToUInt32() { return pSigned ? (UInt32)pSValue : (UInt32)pUValue; }
		public override ulong ToUInt64() { return pSigned ? (UInt64)pSValue : pUValue; }
		public override char ToChar() { return pSigned ? (Char)pSValue : (Char)pUValue; }
		public override byte[] ToByteArray() { return pSigned ? BitConverter.GetBytes(pSValue) : BitConverter.GetBytes(pUValue); }
	}
}