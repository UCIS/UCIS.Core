using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using UCIS.HWLib.Windows.Devices;
using UCIS.USBLib.Communication;
using UCIS.USBLib.Communication.WinUsb;
using UCIS.USBLib.Internal.Windows;

namespace UCIS.USBLib.Communication.VBoxUSB {
	enum USBFILTERTYPE : int {
		USBFILTERTYPE_INVALID = 0,
		USBFILTERTYPE_FIRST,
		USBFILTERTYPE_ONESHOT_IGNORE = USBFILTERTYPE_FIRST,
		USBFILTERTYPE_ONESHOT_CAPTURE,
		USBFILTERTYPE_IGNORE,
		USBFILTERTYPE_CAPTURE,
		USBFILTERTYPE_END,
	}
	enum USBFILTERMATCH : ushort {
		USBFILTERMATCH_INVALID = 0,
		USBFILTERMATCH_IGNORE,
		USBFILTERMATCH_PRESENT,
		USBFILTERMATCH_NUM_FIRST,
		USBFILTERMATCH_NUM_EXACT = USBFILTERMATCH_NUM_FIRST,
		USBFILTERMATCH_NUM_EXACT_NP,
		USBFILTERMATCH_NUM_EXPRESSION,
		USBFILTERMATCH_NUM_EXPRESSION_NP,
		USBFILTERMATCH_NUM_LAST = USBFILTERMATCH_NUM_EXPRESSION_NP,
		USBFILTERMATCH_STR_FIRST,
		USBFILTERMATCH_STR_EXACT = USBFILTERMATCH_STR_FIRST,
		USBFILTERMATCH_STR_EXACT_NP,
		USBFILTERMATCH_STR_PATTERN,
		USBFILTERMATCH_STR_PATTERN_NP,
		USBFILTERMATCH_STR_LAST = USBFILTERMATCH_STR_PATTERN_NP,
		USBFILTERMATCH_END = 11
	}
	enum USBFILTERIDX : int {
		USBFILTERIDX_VENDOR_ID = 0,
		USBFILTERIDX_PRODUCT_ID,
		USBFILTERIDX_DEVICE_REV,
		USBFILTERIDX_DEVICE_CLASS,
		USBFILTERIDX_DEVICE_SUB_CLASS,
		USBFILTERIDX_DEVICE_PROTOCOL,
		USBFILTERIDX_BUS,
		USBFILTERIDX_PORT,
		USBFILTERIDX_MANUFACTURER_STR,
		USBFILTERIDX_PRODUCT_STR,
		USBFILTERIDX_SERIAL_NUMBER_STR,
		USBFILTERIDX_END = 11
	}
	[StructLayout(LayoutKind.Sequential, Size = 4)]
	struct USBFILTERFIELD {
		public USBFILTERMATCH enmMatch;
		public UInt16 u16Value;
	}
	[StructLayout(LayoutKind.Sequential)]
	unsafe struct USBFILTER {
		public UInt32 u32Magic;
		public USBFILTERTYPE enmType;
		public fixed UInt32 aFields[(int)USBFILTERIDX.USBFILTERIDX_END];
		public UInt32 offCurEnd;
		public fixed Byte achStrTab[256];

