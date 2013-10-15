using System;
using System.IO;

namespace UCIS.USBLib.Communication {
	public class UsbPipeStream : Stream {
		public IUsbInterface Device { get; private set; }
		public Byte Endpoint { get; private set; }

		public UsbPipeStream(IUsbInterface device, Byte endpoint) {
			this.Device = device;
			this.Endpoint = endpoint;
		}

		public override bool CanRead {
			get { return (Endpoint & 0x80) != 0; }
		}

		public override bool CanSeek {
			get { return false; }
		}

		public override bool CanWrite {
			get { return (Endpoint & 0x80) == 0; }
		}

		public override void Flush() {
		}

		public override long Length { get { return 0; } }

		public override long Position {
			get { return 0; }
			set { throw new NotImplementedException(); }
		}

		public void Abort() {
			Device.PipeAbort(Endpoint);
		}

		public void ClearHalt() {
			Device.PipeReset(Endpoint);
		}

		public override int Read(byte[] buffer, int offset, int count) {
			if (!CanRead) throw new InvalidOperationException("Can not read from an output endpoint");
			return Device.PipeTransfer(Endpoint, buffer, offset, count);
		}

		public override long Seek(long offset, SeekOrigin origin) {
			throw new NotImplementedException();
		}

		public override void SetLength(long value) {
			throw new NotImplementedException();
		}

		public override void Write(byte[] buffer, int offset, int count) {
			if (!CanWrite) throw new InvalidOperationException("Can not write to an input endpoint");
			int written = Device.PipeTransfer(Endpoint, buffer, offset, count);
			if (written != count) throw new EndOfStreamException("Could not write all data");
		}

		protected override void Dispose(bool disposing) {
			if (disposing) try { Abort(); } catch { }
			base.Dispose(disposing);
		}
	}
}
