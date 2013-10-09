using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using UCIS.HWLib.Windows.Devices;
using UCIS.USBLib.Internal.Windows;

namespace UCIS.HWLib.Windows.USB {
	public class UsbController {
		public String DevicePath { get; private set; }
		public DeviceNode DeviceNode { get; private set; }
		public String DeviceDescription {
			get { return DeviceNode.GetPropertyString(SPDRP.DeviceDesc); }
		}
		public String DriverKey {
			get { return DeviceNode.GetPropertyString(SPDRP.Driver); }
		}
		public UsbHub RootHub {
			get {
				String rootHubName;
				using (SafeFileHandle handle = UsbHub.OpenHandle(DevicePath)) rootHubName = UsbHub.GetRootHubName(handle);
				return new UsbHub(null, new USB_NODE_CONNECTION_INFORMATION_EX(), @"\\?\" + rootHubName, 0, true);
			}
		}
		internal UsbController(UsbBus parent, DeviceNode di, String devicePath) {
			this.DeviceNode = di;
			this.DevicePath = devicePath;
		}
	}
}
