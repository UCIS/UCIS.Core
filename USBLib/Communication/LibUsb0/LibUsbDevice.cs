using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using UCIS.USBLib.Internal.Windows;

namespace UCIS.USBLib.Communication.LibUsb {
	public class LibUsb0Device : UsbInterface, IUsbDevice {
		//private readonly List<int> mClaimedInterfaces = new List<int>();
		public string DeviceFilename { get; private set; }
		public IUsbDeviceRegistry Registry { get; private set; }
		private SafeFileHandle DeviceHandle;

		public LibUsb0Device(String path, LibUsb0Registry registry) {
			DeviceFilename = path;
			this.Registry = registry;
			DeviceHandle = Kernel32.CreateFile(DeviceFilename,
						   NativeFileAccess.SPECIAL,
						   NativeFileShare.NONE,
						   IntPtr.Zero,
						   NativeFileMode.OPEN_EXISTING,
						   NativeFileFlag.FILE_FLAG_OVERLAPPED,
						   IntPtr.Zero);
			if (DeviceHandle.IsInvalid || DeviceHandle.IsClosed) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not open device");
		}
		public override void Close() {
			if (DeviceHandle != null) DeviceHandle.Close();
		}

		public override Byte Configuration {
			get { return base.Configuration; }
			set {
				ControlWrite(
					UsbControlRequestType.EndpointOut | UsbControlRequestType.TypeStandard | UsbControlRequestType.RecipDevice,
					(byte)UsbStandardRequest.SetConfiguration,
					value,
					0, null, 0, 0);
			}
		}

