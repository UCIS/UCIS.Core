using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using ssize_t = System.IntPtr;

namespace UCIS.USBLib.Communication.LibUsb1 {
	class libusb_context : SafeHandleZeroOrMinusOneIsInvalid {
		public libusb_context() : base(true) { }
		protected override bool ReleaseHandle() {
			libusb1.libusb_exit(handle);
			return true;
		}
	}
	class libusb_device : SafeHandleZeroOrMinusOneIsInvalid {
		public libusb_device() : base(true) { }
		public libusb_device(IntPtr handle, Boolean ownsHandle) : base(ownsHandle) { SetHandle(handle); }
		protected override bool ReleaseHandle() {
			libusb1.libusb_unref_device(handle);
			return true;
		}
	}
	class libusb_device_handle : SafeHandleZeroOrMinusOneIsInvalid {
		public libusb_device_handle() : base(true) { }
		protected override bool ReleaseHandle() {
			libusb1.libusb_close(handle);
			return true;
		}
	}
	unsafe static class libusb1 {
		const CallingConvention LIBUSB1_CC = CallingConvention.Winapi;
		const String LIBUSB1_DLL = "libusb-1.0.dll";
		/* Device and/or Interface Class codes */
		enum libusb_class_code {
			/** In the context of a \ref libusb_device_descriptor "device descriptor",
			 * this bDeviceClass value indicates that each interface specifies its
			 * own class information and all interfaces operate independently.
			 */
			LIBUSB_CLASS_PER_INTERFACE = 0,

			/** Audio class */
			LIBUSB_CLASS_AUDIO = 1,

			/** Communications class */
			LIBUSB_CLASS_COMM = 2,

			/** Human Interface Device class */
			LIBUSB_CLASS_HID = 3,

			/** Physical */
			LIBUSB_CLASS_PHYSICAL = 5,

			/** Printer class */
			LIBUSB_CLASS_PRINTER = 7,

			/** Image class */
			LIBUSB_CLASS_PTP = 6, /* legacy name from libusb-0.1 usb.h */
			LIBUSB_CLASS_IMAGE = 6,

			/** Mass storage class */
			LIBUSB_CLASS_MASS_STORAGE = 8,

			/** Hub class */
			LIBUSB_CLASS_HUB = 9,

			/** Data class */
			LIBUSB_CLASS_DATA = 10,

			/** Smart Card */
			LIBUSB_CLASS_SMART_CARD = 0x0b,

			/** Content Security */
			LIBUSB_CLASS_CONTENT_SECURITY = 0x0d,

			/** Video */
			LIBUSB_CLASS_VIDEO = 0x0e,

			/** Personal Healthcare */
			LIBUSB_CLASS_PERSONAL_HEALTHCARE = 0x0f,

			/** Diagnostic Device */
			LIBUSB_CLASS_DIAGNOSTIC_DEVICE = 0xdc,

			/** Wireless class */
			LIBUSB_CLASS_WIRELESS = 0xe0,

			/** Application class */
			LIBUSB_CLASS_APPLICATION = 0xfe,

			/** Class is vendor-specific */
			LIBUSB_CLASS_VENDOR_SPEC = 0xff
		}

		/* Descriptor types as defined by the USB specification. */
		enum libusb_descriptor_type {
			/** Device descriptor. See libusb_device_descriptor. */
			LIBUSB_DT_DEVICE = 0x01,

			/** Configuration descriptor. See libusb_config_descriptor. */
			LIBUSB_DT_CONFIG = 0x02,

			/** String descriptor */
			LIBUSB_DT_STRING = 0x03,

			/** Interface descriptor. See libusb_interface_descriptor. */
			LIBUSB_DT_INTERFACE = 0x04,

			/** Endpoint descriptor. See libusb_endpoint_descriptor. */
			LIBUSB_DT_ENDPOINT = 0x05,

			/** HID descriptor */
			LIBUSB_DT_HID = 0x21,

			/** HID report descriptor */
			LIBUSB_DT_REPORT = 0x22,

			/** Physical descriptor */
			LIBUSB_DT_PHYSICAL = 0x23,

			/** Hub descriptor */
			LIBUSB_DT_HUB = 0x29
		}

