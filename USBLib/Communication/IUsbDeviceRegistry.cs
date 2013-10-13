using System;
using System.Collections.Generic;
using UCIS.USBLib.Communication.LibUsb;
using UCIS.USBLib.Communication.LibUsb1;
using UCIS.USBLib.Communication.WinUsb;

namespace UCIS.USBLib.Communication {
	public interface IUsbDeviceRegistry {
		IDictionary<String, Object> DeviceProperties { get; }
		UInt16 Vid { get; }
		UInt16 Pid { get; }
		Byte InterfaceID { get; }

		String Name { get; } //Device product name (or null if not available)
		String Manufacturer { get; } //Device manufacturer name (or null if not available)
		String FullName { get; } //Device manufacturer name and product name
		String SymbolicName { get; } //Arbitrary string that uniquely identifies the device

		IUsbDevice Open();
	}
	public static class UsbDevice {
		public static IList<IUsbDeviceRegistry> AllDevices {
			get {
				List<IUsbDeviceRegistry> list = new List<IUsbDeviceRegistry>();
				if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
					foreach (IUsbDeviceRegistry reg in WinUsbRegistry.DeviceList) list.Add(reg);
					foreach (IUsbDeviceRegistry reg in LibUsb0Registry.DeviceList) list.Add(reg);
				} else {
					foreach (IUsbDeviceRegistry reg in LibUsb1Registry.DeviceList) list.Add(reg);
				}
				return list;
			}
		}
	}
}
