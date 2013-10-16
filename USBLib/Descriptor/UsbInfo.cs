using System;
using System.Collections.Generic;
using System.Text;
using UCIS.USBLib.Communication;
using UCIS.Util;

namespace UCIS.USBLib.Descriptor {
	public class UsbDeviceInfo {
		private UsbDeviceDescriptor mDeviceDescriptor;
		private Boolean mHasDeviceDescriptor = false;
		private UsbConfigurationInfo[] mConfigurations = null;
		private short language = 0;
		public IUsbInterface Device { get; private set; }
		public UsbDeviceInfo(IUsbInterface device) {
			if (device == null) throw new ArgumentNullException("device");
			this.Device = device;
		}
		private void GetDescriptor() {
			if (mHasDeviceDescriptor) return;
			mDeviceDescriptor = UsbDeviceDescriptor.FromDevice(Device);
			mHasDeviceDescriptor = (mDeviceDescriptor.Length >= UsbDeviceDescriptor.Size && mDeviceDescriptor.Type == UsbDescriptorType.Device);
		}
		public String GetString(Byte index, short langId) {
			if (index == 0) return null;
			return UsbStringDescriptor.GetStringFromDevice(Device, index, langId);
		}
		public String GetString(Byte index) {
			if (language == 0) {
				Byte[] buff = new Byte[4];
				int len = Device.GetDescriptor((Byte)UsbDescriptorType.String, 0, 0, buff, 0, buff.Length);
				if (len >= 4) language = BitConverter.ToInt16(buff, 2);
			}
			return GetString(index, language);
		}
		public UsbDeviceDescriptor Descriptor { get { GetDescriptor(); return mDeviceDescriptor; } }
		public String ManufacturerString { get { return GetString(Descriptor.ManufacturerStringID); } }
		public String ProductString { get { return GetString(Descriptor.ProductStringID); } }
		public String SerialString { get { return GetString(Descriptor.SerialNumberStringID); } }
		public IList<UsbConfigurationInfo> Configurations {
			get {
				if (mConfigurations == null) {
					mConfigurations = new UsbConfigurationInfo[Descriptor.NumConfigurations];
					for (Byte i = 0; i < mConfigurations.Length; i++) mConfigurations[i] = new UsbConfigurationInfo(this, i);
				}
				return mConfigurations;
			}
		}
		public UsbConfigurationInfo FindConfiguration(Byte value) {
			foreach (UsbConfigurationInfo configuration in Configurations) if (configuration.Descriptor.ConfigurationValue == value) return configuration;
			return null;
		}
		public static UsbDeviceInfo FromDevice(IUsbInterface device) {
			return new UsbDeviceInfo(device);
		}
		public static UsbDeviceInfo FromDeviceNode(UCIS.HWLib.Windows.Devices.DeviceNode devnode) {
			IUsbInterface device = UCIS.HWLib.Windows.USB.UsbDevice.GetUsbDevice(devnode);
			if (device == null) return null;
			return new UsbDeviceInfo(device);
		}
	}
	public class UsbConfigurationInfo {
		private UsbConfigurationDescriptor mConfigurationDescriptor;
		private Boolean mHasConfigurationDescriptor = false;
		private Byte[] mConfigurationBlob = null;
		private UsbDescriptorBlob[] mDescriptors = null;
		private UsbInterfaceInfo[] mInterfaces = null;
		public UsbDeviceInfo Device { get; private set; }
		public Byte Index { get; private set; }
		internal UsbConfigurationInfo(UsbDeviceInfo device, Byte index) {
			this.Device = device;
			this.Index = index;
		}
		private void GetConfigurationBlob() {
			if (mConfigurationBlob != null) return;
			int length = Descriptor.TotalLength;
			if (!mHasConfigurationDescriptor) throw new Exception("Configuration descriptor is invalid");
			Byte[] blob = new Byte[length];
			length = Device.Device.GetDescriptor((Byte)UsbDescriptorType.Configuration, Index, 0, blob, 0, length);
			if (length != blob.Length) throw new Exception("Could not read configuration descriptor");
			List<UsbDescriptorBlob> descriptors = new List<UsbDescriptorBlob>();
			for (int offset = 0; offset < length; ) {
				if (length - offset < 2) throw new Exception("Descriptor has been truncated");
				UsbDescriptorBlob descriptor = new UsbDescriptorBlob(blob, offset);
				if (length - offset < descriptor.Length) throw new Exception("Descriptor has been truncated");
				descriptors.Add(descriptor);
				offset += descriptor.Length;
			}
			mConfigurationBlob = blob;
			mDescriptors = descriptors.ToArray();
		}
		public UsbConfigurationDescriptor Descriptor {
			get {
				if (!mHasConfigurationDescriptor) {
					mConfigurationDescriptor = UsbConfigurationDescriptor.FromDevice(Device.Device, Index);
					mHasConfigurationDescriptor = (mConfigurationDescriptor.Length >= UsbConfigurationDescriptor.Size && mConfigurationDescriptor.Type == UsbDescriptorType.Configuration);
				}
				return mConfigurationDescriptor;
			}
		}
		public IList<UsbDescriptorBlob> Descriptors { get { GetConfigurationBlob(); return mDescriptors; } }
		private void GetInterfaces() {
			if (mInterfaces != null) return;
			GetConfigurationBlob();
			UsbInterfaceInfo[] interfaces = new UsbInterfaceInfo[Descriptor.NumInterfaces];
			int index = 0;
			int first = -1;
			for (int i = 0; i < mDescriptors.Length; i++) {
				UsbDescriptorBlob descriptor = mDescriptors[i];
				if (descriptor.Type != UsbDescriptorType.Interface) continue;
				if (first != -1) {
					if (index >= interfaces.Length) Array.Resize(ref interfaces, index + 1);
					interfaces[index] = new UsbInterfaceInfo(this, ArrayUtil.Slice(mDescriptors, first, i - first));
					index++;
				}
				first = i;
			}
			if (first != -1) {
				if (index >= interfaces.Length) Array.Resize(ref interfaces, index + 1);
				interfaces[index] = new UsbInterfaceInfo(this, ArrayUtil.Slice(mDescriptors, first, mDescriptors.Length - first));
			}
			mInterfaces = interfaces;
		}
		public IList<UsbInterfaceInfo> Interfaces { get { GetInterfaces(); return mInterfaces; } }
		public UsbInterfaceInfo FindInterface(Predicate<UsbInterfaceInfo> predicate) {
			GetInterfaces();
			foreach (UsbInterfaceInfo interf in mInterfaces) if (predicate(interf)) return interf;
			return null;
		}
		public UsbInterfaceInfo FindInterface(Byte number) {
			return FindInterface(number, 0);
		}
		public UsbInterfaceInfo FindInterface(Byte number, Byte alternateSetting) {
			return FindInterface(delegate(UsbInterfaceInfo interf) { return interf.Descriptor.InterfaceNumber == number && interf.Descriptor.AlternateSetting == alternateSetting; });
		}
		public UsbInterfaceInfo FindInterfaceByClass(UsbClassCode usbclass) {
			return FindInterface(delegate(UsbInterfaceInfo interf) { return interf.Descriptor.InterfaceClass == usbclass; });
		}
		public UsbInterfaceInfo FindInterfaceByClass(UsbClassCode usbclass, Byte subclass) {
			return FindInterface(delegate(UsbInterfaceInfo interf) { return interf.Descriptor.InterfaceClass == usbclass && interf.Descriptor.InterfaceSubClass == subclass; });
		}
		public UsbDescriptorBlob FindDescriptor(UsbDescriptorType type) {
			GetConfigurationBlob();
			return Array.Find(mDescriptors, delegate(UsbDescriptorBlob descriptor) { return descriptor.Type == type; });
		}
		public UsbDescriptorBlob[] FindDescriptors(UsbDescriptorType type) {
			GetConfigurationBlob();
			return Array.FindAll(mDescriptors, delegate(UsbDescriptorBlob descriptor) { return descriptor.Type == type; });
		}
	}
	public class UsbInterfaceInfo {
		public UsbConfigurationInfo Configuration { get; private set; }
		private UsbDescriptorBlob[] mDescriptors;
		public UsbInterfaceInfo(UsbConfigurationInfo configuration, UsbDescriptorBlob[] descriptors) {
			this.Configuration = configuration;
			this.mDescriptors = descriptors;
		}
		public UsbInterfaceDescriptor Descriptor { get { return (UsbInterfaceDescriptor)mDescriptors[0]; } }
		public IList<UsbEndpointDescriptor> Endpoints {
			get {
				return Array.ConvertAll(FindDescriptors(UsbDescriptorType.Endpoint), delegate(UsbDescriptorBlob descriptor) { return (UsbEndpointDescriptor)descriptor; });
			}
		}
		public UsbEndpointDescriptor FindEndpoint(Predicate<UsbEndpointDescriptor> predicate) {
			foreach (UsbDescriptorBlob descriptor in mDescriptors) {
				if (descriptor.Type != UsbDescriptorType.Endpoint) continue;
				UsbEndpointDescriptor ep = (UsbEndpointDescriptor)descriptor;
				if (predicate(ep)) return ep;
			}
			return default(UsbEndpointDescriptor);
		}
		public UsbEndpointDescriptor FindEndpoint(Byte address) {
			return FindEndpoint(delegate(UsbEndpointDescriptor ep) { return ep.EndpointAddress == address; });
		}
		public UsbEndpointDescriptor FindEndpoint(Boolean input) {
			return FindEndpoint(delegate(UsbEndpointDescriptor ep) { return ep.IsInput == input; });
		}
		public UsbEndpointDescriptor FindEndpoint(Boolean input, int transferType) {
			return FindEndpoint(delegate(UsbEndpointDescriptor ep) { return ep.TransferType == transferType && ep.IsInput == input; });
		}
		public UsbDescriptorBlob FindDescriptor(UsbDescriptorType type) {
			return Array.Find(mDescriptors, delegate(UsbDescriptorBlob descriptor) { return descriptor.Type == type; });
		}
		public UsbDescriptorBlob[] FindDescriptors(UsbDescriptorType type) {
			return Array.FindAll(mDescriptors, delegate(UsbDescriptorBlob descriptor) { return descriptor.Type == type; });
		}
	}
}
