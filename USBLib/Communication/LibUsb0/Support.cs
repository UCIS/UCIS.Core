using System;
using System.Runtime.InteropServices;

namespace UCIS.USBLib.Communication.LibUsb {
	/// <summary> Transfers data to the main control endpoint (Endpoint 0).
	/// </summary> 
	/// <remarks> All USB devices respond to requests from the host on the device’s Default Control Pipe. These requests are made using control transfers. The request and the request’s parameters are sent to the device in the Setup packet. The host is responsible for establishing the values passed in the fields. Every Setup packet has eight bytes.
	/// </remarks> 
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct UsbSetupPacket {
		/// <summary>
		/// This bitmapped field identifies the characteristics of the specific request. In particular, this field identifies the direction of data transfer in the second phase of the control transfer. The state of the Direction bit is ignored if the wLength field is zero, signifying there is no Data stage.
		/// The USB Specification defines a series of standard requests that all devices must support. In addition, a device class may define additional requests. A device vendor may also define requests supported by the device.
		/// Requests may be directed to the device, an interface on the device, or a specific endpoint on a device. This field also specifies the intended recipient of the request. When an interface or endpoint is specified, the wIndex field identifies the interface or endpoint.
		/// </summary>
		/// <remarks>
		/// <ul>Characteristics of request:
		/// <li>D7: Data transfer direction</li>
		/// <li>0 = Host-to-device</li>
		/// <li>1 = Device-to-host</li>
		/// <li>D6...5: Type</li>
		/// <li>0 = Standard</li>
		/// <li>1 = Class</li>
		/// <li>2 = Vendor</li>
		/// <li>3 = Reserved</li>
		/// <li>D4...0: Recipient</li>
		/// <li>0 = Device</li>
		/// <li>1 = Interface</li>
		/// <li>2 = Endpoint</li>
		/// <li>3 = Other</li>
		/// <li>4...31 = Reserved</li>
		/// </ul>
		/// </remarks>
		public byte RequestType;

		/// <summary>
		/// This field specifies the particular request. The Type bits in the bmRequestType field modify the meaning of this field. This specification defines values for the bRequest field only when the bits are reset to zero, indicating a standard request.
		/// </summary>
		public byte Request;

		/// <summary>
		/// The contents of this field vary according to the request. It is used to pass a parameter to the device, specific to the request.
		/// </summary>
		public short Value;

		/// <summary>
		/// The contents of this field vary according to the request. It is used to pass a parameter to the device, specific to the request.
		/// </summary>
		public short Index;

		/// <summary>
		/// This field specifies the length of the data transferred during the second phase of the control transfer. The direction of data transfer (host-to-device or device-to-host) is indicated by the Direction bit of the <see cref="RequestType"/> field. If this field is zero, there is no data transfer phase. On an input request, a device must never return more data than is indicated by the wLength value; it may return less. On an output request, wLength will always indicate the exact amount of data to be sent by the host. Device behavior is undefined if the host should send more data than is specified in wLength.
		/// </summary>
		public short Length;

		/// <summary>
		/// Creates a new instance of a <see cref="UsbSetupPacket"/> and initializes all the fields with the following parameters.
		/// </summary>
		/// <param name="requestType">See <see cref="UsbSetupPacket.RequestType"/>.</param>
		/// <param name="request">See <see cref="UsbSetupPacket.Request"/>.</param>
		/// <param name="value">See <see cref="UsbSetupPacket.Value"/>.</param>
		/// <param name="index">See <see cref="UsbSetupPacket.Index"/>.</param>
		/// <param name="length">See <see cref="UsbSetupPacket.Length"/>.</param>
		public UsbSetupPacket(byte requestType, byte request, short value, short index, short length) {
			RequestType = requestType;
			Request = request;
			Value = value;
			Index = index;
			Length = length;
		}
	}
	/// <summary> Standard Windows registry properties for USB devices and other hardware.
	/// </summary> 
	/// DeviceRegistryProperty or DEVICE_REGISTRY_PROPERTY on MSDN
	enum DevicePropertyType {
		/// <summary>
		/// Requests a string describing the device, such as "Microsoft PS/2 Port Mouse", typically defined by the manufacturer. 
		/// </summary>
		DeviceDesc = 0,
		/// <summary>
		/// Requests the hardware IDs provided by the device that identify the device.
		/// </summary>
		HardwareId = 1,
		/// <summary>
		/// Requests the compatible IDs reported by the device.
		/// </summary>
		CompatibleIds = 2,
		/// <summary>
		/// Requests the name of the device's setup class, in text format. 
		/// </summary>
		Class = 5,
		/// <summary>
		/// Requests the GUID for the device's setup class.
		/// </summary>
		ClassGuid = 6,
		/// <summary>
		/// Requests the name of the driver-specific registry key.
		/// </summary>
		Driver = 7,
		/// <summary>
		/// Requests a string identifying the manufacturer of the device.
		/// </summary>
		Mfg = 8,
		/// <summary>
		/// Requests a string that can be used to distinguish between two similar devices, typically defined by the class installer.
		/// </summary>
		FriendlyName = 9,
		/// <summary>
		/// Requests information about the device's location on the bus; the interpretation of this information is bus-specific. 
		/// </summary>
		LocationInformation = 10,
		/// <summary>
		/// Requests the name of the PDO for this device.
		/// </summary>
		PhysicalDeviceObjectName = 11,
		/// <summary>
		/// Requests the GUID for the bus that the device is connected to.
		/// </summary>
		BusTypeGuid = 12,
		/// <summary>
		/// Requests the bus type, such as PCIBus or PCMCIABus.
		/// </summary>
		LegacyBusType = 13,
		/// <summary>
		/// Requests the legacy bus number of the bus the device is connected to. 
		/// </summary>
		BusNumber = 14,
		/// <summary>
		/// Requests the name of the enumerator for the device, such as "USB".
		/// </summary>
		EnumeratorName = 15,
		/// <summary>
		/// Requests the address of the device on the bus. 
		/// </summary>
		Address = 16,
		/// <summary>
		/// Requests a number associated with the device that can be displayed in the user interface.
		/// </summary>
		UiNumber = 17,
		/// <summary>
		/// Windows XP and later.) Requests the device's installation state.
		/// </summary>
		InstallState = 18,
		/// <summary>
		/// (Windows XP and later.) Requests the device's current removal policy. The operating system uses this value as a hint to determine how the device is normally removed.
		/// </summary>
		RemovalPolicy = 19
	}
	/// <summary> Various USB constants.
	/// </summary> 
	static class UsbConstants {
		/// <summary>
		/// Default timeout for all USB IO operations.
		/// </summary>
		public const int DEFAULT_TIMEOUT = 1000;

