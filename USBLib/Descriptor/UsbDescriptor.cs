using System;
using System.Runtime.InteropServices;
using System.Text;
using UCIS.USBLib.Communication;
using UCIS.Util;

namespace UCIS.USBLib.Descriptor {
	public struct UsbDescriptorBlob {
		public UsbDescriptorBlob(ArraySegment<Byte> data) : this() { this.Data = data; }
		public UsbDescriptorBlob(Byte[] data) : this() { this.Data = new ArraySegment<byte>(data); }
		public UsbDescriptorBlob(Byte[] data, int offset) : this() { this.Data = new ArraySegment<byte>(data, offset, data[offset + 0]); }
		public ArraySegment<Byte> Data { get; private set; }
		public Byte Length { get { return Data.Array[Data.Offset + 0]; } }
		public UsbDescriptorType Type { get { return (UsbDescriptorType)Data.Array[Data.Offset + 1]; } }
		public Byte[] GetBytes() { return ArrayUtil.ToArray(Data); }
		public static explicit operator UsbDescriptor(UsbDescriptorBlob self) { return UsbDescriptor.FromByteArray(self.Data.Array, self.Data.Offset, self.Data.Count); }
		public static explicit operator UsbDeviceDescriptor(UsbDescriptorBlob self) { return UsbDeviceDescriptor.FromByteArray(self.Data.Array, self.Data.Offset, self.Data.Count); }
		public static explicit operator UsbConfigurationDescriptor(UsbDescriptorBlob self) { return UsbConfigurationDescriptor.FromByteArray(self.Data.Array, self.Data.Offset, self.Data.Count); }
		public static explicit operator UsbInterfaceDescriptor(UsbDescriptorBlob self) { return UsbInterfaceDescriptor.FromByteArray(self.Data.Array, self.Data.Offset, self.Data.Count); }
		public static explicit operator UsbEndpointDescriptor(UsbDescriptorBlob self) { return UsbEndpointDescriptor.FromByteArray(self.Data.Array, self.Data.Offset, self.Data.Count); }
		public static explicit operator UsbHidDescriptor(UsbDescriptorBlob self) { return UsbHidDescriptor.FromByteArray(self.Data.Array, self.Data.Offset, self.Data.Count); }
		public static explicit operator UsbHubDescriptor(UsbDescriptorBlob self) { return UsbHubDescriptor.FromByteArray(self.Data.Array, self.Data.Offset, self.Data.Count); }
	}
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct UsbDescriptor {
		byte bmLength;
		byte bType;
		public Byte Length { get { return bmLength; } }
		public UsbDescriptorType Type { get { return (UsbDescriptorType)bType; } }
		internal static short FromLittleEndian(short value) {
			if (BitConverter.IsLittleEndian) return value;
			return (short)(((value & 0xFF) << 8) | ((value >> 8) & 0xFF));
		}
		public unsafe static UsbDescriptor FromByteArray(Byte[] buffer, int offset, int length) {
			if (length < Marshal.SizeOf(typeof(UsbDescriptor))) throw new ArgumentOutOfRangeException("length", "The data length is smaller than the descriptor length");
			if (offset < 0 || length < 0 || offset + length > buffer.Length) throw new ArgumentOutOfRangeException("length", "The specified offset and length exceed the buffer dimensions");
			fixed (Byte* ptr = buffer) return *(UsbDescriptor*)(ptr + offset);
		}
	}
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct UsbStringDescriptor {
		public static String GetString(Byte[] buffer, int offset, int length) {
			if (length < 2) throw new ArgumentOutOfRangeException("length", "The data length is smaller than the descriptor length");
			if (offset < 0 || length < 0 || offset + length > buffer.Length) throw new ArgumentOutOfRangeException("length", "The specified offset and length exceed the buffer dimensions");
			if (buffer[offset + 1] != (Byte)UsbDescriptorType.String) throw new InvalidOperationException("The descriptor is not a string descriptor");
			int slen = buffer[offset];
			if (slen > length) throw new InvalidOperationException("The string has been truncated");
			return Encoding.Unicode.GetString(buffer, offset + 2, slen - 2);
		}
		public static String GetStringFromDevice(IUsbInterface device, byte index, short langId) {
			Byte[] buff = new Byte[255];
			int len = device.GetDescriptor((Byte)UsbDescriptorType.String, index, langId, buff, 0, buff.Length);
			if (len == 0) return null;
			return GetString(buff, 0, len);
		}
	}
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct UsbDeviceDescriptor {
		byte bmLength;
		byte bType;
		short bcdUSB;
		byte bDeviceClass;
		byte bDeviceSubClass;
		byte bDeviceProtocol;
		byte bMaxControlPacketSize;
		short idVendor;
		short idProduct;
		short bcdDevice;
		byte iManufacturer;
		byte iProduct;
		byte iSerialNumber;
		byte numConfigurations;
		public Byte Length { get { return bmLength; } }
		public UsbDescriptorType Type { get { return (UsbDescriptorType)bType; }}
		public short USBVersion { get { return UsbDescriptor.FromLittleEndian(bcdUSB); } }
		public UsbClassCode DeviceClass { get { return (UsbClassCode)bDeviceClass; } }
		public Byte DeviceSubClass { get { return bDeviceSubClass; } }
		public Byte DeviceProtocol { get { return bDeviceProtocol; } }
		public UInt16 DeviceVersion { get { return (UInt16)UsbDescriptor.FromLittleEndian(bcdDevice); } }
		public Byte MaxControlPacketSize { get { return bMaxControlPacketSize; } }
		public UInt16 VendorID { get { return (UInt16)UsbDescriptor.FromLittleEndian(idVendor); } }
		public UInt16 ProductID { get { return (UInt16)UsbDescriptor.FromLittleEndian(idProduct); } }
		public Byte ManufacturerStringID { get { return iManufacturer; } }
		public Byte ProductStringID { get { return iProduct; } }
		public Byte SerialNumberStringID { get { return iSerialNumber; } }
		public Byte NumConfigurations { get { return numConfigurations; } }
		public unsafe Byte[] GetBytes() {
			Byte[] buffer = new Byte[Size];
			fixed (Byte* ptr = buffer) *(UsbDeviceDescriptor*)ptr = this;
			return buffer;
		}
		public unsafe static UsbDeviceDescriptor FromByteArray(Byte[] buffer, int offset, int length) {
			if (length < Size) throw new ArgumentOutOfRangeException("length", "The data length is smaller than the descriptor length");
			if (offset < 0 || length < 0 || offset + length > buffer.Length) throw new ArgumentOutOfRangeException("length", "The specified offset and length exceed the buffer dimensions");
			fixed (Byte* ptr = buffer) return *(UsbDeviceDescriptor*)(ptr + offset);
		}
		public static UsbDeviceDescriptor FromDevice(IUsbInterface device) {
			Byte[] buff = new Byte[Size];
			int len = device.GetDescriptor((Byte)UsbDescriptorType.Device, 0, 0, buff, 0, buff.Length);
			if (len == 0) return new UsbDeviceDescriptor();
			return FromByteArray(buff, 0, len);
		}
		public static unsafe int Size { get { return sizeof(UsbDeviceDescriptor); } }
	}
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct UsbConfigurationDescriptor {
		byte bmLength;
		byte bType;
		short wTotalLength;
		byte bNumInterfaces;
		byte bConfigurationValue;
		byte bConfigurationStringID;
		byte bmAttributes;
		byte bMaxPower;
		public Byte Length { get { return bmLength; } }
		public UsbDescriptorType Type { get { return (UsbDescriptorType)bType; } }
		public short TotalLength { get { return UsbDescriptor.FromLittleEndian(wTotalLength); } }
		public Byte NumInterfaces { get { return bNumInterfaces; } }
		public Byte ConfigurationValue { get { return bConfigurationValue; } }
		public Byte ConfigurationStringID { get { return bConfigurationStringID; } }
		public Boolean SelfPowered { get { return 0 != (bmAttributes & (1 << 6)); } }
		public Boolean RemoteWakeup { get { return 0 != (bmAttributes & (1 << 5)); } }
		public int MaxPowerMA { get { return bMaxPower * 2; } }
		public unsafe static UsbConfigurationDescriptor FromByteArray(Byte[] buffer, int offset, int length) {
			if (length < Size) throw new ArgumentOutOfRangeException("length", "The data length is smaller than the descriptor length");
			if (offset < 0 || length < 0 || offset + length > buffer.Length) throw new ArgumentOutOfRangeException("length", "The specified offset and length exceed the buffer dimensions");
			fixed (Byte* ptr = buffer) return *(UsbConfigurationDescriptor*)(ptr + offset);
		}
		public static UsbConfigurationDescriptor FromDevice(IUsbInterface device, Byte index) {
			Byte[] buff = new Byte[UsbConfigurationDescriptor.Size];
			int len = device.GetDescriptor((Byte)UsbDescriptorType.Configuration, index, 0, buff, 0, buff.Length);
			if (len == 0) return new UsbConfigurationDescriptor();
			return FromByteArray(buff, 0, len);
		}
		public static unsafe int Size { get { return sizeof(UsbConfigurationDescriptor); } }
	}
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct UsbInterfaceDescriptor {
		byte bmLength;
		byte bType;
		byte bInterfaceNumber;
		byte bAlternateSetting;
		byte bNumEndpoints;
		byte bInterfaceClass;
		byte bInterfaceSubClass;
		byte bInterfaceProtocol;
		byte bInterfaceStringID;
		public Byte Length { get { return bmLength; } }
		public UsbDescriptorType Type { get { return (UsbDescriptorType)bType; } }
		public Byte InterfaceNumber { get { return bInterfaceNumber; } }
		public Byte AlternateSetting { get { return bAlternateSetting; } }
		public Byte NumEndpoints { get { return bNumEndpoints; } }
		public UsbClassCode InterfaceClass { get { return (UsbClassCode)bInterfaceClass; } }
		public Byte InterfaceSubClass { get { return bInterfaceSubClass; } }
		public Byte InterfaceProtocol { get { return bInterfaceProtocol; } }
		public Byte InterfaceStringID { get { return bInterfaceStringID; } }
		public unsafe static UsbInterfaceDescriptor FromByteArray(Byte[] buffer, int offset, int length) {
			if (length < Size) throw new ArgumentOutOfRangeException("length", "The data length is smaller than the descriptor length");
			if (offset < 0 || length < 0 || offset + length > buffer.Length) throw new ArgumentOutOfRangeException("length", "The specified offset and length exceed the buffer dimensions");
			fixed (Byte* ptr = buffer) return *(UsbInterfaceDescriptor*)(ptr + offset);
		}
		public static unsafe int Size { get { return sizeof(UsbInterfaceDescriptor); } }
	}
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct UsbEndpointDescriptor {
		byte bmLength;
		byte bType;
		byte bEndpointAddress;
		Byte bmAttributes;
		short wMaxPacketSize;
		byte bInterval;
		public Byte Length { get { return bmLength; } }
		public UsbDescriptorType Type { get { return (UsbDescriptorType)bType; } }
		public Byte EndpointAddress { get { return bEndpointAddress; } }
		public Boolean IsInput { get { return 0 != (EndpointAddress & (1 << 7)); } }
		public int EndpointNumber { get { return EndpointAddress & 0xF; } }
		public int TransferType { get { return bmAttributes & 3; } }
		public Boolean IsControl { get { return TransferType == 0; } }
		public Boolean IsIsochronous { get { return TransferType == 1; } }
		public Boolean IsBulk { get { return TransferType == 2; } }
		public Boolean IsInterrupt { get { return TransferType == 3; } }
		public int MaxPacketSize { get { return UsbDescriptor.FromLittleEndian(wMaxPacketSize) & 0x7FF; } }
		public Byte Interval { get { return bInterval; } }
		public unsafe static UsbEndpointDescriptor FromByteArray(Byte[] buffer, int offset, int length) {
			if (length < Size) throw new ArgumentOutOfRangeException("length", "The data length is smaller than the descriptor length");
			if (offset < 0 || length < 0 || offset + length > buffer.Length) throw new ArgumentOutOfRangeException("length", "The specified offset and length exceed the buffer dimensions");
			fixed (Byte* ptr = buffer) return *(UsbEndpointDescriptor*)(ptr + offset);
		}
		public static unsafe int Size { get { return sizeof(UsbEndpointDescriptor); } }
	}
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct UsbHidDescriptor {
		byte bmLength;
		byte bType;
		short bcdHID;
		byte bCountryCode;
		byte bNumDescriptors;
		byte bDescriptorType;
		short wDescriptorLength;
		public Byte Length { get { return bmLength; } }
		public UsbDescriptorType Type { get { return (UsbDescriptorType)bType; } }
		public short HIDVersion { get { return UsbDescriptor.FromLittleEndian(bcdHID); } }
		public Byte CountryCode { get { return bCountryCode; } }
		public Byte NumDescriptors { get { return bNumDescriptors; } }
		public UsbDescriptorType DescriptorType { get { return (UsbDescriptorType)bDescriptorType; } }
		public short DescriptorLength { get { return UsbDescriptor.FromLittleEndian(wDescriptorLength); } }
		public unsafe static UsbHidDescriptor FromByteArray(Byte[] buffer, int offset, int length) {
			if (length < Size) throw new ArgumentOutOfRangeException("length", "The data length is smaller than the descriptor length");
			if (offset < 0 || length < 0 || offset + length > buffer.Length) throw new ArgumentOutOfRangeException("length", "The specified offset and length exceed the buffer dimensions");
			fixed (Byte* ptr = buffer) return *(UsbHidDescriptor*)(ptr + offset);
		}
		public static unsafe int Size { get { return sizeof(UsbHidDescriptor); } }
	}
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct UsbHubDescriptor {
		byte bmLength;
		byte bType;
		byte bNumPorts;
		short wHubCharacteristics;
		byte bPwrOn2PwrGood; //2ms intervals
		byte bHubControllerCurrent;
		public Byte Length { get { return bmLength; } }
		public UsbDescriptorType Type { get { return (UsbDescriptorType)bType; } }
		public Byte NumPorts { get { return bNumPorts; } }
		public Boolean IsCompoundDevice { get { return 0 != (wHubCharacteristics & (1 << 2)); } }
		public Byte HubControllerCurrent { get { return bHubControllerCurrent; } }
		public unsafe static UsbHubDescriptor FromByteArray(Byte[] buffer, int offset, int length) {
			if (length < Marshal.SizeOf(typeof(UsbHubDescriptor))) throw new ArgumentOutOfRangeException("length", "The data length is smaller than the descriptor length");
			if (offset < 0 || length < 0 || offset + length > buffer.Length) throw new ArgumentOutOfRangeException("length", "The specified offset and length exceed the buffer dimensions");
			fixed (Byte* ptr = buffer) return *(UsbHubDescriptor*)(ptr + offset);
		}
		public static unsafe int Size { get { return sizeof(UsbHubDescriptor); } }
	}
}
