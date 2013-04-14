using System;

namespace UCIS.USBLib.Communication {
	[Flags]
	public enum UsbControlRequestType : byte {
		/// <summary>
		/// Class specific request.
		/// </summary>
		TypeClass = (0x01 << 5),
		/// <summary>
		/// RESERVED.
		/// </summary>
		TypeReserved = (0x03 << 5),
		/// <summary>
		/// Standard request.
		/// </summary>
		TypeStandard = (0x00 << 5),
		/// <summary>
		/// Vendor specific request.
		/// </summary>
		TypeVendor = (0x02 << 5),

		TypeMask = 0x03 << 5,

		/// <summary>
		/// Device is recipient.
		/// </summary>
		RecipDevice = 0x00,
		/// <summary>
		/// Endpoint is recipient.
		/// </summary>
		RecipEndpoint = 0x02,
		/// <summary>
		/// Interface is recipient.
		/// </summary>
		RecipInterface = 0x01,
		/// <summary>
		/// Other is recipient.
		/// </summary>
		RecipOther = 0x03,

		RecipMask = 0x03,

		/// <summary>
		/// In Direction
		/// </summary>
		EndpointIn = 0x80,
		/// <summary>
		/// Out Direction
		/// </summary>
		EndpointOut = 0x00,

		EndpointMask = 0x80,
	}

	[Flags]
	public enum UsbStandardRequest : byte {
		/// <summary>
		/// Clear or disable a specific feature.
		/// </summary>
		ClearFeature = 0x01,
		/// <summary>
		/// Returns the current device Configuration value.
		/// </summary>
		GetConfiguration = 0x08,
		/// <summary>
		/// Returns the specified descriptor if the descriptor exists.
		/// </summary>
		GetDescriptor = 0x06,
		/// <summary>
		/// Returns the selected alternate setting for the specified interface.
		/// </summary>
		GetInterface = 0x0A,
		/// <summary>
		/// Returns status for the specified recipient.
		/// </summary>
		GetStatus = 0x00,
		/// <summary>
		/// Sets the device address for all future device accesses.
		/// </summary>
		SetAddress = 0x05,
		/// <summary>
		/// Sets the device Configuration.
		/// </summary>
		SetConfiguration = 0x09,
		/// <summary>
		/// Optional and may be used to update existing descriptors or new descriptors may be added.
		/// </summary>
		SetDescriptor = 0x07,
		/// <summary>
		/// used to set or enable a specific feature.
		/// </summary>
		SetFeature = 0x03,
		/// <summary>
		/// Allows the host to select an alternate setting for the specified interface.
		/// </summary>
		SetInterface = 0x0B,
		/// <summary>
		/// Used to set and then report an endpoint’s synchronization frame.
		/// </summary>
		SynchFrame = 0x0C,
	}
}
