using System;
using System.IO;

namespace UCIS.USBLib.Communication {
	public class UsbPipeStream : Stream {
		public IUsbInterface Device { get; private set; }
		public Byte Endpoint { get; private set; }
		public Boolean InterruptEndpoint { get; private set; }

		public UsbPipeStream(IUsbInterface device, Byte endpoint, Boolean interrupt) {
			this.Device = device;
			this.Endpoint = endpoint;
			this.InterruptEndpoint = interrupt;
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

		public override int Read(byte[] buffer, int offset, int count) {
			if (InterruptEndpoint) {
				return Device.InterruptRead(Endpoint, buffer, offset, count);
			} else {
				return Device.BulkRead(Endpoint, buffer, offset, count);
			}
		}

		public override long Seek(long offset, SeekOrigin origin) {
			throw new NotImplementedException();
		}

		public override void SetLength(long value) {
			throw new NotImplementedException();
		}

		public override void Write(byte[] buffer, int offset, int count) {
			int written;
			if (InterruptEndpoint) {
				written = Device.InterruptWrite(Endpoint, buffer, offset, count);
			} else {
				written = Device.BulkWrite(Endpoint, buffer, offset, count);
			}
			if (written != count) throw new EndOfStreamException("Could not write all data");
		}
	}
}
