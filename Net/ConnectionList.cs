using System;
using System.Collections;
using System.Collections.Generic;

namespace UCIS.Net {
	public class NetworkConnectionList : ICollection<INetworkConnection> {
		List<INetworkConnection> _list = new List<INetworkConnection>();

		public ulong BytesWritten {
			get {
				ulong sum = 0;
				lock (_list) foreach (INetworkConnection c in _list) sum += c.BytesWritten;
				return sum;
			}
		}
		public ulong BytesRead {
			get {
				ulong sum = 0;
				lock (_list) foreach (INetworkConnection c in _list) sum += c.BytesRead;
				return sum;
			}
		}
		public NetworkConnectionList FindByHandler(Object Handler) {
			NetworkConnectionList l = new NetworkConnectionList();
			lock (_list) foreach (INetworkConnection c in _list) if (c.Handler == Handler) l.Add(c);
			return l;
		}
		public NetworkConnectionList FindByHandlerType(Type HandlerType) {
			NetworkConnectionList l = new NetworkConnectionList();
			lock (_list) foreach (INetworkConnection c in _list) if (c.Handler.GetType() == HandlerType) l.Add(c);
			return l;
		}
		public void CloseAll() {
			foreach (INetworkConnection c in ToArray()) c.Close();
		}

		public void Add(INetworkConnection item) {
			if (item == null) throw new ArgumentNullException("item");
			lock (_list) _list.Add(item);
			item.Closed += _ConnectionClosed;
			if (!item.Connected) Remove(item);
		}
		public void Insert(int index, INetworkConnection item) {
			throw new NotSupportedException();
		}
		public bool Remove(INetworkConnection item) {
			if (item == null) throw new ArgumentNullException("item");
			item.Closed -= _ConnectionClosed;
			lock (_list) return _list.Remove(item);
		}
		public void RemoveAt(int index) {
			lock (_list) {
				INetworkConnection item = _list[index];
				item.Closed -= _ConnectionClosed;
				_list.Remove(item);
			}
		}
		public int Count { get { lock (_list) return _list.Count; } }
		public bool IsReadOnly { get { return false; } }
		public bool Contains(INetworkConnection item) {
			lock (_list) return _list.Contains(item);
		}
		public void Clear() {
			lock (_list) {
				foreach (INetworkConnection c in _list) c.Closed -= _ConnectionClosed;
				_list.Clear();
			}
		}
		public INetworkConnection this[int index] {
			get { return _list[index]; }
			set { throw new NotSupportedException(); }
		}
		public int IndexOf(INetworkConnection item) {
			lock (_list) return _list.IndexOf(item);
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return new Enumerator(_list);
		}
		public IEnumerator<INetworkConnection> GetEnumerator() {
			return new Enumerator(_list);
		}

		public void CopyTo(INetworkConnection[] array, int offset) {
			lock (_list) _list.CopyTo(array, offset);
		}

		public INetworkConnection[] ToArray() {
			lock (_list) return _list.ToArray();
		}

		private void _ConnectionClosed(Object sender, EventArgs e) {
			if (sender == null || !(sender is INetworkConnection)) return;
			Remove((INetworkConnection)sender);
		}

		private class Enumerator : IEnumerator<INetworkConnection> {
			int index = 0;
			INetworkConnection current = null;
			IList<INetworkConnection> list;

			public Enumerator(IList<INetworkConnection> list) {
				this.list = list;
			}

			public void Reset() {
				index = 0;
			}
			public bool MoveNext() {
				lock (list) {
					if (index < list.Count) {
						current = list[index];
						index++;
						return true;
					} else {
						current = null;
						return false;
					}
				}
			}
			public INetworkConnection Current {
				get { return current; } 
			}
			object IEnumerator.Current {
				get { return current; }
			}
			public void Dispose() {
				current = null;
				list = null;
			}
		}
	}
}
