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
		const String LIBUSB1_DLL = "libusb-1.0.so.0";

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct libusb_device_descriptor {
			public Byte bLength;
			public Byte bDescriptorType;
			public UInt16 bcdUSB;
			public Byte bDeviceClass;
			public Byte bDeviceSubClass;
			public Byte bDeviceProtocol;
			public Byte bMaxPacketSize0;
			public UInt16 idVendor;
			public UInt16 idProduct;
			public UInt16 bcdDevice;
			public Byte iManufacturer;
			public Byte iProduct;
			public Byte iSerialNumber;
			public Byte bNumConfigurations;
		}

		[StructLayout(LayoutKind.Sequential)]
		struct libusb_endpoint_descriptor {
			Byte bLength;
			Byte bDescriptorType;
			Byte bEndpointAddress;
			Byte bmAttributes;
			UInt16 wMaxPacketSize;
			Byte bInterval;
			Byte bRefresh;
			Byte bSynchAddress;
			byte* extra;
			int extra_length;
		}

		[StructLayout(LayoutKind.Sequential)]
		struct libusb_interface_descriptor {
			Byte bLength;
			Byte bDescriptorType;
			Byte bInterfaceNumber;
			Byte bAlternateSetting;
			Byte bNumEndpoints;
			Byte bInterfaceClass;
			Byte bInterfaceSubClass;
			Byte bInterfaceProtocol;
			Byte iInterface;
			libusb_endpoint_descriptor* endpoint;
			Byte* extra;
			int extra_length;
		}
		[StructLayout(LayoutKind.Sequential)]
		struct libusb_interface {
			libusb_interface_descriptor* altsetting;
			int num_altsetting;
		}
		struct libusb_config_descriptor {
			Byte bLength;
			Byte bDescriptorType;
			Byte wTotalLength;
			Byte bNumInterfaces;
			Byte bConfigurationValue;
			Byte iConfiguration;
			Byte bmAttributes;
			Byte MaxPower;
			libusb_interface* @interface;
			Byte* extra;
			int extra_length;
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
		public static extern int libusb_clear_halt(libusb_device_handle dev, Byte endpoint);
		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		public static extern int libusb_reset_device(libusb_device_handle dev);

		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		static extern int libusb_kernel_driver_active(libusb_device_handle dev, int interface_number);
		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		public static extern int libusb_detach_kernel_driver(libusb_device_handle dev, int interface_number);
		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		public static extern int libusb_attach_kernel_driver(libusb_device_handle dev, int interface_number);

		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		public static extern int libusb_control_transfer(libusb_device_handle dev_handle, Byte request_type, Byte bRequest, UInt16 wValue, UInt16 wIndex, Byte* data, UInt16 wLength, UInt32 timeout);

		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		public static extern int libusb_bulk_transfer(libusb_device_handle dev_handle, Byte endpoint, Byte* data, int length, out int actual_length, UInt32 timeout);

		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		public static extern int libusb_interrupt_transfer(libusb_device_handle dev_handle, Byte endpoint, Byte* data, int length, out int actual_length, UInt32 timeout);

		[DllImport(LIBUSB1_DLL, CallingConvention = LIBUSB1_CC)]
		public static extern int libusb_get_string_descriptor_ascii(libusb_device_handle dev, Byte desc_index, [MarshalAs(UnmanagedType.LPStr)] StringBuilder data, int length);
	}
}
