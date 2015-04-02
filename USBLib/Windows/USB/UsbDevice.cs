using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using UCIS.HWLib.Windows.Devices;
using UCIS.USBLib.Communication;
using UCIS.USBLib.Descriptor;
using UCIS.USBLib.Internal.Windows;

namespace UCIS.HWLib.Windows.USB {
	public class UsbDevice : IUsbDevice, IUsbInterface {
		internal static USB_NODE_CONNECTION_INFORMATION_EX GetNodeConnectionInformation(SafeFileHandle handle, UInt32 port) {
			int nBytes = Marshal.SizeOf(typeof(USB_NODE_CONNECTION_INFORMATION_EX));
			USB_NODE_CONNECTION_INFORMATION_EX nodeConnection = new USB_NODE_CONNECTION_INFORMATION_EX();
			nodeConnection.ConnectionIndex = port;
			if (!Kernel32.DeviceIoControl(handle, UsbApi.IOCTL_USB_GET_NODE_CONNECTION_INFORMATION_EX, ref nodeConnection, nBytes, out nodeConnection, nBytes, out nBytes, IntPtr.Zero))
				throw new Win32Exception();
			return nodeConnection;
		}

		private Boolean HasNodeConnectionInfo = false;
		private USB_NODE_CONNECTION_INFORMATION_EX NodeConnectionInfo;

		private void GetNodeConnectionInfo() {
			if (HasNodeConnectionInfo) return;
			if (Parent == null) {
				HasNodeConnectionInfo = true;
				return;
			}
			using (SafeFileHandle handle = Parent.OpenHandle()) NodeConnectionInfo = GetNodeConnectionInformation(handle, AdapterNumber);
			HasNodeConnectionInfo = true;
		}
		internal void SetNodeConnectionInfo(USB_NODE_CONNECTION_INFORMATION_EX nci) {
			NodeConnectionInfo = nci;
			HasNodeConnectionInfo = true;
		}

		public UsbDeviceDescriptor DeviceDescriptor { get { GetNodeConnectionInfo(); return NodeConnectionInfo.DeviceDescriptor; } }

		public bool IsHub { get { GetNodeConnectionInfo(); return NodeConnectionInfo.DeviceIsHub != 0; } }
		public bool IsConnected { get { GetNodeConnectionInfo(); return NodeConnectionInfo.ConnectionStatus == USB_CONNECTION_STATUS.DeviceConnected; } }
		public string Status { get { GetNodeConnectionInfo(); return NodeConnectionInfo.ConnectionStatus.ToString(); } }
		public string Speed { get { GetNodeConnectionInfo(); return NodeConnectionInfo.Speed.ToString(); } }
		public Byte CurrentConfigurationValue { get { GetNodeConnectionInfo(); return NodeConnectionInfo.CurrentConfigurationValue; } }
		public UInt16 DeviceAddress { get { GetNodeConnectionInfo(); return NodeConnectionInfo.DeviceAddress; } }
		public UInt32 NumberOfOpenPipes { get { GetNodeConnectionInfo(); return NodeConnectionInfo.NumberOfOpenPipes; } }

		public int NumConfigurations { get { return DeviceDescriptor.NumConfigurations; } }
		public int VendorID { get { return DeviceDescriptor.VendorID; } }
		public int ProductID { get { return DeviceDescriptor.ProductID; } }

		private short[] languages = null;

		private String GetStringSafe(Byte id) {
			if (id == 0) return null;
			if (languages == null) {
				Byte[] buff = new Byte[256];
				int len = GetDescriptor((Byte)UsbDescriptorType.String, 0, 0, buff, 0, buff.Length);
				if (len > 1) {
					languages = new short[len / 2 - 1];
					for (int i = 0; i < languages.Length; i++) languages[i] = BitConverter.ToInt16(buff, i * 2 + 2);
				}
			}
			short language = (languages == null || languages.Length == 0) ? (short)0 : languages[0];
			String s = UsbStringDescriptor.GetStringFromDevice(this, id, language);
			if (s == null) return s;
			return s.Trim(' ', '\0');
		}

		public string Manufacturer { get { return GetStringSafe(DeviceDescriptor.ManufacturerStringID); } }
		public string Product { get { return GetStringSafe(DeviceDescriptor.ProductStringID); } }
		public string SerialNumber { get { return GetStringSafe(DeviceDescriptor.SerialNumberStringID); } }
		public String DriverKey {
			get {
				if (mParent != null) {
					using (SafeFileHandle handle = mParent.OpenHandle()) {
						USB_NODE_CONNECTION_DRIVERKEY_NAME DriverKeyStruct = new USB_NODE_CONNECTION_DRIVERKEY_NAME();
						int nBytes = Marshal.SizeOf(DriverKeyStruct);
						DriverKeyStruct.ConnectionIndex = AdapterNumber;
						if (!Kernel32.DeviceIoControl(handle, UsbApi.IOCTL_USB_GET_NODE_CONNECTION_DRIVERKEY_NAME, ref DriverKeyStruct, nBytes, out DriverKeyStruct, nBytes, out nBytes, IntPtr.Zero))
							return null;
						return DriverKeyStruct.DriverKeyName;
					}
				}
				if (mDeviceNode != null) return mDeviceNode.DriverKey;
				return null;
			}
		}

