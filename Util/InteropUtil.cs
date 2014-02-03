using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;

namespace UCIS.Util {
	public class PinnedObject : SafeHandle {
		GCHandle gch;
		public PinnedObject(Object obj)
			: base(IntPtr.Zero, true) {
			gch = GCHandle.Alloc(obj, GCHandleType.Pinned);
			SetHandle(gch.AddrOfPinnedObject());
		}
		public override bool IsInvalid { get { return handle == IntPtr.Zero; } }
		protected override bool ReleaseHandle() {
			if (gch.IsAllocated) {
				gch.Free();
				return true;
			} else {
				return false;
			}
		}
		public static implicit operator IntPtr(PinnedObject p) { return p.DangerousGetHandle(); }
		public static implicit operator PinnedObject(Array o) { return new PinnedObject(o); }
	}
	public class PinnedString : SafeHandle {
		public PinnedString(String str, Boolean unicode)
			: base(IntPtr.Zero, true) {
			SetHandle(unicode ? Marshal.StringToHGlobalUni(str) : Marshal.StringToHGlobalAnsi(str));
		}
		public override bool IsInvalid { get { return handle == IntPtr.Zero; } }
		protected override bool ReleaseHandle() {
			Marshal.FreeHGlobal(handle);
			return true;
		}
		public static implicit operator IntPtr(PinnedString p) { return p.DangerousGetHandle(); }
	}
	public class PinnedStringAnsi : PinnedString {
		public PinnedStringAnsi(String str) : base(str, false) { }
		public static implicit operator PinnedStringAnsi(String s) { return new PinnedStringAnsi(s); }
	}
	public class PinnedStringUni : PinnedString {
		public PinnedStringUni(String str) : base(str, true) { }
		public static implicit operator PinnedStringUni(String s) { return new PinnedStringUni(s); }
	}
}
