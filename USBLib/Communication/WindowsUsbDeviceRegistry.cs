using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UCIS.HWLib.Windows.Devices;
using UCIS.USBLib.Internal.Windows;

namespace UCIS.USBLib.Communication {
	public abstract class WindowsUsbDeviceRegistry {
		public DeviceNode DeviceNode { get; private set; }

		// Parsed out of the device ID
		private bool mIsDeviceIDParsed;
		private byte mInterfaceID;
		private ushort mVid;
		private ushort mPid;
		private ushort mRevision;

		private IDictionary<string, object> mDeviceProperties;

		public String DevicePath { get; private set; }
		public String DeviceID { get; private set; }
		public String SymbolicName { get { return DevicePath; } }

		private static Regex RegHardwareID = null;

		protected WindowsUsbDeviceRegistry(DeviceNode device, String interfacepath) {
			DeviceNode = device;
			DeviceID = device.DeviceID;
			DevicePath = interfacepath;
		}

		public IDictionary<string, object> DeviceProperties {
			get {
				if (mDeviceProperties == null) mDeviceProperties = SetupApi.GetSPDRPProperties(DeviceNode);
				return mDeviceProperties;
			}
		}

		private void parseDeviceID() {
			if (mIsDeviceIDParsed) return;
			if (RegHardwareID == null) {
				RegexOptions OPTIONS = RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase;
				string PATTERN = "(Vid_(?<Vid>[0-9A-F]{1,4}))|(Pid_(?<Pid>[0-9A-F]{1,4}))|(Rev_(?<Rev>[0-9]{1,4}))|(MI_(?<MI>[0-9A-F]{1,2}))";
				RegHardwareID = new Regex(PATTERN, OPTIONS);
			}
			String[] HardwareIDs = DeviceNode.GetPropertyStringArray(CMRDP.HARDWAREID);
			String HardwareID = null;
			if (HardwareIDs == null || HardwareIDs.Length < 1 || HardwareIDs[0].Length == 0) {
				HardwareID = DeviceID;
			} else {
				HardwareID = HardwareIDs[0];
			}
			foreach (Match match in RegHardwareID.Matches(HardwareID)) {
				Group group = match.Groups["Vid"];
				if (group.Success) ushort.TryParse(group.Value, NumberStyles.HexNumber, null, out mVid);
				group = match.Groups["Pid"];
				if (group.Success) ushort.TryParse(group.Value, NumberStyles.HexNumber, null, out mPid);
				group = match.Groups["Rev"];
				if (group.Success) ushort.TryParse(group.Value, NumberStyles.Integer, null, out mRevision);
				group = match.Groups["MI"];
				if (group.Success) Byte.TryParse(group.Value, NumberStyles.HexNumber, null, out mInterfaceID);
			}
			mIsDeviceIDParsed = true;
		}
		public int Vid {
			get {
				parseDeviceID();
				return mVid;
			}
		}
		public int Pid {
			get {
				parseDeviceID();
				return mPid;
			}
		}
		public byte InterfaceID {
			get {
				parseDeviceID();
				return (byte)mInterfaceID;
			}
		}

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
	}
}
