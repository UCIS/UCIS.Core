using System;

namespace UCIS.USBLib.Descriptor {
	public enum UsbDescriptorType : byte {
		/// <summary>
		/// Device descriptor type.
		/// </summary>
		Device = 1,
		/// <summary>
		/// Configuration descriptor type.
		/// </summary>
		Configuration = 2,
		/// <summary>
		/// String descriptor type.
		/// </summary>
		String = 3,
		/// <summary>
		/// Interface descriptor type.
		/// </summary>
		Interface = 4,
		/// <summary>
		/// Endpoint descriptor type.
		/// </summary>
		Endpoint = 5,
		/// <summary>
		/// Device Qualifier descriptor type.
		/// </summary>
		DeviceQualifier = 6,
		/// <summary>
		/// Other Speed Configuration descriptor type.
		/// </summary>
		OtherSpeedConfiguration = 7,
		/// <summary>
		/// Interface Power descriptor type.
		/// </summary>
		InterfacePower = 8,
		/// <summary>
		/// OTG descriptor type.
		/// </summary>
		OTG = 9,
		/// <summary>
		/// Debug descriptor type.
		/// </summary>
		Debug = 10,
		/// <summary>
		/// Interface Association descriptor type.
		/// </summary>
		InterfaceAssociation = 11,

		///<summary> HID descriptor</summary>
		Hid = 0x21,

		///<summary> HID report descriptor</summary>
		HidReport = 0x22,

		///<summary> Physical descriptor</summary>
		Physical = 0x23,

		///<summary> Hub descriptor</summary>
		Hub = 0x29
	}
	public enum UsbClassCode : byte {
		Unspecified = 0x00,
		Audio = 0x01,
		Communications = 0x02,
		HID = 0x03,
		PID = 0x05,
		Image = 0x06,
		Printer = 0x07,
		MassStorage = 0x08,
		Hub = 0x09,
		Data = 0x0A,
		SmartCard = 0x0B,
		ContentSecurity = 0x0D,
		Video = 0x0E,
		Healthcare = 0x0F,
		AV = 0x10,
		Diagnostic = 0xDC,
		Wireless = 0xE0,
		Miscellaneous = 0xEF,
		ApplicationSpecific = 0xFE,
		VendorSpecific = 0xFF
	}
}