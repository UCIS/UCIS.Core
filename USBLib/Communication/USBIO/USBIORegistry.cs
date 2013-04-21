using System;
using System.Collections.Generic;
using UCIS.HWLib.Windows.Devices;

namespace UCIS.USBLib.Communication.USBIO {
	public class USBIORegistry : WindowsUsbDeviceRegistry, IUsbDeviceRegistry {
		public static readonly Guid USBIO_IID = new Guid("{325ddf96-938c-11d3-9e34-0080c82727f4}");
		private USBIORegistry(DeviceNode device, String interfacepath) : base(device, interfacepath) { }
		public static List<USBIORegistry> GetDevicesByInterfaceClass(Guid classGuid) {
			List<USBIORegistry> deviceList = new List<USBIORegistry>();
			IList<DeviceNode> usbdevices = DeviceNode.GetDevices(classGuid);
			foreach (DeviceNode device in usbdevices) {
				USBIORegistry regInfo = GetDeviceForDeviceNode(device, classGuid);
				if (regInfo != null) deviceList.Add(regInfo);
			}
			return deviceList;
		}
		public static List<USBIORegistry> DeviceList {
			get { return GetDevicesByInterfaceClass(USBIO_IID); }
		}
		public static USBIORegistry GetDeviceForDeviceNode(DeviceNode device, Guid classGuid) {
			String[] iLibUsb = device.GetInterfaces(classGuid);
			if (iLibUsb == null || iLibUsb.Length == 0) return null;
			return new USBIORegistry(device, iLibUsb[0]);
		}
		public static USBIORegistry GetDeviceForDeviceNode(DeviceNode device) {
			return GetDeviceForDeviceNode(device, USBIO_IID);
		}

		public IUsbDevice Open() {
			return new USBIODevice(DevicePath, this);
		}
		IUsbDevice IUsbDeviceRegistry.Open() {
			return Open();
		}
	}
}