using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using UCIS.HWLib.Windows.Devices;
using UCIS.USBLib.Communication;
using UCIS.USBLib.Descriptor;
using UCIS.USBLib.Internal.Windows;

namespace UCIS.HWLib.Windows.USB {
	public class UsbDevice : IUsbInterface {
		internal static SafeFileHandle OpenHandle(String path) {
			SafeFileHandle handle = Kernel32.CreateFile(path, Kernel32.GENERIC_WRITE, Kernel32.FILE_SHARE_WRITE, IntPtr.Zero, Kernel32.OPEN_EXISTING, 0, IntPtr.Zero);
			if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
			return handle;
		}
		internal static Boolean GetNodeInformation(SafeFileHandle handle, out USB_NODE_INFORMATION nodeInfo) {
			nodeInfo = new USB_NODE_INFORMATION();
			int nBytes = Marshal.SizeOf(typeof(USB_NODE_INFORMATION));
			return Kernel32.DeviceIoControl(handle, UsbApi.IOCTL_USB_GET_NODE_INFORMATION, ref nodeInfo, nBytes, out nodeInfo, nBytes, out nBytes, IntPtr.Zero);
		}
		internal static Boolean GetNodeConnectionInformation(SafeFileHandle handle, UInt32 port, out USB_NODE_CONNECTION_INFORMATION_EX nodeConnection) {
			int nBytes = Marshal.SizeOf(typeof(USB_NODE_CONNECTION_INFORMATION_EX));
			nodeConnection = new USB_NODE_CONNECTION_INFORMATION_EX();
			nodeConnection.ConnectionIndex = port;
			if (!Kernel32.DeviceIoControl(handle, UsbApi.IOCTL_USB_GET_NODE_CONNECTION_INFORMATION_EX, ref nodeConnection, nBytes, out nodeConnection, nBytes, out nBytes, IntPtr.Zero))
				throw new Win32Exception(Marshal.GetLastWin32Error());
			return true;
		}
		internal static String GetNodeConnectionName(SafeFileHandle handle, UInt32 port) {
			int nBytes = Marshal.SizeOf(typeof(USB_NODE_CONNECTION_NAME));
			USB_NODE_CONNECTION_NAME nameConnection = new USB_NODE_CONNECTION_NAME();
			nameConnection.ConnectionIndex = port;
			if (!Kernel32.DeviceIoControl(handle, UsbApi.IOCTL_USB_GET_NODE_CONNECTION_NAME, ref nameConnection, nBytes, out nameConnection, nBytes, out nBytes, IntPtr.Zero))
				throw new Win32Exception(Marshal.GetLastWin32Error());
			return nameConnection.NodeName;
		}
		internal unsafe static int GetDescriptor(SafeFileHandle handle, UInt32 port, byte descriptorType, byte index, short langId, byte[] buffer, int offset, int length) {
			int szRequest = Marshal.SizeOf(typeof(USB_DESCRIPTOR_REQUEST));
			USB_DESCRIPTOR_REQUEST request = new USB_DESCRIPTOR_REQUEST();
			request.ConnectionIndex = port;
			request.SetupPacket.wValue = (ushort)((descriptorType << 8) + index);
			request.SetupPacket.wIndex = (ushort)langId;
			request.SetupPacket.wLength = (ushort)length;
			int nBytes = length + szRequest;
			Byte[] bigbuffer = new Byte[nBytes];
			if (!Kernel32.DeviceIoControl(handle, UsbApi.IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION, ref request, Marshal.SizeOf(typeof(USB_DESCRIPTOR_REQUEST)), bigbuffer, nBytes, out nBytes, IntPtr.Zero)) {
				int err = Marshal.GetLastWin32Error();
				if (err != 2 && err != 31 && err != 87) throw new Win32Exception(err);
				return 0;
			}
			nBytes -= szRequest;
			if (nBytes > length) nBytes = length;
			if (nBytes < 0) return 0;
			if (nBytes > 0) Buffer.BlockCopy(bigbuffer, szRequest, buffer, offset, nBytes);
			return nBytes;
		}
		internal unsafe static String GetRootHubName(SafeFileHandle handle) {
			USB_ROOT_HUB_NAME rootHubName = new USB_ROOT_HUB_NAME();
			int nBytesReturned;
			if (!Kernel32.DeviceIoControl(handle, UsbApi.IOCTL_USB_GET_ROOT_HUB_NAME, IntPtr.Zero, 0, out rootHubName, Marshal.SizeOf(rootHubName), out nBytesReturned, IntPtr.Zero))
				throw new Win32Exception(Marshal.GetLastWin32Error());
			if (rootHubName.ActualLength <= 0) return null;
			return rootHubName.RootHubName;
		}
		internal unsafe static String GetNodeConnectionDriverKey(SafeFileHandle handle, UInt32 port) {
			USB_NODE_CONNECTION_DRIVERKEY_NAME DriverKeyStruct = new USB_NODE_CONNECTION_DRIVERKEY_NAME();
			int nBytes = Marshal.SizeOf(DriverKeyStruct);
			DriverKeyStruct.ConnectionIndex = port;
			if (!Kernel32.DeviceIoControl(handle, UsbApi.IOCTL_USB_GET_NODE_CONNECTION_DRIVERKEY_NAME, ref DriverKeyStruct, nBytes, out DriverKeyStruct, nBytes, out nBytes, IntPtr.Zero))
				return null;
			return DriverKeyStruct.DriverKeyName;
		}

