using System;
using System.Collections.Generic;
using System.Text;
using UCIS.Pml;

namespace UCIS.Cci {
	public enum CciResultType {
		Success,
		Error,
		Value,
		Message,
		List,
		Pml,
		Object,
		Binary
	}

	public class CciResult {
		private CciResultType _type;
		private Object _value;

		public CciResult(CciResultType type, Object value) {
			_type = type;
			_value = value;
		}
		public CciResult(CciResultType type) : this(type, null) { }
		public CciResult(Exception ex) : this(CciResultType.Error, ex) { }
		public CciResult(Pml.PmlElement pml) : this(CciResultType.Pml, pml) { }
		public CciResult(byte[] binary) : this(CciResultType.Binary, binary) { }
		public CciResult(string[] stringList) : this(CciResultType.List, stringList) { }
		public CciResult(string message) : this(CciResultType.Message, message) { }

		public override string ToString() {
			if (_value == null) {
				return _type.ToString();
			} else {
				return _type.ToString() + ": " + ValueToString();
			}
		}
		public string ValueToString() {
			if (_value == null) {
				return "";
			} else if (_value is string) {
				return (string)_value;
			} else if (_value is byte[]) {
				return Encoding.UTF8.GetString((byte[])_value);
			} else if (_value is PmlElement) {
					return Pml.PmlTextWriter.GetMessageString((PmlElement)_value);
			} else if (_value is IEnumerable<Object>) {
				StringBuilder sb = new StringBuilder();
				foreach (Object i in (Array)_value) sb.AppendLine(i.ToString());
				return sb.ToString();
			} else {
				return _value.ToString();
			}
		}
		public PmlElement ValueToPml() {
			if (_value == null) return new PmlNull();
			switch (_type) {
				case CciResultType.Binary: return new PmlBinary((byte[])_value);
				case CciResultType.Message:
				case CciResultType.Object:
				case CciResultType.Value:
				case CciResultType.Error: return new PmlString(_value.ToString());
				case CciResultType.Success: return new PmlInteger(1);
				case CciResultType.List: {
						PmlCollection c = new PmlCollection();
						foreach (Object i in (Array)_value) c.Add(i.ToString());
						return c;
					}
				case CciResultType.Pml: return (PmlElement)_value;
				default: return new PmlString("Unknown type: " + _type.ToString());
			}
		}
		public PmlElement ToPml() {
			PmlDictionary d = new PmlDictionary();
			//d.Add("Type", (int)_type);
			d.Add("Type", (int)CciResultType.Message);
			//d.Add("TypeName", _type.ToString());
			d.Add("TypeName", "Message");
			//d.Add("Value", ValueToPml());
			d.Add("Value", ToString());
			d.Add("String", ToString());
			return d;
		}

		public CciResultType Type { get { return _type; } }
		public Object Value { get { return _value; } }
	}

	public class CciCommand {
		private string[] _command;
		private int _offset;
		private CciResult _result;

		public CciCommand(string[] command) {
			_command = command;
			_offset = 0;
			_result = null;
		}

		public int Offset { get { return _offset; } set { _offset = value; } }

		public static CciCommand Parse(string command, bool urlEncode, bool cEscape, bool quotes) {
			List<string> l = new List<string>();

			StringBuilder part = new StringBuilder();
			bool inQuotes = false;
			int len = command.Length;
			for (int i = 0; i < len; i++) {
				char c = command[i];
				if (quotes && !inQuotes && c == '"') {
					inQuotes = true;
				} else if (quotes && inQuotes && c == '"') {
					inQuotes = false;
				} else if (urlEncode && c == '%') {
					part.Append(Uri.HexUnescape(command, ref i));
					i--;
				} else if (cEscape && c == '\\' && i + 1 < len) {
					i++;
					switch (command[i]) {
						case 't': part.Append('\t'); break;
						case 'n': part.Append('\n'); break;
						case 'r': part.Append('\r'); break;
						default: part.Append(command[i]); break;
					}
				} else if (!inQuotes && (c == ' ' || c == '\t')) {
					if (part.Length > 0) l.Add(part.ToString());
					part.Length = 0;
				} else {
					part.Append(c);
				}
			}
			if (part.Length > 0) l.Add(part.ToString());

			return new CciCommand(l.ToArray());
		}

		public PmlCollection ToPml() { return ToPml(0); }
		public PmlCollection ToPml(int offset) {
			PmlCollection c = new PmlCollection();
			for (int i = _offset + offset; i < _command.Length; i++) c.Add(_command[i]);
			return c;
		}
		public static CciCommand FromPml(PmlCollection pml) {
			List<string> l = new List<string>();
			foreach (PmlElement e in pml) l.Add(e.ToString());
			return new CciCommand(l.ToArray());
		}

		public string Command {
			get {
				return _command[_offset];
			}
		}

		public string GetArgument(int index) {
			return _command[_offset + index + 1];
		}

		public CciCommand Jump(int offset) {
			_offset += offset;
			return this;
		}

		public int Count {
			get {
				return _command.Length - _offset - 1;
			}
		}

		public CciResult Result {
			get {
				return _result;
			}
			set {
				_result = value;
			}
		}

		public CciCommand Return(CciResult result) {
			_result = result;
			return this;
		}
		public CciCommand Return(CciResultType type, Object value) { return Return(new CciResult(type, value)); }
		public CciCommand Return() { return Return(CciResultType.Success); }
		public CciCommand Return(int value) { return Return(CciResultType.Value, value); }
		public CciCommand Return(CciResultType type) { return Return(type, null); }
		public CciCommand Return(Exception ex) { return Return(CciResultType.Error, ex); }
		public CciCommand Return(PmlElement pml) { return Return(CciResultType.Pml, pml); }
		public CciCommand Return(byte[] binary) { return Return(CciResultType.Binary, binary); }
		public CciCommand Return(string[] stringList) { return Return(CciResultType.List, stringList); }
		public CciCommand Return(string message) { return Return(CciResultType.Message, message); }
	}
}
