using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using UCIS.USBLib.Internal.Windows;
using UCIS.HWLib.Windows.Devices;

namespace UCIS.HWLib.Windows.USB {
	public class UsbDevice {
		public UsbDevice Parent { get; protected set; }
		internal USB_NODE_CONNECTION_INFORMATION_EX NodeConnectionInfo { get; set; }
		internal USB_DEVICE_DESCRIPTOR DeviceDescriptor { get; private set; }
		internal IList<USB_CONFIGURATION_DESCRIPTOR> ConfigurationDescriptor { get; private set; }
		internal IList<USB_INTERFACE_DESCRIPTOR> InterfaceDescriptor { get; private set; }
		internal IList<USB_ENDPOINT_DESCRIPTOR> EndpointDescriptor { get; private set; }
		internal IList<HID_DESCRIPTOR> HdiDescriptor { get; private set; }
		public string DevicePath { get; private set; }
		public uint AdapterNumber { get; internal set; }
		public string DriverKey { get; private set; }

		public virtual string DeviceDescription { get { return DeviceNode.GetPropertyString(CMRDP.DEVICEDESC); } }
		public string DeviceID { get { return DeviceNode.DeviceID; } }

		public bool IsConnected { get; internal set; }
		public bool IsHub { get; internal set; }
		public string Status { get; internal set; }
		public string Speed { get; internal set; }

		public string Manufacturer { get; private set; }
		public string SerialNumber { get; private set; }
		public string Product { get; private set; }

		public int VendorID { get { return DeviceDescriptor.idVendor; } }
		public int ProductID { get { return DeviceDescriptor.idProduct; } }

		private DeviceNode mDeviceNode;
		public DeviceNode DeviceNode {
			get {
				if (mDeviceNode == null && DriverKey != null) {
					foreach (DeviceNode node in DeviceNode.GetDevices("USB")) {
						if (DriverKey.Equals(node.DriverKey, StringComparison.InvariantCultureIgnoreCase)) {
							mDeviceNode = node;
							break;
						}
					}
				}
				return mDeviceNode;
			}
		}

