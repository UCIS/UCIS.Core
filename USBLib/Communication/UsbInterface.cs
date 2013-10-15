using System;
using System.Text;
using UCIS.USBLib.Descriptor;

namespace UCIS.USBLib.Communication {
	public abstract class UsbInterface : IUsbInterface {
		public virtual byte Configuration {
			get {
				byte[] buf = new byte[1];
				int tl = ControlRead(
					UsbControlRequestType.EndpointIn | UsbControlRequestType.TypeStandard | UsbControlRequestType.RecipDevice,
					(byte)UsbStandardRequest.GetConfiguration, 0, 0,
					buf, 0, buf.Length);
				if (tl != buf.Length) throw new Exception("Read failed");
				return buf[0];
			}
			set {
				throw new NotImplementedException();
			}
		}
		public virtual int GetDescriptor(byte descriptorType, byte index, short langId, byte[] buffer, int offset, int length) {
			return ControlRead(
				UsbControlRequestType.EndpointIn | UsbControlRequestType.RecipDevice | UsbControlRequestType.TypeStandard,
				(Byte)UsbStandardRequest.GetDescriptor,
				(short)((descriptorType << 8) | index), langId, buffer, offset, length);
		}
		public virtual int ControlWrite(UsbControlRequestType requestType, byte request, short value, short index) {
			return ControlWrite(requestType, request, value, index, null, 0, 0);
		}

		public abstract void Close();

		public abstract int BulkWrite(Byte endpoint, Byte[] buffer, int offset, int length);
		public abstract int BulkRead(Byte endpoint, Byte[] buffer, int offset, int length);
		public virtual void BulkReset(Byte endpoint) { throw new NotImplementedException(); }
		public abstract int InterruptWrite(Byte endpoint, Byte[] buffer, int offset, int length);
		public abstract int InterruptRead(Byte endpoint, Byte[] buffer, int offset, int length);
		public virtual void InterruptReset(Byte endpoint) { throw new NotImplementedException(); }
		public abstract int ControlWrite(UsbControlRequestType requestType, byte request, short value, short index, byte[] buffer, int offset, int length);
		public abstract int ControlRead(UsbControlRequestType requestType, byte request, short value, short index, byte[] buffer, int offset, int length);

		public virtual UsbPipeStream GetBulkStream(Byte endpoint) {
			return new UsbPipeStream(this, endpoint, false);
		}
		public virtual UsbPipeStream GetInterruptStream(Byte endpoint) {
			return new UsbPipeStream(this, endpoint, true);
		}

		public void Dispose() {
			Close();
			GC.SuppressFinalize(this);
		}
		~UsbInterface() {
			Close();
		}
	}
}
