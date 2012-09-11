using System;
using System.Collections;
using System.Collections.Generic;
//using KvpType = System.Collections.Generic.KeyValuePair<string, UCIS.Pml.PmlElement>;
using KvpType = UCIS.Pml.PmlArray.KeyValuePair;

namespace UCIS.Pml {
	public class PmlArray : PmlElement, IDictionary<string, PmlElement>, IList<KvpType>, IList<PmlElement>, ICollection<String> {
		/* Internal variables */
		private List<KvpType> _items = new List<KvpType>();
		private int _indexed = 0;

		/* Internal key-value pair structure */
		public class KeyValuePair {
			internal KeyValuePair(string key, PmlElement value) { this.Key = key; this.Value = value; }
			public string Key { get; set; }
			public PmlElement Value { get; set; }
		}

		/* Constructors */
		public PmlArray() {
		}
		public PmlArray(params KeyValuePair<string, PmlElement>[] elements) {
			foreach (KeyValuePair<string, PmlElement> item in elements) Add(item);
		}
		public PmlArray(IEnumerable<KeyValuePair<string, PmlElement>> elements) {
			foreach (KeyValuePair<String, PmlElement> item in elements) Add(item);
		}
		public PmlArray(params PmlElement[] elements) {
			foreach (PmlElement item in elements) Add(item);
		}
		public PmlArray(IEnumerable<PmlElement> elements) {
			foreach (PmlElement item in elements) Add(item);
		}
		public PmlArray(String key1, PmlElement val1) {
			Add(key1, val1);
		}
		public PmlArray(String key1, PmlElement val1, String key2, PmlElement val2) {
			Add(key1, val1);
			Add(key2, val2);
		}
		public PmlArray(String key1, PmlElement val1, String key2, PmlElement val2, String key3, PmlElement val3) {
			Add(key1, val1);
			Add(key2, val2);
			Add(key3, val3);
		}
		public PmlArray(params Object[] interleavedKeyValue) {
			for (int i = 0; i < interleavedKeyValue.Length; ) {
				Add(interleavedKeyValue[i++] as String, interleavedKeyValue[i++]);
			}
		}
		public PmlArray(String[] keys, params PmlElement[] values) {
			if (keys.Length != values.Length) throw new ArgumentException("Length of keys array does not match length of values array");
			for (int i = 0; i < keys.Length; i++) { Add(keys[i] as String, values[i]); }
		}

		public bool IsIndexed {
			get {
				if (_indexed == -1) {
					_indexed = 0;
					foreach (KvpType kvp in _items) {
						if (kvp.Key != null && kvp.Key.Length > 0) {
							_indexed = 1;
							break;
						}
					}
				}
				return _indexed != 0;
			}
		}

		/* PmlElement implementation */
		protected override object GetValue() { return _items; }
		public override PmlElement GetChild(string Name) { return GetItem(Name); }
		public override PmlElement GetChild(int index) { return GetItem(index); }
		public override object ToNumeric() { return _items.Count; }
		public override PmlElementType Type { get { return PmlElementType.Dictionary; } }

		/* Internal KVP lookup */
		private KvpType GetKVP(string name) {
			return _items.Find(delegate(KvpType kvp) { return (kvp.Key == name); });
		}
		private KvpType GetKVP(PmlElement value) {
			return _items.Find(delegate(KvpType kvp) { return (kvp.Value == value); });
		}
		private KvpType GetKVP(int index) {
			if (index < 0 || index >= _items.Count) return null;
			return _items[index];
		}

		/* Item retrieval */
		public PmlElement GetItem(string name) {
			KvpType kvp = GetKVP(name);
			return kvp == null ? null : kvp.Value;
		}
		public PmlElement GetItem(int index) {
			if (index < 0 || index >= _items.Count) return null;
			return _items[index].Value;
		}
		public bool TryGetValue(string key, out PmlElement value) {
			value = GetItem(key);
			return (value != null);
		}