		internal UsbDevice(UsbDevice parent, USB_DEVICE_DESCRIPTOR deviceDescriptor, uint adapterNumber)
			: this(parent, deviceDescriptor, adapterNumber, null) { }
		unsafe internal UsbDevice(UsbDevice parent, USB_DEVICE_DESCRIPTOR deviceDescriptor, uint adapterNumber, string devicePath) {
			this.Parent = parent;
			this.AdapterNumber = adapterNumber;
			this.DeviceDescriptor = deviceDescriptor;
			this.DevicePath = devicePath;
			ConfigurationDescriptor = new List<USB_CONFIGURATION_DESCRIPTOR>();
			InterfaceDescriptor = new List<USB_INTERFACE_DESCRIPTOR>();
			HdiDescriptor = new List<HID_DESCRIPTOR>();
			EndpointDescriptor = new List<USB_ENDPOINT_DESCRIPTOR>();
			if (devicePath == null) return;

			using (SafeFileHandle handel = Kernel32.CreateFile(devicePath, Kernel32.GENERIC_WRITE, Kernel32.FILE_SHARE_WRITE, IntPtr.Zero, Kernel32.OPEN_EXISTING, 0, IntPtr.Zero)) {
				if (handel.IsInvalid) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
				int nBytesReturned;
				int nBytes = UsbApi.MAX_BUFFER_SIZE;

				USB_DESCRIPTOR_REQUEST Request1 = new USB_DESCRIPTOR_REQUEST();
				Request1.ConnectionIndex = adapterNumber;// portCount;
				Request1.SetupPacket.wValue = (ushort)((UsbApi.USB_CONFIGURATION_DESCRIPTOR_TYPE << 8));
				Request1.SetupPacket.wLength = (ushort)(nBytes - Marshal.SizeOf(Request1));
				Request1.SetupPacket.wIndex = 0; // 0x409; // Language Code

				// Use an IOCTL call to request the String Descriptor
				Byte[] buffer = new Byte[nBytes];
				fixed (Byte* bufferptr = buffer) {
					if (!Kernel32.DeviceIoControl(handel, UsbApi.IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION, ref Request1, Marshal.SizeOf(typeof(USB_DESCRIPTOR_REQUEST)), (IntPtr)bufferptr, nBytes, out nBytesReturned, IntPtr.Zero)) {
						int err = Marshal.GetLastWin32Error();
						Console.WriteLine("IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION returned {0} {1}", err, (new System.ComponentModel.Win32Exception(err)).Message);
						//SerialNumber = (new System.ComponentModel.Win32Exception(err)).Message;
						if (err != 2 && err != 31 && err != 87) throw new System.ComponentModel.Win32Exception(err);
					} else {
						if (nBytesReturned > nBytes) throw new IndexOutOfRangeException("IOCtl returned too much data");
						if (nBytesReturned < 0) throw new IndexOutOfRangeException("IOCtl returned insufficient data");
						Byte* ptr = bufferptr + Marshal.SizeOf(Request1);
						nBytesReturned -= Marshal.SizeOf(Request1);
						if (nBytesReturned < 0) throw new IndexOutOfRangeException("IOCtl returned insufficient data");

						int offset = 0;
						while (offset < nBytesReturned) {
							if (offset + Marshal.SizeOf(typeof(USB_DESCRIPTOR)) >= nBytesReturned) throw new IndexOutOfRangeException("Error in configuration descriptor");
							USB_DESCRIPTOR* desc = (USB_DESCRIPTOR*)(ptr + offset);
							offset += desc->bLength;
							if (offset > nBytesReturned) throw new IndexOutOfRangeException("Error in configuration descriptor");
							Console.WriteLine("Descriptor type {0} length {1}", desc->bDescriptorType, desc->bLength);
							if (desc->bDescriptorType == USB_DESCRIPTOR_TYPE.ConfigurationDescriptorType) {
								if (desc->bLength < 9) throw new IndexOutOfRangeException("Error in configuration descriptor");
								USB_CONFIGURATION_DESCRIPTOR configurationDescriptor = *(USB_CONFIGURATION_DESCRIPTOR*)desc;
								ConfigurationDescriptor.Add(configurationDescriptor);
							} else if (desc->bDescriptorType == USB_DESCRIPTOR_TYPE.InterfaceDescriptorType) {
								if (desc->bLength < 9) throw new IndexOutOfRangeException("Error in configuration descriptor");
								USB_INTERFACE_DESCRIPTOR interfaceDescriptor = *(USB_INTERFACE_DESCRIPTOR*)desc;
								InterfaceDescriptor.Add(interfaceDescriptor);
							} else if (desc->bDescriptorType == USB_DESCRIPTOR_TYPE.EndpointDescriptorType) {
								if (desc->bLength < 7) throw new IndexOutOfRangeException("Error in configuration descriptor");
								USB_ENDPOINT_DESCRIPTOR endpointDescriptor1 = *(USB_ENDPOINT_DESCRIPTOR*)desc;
								EndpointDescriptor.Add(endpointDescriptor1);
							}
						}
					}
					// The iManufacturer, iProduct and iSerialNumber entries in the
					// device descriptor are really just indexes.  So, we have to 
					// request a string descriptor to get the values for those strings.
					if (DeviceDescriptor != null) {
						if (DeviceDescriptor.iManufacturer > 0) Manufacturer = GetStringDescriptor(handel, DeviceDescriptor.iManufacturer);
						if (DeviceDescriptor.iProduct > 0) Product = GetStringDescriptor(handel, DeviceDescriptor.iProduct);
						if (DeviceDescriptor.iSerialNumber > 0) SerialNumber = GetStringDescriptor(handel, DeviceDescriptor.iSerialNumber);
					}
				}
				// Get the Driver Key Name (usefull in locating a device)
				USB_NODE_CONNECTION_DRIVERKEY_NAME DriverKeyStruct = new USB_NODE_CONNECTION_DRIVERKEY_NAME();
				DriverKeyStruct.ConnectionIndex = adapterNumber;
				// Use an IOCTL call to request the Driver Key Name
				if (Kernel32.DeviceIoControl(handel, UsbApi.IOCTL_USB_GET_NODE_CONNECTION_DRIVERKEY_NAME, ref DriverKeyStruct, Marshal.SizeOf(DriverKeyStruct), out DriverKeyStruct, Marshal.SizeOf(DriverKeyStruct), out nBytesReturned, IntPtr.Zero)) {
					DriverKey = DriverKeyStruct.DriverKeyName;
				}
			}
		}