		/* Descriptor sizes per descriptor type */
		const int LIBUSB_DT_DEVICE_SIZE = 18;
		const int LIBUSB_DT_CONFIG_SIZE = 9;
		const int LIBUSB_DT_INTERFACE_SIZE = 9;
		const int LIBUSB_DT_ENDPOINT_SIZE = 7;
		const int LIBUSB_DT_ENDPOINT_AUDIO_SIZE = 9;	/* Audio extension */
		const int LIBUSB_DT_HUB_NONVAR_SIZE = 7;

		const int LIBUSB_ENDPOINT_ADDRESS_MASK = 0x0f;    /* in bEndpointAddress */
		const int LIBUSB_ENDPOINT_DIR_MASK = 0x80;

		/* Endpoint direction. Values for bit 7 of the
		 * \ref libusb_endpoint_descriptor::bEndpointAddress "endpoint address" scheme.
		 */
		enum libusb_endpoint_direction {
			/** In: device-to-host */
			LIBUSB_ENDPOINT_IN = 0x80,

			/** Out: host-to-device */
			LIBUSB_ENDPOINT_OUT = 0x00
		}

		const int LIBUSB_TRANSFER_TYPE_MASK = 0x03;    /* in bmAttributes */

		/* Endpoint transfer type. Values for bits 0:1 of the
		 * \ref libusb_endpoint_descriptor::bmAttributes "endpoint attributes" field.
		 */
		enum libusb_transfer_type {
			/** Control endpoint */
			LIBUSB_TRANSFER_TYPE_CONTROL = 0,

			/** Isochronous endpoint */
			LIBUSB_TRANSFER_TYPE_ISOCHRONOUS = 1,

			/** Bulk endpoint */
			LIBUSB_TRANSFER_TYPE_BULK = 2,

			/** Interrupt endpoint */
			LIBUSB_TRANSFER_TYPE_INTERRUPT = 3
		}

		/* Standard requests, as defined in table 9-3 of the USB2 specifications */
		enum libusb_standard_request {
			/** Request status of the specific recipient */
			LIBUSB_REQUEST_GET_STATUS = 0x00,

			/** Clear or disable a specific feature */
			LIBUSB_REQUEST_CLEAR_FEATURE = 0x01,

			/* 0x02 is reserved */

			/** Set or enable a specific feature */
			LIBUSB_REQUEST_SET_FEATURE = 0x03,

			/* 0x04 is reserved */

			/** Set device address for all future accesses */
			LIBUSB_REQUEST_SET_ADDRESS = 0x05,

			/** Get the specified descriptor */
			LIBUSB_REQUEST_GET_DESCRIPTOR = 0x06,

			/** Used to update existing descriptors or add new descriptors */
			LIBUSB_REQUEST_SET_DESCRIPTOR = 0x07,

			/** Get the current device configuration value */
			LIBUSB_REQUEST_GET_CONFIGURATION = 0x08,

			/** Set device configuration */
			LIBUSB_REQUEST_SET_CONFIGURATION = 0x09,

			/** Return the selected alternate setting for the specified interface */
			LIBUSB_REQUEST_GET_INTERFACE = 0x0A,

			/** Select an alternate interface for the specified interface */
			LIBUSB_REQUEST_SET_INTERFACE = 0x0B,

			/** Set then report an endpoint's synchronization frame */
			LIBUSB_REQUEST_SYNCH_FRAME = 0x0C
		}

		/* Request type bits of the
		 * \ref libusb_control_setup::bmRequestType "bmRequestType" field in control
		 * transfers. */
		enum libusb_request_type {
			/** Standard */
			LIBUSB_REQUEST_TYPE_STANDARD = (0x00 << 5),

			/** Class */
			LIBUSB_REQUEST_TYPE_CLASS = (0x01 << 5),

			/** Vendor */
			LIBUSB_REQUEST_TYPE_VENDOR = (0x02 << 5),

			/** Reserved */
			LIBUSB_REQUEST_TYPE_RESERVED = (0x03 << 5)
		}

		/* Recipient bits of the
		 * \ref libusb_control_setup::bmRequestType "bmRequestType" field in control
		 * transfers. Values 4 through 31 are reserved. */
		enum libusb_request_recipient {
			/** Device */
			LIBUSB_RECIPIENT_DEVICE = 0x00,

			/** Interface */
			LIBUSB_RECIPIENT_INTERFACE = 0x01,

			/** Endpoint */
			LIBUSB_RECIPIENT_ENDPOINT = 0x02,

			/** Other */
			LIBUSB_RECIPIENT_OTHER = 0x03
		}