		const UInt32 USBFILTER_MAGIC = 0x19670408;
		public unsafe USBFILTER(USBFILTERTYPE enmType)
			: this() {
			u32Magic = USBFILTER_MAGIC;
			this.enmType = enmType;
			fixed (UInt32* aFieldsBytes = this.aFields) {
				USBFILTERFIELD* aFields = (USBFILTERFIELD*)aFieldsBytes;
				for (int i = 0; i < (int)USBFILTERIDX.USBFILTERIDX_END; i++)
					aFields[i].enmMatch = USBFILTERMATCH.USBFILTERMATCH_IGNORE;
			}
		}
		static Boolean IsNumericField(USBFILTERIDX enmFieldIdx) {
			switch (enmFieldIdx) {
				case USBFILTERIDX.USBFILTERIDX_VENDOR_ID:
				case USBFILTERIDX.USBFILTERIDX_PRODUCT_ID:
				case USBFILTERIDX.USBFILTERIDX_DEVICE_REV:
				case USBFILTERIDX.USBFILTERIDX_DEVICE_CLASS:
				case USBFILTERIDX.USBFILTERIDX_DEVICE_SUB_CLASS:
				case USBFILTERIDX.USBFILTERIDX_DEVICE_PROTOCOL:
				case USBFILTERIDX.USBFILTERIDX_BUS:
				case USBFILTERIDX.USBFILTERIDX_PORT:
					return true;
				case USBFILTERIDX.USBFILTERIDX_MANUFACTURER_STR:
				case USBFILTERIDX.USBFILTERIDX_PRODUCT_STR:
				case USBFILTERIDX.USBFILTERIDX_SERIAL_NUMBER_STR:
					return false;
				default:
					throw new ArgumentOutOfRangeException("enmFieldIdx");
			}
		}
		static Boolean IsMethodUsingStringValue(USBFILTERMATCH enmMatchingMethod) {
			switch (enmMatchingMethod) {
				case USBFILTERMATCH.USBFILTERMATCH_NUM_EXPRESSION:
				case USBFILTERMATCH.USBFILTERMATCH_NUM_EXPRESSION_NP:
				case USBFILTERMATCH.USBFILTERMATCH_STR_EXACT:
				case USBFILTERMATCH.USBFILTERMATCH_STR_EXACT_NP:
				case USBFILTERMATCH.USBFILTERMATCH_STR_PATTERN:
				case USBFILTERMATCH.USBFILTERMATCH_STR_PATTERN_NP:
					return true;
				case USBFILTERMATCH.USBFILTERMATCH_IGNORE:
				case USBFILTERMATCH.USBFILTERMATCH_PRESENT:
				case USBFILTERMATCH.USBFILTERMATCH_NUM_EXACT:
				case USBFILTERMATCH.USBFILTERMATCH_NUM_EXACT_NP:
					return false;
				default:
					throw new ArgumentOutOfRangeException("enmMatchingMethod");
			}
		}
		static unsafe int strlen(byte* ptr) {
			for (int length = 0; ; length++) if (ptr[length] != 0) return length;
		}
		static unsafe void memset(void* ptr, byte value, int count) {
			for (int i = 0; i < count; i++) ((byte*)ptr)[i] = value;
		}
		static unsafe void memmove(void* dest, void* src, int count) {
			if (dest > src) {
				for (int i = count - 1; i >= 0; i--) ((byte*)dest)[i] = ((byte*)src)[i];
			} else if (dest < src) {
				for (int i = 0; i < count; i++) ((byte*)dest)[i] = ((byte*)src)[i];
			}
		}
		public unsafe void SetString(USBFILTERIDX enmFieldIdx, Byte[] pszString) {
			fixed (USBFILTER* pFilter = &this) {
				USBFILTERFIELD* aFields = (USBFILTERFIELD*)pFilter->aFields;
				if (IsMethodUsingStringValue((USBFILTERMATCH)aFields[(int)enmFieldIdx].enmMatch) && aFields[(int)enmFieldIdx].u16Value != 0) {
					int off = aFields[(int)enmFieldIdx].u16Value;
					aFields[(int)enmFieldIdx].u16Value = 0;     /* Assign it to the NULL string. */
					int cchShift = strlen(&pFilter->achStrTab[off]) + 1;
					int cchToMove = ((int)offCurEnd + 1) - (off + cchShift);
					if (cchToMove > 0) {
						memmove(&pFilter->achStrTab[off], &pFilter->achStrTab[off + cchShift], cchToMove);
						for (int i = 0; i < (int)USBFILTERIDX.USBFILTERIDX_END; i++)
							if (aFields[i].u16Value >= off && IsMethodUsingStringValue(aFields[i].enmMatch))
								aFields[i].u16Value -= (ushort)cchShift;
					}
					offCurEnd -= (uint)cchShift;
					memset(&pFilter->achStrTab[offCurEnd], 0, cchShift);
				}
				if (pszString.Length == 0) {
					aFields[(int)enmFieldIdx].u16Value = 0;
				} else {
					int cch = pszString.Length;
					if (this.offCurEnd + cch + 2 > 256) throw new IndexOutOfRangeException("Buffer overflow");
					aFields[(int)enmFieldIdx].u16Value = (ushort)(this.offCurEnd + 1);
					for (int i = 0; i < cch + 1; i++) pFilter->achStrTab[offCurEnd + 1 + i] = pszString[i];
					offCurEnd += (uint)cch + 1;
				}
			}
		}
		unsafe void DeleteAnyStringValue(USBFILTERIDX enmFieldIdx) {
			fixed (USBFILTER* pFilter = &this) {
				USBFILTERFIELD* aFields = (USBFILTERFIELD*)pFilter->aFields;
				if (IsMethodUsingStringValue((USBFILTERMATCH)aFields[(int)enmFieldIdx].enmMatch) && aFields[(int)enmFieldIdx].u16Value != 0)
					SetString(enmFieldIdx, new Byte[0]);
				else if (enmFieldIdx >= USBFILTERIDX.USBFILTERIDX_END)
					throw new ArgumentOutOfRangeException("enmFieldIdx");
			}
		}
		public unsafe void SetNumExact(USBFILTERIDX enmFieldIdx, UInt16 u16Value, bool fMustBePresent) {
			if (!IsNumericField(enmFieldIdx)) throw new ArgumentOutOfRangeException("enmFieldIdx");
			DeleteAnyStringValue(enmFieldIdx);
			fixed (USBFILTER* pFilter = &this) {
				USBFILTERFIELD* aFields = (USBFILTERFIELD*)pFilter->aFields;
				aFields[(int)enmFieldIdx].u16Value = u16Value;
				aFields[(int)enmFieldIdx].enmMatch = fMustBePresent ? USBFILTERMATCH.USBFILTERMATCH_NUM_EXACT : USBFILTERMATCH.USBFILTERMATCH_NUM_EXACT_NP;
			}
		}
	}
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	struct USBSUP_FLTADDOUT {
		public IntPtr uId;
		public int rc;
	}
	[StructLayout(LayoutKind.Sequential)]
	struct USBSUP_VERSION {
		public UInt32 u32Major;
		public UInt32 u32Minor;
	}
	[StructLayout(LayoutKind.Sequential)]
	struct USBSUP_CLAIMDEV {
		public Byte bInterfaceNumber;
		public Byte fClaimed;
	}
	[StructLayout(LayoutKind.Sequential)]
	struct USBSUP_CLEAR_ENDPOINT {
		public Byte bEndpoint;
	}
	[StructLayout(LayoutKind.Sequential)]
	struct USBSUP_SET_CONFIG {
		public Byte bConfigurationValue;
	}
	[StructLayout(LayoutKind.Sequential)]
	struct USBSUP_SELECT_INTERFACE {
		public Byte bInterfaceNumber;
		public Byte bAlternateSetting;
	}
	enum USBSUP_TRANSFER_TYPE : int {
		USBSUP_TRANSFER_TYPE_CTRL = 0,
		USBSUP_TRANSFER_TYPE_ISOC = 1,
		USBSUP_TRANSFER_TYPE_BULK = 2,
		USBSUP_TRANSFER_TYPE_INTR = 3,
		USBSUP_TRANSFER_TYPE_MSG = 4
	}
	enum USBSUP_DIRECTION : int {
		USBSUP_DIRECTION_SETUP = 0,
		USBSUP_DIRECTION_IN = 1,
		USBSUP_DIRECTION_OUT = 2
	}
	enum USBSUP_XFER_FLAG : int {
		USBSUP_FLAG_NONE = 0,
		USBSUP_FLAG_SHORT_OK = 1
	}
	enum USBSUP_ERROR : int {
		USBSUP_XFER_OK = 0,
		USBSUP_XFER_STALL = 1,
		USBSUP_XFER_DNR = 2,
		USBSUP_XFER_CRC = 3,
		USBSUP_XFER_NAC = 4,
		USBSUP_XFER_UNDERRUN = 5,
		USBSUP_XFER_OVERRUN = 6
	}
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	unsafe struct USBSUP_URB {
		public USBSUP_TRANSFER_TYPE type;
		public UInt32 ep;
		public USBSUP_DIRECTION dir;
		public USBSUP_XFER_FLAG flags;
		public USBSUP_ERROR error;
		public UIntPtr len;
		public void* buf;
		public UInt32 numIsoPkts;
		public fixed byte aIsoPkts[8 * 8];
	}
	class USBRegistry : IUsbDeviceRegistry {
		public DeviceNode DeviceNode { get; private set; }
		public String DevicePath { get; private set; }
		public String DeviceID { get; private set; }
		public String SymbolicName { get { return DevicePath; } }
		private UCIS.HWLib.Windows.USB.UsbDevice usbdev = null;
		private Boolean hasDeviceDescriptor = false;
		private UCIS.USBLib.Descriptor.UsbDeviceDescriptor deviceDescriptor;
		private IDictionary<string, object> mDeviceProperties;
		public UCIS.HWLib.Windows.USB.UsbDevice USBDevice {
			get {
				if (usbdev == null) usbdev = UCIS.HWLib.Windows.USB.UsbDevice.GetUsbDevice(DeviceNode);
				return usbdev;
			}
		}
		public UCIS.USBLib.Descriptor.UsbDeviceDescriptor DeviceDescriptor {
			get {
				if (!hasDeviceDescriptor) deviceDescriptor = UCIS.USBLib.Descriptor.UsbDeviceDescriptor.FromDevice(USBDevice);
				return deviceDescriptor;
			}
		}
		public IDictionary<string, object> DeviceProperties {
			get {
				if (mDeviceProperties == null) mDeviceProperties = SetupApi.GetSPDRPProperties(DeviceNode);
				return mDeviceProperties;
			}
		}

