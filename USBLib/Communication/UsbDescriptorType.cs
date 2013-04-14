using System;

namespace UCIS.USBLib.Communication {
	[Flags]
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
}