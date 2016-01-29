using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;

namespace UCIS.Util {
	public class PrebufferingStream : Stream {
		class AsyncResult : AsyncResultBase {
			public Byte[] Buffer { get; set; }
			public int Offset { get; set; }
			public int Left { get; set; }
			public int Count { get; set; }
			public AsyncResult(AsyncCallback callback, Object state) : base(callback, state) { }
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

		Stream baseStream;
		Byte[] prebuffer = null;
		int prebufferoffset = 0;
		int prebuffercount = 0;
		int defaultbuffersize;

		public Stream BaseStream { get { return baseStream; } }

		public PrebufferingStream(Stream stream) : this(stream, 1024) { }
		public PrebufferingStream(Stream stream, int bufferSize) {
			if (stream == null) throw new ArgumentNullException("stream");
			baseStream = stream;
			defaultbuffersize = bufferSize;
		}

		public IAsyncResult BeginPrebuffering(AsyncCallback callback, Object state) {
			return BeginPrebuffering(1, callback, state);
		}
		public IAsyncResult BeginPrebuffering(int count, AsyncCallback callback, Object state) {
			AsyncResult ar = new AsyncResult(callback, state);
			if (prebuffercount >= count) {
				ar.SetCompleted(true, prebuffercount, null);
			} else {
				PrepareBuffer(count);
				ar.Left = count - prebuffercount;
				int off = prebufferoffset + prebuffercount;
				baseStream.BeginRead(prebuffer, off, prebuffer.Length - off, asyncPrebufferReadCallback, ar);
			}
			return ar;
		}
		private void asyncPrebufferReadCallback(IAsyncResult ar) {
			AsyncResult myar = (AsyncResult)ar.AsyncState;
			try {
				int len = baseStream.EndRead(ar);
				if (len <= 0) {
					myar.SetCompleted(false, prebuffercount, null);
				} else {
					myar.Left -= len;
					prebuffercount += len;
					if (myar.Left > 0) {
						int off = prebufferoffset + prebuffercount;
						baseStream.BeginRead(prebuffer, off, prebuffer.Length - off, asyncPrebufferReadCallback, myar);
					} else {
						myar.SetCompleted(false, prebuffercount, null);
					}
				}
			} catch (Exception ex) {
				myar.SetCompleted(false, prebuffercount, ex);
			}
		}
		public int EndPrebuffering(IAsyncResult ar) {
			AsyncResult myar = (AsyncResult)ar;
			return myar.WaitForCompletion();
		}
		public int Prebuffer() {
			return Prebuffer(1);
		}
		public int Prebuffer(int count) {
			count -= prebuffercount;
			if (count <= 0) return prebuffercount;
			PrepareBuffer(prebuffercount + count);
			while (count > 0) {
				int off = prebufferoffset + prebuffercount;
				int len = baseStream.Read(prebuffer, off, prebuffer.Length - off);
				if (len <= 0) return prebuffercount;
				count -= len;
				prebuffercount += len;
			}
			return prebuffercount;
		}
		private void PrepareBuffer(int count) {
			if (prebuffercount == 0) prebufferoffset = 0;
			if (prebuffer == null || (prebuffercount == 0 && prebuffer.Length > defaultbuffersize)) {
				if (count < defaultbuffersize) count = defaultbuffersize;
				prebuffer = new Byte[count];
				prebufferoffset = 0;
			} else if (prebufferoffset + count > prebuffer.Length) {
				if (count > prebuffer.Length) {
					Byte[] newbuffer = new Byte[prebuffercount + count];
					Buffer.BlockCopy(prebuffer, prebufferoffset, newbuffer, 0, prebuffercount);
					prebuffer = newbuffer;
				} else {
					Buffer.BlockCopy(prebuffer, prebufferoffset, prebuffer, 0, prebuffercount);
				}
				prebufferoffset = 0;
			}
		}
		public Byte Peek() {
			return Peek(0);
		}
		public Byte Peek(int offset) {
			if (Prebuffer(offset + 1) < offset + 1) throw new EndOfStreamException();
			return prebuffer[prebufferoffset + offset];
		}
		public void Peek(Byte[] buffer, int offset, int count) {
			Peek(buffer, offset, 0, count);
		}
		public void Peek(Byte[] buffer, int bufferoffset, int peekoffset, int count) {
			if (Prebuffer(peekoffset + count) < peekoffset + count) throw new EndOfStreamException();
			Buffer.BlockCopy(prebuffer, prebufferoffset + peekoffset, buffer, bufferoffset, count);
		}
		public int TryPeek() {
			return TryPeek(0);
		}
		public int TryPeek(int offset) {
			if (prebuffercount <= offset) return -1;
			return prebuffer[prebufferoffset + offset];
		}
		public int TryPeek(Byte[] buffer, int offset, int count) {
			return TryPeek(buffer, offset, 0, count);
		}
		public int TryPeek(Byte[] buffer, int bufferoffset, int peekoffset, int count) {
			if (prebuffercount < peekoffset + count) count = prebuffercount - peekoffset;
			if (count < 0) count = 0;
			if (count > 0) Buffer.BlockCopy(prebuffer, prebufferoffset + peekoffset, buffer, bufferoffset, count);
			return count;
		}

