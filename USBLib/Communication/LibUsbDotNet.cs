using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text;
using System.IO;
using LibUsbDotNet.Main;
using LibUsbDotNet.Info;
using LibUsbDotNet.Descriptors;
using UCIS.USBLib.Communication;
using LibUsb0Registry = UCIS.USBLib.Communication.LibUsb.LibUsb0Registry;
using LibUsb1Registry = UCIS.USBLib.Communication.LibUsb1.LibUsb1Registry;
using nIUsbDevice = UCIS.USBLib.Communication.IUsbDevice;
using nIUsbInterface = UCIS.USBLib.Communication.IUsbInterface;
using WinUsbRegistry = UCIS.USBLib.Communication.WinUsb.WinUsbRegistry;

namespace LibUsbDotNet {
	public class UsbDevice : IUsbDevice {
		public nIUsbInterface Device { get; private set; }
		public UsbDevice(nIUsbInterface dev) {
			Device = dev;
		}
		public bool GetDescriptor(byte descriptorType, byte index, short langId, Byte[] buffer, int bufferLength, out int transferLength) {
			try {
				transferLength = Device.GetDescriptor(descriptorType, index, langId, buffer, 0, bufferLength);
				return true;
			} catch {
				transferLength = 0;
				return false;
			}
		}
		public bool ControlTransfer(ref UsbSetupPacket setupPacket, Byte[] buffer, int bufferLength, out int lengthTransferred) {
			if ((setupPacket.RequestType & 128) != 0) {
				lengthTransferred = Device.ControlRead((UsbControlRequestType)setupPacket.RequestType, setupPacket.Request, setupPacket.Value, setupPacket.Index, buffer, 0, bufferLength);
			} else {
				lengthTransferred = Device.ControlWrite((UsbControlRequestType)setupPacket.RequestType, setupPacket.Request, setupPacket.Value, setupPacket.Index, buffer, 0, bufferLength);
			}
			return true;
		}
		public DriverModeType DriverMode { get { return DriverModeType.Unknown; } }
		public enum DriverModeType {
			Unknown,
			LibUsb,
			WinUsb,
			MonoLibUsb,
			LibUsbWinBack
		}
		public UsbEndpointWriter OpenEndpointWriter(WriteEndpointID writeEndpointID, EndpointType endpointType) {
			return new UsbEndpointWriter(Device, (Byte)writeEndpointID, endpointType);
		}
		public UsbEndpointReader OpenEndpointReader(ReadEndpointID readEndpointID, int buffersize, EndpointType endpointType) {
			UsbEndpointReader reader = new UsbEndpointReader(Device, (Byte)readEndpointID, endpointType);
			reader.ReadBufferSize = buffersize;
			return reader;
		}
		public void Close() {
			Device.Dispose();
		}
		public UsbDeviceInfo Info { get { return new UsbDeviceInfo(this); } }
		public static IList<UsbRegistry> AllDevices {
			get {
				List<UsbRegistry> list = new List<UsbRegistry>();
				if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
					foreach (IUsbDeviceRegistry reg in WinUsbRegistry.DeviceList) list.Add(new UsbRegistry(reg));
					foreach (IUsbDeviceRegistry reg in LibUsb0Registry.DeviceList) list.Add(new UsbRegistry(reg));
				} else {
					foreach (IUsbDeviceRegistry reg in LibUsb1Registry.DeviceList) list.Add(new UsbRegistry(reg));
				}
				return list;
			}
		}
		private SafeHandle Handle { get { return null; } }
		public bool SetConfiguration(byte config) {
			nIUsbDevice dev = Device as nIUsbDevice;
			if (dev == null) return false;
			try {
				dev.Configuration = config;
				return true;
			} catch {
				return false;
			}
		}
		public bool ClaimInterface(int interfaceID) {
			nIUsbDevice dev = Device as nIUsbDevice;
			if (dev == null) return false;
			try {
				dev.ClaimInterface(interfaceID);
				return true;
			} catch {
				return false;
			}
		}
		public bool ReleaseInterface(int interfaceID) {
			nIUsbDevice dev = Device as nIUsbDevice;
			if (dev == null) return false;
			try {
				dev.ReleaseInterface(interfaceID);
				return true;
			} catch {
				return false;
			}
		}
		public IList<UsbConfigInfo> Configs {
			get {
				List<UsbConfigInfo> rtnConfigs = new List<UsbConfigInfo>();
				byte[] cfgBuffer = new byte[UsbConstants.MAX_CONFIG_SIZE];
				int iConfigs = Info.Descriptor.ConfigurationCount;
				for (int iConfig = 0; iConfig < iConfigs; iConfig++) {
					int iBytesTransmitted;
					if (!GetDescriptor((byte)UsbDescriptorType.Configuration, 0, 0, cfgBuffer, cfgBuffer.Length, out iBytesTransmitted))
						throw new Exception("Could not read configuration descriptor");
					if (iBytesTransmitted < UsbConfigDescriptor.Size || cfgBuffer[1] != (byte)UsbDescriptorType.Configuration)
						throw new Exception("GetDeviceConfigs: USB config descriptor is invalid.");
					UsbConfigDescriptor configDescriptor = new UsbConfigDescriptor();
					Helper.BytesToObject(cfgBuffer, 0, Math.Min(UsbConfigDescriptor.Size, cfgBuffer[0]), configDescriptor);
					if (configDescriptor.TotalLength != iBytesTransmitted) throw new Exception("GetDeviceConfigs: USB config descriptor length doesn't match the length received.");
					List<byte[]> rawDescriptorList = new List<byte[]>();
					int iRawLengthPosition = configDescriptor.Length;
					while (iRawLengthPosition < configDescriptor.TotalLength) {
						byte[] rawDescriptor = new byte[cfgBuffer[iRawLengthPosition]];
						if (iRawLengthPosition + rawDescriptor.Length > iBytesTransmitted) throw new Exception("Descriptor length is out of range.");
						Array.Copy(cfgBuffer, iRawLengthPosition, rawDescriptor, 0, rawDescriptor.Length);
						rawDescriptorList.Add(rawDescriptor);
						iRawLengthPosition += rawDescriptor.Length;
					}
					rtnConfigs.Add(new UsbConfigInfo(this, configDescriptor, ref rawDescriptorList));
				}
				return rtnConfigs;
			}
		}
	}
	public interface IUsbDevice {
		bool SetConfiguration(byte config);
		bool ClaimInterface(int interfaceID);
		bool ReleaseInterface(int interfaceID);
	}
	public class UsbEndpointWriter : IDisposable {
		public nIUsbInterface Device { get; private set; }
		public Byte EndpointID { get; private set; }
		public EndpointType EndpointType { get; private set; }
		public Byte EpNum { get { return EndpointID; } }
		public UsbEndpointWriter(nIUsbInterface dev, byte epid, EndpointType eptype) {
			Device = dev;
			EndpointID = epid;
			EndpointType = eptype;
		}
		public ErrorCode Write(byte[] buffer, int offset, int count, int timeout, out int transferLength) {
			switch (EndpointType) {
				case EndpointType.Bulk: transferLength = Device.BulkWrite(EndpointID, buffer, offset, count); break;
				case EndpointType.Interrupt: transferLength = Device.InterruptWrite(EndpointID, buffer, offset, count); break;
				default: transferLength = 0; return ErrorCode.Error;
			}
			return ErrorCode.Ok;
		}
		public void Dispose() { }
	}
	public class UsbEndpointReader : IDisposable {
		public nIUsbInterface Device { get; private set; }
		public Byte EndpointID { get; private set; }
		public EndpointType EndpointType { get; private set; }
		public Byte EpNum { get { return EndpointID; } }
		public int ReadBufferSize { get; set; }

		public UsbEndpointReader(nIUsbInterface dev, byte epid, EndpointType eptype) {
			Device = dev;
			EndpointID = epid;
			EndpointType = eptype;
			ReadBufferSize = 4096;
		}
		public ErrorCode Read(byte[] buffer, int offset, int count, int timeout, out int transferLength) {
			switch (EndpointType) {
				case EndpointType.Bulk: transferLength = Device.BulkRead(EndpointID, buffer, offset, count); break;
				case EndpointType.Interrupt: transferLength = Device.InterruptRead(EndpointID, buffer, offset, count); break;
				default: transferLength = 0; return ErrorCode.Error;
			}
			return ErrorCode.Ok;
		}
		public void Dispose() { DataReceivedEnabled = false; }

		private bool mDataReceivedEnabled;
		private Thread mReadThread;

		public virtual bool DataReceivedEnabled {
			get { return mDataReceivedEnabled; }
			set {
				if (value != mDataReceivedEnabled) {
					if (mDataReceivedEnabled) {
						mReadThread.Abort();
					} else {
						mDataReceivedEnabled = true;
						mReadThread = new Thread(ReadDataProcess);
						mReadThread.Start();
					}
				}
			}
		}

		private void ReadDataProcess(object state) {
			byte[] buffer = new byte[ReadBufferSize];
			try {
				while (mDataReceivedEnabled) {
					int transferLength;
					Read(buffer, 0, buffer.Length, -1, out transferLength);
					EventHandler<EndpointDataEventArgs> eh = DataReceived;
					if (!ReferenceEquals(eh, null)) eh(this, new EndpointDataEventArgs(buffer, transferLength));
				}
			} catch (Exception ex) {
				if (ReadError != null) ReadError(this, new ErrorEventArgs(ex));
			} finally {
				mDataReceivedEnabled = false;
			}
		}

		public event EventHandler<EndpointDataEventArgs> DataReceived;
		public event ErrorEventHandler ReadError;
	}
}
namespace LibUsbDotNet.Main {
	public static class UsbConstants {
		public const int MAX_CONFIG_SIZE = 4096;
		public const int MAX_DEVICES = 128;
		public const int MAX_ENDPOINTS = 32;
		public const byte ENDPOINT_DIR_MASK = 0x80;
		public const byte ENDPOINT_NUMBER_MASK = 0xf;
	}
	public static class Helper {
		public static void BytesToObject(byte[] sourceBytes, int iStartIndex, int iLength, object destObject) {
			GCHandle gch = GCHandle.Alloc(destObject, GCHandleType.Pinned);
			IntPtr ptrDestObject = gch.AddrOfPinnedObject();
			Marshal.Copy(sourceBytes, iStartIndex, ptrDestObject, iLength);
			gch.Free();
		}
		public static string ToString(string sep0, string[] names, string sep1, object[] values, string sep2) {
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < names.Length; i++) sb.Append(sep0 + names[i] + sep1 + values[i] + sep2);
			return sb.ToString();
		}
	}
	public class EndpointDataEventArgs : EventArgs {
		internal EndpointDataEventArgs(byte[] bytes, int size) {
			Buffer = bytes;
			Count = size;
		}
		public byte[] Buffer { get; private set; }
		public int Count { get; private set; }
	}
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct UsbSetupPacket {
		public byte RequestType;
		public byte Request;
		public short Value;
		public short Index;
		public short Length;
		public UsbSetupPacket(byte requestType, byte request, short value, short index, short length) {
			RequestType = requestType;
			Request = request;
			Value = value;
			Index = index;
			Length = length;
		}
	}
	public enum UsbEndpointDirection : byte {
		EndpointIn = 0x80,
		EndpointOut = 0x00,
	}
	public enum UsbRequestType : byte {
		TypeClass = (0x01 << 5),
		TypeReserved = (0x03 << 5),
		TypeStandard = (0x00 << 5),
		TypeVendor = (0x02 << 5),
	}
	public enum WriteEndpointID : byte {
		Ep01 = 0x01,
		Ep02 = 0x02,
		Ep03 = 0x03,
		Ep04 = 0x04,
		Ep05 = 0x05,
		Ep06 = 0x06,
		Ep07 = 0x07,
		Ep08 = 0x08,
		Ep09 = 0x09,
		Ep10 = 0x0A,
		Ep11 = 0x0B,
		Ep12 = 0x0C,
		Ep13 = 0x0D,
		Ep14 = 0x0E,
		Ep15 = 0x0F,
	}
	public enum ReadEndpointID : byte {
		Ep01 = 0x81,
		Ep02 = 0x82,
		Ep03 = 0x83,
		Ep04 = 0x84,
		Ep05 = 0x85,
		Ep06 = 0x86,
		Ep07 = 0x87,
		Ep08 = 0x88,
		Ep09 = 0x89,
		Ep10 = 0x8A,
		Ep11 = 0x8B,
		Ep12 = 0x8C,
		Ep13 = 0x8D,
		Ep14 = 0x8E,
		Ep15 = 0x8F,
	}
	public enum UsbRequestRecipient : byte {
		RecipDevice = 0x00,
		RecipInterface = 0x01,
		RecipEndpoint = 0x02,
		RecipOther = 0x03,
	}
	public enum EndpointType : byte {
		Control,
		Isochronous,
		Bulk,
		Interrupt
	}
	public enum ErrorCode {
		Ok = 0,
		Error = 1,
	}
	public class UsbRegistry {
		public IUsbDeviceRegistry Registry { get; private set; }
		public UsbRegistry(IUsbDeviceRegistry reg) {
			Registry = reg;
		}
		public int Vid { get { return Registry.Vid; } }
		public int Pid { get { return Registry.Pid; } }
		public int Rev { get { return 0; } }
		public Boolean IsAlive { get { return true; } }
		public Boolean Open(out UsbDevice hand) {
			hand = new UsbDevice(Registry.Open());
			return true;
		}
		public IDictionary<String, Object> DeviceProperties { get { return Registry.DeviceProperties; } }
		public String FullName { get { return Registry.FullName; } }
		public String Name { get { return Registry.Name; } }
		public String SymbolicName { get { return Registry.SymbolicName; } }
	}
}
namespace LibUsbDotNet.Info {
	public class UsbDeviceInfo {
		private readonly UsbDeviceDescriptor mDeviceDescriptor;
		internal UsbDevice mUsbDevice;
		internal UsbDeviceInfo(UsbDevice usbDevice) {
			mUsbDevice = usbDevice;
			mDeviceDescriptor = new UsbDeviceDescriptor();
			Byte[] bytes = new Byte[UsbDeviceDescriptor.Size];
			int ret;
			usbDevice.GetDescriptor((byte)UsbDescriptorType.Device, 0, 0, bytes, UsbDeviceDescriptor.Size, out ret);
			Object asobj = mDeviceDescriptor;
			Helper.BytesToObject(bytes, 0, ret, asobj);
			mDeviceDescriptor = (UsbDeviceDescriptor)asobj;
		}
		public UsbDeviceDescriptor Descriptor { get { return mDeviceDescriptor; } }
		public String ManufacturerString {
			get {
				if (Descriptor.ManufacturerStringIndex == 0) return null;
				return mUsbDevice.Device.GetString(0, Descriptor.ManufacturerStringIndex);
			}
		}
		public String ProductString {
			get {
				if (Descriptor.ProductStringIndex == 0) return null;
				return mUsbDevice.Device.GetString(0, Descriptor.ProductStringIndex);
			}
		}
		public String SerialString {
			get {
				if (Descriptor.SerialStringIndex == 0) return null;
				return mUsbDevice.Device.GetString(0, Descriptor.SerialStringIndex);
			}
		}
	}
	public class UsbConfigInfo {
		private readonly List<UsbInterfaceInfo> mInterfaceList = new List<UsbInterfaceInfo>();
		internal readonly UsbConfigDescriptor mUsbConfigDescriptor;
		internal UsbDevice mUsbDevice;
		internal UsbConfigInfo(UsbDevice usbDevice, UsbConfigDescriptor descriptor, ref List<byte[]> rawDescriptors) {
			mUsbDevice = usbDevice;
			mUsbConfigDescriptor = descriptor;
			UsbInterfaceInfo currentInterface = null;
			for (int iRawDescriptor = 0; iRawDescriptor < rawDescriptors.Count; iRawDescriptor++) {
				byte[] bytesRawDescriptor = rawDescriptors[iRawDescriptor];
				switch (bytesRawDescriptor[1]) {
					case (byte)UsbDescriptorType.Interface:
						currentInterface = new UsbInterfaceInfo(usbDevice, bytesRawDescriptor);
						mInterfaceList.Add(currentInterface);
						break;
					case (byte)UsbDescriptorType.Endpoint:
						if (currentInterface == null) throw new Exception("Recieved and endpoint descriptor before receiving an interface descriptor.");
						currentInterface.mEndpointInfo.Add(new UsbEndpointInfo(bytesRawDescriptor));
						break;
				}
			}
		}
		public UsbConfigDescriptor Descriptor { get { return mUsbConfigDescriptor; } }
		public ReadOnlyCollection<UsbInterfaceInfo> InterfaceInfoList { get { return mInterfaceList.AsReadOnly(); } }
	}
	public class UsbInterfaceInfo {
		internal readonly UsbInterfaceDescriptor mUsbInterfaceDescriptor;
		internal UsbDevice mUsbDevice;
		internal List<UsbEndpointInfo> mEndpointInfo = new List<UsbEndpointInfo>();
		internal UsbInterfaceInfo(UsbDevice usbDevice, byte[] descriptor) {
			mUsbDevice = usbDevice;
			mUsbInterfaceDescriptor = new UsbInterfaceDescriptor();
			Helper.BytesToObject(descriptor, 0, Math.Min(UsbInterfaceDescriptor.Size, descriptor[0]), mUsbInterfaceDescriptor);
		}
		public UsbInterfaceDescriptor Descriptor { get { return mUsbInterfaceDescriptor; } }
		public ReadOnlyCollection<UsbEndpointInfo> EndpointInfoList { get { return mEndpointInfo.AsReadOnly(); } }
	}
	public class UsbEndpointInfo {
		internal UsbEndpointDescriptor mUsbEndpointDescriptor;
		internal UsbEndpointInfo(byte[] descriptor) {
			mUsbEndpointDescriptor = new UsbEndpointDescriptor();
			Helper.BytesToObject(descriptor, 0, Math.Min(UsbEndpointDescriptor.Size, descriptor[0]), mUsbEndpointDescriptor);
		}
		public UsbEndpointDescriptor Descriptor {
			get { return mUsbEndpointDescriptor; }
		}
	}
}
namespace LibUsbDotNet.Descriptors {
	public enum DescriptorType : byte {
		Device = 1,
		Configuration = 2,
		String = 3,
		Interface = 4,
		Endpoint = 5,
		DeviceQualifier = 6,
		OtherSpeedConfiguration = 7,
		InterfacePower = 8,
		OTG = 9,
		Debug = 10,
		InterfaceAssociation = 11,
		Hid = 0x21,
		HidReport = 0x22,
		Physical = 0x23,
		Hub = 0x29
	}
	public enum ClassCodeType : byte {
		PerInterface = 0,
		Audio = 1,
		Comm = 2,
		Hid = 3,
		Printer = 7,
		Ptp = 6,
		MassStorage = 8,
		Hub = 9,
		Data = 10,
		VendorSpec = 0xff
	}
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public abstract class UsbDescriptor {
		public static readonly int Size = Marshal.SizeOf(typeof(UsbDescriptor));
		public byte Length;
		public DescriptorType DescriptorType;
	}
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public class UsbDeviceDescriptor : UsbDescriptor {
		public new static readonly int Size = Marshal.SizeOf(typeof(UsbDeviceDescriptor));
		public short BcdUsb;
		public ClassCodeType Class;
		public byte SubClass;
		public byte Protocol;
		public byte MaxPacketSize0;
		public short VendorID;
		public short ProductID;
		public short BcdDevice;
		public byte ManufacturerStringIndex;
		public byte ProductStringIndex;
		public byte SerialStringIndex;
		public byte ConfigurationCount;
		internal UsbDeviceDescriptor() { }
	}
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public class UsbConfigDescriptor : UsbDescriptor {
		public new static readonly int Size = Marshal.SizeOf(typeof(UsbConfigDescriptor));
		public readonly short TotalLength;
		public readonly byte InterfaceCount;
		public readonly byte ConfigID;
		public readonly byte StringIndex;
		public readonly byte Attributes;
		public readonly byte MaxPower;
		internal UsbConfigDescriptor() { }
	}
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public class UsbInterfaceDescriptor : UsbDescriptor {
		public new static readonly int Size = Marshal.SizeOf(typeof(UsbInterfaceDescriptor));
		public readonly byte InterfaceID;
		public readonly byte AlternateID;
		public readonly byte EndpointCount;
		public readonly ClassCodeType Class;
		public readonly byte SubClass;
		public readonly byte Protocol;
		public readonly byte StringIndex;
		internal UsbInterfaceDescriptor() { }
	}
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public class UsbEndpointDescriptor : UsbDescriptor {
		public new static readonly int Size = Marshal.SizeOf(typeof(UsbEndpointDescriptor));
		public readonly byte EndpointID;
		public readonly byte Attributes;
		public readonly short MaxPacketSize;
		public readonly byte Interval;
		public readonly byte Refresh;
		public readonly byte SynchAddress;
		internal UsbEndpointDescriptor() { }
	}
}
namespace MonoLibUsb {
	public static class MonoUsbApi {
		internal const CallingConvention CC = 0;
		internal const string LIBUSB_DLL = "libusb-1.0.dll";
		[DllImport(LIBUSB_DLL, CallingConvention = CC, SetLastError = false, EntryPoint = "libusb_detach_kernel_driver")]
		public static extern int DetachKernelDriver([In] MonoUsbDeviceHandle deviceHandle, int interfaceNumber);
		public static int ControlTransfer(MonoUsbDeviceHandle deviceHandle, byte requestType, byte request, short value, short index, object data, short dataLength, int timeout) {
			throw new NotImplementedException();
		}
		public static int BulkTransfer(MonoUsbDeviceHandle deviceHandle, byte endpoint, object data, int length, out int actualLength, int timeout) {
			throw new NotImplementedException();
		}
	}
	public abstract class MonoUsbDeviceHandle : SafeHandle {
		public MonoUsbDeviceHandle(Boolean bOwnsHandle) : base(IntPtr.Zero, bOwnsHandle) { }
	}
}
