using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using UCIS.USBLib.Internal.Windows;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace UCIS.HWLib.Windows.USB {
	public class UsbHub : UsbDevice {
		public int PortCount { get; private set; }
		public bool IsBusPowered { get; private set; }
		public bool IsRootHub { get; private set; }
		internal USB_NODE_INFORMATION NodeInformation { get; private set; }
		private List<UsbDevice> devices = new List<UsbDevice>();
		public IList<UsbDevice> Devices { get { return devices.AsReadOnly(); } }
		public override string DeviceDescription { get { return IsRootHub ? "RootHub" : "Standard-USB-Hub"; } }
		internal UsbHub(UsbController parent, USB_DEVICE_DESCRIPTOR deviceDescriptor, string devicePath)
			: this(null, deviceDescriptor, devicePath, true) { }
		internal UsbHub(UsbDevice parent, USB_DEVICE_DESCRIPTOR deviceDescriptor, string devicePath, Boolean roothub)
			: base(parent, deviceDescriptor, 0, devicePath) {
			this.IsRootHub = roothub;

			// TODO: Get the driver key name for the root hub.
			// Now let's open the hub (based upon the hub name we got above).
			using (SafeFileHandle handel2 = Kernel32.CreateFile(this.DevicePath, Kernel32.GENERIC_WRITE, Kernel32.FILE_SHARE_WRITE, IntPtr.Zero, Kernel32.OPEN_EXISTING, 0, IntPtr.Zero)) {
				if (handel2.IsInvalid) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
				USB_NODE_INFORMATION NodeInfo = new USB_NODE_INFORMATION();
				int nBytes = Marshal.SizeOf(typeof(USB_NODE_INFORMATION)); // Marshal.SizeOf(NodeInfo);
				// Get the hub information.
				int nBytesReturned = -1;
				if (Kernel32.DeviceIoControl(handel2, UsbApi.IOCTL_USB_GET_NODE_INFORMATION, ref NodeInfo, nBytes, out NodeInfo, nBytes, out nBytesReturned, IntPtr.Zero)) {
					this.NodeInformation = NodeInfo;
					this.IsBusPowered = Convert.ToBoolean(NodeInfo.HubInformation.HubIsBusPowered);
					this.PortCount = NodeInfo.HubInformation.HubDescriptor.bNumberOfPorts;
				}

				for (uint index = 1; index <= PortCount; index++) {
					devices.Add(BuildDevice(this, index, this.DevicePath, handel2));
				}
			}
		}

		private static UsbDevice BuildDevice(UsbDevice parent, uint portCount, string devicePath, SafeFileHandle handel1) {
			int nBytesReturned;
			int nBytes = Marshal.SizeOf(typeof(USB_NODE_CONNECTION_INFORMATION_EX));
			USB_NODE_CONNECTION_INFORMATION_EX nodeConnection = new USB_NODE_CONNECTION_INFORMATION_EX();
			nodeConnection.ConnectionIndex = portCount;

			//DateTime t = DateTime.Now;
			if (!Kernel32.DeviceIoControl(handel1, UsbApi.IOCTL_USB_GET_NODE_CONNECTION_INFORMATION_EX, ref nodeConnection, nBytes, out nodeConnection, nBytes, out nBytesReturned, IntPtr.Zero))
				throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
			//Console.WriteLine("IOCTL_USB_GET_NODE_CONNECTION_INFORMATION_EX took {0} ms (class={1})", DateTime.Now.Subtract(t).TotalMilliseconds, nodeConnection.DeviceDescriptor.bDeviceClass);
			bool isConnected = (nodeConnection.ConnectionStatus == USB_CONNECTION_STATUS.DeviceConnected);

			UsbDevice _Device = null;
			if (!isConnected) {
				_Device = new UsbDevice(parent, null, portCount);
			} else if (nodeConnection.DeviceDescriptor.bDeviceClass == UsbDeviceClass.HubDevice) {
				nBytes = Marshal.SizeOf(typeof(USB_NODE_CONNECTION_NAME));
				USB_NODE_CONNECTION_NAME nameConnection = new USB_NODE_CONNECTION_NAME();
				nameConnection.ConnectionIndex = portCount;
				if (!Kernel32.DeviceIoControl(handel1, UsbApi.IOCTL_USB_GET_NODE_CONNECTION_NAME, ref nameConnection, nBytes, out nameConnection, nBytes, out nBytesReturned, IntPtr.Zero))
					throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
				_Device = new UsbHub(parent, nodeConnection.DeviceDescriptor, @"\\?\" + nameConnection.NodeName, false);
			} else {
				_Device = new UsbDevice(parent, nodeConnection.DeviceDescriptor, portCount, devicePath);
			}
			_Device.NodeConnectionInfo = nodeConnection;
			_Device.AdapterNumber = _Device.NodeConnectionInfo.ConnectionIndex;
			_Device.Status = ((USB_CONNECTION_STATUS)_Device.NodeConnectionInfo.ConnectionStatus).ToString();
			_Device.Speed = ((USB_DEVICE_SPEED)_Device.NodeConnectionInfo.Speed).ToString();
			_Device.IsConnected = isConnected;
			_Device.IsHub = Convert.ToBoolean(_Device.NodeConnectionInfo.DeviceIsHub);
			return _Device;
		}
	}
}