using System;
using System.Collections.Generic;
using UCIS.HWLib.Windows.Devices;

namespace UCIS.USBLib.Communication.WinUsb {
	public class WinUsbRegistry : WindowsUsbDeviceRegistry, IUsbDeviceRegistry {
		private WinUsbRegistry(DeviceNode device, String interfacepath) : base(device, interfacepath) { }
		public IList<Guid> DeviceInterfaceGuids { get; private set; }

		public static List<WinUsbRegistry> DeviceList {
			get {
				List<WinUsbRegistry> deviceList = new List<WinUsbRegistry>();
				IList<DeviceNode> usbdevices = DeviceNode.GetDevices("USB");
				foreach (DeviceNode device in usbdevices) {
					WinUsbRegistry regInfo = GetDeviceForDeviceNode(device);
					if (regInfo != null) deviceList.Add(regInfo);
				}
				return deviceList;
			}
		}
		public static WinUsbRegistry GetDeviceForDeviceNode(DeviceNode device) {
			if (device.Service != "WinUSB") return null;
			String[] devInterfaceGuids = device.GetCustomPropertyStringArray("DeviceInterfaceGuids");
			if (devInterfaceGuids == null || devInterfaceGuids.Length < 1) return null;
			Guid deviceInterfaceGuid = new Guid(devInterfaceGuids[0]);
			String[] interfaces = device.GetInterfaces(deviceInterfaceGuid);
			if (interfaces == null || interfaces.Length < 1) return null;
			WinUsbRegistry regInfo = new WinUsbRegistry(device, interfaces[0]);
			regInfo.DeviceInterfaceGuids = Array.ConvertAll(devInterfaceGuids, delegate(String g) { return new Guid(g); });
			return regInfo;
		}

		public WinUsbDevice Open() {
			return new WinUsbDevice(DevicePath, this);
		}
		IUsbDevice IUsbDeviceRegistry.Open() {
			return Open();
		}
	}
}