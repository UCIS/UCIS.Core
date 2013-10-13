using System;
using System.Runtime.InteropServices;
using UCIS.USBLib.Communication;
using UCIS.USBLib.Communication.WinUsb;
using UCIS.USBLib.Descriptor;

namespace UCIS.USBLib.Internal.Windows {
	class UsbApi {
		public const int IOCTL_GET_HCD_DRIVERKEY_NAME = 0x220424;
		public const int IOCTL_USB_GET_ROOT_HUB_NAME = 0x220408;
		public const int IOCTL_USB_GET_NODE_INFORMATION = 0x220408;
		public const int IOCTL_USB_GET_NODE_CONNECTION_INFORMATION_EX = 0x220448;
		public const int IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION = 0x220410;
		public const int IOCTL_USB_GET_NODE_CONNECTION_NAME = 0x220414;
		public const int IOCTL_USB_GET_NODE_CONNECTION_DRIVERKEY_NAME = 0x220420;
		public const int IOCTL_STORAGE_GET_DEVICE_NUMBER = 0x2D1080;

		public const string GUID_DEVINTERFACE_USB_HOST_CONTROLLER = "{3ABF6F2D-71C4-462A-8A92-1E6861E6AF27}";
		public const string GUID_DEVINTERFACE_USB_HUB = "{F18A0E88-C30C-11D0-8815-00A0C906BED8}";
		public const string GUID_DEVINTERFACE_USB_DEVICE = "{A5DCBF10-6530-11D2-901F-00C04FB951ED}";

		public const int MAX_BUFFER_SIZE = 2048;
		public const int DIF_PROPERTYCHANGE = 0x00000012;
		public const int DICS_FLAG_GLOBAL = 0x00000001;

		public const int DICS_ENABLE = 0x00000001;
		public const int DICS_DISABLE = 0x00000002;
	}

	enum UsbDeviceClass : byte {
		UnspecifiedDevice = 0x00,
		AudioInterface = 0x01,
		CommunicationsAndCDCControlBoth = 0x02,
		HIDInterface = 0x03,
		PhysicalInterfaceDevice = 0x5,
		ImageInterface = 0x06,
		PrinterInterface = 0x07,
		MassStorageInterface = 0x08,
		HubDevice = 0x09,
		CDCDataInterface = 0x0A,
		SmartCardInterface = 0x0B,
		ContentSecurityInterface = 0x0D,
		VidioInterface = 0x0E,
		PersonalHeathcareInterface = 0x0F,
		DiagnosticDeviceBoth = 0xDC,
		WirelessControllerInterface = 0xE0,
		MiscellaneousBoth = 0xEF,
		ApplicationSpecificInterface = 0xFE,
		VendorSpecificBoth = 0xFF
	}

	enum HubCharacteristics : byte {
		GangedPowerSwitching = 0x00,
		IndividualPotPowerSwitching = 0x01,
	}

	enum USB_CONNECTION_STATUS : int {
		NoDeviceConnected,
		DeviceConnected,
		DeviceFailedEnumeration,
		DeviceGeneralFailure,
		DeviceCausedOvercurrent,
		DeviceNotEnoughPower,
		DeviceNotEnoughBandwidth,
		DeviceHubNestedTooDeeply,
		DeviceInLegacyHub
	}

	enum USB_DEVICE_SPEED : byte {
		UsbLowSpeed = 0,
		UsbFullSpeed,
		UsbHighSpeed
	}

	[Flags]
	enum DeviceInterfaceDataFlags : uint {
		Unknown = 0x00000000,
		Active = 0x00000001,
		Default = 0x00000002,
		Removed = 0x00000004
	}

	[Flags]
	enum HubPortStatus : short {
		Connection = 0x0001,
		Enabled = 0x0002,
		Suspend = 0x0004,
		OverCurrent = 0x0008,
		BeingReset = 0x0010,
		Power = 0x0100,
		LowSpeed = 0x0200,
		HighSpeed = 0x0400,
		TestMode = 0x0800,
		Indicator = 0x1000,
	}

	enum HubStatus : byte {
		LocalPowerStatus = 1,
		OverCurrentIndicator = 2,
		LocalPowerStatusChange = 1,
		OverCurrentIndicatorChange = 2
	}

