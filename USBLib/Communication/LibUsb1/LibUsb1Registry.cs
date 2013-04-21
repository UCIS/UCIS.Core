using System;
using System.Collections.Generic;
using System.Text;

namespace UCIS.USBLib.Communication.LibUsb1 {
	public class LibUsb1Registry : IUsbDeviceRegistry {
		static libusb_context Context;

		libusb_device Device;
		libusb1.libusb_device_descriptor? DeviceDescriptor = null;

		public unsafe static List<LibUsb1Registry> DeviceList {
			get {
				List<LibUsb1Registry> deviceList = new List<LibUsb1Registry>();
				if (Context == null) {
					int ret = libusb1.libusb_init(out Context);
					if (ret != 0) throw new Exception("libusb_init returned " + ret.ToString());
				}
				IntPtr* list;
				IntPtr count = libusb1.libusb_get_device_list(Context, out list);
				for (IntPtr* item = list; *item != IntPtr.Zero; item++) {
					deviceList.Add(new LibUsb1Registry(new libusb_device(*item, true)));
				}
				libusb1.libusb_free_device_list(list, 0);
				return deviceList;
			}
		}

		private LibUsb1Registry(libusb_device device) {
			this.Device = device;
		}
		private libusb1.libusb_device_descriptor GetDeviceDescriptor() {
			if (DeviceDescriptor == null) {
				libusb1.libusb_device_descriptor descriptor;
				int ret = libusb1.libusb_get_device_descriptor(Device, out descriptor);
				if (ret < 0) throw new Exception("libusb_get_device_descriptor returned " + ret.ToString());
				DeviceDescriptor = descriptor;
			}
			return DeviceDescriptor.Value;
		}

		public int Vid { get { return GetDeviceDescriptor().idVendor; } }
		public int Pid { get { return GetDeviceDescriptor().idProduct; } }
		public byte InterfaceID { get { return 0; } }

		public string Name {
			get {
				byte iProduct = GetDeviceDescriptor().iProduct;
				libusb_device_handle handle;
				int ret = libusb1.libusb_open(Device, out handle);
				if (ret != 0) return null;
				if (ret != 0) throw new Exception("libusb_open returned " + ret.ToString());
				StringBuilder data = new StringBuilder(1024);
				ret = libusb1.libusb_get_string_descriptor_ascii(handle, iProduct, data, data.Capacity);
				if (ret < 0) throw new Exception("libusb_get_string_descriptor_ascii returned " + ret.ToString());
				handle.Close();
				return data.ToString();
			}
		}
		public string Manufacturer {
			get {
				byte iProduct = GetDeviceDescriptor().iManufacturer;
				libusb_device_handle handle;
				int ret = libusb1.libusb_open(Device, out handle);
				if (ret != 0) return null;
				if (ret != 0) throw new Exception("libusb_open returned " + ret.ToString());
				StringBuilder data = new StringBuilder(1024);
				ret = libusb1.libusb_get_string_descriptor_ascii(handle, iProduct, data, data.Capacity);
				if (ret < 0) throw new Exception("libusb_get_string_descriptor_ascii returned " + ret.ToString());
				handle.Close();
				return data.ToString();
			}
		}
		public string FullName {
			get {
				libusb1.libusb_device_descriptor descriptor = GetDeviceDescriptor();
				String mfg = null, prod = null;
				libusb_device_handle handle;
				int ret = libusb1.libusb_open(Device, out handle);
				if (ret != 0) return null;
				if (ret != 0) throw new Exception("libusb_open returned " + ret.ToString());
				if (descriptor.iManufacturer != 0) {
					StringBuilder data = new StringBuilder(1024);
					ret = libusb1.libusb_get_string_descriptor_ascii(handle, descriptor.iManufacturer, data, data.Capacity);
					if (ret < 0) throw new Exception("libusb_get_string_descriptor_ascii returned " + ret.ToString());
					mfg = data.ToString();
				}
				if (descriptor.iProduct != 0) {
					StringBuilder data = new StringBuilder(1024);
					ret = libusb1.libusb_get_string_descriptor_ascii(handle, descriptor.iProduct, data, data.Capacity);
					if (ret < 0) throw new Exception("libusb_get_string_descriptor_ascii returned " + ret.ToString());
					prod = data.ToString();
				}
				handle.Close();
				if (mfg == null && prod == null) return null;
				if (mfg == null) return prod;
				if (prod == null) return mfg;
				return mfg + " - " + prod;
			}
		}
		public Byte BusNumber {
			get {
				return libusb1.libusb_get_bus_number(Device);
			}
		}
		public Byte DeviceAddress {
			get {
				return libusb1.libusb_get_device_address(Device);
			}
		}
		public String SymbolicName { get { return String.Format("libusb;bus={0};address={1}", BusNumber, DeviceAddress); } }
		IDictionary<String, Object> IUsbDeviceRegistry.DeviceProperties { get { return null; } }
		public LibUsb1Device Open() {
			return new LibUsb1Device(Device, this);
		}
		IUsbDevice IUsbDeviceRegistry.Open() {
			return Open();
		}

		public override bool Equals(object obj) {
			LibUsb1Registry r = obj as LibUsb1Registry;
			if (r == null) return false;
			return r.Device.DangerousGetHandle() == Device.DangerousGetHandle();
		}

		public override int GetHashCode() {
			return Device.DangerousGetHandle().GetHashCode();
		}
	}
}
