using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using UCIS.HWLib.Windows.Devices;

namespace UCIS.USBLib.Internal.Windows {
	class SafeDeviceInfoSetHandle : SafeHandleZeroOrMinusOneIsInvalid {
		public SafeDeviceInfoSetHandle() : base(true) { }
		//public SafeDeviceInfoSetHandle(IntPtr handle) : base(true) { SetHandle(handle); }
		protected override bool ReleaseHandle() {
			if (IsInvalid) return true;
			bool bSuccess = SetupApi.SetupDiDestroyDeviceInfoList(handle);
			handle = IntPtr.Zero;
			return bSuccess;
		}
	}
	class SetupApi {
		[DllImport("setupapi.dll", CharSet = CharSet.Auto)]
		public static extern SafeDeviceInfoSetHandle SetupDiGetClassDevs(IntPtr ClassGuid, [MarshalAs(UnmanagedType.LPTStr)] string Enumerator, IntPtr hwndParent, int Flags);
		[DllImport("setupapi.dll", CharSet = CharSet.Auto)]
		public static extern SafeDeviceInfoSetHandle SetupDiGetClassDevsA(ref Guid ClassGuid, [MarshalAs(UnmanagedType.LPTStr)] string Enumerator, IntPtr hwndParent, DICFG Flags);
		[DllImport("setupapi.dll", CharSet = CharSet.Auto)]
		public static extern SafeDeviceInfoSetHandle SetupDiGetClassDevsA(IntPtr ClassGuid, [MarshalAs(UnmanagedType.LPStr)] string Enumerator, IntPtr hwndParent, DICFG Flags);

		[DllImport("setupapi.dll", CharSet = CharSet.Auto /*, SetLastError = true*/)]
		public static extern bool SetupDiDestroyDeviceInfoList(IntPtr hDevInfo);