		public UsbDevice Parent { get; protected set; }
		public string DevicePath { get; private set; }
		public uint AdapterNumber { get; private set; }
		internal USB_NODE_CONNECTION_INFORMATION_EX NodeConnectionInfo { get; set; }
		internal USB_DEVICE_DESCRIPTOR DeviceDescriptor { get { return NodeConnectionInfo.DeviceDescriptor; } }

		public bool IsHub { get { return NodeConnectionInfo.DeviceIsHub != 0; } }
		public bool IsConnected { get { return NodeConnectionInfo.ConnectionStatus == USB_CONNECTION_STATUS.DeviceConnected; } }
		public string Status { get { return NodeConnectionInfo.ConnectionStatus.ToString(); } }
		public string Speed { get { return NodeConnectionInfo.Speed.ToString(); } }

		SafeFileHandle OpenHandle() {
			return OpenHandle(DevicePath);
		}

		public int NumConfigurations { get { return DeviceDescriptor == null ? 0 : DeviceDescriptor.bNumConfigurations; } }
		public int VendorID { get { return DeviceDescriptor == null ? 0 : DeviceDescriptor.idVendor; } }
		public int ProductID { get { return DeviceDescriptor == null ? 0 : DeviceDescriptor.idProduct; } }

		private String GetStringSafe(Byte id) {
			if (id == 0) return null;
			String s = GetStringDescriptor(id);
			if (s == null) return s;
			return s.Trim(' ', '\0');
		}

		public string Manufacturer { get { return DeviceDescriptor == null ? null : GetStringSafe(DeviceDescriptor.iManufacturer); } }
		public string Product { get { return DeviceDescriptor == null ? null : GetStringSafe(DeviceDescriptor.iProduct); } }
		public string SerialNumber { get { return DeviceDescriptor == null ? null : GetStringSafe(DeviceDescriptor.iSerialNumber); } }
		public virtual string DriverKey { get { using (SafeFileHandle handle = OpenHandle(DevicePath)) return UsbHub.GetNodeConnectionDriverKey(handle, AdapterNumber); } }

		public virtual string DeviceDescription { get { return DeviceNode == null ? null : DeviceNode.GetPropertyString(CMRDP.DEVICEDESC); } }
		public string DeviceID { get { return DeviceNode == null ? null : DeviceNode.DeviceID; } }

		private DeviceNode mDeviceNode;
		public DeviceNode DeviceNode {
			get {
				String dk = DriverKey;
				if (mDeviceNode == null && dk != null) {
					foreach (DeviceNode node in DeviceNode.GetDevices("USB")) {
						if (dk.Equals(node.DriverKey, StringComparison.InvariantCultureIgnoreCase)) {
							mDeviceNode = node;
							break;
						}
					}
				}
				return mDeviceNode;
			}
		}

		internal UsbDevice(UsbDevice parent, USB_NODE_CONNECTION_INFORMATION_EX nci, uint port, string devicePath) {
			this.Parent = parent;
			this.NodeConnectionInfo = nci;
			this.DevicePath = devicePath;
			this.AdapterNumber = port;
			if (devicePath == null) return;
		}

		private String GetStringDescriptor(Byte index) {
			return UsbStringDescriptor.GetStringFromDevice(this, index, 0); //0x409
		}