		/* Array implementation */
		public PmlElement this[string key] {
			get { return GetItem(key); }
			set {
				Remove(key);
				Add(key, value);
			}
		}
		public KvpType this[int id] {
			get { return GetKVP(id); }
			set { _items[id] = value; }
		}
		PmlElement IList<PmlElement>.this[int id] {
			get { return GetItem(id); }
			set {
				KvpType item = GetKVP(id);
				item = new KvpType(item == null ? null : item.Key, value);
				_items[id] = item;
			}
		}

		/* Add implementations */
		public void Add(KvpType kvp) { //Final Add method, handles all additions (except insertions!)
			if (_indexed == 0 && kvp.Key != null && kvp.Key.Length > 0) _indexed = 1;
			_items.Add(kvp);
		}

		public PmlElement Add(string Key, PmlElement Element) {
			if (Element == null) Element = new PmlNull();
			Add(new KvpType(Key, Element));
			return Element;
		}

		public PmlElement Add(string key, Object value) {
			PmlElement el;
			if (value == null) {
				el = new PmlNull();
			} else if (value is PmlElement) {
				el = value as PmlElement;
			} else if (value is String) {
				el = new PmlString(value as string);
			} else if (value is Int32 || value is Int64) {
				el = new PmlNumber((Int64)value);
			} else if (value is UInt32 || value is UInt32) {
				el = new PmlNumber((UInt64)value);
			} else {
				el = new PmlString(value.ToString());
			}
			return Add(key, el);
		}
		public PmlElement Add(string Key, string Element) { return Add(Key, new PmlString(Element)); }
		public PmlElement Add(string Key, long Element) { return Add(Key, new PmlInteger(Element)); }
		public PmlElement Add(string Key, ulong Element) { return Add(Key, new PmlInteger(Element)); }

		public PmlElement Add(PmlElement Element) { return Add(null, Element); }
		public PmlElement Add(object Element) { return Add(null, Element); }
		public PmlElement Add(string Element) { return Add(new PmlString(Element)); }
		public PmlElement Add(long Element) { return Add(new PmlInteger(Element)); }
		public PmlElement Add(ulong Element) { return Add(new PmlInteger(Element)); }

		public void Add(KeyValuePair<string, PmlElement> item) { Add(item.Key, item.Value); }

		void ICollection<PmlElement>.Add(PmlElement item) { Add(item); }
		void IDictionary<String, PmlElement>.Add(string key, PmlElement value) { Add(key, value); }

		/* Insert implementations */
		private void Insert(int index, KvpType kvp) {
			if (kvp.Key != null && kvp.Key.Length > 0 && _indexed == 0) _indexed = 1;
			_items.Insert(index, kvp);
		}
		void IList<KvpType>.Insert(int index, KvpType value) { Insert(index, value); }
		void IList<PmlElement>.Insert(int index, PmlElement value) { Insert(index, new KvpType(null, value)); }

		/* Remove */
		public bool Remove(KvpType item) {
			_indexed = -1;
			return _items.Remove(item);
		}
		public void RemoveAt(int index) {
			_indexed = -1;
			_items.RemoveAt(index);
		}
		public bool Remove(PmlElement item) {
			KvpType kvp = GetKVP(item);
			if (kvp == null) return false;
			return Remove(kvp);
		}
		public bool Remove(string Key) {
			KvpType kvp = GetKVP(Key);
			if (kvp == null) return false;
			return Remove(kvp);
		}
		bool ICollection<KeyValuePair<string, PmlElement>>.Remove(KeyValuePair<string, PmlElement> kvp) { return Remove(kvp.Key); }

		public void Clear() {
			_indexed = 0;
			_items.Clear();
		}

		/* Contains */
		public bool Contains(PmlElement item) { return GetKVP(item) != null; }
		public bool Contains(string key) { return GetKVP(key) != null; }
		bool IDictionary<String, PmlElement>.ContainsKey(string key) { return Contains(key); }
		bool ICollection<KvpType>.Contains(KvpType kvp) { return _items.Contains(kvp); }
		bool ICollection<KeyValuePair<String, PmlElement>>.Contains(KeyValuePair<String, PmlElement> kvp) { return Contains(kvp.Key); }

