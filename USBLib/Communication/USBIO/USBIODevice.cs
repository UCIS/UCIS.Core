using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using UCIS.USBLib.Internal.Windows;

namespace UCIS.USBLib.Communication.USBIO {
	public class USBIODevice : UsbInterface, IUsbDevice {
		public string DeviceFilename { get; private set; }
		public IUsbDeviceRegistry Registry { get; private set; }
		private SafeFileHandle DeviceHandle;
		private SafeFileHandle[] PipeHandlesIn = null;
		private SafeFileHandle[] PipeHandlesOut = null;

		static int CTL_CODE(int DeviceType, int Function, int Method, int Access) { return ((DeviceType) << 16) | ((Access) << 14) | ((Function) << 2) | (Method); }
		static int _USBIO_IOCTL_CODE(int FnCode, int Method) { return CTL_CODE(0x8094, 0x800 + FnCode, Method, 0); }
		const int METHOD_BUFFERED = 0;
		const int METHOD_IN_DIRECT = 1;
		const int METHOD_OUT_DIRECT = 2;
		static readonly int IOCTL_USBIO_GET_DESCRIPTOR = _USBIO_IOCTL_CODE(1, METHOD_OUT_DIRECT);
		static readonly int IOCTL_USBIO_GET_CONFIGURATION = _USBIO_IOCTL_CODE(6, METHOD_BUFFERED);
		static readonly int IOCTL_USBIO_SET_CONFIGURATION = _USBIO_IOCTL_CODE(9, METHOD_BUFFERED);
		static readonly int IOCTL_USBIO_CLASS_OR_VENDOR_IN_REQUEST = _USBIO_IOCTL_CODE(12, METHOD_OUT_DIRECT);
		static readonly int IOCTL_USBIO_CLASS_OR_VENDOR_OUT_REQUEST = _USBIO_IOCTL_CODE(13, METHOD_IN_DIRECT);
		static readonly int IOCTL_USBIO_RESET_DEVICE = _USBIO_IOCTL_CODE(21, METHOD_BUFFERED);
		static readonly int IOCTL_USBIO_BIND_PIPE = _USBIO_IOCTL_CODE(30, METHOD_BUFFERED);

		[DllImport("kernel32.dll", SetLastError = true)]
		static unsafe extern bool ReadFile(SafeFileHandle hFile, byte* lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, IntPtr lpOverlapped);
		[DllImport("kernel32.dll", SetLastError = true)]
		static unsafe extern bool WriteFile(SafeFileHandle hFile, byte* lpBuffer, uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, IntPtr lpOverlapped);

		enum USBIO_REQUEST_RECIPIENT : uint {
			Device = 0,
			Interface,
			Endpoint,
			Other,
		}
		enum USBIO_REQUEST_TYPE : uint {
			Class = 1,
			Vendor,
		}