		const int LIBUSB_ISO_SYNC_TYPE_MASK = 0x0C;

		/* Synchronization type for isochronous endpoints. Values for bits 2:3 of the
		 * \ref libusb_endpoint_descriptor::bmAttributes "bmAttributes" field in
		 * libusb_endpoint_descriptor.
		 */
		enum libusb_iso_sync_type {
			/** No synchronization */
			LIBUSB_ISO_SYNC_TYPE_NONE = 0,

			/** Asynchronous */
			LIBUSB_ISO_SYNC_TYPE_ASYNC = 1,

			/** Adaptive */
			LIBUSB_ISO_SYNC_TYPE_ADAPTIVE = 2,

			/** Synchronous */
			LIBUSB_ISO_SYNC_TYPE_SYNC = 3
		}
		const int LIBUSB_ISO_USAGE_TYPE_MASK = 0x30;

		/* Usage type for isochronous endpoints. Values for bits 4:5 of the
		 * \ref libusb_endpoint_descriptor::bmAttributes "bmAttributes" field in
		 * libusb_endpoint_descriptor.
		 */
		enum libusb_iso_usage_type {
			/** Data endpoint */
			LIBUSB_ISO_USAGE_TYPE_DATA = 0,

			/** Feedback endpoint */
			LIBUSB_ISO_USAGE_TYPE_FEEDBACK = 1,

			/** Implicit feedback Data endpoint */
			LIBUSB_ISO_USAGE_TYPE_IMPLICIT = 2
		}

		/* A structure representing the standard USB device descriptor. This
		 * descriptor is documented in section 9.6.1 of the USB 2.0 specification.
		 * All multiple-byte fields are represented in host-endian format.
		 */
		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct libusb_device_descriptor {
			/** Size of this descriptor (in bytes) */
			public Byte bLength;

			/** Descriptor type. Will have value
			 * \ref libusb_descriptor_type::LIBUSB_DT_DEVICE LIBUSB_DT_DEVICE in this
			 * context. */
			public Byte bDescriptorType;

			/** USB specification release number in binary-coded decimal. A value of
			 * 0x0200 indicates USB 2.0, 0x0110 indicates USB 1.1, etc. */
			public UInt16 bcdUSB;

			/** USB-IF class code for the device. See \ref libusb_class_code. */
			public Byte bDeviceClass;

			/** USB-IF subclass code for the device, qualified by the bDeviceClass
			 * value */
			public Byte bDeviceSubClass;

			/** USB-IF protocol code for the device, qualified by the bDeviceClass and
			 * bDeviceSubClass values */
			public Byte bDeviceProtocol;

			/** Maximum packet size for endpoint 0 */
			public Byte bMaxPacketSize0;

			/** USB-IF vendor ID */
			public UInt16 idVendor;

			/** USB-IF product ID */
			public UInt16 idProduct;

			/** Device release number in binary-coded decimal */
			public UInt16 bcdDevice;

			/** Index of string descriptor describing manufacturer */
			public Byte iManufacturer;

			/** Index of string descriptor describing product */
			public Byte iProduct;

			/** Index of string descriptor containing device serial number */
			public Byte iSerialNumber;

			/** Number of possible configurations */
			public Byte bNumConfigurations;
		}

		/* A structure representing the standard USB endpoint descriptor. This
		 * descriptor is documented in section 9.6.3 of the USB 2.0 specification.
		 * All multiple-byte fields are represented in host-endian format.
		 */
		[StructLayout(LayoutKind.Sequential)]
		struct libusb_endpoint_descriptor {
			/** Size of this descriptor (in bytes) */
			Byte bLength;

			/** Descriptor type. Will have value
			 * \ref libusb_descriptor_type::LIBUSB_DT_ENDPOINT LIBUSB_DT_ENDPOINT in
			 * this context. */
			Byte bDescriptorType;

			/** The address of the endpoint described by this descriptor. Bits 0:3 are
			 * the endpoint number. Bits 4:6 are reserved. Bit 7 indicates direction,
			 * see \ref libusb_endpoint_direction.
			 */
			Byte bEndpointAddress;

			/** Attributes which apply to the endpoint when it is configured using
			 * the bConfigurationValue. Bits 0:1 determine the transfer type and
			 * correspond to \ref libusb_transfer_type. Bits 2:3 are only used for
			 * isochronous endpoints and correspond to \ref libusb_iso_sync_type.
			 * Bits 4:5 are also only used for isochronous endpoints and correspond to
			 * \ref libusb_iso_usage_type. Bits 6:7 are reserved.
			 */
			Byte bmAttributes;

