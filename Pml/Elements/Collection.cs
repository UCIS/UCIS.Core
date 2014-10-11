using System;
using System.Collections;
using System.Collections.Generic;

namespace UCIS.Pml {
	public class PmlCollection : PmlElement, ICollection<PmlElement> {
		private List<PmlElement> pItems = new List<PmlElement>();

		public PmlCollection() { }
		public PmlCollection(params PmlElement[] Elements) {
			pItems.AddRange(Elements);
		}
		public PmlCollection(IEnumerable<PmlElement> Elements) {
			pItems.AddRange(Elements);
		}
		public PmlCollection(params String[] Elements) {
			foreach (String s in Elements) pItems.Add(s);
		}

		public PmlElement Add(PmlElement Element) {
			pItems.Add(Element);
			return Element;
		}
		public void Remove(PmlElement Element) {
			pItems.Remove(Element);
		}
		public void RemoveAt(int Index) {
			pItems.RemoveAt(Index);
		}
		public void Clear() {
			pItems.Clear();
		}
		public bool Contains(PmlElement item) {
			return pItems.Contains(item);
		}
		public void CopyTo(PmlElement[] array, int arrayIndex) {
			pItems.CopyTo(array, arrayIndex);
		}
		public int Count { get { return pItems.Count; } }
		public bool IsReadOnly { get { return false; } }
		public IEnumerator<PmlElement> GetEnumerator() { return pItems.GetEnumerator(); }
		IEnumerator IEnumerable.GetEnumerator() { return pItems.GetEnumerator(); }
		bool ICollection<PmlElement>.Remove(PmlElement item) { return pItems.Remove(item); }
		void ICollection<PmlElement>.Add(PmlElement item) { Add(item); }

		public override PmlType Type { get { return PmlType.Collection; } }

		public override object ToObject() { return pItems; }
		public override string ToString() { return null; }
		public override bool ToBoolean() { return pItems.Count > 0; }
		public override byte ToByte() { return (Byte)pItems.Count; }
		public override decimal ToDecimal() { return pItems.Count; }
		public override double ToDouble() { return pItems.Count; }
		public override short ToInt16() { return (Int16)pItems.Count; }
		public override int ToInt32() { return pItems.Count; }
		public override long ToInt64() { return pItems.Count; }
		public override sbyte ToSByte() { return (SByte)pItems.Count; }
		public override float ToSingle() { return pItems.Count; }
		public override ushort ToUInt16() { return (UInt16)pItems.Count; }
		public override uint ToUInt32() { return (UInt32)pItems.Count; }
		public override ulong ToUInt64() { return (UInt64)pItems.Count; }
		public override char ToChar() { return '\0'; }
		public override byte[] ToByteArray() { return null; }

		//public override PmlElement GetChild(string name) { return GetChild(name); }
		public override PmlElement GetChild(int index) { return pItems[index]; }
		public override IEnumerable<PmlElement> GetChildren() { return pItems; }
		public override IEnumerable<KeyValuePair<String, PmlElement>> GetNamedChildren() {
			KeyValuePair<String, PmlElement>[] kvps = new KeyValuePair<string, PmlElement>[pItems.Count];
			for (int i = 0; i < kvps.Length; i++) kvps[i] = new KeyValuePair<string, PmlElement>(null, pItems[i]);
			return kvps;
		}
		public override int GetChildCount() { return pItems.Count; }
		public override void AddChild(string name, PmlElement value) { Add(value); }
		public override void AddChild(PmlElement value) { Add(value); }
	}
}
