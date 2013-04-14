using System;

namespace UCIS.USBLib.Communication.LibUsb1 {
	public unsafe class LibUsb1Device : UsbInterface, IUsbDevice {
		libusb_device Device;
		libusb_device_handle Handle;
		//Boolean KernelDriverWasAttached = false;
		public IUsbDeviceRegistry Registry { get; private set; }
		internal LibUsb1Device(libusb_device device, LibUsb1Registry registry) {
			this.Device = device;
			this.Registry = registry;
			int ret = libusb1.libusb_open(Device, out Handle);
			if (ret != 0) throw new Exception("libusb_open returned " + ret.ToString());
		}

		public override void Close() {
			if (Handle != null) Handle.Close();
		}

		public override int BulkWrite(byte endpoint, byte[] buffer, int offset, int length) {
			return BulkTransfer(endpoint, buffer, offset, length);
		}
		public override int BulkRead(byte endpoint, byte[] buffer, int offset, int length) {
			return BulkTransfer(endpoint, buffer, offset, length);
		}
		private int BulkTransfer(byte endpoint, byte[] buffer, int offset, int length) {
			if (offset < 0 || length < 0 || offset + length > buffer.Length) throw new ArgumentOutOfRangeException("length", "The specified offset and length exceed the buffer length");
			if (length == 0) return 0;
			fixed (Byte* b = buffer) {
				int ret = libusb1.libusb_bulk_transfer(Handle, endpoint, b + offset, length, out length, 0);
				if (ret < 0) throw new Exception("libusb_bulk_transfer returned " + ret.ToString());
			}
			return length;
		}

		public override int InterruptWrite(byte endpoint, byte[] buffer, int offset, int length) {
			return InterruptTransfer(endpoint, buffer, offset, length);
		}
		public override int InterruptRead(byte endpoint, byte[] buffer, int offset, int length) {
			return InterruptTransfer(endpoint, buffer, offset, length);
		}
		private int InterruptTransfer(byte endpoint, byte[] buffer, int offset, int length) {
			if (offset < 0 || length < 0 || offset + length > buffer.Length) throw new ArgumentOutOfRangeException("length", "The specified offset and length exceed the buffer length");
			if (length == 0) return 0;
			fixed (Byte* b = buffer) {
				int ret = libusb1.libusb_interrupt_transfer(Handle, endpoint, b + offset, length, out length, 0);
				if (ret < 0) throw new Exception("libusb_interrupt_transfer returned " + ret.ToString());
			}
			return length;
		}

		public override int ControlWrite(UsbControlRequestType requestType, byte request, short value, short index, byte[] buffer, int offset, int length) {
			return ControlTransfer(requestType, request, value, index, buffer, offset, length);
		}
		public override int ControlRead(UsbControlRequestType requestType, byte request, short value, short index, byte[] buffer, int offset, int length) {
			return ControlTransfer(requestType, request, value, index, buffer, offset, length);
		}
		private int ControlTransfer(UsbControlRequestType requestType, byte request, short value, short index, byte[] buffer, int offset, int length) {
			if (buffer == null) buffer = new Byte[0];
			if (offset < 0 || length < 0 || length > ushort.MaxValue || offset + length > buffer.Length) throw new ArgumentOutOfRangeException("length");
			fixed (Byte* b = buffer) {
				int ret = libusb1.libusb_control_transfer(Handle, (Byte)requestType, request, (ushort)value, (ushort)index, b + offset, (ushort)length, 0);
				if (ret < 0) throw new Exception("libusb_control_transfer returned " + ret.ToString());
				return ret;
			}
		}

		public override byte Configuration {
			get { return base.Configuration; }
			set {
				if (value == base.Configuration) return;
				for (int i = 0; i < 16; i++) libusb1.libusb_detach_kernel_driver(Handle, i);
				int ret = libusb1.libusb_set_configuration(Handle, value);
				if (ret != 0) throw new Exception("libusb_set_configuration returned " + ret.ToString());
			}
		}
		public void ClaimInterface(int interfaceID) {
			int ret = libusb1.libusb_detach_kernel_driver(Handle, interfaceID);
			ret = libusb1.libusb_claim_interface(Handle, interfaceID);
			if (ret != 0) throw new Exception("libusb_claim_interface returned " + ret.ToString());
		}
		public void ReleaseInterface(int interfaceID) {
			int ret = libusb1.libusb_release_interface(Handle, interfaceID);
			if (ret != 0) throw new Exception("libusb_release_interface returned " + ret.ToString());
			ret = libusb1.libusb_attach_kernel_driver(Handle, interfaceID);
		}
		public void ResetDevice() {
			int ret = libusb1.libusb_reset_device(Handle);
			if (ret != 0) throw new Exception("libusb_reset_device returned " + ret.ToString());
		}
	}
}