			/** Maximum packet size this endpoint is capable of sending/receiving. */
			UInt16 wMaxPacketSize;

			/** Interval for polling endpoint for data transfers. */
			Byte bInterval;

			/** For audio devices only: the rate at which synchronization feedback
			 * is provided. */
			Byte bRefresh;

			/** For audio devices only: the address if the synch endpoint */
			Byte bSynchAddress;

			/** Extra descriptors. If libusb encounters unknown endpoint descriptors,
			 * it will store them here, should you wish to parse them. */
			byte* extra;

			/** Length of the extra descriptors, in bytes. */
			int extra_length;
		}

		/* A structure representing the standard USB interface descriptor. This
		 * descriptor is documented in section 9.6.5 of the USB 2.0 specification.
		 * All multiple-byte fields are represented in host-endian format.
		 */
		[StructLayout(LayoutKind.Sequential)]
		struct libusb_interface_descriptor {
			/** Size of this descriptor (in bytes) */
			Byte bLength;

			/** Descriptor type. Will have value
			 * \ref libusb_descriptor_type::LIBUSB_DT_INTERFACE LIBUSB_DT_INTERFACE
			 * in this context. */
			Byte bDescriptorType;

			/** Number of this interface */
			Byte bInterfaceNumber;

			/** Value used to select this alternate setting for this interface */
			Byte bAlternateSetting;

			/** Number of endpoints used by this interface (excluding the control
			 * endpoint). */
			Byte bNumEndpoints;

			/** USB-IF class code for this interface. See \ref libusb_class_code. */
			Byte bInterfaceClass;

			/** USB-IF subclass code for this interface, qualified by the
			 * bInterfaceClass value */
			Byte bInterfaceSubClass;

			/** USB-IF protocol code for this interface, qualified by the
			 * bInterfaceClass and bInterfaceSubClass values */
			Byte bInterfaceProtocol;

			/** Index of string descriptor describing this interface */
			Byte iInterface;

			/** Array of endpoint descriptors. This length of this array is determined
			 * by the bNumEndpoints field. */
			libusb_endpoint_descriptor* endpoint;

			/** Extra descriptors. If libusb encounters unknown interface descriptors,
			 * it will store them here, should you wish to parse them. */
			Byte* extra;

			/** Length of the extra descriptors, in bytes. */
			int extra_length;
		}

		/* A collection of alternate settings for a particular USB interface.
		 */
		[StructLayout(LayoutKind.Sequential)]
		struct libusb_interface {
			/** Array of interface descriptors. The length of this array is determined
			 * by the num_altsetting field. */
			libusb_interface_descriptor* altsetting;

			/** The number of alternate settings that belong to this interface */
			int num_altsetting;
		}

		/* A structure representing the standard USB configuration descriptor. This
		 * descriptor is documented in section 9.6.3 of the USB 2.0 specification.
		 * All multiple-byte fields are represented in host-endian format.
		 */
		struct libusb_config_descriptor {
			/** Size of this descriptor (in bytes) */
			Byte bLength;

			/** Descriptor type. Will have value
			 * \ref libusb_descriptor_type::LIBUSB_DT_CONFIG LIBUSB_DT_CONFIG
			 * in this context. */
			Byte bDescriptorType;

			/** Total length of data returned for this configuration */
			Byte wTotalLength;

			/** Number of interfaces supported by this configuration */
			Byte bNumInterfaces;

			/** Identifier value for this configuration */
			Byte bConfigurationValue;

			/** Index of string descriptor describing this configuration */
			Byte iConfiguration;

			/** Configuration characteristics */
			Byte bmAttributes;

			/** Maximum power consumption of the USB device from this bus in this
			 * configuration when the device is fully opreation. Expressed in units
			 * of 2 mA. */
			Byte MaxPower;

			/** Array of interfaces supported by this configuration. The length of
			 * this array is determined by the bNumInterfaces field. */
			libusb_interface* @interface;

			/** Extra descriptors. If libusb encounters unknown configuration
			 * descriptors, it will store them here, should you wish to parse them. */
			Byte* extra;

			/** Length of the extra descriptors, in bytes. */
			int extra_length;
		}