	enum PortIndicatorSlectors : byte {
		IndicatorAutomatic = 0,
		IndicatorAmber,
		IndicatorGreen,
		IndicatorOff
	}

	enum PowerSwitching : byte {
		SupportsGangPower = 0,
		SupportsIndividualPortPower = 1,
		SetPowerOff = 0,
		SetPowerOn = 1
	}

	[StructLayout(LayoutKind.Sequential)]
	struct SP_CLASSINSTALL_HEADER {
		public int cbSize;
		public int InstallFunction; //DI_FUNCTION  InstallFunction;
	}

	[StructLayout(LayoutKind.Sequential)]
	struct SP_PROPCHANGE_PARAMS {
		public SP_CLASSINSTALL_HEADER ClassInstallHeader;
		public int StateChange;
		public int Scope;
		public int HwProfile;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	struct USB_HCD_DRIVERKEY_NAME {
		public UInt32 ActualLength;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = UsbApi.MAX_BUFFER_SIZE)]
		public String DriverKeyName;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	struct USB_ROOT_HUB_NAME {
		public UInt32 ActualLength;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = UsbApi.MAX_BUFFER_SIZE)]
		public String RootHubName;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct USB_HUB_DESCRIPTOR {
		public byte bDescriptorLength;
		public UsbDescriptorType bDescriptorType;
		public byte bNumberOfPorts;
		public ushort wHubCharacteristics;
		public byte bPowerOnToPowerGood;
		public byte bHubControlCurrent;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
		public byte[] bRemoveAndPowerMask; //UCHAR  bRemoveAndPowerMask[64];
	}

	[StructLayout(LayoutKind.Sequential)]
	struct USB_HUB_INFORMATION {
		public USB_HUB_DESCRIPTOR HubDescriptor;
		public bool HubIsBusPowered;
	}

	[StructLayout(LayoutKind.Sequential)]
	struct USB_NODE_INFORMATION {
		public int NodeType;
		public USB_HUB_INFORMATION HubInformation;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct USB_NODE_CONNECTION_INFORMATION_EX {
		public uint ConnectionIndex;
		public UsbDeviceDescriptor DeviceDescriptor;
		public byte CurrentConfigurationValue;
		public USB_DEVICE_SPEED Speed;
		public byte DeviceIsHub; //BOOLEAN  DeviceIsHub;
		public ushort DeviceAddress;
		public uint NumberOfOpenPipes;
		public USB_CONNECTION_STATUS ConnectionStatus;
		//public IntPtr PipeList; //USB_PIPE_INFO  PipeList[0];
		//[MarshalAs(UnmanagedType.ByValArray, SizeConst=100)]
		//Byte[] PipeList;
	}

	[StructLayout(LayoutKind.Sequential)]
	struct HID_DESCRIPTOR_DESC_LIST {
		public byte bReportType;
		public short wReportLength;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct HID_DESCRIPTOR {
		public byte bLength;
		public UsbDescriptorType bDescriptorType;
		public ushort bcdHID;
		public byte bCountry;
		public byte bNumDescriptors;
		public HID_DESCRIPTOR_DESC_LIST hid_desclist; //DescriptorList [1];
	}

	[StructLayout(LayoutKind.Sequential, Pack = 8)]
	struct USB_DESCRIPTOR_REQUEST {
		public uint ConnectionIndex;
		public UsbSetupPacket SetupPacket;
		//public byte[] Data; //UCHAR  Data[0];
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	struct USB_NODE_CONNECTION_NAME {
		public uint ConnectionIndex;
		public uint ActualLength;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = UsbApi.MAX_BUFFER_SIZE)]
		public string NodeName;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	struct USB_NODE_CONNECTION_DRIVERKEY_NAME {
		public uint ConnectionIndex;
		public uint ActualLength;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = UsbApi.MAX_BUFFER_SIZE)]
		public string DriverKeyName;
	}

	[StructLayout(LayoutKind.Sequential)]
	struct STORAGE_DEVICE_NUMBER {
		public int DeviceType; //DEVICE_TYPE  DeviceType;
		public uint DeviceNumber;
		public uint PartitionNumber;
	}
}
