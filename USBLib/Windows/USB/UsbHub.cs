using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using UCIS.HWLib.Windows.Devices;
using UCIS.USBLib.Internal.Windows;

namespace UCIS.HWLib.Windows.USB {
	public class UsbHub : UsbDevice {
		internal static SafeFileHandle OpenHandle(String path) {
			SafeFileHandle handle = Kernel32.CreateFile(path, Kernel32.GENERIC_WRITE, Kernel32.FILE_SHARE_WRITE, IntPtr.Zero, Kernel32.OPEN_EXISTING, 0, IntPtr.Zero);
			if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
			return handle;
		}

		private Boolean HasNodeInformation = false;
		private USB_NODE_INFORMATION NodeInformation;

		private void GetNodeInformation() {
			if (HasNodeInformation) return;
			NodeInformation = new USB_NODE_INFORMATION();
			int nBytes = Marshal.SizeOf(typeof(USB_NODE_INFORMATION));
			using (SafeFileHandle handle = OpenHandle()) 
				if (!Kernel32.DeviceIoControl(handle, UsbApi.IOCTL_USB_GET_NODE_INFORMATION, ref NodeInformation, nBytes, out NodeInformation, nBytes, out nBytes, IntPtr.Zero))
					throw new Win32Exception(Marshal.GetLastWin32Error());
		}

		public bool IsRootHub { get; private set; }
		public int PortCount { get { GetNodeInformation(); return NodeInformation.HubInformation.HubDescriptor.bNumberOfPorts; } }
		public bool IsBusPowered { get { GetNodeInformation(); return NodeInformation.HubInformation.HubIsBusPowered; } }
		public override string DeviceDescription { get { return IsRootHub ? "Root hub" : base.DeviceDescription; } }

		public String DevicePath { get; private set; }

		internal UsbHub(UsbHub parent, string devicePath, uint port)
			: base(null, parent, port) {
			this.DevicePath = devicePath;
			this.IsRootHub = (parent == null);
			if (IsRootHub) SetNodeConnectionInfo(new USB_NODE_CONNECTION_INFORMATION_EX());
		}

		private UsbHub(DeviceNode devnode, string devicePath)
			: base(devnode, null, 0) {
			this.DevicePath = devicePath;
			this.IsRootHub = false;
		}

		internal SafeFileHandle OpenHandle() {
			return OpenHandle(DevicePath);
		}

		public IList<UsbDevice> Devices {
			get {
				UsbDevice[] devices = new UsbDevice[PortCount];
				using (SafeFileHandle handle = OpenHandle()) {
					for (uint index = 1; index <= PortCount; index++) {
						USB_NODE_CONNECTION_INFORMATION_EX nodeConnection = GetNodeConnectionInformation(handle, index);
						UsbDevice device;
						if (nodeConnection.ConnectionStatus != USB_CONNECTION_STATUS.DeviceConnected) {
							device = new UsbDevice(null, this, index);
						} else if (nodeConnection.DeviceDescriptor.DeviceClass == (Byte)UsbDeviceClass.HubDevice) {
							int nBytes = Marshal.SizeOf(typeof(USB_NODE_CONNECTION_NAME));
							USB_NODE_CONNECTION_NAME nameConnection = new USB_NODE_CONNECTION_NAME();
							nameConnection.ConnectionIndex = index;
							if (!Kernel32.DeviceIoControl(handle, UsbApi.IOCTL_USB_GET_NODE_CONNECTION_NAME, ref nameConnection, nBytes, out nameConnection, nBytes, out nBytes, IntPtr.Zero))
								throw new Win32Exception(Marshal.GetLastWin32Error());
							device = new UsbHub(this, @"\\?\" + nameConnection.NodeName, index);
						} else {
							device = new UsbDevice(null, this, index);
						}
						device.SetNodeConnectionInfo(nodeConnection);
						devices[index - 1] = device;
					}
				}
				return devices;
			}
		}

		static readonly Guid IID_DEVINTERFACE_USB_HUB = new Guid(UsbApi.GUID_DEVINTERFACE_USB_HUB);
		public static UsbHub GetHubForDeviceNode(DeviceNode node) {
			if (node == null) return null;
			String[] interfaces = node.GetInterfaces(IID_DEVINTERFACE_USB_HUB);
			if (interfaces == null || interfaces.Length == 0) return null;
			return new UsbHub(node, interfaces[0]);
		}

		public static IList<UsbHub> GetHubs() {
			IList<UsbHub> devices = new List<UsbHub>();
			foreach (DeviceNode dev in DeviceNode.GetDevices(IID_DEVINTERFACE_USB_HUB)) {
				UsbHub hub = GetHubForDeviceNode(dev);
				if (hub != null) devices.Add(hub);
			}
			return devices;
		}

		public UsbDevice FindChildForDeviceNode(DeviceNode node) {
			String driverkey = node.DriverKey;
			if (driverkey != null) {
				foreach (UsbDevice child in Devices) if (driverkey.Equals(child.DriverKey, StringComparison.InvariantCultureIgnoreCase)) return child;
			} else {
				int? address = node.Address;
				if (address == null || address.Value == 0) return null;
				int port = address.Value;
				foreach (UsbDevice child in Devices) if (port == child.AdapterNumber) return child;
			}
			return null;
		}
	}
}
