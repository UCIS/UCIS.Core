using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using LibUsbDotNet.Descriptors;
using LibUsbDotNet.Info;
using LibUsbDotNet.Main;
using UCIS.USBLib.Communication;
using UCIS.USBLib.Descriptor;
using LibUsb0Registry = UCIS.USBLib.Communication.LibUsb.LibUsb0Registry;
using LibUsb1Registry = UCIS.USBLib.Communication.LibUsb1.LibUsb1Registry;
using nIUsbDevice = UCIS.USBLib.Communication.IUsbDevice;
using nIUsbInterface = UCIS.USBLib.Communication.IUsbInterface;
using WinUsbRegistry = UCIS.USBLib.Communication.WinUsb.WinUsbRegistry;
using USBIORegistry = UCIS.USBLib.Communication.USBIO.USBIORegistry;

namespace LibUsbDotNet {
	public class UsbDevice {
		public nIUsbInterface Device { get; private set; }
		public UsbDevice(nIUsbInterface dev) {
			if (dev == null) throw new ArgumentNullException("dev");
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
					foreach (IUsbDeviceRegistry reg in USBIORegistry.DeviceList) list.Add(new UsbRegistry(reg));
				} else {
					foreach (IUsbDeviceRegistry reg in LibUsb1Registry.DeviceList) list.Add(new UsbRegistry(reg));
				}
				return list;
			}
		}
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
				if (dev.Configuration == 0) dev.Configuration = 1;
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
				int iConfigs = Info.Descriptor.NumConfigurations;
				for (Byte iConfig = 0; iConfig < iConfigs; iConfig++) {
					UsbConfigurationDescriptor configDescriptor = UsbConfigurationDescriptor.FromDevice(Device, iConfig);
					if (configDescriptor.Length < UsbConfigurationDescriptor.Size || configDescriptor.Type != UsbDescriptorType.Configuration)
						throw new Exception("GetDeviceConfigs: USB config descriptor is invalid.");
					Byte[] cfgBuffer = new Byte[configDescriptor.TotalLength];
					int iBytesTransmitted;
					if (!GetDescriptor((byte)UsbDescriptorType.Configuration, (byte)iConfig, 0, cfgBuffer, cfgBuffer.Length, out iBytesTransmitted))
						throw new Exception("Could not read configuration descriptor");
					configDescriptor = UsbConfigurationDescriptor.FromByteArray(cfgBuffer, 0, iBytesTransmitted);
					if (configDescriptor.Length < UsbConfigurationDescriptor.Size || configDescriptor.Type != UsbDescriptorType.Configuration)
						throw new Exception("GetDeviceConfigs: USB config descriptor is invalid.");
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
					rtnConfigs.Add(new UsbConfigInfo(this, configDescriptor, rawDescriptorList));
				}
				return rtnConfigs;
			}
		}
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
			mDeviceDescriptor = UsbDeviceDescriptor.FromDevice(usbDevice.Device);
		}
		public UsbDeviceDescriptor Descriptor { get { return mDeviceDescriptor; } }
		public String ManufacturerString {
			get {
				if (Descriptor.ManufacturerStringID == 0) return null;
				return UsbStringDescriptor.GetStringFromDevice(mUsbDevice.Device, Descriptor.ManufacturerStringID, 0);
			}
		}
		public String ProductString {
			get {
				if (Descriptor.ProductStringID == 0) return null;
				return UsbStringDescriptor.GetStringFromDevice(mUsbDevice.Device, Descriptor.ProductStringID, 0);
			}
		}
		public String SerialString {
			get {
				if (Descriptor.SerialNumberStringID == 0) return null;
				return UsbStringDescriptor.GetStringFromDevice(mUsbDevice.Device, Descriptor.SerialNumberStringID, 0);
			}
		}
	}
	public class UsbConfigInfo {
		private readonly List<UsbInterfaceInfo> mInterfaceList = new List<UsbInterfaceInfo>();
		internal readonly UsbConfigurationDescriptor mUsbConfigDescriptor;
		internal UsbDevice mUsbDevice;
		internal UsbConfigInfo(UsbDevice usbDevice, UsbConfigurationDescriptor descriptor, IEnumerable<byte[]> rawDescriptors) {
			mUsbDevice = usbDevice;
			mUsbConfigDescriptor = descriptor;
			UsbInterfaceInfo currentInterface = null;
			foreach (Byte[] bytesRawDescriptor in rawDescriptors) {
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
		public UsbConfigurationDescriptor Descriptor { get { return mUsbConfigDescriptor; } }
		public ReadOnlyCollection<UsbInterfaceInfo> InterfaceInfoList { get { return mInterfaceList.AsReadOnly(); } }
	}
	public class UsbInterfaceInfo {
		internal readonly UsbInterfaceDescriptor mUsbInterfaceDescriptor;
		internal UsbDevice mUsbDevice;
		internal List<UsbEndpointInfo> mEndpointInfo = new List<UsbEndpointInfo>();
		internal UsbInterfaceInfo(UsbDevice usbDevice, byte[] descriptor) {
			mUsbDevice = usbDevice;
			mUsbInterfaceDescriptor = UsbInterfaceDescriptor.FromByteArray(descriptor, 0, descriptor.Length);
		}
		public UsbInterfaceDescriptor Descriptor { get { return mUsbInterfaceDescriptor; } }
		public ReadOnlyCollection<UsbEndpointInfo> EndpointInfoList { get { return mEndpointInfo.AsReadOnly(); } }
	}
	public class UsbEndpointInfo {
		internal UsbEndpointDescriptor mUsbEndpointDescriptor;
		internal UsbEndpointInfo(byte[] descriptor) {
			mUsbEndpointDescriptor = UsbEndpointDescriptor.FromByteArray(descriptor, 0, descriptor.Length);
		}
		public UsbEndpointDescriptor Descriptor {
			get { return mUsbEndpointDescriptor; }
		}
	}
}
namespace LibUsbDotNet.Descriptors {
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
}
