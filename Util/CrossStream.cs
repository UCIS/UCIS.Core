using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace UCIS.Util {
	public class CrossStream : Stream {
		private Queue<byte[]> queue = new Queue<byte[]>();
		private byte[] currentBuffer = null;
		private int currentBufferIndex;
		private CrossStream otherPart;
		private AutoResetEvent resetEvent = new AutoResetEvent(false);

		public static CrossStream CreatePair(out CrossStream stream1, out CrossStream stream2) {
			stream1 = new CrossStream();
			stream2 = new CrossStream(stream1);
			stream1.otherPart = stream2;
			return stream1;
		}
		public static CrossStream CreatePair(out CrossStream stream2) {
			CrossStream stream1 = new CrossStream();
			stream2 = new CrossStream(stream1);
			stream1.otherPart = stream2;
			return stream1;
		}

		private CrossStream() { }
		public CrossStream(CrossStream other) {
			this.otherPart = other;
		}

		public override bool CanRead { get { return true; } }
		public override bool CanWrite { get { return true; } }
		public override bool CanSeek { get { return false; } }
		public override bool CanTimeout { get { return true; } }
		public override long Length { get { throw new NotSupportedException(); } }
		public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
		public override void Flush() { }
		public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
		public override void SetLength(long value) { throw new NotSupportedException(); }
		public override int Read(byte[] buffer, int offset, int count) {
			if (currentBuffer == null) {
				//if (queue.Count == 0) if (!resetEvent.WaitOne(this.ReadTimeout)) throw new TimeoutException();
				if (queue.Count == 0 && !resetEvent.WaitOne()) throw new TimeoutException();
				//while (queue.Count == 0) Thread.Sleep(100);
				resetEvent.Reset();
				currentBuffer = queue.Dequeue();
				currentBufferIndex = 0;
			}
			if (count > currentBuffer.Length - currentBufferIndex) count = currentBuffer.Length - currentBufferIndex;
			Buffer.BlockCopy(currentBuffer, currentBufferIndex, buffer, offset, count);
			currentBufferIndex += count;
			if (currentBufferIndex == currentBuffer.Length) currentBuffer = null;
			return count;
		}
		public override void Write(byte[] buffer, int offset, int count) {
			byte[] tostore;
			if (count == 0) return;
			tostore = new byte[count];
			Buffer.BlockCopy(buffer, offset, tostore, 0, count);
			otherPart.queue.Enqueue(tostore);
			otherPart.resetEvent.Set();
		}
	}
}
