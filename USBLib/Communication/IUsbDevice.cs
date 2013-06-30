using System;

namespace UCIS.USBLib.Communication {
	public interface IUsbDevice : IUsbInterface, IDisposable {
		new Byte Configuration { get; set; }
		void ClaimInterface(int interfaceID);
		void ReleaseInterface(int interfaceID);
		void ResetDevice();

		IUsbDeviceRegistry Registry { get; }

		//void Close();
	}
	public interface IUsbInterface : IDisposable {
		Byte Configuration { get; }
		void Close();

		//int ControlTransfer(byte requestType, byte request, short value, short index, Byte[] buffer, int offset, int length);
		int GetDescriptor(byte descriptorType, byte index, short langId, Byte[] buffer, int offset, int length);
		String GetString(short langId, byte stringIndex);

		int BulkWrite(Byte endpoint, Byte[] buffer, int offset, int length);
		int BulkRead(Byte endpoint, Byte[] buffer, int offset, int length);
		void BulkReset(Byte endpoint);
		int InterruptWrite(Byte endpoint, Byte[] buffer, int offset, int length);
		int InterruptRead(Byte endpoint, Byte[] buffer, int offset, int length);
		void InterruptReset(Byte endpoint);
		int ControlWrite(UsbControlRequestType requestType, byte request, short value, short index, Byte[] buffer, int offset, int length);
		int ControlRead(UsbControlRequestType requestType, byte request, short value, short index, Byte[] buffer, int offset, int length);

		UsbPipeStream GetBulkStream(Byte endpoint);
		UsbPipeStream GetInterruptStream(Byte endpoint);
	}
}
