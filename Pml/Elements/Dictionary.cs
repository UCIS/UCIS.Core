using System;
using System.Collections;
using System.Collections.Generic;

namespace UCIS.Pml {
	public class PmlDictionary : PmlElement, IDictionary<string, PmlElement>, ICollection<PmlElement> {
		private List<KeyValuePair<string, PmlElement>> pItems = new List<KeyValuePair<string, PmlElement>>();

		public PmlDictionary() { }
		public PmlDictionary(String[] keys, params PmlElement[] values) {
			if (keys.Length != values.Length) throw new ArgumentException("keys.Length != values.Length", "values");
			for (int i = 0; i < keys.Length; i++) Add(keys[i], values[i]);
		}
		public PmlDictionary(params KeyValuePair<string, PmlElement>[] Elements) {
			foreach (KeyValuePair<string, PmlElement> Item in Elements) pItems.Add(Item);
		}
		public PmlDictionary(IEnumerable<KeyValuePair<string, PmlElement>> Elements) {
			foreach (KeyValuePair<string, PmlElement> Item in Elements) pItems.Add(Item);
		}

		public override PmlElement GetChild(string Name) {
			foreach (KeyValuePair<string, PmlElement> KVP in pItems) {
				if (KVP.Key.Equals(Name, StringComparison.InvariantCultureIgnoreCase)) return KVP.Value;
			}
			return null;
		}
		public override PmlElement GetChild(int index) {
			return pItems[index].Value;
		}
		public bool TryGetValue(string key, out PmlElement value) {
			value = GetChild(key);
			return (value != null);
		}
		public PmlElement this[string key] {
			get { return GetChild(key); }
			set {
				Remove(key);
				Add(key, value);
			}
		}

		public PmlElement Add(string Key, PmlElement Element) {
			if (Element == null) Element = new PmlNull();
			pItems.Add(new KeyValuePair<string, PmlElement>(Key, Element));
			return Element;
		}
		private void Add(KeyValuePair<string, PmlElement> item) {
			pItems.Add(item);
		}
		public void Add(PmlElement item) {
			Add("", item);
		}

		void IDictionary<string, PmlElement>.Add(string key, PmlElement value) {
			Add(key, value);
		}
		void ICollection<KeyValuePair<string, PmlElement>>.Add(KeyValuePair<string, PmlElement> item) {
			Add(item);
		}

		public bool Remove(PmlElement item) {
			foreach (KeyValuePair<string, PmlElement> KVP in pItems) {
				if (KVP.Value == item) {
					pItems.Remove(KVP);
					return true;
				}
			}
			return false;
		}
		public bool Remove(string Key) {
			foreach (KeyValuePair<string, PmlElement> KVP in pItems) {
				if (KVP.Key.Equals(Key, StringComparison.InvariantCultureIgnoreCase)) {
					pItems.Remove(KVP);
					return true;
				}
			}
			return false;
		}
		public bool Remove(KeyValuePair<string, PmlElement> item) {
			return ((ICollection<KeyValuePair<string, PmlElement>>)pItems).Remove(item);
		}

		public int Count {
			get { return pItems.Count; }
		}

		public void Clear() {
			pItems.Clear();
		}

		public bool Contains(PmlElement item) {
			foreach (KeyValuePair<string, PmlElement> KVP in pItems) {
				if (KVP.Value == item) return true;
			}
			return false;
		}
		bool ICollection<KeyValuePair<string, PmlElement>>.Contains(KeyValuePair<string, PmlElement> item) {
			return ((ICollection<KeyValuePair<string, PmlElement>>)pItems).Contains(item);
		}
		public bool ContainsKey(string key) {
			foreach (KeyValuePair<string, PmlElement> KVP in pItems) {
				if (KVP.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase)) return true;
			}
			return false;
		}

		void ICollection<KeyValuePair<string, PmlElement>>.CopyTo(KeyValuePair<string, PmlElement>[] array, int arrayIndex) {
			((ICollection<KeyValuePair<string, PmlElement>>)pItems).CopyTo(array, arrayIndex);
		}
		void ICollection<PmlElement>.CopyTo(PmlElement[] array, int arrayIndex) {
			foreach (KeyValuePair<string, PmlElement> KVP in pItems) {
				array[arrayIndex] = KVP.Value;
				arrayIndex += 1;
			}
		}

		public ICollection<string> Keys {
			get {
				List<string> Ret = new List<string>();
				foreach (KeyValuePair<string, PmlElement> KVP in pItems) Ret.Add(KVP.Key);
				return Ret;
			}
		}
		public ICollection<PmlElement> Values {
			get {
				List<PmlElement> Ret = new List<PmlElement>();
				foreach (KeyValuePair<string, PmlElement> KVP in pItems) Ret.Add(KVP.Value);
				return Ret;
			}
		}
		public IEnumerator<KeyValuePair<string, PmlElement>> GetEnumerator() { return pItems.GetEnumerator(); }
		IEnumerator IEnumerable.GetEnumerator() { return pItems.GetEnumerator(); }
		IEnumerator<PmlElement> IEnumerable<PmlElement>.GetEnumerator() { return Values.GetEnumerator(); }

		public bool IsReadOnly { get { return false; } }


		public override PmlType Type { get { return PmlType.Dictionary; } }

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
		//public override PmlElement GetChild(int index) { return GetChild(index); }
		public override IEnumerable<PmlElement> GetChildren() { return Values; }
		public override IEnumerable<KeyValuePair<String, PmlElement>> GetNamedChildren() { return pItems; }
		public override int GetChildCount() { return pItems.Count; }
		public override void AddChild(string name, PmlElement value) { Add(name, value); }
		public override void AddChild(PmlElement value) { Add(value); }
	}
}