		[DllImport("setupapi.dll", SetLastError = true)]
		public static extern bool SetupDiEnumDeviceInfo(SafeDeviceInfoSetHandle DeviceInfoSet, int MemberIndex, ref SP_DEVINFO_DATA DeviceInfoData);
		[DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
		public static extern Boolean SetupDiEnumDeviceInterfaces(SafeDeviceInfoSetHandle hDevInfo, ref SP_DEVINFO_DATA devInfo, ref Guid interfaceClassGuid, int memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);
		[DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
		public static extern Boolean SetupDiEnumDeviceInterfaces(SafeDeviceInfoSetHandle hDevInfo, IntPtr devInfo, ref Guid interfaceClassGuid, int memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);
		[DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
		public static extern Boolean SetupDiGetDeviceInterfaceDetail(SafeDeviceInfoSetHandle hDevInfo, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, ref SP_DEVICE_INTERFACE_DETAIL_DATA deviceInterfaceDetailData, int deviceInterfaceDetailDataSize, out int requiredSize, ref SP_DEVINFO_DATA deviceInfoData);

		[DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
		public static extern bool SetupDiGetCustomDeviceProperty(SafeDeviceInfoSetHandle DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, string CustomPropertyName, DICUSTOMDEVPROP Flags, out RegistryValueKind PropertyRegDataType, Byte[] PropertyBuffer, int PropertyBufferSize, out int RequiredSize);
		[DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Ansi)]
		public static extern bool SetupDiGetDeviceInstanceIdA(SafeDeviceInfoSetHandle DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, StringBuilder DeviceInstanceId, int DeviceInstanceIdSize, out int RequiredSize);
		[DllImport("setupapi.dll", CharSet = CharSet.Auto)]
		public static extern bool SetupDiGetDeviceInterfacePropertyKeys(SafeDeviceInfoSetHandle DeviceInfoSet, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData, byte[] propKeyBuffer, int propKeyBufferElements, out int RequiredPropertyKeyCount, int Flags);
		[DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
		public static extern bool SetupDiGetDeviceRegistryProperty(SafeDeviceInfoSetHandle DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, SPDRP Property, out RegistryValueKind PropertyRegDataType, byte[] PropertyBuffer, int PropertyBufferSize, out int RequiredSize);
		[DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern bool SetupDiGetDeviceRegistryProperty(SafeDeviceInfoSetHandle DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, SPDRP iProperty, out int PropertyRegDataType, IntPtr PropertyBuffer, int PropertyBufferSize, out int RequiredSize);
		[DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern bool SetupDiGetDeviceRegistryProperty(SafeDeviceInfoSetHandle DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, SPDRP iProperty, out int PropertyRegDataType, [MarshalAs(UnmanagedType.LPTStr)] StringBuilder PropertyBuffer, int PropertyBufferSize, out int RequiredSize);

		[DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern bool SetupDiGetDeviceInstanceId(SafeDeviceInfoSetHandle DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, StringBuilder DeviceInstanceId, int DeviceInstanceIdSize, out int RequiredSize);

		[DllImport("setupapi.dll")]
		public static extern bool SetupDiSetClassInstallParams(SafeDeviceInfoSetHandle DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, ref SP_CLASSINSTALL_HEADER ClassInstallParams, int ClassInstallParamsSize);

		[DllImport("setupapi.dll")]
		public static extern bool SetupDiCallClassInstaller(int InstallFunction, SafeDeviceInfoSetHandle DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData);

		[DllImport("setupapi.dll", CharSet = CharSet.Auto)]
		public static extern CR CM_Get_Device_ID(UInt32 dnDevInst, StringBuilder Buffer, int BufferLen, int ulFlags);
		[DllImport("setupapi.dll", CharSet = CharSet.Auto)]
		public static extern CR CM_Get_Device_ID(UInt32 dnDevInst, IntPtr Buffer, int BufferLen, int ulFlags);
		[DllImport("setupapi.dll", CharSet = CharSet.Auto)]
		public static extern CR CM_Get_Device_ID_Size(out UInt32 pulLen, UInt32 dnDevInst, UInt32 ulFlags);
		[DllImport("setupapi.dll")]
		public static extern CR CM_Get_Parent(out UInt32 pdnDevInst, UInt32 dnDevInst, int ulFlags);

		[DllImport("setupapi.dll", CharSet = CharSet.Auto)]
		public static extern CR CM_Locate_DevNode(out UInt32 pdnDevInst, [MarshalAs(UnmanagedType.LPWStr)] String pDeviceID, UInt32 ulFlags);
		[DllImport("setupapi.dll", CharSet = CharSet.Auto)]
		public static extern CR CM_Get_Sibling(out UInt32 pdnDevInst, UInt32 DevInst, UInt32 ulFlags);
		[DllImport("setupapi.dll", CharSet = CharSet.Auto)]
		public static extern CR CM_Get_Child(out UInt32 pdnDevInst, UInt32 dnDevInst, UInt32 ulFlags);
		[DllImport("setupapi.dll", CharSet = CharSet.Auto)]
		public static extern CR CM_Get_DevNode_Registry_Property(UInt32 dnDevInst, CMRDP ulProperty, out UInt32 pulRegDataType, Byte[] Buffer, ref UInt32 pulLength, UInt32 ulFlags);
		[DllImport("setupapi.dll", CharSet = CharSet.Auto)]
		public static extern CR CM_Get_Device_Interface_List([In] ref Guid InterfaceClassGuid, [MarshalAs(UnmanagedType.LPWStr)] String pDeviceID, Byte[] Buffer, UInt32 BufferLen, UInt32 ulFlags);
		[DllImport("setupapi.dll", CharSet = CharSet.Auto)]
		public static extern CR CM_Get_Device_Interface_List_Size(out UInt32 pulLen, [In] ref Guid InterfaceClassGuid, [MarshalAs(UnmanagedType.LPWStr)] String pDeviceID, UInt32 ulFlags);
		[DllImport("setupapi.dll", CharSet = CharSet.Auto)]
		public static extern CR CM_Enumerate_Classes(UInt32 ulClassIndex, out Guid ClassGuid, UInt32 ulFlags);
		[DllImport("setupapi.dll", CharSet = CharSet.Auto)]
		public static extern CR CM_Get_DevNode_Status(out UInt32 pulStatus, out UInt32 pulProblemNumber, UInt32 dnDevInst, UInt32 ulFlags);


		//public const int DIGCF_DEFAULT = 0x00000001;  // only valid with DIGCF_DEVICEINTERFACE
		public const int DIGCF_PRESENT = 0x00000002;
		public const int DIGCF_ALLCLASSES = 0x00000004;
		public const int DIGCF_PROFILE = 0x00000008;
		public const int DIGCF_DEVICEINTERFACE = 0x00000010;

		internal static Guid? GetAsGuid(byte[] buffer) { if (buffer == null) return null; else return GetAsGuid(buffer, buffer.Length); }
		internal static Guid GetAsGuid(byte[] buffer, int len) {
			if (len != 16) return Guid.Empty;
			byte[] guidBytes = new byte[len];
			Array.Copy(buffer, guidBytes, guidBytes.Length);
			return new Guid(guidBytes);
		}
		internal static string GetAsString(byte[] buffer) { return GetAsString(buffer, buffer.Length); }
		internal static string GetAsString(byte[] buffer, int len) {
			if (len <= 2) return String.Empty;
			return Encoding.Unicode.GetString(buffer, 0, len).TrimEnd('\0');
		}
		internal static string[] GetAsStringArray(byte[] buffer) { return GetAsStringArray(buffer, buffer.Length); }
		internal static string[] GetAsStringArray(byte[] buffer, int len) {
			return GetAsString(buffer, len).Split(new char[] { '\0' }, StringSplitOptions.RemoveEmptyEntries); 
		}
		internal static Int32? GetAsInt32(byte[] buffer) { if (buffer == null) return null; else return GetAsInt32(buffer, buffer.Length); }
		internal static Int32 GetAsInt32(byte[] buffer, int len) {
			if (len != 4) return 0;
			return buffer[0] | ((buffer[1]) << 8) | ((buffer[2]) << 16) | ((buffer[3]) << 24);
		}

		public static IDictionary<String, Object> GetSPDRPProperties(DeviceNode device) {
			Dictionary<string, object> deviceProperties = new Dictionary<string, object>();
			foreach (SPDRP prop in Enum.GetValues(typeof(SPDRP))) {
				Byte[] propBuffer = device.GetProperty(prop);
				if (propBuffer == null) continue;
				int iReturnBytes = propBuffer.Length;
				object oValue = null;
				switch (prop) {
					case SPDRP.PhysicalDeviceObjectName:
					case SPDRP.LocationInformation:
					case SPDRP.Class:
					case SPDRP.Mfg:
					case SPDRP.DeviceDesc:
					case SPDRP.Driver:
					case SPDRP.EnumeratorName:
					case SPDRP.FriendlyName:
					case SPDRP.ClassGuid:
					case SPDRP.Service:
						oValue = GetAsString(propBuffer, iReturnBytes);
						break;
					case SPDRP.HardwareId:
					case SPDRP.CompatibleIds:
					case SPDRP.LocationPaths:
						oValue = GetAsStringArray(propBuffer, iReturnBytes);
						break;
					case SPDRP.BusNumber:
					case SPDRP.InstallState:
					case SPDRP.LegacyBusType:
					case SPDRP.RemovalPolicy:
					case SPDRP.UiNumber:
					case SPDRP.Address:
						oValue = GetAsInt32(propBuffer, iReturnBytes);
						break;
					case SPDRP.BusTypeGuid:
						oValue = GetAsGuid(propBuffer, iReturnBytes);
						break;
					default:
						oValue = propBuffer;
						break;
				}
				deviceProperties.Add(prop.ToString(), oValue);
			}
			return deviceProperties;
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	struct SP_DEVICE_INTERFACE_DATA {
		public UInt32 cbSize;
		public Guid interfaceClassGuid;
		public UInt32 flags;
		private UIntPtr reserved;
		public SP_DEVICE_INTERFACE_DATA(Boolean b) : this() {
			cbSize = (uint)Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA));
		}
	}
	[StructLayout(LayoutKind.Sequential)]
	struct SP_DEVINFO_DATA {
		public UInt32 cbSize;
		public Guid ClassGuid;
		public UInt32 DevInst;
		public IntPtr Reserved;
		public SP_DEVINFO_DATA(Boolean b)
			: this() {
			cbSize = (uint)Marshal.SizeOf(typeof(SP_DEVINFO_DATA));
		}
	}
	[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
	struct SP_DEVICE_INTERFACE_DETAIL_DATA {
		public uint cbSize; //<summary>The size, in bytes, of the fixed portion of the SP_DEVICE_INTERFACE_DETAIL_DATA structure.</summary>
		//Byte[1] DevicePath
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)]
		public String DevicePath; //<summary>A NULL-terminated string that contains the device interface path. This path can be passed to Win32 functions such as CreateFile.</summary>
		public SP_DEVICE_INTERFACE_DETAIL_DATA(Boolean b)
			: this() {
			//cbSize should be sizeof(SP_DEVICE_INTERFACE_DETAIL_DATA) which is the same as the following
			//if DevicePath is a 1-byte array and the structure is properly padded... is it really?
			if (IntPtr.Size == 8) cbSize = 8; //Workaround for x64
			else cbSize = 4 + (uint)Marshal.SystemDefaultCharSize;
		}
	}

	enum CR {
		SUCCESS = (0x00000000),
		DEFAULT = (0x00000001),
		OUT_OF_MEMORY = (0x00000002),
		INVALID_POINTER = (0x00000003),
		INVALID_FLAG = (0x00000004),
		INVALID_DEVNODE = (0x00000005),
		INVALID_DEVINST = INVALID_DEVNODE,
		INVALID_RES_DES = (0x00000006),
		INVALID_LOG_CONF = (0x00000007),
		INVALID_ARBITRATOR = (0x00000008),
		INVALID_NODELIST = (0x00000009),
		DEVNODE_HAS_REQS = (0x0000000A),
		DEVINST_HAS_REQS = DEVNODE_HAS_REQS,
		INVALID_RESOURCEID = (0x0000000B),
		DLVXD_NOT_FOUND = (0x0000000C), // WIN 95 ONLY
		NO_SUCH_DEVNODE = (0x0000000D),
		NO_SUCH_DEVINST = NO_SUCH_DEVNODE,
		NO_MORE_LOG_CONF = (0x0000000E),
		NO_MORE_RES_DES = (0x0000000F),
		ALREADY_SUCH_DEVNODE = (0x00000010),
		ALREADY_SUCH_DEVINST = ALREADY_SUCH_DEVNODE,
		INVALID_RANGE_LIST = (0x00000011),
		INVALID_RANGE = (0x00000012),
		FAILURE = (0x00000013),
		NO_SUCH_LOGICAL_DEV = (0x00000014),
		CREATE_BLOCKED = (0x00000015),
		NOT_SYSTEM_VM = (0x00000016), // WIN 95 ONLY
		REMOVE_VETOED = (0x00000017),
		APM_VETOED = (0x00000018),
		INVALID_LOAD_TYPE = (0x00000019),
		BUFFER_SMALL = (0x0000001A),
		NO_ARBITRATOR = (0x0000001B),
		NO_REGISTRY_HANDLE = (0x0000001C),
		REGISTRY_ERROR = (0x0000001D),
		INVALID_DEVICE_ID = (0x0000001E),
		INVALID_DATA = (0x0000001F),
		INVALID_API = (0x00000020),
		DEVLOADER_NOT_READY = (0x00000021),
		NEED_RESTART = (0x00000022),
		NO_MORE_HW_PROFILES = (0x00000023),
		DEVICE_NOT_THERE = (0x00000024),
		NO_SUCH_VALUE = (0x00000025),
		WRONG_TYPE = (0x00000026),
		INVALID_PRIORITY = (0x00000027),
		NOT_DISABLEABLE = (0x00000028),
		FREE_RESOURCES = (0x00000029),
		QUERY_VETOED = (0x0000002A),
		CANT_SHARE_IRQ = (0x0000002B),
		NO_DEPENDENT = (0x0000002C),
		SAME_RESOURCES = (0x0000002D),
		NO_SUCH_REGISTRY_KEY = (0x0000002E),
		INVALID_MACHINENAME = (0x0000002F), // NT ONLY
		REMOTE_COMM_FAILURE = (0x00000030), // NT ONLY
		MACHINE_UNAVAILABLE = (0x00000031), // NT ONLY
		NO_CM_SERVICES = (0x00000032), // NT ONLY
		ACCESS_DENIED = (0x00000033), // NT ONLY
		CALL_NOT_IMPLEMENTED = (0x00000034),
		INVALID_PROPERTY = (0x00000035),
		DEVICE_INTERFACE_ACTIVE = (0x00000036),
		NO_SUCH_DEVICE_INTERFACE = (0x00000037),
		INVALID_REFERENCE_STRING = (0x00000038),
		INVALID_CONFLICT_LIST = (0x00000039),
		INVALID_INDEX = (0x0000003A),
		INVALID_STRUCTURE_SIZE = (0x0000003B),
		NUM_CR_RESULTS = (0x0000003C)
	}
	[Flags]
	enum DICFG {
		/// <summary>
		/// Return only the device that is associated with the system default device interface, if one is set, for the specified device interface classes. 
		///  only valid with <see cref="DEVICEINTERFACE"/>.
		/// </summary>
		DEFAULT = 0x00000001,
		/// <summary>
		/// Return only devices that are currently present in a system. 
		/// </summary>
		PRESENT = 0x00000002,
		/// <summary>
		/// Return a list of installed devices for all device setup classes or all device interface classes. 
		/// </summary>
		ALLCLASSES = 0x00000004,
		/// <summary>
		/// Return only devices that are a part of the current hardware profile. 
		/// </summary>
		PROFILE = 0x00000008,
		/// <summary>
		/// Return devices that support device interfaces for the specified device interface classes. 
		/// </summary>
		DEVICEINTERFACE = 0x00000010,
	}
	enum DICUSTOMDEVPROP {
		NONE = 0,
		MERGE_MULTISZ = 0x00000001,
	}
	public enum SPDRP {
		/// <summary>
		/// Requests a string describing the device, such as "Microsoft PS/2 Port Mouse", typically defined by the manufacturer. 
		/// </summary>
		DeviceDesc = (0x00000000),
		/// <summary>
		/// Requests the hardware IDs provided by the device that identify the device.
		/// </summary>
		HardwareId = (0x00000001),
		/// <summary>
		/// Requests the compatible IDs reported by the device.
		/// </summary>
		CompatibleIds = (0x00000002),
		Service = 0x00000004,
		/// <summary>
		/// Requests the name of the device's setup class, in text format. 
		/// </summary>
		Class = (0x00000007),
		/// <summary>
		/// Requests the GUID for the device's setup class.
		/// </summary>
		ClassGuid = (0x00000008),
		/// <summary>
		/// Requests the name of the driver-specific registry key.
		/// </summary>
		Driver = (0x00000009),
		ConfigFlags = 0x0000000A,
		/// <summary>
		/// Requests a string identifying the manufacturer of the device.
		/// </summary>
		Mfg = (0x0000000B),
		/// <summary>
		/// Requests a string that can be used to distinguish between two similar devices, typically defined by the class installer.
		/// </summary>
		FriendlyName = (0x0000000C),
		/// <summary>
		/// Requests information about the device's location on the bus; the interpretation of this information is bus-specific. 
		/// </summary>
		LocationInformation = (0x0000000D),
		/// <summary>
		/// Requests the name of the PDO for this device.
		/// </summary>
		PhysicalDeviceObjectName = (0x0000000E),
		Capabilities = 0x0000000F,
		/// <summary>
		/// Requests a number associated with the device that can be displayed in the user interface.
		/// </summary>
		UiNumber = (0x00000010),
		UpperFilters = 0x00000011,
		LowerFilters = 0x00000012,
		/// <summary>
		/// Requests the GUID for the bus that the device is connected to.
		/// </summary>
		BusTypeGuid = (0x00000013),
		/// <summary>
		/// Requests the bus type, such as PCIBus or PCMCIABus.
		/// </summary>
		LegacyBusType = (0x00000014),
		/// <summary>
		/// Requests the legacy bus number of the bus the device is connected to. 
		/// </summary>
		BusNumber = (0x00000015),
		/// <summary>
		/// Requests the name of the enumerator for the device, such as "USB".
		/// </summary>
		EnumeratorName = (0x00000016),
		DevType = 0x00000019,
		Exclusive = 0x0000001A,
		Characteristics = 0x0000001B,
		/// <summary>
		/// Requests the address of the device on the bus. 
		/// </summary>
		Address = (0x0000001C),
		/// <summary>
		/// (Windows XP and later.) Requests the device's current removal policy. The operating system uses this value as a hint to determine how the device is normally removed.
		/// </summary>
		RemovalPolicy = (0x0000001F),
		/// <summary>
		/// Windows XP and later.) Requests the device's installation state.
		/// </summary>
		InstallState = (0x00000022),
		/// <summary>
		/// Device Location Paths (R)
		/// </summary>
		LocationPaths = (0x00000023),
	}
	public enum CMRDP : uint {
		DEVICEDESC = 0x00000001,
		HARDWAREID = 0x00000002,
		COMPATIBLEIDS = 0x00000003,
		SERVICE = 0x00000005,
		CLASS = 0x00000008,
		CLASSGUID = 0x00000009,
		DRIVER = 0x0000000A,
		CONFIGFLAGS = 0x0000000B,
		MFG = 0x0000000C,
		FRIENDLYNAME = 0x0000000D,
		LOCATION_INFORMATION = 0x0000000E,
		PHYSICAL_DEVICE_OBJECT_NAME = 0x0000000F,
		CAPABILITIES = 0x00000010,
		UI_NUMBER = 0x00000011,
		UPPERFILTERS = 0x00000012,
		LOWERFILTERS = 0x00000013,
		BUSTYPEGUID = 0x00000014,
		LEGACYBUSTYPE = 0x00000015,
		BUSNUMBER = 0x00000016,
		ENUMERATOR_NAME = 0x00000017,
	}
}