		internal USBRegistry(DeviceNode device, String interfacepath) {
			DeviceNode = device;
			DeviceID = device.DeviceID;
			DevicePath = interfacepath;
		}

		public UInt16 Vid { get { return DeviceDescriptor.VendorID; } }
		public UInt16 Pid { get { return DeviceDescriptor.ProductID; } }
		public byte InterfaceID { get { return 0; } }

		public string Name { get { return DeviceNode.GetPropertyString(CMRDP.DEVICEDESC); } }
		public string Manufacturer { get { return DeviceNode.GetPropertyString(CMRDP.MFG); } }
		public string FullName {
			get {
				String desc = Name;
				String mfg = Manufacturer;
				if (mfg == null) return desc;
				if (desc == null) return mfg;
				return mfg + " - " + desc;
			}
		}

		public IUsbDevice Open() { return new VBoxUSB(this); }
	}

	public class VBoxUSB : UsbInterface, IUsbDevice {
		const int FILE_DEVICE_UNKNOWN = 0x00000022;
		const int METHOD_BUFFERED = 0;
		const int FILE_WRITE_ACCESS = 0x0002;
		static int CTL_CODE(int DeviceType, int Function, int Method, int Access) { return (DeviceType << 16) | (Access << 14) | (Function << 2) | Method; }
		static readonly int SUPUSBFLT_IOCTL_ADD_FILTER = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x611, METHOD_BUFFERED, FILE_WRITE_ACCESS);
		static readonly int SUPUSBFLT_IOCTL_RUN_FILTERS = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x615, METHOD_BUFFERED, FILE_WRITE_ACCESS);
		static readonly int SUPUSBFLT_IOCTL_REMOVE_FILTER = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x612, METHOD_BUFFERED, FILE_WRITE_ACCESS);
		static readonly int SUPUSBFLT_IOCTL_GET_VERSION = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x610, METHOD_BUFFERED, FILE_WRITE_ACCESS);
		static readonly int SUPUSB_IOCTL_GET_VERSION = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x60F, METHOD_BUFFERED, FILE_WRITE_ACCESS);
		static readonly int SUPUSB_IOCTL_USB_CLAIM_DEVICE = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x60B, METHOD_BUFFERED, FILE_WRITE_ACCESS);
		static readonly int SUPUSB_IOCTL_USB_RELEASE_DEVICE = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x60C, METHOD_BUFFERED, FILE_WRITE_ACCESS);
		static readonly int SUPUSB_IOCTL_USB_RESET = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x608, METHOD_BUFFERED, FILE_WRITE_ACCESS);
		static readonly int SUPUSB_IOCTL_USB_CLEAR_ENDPOINT = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x60E, METHOD_BUFFERED, FILE_WRITE_ACCESS);
		static readonly int SUPUSB_IOCTL_USB_SET_CONFIG = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x60A, METHOD_BUFFERED, FILE_WRITE_ACCESS);
		static readonly int SUPUSB_IOCTL_USB_SELECT_INTERFACE = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x609, METHOD_BUFFERED, FILE_WRITE_ACCESS);
		static readonly int SUPUSB_IOCTL_SEND_URB = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x607, METHOD_BUFFERED, FILE_WRITE_ACCESS);
		static readonly int SUPUSB_IOCTL_USB_ABORT_ENDPOINT = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x610, METHOD_BUFFERED, FILE_WRITE_ACCESS);

		const UInt32 USBDRV_MAJOR_VERSION = 4;
		const UInt32 USBDRV_MINOR_VERSION = 0;
		const UInt32 USBMON_MAJOR_VERSION = 5;
		const UInt32 USBMON_MINOR_VERSION = 0;

		static SafeFileHandle hMonitor = null;
		const String USBMON_DEVICE_NAME = "\\\\.\\VBoxUSBMon";

		static unsafe int SyncIoControl(SafeHandle hDevice, int IoControlCode, void* InBuffer, int nInBufferSize, void* OutBuffer, int nOutBufferSize, Boolean throwError) {
			Int32 pBytesReturned = 0;
			if (Kernel32.DeviceIoControl(hDevice, IoControlCode, InBuffer, nInBufferSize, OutBuffer, nOutBufferSize, out pBytesReturned, null)) return 0;
			int ret = Marshal.GetLastWin32Error();
			if (throwError) throw new Win32Exception(ret);
			return ret;
		}
		static unsafe void SyncIoControl(SafeHandle hDevice, int IoControlCode, void* InBuffer, int nInBufferSize, void* OutBuffer, int nOutBufferSize) {
			SyncIoControl(hDevice, IoControlCode, InBuffer, nInBufferSize, OutBuffer, nOutBufferSize, true);
		}
		
		unsafe static void InitMonitor() {
			if (hMonitor != null && !hMonitor.IsClosed && !hMonitor.IsInvalid) return;
			hMonitor = Kernel32.CreateFile(USBMON_DEVICE_NAME, Kernel32.GENERIC_READ | Kernel32.GENERIC_WRITE, Kernel32.FILE_SHARE_READ | Kernel32.FILE_SHARE_WRITE, IntPtr.Zero, Kernel32.OPEN_EXISTING, Kernel32.FILE_ATTRIBUTE_SYSTEM, IntPtr.Zero);
			if (hMonitor.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
			try {
				USBSUP_VERSION Version = new USBSUP_VERSION();
				SyncIoControl(hMonitor, SUPUSBFLT_IOCTL_GET_VERSION, null, 0, &Version, sizeof(USBSUP_VERSION));
				if (Version.u32Major != USBMON_MAJOR_VERSION || Version.u32Minor < USBMON_MINOR_VERSION) throw new InvalidOperationException("Unsupported USBMON version");
			} catch {
				hMonitor.Close();
			}
		}

		static unsafe IntPtr USBLibAddFilter(ref USBFILTER filter) {
			USBSUP_FLTADDOUT FltAddRc;
			fixed (USBFILTER* pFilter = &filter) SyncIoControl(hMonitor, SUPUSBFLT_IOCTL_ADD_FILTER, pFilter, sizeof(USBFILTER), &FltAddRc, sizeof(USBSUP_FLTADDOUT));
			if (FltAddRc.rc != 0) throw new Exception(String.Format("rc={0}", FltAddRc.rc));
			return FltAddRc.uId;
		}
		static unsafe void USBLibRemoveFilter(UIntPtr filterId) {
			SyncIoControl(hMonitor, SUPUSBFLT_IOCTL_REMOVE_FILTER, &filterId, sizeof(UIntPtr), null, 0);
		}
		static unsafe void USBLibRunFilters() {
			SyncIoControl(hMonitor, SUPUSBFLT_IOCTL_RUN_FILTERS, null, 0, null, 0);
		}

		static void initFilterFromDevice(ref USBFILTER aFilter, DeviceNode aDevice) {
			int mVid, mPid, mRev, mMi;
			WindowsUsbDeviceRegistry.DecodeDeviceIDs(aDevice, out mVid, out mPid, out mRev, out mMi);
			if (mVid != -1) aFilter.SetNumExact(USBFILTERIDX.USBFILTERIDX_VENDOR_ID, (ushort)mVid, true);
			if (mPid != -1) aFilter.SetNumExact(USBFILTERIDX.USBFILTERIDX_PRODUCT_ID, (ushort)mPid, true);
			if (mRev != -1) {
				int mRevBCD = (((mRev % 10) / 1) << 0) | (((mRev % 100) / 10) << 4) | (((mRev % 1000) / 100) << 8) | (((mRev % 10000) / 1000) << 12);
				aFilter.SetNumExact(USBFILTERIDX.USBFILTERIDX_DEVICE_REV, (ushort)mRevBCD, true);
			}
			//aFilter.SetNumExact(USBFILTERIDX.USBFILTERIDX_DEVICE_CLASS, 0xff, true);
			//aFilter.SetNumExact(USBFILTERIDX.USBFILTERIDX_DEVICE_SUB_CLASS, 0x00, true);
			//aFilter.SetNumExact(USBFILTERIDX.USBFILTERIDX_DEVICE_PROTOCOL, 0x00, true);
			//aFilter.SetNumExact(USBFILTERIDX.USBFILTERIDX_PORT, 0, true);
			//aFilter.SetNumExact(USBFILTERIDX.USBFILTERIDX_BUS, 0, true);
			//if (pDev->pszSerialNumber) aFilter.SetStringExact(USBFILTERIDX.USBFILTERIDX_SERIAL_NUMBER_STR, pDev->pszSerialNumber, true);
			//if (pDev->pszProduct) aFilter.SetStringExact(USBFILTERIDX.USBFILTERIDX_PRODUCT_STR, pDev->pszProduct, true);
			//if (pDev->pszManufacturer) aFilter.SetStringExact(USBFILTERIDX.USBFILTERIDX_MANUFACTURER_STR, pDev->pszManufacturer, true);
		}

		public unsafe static void Capture(DeviceNode aDevice) {
			InitMonitor();
			USBFILTER Filter = new USBFILTER(USBFILTERTYPE.USBFILTERTYPE_ONESHOT_CAPTURE);
			initFilterFromDevice(ref Filter, aDevice);
			IntPtr pvId = USBLibAddFilter(ref Filter);
			if (pvId == IntPtr.Zero) throw new Exception("Add one-shot Filter failed");
			USBLibRunFilters();
			//aDevice.Reenumerate(0);
		}
		public unsafe static void Release(DeviceNode aDevice) {
			InitMonitor();
			USBFILTER Filter = new USBFILTER(USBFILTERTYPE.USBFILTERTYPE_ONESHOT_IGNORE);
			initFilterFromDevice(ref Filter, aDevice);
			IntPtr pvId = USBLibAddFilter(ref Filter);
			if (pvId == IntPtr.Zero) throw new Exception("Add one-shot Filter failed");
			USBLibRunFilters();
			//aDevice.Reenumerate(0);
		}
		public static IUsbDeviceRegistry GetDeviceForDeviceNode(DeviceNode device) {
			String[] intfpath = device.GetInterfaces(new Guid(0x873fdf, 0xCAFE, 0x80EE, 0xaa, 0x5e, 0x0, 0xc0, 0x4f, 0xb1, 0x72, 0xb));
			if (intfpath == null || intfpath.Length == 0) return null;
			return new USBRegistry(device, intfpath[0]);
		}

		SafeHandle hDev;
		Byte bInterfaceNumber;

		public IUsbDeviceRegistry Registry { get; private set; }

		internal unsafe VBoxUSB(USBRegistry devreg) {
			this.Registry = devreg;
			hDev = Kernel32.CreateFile(devreg.DevicePath, Kernel32.GENERIC_READ | Kernel32.GENERIC_WRITE, Kernel32.FILE_SHARE_WRITE | Kernel32.FILE_SHARE_READ, IntPtr.Zero, Kernel32.OPEN_EXISTING, Kernel32.FILE_ATTRIBUTE_SYSTEM | Kernel32.FILE_FLAG_OVERLAPPED, IntPtr.Zero);
			if (hDev.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
			try {
				USBSUP_VERSION version = new USBSUP_VERSION();
				SyncIoControl(hDev, SUPUSB_IOCTL_GET_VERSION, null, 0, &version, sizeof(USBSUP_VERSION));
				if (version.u32Major != USBDRV_MAJOR_VERSION || version.u32Minor < USBDRV_MINOR_VERSION) throw new InvalidOperationException("Unsupported USBDRV version");
				USBSUP_CLAIMDEV claim = new USBSUP_CLAIMDEV() { bInterfaceNumber = 0 };
				SyncIoControl(hDev, SUPUSB_IOCTL_USB_CLAIM_DEVICE, &claim, sizeof(USBSUP_CLAIMDEV), &claim, sizeof(USBSUP_CLAIMDEV));
				if (claim.fClaimed == 0) throw new InvalidOperationException("Claim failed");
			} catch {
				hDev.Close();
				throw;
			}
		}
		protected unsafe override void Dispose(Boolean disposing) {
			if (!disposing) return;
			if (!hDev.IsInvalid && !hDev.IsClosed) {
				USBSUP_CLAIMDEV release = new USBSUP_CLAIMDEV() { bInterfaceNumber = bInterfaceNumber };
				SyncIoControl(hDev, SUPUSB_IOCTL_USB_RELEASE_DEVICE, &release, sizeof(USBSUP_CLAIMDEV), null, 0, false);
			}
			hDev.Close();
		}

		public unsafe override void PipeReset(byte endpoint) {
			USBSUP_CLEAR_ENDPOINT inp = new USBSUP_CLEAR_ENDPOINT() { bEndpoint = endpoint };
			SyncIoControl(hDev, SUPUSB_IOCTL_USB_CLEAR_ENDPOINT, &inp, sizeof(USBSUP_CLEAR_ENDPOINT), null, 0);
		}
		public unsafe override void PipeAbort(byte endpoint) {
			USBSUP_CLEAR_ENDPOINT inp = new USBSUP_CLEAR_ENDPOINT() { bEndpoint = endpoint };
			SyncIoControl(hDev, SUPUSB_IOCTL_USB_ABORT_ENDPOINT, &inp, sizeof(USBSUP_CLEAR_ENDPOINT), null, 0);
		}

		public unsafe void ResetDevice() {
			SyncIoControl(hDev, SUPUSB_IOCTL_USB_RESET, null, 0, null, 0);
		}

		public unsafe override byte Configuration {
			get { return base.Configuration; }
			set {
				USBSUP_SET_CONFIG inp = new USBSUP_SET_CONFIG() { bConfigurationValue = value };
				SyncIoControl(hDev, SUPUSB_IOCTL_USB_SET_CONFIG, &inp, sizeof(USBSUP_SET_CONFIG), null, 0);
			}
		}

		private unsafe void HandleURB(USBSUP_URB* urb) {
			using (ManualResetEvent evt = new ManualResetEvent(false)) {
				NativeOverlapped overlapped = new NativeOverlapped();
				overlapped.EventHandle = evt.SafeWaitHandle.DangerousGetHandle();
				int size;
				if (Kernel32.DeviceIoControl(hDev, SUPUSB_IOCTL_SEND_URB, urb, sizeof(USBSUP_URB), urb, sizeof(USBSUP_URB), out size, &overlapped))
					return;
				int err = Marshal.GetLastWin32Error();
				if (err != 997) throw new Win32Exception(err);
				evt.WaitOne();
				if (!Kernel32.GetOverlappedResult(hDev, &overlapped, out size, false))
					throw new Win32Exception(Marshal.GetLastWin32Error());
			}
		}

		private unsafe int BlockTransfer(USBSUP_TRANSFER_TYPE type, USBSUP_DIRECTION dir, USBSUP_XFER_FLAG flags, UInt32 ep, Byte[] buffer, int offset, int length) {
			if (offset < 0 || length < 0 || offset + length > buffer.Length) throw new ArgumentOutOfRangeException("length", "The specified offset and length exceed the buffer length");
			fixed (Byte* ptr = buffer) {
				USBSUP_URB urb = new USBSUP_URB();
				urb.type = type;
				urb.dir = dir;
				urb.flags = flags;
				urb.ep = ep;
				urb.len = (UIntPtr)length;
				urb.buf = ptr + offset;
				HandleURB(&urb);
				return (int)urb.len;
			}
		}

		public override int PipeTransfer(byte endpoint, byte[] buffer, int offset, int length) {
			return BlockTransfer(USBSUP_TRANSFER_TYPE.USBSUP_TRANSFER_TYPE_BULK, (endpoint & 0x80) == 0 ? USBSUP_DIRECTION.USBSUP_DIRECTION_OUT : USBSUP_DIRECTION.USBSUP_DIRECTION_IN, USBSUP_XFER_FLAG.USBSUP_FLAG_NONE, endpoint, buffer, offset, length);
			//return BlockTransfer(USBSUP_TRANSFER_TYPE.USBSUP_TRANSFER_TYPE_INTR, USBSUP_DIRECTION.USBSUP_DIRECTION_OUT, USBSUP_XFER_FLAG.USBSUP_FLAG_NONE, endpoint, buffer, offset, length);
		}
		public override unsafe int ControlTransfer(UsbControlRequestType requestType, byte request, short value, short index, byte[] buffer, int offset, int length) {
			Byte[] bigbuffer = new Byte[sizeof(UsbSetupPacket) + length];
			Boolean isout = (requestType & UsbControlRequestType.EndpointMask) == UsbControlRequestType.EndpointOut;
			if (isout && length > 0) Buffer.BlockCopy(buffer, offset, bigbuffer, sizeof(UsbSetupPacket), length);
			fixed (Byte* ptr = bigbuffer) *(UsbSetupPacket*)ptr = new UsbSetupPacket((Byte)requestType, request, value, index, (short)length);
			int dlen = BlockTransfer(USBSUP_TRANSFER_TYPE.USBSUP_TRANSFER_TYPE_MSG, isout ? USBSUP_DIRECTION.USBSUP_DIRECTION_OUT : USBSUP_DIRECTION.USBSUP_DIRECTION_IN, USBSUP_XFER_FLAG.USBSUP_FLAG_NONE, 0, bigbuffer, 0, bigbuffer.Length);
			dlen -= sizeof(UsbSetupPacket);
			if (dlen > length) dlen = length;
			if (dlen < 0) dlen = 0;
			if (!isout) Buffer.BlockCopy(bigbuffer, sizeof(UsbSetupPacket), buffer, offset, dlen);
			return dlen;
		}

		public void ClaimInterface(int interfaceID) {
			bInterfaceNumber = (Byte)interfaceID;
		}

		public void ReleaseInterface(int interfaceID) {
		}
	}
}
