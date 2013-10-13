using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using UCIS.HWLib.Windows.Devices;
using UCIS.USBLib.Internal.Windows;

namespace UCIS.HWLib.Windows.USB {
	public class UsbController {
		public String DevicePath { get; private set; }
		public DeviceNode DeviceNode { get; private set; }
		public String DeviceDescription { get { return DeviceNode.DeviceDescription; } }
		public String DriverKey { get { return DeviceNode.DriverKey; } }
		public UsbHub RootHub {
			get {
				USB_ROOT_HUB_NAME rootHubName = new USB_ROOT_HUB_NAME();
				int nBytesReturned;
				using (SafeFileHandle handle = UsbHub.OpenHandle(DevicePath))
					if (!Kernel32.DeviceIoControl(handle, UsbApi.IOCTL_USB_GET_ROOT_HUB_NAME, IntPtr.Zero, 0, out rootHubName, Marshal.SizeOf(rootHubName), out nBytesReturned, IntPtr.Zero))
						throw new Win32Exception(Marshal.GetLastWin32Error());
				if (rootHubName.ActualLength <= 0) return null;
				return new UsbHub(null, @"\\?\" + rootHubName.RootHubName, 0);
			}
		}
		private UsbController(DeviceNode di, String devicePath) {
			this.DeviceNode = di;
			this.DevicePath = devicePath;
		}

		static readonly Guid IID_DEVINTERFACE_USB_HOST_CONTROLLER = new Guid(UsbApi.GUID_DEVINTERFACE_USB_HOST_CONTROLLER);
		public static UsbController GetControllerForDeviceNode(DeviceNode node) {
			if (node == null) return null;
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
