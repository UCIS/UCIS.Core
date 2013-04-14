using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UCIS.HWLib.Windows.Devices;
using UCIS.USBLib.Internal.Windows;

namespace UCIS.HWLib.Windows.USB {
	public class UsbBus {
		private List<UsbController> devices = null;
		public IList<UsbController> Controllers {
			get {
				if (devices == null) Refresh();
				return devices.AsReadOnly();
			}
		}
		public void Refresh() {
			devices = new List<UsbController>();
			Guid m_Guid = new Guid(UsbApi.GUID_DEVINTERFACE_HUBCONTROLLER);
			foreach (DeviceNode dev in DeviceNode.GetDevices(m_Guid)) {
				String[] interfaces = dev.GetInterfaces(m_Guid);
				if (interfaces == null || interfaces.Length == 0) continue;
				devices.Add(new UsbController(this, dev, interfaces[0]));
			}
		}
	}
}