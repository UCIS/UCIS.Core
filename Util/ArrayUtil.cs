﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Globalization;

namespace UCIS.Util {
	public class ArrayUtil {
		public static T[] Slice<T>(T[] input, int offset) {
			if (offset < 0) offset = input.Length + offset;
			return Slice(input, offset, input.Length - offset);
		}
		public static T[] Slice<T>(T[] input, int offset, int count) {
			if (offset < 0) offset = input.Length + offset;
			if (count < 0) count = input.Length + count - offset;
			T[] output = new T[count];
			Array.Copy(input, offset, output, 0, count);
			return output;
		}
		public static Object[] ToArray(ICollection input) {
			Object[] output = new Object[input.Count];
			input.CopyTo(output, 0);
			return output;
		}
		public static T[] ToArray<T>(ICollection input) {
			T[] output = new T[input.Count];
			input.CopyTo(output, 0);
			return output;
		}
		public static T[] ToArray<T>(ICollection<T> input) {
			T[] output = new T[input.Count];
			input.CopyTo(output, 0);
			return output;
		}
		public static T[] ToArray<T>(ArraySegment<T> input) {
			return Slice(input.Array, input.Offset, input.Count);
		}
		public static T[] ToArray<T>(T[] input) {
			return (T[])input.Clone();
		}
		public static Tout[] Convert<Tin, Tout>(IList<Tin> input, Converter<Tin, Tout> converter) {
			Tout[] output = new Tout[input.Count];
			for (int i = 0; i < output.Length; i++) output[i] = converter(input[i]);
			return output;
		}
		public static T[] Convert<T>(IList input, Converter<Object, T> converter) {
			T[] output = new T[input.Count];
			for (int i = 0; i < output.Length; i++) output[i] = converter(input[i]);
			return output;
		}
		public static IList<T> ToList<T>(IEnumerable<T> input) {
			return new List<T>(input);
		}
		public static void GnomeSort<T>(IList<T> a, Comparison<T> comparer) {
			int pos = 1;
			while (pos < a.Count) {
				if (comparer(a[pos], a[pos - 1]) >= 0) {
					pos++;
				} else {
					T tmp = a[pos];
					a[pos] = a[pos - 1];
					a[pos - 1] = tmp;
					if (pos > 1) pos--; else pos++;
				}
			}
		}
		//Array shuffle
		//Array unique
		public static T[] Concat<T>(params ArraySegment<T>[] parts) {
			int count = 0;
			foreach (ArraySegment<T> segment in parts) count += segment.Count;
			T[] ret = new T[count];
			int offset = 0;
			foreach (ArraySegment<T> segment in parts) {
				Array.Copy(segment.Array, segment.Offset, ret, offset, segment.Count);
				offset += segment.Count;
			}
			return ret;
		}
		public static T[] Concat<T>(params T[][] parts) {
			int count = 0;
			foreach (T[] segment in parts) count += segment.Length;
			T[] ret = new T[count];
			int offset = 0;
			foreach (T[] segment in parts) {
				segment.CopyTo(ret, offset);
				offset += segment.Length;
			}
			return ret;
		}
		public static T[] Concat<T>(params IList<T>[] parts) {
			int count = 0;
			foreach (IList<T> segment in parts) count += segment.Count;
			T[] ret = new T[count];
			int offset = 0;
			foreach (IList<T> segment in parts) {
				segment.CopyTo(ret, offset);
				offset += segment.Count;
			}
			return ret;
		}
		public static Boolean Equal<T>(T[] a, T[] b, IEqualityComparer<T> comparer) {
			if (ReferenceEquals(a, b)) return true;
			if (a == null || b == null) return false;
			if (a.Length != b.Length) return false;
			for (int i = 0; i < a.Length; i++) if (!comparer.Equals(a[i], b[i])) return false;
			return true;
		}
		public static Boolean Equal<T>(T[] a, T[] b) where T : IEquatable<Byte> {
			if (ReferenceEquals(a, b)) return true;
			if (a == null || b == null) return false;
			if (a.Length != b.Length) return false;
			for (int i = 0; i < a.Length; i++) {
				if (ReferenceEquals(a[i], b[i])) continue;
				if (ReferenceEquals(a[i], null) || ReferenceEquals(b[i], null)) continue;
				if (!a[i].Equals(b[i])) return false;
			}
			return true;
		}
		public static Boolean Equal(Byte[] a, Byte[] b) {
			if (ReferenceEquals(a, b)) return true;
			if (a == null || b == null) return false;
			if (a.Length != b.Length) return false;
			for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
			return true;
		}
		public static int GetHashCode<T>(T[] array) {
			int h = 0;
			foreach (T v in array) h ^= v.GetHashCode();
			return h;
		}
		public static int Add<T>(ref T[] array, params T[] items) {
			if (array == null) {
				array = new T[items.Length];
				items.CopyTo(array, 0);
				return 0;
			} else {
				int index = array.Length;
				Array.Resize(ref array, index + items.Length);
				items.CopyTo(array, index);
				return index;
			}
		}
		public static int Add<T>(ref T[] array, ICollection<T> items) {
			if (array == null) {
				array = new T[items.Count];
				items.CopyTo(array, 0);
				return 0;
			} else {
				int index = array.Length;
				Array.Resize(ref array, index + items.Count);
				items.CopyTo(array, index);
				return index;
			}
		}
		public static int Add<T>(ref T[] array, T item) {
			if (array == null) {
				array = new T[] { item };
				return 0;
			} else {
				int index = array.Length;
				Array.Resize(ref array, index + 1);
				array[index] = item;
				return index;
			}
		}
		public static int AddUnique<T>(ref T[] array, T item) {
			if (array == null) {
				array = new T[] { item };
				return 0;
			} else {
				int index = Array.IndexOf(array, item);
				if (index == -1) index = Add(ref array, item);
				return index;
			}
		}
		public static Boolean Remove<T>(ref T[] array, T item) {
			if (array == null) return false;
			int index = Array.IndexOf(array, item);
			if (index == -1) return false;
			T[] newarray = new T[array.Length - 1];
			if (index > 0) Array.Copy(array, 0, newarray, 0, index);
			if (index < array.Length - 1) Array.Copy(array, index + 1, newarray, index, array.Length - index - 1);
			array = newarray;
			return true;
		}
	}

