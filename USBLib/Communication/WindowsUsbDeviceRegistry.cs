using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UCIS.HWLib.Windows.Devices;
using UCIS.USBLib.Internal.Windows;

namespace UCIS.USBLib.Communication {
	public abstract class WindowsUsbDeviceRegistry {
		public DeviceNode DeviceNode { get; private set; }

		public static Boolean DecodeDeviceIDs(DeviceNode device, out int vendorID, out int productID, out int revision, out int interfaceID) {
			String[] hwids = device.HardwareID;
			String hwid = null;
			if (hwids == null || hwids.Length < 1 || hwids[0].Length == 0) {
				hwid = device.DeviceID;
			} else {
				hwid = hwids[0];
			}
			vendorID = productID = revision = interfaceID = -1;
			foreach (String token in hwid.Split(new Char[] { '\\', '#', '&' }, StringSplitOptions.None)) {
				if (token.StartsWith("VID_", StringComparison.InvariantCultureIgnoreCase)) {
					if (!Int32.TryParse(token.Substring(4), NumberStyles.HexNumber, null, out vendorID)) vendorID = -1;
				} else if (token.StartsWith("PID_", StringComparison.InvariantCultureIgnoreCase)) {
					if (!Int32.TryParse(token.Substring(4), NumberStyles.HexNumber, null, out productID)) productID = -1;
				} else if (token.StartsWith("REV_", StringComparison.InvariantCultureIgnoreCase)) {
					if (!Int32.TryParse(token.Substring(4), NumberStyles.Integer, null, out revision)) revision = -1;
				} else if (token.StartsWith("MI_", StringComparison.InvariantCultureIgnoreCase)) {
					if (!Int32.TryParse(token.Substring(3), NumberStyles.HexNumber, null, out interfaceID)) interfaceID = -1;
				}
			}
			return vendorID != -1 && productID != -1;
		}

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
			int vid, pid, rev, mid;
			if (!DecodeDeviceIDs(DeviceNode, out vid, out pid, out rev, out mid)) return;
			mVid = (UInt16)vid;
			mPid = (UInt16)pid;
			mRevision = (UInt16)rev;
			mInterfaceID = (Byte)mid;
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
