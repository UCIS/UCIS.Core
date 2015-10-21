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
			if (ret != 0) throw new LibUsb1Exception("libusb_open", ret);
		}

		protected override void Dispose(Boolean disposing) {
			if (disposing && Handle != null) Handle.Close();
		}

		public override void PipeReset(byte endpoint) {
			int ret = libusb1.libusb_clear_halt(Handle, endpoint);
			if (ret < 0) throw new LibUsb1Exception("libusb_clear_halt", ret);
		}
		public override int PipeTransfer(byte endpoint, byte[] buffer, int offset, int length) {
			if (offset < 0 || length < 0 || offset + length > buffer.Length) throw new ArgumentOutOfRangeException("length", "The specified offset and length exceed the buffer length");
			if (length == 0) return 0;
			fixed (Byte* b = buffer) {
				int ret = libusb1.libusb_bulk_transfer(Handle, endpoint, b + offset, length, out length, 0);
				//libusb1.libusb_interrupt_transfer(Handle, endpoint, b + offset, length, out length, 0);
				if (ret < 0) throw new LibUsb1Exception("libusb_bulk_transfer", ret);
			}
			return length;
		}

		public override int ControlTransfer(UsbControlRequestType requestType, byte request, short value, short index, byte[] buffer, int offset, int length) {
			if (buffer == null) buffer = new Byte[0];
			if (offset < 0 || length < 0 || length > ushort.MaxValue || offset + length > buffer.Length) throw new ArgumentOutOfRangeException("length");
			fixed (Byte* b = buffer) {
				int ret = libusb1.libusb_control_transfer(Handle, (Byte)requestType, request, (ushort)value, (ushort)index, b + offset, (ushort)length, 0);
				if (ret < 0) throw new LibUsb1Exception("libusb_control_transfer", ret);
				return ret;
			}
		}

		public override byte Configuration {
			get { return base.Configuration; }
			set {
				if (value == base.Configuration) return;
				for (int i = 0; i < 16; i++) libusb1.libusb_detach_kernel_driver(Handle, i);
				int ret = libusb1.libusb_set_configuration(Handle, value);
				if (ret != 0) throw new LibUsb1Exception("libusb_set_configuration", ret);
			}
		}
		public void ClaimInterface(int interfaceID) {
			int ret = libusb1.libusb_detach_kernel_driver(Handle, interfaceID);
			ret = libusb1.libusb_claim_interface(Handle, interfaceID);
			if (ret != 0) throw new LibUsb1Exception("libusb_claim_interface", ret);
		}
		public void ReleaseInterface(int interfaceID) {
			int ret = libusb1.libusb_release_interface(Handle, interfaceID);
			if (ret != 0) throw new LibUsb1Exception("libusb_release_interface", ret);
			ret = libusb1.libusb_attach_kernel_driver(Handle, interfaceID);
		}
		public void ResetDevice() {
			int ret = libusb1.libusb_reset_device(Handle);
			if (ret != 0) throw new LibUsb1Exception("libusb_reset_device", ret);
		}
	}
}