		public override int Read(byte[] buffer, int offset, int count) {
			if (prebuffercount > 0 || (count < 16 && defaultbuffersize > 0)) {
				if (prebuffercount == 0) if (Prebuffer() < 1) return 0;
				if (count > prebuffercount) count = prebuffercount;
				Buffer.BlockCopy(prebuffer, prebufferoffset, buffer, offset, count);
				prebufferoffset += count;
				prebuffercount -= count;
				return count;
			} else {
				return baseStream.Read(buffer, offset, count);
			}
		}

		public void ReadAll(Byte[] buffer, int offset, int count) {
			while (count > 0) {
				int read = Read(buffer, offset, count);
				if (read <= 0) throw new EndOfStreamException();
				offset += read;
				count -= read;
			}
		}

		public Byte[] ReadAll(int count) {
			Byte[] buffer = new Byte[count];
			ReadAll(buffer, 0, count);
			return buffer;
		}

		public override int ReadByte() {
			if (Prebuffer(1) < 1) return -1;
			int v = prebuffer[prebufferoffset];
			prebufferoffset++;
			prebuffercount--;
			return v;
		}

		public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state) {
			if (prebuffercount > 0) {
				if (count > prebuffercount) count = prebuffercount;
				Buffer.BlockCopy(prebuffer, prebufferoffset, buffer, offset, count);
				prebufferoffset += count;
				prebuffercount -= count;
				AsyncResult ar = new AsyncResult(callback, state);
				ar.SetCompleted(true, count, null);
				return ar;
			} else {
				return baseStream.BeginRead(buffer, offset, count, callback, state);
			}
		}

		public override int EndRead(IAsyncResult asyncResult) {
			AsyncResult myar = asyncResult as AsyncResult;
			if (myar != null) {
				return myar.WaitForCompletion();
			} else {
				return baseStream.EndRead(asyncResult);
			}
		}

		public IAsyncResult BeginReadAll(byte[] buffer, int offset, int count, AsyncCallback callback, object state) {
			AsyncResult ar = new AsyncResult(callback, state);
			ar.Buffer = buffer;
			ar.Offset = 0;
			ar.Left = count;
			ar.Count = 0;
			if (prebuffercount > 0) {
				int read = Math.Min(ar.Left, prebuffercount);
				Buffer.BlockCopy(prebuffer, prebufferoffset, ar.Buffer, ar.Offset, read);
				prebufferoffset += read;
				prebuffercount -= read;
				ar.Offset += read;
				ar.Left -= read;
				ar.Count += read;
			}
			if (ar.Left > 0) {
				baseStream.BeginRead(ar.Buffer, ar.Offset, ar.Left, asyncReadAllReadCallback, ar);
			} else {
				ar.SetCompleted(true, count, null);
			}
			return ar;
		}

		private void asyncReadAllReadCallback(IAsyncResult ar) {
			AsyncResult myar = (AsyncResult)ar.AsyncState;
			try {
				int len = baseStream.EndRead(ar);
				if (len <= 0) throw new EndOfStreamException();
				myar.Offset += len;
				myar.Left -= len;
				myar.Count += len;
				if (myar.Left > 0) {
					int off = prebufferoffset + prebuffercount;
					baseStream.BeginRead(myar.Buffer, myar.Offset, myar.Left, asyncReadAllReadCallback, ar);
				} else {
					myar.SetCompleted(false, myar.Count, null);
				}
			} catch (Exception ex) {
				myar.SetCompleted(false, ex);
			}
		}

		public int EndReadAll(IAsyncResult asyncResult) {
			AsyncResult myar = asyncResult as AsyncResult;
			return myar.WaitForCompletion();
		}

		public override void Close() {
			base.Close();
			baseStream.Close();
		}

		public override void Write(byte[] buffer, int offset, int count) {
			baseStream.Write(buffer, offset, count);
		}

		public override void WriteByte(byte value) {
			baseStream.WriteByte(value);
		}

		public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state) {
			return baseStream.BeginWrite(buffer, offset, count, callback, state);
		}

		public override void EndWrite(IAsyncResult asyncResult) {
			baseStream.EndWrite(asyncResult);
		}

		public override int ReadTimeout {
			get { return baseStream.ReadTimeout; }
			set { baseStream.ReadTimeout = value; }
		}

		public override int WriteTimeout {
			get { return baseStream.WriteTimeout; }
			set { baseStream.WriteTimeout = value; }
		}

		public override long Length { get { return prebuffercount + baseStream.Length; } }
		public override long Position {
			get { return baseStream.Position - prebuffercount; }
			set { throw new NotImplementedException(); }
		}

		public override void SetLength(long value) { throw new NotImplementedException(); }
		public override long Seek(long offset, SeekOrigin origin) {
			prebuffercount = 0;
			return baseStream.Seek(offset, origin);
		}
		public void Skip(int count) {
			if (count < 0) throw new ArgumentOutOfRangeException("count");
			while (count > 0) {
				int skip = Math.Min(count, prebuffercount);
				prebufferoffset += skip;
				prebuffercount -= skip;
				count -= skip;
				if (count > 0) Prebuffer(Math.Min(count, defaultbuffersize));
			}
		}

		public override bool CanRead { get { return prebuffercount > 0 || baseStream.CanRead; } }
		public override bool CanSeek { get { return baseStream.CanSeek; } }
		public override bool CanTimeout { get { return baseStream.CanTimeout; } }
		public override bool CanWrite { get { return baseStream.CanWrite; } }

		public int Buffered { get { return prebuffercount; } }

		public override void Flush() {
			baseStream.Flush();
		}
	}
}
