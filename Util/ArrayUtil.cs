using System;
using System.Collections;
using System.Collections.Generic;

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
		public static T[] Merge<T>(params ArraySegment<T>[] parts) {
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
		public static T[] Merge<T>(params T[][] parts) {
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
		public static T[] Merge<T>(params IList<T>[] parts) {
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
		public static void Add<T>(ref T[] array, params T[] items) {
			if (array == null) {
				array = new T[items.Length];
				items.CopyTo(array, 0);
			} else {
				int index = array.Length;
				Array.Resize(ref array, index + items.Length);
				items.CopyTo(array, index);
			}
		}
		public static void Add<T>(ref T[] array, ICollection<T> items) {
			if (array == null) {
				array = new T[items.Count];
				items.CopyTo(array, 0);
			} else {
				int index = array.Length;
				Array.Resize(ref array, index + items.Count);
				items.CopyTo(array, index);
			}
		}
		public static void Add<T>(ref T[] array, T item) {
			if (array == null) {
				array = new T[] { item };
			} else {
				int index = array.Length;
				Array.Resize(ref array, index + 1);
				array[index] = item;
			}
		}
		public static void AddUnique<T>(ref T[] array, T item) {
			if (Array.IndexOf(array, item) != -1) return;
			Add(ref array, item);
		}
	}
}
