using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using UCIS.USBLib.Internal.Windows;

namespace UCIS.HWLib.Windows.USB {
	public class UsbHub : UsbDevice {
		public bool IsRootHub { get; private set; }
		internal USB_NODE_INFORMATION NodeInformation { get; private set; }

		public int PortCount { get { return NodeInformation.HubInformation.HubDescriptor.bNumberOfPorts; } }
		public bool IsBusPowered { get { return NodeInformation.HubInformation.HubIsBusPowered; } }
		public override string DeviceDescription { get { return IsRootHub ? "RootHub" : "Standard-USB-Hub"; } }

		public override string DriverKey {
			get {
				if (Parent == null) return null;
				using (SafeFileHandle handle = OpenHandle(Parent.DevicePath)) return GetNodeConnectionDriverKey(handle, AdapterNumber);
			}
		}
		
		internal UsbHub(UsbDevice parent, USB_NODE_CONNECTION_INFORMATION_EX nci, string devicePath, uint port, Boolean roothub)
			: base(parent, nci, port, devicePath) {
			this.IsRootHub = roothub;
			using (SafeFileHandle handle = OpenHandle(DevicePath)) {
				USB_NODE_INFORMATION NodeInfo;
				GetNodeInformation(handle, out NodeInfo);
				this.NodeInformation = NodeInfo;
			}
		}

		public override int GetDescriptor(byte descriptorType, byte index, short langId, byte[] buffer, int offset, int length) {
			if (Parent == null) return 0;
			using (SafeFileHandle handle = UsbHub.OpenHandle(Parent.DevicePath)) return GetDescriptor(handle, AdapterNumber, descriptorType, index, langId, buffer, offset, length);
		}


		public IList<UsbDevice> Devices {
			get {
				List<UsbDevice> devices = new List<UsbDevice>();
				using (SafeFileHandle handle = OpenHandle(DevicePath)) {
					for (uint index = 1; index <= PortCount; index++) {
						devices.Add(BuildDevice(this, index, this.DevicePath, handle));
					}
				}
				return devices;
			}
		}

		internal static UsbDevice BuildDevice(UsbDevice parent, uint portCount, string devicePath) {
			using (SafeFileHandle handle = OpenHandle(devicePath)) return BuildDevice(parent, portCount, devicePath, handle);
		}
		internal static UsbDevice BuildDevice(UsbDevice parent, uint portCount, string devicePath, SafeFileHandle handle) {
			USB_NODE_CONNECTION_INFORMATION_EX nodeConnection;
			if (!GetNodeConnectionInformation(handle, portCount, out nodeConnection)) throw new Win32Exception(Marshal.GetLastWin32Error());
			UsbDevice device = null;
			if (nodeConnection.ConnectionStatus != USB_CONNECTION_STATUS.DeviceConnected) {
				device = new UsbDevice(parent, nodeConnection, portCount, devicePath);
			} else if (nodeConnection.DeviceDescriptor.DeviceClass == (Byte)UsbDeviceClass.HubDevice) {
				String nodeName = GetNodeConnectionName(handle, portCount);
				device = new UsbHub(parent, nodeConnection, @"\\?\" + nodeName, portCount, false);
			} else {
				device = new UsbDevice(parent, nodeConnection, portCount, devicePath);
			}
			device.NodeConnectionInfo = nodeConnection;
			return device;
		}
	}
}
