using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using UCIS.USBLib.Internal.Windows;

namespace UCIS.HWLib.Windows.Devices {
	public class CMException : Exception {
		internal CMException(CR result, String method)
			: base(method + " returned " + result.ToString()) {
		}
		internal static void Throw(CR result, String method) {
			if (result == CR.SUCCESS) return;
			throw new CMException(result, method);
		}
	}
	public class DeviceNode {
		public static DeviceNode Root {
			get {
				UInt32 node;
				CR ret = SetupApi.CM_Locate_DevNode(out node, null, 0);
				CMException.Throw(ret, "CM_Locate_DevNode");
				return new DeviceNode(node);
			}
		}
		public static DeviceNode GetDevice(String deviceID) {
			UInt32 node;
			CR ret = SetupApi.CM_Locate_DevNode(out node, deviceID, 0);
			CMException.Throw(ret, "CM_Locate_DevNode");
			return new DeviceNode(node);
		}
		private static IList<DeviceNode> GetDevicesInSet(SafeDeviceInfoSetHandle dis) {
			List<DeviceNode> list = new List<DeviceNode>();
			if (dis.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
			SP_DEVINFO_DATA dd = new SP_DEVINFO_DATA(true);
			for (int index = 0; ; index++) {
				if (!SetupApi.SetupDiEnumDeviceInfo(dis, index, ref dd)) break;
				list.Add(new DeviceNode(dd.DevInst));
			}
			return list;
		}
		public static IList<DeviceNode> GetDevices() {
			using (SafeDeviceInfoSetHandle dis = SetupApi.SetupDiGetClassDevsA(IntPtr.Zero, null, IntPtr.Zero, DICFG.PRESENT | DICFG.ALLCLASSES)) {
				return GetDevicesInSet(dis);
			}
		}
		public static IList<DeviceNode> GetDevices(Guid classGuid) {
			using (SafeDeviceInfoSetHandle dis = SetupApi.SetupDiGetClassDevsA(ref classGuid, null, IntPtr.Zero, DICFG.PRESENT | DICFG.DEVICEINTERFACE)) {
				return GetDevicesInSet(dis);
			}
		}
		public static IList<DeviceNode> GetDevices(String enumerator) {
			return GetDevices(enumerator, true);
		}
		public static IList<DeviceNode> GetDevices(String enumerator, Boolean present) {
			using (SafeDeviceInfoSetHandle dis = SetupApi.SetupDiGetClassDevsA(IntPtr.Zero, enumerator, IntPtr.Zero, (present ? DICFG.PRESENT : 0) | DICFG.ALLCLASSES)) {
			//using (SafeDeviceInfoSetHandle dis = SetupApi.SetupDiGetClassDevsA(IntPtr.Zero, enumerator, IntPtr.Zero, DICFG.ALLCLASSES | DICFG.DEVICEINTERFACE)) {
				return GetDevicesInSet(dis);
			}
		}

		public UInt32 DevInst { get; private set; }
		private String _DeviceID = null;
		internal DeviceNode(UInt32 node) {
			DevInst = node;
		}
		public String DeviceID {
			get {
				if (_DeviceID == null) {
					uint deviceidlen;
					CR ret = SetupApi.CM_Get_Device_ID_Size(out deviceidlen, DevInst, 0);
					CMException.Throw(ret, "CM_Get_Device_ID_Size");
					StringBuilder deviceid = new StringBuilder((int)deviceidlen);
					ret = SetupApi.CM_Get_Device_ID(DevInst, deviceid, deviceid.MaxCapacity, 0);
					CMException.Throw(ret, "CM_Get_Device_ID");
					_DeviceID = deviceid.ToString();
				}
				return _DeviceID;
			}
		}
		public Byte[] GetProperty(SPDRP property) {
			using (SafeDeviceInfoSetHandle dis = SetupApi.SetupDiGetClassDevsA(IntPtr.Zero, DeviceID, IntPtr.Zero, DICFG.DEVICEINTERFACE | DICFG.ALLCLASSES)) {
				if (dis.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
				SP_DEVINFO_DATA dd = new SP_DEVINFO_DATA(true);
				if (!SetupApi.SetupDiEnumDeviceInfo(dis, 0, ref dd))
					return null;
				//throw new Win32Exception(Marshal.GetLastWin32Error());
				RegistryValueKind propertyType;
				byte[] propBuffer = new byte[256];
				int requiredSize;
				if (!SetupApi.SetupDiGetDeviceRegistryProperty(dis, ref dd, property, out propertyType, propBuffer, propBuffer.Length, out requiredSize))
					return null;
				if (requiredSize > propBuffer.Length) {
					propBuffer = new Byte[requiredSize];
					if (!SetupApi.SetupDiGetDeviceRegistryProperty(dis, ref dd, property, out propertyType, propBuffer, propBuffer.Length, out requiredSize))
						throw new Win32Exception(Marshal.GetLastWin32Error());
				}
				if (requiredSize < propBuffer.Length) Array.Resize(ref propBuffer, requiredSize);
				return propBuffer;
			}
		}
		public String GetPropertyString(SPDRP property) {
			Byte[] buffer = GetProperty(property);
			if (buffer == null) return null;
			return SetupApi.GetAsString(buffer, buffer.Length);
		}
		public Byte[] GetProperty(CMRDP property) {
			uint proplength = 0;
			uint proptype;
			CR ret = SetupApi.CM_Get_DevNode_Registry_Property(DevInst, property, out proptype, null, ref proplength, 0);
			if (ret == CR.NO_SUCH_VALUE || ret == CR.NO_SUCH_DEVNODE) return null;
			if (ret != CR.BUFFER_SMALL) CMException.Throw(ret, "CM_Get_DevNode_Registry_Property");
			Byte[] propbuffer = new Byte[proplength];
			ret = SetupApi.CM_Get_DevNode_Registry_Property(DevInst, property, out proptype, propbuffer, ref proplength, 0);
			CMException.Throw(ret, "CM_Get_DevNode_Registry_Property");
			if (propbuffer.Length > proplength) Array.Resize(ref propbuffer, (int)proplength);
			return propbuffer;
		}
		public String GetPropertyString(CMRDP property) {
			Byte[] buffer = GetProperty(property);
			if (buffer == null) return null;
			return SetupApi.GetAsString(buffer, buffer.Length);
		}
		public String[] GetPropertyStringArray(CMRDP property) {
			Byte[] buffer = GetProperty(property);
			if (buffer == null) return null;
			return SetupApi.GetAsStringArray(buffer, buffer.Length);
		}

		public void SetProperty(SPDRP property, Byte[] value) {
			using (SafeDeviceInfoSetHandle dis = SetupApi.SetupDiGetClassDevsA(IntPtr.Zero, DeviceID, IntPtr.Zero, DICFG.DEVICEINTERFACE | DICFG.ALLCLASSES)) {
				if (dis.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
				SP_DEVINFO_DATA dd = new SP_DEVINFO_DATA(true);
				if (!SetupApi.SetupDiEnumDeviceInfo(dis, 0, ref dd))
					throw new Win32Exception(Marshal.GetLastWin32Error());
				if (!SetupApi.SetupDiSetDeviceRegistryProperty(dis, ref dd, property, value, (uint)value.Length))
					throw new Win32Exception(Marshal.GetLastWin32Error());
			}
		}

		public Byte[] GetCustomProperty(String name) {
			using (SafeDeviceInfoSetHandle dis = SetupApi.SetupDiGetClassDevsA(IntPtr.Zero, DeviceID, IntPtr.Zero, DICFG.DEVICEINTERFACE | DICFG.ALLCLASSES)) {
				if (dis.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
				SP_DEVINFO_DATA dd = new SP_DEVINFO_DATA(true);
				if (!SetupApi.SetupDiEnumDeviceInfo(dis, 0, ref dd))
					return null;
					//throw new Win32Exception(Marshal.GetLastWin32Error());
				RegistryValueKind propertyType;
				byte[] propBuffer = new byte[256];
				int requiredSize;
				if (!SetupApi.SetupDiGetCustomDeviceProperty(dis, ref dd, name, DICUSTOMDEVPROP.NONE, out propertyType, propBuffer, propBuffer.Length, out requiredSize))
					return null;
				if (requiredSize > propBuffer.Length) {
					propBuffer = new Byte[requiredSize];
					if (!SetupApi.SetupDiGetCustomDeviceProperty(dis, ref dd, name, DICUSTOMDEVPROP.NONE, out propertyType, propBuffer, propBuffer.Length, out requiredSize))
						throw new Win32Exception(Marshal.GetLastWin32Error());
				}
				if (requiredSize < propBuffer.Length) Array.Resize(ref propBuffer, requiredSize);
				return propBuffer;
			}
		}
		public String GetCustomPropertyString(String name) {
			Byte[] buffer = GetCustomProperty(name);
			if (buffer == null) return null;
			return SetupApi.GetAsString(buffer, buffer.Length);
		}
		public String[] GetCustomPropertyStringArray(String name) {
			Byte[] buffer = GetCustomProperty(name);
			if (buffer == null) return null;
			return SetupApi.GetAsStringArray(buffer, buffer.Length);
		}

		public RegistryKey OpenRegistryKey(UInt32 scope, UInt32 hwProfile, UInt32 keyType, UInt32 samDesired) {
			using (SafeDeviceInfoSetHandle dis = SetupApi.SetupDiGetClassDevsA(IntPtr.Zero, DeviceID, IntPtr.Zero, DICFG.DEVICEINTERFACE | DICFG.ALLCLASSES)) {
				if (dis.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
				SP_DEVINFO_DATA dd = new SP_DEVINFO_DATA(true);
				if (!SetupApi.SetupDiEnumDeviceInfo(dis, 0, ref dd))
					return null;
				IntPtr handle = SetupApi.SetupDiOpenDevRegKey(dis, ref dd, scope, hwProfile, keyType, samDesired);
				if (handle == (IntPtr)(-1)) return null;
				return RegistryKeyFromHandle(handle, true, (samDesired & (0x00000002 | 0x00000004 | 0x00000020)) != 0);
			}
		}

		private RegistryKey RegistryKeyFromHandle(IntPtr hKey, bool writable, bool ownsHandle) {
			BindingFlags privateConstructors = BindingFlags.Instance | BindingFlags.NonPublic;
			Type safeRegistryHandleType = typeof(SafeHandleZeroOrMinusOneIsInvalid).Assembly.GetType("Microsoft.Win32.SafeHandles.SafeRegistryHandle");
			Type[] safeRegistryHandleCtorTypes = new Type[] { typeof(IntPtr), typeof(bool) };
			ConstructorInfo safeRegistryHandleCtorInfo = safeRegistryHandleType.GetConstructor(privateConstructors, null, safeRegistryHandleCtorTypes, null);
			Object safeHandle = safeRegistryHandleCtorInfo.Invoke(new Object[] { hKey, ownsHandle });
			Type registryKeyType = typeof(RegistryKey);
			Type[] registryKeyConstructorTypes = new Type[] { safeRegistryHandleType, typeof(bool) };
			ConstructorInfo registryKeyCtorInfo = registryKeyType.GetConstructor(privateConstructors, null, registryKeyConstructorTypes, null);
			RegistryKey resultKey = (RegistryKey)registryKeyCtorInfo.Invoke(new Object[] { safeHandle, writable });
			return resultKey;
		}

		public Byte[] GetDeviceProperty(Guid fmtid, UInt32 pid) {
			using (SafeDeviceInfoSetHandle dis = SetupApi.SetupDiGetClassDevsA(IntPtr.Zero, DeviceID, IntPtr.Zero, DICFG.DEVICEINTERFACE | DICFG.ALLCLASSES)) {
				if (dis.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
				SP_DEVINFO_DATA dd = new SP_DEVINFO_DATA(true);
				if (!SetupApi.SetupDiEnumDeviceInfo(dis, 0, ref dd))
					return null;
				byte[] propBuffer = new byte[256];
				UInt32 requiredSize;
				UInt32 propertyType;
				DEVPROPKEY propertyKey = new DEVPROPKEY() { fmtid = fmtid, pid = pid };
				if (!SetupApi.SetupDiGetDeviceProperty(dis, ref dd, ref propertyKey, out propertyType, propBuffer, (uint)propBuffer.Length, out requiredSize, 0))
					return null;
				if (requiredSize > propBuffer.Length) {
					propBuffer = new Byte[requiredSize];
					if (!SetupApi.SetupDiGetDeviceProperty(dis, ref dd, ref propertyKey, out propertyType, propBuffer, (uint)propBuffer.Length, out requiredSize, 0))
						throw new Win32Exception(Marshal.GetLastWin32Error());
				}
				if (requiredSize < propBuffer.Length) Array.Resize(ref propBuffer, (int)requiredSize);
				return propBuffer;
			}
		}
		public String GetDevicePropertyString(Guid fmtid, UInt32 pid) {
			Byte[] buffer = GetDeviceProperty(fmtid, pid);
			if (buffer == null) return null;
			return SetupApi.GetAsString(buffer, buffer.Length);
		}

		public void Reenumerate(UInt32 flags) {
			CR ret = SetupApi.CM_Reenumerate_DevNode(DevInst, flags);
			CMException.Throw(ret, "CM_Reenumerate_DevNode");
		}

		public String DeviceDescription { get { return GetPropertyString(CMRDP.DEVICEDESC); } }
		public String[] HardwareID { get { return GetPropertyStringArray(CMRDP.HARDWAREID); } }
		public String[] CompatibleIDs { get { return GetPropertyStringArray(CMRDP.COMPATIBLEIDS); } }
		public String Service { get { return GetPropertyString(CMRDP.SERVICE); } }
		public String Class { get { return GetPropertyString(CMRDP.CLASS); } }
		public String ClassGuid { get { return GetPropertyString(CMRDP.CLASSGUID); } }
		public String DriverKey { get { return GetPropertyString(CMRDP.DRIVER); } }
		public String Manufacturer { get { return GetPropertyString(CMRDP.MFG); } }
		public String FriendlyName { get { return GetPropertyString(CMRDP.FRIENDLYNAME); } }
		public String LocationInformation { get { return GetPropertyString(CMRDP.LOCATION_INFORMATION); } }
		public String PhysicalDeviceObjectName { get { return GetPropertyString(CMRDP.PHYSICAL_DEVICE_OBJECT_NAME); } }
		public Guid? BusTypeGuid { get { return SetupApi.GetAsGuid(GetProperty(CMRDP.BUSTYPEGUID)); } }
		public Int32? BusNumber { get { return SetupApi.GetAsInt32(GetProperty(CMRDP.BUSNUMBER)); } }
		public Int32? Address { get { return SetupApi.GetAsInt32(GetProperty(CMRDP.ADDRESS)); } }
		public String EnumeratorName { get { return GetPropertyString(CMRDP.ENUMERATOR_NAME); } }

		public String[] GetInterfaces(String classGuid) {
			return GetInterfaces(new Guid(classGuid));
		}
		public String[] GetInterfaces(Guid classGuid) {
			uint len;
			CR ret = SetupApi.CM_Get_Device_Interface_List_Size(out len, ref classGuid, DeviceID, 0);
			CMException.Throw(ret, "CM_Get_Device_Interface_List_Size");
			if (len <= 1) return null;
			Byte[] buffer = new Byte[2 * len];
			ret = SetupApi.CM_Get_Device_Interface_List(ref classGuid, DeviceID, buffer, len, 0);
			CMException.Throw(ret, "CM_Get_Device_Interface_List");
			return SetupApi.GetAsStringArray(buffer, 2 * (int)len);
		}
		public Boolean SupportsInterface(Guid classGuid) {
			uint len;
			CR ret = SetupApi.CM_Get_Device_Interface_List_Size(out len, ref classGuid, DeviceID, 0);
			CMException.Throw(ret, "CM_Get_Device_Interface_List_Size");
			return len > 2;
		}

		public IList<DeviceNode> GetChildren() {
			UInt32 child;
			CR ret = SetupApi.CM_Get_Child(out child, DevInst, 0);
			if (ret == CR.NO_SUCH_DEVNODE) return null;
			CMException.Throw(ret, "CM_Get_Child");
			List<DeviceNode> list = new List<DeviceNode>();
			while (true) {
				list.Add(new DeviceNode(child));
				ret = SetupApi.CM_Get_Sibling(out child, child, 0);
				if (ret == CR.NO_SUCH_DEVNODE) break;
				CMException.Throw(ret, "CM_Get_Sibling");
			}
			return list;
		}

		public DeviceNode GetParent() {
			UInt32 node;
			CR ret = SetupApi.CM_Get_Parent(out node, DevInst, 0);
			if (ret == CR.NO_SUCH_DEVNODE) return null;
			CMException.Throw(ret, "CM_Get_Parent");
			return new DeviceNode(node);
		}

		public Boolean Present {
			get {
				UInt32 status, problem;
				CR ret = SetupApi.CM_Get_DevNode_Status(out status, out problem, DevInst, 0);
				if (ret == CR.NO_SUCH_DEVNODE) return false;
				CMException.Throw(ret, "CM_Get_DevNode_Status");
				if (status == 25174016) return false;
				return true;
			}
		}

		public override bool Equals(object obj) {
			DeviceNode other = obj as DeviceNode;
			if (ReferenceEquals(other, null)) return false;
			return DevInst == other.DevInst;
		}
		public override int GetHashCode() {
			return (int)DevInst;
		}
		public static Boolean operator ==(DeviceNode x, DeviceNode y) {
			if (ReferenceEquals(x, y)) return true;
			if (ReferenceEquals(x, null)) return false;
			if (ReferenceEquals(y, null)) return false;
			return x.DevInst == y.DevInst;
		}
		public static Boolean operator !=(DeviceNode x, DeviceNode y) {
			return !(x == y);
		}

		public void SetEnabled(Boolean enabled) {
			using (SafeDeviceInfoSetHandle dis = SetupApi.SetupDiGetClassDevsA(IntPtr.Zero, DeviceID, IntPtr.Zero, DICFG.DEVICEINTERFACE | DICFG.ALLCLASSES)) {
				if (dis.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
				SP_DEVINFO_DATA dd = new SP_DEVINFO_DATA(true);
				if (!SetupApi.SetupDiEnumDeviceInfo(dis, 0, ref dd))
					throw new Win32Exception(Marshal.GetLastWin32Error());
				SP_PROPCHANGE_PARAMS PropChangeParams = new SP_PROPCHANGE_PARAMS();
				PropChangeParams.ClassInstallHeader.cbSize = Marshal.SizeOf(PropChangeParams.ClassInstallHeader);
				PropChangeParams.ClassInstallHeader.InstallFunction = UsbApi.DIF_PROPERTYCHANGE;
				PropChangeParams.Scope = UsbApi.DICS_FLAG_GLOBAL; // or use DICS_FLAG_CONFIGSPECIFIC to limit to current HW profile
				PropChangeParams.HwProfile = 0; //Current hardware profile
				PropChangeParams.StateChange = enabled ? UsbApi.DICS_ENABLE : UsbApi.DICS_DISABLE;
				if (!SetupApi.SetupDiSetClassInstallParams(dis, ref dd, ref PropChangeParams.ClassInstallHeader, Marshal.SizeOf(PropChangeParams)))
					throw new Win32Exception(Marshal.GetLastWin32Error());
				if (!SetupApi.SetupDiCallClassInstaller(UsbApi.DIF_PROPERTYCHANGE, dis, ref dd))
					throw new Win32Exception(Marshal.GetLastWin32Error());
			}
		}
	}
}
