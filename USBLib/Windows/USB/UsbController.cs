using System;
using System.Collections.Generic;
using Microsoft.Win32.SafeHandles;
using UCIS.HWLib.Windows.Devices;
using UCIS.USBLib.Internal.Windows;

namespace UCIS.HWLib.Windows.USB {
	public class UsbController {
		static readonly Guid IID_DEVINTERFACE_USB_HOST_CONTROLLER = new Guid(UsbApi.GUID_DEVINTERFACE_USB_HOST_CONTROLLER);
		public String DevicePath { get; private set; }
		public DeviceNode DeviceNode { get; private set; }
		public String DeviceDescription { get { return DeviceNode.DeviceDescription; } }
		public String DriverKey { get { return DeviceNode.DriverKey; } }
		public UsbHub RootHub {
			get {
				String rootHubName;
				using (SafeFileHandle handle = UsbHub.OpenHandle(DevicePath)) rootHubName = UsbHub.GetRootHubName(handle);
				return new UsbHub(null, new USB_NODE_CONNECTION_INFORMATION_EX(), @"\\?\" + rootHubName, 0, true);
			}
		}
		private UsbController(DeviceNode di, String devicePath) {
			this.DeviceNode = di;
			this.DevicePath = devicePath;
		}

		public static UsbController GetControllerForDeviceNode(DeviceNode node) {
			String[] interfaces = node.GetInterfaces(IID_DEVINTERFACE_USB_HOST_CONTROLLER);
			if (interfaces == null || interfaces.Length == 0) return null;
			return new UsbController(node, interfaces[0]);
		}

		public static IList<UsbController> GetControllers() {
			IList<UsbController>  devices = new List<UsbController>();
			foreach (DeviceNode dev in DeviceNode.GetDevices(IID_DEVINTERFACE_USB_HOST_CONTROLLER)) {
				UsbController controller = GetControllerForDeviceNode(dev);
				if (controller != null) devices.Add(controller);
			}
			return devices;
		}
	}
}
