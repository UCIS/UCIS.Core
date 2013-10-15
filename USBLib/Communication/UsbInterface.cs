using System;
using System.Text;
using UCIS.USBLib.Descriptor;

namespace UCIS.USBLib.Communication {
	public abstract class UsbInterface : IUsbInterface {
		public virtual byte Configuration {
			get {
				byte[] buf = new byte[1];
				int tl = ControlTransfer(
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
			return ControlTransfer(
				UsbControlRequestType.EndpointIn | UsbControlRequestType.RecipDevice | UsbControlRequestType.TypeStandard,
				(Byte)UsbStandardRequest.GetDescriptor,
				(short)((descriptorType << 8) | index), langId, buffer, offset, length);
		}
		public virtual int ControlTransfer(UsbControlRequestType requestType, byte request, short value, short index) {
			return ControlTransfer(requestType, request, value, index, null, 0, 0);
		}

		public abstract void Close();

		public abstract int PipeTransfer(Byte endpoint, Byte[] buffer, int offset, int length);
		public virtual void PipeReset(Byte endpoint) { throw new NotImplementedException(); }
		public virtual void PipeAbort(Byte endpoint) { throw new NotImplementedException(); }
		public abstract int ControlTransfer(UsbControlRequestType requestType, byte request, short value, short index, byte[] buffer, int offset, int length);

		public virtual UsbPipeStream GetPipeStream(Byte endpoint) {
			return new UsbPipeStream(this, endpoint);
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
