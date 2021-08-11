using System;
using System.Collections.Generic;
using System.IO;

namespace UCIS.Util {
	public static class INIReader {
		public static IDictionary<String, IDictionary<String, String>> ParseFile(String fileName) {
			using (TextReader reader = File.OpenText(fileName)) return Parse(reader);
		}
		public static IDictionary<String, IDictionary<String, String>> Parse(Stream stream) {
			return Parse(new StreamReader(stream));
		}
		public static IDictionary<String, IDictionary<String, String>> Parse(String data) {
			return Parse(new StringReader(data));
		}
		public static IDictionary<String, IDictionary<String, String>> Parse(TextReader reader) {
			IDictionary<String, IDictionary<String, String>> result = new Dictionary<String, IDictionary<String, String>>();
			String section = "";
			while (true) {
				String line = reader.ReadLine();
				if (line == null) break;
				line = line.TrimEnd();
				if (line.Length == 0 || line.StartsWith(";")) {
					continue;
				} else if (line.StartsWith("[") && line.EndsWith("]")) {
					section = line.Substring(1, line.Length - 2);
					if (!result.ContainsKey(section)) result.Add(section, new Dictionary<String, String>());
				} else if (line.Contains("=")) {
					String[] parts = line.Split(new Char[] { '=' }, 2);
					IDictionary<String, String> parent;
					if (!result.TryGetValue(section, out parent)) result.Add(section, parent = new Dictionary<String, String>());
					parent.Add(parts[0], parts[1]);
				} else {
					throw new FormatException("Could not parse INI line: " + line);
				}
			}
			return result;
		}
	}
}