		/// <summary>
		/// Maximum size of a config descriptor.
		/// </summary>
		public const int MAX_CONFIG_SIZE = 4096;

		/// <summary>
		/// Maximum number of USB devices.
		/// </summary>
		public const int MAX_DEVICES = 128;

		/// <summary>
		/// Maximum number of endpoints per device.
		/// </summary>
		public const int MAX_ENDPOINTS = 32;

		/// <summary>
		/// Endpoint direction mask.
		/// </summary>
		public const byte ENDPOINT_DIR_MASK = 0x80;

		/// <summary>
		/// Endpoint number mask.
		/// </summary>
		public const byte ENDPOINT_NUMBER_MASK = 0xf;

	}
	///<summary>Endpoint direction.</summary>
	/// <seealso cref="UsbCtrlFlags"/>
	[Flags]
	enum UsbEndpointDirection : byte {
		/// <summary>
		/// In Direction
		/// </summary>
		EndpointIn = 0x80,
		/// <summary>
		/// Out Direction
		/// </summary>
		EndpointOut = 0x00,
	}
	///<summary>
	/// Contains version information for the LibUsb Sys driver.
	///</summary>
	/// <remarks>
	/// This version is not related to LibUsbDotNet.  TO get the LibUsbDotNet version use .NET reflections.
	/// </remarks>
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct UsbKernelVersion {
		/// <summary>
		/// True if Major == 0 and Minor == 0 and Micro == 0 and Nano == 0.
		/// </summary>
		public bool IsEmpty {
			get {
				if (Major == 0 && Minor == 0 && Micro == 0 && Nano == 0) return true;
				return false;
			}
		}

		internal UsbKernelVersion(int major, int minor, int micro, int nano, int bcdLibUsbDotNetKernelMod) {
			Major = major;
			Minor = minor;
			Micro = micro;
			Nano = nano;
			BcdLibUsbDotNetKernelMod = bcdLibUsbDotNetKernelMod;
		}

		/// <summary>
		/// LibUsb-Win32 Major version
		/// </summary>
		public readonly int Major;

		/// <summary>
		/// LibUsb-Win32 Minor version
		/// </summary>
		public readonly int Minor;

		/// <summary>
		/// LibUsb-Win32 Micro version
		/// </summary>
		public readonly int Micro;

		/// <summary>
		/// LibUsb-Win32 Nano version
		/// </summary>
		public readonly int Nano;

		/// <summary>
		/// The LibUsbDotNet - LibUsb-Win32 binary mod code. if not running the LibUsbDotNet LibUsb-Win32 modified kernel driver, this value is 0.
		/// </summary>
		public readonly int BcdLibUsbDotNetKernelMod;

		///<summary>
		///The full LibUsb-Win32 kernel driver version (libusb0.sys).
		///</summary>
		///
		///<returns>
		///A <see cref="System.String"/> containing the full LibUsb-Win32 version.
		///</returns>
		public override string ToString() { return string.Format("{0}.{1}.{2}.{3}", Major, Minor, Micro, Nano); }
	}
	[StructLayout(LayoutKind.Explicit, Pack = 1, Size = sizeof(int) * 6)]
	struct LibUsbRequest {
		public static int Size = Marshal.SizeOf(typeof(LibUsbRequest));
		[FieldOffset(0)]
		public int Timeout; // = UsbConstants.DEFAULT_TIMEOUT;

		#region Union Struct

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
		#endregion

		public Byte[] Bytes {
			get {
				Byte[] rtn = new byte[Size];

				for (int i = 0; i < Size; i++)
					rtn[i] = Marshal.ReadByte(this, i);

				return rtn;
			}
		}


		public void RequestConfigDescriptor(int index) {
			Timeout = UsbConstants.DEFAULT_TIMEOUT;

			int value = ((int)UsbDescriptorType.Configuration << 8) + index;

			Descriptor.Recipient = (byte)UsbEndpointDirection.EndpointIn & 0x1F;
			Descriptor.Type = (value >> 8) & 0xFF;
			Descriptor.Index = value & 0xFF;
			Descriptor.LangID = 0;
		}

		public void RequestStringDescriptor(int index, short langid) {
			Timeout = UsbConstants.DEFAULT_TIMEOUT;

			int value = ((int)UsbDescriptorType.String << 8) + index;

			Descriptor.Recipient = (byte)UsbEndpointDirection.EndpointIn & 0x1F;
			Descriptor.Type = value >> 8 & 0xFF;
			Descriptor.Index = value & 0xFF;
			Descriptor.LangID = langid;
		}
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
