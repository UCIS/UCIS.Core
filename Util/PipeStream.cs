using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace UCIS.Util {
	public class PipeStream : Stream {
		private Queue<byte[]> queue = new Queue<byte[]>();
		private byte[] currentBuffer = null;
		private int currentBufferIndex;
		private AutoResetEvent resetEvent = new AutoResetEvent(false);
		private Boolean closed = false;

		public PipeStream() {
			ReadTimeout = Timeout.Infinite;
		}

		public override int ReadTimeout { get; set; }
		public override bool CanRead { get { return !closed; } }
		public override bool CanWrite { get { return !closed; } }
		public override bool CanSeek { get { return false; } }
		public override bool CanTimeout { get { return !closed; } }
		public override long Length { get { throw new NotSupportedException(); } }
		public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
		public override void Flush() { }
		public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
		public override void SetLength(long value) { throw new NotSupportedException(); }
		public override int Read(byte[] buffer, int offset, int count) {
			if (closed) throw new ObjectDisposedException("PipeStream");
			if (currentBuffer == null) {
				//if (queue.Count == 0) if (!resetEvent.WaitOne(this.ReadTimeout)) throw new TimeoutException();
				if (queue.Count == 0 && (ReadTimeout == 0 || !resetEvent.WaitOne(ReadTimeout))) throw new TimeoutException();
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
			if (closed) throw new ObjectDisposedException("PipeStream");
			if (count == 0) return;
			tostore = new byte[count];
			Buffer.BlockCopy(buffer, offset, tostore, 0, count);
			queue.Enqueue(tostore);
			resetEvent.Set();
		}
		public override void Close() {
			closed = true;
			resetEvent.Set();
			base.Close();
		}
	}
}
