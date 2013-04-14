using System;
using System.Runtime.InteropServices;

namespace UCIS.USBLib.Communication.LibUsb {
	static class UsbConstants {
		public const int DEFAULT_TIMEOUT = 1000;
	}
	[Flags]
	enum UsbEndpointDirection : byte {
		EndpointIn = 0x80,
		EndpointOut = 0x00,
	}
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct UsbKernelVersion {
		public bool IsEmpty { get { return Major == 0 && Minor == 0 && Micro == 0 && Nano == 0; } }
		public UsbKernelVersion(int major, int minor, int micro, int nano, int bcdLibUsbDotNetKernelMod) {
			Major = major;
			Minor = minor;
			Micro = micro;
			Nano = nano;
			BcdLibUsbDotNetKernelMod = bcdLibUsbDotNetKernelMod;
		}
		public readonly int Major;
		public readonly int Minor;
		public readonly int Micro;
		public readonly int Nano;
		public readonly int BcdLibUsbDotNetKernelMod;
		public override string ToString() { return string.Format("{0}.{1}.{2}.{3}", Major, Minor, Micro, Nano); }
	}
	[StructLayout(LayoutKind.Explicit, Pack = 1, Size = sizeof(int) * 6)]
	struct LibUsbRequest {
		public static int Size = Marshal.SizeOf(typeof(LibUsbRequest));
		[FieldOffset(0)]
		public int Timeout;
		[FieldOffset(sizeof(int))]
		public Control Control;
		[FieldOffset(sizeof(int))]
		public Config Config;
		[FieldOffset(sizeof(int))]
		public Debug Debug;
		[FieldOffset(sizeof(int))]
		public Descriptor Descriptor;
		[FieldOffset(sizeof(int))]
		public Endpoint Endpoint;
		[FieldOffset(sizeof(int))]
		public Feature Feature;
		[FieldOffset(sizeof(int))]
		public Iface Iface;
		[FieldOffset(sizeof(int))]
		public Status Status;
		[FieldOffset(sizeof(int))]
		public Vendor Vendor;
		[FieldOffset(sizeof(int))]
		public UsbKernelVersion Version;
		[FieldOffset(sizeof(int))]
		public DeviceProperty DeviceProperty;
		[FieldOffset(sizeof(int))]
		public DeviceRegKey DeviceRegKey;
		[FieldOffset(sizeof(int))]
		public BusQueryID BusQueryID;
	}
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct Descriptor {
		public int Type;
		public int Index;
		public int LangID;
		public int Recipient;
	}
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct Config {
		public int ID;
	}
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct Control {
		public byte RequestType;
		public byte Request;
		public ushort Value;
		public ushort Index;
		public ushort Length;
	}
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct DeviceProperty {
		public int ID;
	}
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct Iface {
		public int ID;
		public int AlternateID;
	}
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct Endpoint {
		public int ID;
		public int PacketSize;
	}
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct Vendor {
		public int Type;
		public int Recipient;
		public int Request;
		public int ID;
		public int Index;
	}
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct Feature {
		public int Recipient;
		public int ID;
		public int Index;
	}
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct Status {
		public int Recipient;
		public int Index;
		public int ID;
	}
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct Debug {
		public int Level;
	}
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct DeviceRegKey {
		public int KeyType;
		public int NameOffset;
		public int ValueOffset;
		public int ValueLength;
	}
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct BusQueryID {
		public ushort IDType;
	}
	static class LibUsbIoCtl {
		private const int FILE_ANY_ACCESS = 0;
		private const int FILE_DEVICE_UNKNOWN = 0x00000022;

		private const int METHOD_BUFFERED = 0;
		private const int METHOD_IN_DIRECT = 1;
		private const int METHOD_OUT_DIRECT = 2;
		private const int METHOD_NEITHER = 3;

		public static readonly int ABORT_ENDPOINT = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x80F, METHOD_BUFFERED, FILE_ANY_ACCESS);
		public static readonly int CLAIM_INTERFACE = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x815, METHOD_BUFFERED, FILE_ANY_ACCESS);
		public static readonly int CLEAR_FEATURE = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x806, METHOD_BUFFERED, FILE_ANY_ACCESS);
		public static readonly int CONTROL_TRANSFER = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x903, METHOD_BUFFERED, FILE_ANY_ACCESS);

		public static readonly int GET_CONFIGURATION = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x802, METHOD_BUFFERED, FILE_ANY_ACCESS);
		public static readonly int GET_CUSTOM_REG_PROPERTY = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x901, METHOD_BUFFERED, FILE_ANY_ACCESS);
		public static readonly int GET_DESCRIPTOR = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x809, METHOD_BUFFERED, FILE_ANY_ACCESS);
		public static readonly int GET_INTERFACE = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x804, METHOD_BUFFERED, FILE_ANY_ACCESS);
		public static readonly int GET_STATUS = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x807, METHOD_BUFFERED, FILE_ANY_ACCESS);
		public static readonly int GET_VERSION = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x812, METHOD_BUFFERED, FILE_ANY_ACCESS);
		public static readonly int GET_REG_PROPERTY = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x900, METHOD_BUFFERED, FILE_ANY_ACCESS);
		public static readonly int INTERRUPT_OR_BULK_READ = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x80B, METHOD_OUT_DIRECT, FILE_ANY_ACCESS);
		public static readonly int INTERRUPT_OR_BULK_WRITE = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x80A, METHOD_IN_DIRECT, FILE_ANY_ACCESS);
		public static readonly int ISOCHRONOUS_READ = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x814, METHOD_OUT_DIRECT, FILE_ANY_ACCESS);
		public static readonly int ISOCHRONOUS_WRITE = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x813, METHOD_IN_DIRECT, FILE_ANY_ACCESS);
		public static readonly int RELEASE_INTERFACE = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x816, METHOD_BUFFERED, FILE_ANY_ACCESS);
		public static readonly int RESET_DEVICE = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x810, METHOD_BUFFERED, FILE_ANY_ACCESS);
		public static readonly int RESET_ENDPOINT = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x80E, METHOD_BUFFERED, FILE_ANY_ACCESS);
		public static readonly int SET_CONFIGURATION = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x801, METHOD_BUFFERED, FILE_ANY_ACCESS);
		public static readonly int SET_DEBUG_LEVEL = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x811, METHOD_BUFFERED, FILE_ANY_ACCESS);
		public static readonly int SET_DESCRIPTOR = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x808, METHOD_BUFFERED, FILE_ANY_ACCESS);
		public static readonly int SET_FEATURE = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x805, METHOD_BUFFERED, FILE_ANY_ACCESS);
		public static readonly int SET_INTERFACE = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x803, METHOD_BUFFERED, FILE_ANY_ACCESS);
		public static readonly int VENDOR_READ = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x80D, METHOD_BUFFERED, FILE_ANY_ACCESS);
		public static readonly int VENDOR_WRITE = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x80C, METHOD_BUFFERED, FILE_ANY_ACCESS);

		private static int CTL_CODE(int DeviceType, int Function, int Method, int Access) { return ((DeviceType) << 16) | ((Access) << 14) | ((Function) << 2) | (Method); }
	}
}
