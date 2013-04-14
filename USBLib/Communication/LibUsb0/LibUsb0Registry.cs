using System;
using System.Collections.Generic;
using UCIS.HWLib.Windows.Devices;

namespace UCIS.USBLib.Communication.LibUsb {
	public class LibUsb0Registry : WindowsUsbDeviceRegistry, IUsbDeviceRegistry {
		private LibUsb0Registry(DeviceNode device, String interfacepath) : base(device, interfacepath) { }
		public static List<LibUsb0Registry> GetDevicesByInterfaceClass(Guid classGuid) {
			List<LibUsb0Registry> deviceList = new List<LibUsb0Registry>();
			IList<DeviceNode> usbdevices = DeviceNode.GetDevices(classGuid);
			foreach (DeviceNode device in usbdevices) {
				LibUsb0Registry regInfo = GetDeviceForDeviceNode(device, classGuid);
				if (regInfo != null) deviceList.Add(regInfo);
			}
			return deviceList;
		}
		public static List<LibUsb0Registry> DeviceList {
			get {
				List<LibUsb0Registry> deviceList = new List<LibUsb0Registry>();
				IList<DeviceNode> usbdevices = DeviceNode.GetDevices("USB");
				foreach (DeviceNode device in usbdevices) {
					LibUsb0Registry regInfo = GetDeviceForDeviceNode(device);
					if (regInfo == null) continue;
					deviceList.Add(regInfo);
				}
				return deviceList;
			}
		}
		public static List<LibUsb0Registry> FilterDeviceList {
			get {
				return GetDevicesByInterfaceClass(new Guid("{F9F3FF14-AE21-48A0-8A25-8011A7A931D9}"));
			}
		}
		public static LibUsb0Registry GetDeviceForDeviceNode(DeviceNode device, Guid classGuid) {
			String[] iLibUsb = device.GetInterfaces(classGuid);
			if (iLibUsb == null || iLibUsb.Length == 0) return null;
			return new LibUsb0Registry(device, iLibUsb[0]);
		}
		public static LibUsb0Registry GetDeviceForDeviceNode(DeviceNode device) {
			String deviceInterface = null;
			if (deviceInterface == null) {
				String[] interfaces = device.GetInterfaces("{20343A29-6DA1-4DB8-8A3C-16E774057BF5}");
				if (interfaces != null && interfaces.Length > 0) {
					deviceInterface = interfaces[0];
				}
			}
			if (deviceInterface == null && device.Service == "libusb0") {
				String[] devInterfaceGuids = device.GetCustomPropertyStringArray("DeviceInterfaceGuids");
				if (devInterfaceGuids != null && devInterfaceGuids.Length > 0) {
					Guid deviceInterfaceGuid = new Guid(devInterfaceGuids[0]);
					String[] interfaces = device.GetInterfaces(deviceInterfaceGuid);
					if (interfaces != null && interfaces.Length > 0) {
						deviceInterface = interfaces[0];
					}
				}
			}
			/*if (deviceInterface == null) {
				String[] interfaces = device.GetInterfaces("{F9F3FF14-AE21-48A0-8A25-8011A7A931D9}");
				if (interfaces != null && interfaces.Length > 0) {
					deviceInterface = interfaces[0];
				}
			}*/
			if (deviceInterface == null) return null;
			return new LibUsb0Registry(device, deviceInterface);
		}

		public LibUsb0Device Open() {
			return new LibUsb0Device(DevicePath, this);
		}
		IUsbDevice IUsbDeviceRegistry.Open() {
			return Open();
		}
	}
}