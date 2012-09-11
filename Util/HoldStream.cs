using System;
using System.IO;

namespace UCIS.Util {
	public class HoldStream : Stream {
		private Stream baseStream;
		private MemoryStream buffer;

		public HoldStream(Stream baseStream) {
			this.baseStream = baseStream;
			this.buffer = new MemoryStream(4096);
		}

		public override bool CanRead {
			get { return baseStream.CanRead; }
		}
		public override bool CanSeek {
			get { return buffer.CanSeek; }
		}
		public override bool CanTimeout {
			get { return baseStream.CanTimeout; }
		}
		public override bool CanWrite {
			get { return buffer.CanWrite; }
		}
		public override void Close() {
			buffer.Close();
			baseStream.Close();
		}
		public override void Flush() {
			buffer.WriteTo(baseStream);
			buffer.SetLength(0);
			buffer.Seek(0, SeekOrigin.Begin);
		}
		public override void Write(byte[] buffer, int offset, int count) {
			this.buffer.Write(buffer, offset, count);
		}
		public override int Read(byte[] buffer, int offset, int count) {
			return baseStream.Read(buffer, offset, count);
		}
		public override void SetLength(long value) {
			buffer.SetLength(value);
		}
		public override long Seek(long offset, SeekOrigin origin) {
			return buffer.Seek(offset, origin);
		}
		public override long Position {
			get { return buffer.Position; }
			set { buffer.Position = value; }
		}
		public override long Length {
			get { return buffer.Length; }
		}
		public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state) {
			return baseStream.BeginRead(buffer, offset, count, callback, state);
		}
		public override int EndRead(IAsyncResult asyncResult) {
			return baseStream.EndRead(asyncResult);
		}
		public override void WriteByte(byte value) {
			buffer.WriteByte(value);
		}
	}
}