		/* Setup packet for control transfers. */
		[StructLayout(LayoutKind.Sequential, Size = 8)]
		struct libusb_control_setup {
			/** Request type. Bits 0:4 determine recipient, see
			 * \ref libusb_request_recipient. Bits 5:6 determine type, see
			 * \ref libusb_request_type. Bit 7 determines data transfer direction, see
			 * \ref libusb_endpoint_direction.
			 */
			Byte bmRequestType;

			/** Request. If the type bits of bmRequestType are equal to
			 * \ref libusb_request_type::LIBUSB_REQUEST_TYPE_STANDARD
			 * "LIBUSB_REQUEST_TYPE_STANDARD" then this field refers to
			 * \ref libusb_standard_request. For other cases, use of this field is
			 * application-specific. */
			Byte bRequest;

			/** Value. Varies according to request */
			UInt16 wValue;

			/** Index. Varies according to request, typically used to pass an index
			 * or offset */
			UInt16 wIndex;

			/** Number of bytes to transfer */
			UInt16 wLength;
		}

		const int LIBUSB_CONTROL_SETUP_SIZE = 8; //sizeof(libusb_control_setup); // Marshal.SizeOf(typeof(libusb_control_setup));

		/* Structure representing a libusb session. The concept of individual libusb
		 * sessions allows for your program to use two libraries (or dynamically
		 * load two modules) which both independently use libusb. This will prevent
		 * interference between the individual libusb users - for example
		 * libusb_set_debug() will not affect the other user of the library, and
		 * libusb_exit() will not destroy resources that the other user is still
		 * using.
		 *
		 * Sessions are created by libusb_init() and destroyed through libusb_exit().
		 * If your application is guaranteed to only ever include a single libusb
		 * user (i.e. you), you do not have to worry about contexts: pass NULL in
		 * every function call where a context is required. The default context
		 * will be used.
		 *
		 * For more information, see \ref contexts.
		 */

		/* Structure representing a USB device detected on the system. This is an
		 * opaque type for which you are only ever provided with a pointer, usually
		 * originating from libusb_get_device_list().
		 *
		 * Certain operations can be performed on a device, but in order to do any
		 * I/O you will have to first obtain a device handle using libusb_open().
		 *
		 * Devices are reference counted with libusb_device_ref() and
		 * libusb_device_unref(), and are freed when the reference count reaches 0.
		 * New devices presented by libusb_get_device_list() have a reference count of
		 * 1, and libusb_free_device_list() can optionally decrease the reference count
		 * on all devices in the list. libusb_open() adds another reference which is
		 * later destroyed by libusb_close().
		 */

		/* Structure representing a handle on a USB device. This is an opaque type for
		 * which you are only ever provided with a pointer, usually originating from
		 * libusb_open().
		 *
		 * A device handle is used to perform I/O and other operations. When finished
		 * with a device handle, you should call libusb_close().
		 */

		/* Speed codes. Indicates the speed at which the device is operating. */
		enum libusb_speed {
			/** The OS doesn't report or know the device speed. */
			LIBUSB_SPEED_UNKNOWN = 0,

			/** The device is operating at low speed (1.5MBit/s). */
			LIBUSB_SPEED_LOW = 1,

			/** The device is operating at full speed (12MBit/s). */
			LIBUSB_SPEED_FULL = 2,

			/** The device is operating at high speed (480MBit/s). */
			LIBUSB_SPEED_HIGH = 3,

			/** The device is operating at super speed (5000MBit/s). */
			LIBUSB_SPEED_SUPER = 4,
		}

		/* Error codes. Most libusb functions return 0 on success or one of these
		 * codes on failure.
		 * You can call \ref libusb_error_name() to retrieve a string representation
		 * of an error code.
		 */
		enum libusb_error {
			/** Success (no error) */
			LIBUSB_SUCCESS = 0,

			/** Input/output error */
			LIBUSB_ERROR_IO = -1,

			/** Invalid parameter */
			LIBUSB_ERROR_INVALID_PARAM = -2,

			/** Access denied (insufficient permissions) */
			LIBUSB_ERROR_ACCESS = -3,

			/** No such device (it may have been disconnected) */
			LIBUSB_ERROR_NO_DEVICE = -4,

			/** Entity not found */
			LIBUSB_ERROR_NOT_FOUND = -5,

			/** Resource busy */
			LIBUSB_ERROR_BUSY = -6,

			/** Operation timed out */
			LIBUSB_ERROR_TIMEOUT = -7,

