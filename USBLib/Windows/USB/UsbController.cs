using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using UCIS.HWLib.Windows.Devices;
using UCIS.USBLib.Internal.Windows;

namespace UCIS.HWLib.Windows.USB {
	public class UsbController {
		public String DevicePath { get; private set; }
		public DeviceNode DeviceNode { get; private set; }
		public String DeviceDescription { get; private set; }
		public String DriverKey { get; private set; }
		public UsbHub RootHub { get; private set; }
		internal UsbController(UsbBus parent, DeviceNode di, String devicePath) {
			this.DeviceNode = di;
			this.DevicePath = devicePath;
			this.DeviceDescription = di.GetPropertyString(SPDRP.DeviceDesc);
			this.DriverKey = di.GetPropertyString(SPDRP.Driver);

			USB_ROOT_HUB_NAME rootHubName;
			using (SafeFileHandle handel1 = Kernel32.CreateFile(DevicePath, Kernel32.GENERIC_WRITE, Kernel32.FILE_SHARE_WRITE, IntPtr.Zero, Kernel32.OPEN_EXISTING, 0, IntPtr.Zero)) {
				if (handel1.IsInvalid) throw new Exception("No port found!");
				int nBytesReturned;
				if (!Kernel32.DeviceIoControl(handel1, UsbApi.IOCTL_USB_GET_ROOT_HUB_NAME, IntPtr.Zero, 0, out rootHubName, Marshal.SizeOf(typeof(USB_ROOT_HUB_NAME)), out nBytesReturned, IntPtr.Zero))
					throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
			}
			if (rootHubName.ActualLength <= 0) throw new Exception("rootHubName.ActualLength <= 0");
			RootHub = new UsbHub(this, null, @"\\?\" + rootHubName.RootHubName);
		}
	}
}