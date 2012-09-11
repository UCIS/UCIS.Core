using System;
using System.Management;
using System.IO;

namespace UCIS.Windows {
	public class ServiceManager {
		private ManagementObject _serviceObject;

		public const string StartMode_Automatic = "Automatic";
		public const string StartMode_Manual = "Manual";
		public const string StartMode_Disabled = "Disabled";

		private ServiceManager(ManagementObject ob) {
			_serviceObject = ob;
		}

		public static ServiceManager GetServiceByPath(string FileName) {
			ManagementClass mc = new ManagementClass("Win32_Service");
			foreach (ManagementObject ob in mc.GetInstances()) {
				string Value = (string)ob.GetPropertyValue("PathName");
				if (Value == null) continue;
				int Position = Value.IndexOf(FileName);
				if (Position == 0 || Position == 1) { //Either <filepath> or <"filepath">
					return new ServiceManager(ob);
				}
			}
			return null;
		}
		public static ServiceManager GetServiceByName(string Name) {
			ManagementClass mc = new ManagementClass("Win32_Service");
			foreach (ManagementObject ob in mc.GetInstances()) {
				string Value = (string)ob.GetPropertyValue("Name");
				if (Value == null) continue;
				if (Value.Equals(Name, StringComparison.InvariantCultureIgnoreCase)) return new ServiceManager(ob);
			}
			return null;
		}
		public static ServiceManager Create(string Name, string DisplayName, string PathName, string StartMode, bool DesktopInteract, string StartName, string StartPassword) {
			ManagementClass mc = new ManagementClass("Win32_Service");
			UInt32 ret;
			ret = (UInt32)mc.InvokeMethod("Create", new Object[] {
					Name, //Name
					DisplayName, //DisplayName
					PathName, //PathName
					16, //ServiceType (16 = own process)
					1, //ErrorControl (1 = user is notified)
					StartMode, //StartMode
					DesktopInteract, //DesktopInteract
					StartName, //StartName (null = LocalSystem)
					StartPassword, //StartPassword
					null, //LoadOrderGroup
					null, //LoadOrderGroupDependencies
					null //ServiceDependencies
				});
			if (ret != 0) throw new ManagementException("Could not create service (code " + ret.ToString() + ")");
			return GetServiceByName(Name);
		}

		public string Name { get { return (string)_serviceObject.GetPropertyValue("Name"); } }
		public string DisplayName { get { return (string)_serviceObject.GetPropertyValue("DisplayName"); } }
		public string PathName { get { return (string)_serviceObject.GetPropertyValue("PathName"); } }
		public string StartMode {
			get { return (string)_serviceObject.GetPropertyValue("StartMode"); }
			set { _serviceObject.InvokeMethod("ChangeStartMode", new Object[] { value }); Refresh(); }
		}
		public bool DesktopInteract { get { return (bool)_serviceObject.GetPropertyValue("DesktopInteract"); } }
		public string StartName { get { return (string)_serviceObject.GetPropertyValue("StartName"); } }
		public string StartPassword { get { return (string)_serviceObject.GetPropertyValue("StartPassword"); } }

		public void Change(string DisplayName, string PathName, string StartMode, bool DesktopInteract, string StartName, string StartPassword) {
			UInt32 ret;
			ret = (UInt32)_serviceObject.InvokeMethod("Change", new Object[] {
					DisplayName, //DisplayName
					PathName, //PathName
					16, //ServiceType (16 = own process)
					1, //ErrorControl (1 = user is notified)
					StartMode, //StartMode
					DesktopInteract, //DesktopInteract
					StartName, //StartName (null = LocalSystem)
					StartPassword, //StartPassword
					null, //LoadOrderGroup
					null, //LoadOrderGroupDependencies
					null //ServiceDependencies
				});
			if (ret != 0) throw new ManagementException("Could not change service (code " + ret.ToString() + ")");
			Refresh();
		}

		public void Refresh() {
			_serviceObject.Get();
		}

		public void Start() {
			_serviceObject.InvokeMethod("StartService", null);
			Refresh();
		}
		public void Stop() {
			_serviceObject.InvokeMethod("StopService", null);
			Refresh();
		}

		public bool Running {
			get {
				Refresh();
				return (bool)_serviceObject.GetPropertyValue("Started");
			}
		}

		public String State {
			get {
				Refresh();
				return (String)_serviceObject.GetPropertyValue("State");
			}
		}
	}
}