		public virtual string DeviceDescription { get { return DeviceNode == null ? null : DeviceNode.DeviceDescription; } }
		public string DeviceID { get { return DeviceNode == null ? null : DeviceNode.DeviceID; } }

		protected DeviceNode mDeviceNode = null;
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
		protected UsbHub mParent = null;
		private UInt32 mAdapterNumber = 0;
		private UsbHub GetParent() {
			if (mParent == null && mDeviceNode != null) {
				mParent = UsbHub.GetHubForDeviceNode(mDeviceNode.GetParent());
				if (mParent != null) {
					UsbDevice self = mParent.FindChildForDeviceNode(mDeviceNode);
					if (self != null) mAdapterNumber = self.AdapterNumber;
				}
			}
			return mParent;
		}
		public UsbHub Parent { get { return GetParent(); } }
		public UInt32 AdapterNumber { get { GetParent(); return mAdapterNumber; } }

		internal UsbDevice(DeviceNode devnode, UsbHub parent, uint port) {
			this.mDeviceNode = devnode;
			this.mParent = parent;
			this.mAdapterNumber = port;
		}

		public static UsbDevice GetUsbDevice(DeviceNode node) {
			if (node == null) return null;
			//UsbController controller = UsbController.GetControllerForDeviceNode(node);
			//if (controller != null) return controller;
			UsbHub hub = UsbHub.GetHubForDeviceNode(node);
			if (hub != null) return hub;
			DeviceNode parent = node.GetParent();
			if (parent == null) return null;
			if (parent.Service == "usbccgp") return GetUsbDevice(parent);
			hub = UsbHub.GetHubForDeviceNode(parent);
			if (hub == null) return null;
			return hub.FindChildForDeviceNode(node);
		}

		#region IUsbInterface and IUsbDevice Members
		public int GetDescriptor(byte descriptorType, byte index, short langId, byte[] buffer, int offset, int length) {
			using (SafeFileHandle handle = Parent.OpenHandle()) {
				int szRequest = Marshal.SizeOf(typeof(USB_DESCRIPTOR_REQUEST));
				USB_DESCRIPTOR_REQUEST request = new USB_DESCRIPTOR_REQUEST();
				request.ConnectionIndex = AdapterNumber;
				request.SetupPacket.Value = (short)((descriptorType << 8) + index);
				request.SetupPacket.Index = (short)langId;
				request.SetupPacket.Length = (short)length;
				int nBytes = length + szRequest;
				Byte[] bigbuffer = new Byte[nBytes];
				if (!Kernel32.DeviceIoControl(handle, UsbApi.IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION, ref request, Marshal.SizeOf(typeof(USB_DESCRIPTOR_REQUEST)), bigbuffer, nBytes, out nBytes, IntPtr.Zero)) {
					if (descriptorType == (Byte)UsbDescriptorType.Device && index == 0 && langId == 0) {
						Byte[] descbytes = DeviceDescriptor.GetBytes();
						length = Math.Min(descbytes.Length, length);
						Array.Copy(descbytes, 0, buffer, 0, length);
						return length;
					}
					int err = Marshal.GetLastWin32Error();
					if (err != 31) throw new Win32Exception(err);
					return 0;
				}
				nBytes -= szRequest;
				if (nBytes > length) nBytes = length;
				if (nBytes < 0) return 0;
				if (nBytes > 0) Buffer.BlockCopy(bigbuffer, szRequest, buffer, offset, nBytes);
				return nBytes;
			}
		}
		byte IUsbInterface.Configuration { get { return CurrentConfigurationValue; } }
		byte IUsbDevice.Configuration { get { return CurrentConfigurationValue; } set { throw new NotSupportedException(); } }
		void IUsbDevice.ResetDevice() { throw new NotImplementedException(); }
		IUsbDeviceRegistry IUsbDevice.Registry { get { throw new NotImplementedException(); } }
		void IUsbInterface.Close() { }
		int IUsbInterface.PipeTransfer(byte endpoint, byte[] buffer, int offset, int length) { throw new NotSupportedException(); }
		IAsyncResult IUsbInterface.BeginPipeTransfer(Byte endpoint, Byte[] buffer, int offset, int length, AsyncCallback callback, Object state) { throw new NotSupportedException(); }
		int IUsbInterface.EndPipeTransfer(IAsyncResult asyncResult) { throw new NotSupportedException(); }
		void IUsbInterface.PipeReset(byte endpoint) { throw new NotImplementedException(); }
		void IUsbInterface.PipeAbort(byte endpoint) { throw new NotImplementedException(); }
		int IUsbInterface.ControlTransfer(UsbControlRequestType requestType, byte request, short value, short index, byte[] buffer, int offset, int length) { throw new NotSupportedException(); }
		UsbPipeStream IUsbInterface.GetPipeStream(byte endpoint) { throw new NotSupportedException(); }
		void IDisposable.Dispose() { }
		void IUsbDevice.ClaimInterface(int interfaceID) { }
		void IUsbDevice.ReleaseInterface(int interfaceID) { }
		#endregion
	}
}