		public void ClaimInterface(int interfaceID) {
			LibUsbRequest req = new LibUsbRequest();
			req.Iface.ID = interfaceID;
			req.Timeout = UsbConstants.DEFAULT_TIMEOUT;
			int ret;
			DeviceIoControl(DeviceHandle, LibUsbIoCtl.CLAIM_INTERFACE, ref req, LibUsbRequest.Size, null, 0, out ret);
		}
		public void ReleaseInterface(int interfaceID) {
			LibUsbRequest req = new LibUsbRequest();
			req.Iface.ID = interfaceID;
			req.Timeout = UsbConstants.DEFAULT_TIMEOUT;
			int ret;
			DeviceIoControl(DeviceHandle, LibUsbIoCtl.RELEASE_INTERFACE, ref req, LibUsbRequest.Size, null, 0, out ret);
		}
		public void SetAltInterface(int interfaceID, int alternateID) {
			LibUsbRequest req = new LibUsbRequest();
			req.Iface.ID = interfaceID;
			req.Iface.AlternateID = alternateID;
			req.Timeout = UsbConstants.DEFAULT_TIMEOUT;
			int ret;
			DeviceIoControl(DeviceHandle, LibUsbIoCtl.SET_INTERFACE, ref req, LibUsbRequest.Size, null, 0, out ret);
		}
		public void ResetDevice() {
			LibUsbRequest req = new LibUsbRequest();
			req.Timeout = UsbConstants.DEFAULT_TIMEOUT;
			int ret;
			DeviceIoControl(DeviceHandle, LibUsbIoCtl.RESET_DEVICE, ref req, LibUsbRequest.Size, null, 0, out ret);
		}
		public unsafe override int GetDescriptor(byte descriptorType, byte index, short langId, byte[] buffer, int offset, int length) {
			if (offset < 0 || length < 0 || offset + length > buffer.Length) throw new ArgumentOutOfRangeException("length", "The specified offset and length exceed the buffer length");
			LibUsbRequest req = new LibUsbRequest();
			req.Descriptor.Index = index;
			req.Descriptor.LangID = langId;
			req.Descriptor.Recipient = (byte)UsbEndpointDirection.EndpointIn & 0x1F;
			req.Descriptor.Type = descriptorType;
			req.Timeout = UsbConstants.DEFAULT_TIMEOUT;
			int ret;
			fixed (Byte* b = buffer) {
				DeviceIoControl(DeviceHandle, LibUsbIoCtl.GET_DESCRIPTOR, ref req, LibUsbRequest.Size, (IntPtr)(b + offset), length, out ret);
			}
			return ret;
		}
		public override int BulkRead(byte endpoint, byte[] buffer, int offset, int length) {
			return PipeTransfer(endpoint, false, false, buffer, offset, length, 0);
		}
		public override int BulkWrite(byte endpoint, byte[] buffer, int offset, int length) {
			return PipeTransfer(endpoint, true, false, buffer, offset, length, 0);
		}
		public override int InterruptRead(byte endpoint, byte[] buffer, int offset, int length) {
			return PipeTransfer(endpoint, false, false, buffer, offset, length, 0);
		}
		public override int InterruptWrite(byte endpoint, byte[] buffer, int offset, int length) {
			return PipeTransfer(endpoint, true, false, buffer, offset, length, 0);
		}
		public unsafe override int ControlRead(UsbControlRequestType requestType, byte request, short value, short index, byte[] buffer, int offset, int length) {
			if (buffer == null) buffer = new Byte[0];
			if (offset < 0 || length < 0 || offset + length > buffer.Length) throw new ArgumentOutOfRangeException("length", "The specified offset and length exceed the buffer length");
			int code;
			LibUsbRequest req = new LibUsbRequest();
			PrepareControlTransfer(requestType, request, value, index, length, ref req, out code);
			int ret;
			fixed (Byte* b = buffer) {
				DeviceIoControl(DeviceHandle, code, ref req, LibUsbRequest.Size, (IntPtr)(b + offset), length, out ret);
			}
			return ret;
		}
		public unsafe override int ControlWrite(UsbControlRequestType requestType, byte request, short value, short index, byte[] buffer, int offset, int length) {
			Byte[] inbuffer = new Byte[length + LibUsbRequest.Size];
			if (length > 0) Buffer.BlockCopy(buffer, offset, inbuffer, LibUsbRequest.Size, length);
			int code;
			fixed (Byte* inbufferp = inbuffer)
				PrepareControlTransfer(requestType, request, value, index, length, ref *((LibUsbRequest*)inbufferp), out code);
			int ret;
			DeviceIoControl(DeviceHandle, code, inbuffer, length + LibUsbRequest.Size, null, 0, out ret);
			return length;
			//ret -= LibUsbRequest.Size;
			//if (ret <= 0) return 0;
			//return ret;
		}
		void PrepareControlTransfer(UsbControlRequestType requestType, byte request, short value, short index, int length, ref LibUsbRequest req, out int code) {
			code = LibUsbIoCtl.CONTROL_TRANSFER;
			req.Timeout = UsbConstants.DEFAULT_TIMEOUT;
			req.Control.RequestType = (Byte)requestType;
			req.Control.Request = request;
			req.Control.Value = (ushort)value;
			req.Control.Index = (ushort)index;
			req.Control.Length = (ushort)length;
			switch ((UsbControlRequestType)((int)requestType & (0x03 << 5))) {
				case UsbControlRequestType.TypeStandard:
					switch ((UsbStandardRequest)request) {
						case UsbStandardRequest.GetStatus:
							req.Status.Recipient = (int)requestType & 0x1F;
							req.Status.Index = index;
							code = LibUsbIoCtl.GET_STATUS;
							break;
						case UsbStandardRequest.ClearFeature:
							req.Feature.Recipient = (int)requestType & 0x1F;
							req.Feature.ID = value;
							req.Feature.Index = index;
							code = LibUsbIoCtl.CLEAR_FEATURE;
							break;
						case UsbStandardRequest.SetFeature:
							req.Feature.Recipient = (int)requestType & 0x1F;
							req.Feature.ID = value;
							req.Feature.Index = index;
							code = LibUsbIoCtl.SET_FEATURE;
							break;
						case UsbStandardRequest.GetDescriptor:
							req.Descriptor.Recipient = (int)requestType & 0x1F;
							req.Descriptor.Type = (value >> 8) & 0xFF;
							req.Descriptor.Index = value & 0xFF;
							req.Descriptor.LangID = index;
							code = LibUsbIoCtl.GET_DESCRIPTOR;
							break;
						case UsbStandardRequest.SetDescriptor:
							req.Descriptor.Recipient = (int)requestType & 0x1F;
							req.Descriptor.Type = (value >> 8) & 0xFF;
							req.Descriptor.Index = value & 0xFF;
							req.Descriptor.LangID = index;
							code = LibUsbIoCtl.SET_DESCRIPTOR;
							break;
						case UsbStandardRequest.GetConfiguration:
							code = LibUsbIoCtl.GET_CONFIGURATION;
							break;
						case UsbStandardRequest.SetConfiguration:
							req.Config.ID = value;
							code = LibUsbIoCtl.SET_CONFIGURATION;
							break;
						case UsbStandardRequest.GetInterface:
							req.Iface.ID = index;
							code = LibUsbIoCtl.GET_INTERFACE;
							break;
						case UsbStandardRequest.SetInterface:
							req.Iface.ID = index;
							req.Iface.AlternateID = value;
							code = LibUsbIoCtl.SET_INTERFACE;
							break;
						default:
							throw new ArgumentException(String.Format("Invalid request: 0x{0:X8}", request));
					}
					break;
				case UsbControlRequestType.TypeVendor:
				case UsbControlRequestType.TypeClass:
					req.Vendor.Type = ((byte)requestType >> 5) & 0x03;
					req.Vendor.Recipient = (int)requestType & 0x1F;
					req.Vendor.Request = (int)request;
					req.Vendor.ID = value;
					req.Vendor.Index = index;
					code = ((byte)requestType & 0x80) != 0 ? LibUsbIoCtl.VENDOR_READ : LibUsbIoCtl.VENDOR_WRITE;
					break;
				case UsbControlRequestType.TypeReserved:
				default:
					throw new ArgumentException(String.Format("Invalid or unsupported request type: 0x{0:X8}", requestType));
			}
		}

