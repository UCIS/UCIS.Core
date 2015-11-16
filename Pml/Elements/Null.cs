namespace UCIS.Pml {
	public class PmlNull : PmlElement {
		public override PmlType Type { get { return PmlType.Null; } }

		public override object ToObject() { return null; }
		public override string ToString() { return null; }
		public override bool ToBoolean() { return false; }
		public override byte ToByte() { return 0; }
		public override decimal ToDecimal() { return 0; }
		public override double ToDouble() { return 0; }
		public override short ToInt16() { return 0; }
		public override int ToInt32() { return 0; }
		public override long ToInt64() { return 0; }
		public override sbyte ToSByte() { return 0; }
		public override float ToSingle() { return 0; }
		public override ushort ToUInt16() { return 0; }
		public override uint ToUInt32() { return 0; }
		public override ulong ToUInt64() { return 0; }
		public override char ToChar() { return '\0'; }
		public override byte[] ToByteArray() { return null; }
	}
}
