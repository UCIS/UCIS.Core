using System;
using System.IO;

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

		class IOAsyncResult : AsyncResultBase {
			public Stream Stream { get; set; }
			public Byte[] Buffer { get; set; }
			public int Offset { get; set; }
			public int Left { get; set; }
			public int Count { get; set; }
			public IOAsyncResult(AsyncCallback callback, Object state) : base(callback, state) { }
			public void SetCompleted(Boolean synchronously, int count, Exception error) {
				this.Count = count;
				base.SetCompleted(synchronously, error);
			}
			public new void SetCompleted(Boolean synchronously, Exception error) {
				base.SetCompleted(synchronously, error);
			}
			public new int WaitForCompletion() {
				base.WaitForCompletion();
				ThrowError();
				return Count;
			}
		}
		public static IAsyncResult BeginReadAll(Stream stream, byte[] buffer, int offset, int count, AsyncCallback callback, object state) {
			IOAsyncResult ar = new IOAsyncResult(callback, state) { Stream = stream, Buffer = buffer, Offset = 0, Count = 0, Left = count };
			if (ar.Left <= 0) {
				ar.SetCompleted(true, null);
				return ar;
			}
			stream.BeginRead(ar.Buffer, ar.Offset, ar.Left, asyncReadAllReadCallback, ar);
			return ar;
		}
		private static void asyncReadAllReadCallback(IAsyncResult ar) {
			IOAsyncResult myar = (IOAsyncResult)ar.AsyncState;
			try {
				int len = myar.Stream.EndRead(ar);
				if (len <= 0) throw new EndOfStreamException();
				myar.Offset += len;
				myar.Left -= len;
				myar.Count += len;
				if (myar.Left > 0) {
					myar.Stream.BeginRead(myar.Buffer, myar.Offset, myar.Left, asyncReadAllReadCallback, ar);
				} else {
					myar.SetCompleted(false, myar.Count, null);
				}
			} catch (Exception ex) {
				myar.SetCompleted(false, ex);
			}
		}
		public static int EndReadAll(IAsyncResult asyncResult) {
			IOAsyncResult myar = (IOAsyncResult)asyncResult;
			return myar.WaitForCompletion();
		}
	}
}