		unsafe int PipeTransfer(Byte epnum, Boolean write, Boolean isochronous, Byte[] buffer, int offset, int length, int packetsize) {
			if (offset < 0 || length < 0 || offset + length > buffer.Length) throw new ArgumentOutOfRangeException("length", "The specified offset and length exceed the buffer length");
			LibUsbRequest req = new LibUsbRequest();
			req.Endpoint.ID = epnum;
			req.Endpoint.PacketSize = packetsize;
			req.Timeout = UsbConstants.DEFAULT_TIMEOUT;
			fixed (Byte* b = buffer) {
				if (write) {
					int cltCode = isochronous ? LibUsbIoCtl.ISOCHRONOUS_WRITE : LibUsbIoCtl.INTERRUPT_OR_BULK_WRITE;
					int transfered = 0;
					while (length > 0) {
						int ret;
						DeviceIoControl(DeviceHandle, cltCode, ref req, LibUsbRequest.Size, (IntPtr)(b + offset), Math.Min(Int16.MaxValue, length), out ret);
						if (ret <= 0) throw new System.IO.EndOfStreamException();
						length -= ret;
						offset += ret;
						transfered += ret;
					}
					return transfered;
				} else {
					int cltCode = isochronous ? LibUsbIoCtl.ISOCHRONOUS_READ : LibUsbIoCtl.INTERRUPT_OR_BULK_READ;
					int ret;
					DeviceIoControl(DeviceHandle, cltCode, ref req, LibUsbRequest.Size, (IntPtr)(b + offset), Math.Min(UInt16.MaxValue, length), out ret);
					return ret;
				}
			}
		}
		public void PipeReset(byte pipeID) {
			LibUsbRequest req = new LibUsbRequest();
			req.Endpoint.ID = pipeID;
			req.Timeout = UsbConstants.DEFAULT_TIMEOUT;
			int ret;
			DeviceIoControl(DeviceHandle, LibUsbIoCtl.RESET_ENDPOINT, ref req, LibUsbRequest.Size, null, 0, out ret);
		}

		private unsafe void DeviceIoControl(SafeHandle hDevice, int IoControlCode, [In] ref LibUsbRequest InBuffer, int nInBufferSize, Byte[] OutBuffer, int nOutBufferSize, out int pBytesReturned) {
			fixed (LibUsbRequest* InBufferPtr = &InBuffer) {
				fixed (Byte* OutBufferPtr = OutBuffer) {
					DeviceIoControl(hDevice, IoControlCode, (IntPtr)InBufferPtr, nInBufferSize, (IntPtr)OutBufferPtr, nOutBufferSize, out pBytesReturned);
				}
			}
		}
		private unsafe void DeviceIoControl(SafeHandle hDevice, int IoControlCode, Byte[] InBuffer, int nInBufferSize, Byte[] OutBuffer, int nOutBufferSize, out int pBytesReturned) {
			fixed (Byte* InBufferPtr = InBuffer, OutBufferPtr = OutBuffer) {
				DeviceIoControl(hDevice, IoControlCode, (IntPtr)InBufferPtr, nInBufferSize, (IntPtr)OutBufferPtr, nOutBufferSize, out pBytesReturned);
			}
		}
		private unsafe void DeviceIoControl(SafeHandle hDevice, int IoControlCode, [In] ref LibUsbRequest InBuffer, int nInBufferSize, IntPtr OutBuffer, int nOutBufferSize, out int pBytesReturned) {
			fixed (LibUsbRequest* InBufferPtr = &InBuffer) {
				DeviceIoControl(hDevice, IoControlCode, (IntPtr)InBufferPtr, nInBufferSize, OutBuffer, nOutBufferSize, out pBytesReturned);
			}
		}
		private unsafe void DeviceIoControl(SafeHandle hDevice, int IoControlCode, IntPtr InBuffer, int nInBufferSize, IntPtr OutBuffer, int nOutBufferSize, out int pBytesReturned) {
			using (ManualResetEvent evt = new ManualResetEvent(false)) {
				NativeOverlapped overlapped = new NativeOverlapped();
				overlapped.EventHandle = evt.SafeWaitHandle.DangerousGetHandle();
				if (Kernel32.DeviceIoControl(hDevice, IoControlCode, InBuffer, nInBufferSize, OutBuffer, nOutBufferSize, out pBytesReturned, &overlapped))
					return;
				int err = Marshal.GetLastWin32Error();
				if (err != 997) throw new Win32Exception(err);
				evt.WaitOne();
				if (!Kernel32.GetOverlappedResult(hDevice, &overlapped, out pBytesReturned, false))
					throw new Win32Exception(Marshal.GetLastWin32Error());
			}
		}
	}
}