		const UInt32 USBIO_SHORT_TRANSFER_OK = 0x00010000;

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		struct USBIO_DESCRIPTOR_REQUEST {
			public USBIO_REQUEST_RECIPIENT Recipient;
			public Byte DescriptorType;
			public Byte DescriptorIndex;
			public Int16 LanguageId;
		}
		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		struct USBIO_BIND_PIPE {
			public Byte EndpointAddress;
		}
		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 2 + 2 + 4)]
		struct USBIO_INTERFACE_SETTING {
			public UInt16 InterfaceIndex;
			public UInt16 AlternateSettingIndex;
			public UInt32 MaximumTransferSize;
		}
		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		unsafe struct USBIO_SET_CONFIGURATION{
			public UInt16 ConfigurationIndex;
			public UInt16 NbOfInterfaces;
			public fixed byte InterfaceList[32 * (2 + 2 + 4)];
		}
		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		struct USBIO_CLASS_OR_VENDOR_REQUEST {
			public UInt32 Flags;
			public USBIO_REQUEST_TYPE Type;
			public USBIO_REQUEST_RECIPIENT Recipient;
			public Byte RequestTypeReservedBits;
			public Byte Request;
			public Int16 Value;
			public Int16 Index;
		}

		public USBIODevice(String path, USBIORegistry registry) {
			DeviceFilename = path;
			this.Registry = registry;
			DeviceHandle = OpenHandle();
		}
		private SafeFileHandle OpenHandle() {
			SafeFileHandle handle = Kernel32.CreateFile(DeviceFilename,
						   NativeFileAccess.FILE_GENERIC_READ | NativeFileAccess.FILE_GENERIC_WRITE,
						   NativeFileShare.FILE_SHARE_WRITE | NativeFileShare.FILE_SHARE_READ,
						   IntPtr.Zero,
						   NativeFileMode.OPEN_EXISTING,
						   NativeFileFlag.FILE_ATTRIBUTE_NORMAL,
						   IntPtr.Zero);
			if (handle.IsInvalid || handle.IsClosed) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not open device");
			return handle;
		}
		public override void Close() {
			if (PipeHandlesIn != null) for (int i = 0; i < PipeHandlesIn.Length; i++) if (PipeHandlesIn[i] != null) PipeHandlesIn[i].Close();
			if (PipeHandlesOut != null) for (int i = 0; i < PipeHandlesOut.Length; i++) if (PipeHandlesOut[i] != null) PipeHandlesOut[i].Close();
			if (DeviceHandle != null) DeviceHandle.Close();
		}

		public override Byte Configuration {
			get { return base.Configuration; }
			set {
				if (value == Configuration) return;
				IList<LibUsbDotNet.Info.UsbConfigInfo> configs = (new LibUsbDotNet.UsbDevice(this)).Configs;
				for (int i = 0; i < configs.Count; i++) {
					LibUsbDotNet.Info.UsbConfigInfo config = configs[i];
					if (config.Descriptor.ConfigID == value) {
						unsafe {
							USBIO_SET_CONFIGURATION req = new USBIO_SET_CONFIGURATION();
							req.ConfigurationIndex = (ushort)i;
							req.NbOfInterfaces = Math.Min((ushort)32, config.Descriptor.InterfaceCount);
							for (int j = 0; j < req.NbOfInterfaces; j++) {
								LibUsbDotNet.Info.UsbInterfaceInfo intf = config.InterfaceInfoList[i];
								*((USBIO_INTERFACE_SETTING*)(req.InterfaceList + sizeof(USBIO_INTERFACE_SETTING) * i)) =
									new USBIO_INTERFACE_SETTING() { InterfaceIndex = intf.Descriptor.InterfaceID, AlternateSettingIndex = 0, MaximumTransferSize = UInt16.MaxValue };
							}
							DeviceIoControl(DeviceHandle, IOCTL_USBIO_SET_CONFIGURATION, (IntPtr)(&req), sizeof(USBIO_SET_CONFIGURATION), IntPtr.Zero, 0);
						}
						return;
					}
				}
				throw new InvalidOperationException("Requested configuration ID not found");
			}
		}

		public void ClaimInterface(int interfaceID) {
		}
		public void ReleaseInterface(int interfaceID) {
		}
		public void SetAltInterface(int interfaceID, int alternateID) {
			throw new NotImplementedException();
		}
		public void ResetDevice() {
			DeviceIoControl(DeviceHandle, IOCTL_USBIO_RESET_DEVICE, IntPtr.Zero, 0, IntPtr.Zero, 0);
		}
		public unsafe override int GetDescriptor(byte descriptorType, byte index, short langId, byte[] buffer, int offset, int length) {
			if (offset < 0 || length < 0 || offset + length > buffer.Length) throw new ArgumentOutOfRangeException("length", "The specified offset and length exceed the buffer length");
			USBIO_DESCRIPTOR_REQUEST req = new USBIO_DESCRIPTOR_REQUEST() { DescriptorType = descriptorType, DescriptorIndex = index, LanguageId = langId, Recipient = USBIO_REQUEST_RECIPIENT.Device };
			fixed (Byte* b = buffer) {
				return DeviceIoControl(DeviceHandle, IOCTL_USBIO_GET_DESCRIPTOR, (IntPtr)(&req), sizeof(USBIO_DESCRIPTOR_REQUEST), (IntPtr)(b + offset), length);
			}
		}
		public override int BulkRead(byte endpoint, byte[] buffer, int offset, int length) {
			return PipeRead(endpoint, buffer, offset, length);
		}
		public override int BulkWrite(byte endpoint, byte[] buffer, int offset, int length) {
			return PipeWrite(endpoint, buffer, offset, length);
		}
		public override int InterruptRead(byte endpoint, byte[] buffer, int offset, int length) {
			return PipeRead(endpoint, buffer, offset, length);
		}
		public override int InterruptWrite(byte endpoint, byte[] buffer, int offset, int length) {
			return PipeWrite(endpoint, buffer, offset, length);
		}
		public unsafe override int ControlRead(UsbControlRequestType requestType, byte request, short value, short index, byte[] buffer, int offset, int length) {
			return ControlTransfer(requestType, request, value, index, buffer, offset, length);
		}
		public override int ControlWrite(UsbControlRequestType requestType, byte request, short value, short index, byte[] buffer, int offset, int length) {
			return ControlTransfer(requestType, request, value, index, buffer, offset, length);
		}
		private unsafe int ControlTransfer(UsbControlRequestType requestType, byte request, short value, short index, byte[] buffer, int offset, int length) {
			if (buffer == null) {
				if (offset != 0 || length != 0) throw new ArgumentOutOfRangeException("length", "The specified offset and length exceed the buffer length");
			} else {
				if (offset < 0 || length < 0 || offset + length > buffer.Length) throw new ArgumentOutOfRangeException("length", "The specified offset and length exceed the buffer length");
			}
			switch (requestType & UsbControlRequestType.TypeMask) {
				case UsbControlRequestType.TypeStandard:
					switch ((UsbStandardRequest)request) {
						case UsbStandardRequest.GetDescriptor:
							return GetDescriptor((Byte)(value >> 8), (Byte)value, index, buffer, offset, length);
						case UsbStandardRequest.GetConfiguration:
							fixed (Byte* b = buffer) return DeviceIoControl(DeviceHandle, IOCTL_USBIO_GET_CONFIGURATION, IntPtr.Zero, 0, (IntPtr)(b + offset), length);
						case UsbStandardRequest.SetConfiguration:
							Configuration = (Byte)value;
							return 0;
						default:
							throw new ArgumentException(String.Format("Invalid request: 0x{0:X8}", request));
					}
					break;
				case UsbControlRequestType.TypeVendor:
				case UsbControlRequestType.TypeClass:
					USBIO_CLASS_OR_VENDOR_REQUEST req = new USBIO_CLASS_OR_VENDOR_REQUEST() {
						Flags = USBIO_SHORT_TRANSFER_OK,
						Type = (USBIO_REQUEST_TYPE)((int)(requestType & UsbControlRequestType.TypeMask) >> 5),
						Recipient = (USBIO_REQUEST_RECIPIENT)((int)(requestType & UsbControlRequestType.RecipMask) >> 0),
						RequestTypeReservedBits = 0,
						Request = request,
						Value = value,
						Index = index,
					};
					fixed (Byte* b = buffer) {
						if ((requestType & UsbControlRequestType.EndpointMask) == UsbControlRequestType.EndpointIn) {
							return DeviceIoControl(DeviceHandle, IOCTL_USBIO_CLASS_OR_VENDOR_IN_REQUEST, (IntPtr)(&req), sizeof(USBIO_CLASS_OR_VENDOR_REQUEST), (IntPtr)(b + offset), length);
						} else {
							return DeviceIoControl(DeviceHandle, IOCTL_USBIO_CLASS_OR_VENDOR_OUT_REQUEST, (IntPtr)(&req), sizeof(USBIO_CLASS_OR_VENDOR_REQUEST), (IntPtr)(b + offset), length);
						}
					}
				case UsbControlRequestType.TypeReserved:
				default:
					throw new ArgumentException(String.Format("Invalid or unsupported request type: 0x{0:X8}", requestType));
			}
		}
		private unsafe SafeFileHandle OpenHandleForPipe(Byte epID) {
			SafeFileHandle handle = OpenHandle();
			USBIO_BIND_PIPE req = new USBIO_BIND_PIPE() { EndpointAddress = epID };
			try {
				DeviceIoControl(handle, IOCTL_USBIO_BIND_PIPE, (IntPtr)(&req), sizeof(USBIO_BIND_PIPE), IntPtr.Zero, 0);
			} catch (Exception) {
				handle.Close();
				throw;
			}
			return handle;
		}
		private SafeFileHandle GetHandleForPipe(Byte epID) {
			int epidx = epID & 0x7F;
			if ((epID & 0x80) != 0) {
				if (PipeHandlesIn != null && PipeHandlesIn.Length >= epidx && PipeHandlesIn[epidx] != null) return PipeHandlesIn[epidx];
				SafeFileHandle handle = OpenHandleForPipe(epID);
				if (PipeHandlesIn == null) PipeHandlesIn = new SafeFileHandle[epidx + 1];
				else Array.Resize(ref PipeHandlesIn, epidx + 1);
				PipeHandlesIn[epidx] = handle;
				return handle;
			} else {
				if (PipeHandlesOut != null && PipeHandlesOut.Length >= epidx && PipeHandlesOut[epidx] != null) return PipeHandlesOut[epidx];
				SafeFileHandle handle = OpenHandleForPipe(epID);
				if (PipeHandlesOut == null) PipeHandlesOut = new SafeFileHandle[epidx + 1];
				else Array.Resize(ref PipeHandlesOut, epidx + 1);
				PipeHandlesOut[epidx] = handle;
				return handle;
			}
		}
		unsafe int PipeRead(Byte epnum, Byte[] buffer, int offset, int length) {
			if (offset < 0 || length < 0 || offset + length > buffer.Length) throw new ArgumentOutOfRangeException("length", "The specified offset and length exceed the buffer length");
			SafeFileHandle handle = GetHandleForPipe(epnum);
			uint ret;
			fixed (Byte* b = buffer) {
				if (!ReadFile(handle, b + offset, (uint)length, out ret, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
			}
			return (int)ret;
		}
		unsafe int PipeWrite(Byte epnum, Byte[] buffer, int offset, int length) {
			if (offset < 0 || length < 0 || offset + length > buffer.Length) throw new ArgumentOutOfRangeException("length", "The specified offset and length exceed the buffer length");
			SafeFileHandle handle = GetHandleForPipe(epnum);
			uint ret;
			fixed (Byte* b = buffer) {
				if (!WriteFile(handle, b + offset, (uint)length, out ret, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
			}
			return (int)ret;
		}
		public void PipeReset(byte pipeID) {
			throw new NotImplementedException();
		}

		private unsafe int DeviceIoControl(SafeHandle hDevice, int IoControlCode, IntPtr InBuffer, int nInBufferSize, IntPtr OutBuffer, int nOutBufferSize) {
			int pBytesReturned;
			if (Kernel32.DeviceIoControl(hDevice, IoControlCode, InBuffer, nInBufferSize, OutBuffer, nOutBufferSize, out pBytesReturned, null))
				return pBytesReturned;
			throw new Win32Exception(Marshal.GetLastWin32Error());
		}

		public override UsbPipeStream GetBulkStream(byte endpoint) {
			return new PipeStream(this, endpoint, false, GetHandleForPipe(endpoint));
		}
		public override UsbPipeStream GetInterruptStream(byte endpoint) {
			return new PipeStream(this, endpoint, true, GetHandleForPipe(endpoint));
		}

		class PipeStream : UsbPipeStream {
			private SafeFileHandle Handle;
			public PipeStream(IUsbInterface device, Byte endpoint, Boolean interrupt, SafeFileHandle handle) : base(device, endpoint, interrupt) {
				this.Handle = handle;
			}

			public unsafe override void Write(byte[] buffer, int offset, int length) {
				if (offset < 0 || length < 0 || offset + length > buffer.Length) throw new ArgumentOutOfRangeException("length", "The specified offset and length exceed the buffer length");
				uint ret;
				fixed (Byte* b = buffer) {
					if (!WriteFile(Handle, b + offset, (uint)length, out ret, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
				}
				if (ret <= 0) throw new EndOfStreamException("Could not write all data");
			}

			public unsafe override int Read(byte[] buffer, int offset, int length) {
				if (offset < 0 || length < 0 || offset + length > buffer.Length) throw new ArgumentOutOfRangeException("length", "The specified offset and length exceed the buffer length");
				uint ret;
				fixed (Byte* b = buffer) {
					if (!WriteFile(Handle, b + offset, (uint)length, out ret, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
				}
				return (int)ret;
			}
		}
	}
}