			/** Overflow */
			LIBUSB_ERROR_OVERFLOW = -8,

			/** Pipe error */
			LIBUSB_ERROR_PIPE = -9,

			/** System call interrupted (perhaps due to signal) */
			LIBUSB_ERROR_INTERRUPTED = -10,

			/** Insufficient memory */
			LIBUSB_ERROR_NO_MEM = -11,

			/** Operation not supported or unimplemented on this platform */
			LIBUSB_ERROR_NOT_SUPPORTED = -12,

			/* NB! Remember to update libusb_error_name()
			   when adding new error codes here. */

			/** Other error */
			LIBUSB_ERROR_OTHER = -99
		}

		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		public static extern int libusb_init(out libusb_context ctx);
		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		public static extern void libusb_exit(IntPtr ctx);
		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		static extern void libusb_set_debug(libusb_context ctx, int level);
		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		static extern Byte* libusb_error_name(int errcode);

		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		public static extern ssize_t libusb_get_device_list(libusb_context ctx, out IntPtr* list); //libusb_device** list
		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		public static extern void libusb_free_device_list(IntPtr* list, int unref_devices);
		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		static extern libusb_device libusb_ref_device(libusb_device dev);
		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		public static extern void libusb_unref_device(IntPtr dev);

		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		static extern int libusb_get_configuration(libusb_device_handle dev, out  int config);
		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		public static extern int libusb_get_device_descriptor(libusb_device dev, out libusb_device_descriptor desc);
		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		static extern int libusb_get_active_config_descriptor(libusb_device dev, libusb_config_descriptor** config);
		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		static extern int libusb_get_config_descriptor(libusb_device dev, Byte config_index, libusb_config_descriptor** config);
		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		static extern int libusb_get_config_descriptor_by_value(libusb_device dev, Byte bConfigurationValue, libusb_config_descriptor** config);
		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		static extern void libusb_free_config_descriptor(libusb_config_descriptor* config);
		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		public static extern Byte libusb_get_bus_number(libusb_device dev);
		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		public static extern Byte libusb_get_device_address(libusb_device dev);
		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		static extern int libusb_get_device_speed(libusb_device dev);
		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		static extern int libusb_get_max_packet_size(libusb_device dev, Byte endpoint);
		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		static extern int libusb_get_max_iso_packet_size(libusb_device dev, Byte endpoint);

		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		public static extern int libusb_open(libusb_device dev, out libusb_device_handle handle);
		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		public static extern void libusb_close(IntPtr dev_handle);
		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		static extern libusb_device libusb_get_device(libusb_device_handle dev_handle);

		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		public static extern int libusb_set_configuration(libusb_device_handle dev, int configuration);
		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		public static extern int libusb_claim_interface(libusb_device_handle dev, int interface_number);
		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		public static extern int libusb_release_interface(libusb_device_handle dev, int interface_number);

		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		static extern libusb_device_handle libusb_open_device_with_vid_pid(libusb_context ctx, UInt16 vendor_id, UInt16 product_id);

		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		static extern int libusb_set_interface_alt_setting(libusb_device_handle dev, int interface_number, int alternate_setting);
		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		static extern int libusb_clear_halt(libusb_device_handle dev, Byte endpoint);
		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		public static extern int libusb_reset_device(libusb_device_handle dev);

		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		static extern int libusb_kernel_driver_active(libusb_device_handle dev, int interface_number);
		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		public static extern int libusb_detach_kernel_driver(libusb_device_handle dev, int interface_number);
		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		public static extern int libusb_attach_kernel_driver(libusb_device_handle dev, int interface_number);

		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		public static extern int libusb_control_transfer(libusb_device_handle dev_handle,
			Byte request_type, Byte bRequest, UInt16 wValue, UInt16 wIndex,
			Byte* data, UInt16 wLength, UInt32 timeout);

		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		public static extern int libusb_bulk_transfer(libusb_device_handle dev_handle,
			Byte endpoint, Byte* data, int length,
			out int actual_length, UInt32 timeout);

		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		public static extern int libusb_interrupt_transfer(libusb_device_handle dev_handle,
			Byte endpoint, Byte* data, int length,
			out int actual_length, UInt32 timeout);

		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		public static extern int libusb_get_string_descriptor_ascii(libusb_device_handle dev,
			Byte desc_index, [MarshalAs(UnmanagedType.LPStr)] StringBuilder data, int length);
	}
}
