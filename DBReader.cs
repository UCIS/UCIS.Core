using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace UCIS {
	public class DBReader : IEnumerable, IEnumerator {
		private IDbCommand _Command;
		private IDataReader _Reader;
		private object[] _CurrentRow;

		internal DBReader(IDbCommand Command) {
			_Command = Command;
		}

		public bool Read() {
			if (_Reader == null) {
				if (_Command == null) return false;
				_Reader = _Command.ExecuteReader();
			}
			if (_Reader.Read()) {
				return true;
			} else {
				Close();
				return false;
			}
		}

		public void Close() {
			if (_Reader != null) _Reader.Close();
			if (_Command != null) {
				_Command.Connection.Close();
				_Command.Dispose();
			}
			_Command = null;
			_CurrentRow = null;
			_Reader = null;
		}

		public object GetField() {
			if (_Reader == null) {
				return _Command.ExecuteScalar();
			} else {
				return _Reader.GetValue(0);
			}
		}

		public object GetField(int Offset) {
			if (_Reader == null) Read();
			return _Reader.GetValue(Offset);
		}
		public object[] GetRow(bool GoNextRow) {
			object[] Result = null;
			if (_Reader == null) {
				if (!Read()) return null;
			}
			Result = new object[_Reader.FieldCount];

			_Reader.GetValues(Result);
			if (GoNextRow) Read();
			return Result;
		}
		public object[] GetRow() {
			return GetRow(true);
		}
		public object[][] GetAllRows() {
			List<object[]> Result = new List<object[]>();
			object[] ResultArray = null;
			if (_Reader == null) {
				if (!Read()) return null;
			}
			do {
				ResultArray = new object[_Reader.FieldCount];

				_Reader.GetValues(ResultArray);
				Result.Add(ResultArray);
			}
			while (_Reader.Read());
			Close();
			return Result.ToArray();
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return this;
		}
		object IEnumerator.Current {
			get { return _CurrentRow; }
		}
		bool IEnumerator.MoveNext() {
			if (!Read()) return false;
			_CurrentRow = GetRow(false);
			return true;
		}
		void IEnumerator.Reset() {
			throw new NotImplementedException();
		}

		public void Dispose() {
			Close();
		}
	}
}
