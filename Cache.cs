using System;
using System.Collections.Generic;

namespace UCIS {
	public class Cache<TKey, TValue> {
		private Dictionary<TKey, WeakReference> _items = new Dictionary<TKey, WeakReference>();

		public int Count { get { return _items.Count; } }
		public int GetLiveCount() {
			Purge();
			return _items.Count;
		}

		private void getItem(TKey key, out TValue value, out bool exists, out bool collected) {
			WeakReference w;
			lock (_items) {
				if (!_items.TryGetValue(key, out w)) {
					value = default(TValue);
					exists = false;
					collected = false;
					return;
				}
				Object r = w.Target;
				if (r == null) {
					_items.Remove(key);
					value = default(TValue);
					exists = false;
					collected = true;
					return;
				}
				value = (TValue)r;
				exists = true;
				collected = false;
				return;
			}
		}

		public bool TryGetValue(TKey key, out TValue value) {
			bool exists, collected;
			getItem(key, out value, out exists, out collected);
			return exists;
		}

		public void Purge() {
			lock (_items) {
				List<TKey> remove = new List<TKey>();
				foreach (KeyValuePair<TKey, WeakReference> kvp in _items) {
					if (!kvp.Value.IsAlive) remove.Add(kvp.Key);
				}
				foreach (TKey key in remove) {
					_items.Remove(key);
				}
			}
		}

		public void Add(TKey key, TValue value) {
			lock(_items) _items.Add(key, new WeakReference(value));
		}

		public bool Remove(TKey key) {
			lock(_items) return _items.Remove(key);
		}

		public void Clear() {
			lock (_items) _items.Clear();
		}
	}
}
