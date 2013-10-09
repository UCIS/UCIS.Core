using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace UCIS.USBLib.Internal.Windows {
	[SuppressUnmanagedCodeSecurity]
	static class Kernel32 {
		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		internal static extern SafeFileHandle CreateFile(string fileName, NativeFileAccess fileAccess, NativeFileShare fileShare, IntPtr securityAttributes, NativeFileMode creationDisposition, NativeFileFlag flags, IntPtr template);
		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess, int dwShareMode, IntPtr lpSecurityAttributes, int dwCreationDisposition, int dwFlagsAndAttributes, IntPtr hTemplateFile);

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
		unsafe internal static extern bool DeviceIoControl(SafeHandle hDevice, int IoControlCode, IntPtr InBuffer, int nInBufferSize, IntPtr OutBuffer, int nOutBufferSize, out int pBytesReturned, NativeOverlapped* Overlapped);
		[DllImport("kernel32.dll", SetLastError = true)]
		unsafe internal static extern bool DeviceIoControl(SafeHandle hDevice, int IoControlCode, void* InBuffer, int nInBufferSize, void* OutBuffer, int nOutBufferSize, out int pBytesReturned, NativeOverlapped* Overlapped);
		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
		unsafe internal static extern bool GetOverlappedResult(SafeHandle hFile, NativeOverlapped* lpOverlapped, out int lpNumberOfBytesTransferred, Boolean bWait);

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern bool DeviceIoControl(SafeHandle hDevice, int dwIoControlCode, IntPtr lpInBuffer, int nInBufferSize, out USB_ROOT_HUB_NAME lpOutBuffer, int nOutBufferSize, out int lpBytesReturned, IntPtr lpOverlapped);
		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern bool DeviceIoControl(SafeHandle hDevice, int dwIoControlCode, [In] ref USB_NODE_INFORMATION lpInBuffer, int nInBufferSize, out USB_NODE_INFORMATION lpOutBuffer, int nOutBufferSize, out int lpBytesReturned, IntPtr lpOverlapped);
		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern bool DeviceIoControl(SafeHandle hDevice, int dwIoControlCode, [In] ref USB_DESCRIPTOR_REQUEST lpInBuffer, int nInBufferSize, Byte[] lpOutBuffer, int nOutBufferSize, out int lpBytesReturned, IntPtr lpOverlapped);
		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern bool DeviceIoControl(SafeHandle hDevice, int dwIoControlCode, [In] ref USB_NODE_CONNECTION_DRIVERKEY_NAME lpInBuffer, int nInBufferSize, out USB_NODE_CONNECTION_DRIVERKEY_NAME lpOutBuffer, int nOutBufferSize, out int lpBytesReturned, IntPtr lpOverlapped);
		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern bool DeviceIoControl(SafeHandle hDevice, int dwIoControlCode, [In] ref USB_NODE_CONNECTION_INFORMATION_EX lpInBuffer, int nInBufferSize, out USB_NODE_CONNECTION_INFORMATION_EX lpOutBuffer, int nOutBufferSize, out int lpBytesReturned, IntPtr lpOverlapped);
		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern bool DeviceIoControl(SafeHandle hDevice, int dwIoControlCode, [In] ref USB_NODE_CONNECTION_NAME lpInBuffer, int nInBufferSize, out USB_NODE_CONNECTION_NAME lpOutBuffer, int nOutBufferSize, out int lpBytesReturned, IntPtr lpOverlapped);

		public const uint GENERIC_READ = 0x80000000;
		public const uint GENERIC_WRITE = 0x40000000;
		//public const uint GENERIC_EXECUTE = 0x20000000;
		//public const uint GENERIC_ALL = 0x10000000;
		public const uint FILE_FLAG_NO_BUFFERING = 0x20000000;

		public const int FILE_SHARE_READ = 0x1;
		public const int FILE_SHARE_WRITE = 0x2;
		public const int OPEN_EXISTING = 0x3;

		public const int FILE_ATTRIBUTE_SYSTEM = 0x00000004;
		public const int FILE_FLAG_OVERLAPPED = 0x40000000;
	}

	[Flags]
	enum NativeFileAccess : uint {
		FILE_SPECIAL = 0,
		FILE_APPEND_DATA = (0x0004), // file
		FILE_READ_DATA = (0x0001), // file & pipe
		FILE_WRITE_DATA = (0x0002), // file & pipe
		FILE_READ_EA = (0x0008), // file & directory
		FILE_WRITE_EA = (0x0010), // file & directory
		FILE_READ_ATTRIBUTES = (0x0080), // all
		FILE_WRITE_ATTRIBUTES = (0x0100), // all
		DELETE = 0x00010000,
		READ_CONTROL = (0x00020000),
		WRITE_DAC = (0x00040000),
		WRITE_OWNER = (0x00080000),
		SYNCHRONIZE = (0x00100000),
		STANDARD_RIGHTS_REQUIRED = (0x000F0000),
		STANDARD_RIGHTS_READ = (READ_CONTROL),
		STANDARD_RIGHTS_WRITE = (READ_CONTROL),
		STANDARD_RIGHTS_EXECUTE = (READ_CONTROL),
		STANDARD_RIGHTS_ALL = (0x001F0000),
		SPECIFIC_RIGHTS_ALL = (0x0000FFFF),
		FILE_GENERIC_READ = (STANDARD_RIGHTS_READ | FILE_READ_DATA | FILE_READ_ATTRIBUTES | FILE_READ_EA | SYNCHRONIZE),
		FILE_GENERIC_WRITE = (STANDARD_RIGHTS_WRITE | FILE_WRITE_DATA | FILE_WRITE_ATTRIBUTES | FILE_WRITE_EA | FILE_APPEND_DATA | SYNCHRONIZE),
		SPECIAL = 0
	}

	enum NativeFileMode : uint {
		CREATE_NEW = 1,
		CREATE_ALWAYS = 2,
		OPEN_EXISTING = 3,
		OPEN_ALWAYS = 4,
		TRUNCATE_EXISTING = 5,
	}

	[Flags]
	enum NativeFileShare : uint {
		NONE = 0,
		FILE_SHARE_READ = 0x00000001,
		FILE_SHARE_WRITE = 0x00000002,
		FILE_SHARE_DEELETE = 0x00000004,
	}

	[Flags]
	enum NativeFileFlag : uint {
		FILE_ATTRIBUTE_READONLY = 0x00000001,
		FILE_ATTRIBUTE_HIDDEN = 0x00000002,
		FILE_ATTRIBUTE_SYSTEM = 0x00000004,
		FILE_ATTRIBUTE_DIRECTORY = 0x00000010,
		FILE_ATTRIBUTE_ARCHIVE = 0x00000020,
		FILE_ATTRIBUTE_DEVICE = 0x00000040,
		FILE_ATTRIBUTE_NORMAL = 0x00000080,
		FILE_ATTRIBUTE_TEMPORARY = 0x00000100,
		FILE_ATTRIBUTE_SPARSE_FILE = 0x00000200,
		FILE_ATTRIBUTE_REPARSE_POINT = 0x00000400,
		FILE_ATTRIBUTE_COMPRESSED = 0x00000800,
		FILE_ATTRIBUTE_OFFLINE = 0x00001000,
		FILE_ATTRIBUTE_NOT_CONTENT_INDEXED = 0x00002000,
		FILE_ATTRIBUTE_ENCRYPTED = 0x00004000,
		FILE_FLAG_WRITE_THROUGH = 0x80000000,
		FILE_FLAG_OVERLAPPED = 0x40000000,
		FILE_FLAG_NO_BUFFERING = 0x20000000,
		FILE_FLAG_RANDOM_ACCESS = 0x10000000,
		FILE_FLAG_SEQUENTIAL_SCAN = 0x08000000,
		FILE_FLAG_DELETE_ON_CLOSE = 0x04000000,
		FILE_FLAG_BACKUP_SEMANTICS = 0x02000000,
		FILE_FLAG_POSIX_SEMANTICS = 0x01000000,
		FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000,
		FILE_FLAG_OPEN_NO_RECALL = 0x00100000,
		FILE_FLAG_FIRST_PIPE_INSTANCE = 0x00080000,
	}
}