	public static class ByteArrayUtil {
		public static String ToHexString(Byte[] bytes) {
			StringBuilder sb = new StringBuilder(bytes.Length * 2);
			foreach (Byte b in bytes) sb.Append(b.ToString("x2"));
			return sb.ToString();
		}
		public static Byte[] FromHexString(String hex) {
			if (hex.Length % 2 != 0) hex = "0" + hex;
			Byte[] r = new Byte[hex.Length / 2];
			for (int i = 0; i < r.Length; i++) if (!Byte.TryParse(hex.Substring(2 * i, 2), NumberStyles.HexNumber, null, out r[i])) return null;
			return r;

			{
				Byte[] binary = new Byte[(hex.Length + 1) / 2];
				int i = 0;
				foreach (Char c in hex) {
					if (Char.IsWhiteSpace(c)) continue;
					Byte v;
					if (c >= '0' && c <= '9') v = (Byte)(c - '0');
					else if (c >= 'a' && c <= 'f') v = (Byte)(c - 'a' + 10);
					else if (c >= 'A' && c <= 'F') v = (Byte)(c - 'A' + 10);
					else throw new InvalidOperationException("Unexpected character: " + c);
					if ((i % 2) == 0) v <<= 4;
					binary[i / 2] |= v;
					i++;
				}
				if ((i % 2) != 0) throw new InvalidOperationException("Odd number of data characters in input string");
				if (binary.Length != i / 2) Array.Resize(ref binary, i / 2);
				return binary;
			}
		}
	}
}
