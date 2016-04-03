using System;
using System.IO;
using System.Threading;

namespace UCIS.Util {
	public static class StreamUtil {
		public static void ReadAll(Stream stream, Byte[] buffer, int offset, int count) {
			while (count > 0) {
				int read = stream.Read(buffer, offset, count);
				if (read <= 0) throw new EndOfStreamException();
				offset += read;
				count -= read;
			}
		}
		public static Byte[] ReadAll(Stream stream, int count) {
			Byte[] buffer = new Byte[count];
			ReadAll(stream, buffer, 0, count);
			return buffer;
		}
		public static void ReadAll(Stream stream, Byte[] buffer) {
			ReadAll(stream, buffer, 0, buffer.Length);
		}
		public static void WriteAll(Stream stream, Byte[] buffer) {
			stream.Write(buffer, 0, buffer.Length);
		}
	}
	public class TimedDisposer : IDisposable {
		Timer timer;
		public TimedDisposer(IDisposable target, int timeout) {
			this.timer = new Timer(Callback, target, timeout, Timeout.Infinite);
		}
		void Callback(Object state) {
			((IDisposable)state).Dispose();
			timer.Dispose();
		}
		public void Reset(int timeout) {
			timer.Change(timeout, Timeout.Infinite);
		}
		public void Dispose() {
			timer.Dispose();
		}
	}
}