		static UsbDevice GetUsbDevice(DeviceNode node, out Boolean isHostController) {
			String[] hciinterface = node.GetInterfaces(UsbApi.GUID_DEVINTERFACE_USB_HOST_CONTROLLER);
			if (hciinterface != null && hciinterface.Length > 0) {
				isHostController = true;
				return (new UsbController(null, node, hciinterface[0])).RootHub;
			}
			isHostController = false;
			DeviceNode parent = node.GetParent();
			Boolean isHostControllerA;
			UsbDevice usbdev = GetUsbDevice(parent, out isHostControllerA);
			if (isHostControllerA) return usbdev;
			UsbHub usbhub = usbdev as UsbHub;
			if (usbhub == null) return null;
			String driverkey = node.DriverKey;
			foreach (UsbDevice child in usbhub.Devices) {
				if (driverkey.Equals(child.DriverKey, StringComparison.InvariantCultureIgnoreCase)) return child;
			}
			return null;
		}
		public static UsbDevice GetUsbDevice(DeviceNode node) {
			Boolean isHostController;
			return GetUsbDevice(node, out isHostController);
			/*

			String[] hubinterface = node.GetInterfaces(UsbApi.GUID_DEVINTERFACE_USB_HUB);
			if (hubinterface != null && hubinterface.Length > 0) {
				USB_NODE_CONNECTION_INFORMATION_EX nodeConnection;
				using (SafeFileHandle handle = OpenHandle(hubinterface[0])) {
					if (!GetNodeConnectionInformation(handle, 0, out nodeConnection)) return null;
				}
				return new UsbHub(null, nodeConnection, hubinterface[0], false);
			}
			String[] devinterface = node.GetInterfaces(UsbApi.GUID_DEVINTERFACE_USB_DEVICE);
			if (devinterface == null || devinterface.Length == 0) return null;
			DeviceNode parent = node.GetParent();
			if (parent == null) return null;
			UsbHub usbhub = GetUsbDevice(parent) as UsbHub;
			if (usbhub == null) return null;
			String driverkey = node.DriverKey;
			foreach (UsbDevice usbdev in usbhub.Devices) {
				if (driverkey.Equals(usbdev.DriverKey, StringComparison.InvariantCultureIgnoreCase)) return usbdev;
			}
			return null;*/
		}

		#region IUsbInterface Members
		byte IUsbInterface.Configuration { get { throw new NotImplementedException(); } }
		void IUsbInterface.Close() { }
		public virtual int GetDescriptor(byte descriptorType, byte index, short langId, byte[] buffer, int offset, int length) {
			using (SafeFileHandle handle = UsbHub.OpenHandle(DevicePath)) return UsbHub.GetDescriptor(handle, AdapterNumber, descriptorType, index, langId, buffer, offset, length);
		}
		string IUsbInterface.GetString(short langId, byte stringIndex) {
			return UsbStringDescriptor.GetStringFromDevice(this, stringIndex, langId);
		}
		int IUsbInterface.BulkWrite(byte endpoint, byte[] buffer, int offset, int length) { throw new NotImplementedException(); }
		int IUsbInterface.BulkRead(byte endpoint, byte[] buffer, int offset, int length) { throw new NotImplementedException(); }
		void IUsbInterface.BulkReset(byte endpoint) { throw new NotImplementedException(); }
		int IUsbInterface.InterruptWrite(byte endpoint, byte[] buffer, int offset, int length) { throw new NotImplementedException(); }
		int IUsbInterface.InterruptRead(byte endpoint, byte[] buffer, int offset, int length) { throw new NotImplementedException(); }
		void IUsbInterface.InterruptReset(byte endpoint) { throw new NotImplementedException(); }
		int IUsbInterface.ControlWrite(UsbControlRequestType requestType, byte request, short value, short index, byte[] buffer, int offset, int length) { throw new NotImplementedException(); }
		int IUsbInterface.ControlRead(UsbControlRequestType requestType, byte request, short value, short index, byte[] buffer, int offset, int length) { throw new NotImplementedException(); }
		UsbPipeStream IUsbInterface.GetBulkStream(byte endpoint) { throw new NotImplementedException(); }
		UsbPipeStream IUsbInterface.GetInterruptStream(byte endpoint) { throw new NotImplementedException(); }
		void IDisposable.Dispose() { }
		#endregion
	}
	
}