		/* Index lookup */
		public int IndexOf(PmlElement value) { return _items.FindIndex(delegate(KvpType kvp) { return (kvp.Value == value); }); }
		int IList<KvpType>.IndexOf(KvpType value) { return _items.IndexOf(value); }

		/* Copy operations */
		public void CopyTo(KvpType[] array, int offset) {
			_items.CopyTo(array, offset);
		}
		public void CopyTo(PmlElement[] array, int offset) {
			foreach (KvpType kvp in _items) array[offset++] = kvp.Value;
		}
		void ICollection<String>.CopyTo(string[] array, int offset) {
			foreach (KvpType kvp in _items) array[offset++] = kvp.Key;
		}
		void ICollection<KeyValuePair<string, PmlElement>>.CopyTo(KeyValuePair<string, PmlElement>[] array, int offset) {
			foreach (KvpType kvp in _items) array[offset++] = new KeyValuePair<string,PmlElement>(kvp.Key, kvp.Value);
		}

		/* Dictionary props */
		public ICollection<PmlElement> Values { get { return this as ICollection<PmlElement>; } }
		public ICollection<String> Keys { get { return this as ICollection<String>; } }

		/* Stuf... */
		public int Count { get { return _items.Count; } }
		public bool IsReadOnly { get { return false; } }
		void ICollection<String>.Add(string value) { throw new NotImplementedException(); }

		/* Enumerators */
		IEnumerator<KvpType> IEnumerable<KvpType>.GetEnumerator() { return _items.GetEnumerator(); }
		IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
		public IEnumerator<PmlElement> GetEnumerator() { return new ValueEnumerator(this); }
		IEnumerator<String> IEnumerable<String>.GetEnumerator() { return new KeyEnumerator(this); }
		IEnumerator<KeyValuePair<string, PmlElement>> IEnumerable<KeyValuePair<string, PmlElement>>.GetEnumerator() { return new KvpEnumerator(this); }

		private class KeyEnumerator : IEnumerator<String> {
			protected IEnumerator<KvpType> _enum;
			internal KeyEnumerator(PmlArray array) { _enum = array._items.GetEnumerator(); }
			private KvpType KVP { get { return _enum.Current; } }
			public bool MoveNext() { return _enum.MoveNext(); }
			public void Reset() { _enum.Reset(); }
			public void Dispose() { _enum.Dispose(); }
			Object IEnumerator.Current { get { return Current; } }

			public String Current { get { return KVP == null ? null : KVP.Key; } }
		}
		private class ValueEnumerator : IEnumerator<PmlElement> {
			protected IEnumerator<KvpType> _enum;
			internal ValueEnumerator(PmlArray array) { _enum = array._items.GetEnumerator(); }
			private KvpType KVP { get { return _enum.Current; } }
			public bool MoveNext() { return _enum.MoveNext(); }
			public void Reset() { _enum.Reset(); }
			public void Dispose() { _enum.Dispose(); }
			Object IEnumerator.Current { get { return Current; } }

			public PmlElement Current { get { return KVP == null ? null : KVP.Value; } }
		}
		private class KvpEnumerator : IEnumerator<KeyValuePair<String, PmlElement>> {
			protected IEnumerator<KvpType> _enum;
			internal KvpEnumerator(PmlArray array) { _enum = array._items.GetEnumerator(); }
			private KvpType KVP { get { return _enum.Current; } }
			public bool MoveNext() { return _enum.MoveNext(); }
			public void Reset() { _enum.Reset(); }
			public void Dispose() { _enum.Dispose(); }
			Object IEnumerator.Current { get { return Current; } }

			public KeyValuePair<String, PmlElement> Current { get {
				return KVP == null ? default(KeyValuePair<String, PmlElement>) : new KeyValuePair<String, PmlElement>(KVP.Key, KVP.Value);
			} }
		}
	}
}