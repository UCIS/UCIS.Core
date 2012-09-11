using System;
using System.IO;

namespace UCIS.Util {
	public class AsyncStreamWrapper : Stream {
		ReadDelegate readDelegate;
		WriteDelegate writeDelegate;

		public Stream BaseStream { get; private set; }

		public AsyncStreamWrapper(Stream stream) {
			BaseStream = stream;
			readDelegate = (ReadDelegate)Read;
			writeDelegate = (WriteDelegate)Write;
		}

		public override int Read(byte[] buffer, int offset, int count) { return BaseStream.Read(buffer, offset, count); }
		public override void Write(byte[] buffer, int offset, int count) { BaseStream.Write(buffer, offset, count); }
		public override void Close() { BaseStream.Close(); }

		public override bool CanRead { get { return BaseStream.CanRead; } }
		public override bool CanSeek { get { return BaseStream.CanSeek; } }
		public override bool CanTimeout { get { return BaseStream.CanTimeout ; } }
		public override bool CanWrite { get { return BaseStream.CanWrite ; } }

		public override int ReadTimeout {
			get { return BaseStream.ReadTimeout; }
			set { BaseStream.ReadTimeout = value; }
		}
		public override int WriteTimeout {
			get { return BaseStream.WriteTimeout; }
			set { BaseStream.WriteTimeout = value; }
		}

		public override void Flush() { BaseStream.Flush(); }
		public override long Length { get { return BaseStream.Length; } }
		public override long Position {
			get { return BaseStream.Position; }
			set { BaseStream.Position = value; }
		}
		public override long Seek(long offset, SeekOrigin origin) { return BaseStream.Seek(offset, origin); }
		public override void SetLength(long value) { BaseStream.SetLength(value); }

		public override int ReadByte() { return base.ReadByte(); }
		public override void WriteByte(byte value) { BaseStream.WriteByte(value); }

		private delegate int ReadDelegate(byte[] buffer, int offset, int count);
		private delegate void WriteDelegate(byte[] buffer, int offset, int count);

		public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state) {
			return readDelegate.BeginInvoke(buffer, offset, count, callback, state);
		}
		public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state) {
			return writeDelegate.BeginInvoke(buffer, offset, count, callback, state);
		}
		public override int EndRead(IAsyncResult asyncResult) {
			return readDelegate.EndInvoke(asyncResult);
		}
		public override void EndWrite(IAsyncResult asyncResult) {
			writeDelegate.EndInvoke(asyncResult);
		}
	}
	public abstract class AsyncStream : Stream {
		ReadDelegate readDelegate;
		WriteDelegate writeDelegate;

		public AsyncStream() {
			readDelegate = (ReadDelegate)Read;
			writeDelegate = (WriteDelegate)Write;
		}

		private delegate int ReadDelegate(byte[] buffer, int offset, int count);
		private delegate void WriteDelegate(byte[] buffer, int offset, int count);

		public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state) {
			return readDelegate.BeginInvoke(buffer, offset, count, callback, state);
		}
		public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state) {
			return writeDelegate.BeginInvoke(buffer, offset, count, callback, state);
		}
		public override int EndRead(IAsyncResult asyncResult) {
			return readDelegate.EndInvoke(asyncResult);
		}
		public override void EndWrite(IAsyncResult asyncResult) {
			writeDelegate.EndInvoke(asyncResult);
		}
	}
}