		private unsafe String GetStringDescriptor(SafeFileHandle handel, Byte id) {
			int nBytes = UsbApi.MAX_BUFFER_SIZE;
			int nBytesReturned = 0;
			Byte[] buffer = new Byte[nBytes];
			fixed (Byte* bufferptr = buffer) {
				// Build a request for string descriptor.
				USB_DESCRIPTOR_REQUEST Request = new USB_DESCRIPTOR_REQUEST();
				Request.ConnectionIndex = AdapterNumber;
				Request.SetupPacket.wValue = (ushort)((UsbApi.USB_STRING_DESCRIPTOR_TYPE << 8) + id);
				Request.SetupPacket.wLength = (ushort)(nBytes - Marshal.SizeOf(Request));
				Request.SetupPacket.wIndex = 0x409; // The language code.
				// Use an IOCTL call to request the string descriptor.
				if (Kernel32.DeviceIoControl(handel, UsbApi.IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION, ref Request, Marshal.SizeOf(Request), (IntPtr)bufferptr, nBytes, out nBytesReturned, IntPtr.Zero)) {
					if (nBytesReturned < nBytes) buffer[nBytesReturned] = 0;
					if (nBytesReturned + 1 < nBytes) buffer[nBytesReturned + 1] = 0;
					// The location of the string descriptor is immediately after
					// the Request structure.  Because this location is not "covered"
					// by the structure allocation, we're forced to zero out this
					// chunk of memory by using the StringToHGlobalAuto() hack above
					//*(UsbApi.USB_STRING_DESCRIPTOR*)(bufferptr + Marshal.SizeOf(Request));
					USB_STRING_DESCRIPTOR StringDesc = (USB_STRING_DESCRIPTOR)Marshal.PtrToStructure((IntPtr)(bufferptr + Marshal.SizeOf(Request)), typeof(USB_STRING_DESCRIPTOR));
					//return StringDesc.bString;
					int len = Math.Min(nBytesReturned - Marshal.SizeOf(Request) - 2, StringDesc.bLength);
					return Encoding.Unicode.GetString(buffer, 2 + Marshal.SizeOf(Request), len);
				} else {
					return null;
				}
			}
		}

		/*public static UsbDevice GetUsbDevice(DeviceNode node) {
			String[] hubinterface = node.GetInterfaces(UsbApi.GUID_DEVINTERFACE_USB_HUB);
			if (hubinterface != null && hubinterface.Length > 0) return new UsbHub(null, hubinterface[0], false);
			String[] devinterface = node.GetInterfaces(UsbApi.GUID_DEVINTERFACE_USB_DEVICE);
			if (devinterface == null || devinterface.Length == 0) throw new InvalidOperationException("Device is not an USB device");
			DeviceNode parent = node.GetParent();
			if (parent == null) throw new InvalidOperationException("Could not find parent hub device");
			UsbHub usbhub = GetUsbDevice(parent) as UsbHub;
			if (usbhub == null) throw new InvalidOperationException("Could not find parent hub device");
			String driverkey = node.DriverKey;
			foreach (UsbDevice usbdev in usbhub.Devices) {
				if (driverkey.Equals(usbdev.DriverKey, StringComparison.InvariantCultureIgnoreCase)) return usbdev;
			}
			throw new InvalidOperationException("Could not find device on parent hub");
			//return null;
		}*/
	}
}