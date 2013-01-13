using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using SysThreadPool = System.Threading.ThreadPool;

namespace UCIS.Util {
	public abstract class QueuedPacketStream : PacketStream {
		Queue<Byte[]> ReceiveQueue = new Queue<byte[]>();
		Byte[] ReceiveBuffer = null;
		int ReceiveBufferOffset = 0;
		int ReceiveWaiting = 0;
		AutoResetEvent ReceiveEvent = new AutoResetEvent(false);
		AsyncResult AsyncReceiveOperation = null;
		protected Boolean Closed { get; private set; }

		public QueuedPacketStream() {
			ReadTimeout = Timeout.Infinite;
			Closed = false;
		}

		protected void AddReadBufferCopy(Byte[] buffer, int offset, int count) {
			Byte[] store;
			store = new Byte[count];
			Buffer.BlockCopy(buffer, offset, store, 0, count);
			AddReadBufferNoCopy(store);
		}
		protected void AddReadBufferNoCopy(Byte[] store) {
			if (Closed) return;
			lock (ReceiveQueue) {
				ReceiveQueue.Enqueue(store);
				Interlocked.Add(ref ReceiveWaiting, store.Length);
				ReceiveEvent.Set();
				if (AsyncReceiveOperation != null && (store.Length > 0 || AsyncReceiveOperation.IsReadPacket)) {
					AsyncReceiveOperation.SetCompleted(false);
					AsyncReceiveOperation = null;
				}
			}
		}
		public override void Close() {
			Closed = true;
			base.Close();
			ReceiveEvent.Set();
			lock (ReceiveQueue) {
				if (AsyncReceiveOperation != null) {
					AsyncReceiveOperation.SetCompleted(false);
					AsyncReceiveOperation = null;
				}
			}
		}

		public override bool CanSeek { get { return false; } }
		public override bool CanTimeout { get { return true; } }
		public override bool CanRead { get { return !Closed; } }
		public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
		public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
		public override void SetLength(long value) { throw new NotSupportedException(); }

		public override int ReadTimeout { get; set; }
		public override long Length { get { return ReceiveWaiting; } }

		public int WaitForPacket() {
			while (ReceiveBuffer == null) {
				lock (ReceiveQueue) {
					if (ReceiveQueue.Count > 0) {
						ReceiveBuffer = ReceiveQueue.Dequeue();
						ReceiveBufferOffset = 0;
						continue;
					}
				}
				if (Closed) throw new ObjectDisposedException("QueuedPacketStream", "The connection has been closed");
				if (ReadTimeout == 0 || !ReceiveEvent.WaitOne(ReadTimeout, false)) throw new TimeoutException();
			}
			return ReceiveBuffer.Length - ReceiveBufferOffset;
		}
		public override int Read(byte[] buffer, int offset, int count) {
			int left = 0;
			while (true) {
				left = WaitForPacket();
				if (left > 0) break;
				ReceiveBuffer = null;
			}
			if (count > left) count = left;
			Buffer.BlockCopy(ReceiveBuffer, ReceiveBufferOffset, buffer, offset, count);
			ReceiveBufferOffset += count;
			if (ReceiveBufferOffset == ReceiveBuffer.Length) ReceiveBuffer = null;
			Interlocked.Add(ref ReceiveWaiting, -count);
			return count;
		}
		public override Byte[] ReadPacket() {
			WaitForPacket();
			Byte[] arr = ReceiveBuffer;
			if (ReceiveBufferOffset > 0) {
				arr = new Byte[ReceiveBuffer.Length - ReceiveBufferOffset];
				Buffer.BlockCopy(ReceiveBuffer, ReceiveBufferOffset, arr, 0, arr.Length - ReceiveBufferOffset);
			}
			ReceiveBuffer = null;
			return arr;
		}
		public override ArraySegment<byte> ReadPacketFast() {
			WaitForPacket();
			ArraySegment<byte> ret = new ArraySegment<byte>(ReceiveBuffer, ReceiveBufferOffset, ReceiveBuffer.Length - ReceiveBufferOffset);
			ReceiveBuffer = null;
			return ret;
		}

		class AsyncResult : AsyncResultBase {
			public Boolean IsReadPacket { get; private set; }
			public Byte[] Buffer = null;
			public int BufferOffset = 0;
			public int BufferLength = 0;

			public void SetCompleted(Boolean synchronously) {
				base.SetCompleted(synchronously, null);
			}
			public AsyncResult(AsyncCallback callback, Object state) : base(callback, state) {
				IsReadPacket = true;
			}
			public AsyncResult(AsyncCallback callback, Object state, Byte[] buffer, int bufferOffset, int bufferLength) : base(callback, state) {
				this.Buffer = buffer;
				this.BufferOffset = bufferOffset;
				this.BufferLength = bufferLength;
				IsReadPacket = false;
			}
		}

		private IAsyncResult BeginAsyncReadOperation(AsyncResult ar) {
			lock (ReceiveQueue) {
				if (AsyncReceiveOperation != null) throw new InvalidOperationException("Another asynchronous operation is in progress");
				if (ReceiveBuffer != null || ReceiveQueue.Count > 0) {
					ar.SetCompleted(true);
				} else {
					if (Closed) throw new ObjectDisposedException("QueuedPacketStream", "The connection has been closed");
					AsyncReceiveOperation = ar;
				}
			}
			return ar;
		}
		private void EndAsyncReadOperation(AsyncResult ar) {
			lock (ReceiveQueue) {
				if (AsyncReceiveOperation != null && ar != AsyncReceiveOperation) throw new InvalidOperationException("The given AsyncResult object does not match the current pending operation");
				AsyncReceiveOperation = null;
			}
		}
		public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state) {
			return BeginAsyncReadOperation(new AsyncResult(callback, state, buffer, offset, count));
		}
		public override IAsyncResult BeginReadPacket(AsyncCallback callback, object state) {
			return BeginAsyncReadOperation(new AsyncResult(callback, state));
		}
		public override IAsyncResult BeginReadPacketFast(AsyncCallback callback, object state) {
			return BeginAsyncReadOperation(new AsyncResult(callback, state));
		}
		public override int EndRead(IAsyncResult asyncResult) {
			AsyncResult ar = (AsyncResult)asyncResult;
			EndAsyncReadOperation(ar);
			return Read(ar.Buffer, ar.BufferOffset, ar.BufferLength);
		}
		public override Byte[] EndReadPacket(IAsyncResult asyncResult) {
			AsyncResult ar = (AsyncResult)asyncResult;
			EndAsyncReadOperation(ar);
			return ReadPacket();
		}
		public override ArraySegment<Byte> EndReadPacketFast(IAsyncResult asyncResult) {
			AsyncResult ar = (AsyncResult)asyncResult;
			EndAsyncReadOperation(ar);
			return ReadPacketFast();
		}
	}
}
