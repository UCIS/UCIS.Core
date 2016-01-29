using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using UCIS.USBLib.Internal.Windows;

namespace UCIS.USBLib.Communication.LibUsb {
	public class LibUsb0Device : UsbInterface, IUsbDevice {
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
			if (DeviceHandle.IsInvalid || DeviceHandle.IsClosed) throw new Win32Exception();
			ThreadPool.BindHandle(DeviceHandle);
		}
		protected override void Dispose(Boolean disposing) {
			if (disposing && DeviceHandle != null) DeviceHandle.Close();
		}

		public override Byte Configuration {
			get { return base.Configuration; }
			set {
				ControlTransfer(
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
			DeviceIoControl(LibUsbIoCtl.CLAIM_INTERFACE, ref req);
		}
		public void ReleaseInterface(int interfaceID) {
			LibUsbRequest req = new LibUsbRequest();
			req.Iface.ID = interfaceID;
			req.Timeout = UsbConstants.DEFAULT_TIMEOUT;
			DeviceIoControl(LibUsbIoCtl.RELEASE_INTERFACE, ref req);
		}
		public void SetAltInterface(int interfaceID, int alternateID) {
			LibUsbRequest req = new LibUsbRequest();
			req.Iface.ID = interfaceID;
			req.Iface.AlternateID = alternateID;
			req.Timeout = UsbConstants.DEFAULT_TIMEOUT;
			DeviceIoControl(LibUsbIoCtl.SET_INTERFACE, ref req);
		}
		public void ResetDevice() {
			LibUsbRequest req = new LibUsbRequest();
			req.Timeout = UsbConstants.DEFAULT_TIMEOUT;
			DeviceIoControl(LibUsbIoCtl.RESET_DEVICE, ref req);
		}
		public unsafe override int GetDescriptor(byte descriptorType, byte index, short langId, byte[] buffer, int offset, int length) {
			if (offset < 0 || length < 0 || offset + length > buffer.Length) throw new ArgumentOutOfRangeException("length", "The specified offset and length exceed the buffer length");
			LibUsbRequest req = new LibUsbRequest();
			req.Descriptor.Index = index;
			req.Descriptor.LangID = langId;
			req.Descriptor.Recipient = (byte)UsbEndpointDirection.EndpointIn & 0x1F;
			req.Descriptor.Type = descriptorType;
			req.Timeout = UsbConstants.DEFAULT_TIMEOUT;
			return DeviceIoControl(LibUsbIoCtl.GET_DESCRIPTOR, ref req, buffer, offset, length);
		}
		public override int ControlTransfer(UsbControlRequestType requestType, byte request, short value, short index, byte[] buffer, int offset, int length) {
			int ret = EndDeviceIoControl(BeginControlTransfer(requestType, request, value, index, buffer, offset, length, null, null));
			if ((requestType & UsbControlRequestType.EndpointMask) == UsbControlRequestType.EndpointOut) ret = length;
			return ret;
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
		public override unsafe IAsyncResult BeginControlTransfer(UsbControlRequestType requestType, byte request, short value, short index, byte[] buffer, int offset, int length, AsyncCallback callback, Object state) {
			if (offset < 0 || length < 0 || (buffer == null && length != 0) || offset + length > buffer.Length) throw new ArgumentOutOfRangeException("length", "The specified offset and length exceed the buffer length");
			int code;
			Byte[] inbuffer;
			if ((requestType & UsbControlRequestType.EndpointMask) == UsbControlRequestType.EndpointIn) {
				if (buffer == null) buffer = new Byte[0];
				inbuffer = new Byte[sizeof(LibUsbRequest)];
			} else {
				inbuffer = new Byte[length + LibUsbRequest.Size];
				if (length > 0) Buffer.BlockCopy(buffer, offset, inbuffer, LibUsbRequest.Size, length);
				buffer = null;
				offset = length = 0;
			}
			fixed (Byte* inbufferp = inbuffer) PrepareControlTransfer(requestType, request, value, index, length, ref *((LibUsbRequest*)inbufferp), out code);
			return BeginDeviceIoControl(code, inbuffer, buffer, offset, length, callback, state);
		}
		public override int EndControlTransfer(IAsyncResult asyncResult) {
			return EndDeviceIoControl(asyncResult);
		}

		public override int PipeTransfer(Byte epnum, Byte[] buffer, int offset, int length) {
			return PipeTransfer(epnum, (epnum & (Byte)UsbControlRequestType.EndpointMask) == (Byte)UsbControlRequestType.EndpointOut, false, buffer, offset, length, 0);
		}
		unsafe int PipeTransfer(Byte epnum, Boolean write, Boolean isochronous, Byte[] buffer, int offset, int length, int packetsize) {
			if (offset < 0 || length < 0 || offset + length > buffer.Length) throw new ArgumentOutOfRangeException("length", "The specified offset and length exceed the buffer length");
			LibUsbRequest req = new LibUsbRequest();
			req.Endpoint.ID = epnum;
			req.Endpoint.PacketSize = packetsize;
			req.Timeout = UsbConstants.DEFAULT_TIMEOUT;
			if (write) {
				int cltCode = isochronous ? LibUsbIoCtl.ISOCHRONOUS_WRITE : LibUsbIoCtl.INTERRUPT_OR_BULK_WRITE;
				int transfered = 0;
				while (length > 0) {
					int ret = DeviceIoControl(cltCode, ref req, buffer, offset, length);
					if (ret <= 0) throw new System.IO.EndOfStreamException();
					length -= ret;
					offset += ret;
					transfered += ret;
				}
				return transfered;
			} else {
				int cltCode = isochronous ? LibUsbIoCtl.ISOCHRONOUS_READ : LibUsbIoCtl.INTERRUPT_OR_BULK_READ;
				return DeviceIoControl(cltCode, ref req, buffer, offset, length);
			}
		}
		public override unsafe IAsyncResult BeginPipeTransfer(Byte epnum, Byte[] buffer, int offset, int length, AsyncCallback callback, Object state) {
			if (offset < 0 || length < 0 || offset + length > buffer.Length) throw new ArgumentOutOfRangeException("length", "The specified offset and length exceed the buffer length");
			Byte[] reqb = new Byte[LibUsbRequest.Size];
			fixed (Byte* reqptr = reqb) {
				LibUsbRequest* req = (LibUsbRequest*)reqptr;
				req->Endpoint.ID = epnum;
				req->Endpoint.PacketSize = 0;
				req->Timeout = UsbConstants.DEFAULT_TIMEOUT;
			}
			int code = ((epnum & (Byte)UsbControlRequestType.EndpointMask) == (Byte)UsbControlRequestType.EndpointOut) ? LibUsbIoCtl.INTERRUPT_OR_BULK_WRITE : LibUsbIoCtl.INTERRUPT_OR_BULK_READ;
			return BeginDeviceIoControl(code, reqb, buffer, offset, length, callback, state);
		}
		public override int EndPipeTransfer(IAsyncResult asyncResult) {
			return EndDeviceIoControl(asyncResult);
		}
		public override void PipeReset(byte pipeID) {
			LibUsbRequest req = new LibUsbRequest();
			req.Endpoint.ID = pipeID;
			req.Timeout = UsbConstants.DEFAULT_TIMEOUT;
			DeviceIoControl(LibUsbIoCtl.RESET_ENDPOINT, ref req);
		}
		public override void PipeAbort(byte pipeID) {
			LibUsbRequest req = new LibUsbRequest();
			req.Endpoint.ID = pipeID;
			req.Timeout = UsbConstants.DEFAULT_TIMEOUT;
			DeviceIoControl(LibUsbIoCtl.ABORT_ENDPOINT, ref req);
		}

		void DeviceIoControl(int code, [In] ref LibUsbRequest request) {
			DeviceIoControl(code, ref request, null, 0, 0);
		}
		unsafe int DeviceIoControl(int code, [In] ref LibUsbRequest request, Byte[] outbuffer, int outoffset, int outsize) {
			Byte[] bytes = new Byte[LibUsbRequest.Size];
			fixed (Byte* ptr = bytes) *(LibUsbRequest*)ptr = request;
			return DeviceIoControl(code, bytes, outbuffer, outoffset, outsize);
		}
		unsafe int DeviceIoControl(int code, Byte[] inbuffer, Byte[] outbuffer, int outoffset, int outsize) {
			return EndDeviceIoControl(BeginDeviceIoControl(code, inbuffer, outbuffer, outoffset, outsize, null, null));
		}
		unsafe WindowsOverlappedAsyncResult BeginDeviceIoControl(int code, Byte[] inbuffer, Byte[] outbuffer, int outoffset, int outsize, AsyncCallback callback, Object state) {
			WindowsOverlappedAsyncResult ar = new WindowsOverlappedAsyncResult(callback, state);
			try {
				fixed (Byte* inptr = inbuffer, outptr = outbuffer) {
					int ret;
					Boolean success = Kernel32.DeviceIoControl(DeviceHandle, code, inptr, inbuffer.Length, outptr + outoffset, outsize, out ret, ar.PackOverlapped(new Object[] { inbuffer, outbuffer }));
					ar.SyncResult(success, ret);
				}
				return ar;
			} catch {
				ar.ErrorCleanup();
				throw;
			}
		}
		int EndDeviceIoControl(IAsyncResult ar) {
			return ((WindowsOverlappedAsyncResult)ar).Complete();
		}
	}
}
