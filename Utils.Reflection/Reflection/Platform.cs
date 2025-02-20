using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Utils.Reflection
{
	/// <summary>
	/// Utility class for runtime platform and architecture detection.
	/// </summary>
	public static class Platform
	{
		static Platform()
		{
			// --- OS Detection ---
			if (OperatingSystem.IsWindows())
			{
				IsWindows = true;
			}
			else if (OperatingSystem.IsLinux())
			{
				// Note that OperatingSystem.IsLinux() will be true on many Linux-based OSes, 
				// including some older Android environments. If you want a separate check 
				// for Android, see below.
				if (OperatingSystem.IsAndroid())
				{
					IsAndroid = true;
				}
				else
				{
					IsLinux = true;
				}
			}
			else if (OperatingSystem.IsMacOS())
			{
				// In .NET 7+, OperatingSystem.IsMacOS() is true for macOS, 
				// but iOS, tvOS, and watchOS have their own checks:
				if (OperatingSystem.IsIOS())
				{
					IsIOS = true;
				}
				else if (OperatingSystem.IsTvOS())
				{
					IsTvOS = true;
				}
				else if (OperatingSystem.IsWatchOS())
				{
					IsWatchOS = true;
				}
				else
				{
					IsMacOsX = true;
				}
			}
			else if (OperatingSystem.IsBrowser())
			{
				// WebAssembly in the browser
				IsBrowser = true;
			}
			else
			{
				// If you want to detect other OSes like FreeBSD or Solaris, 
				// you can do so below with:
				// else if (RuntimeInformation.IsOSPlatform(OSPlatform.Create("FREEBSD")))
				//     ...
				// else
				IsUnknown = true;
			}

			// --- CPU Architecture Detection ---
			switch (RuntimeInformation.ProcessArchitecture)
			{
				case Architecture.X86:
					IsX86 = true;
					break;
				case Architecture.X64:
					IsX64 = true;
					break;
				case Architecture.Arm:
					IsArm = true;
					break;
				case Architecture.Arm64:
					IsArm64 = true;
					break;
				case Architecture.Wasm:
					IsWasm = true;
					break;
				case Architecture.S390x:
					IsS390x = true;
					break;
				default:
					// Potentially other future architectures
					IsUnknownArchitecture = true;
					break;
			}

			// Initialize the NativeULongSize and StructPackingSize:
			InitializeNativeULongSize();
			InitializeStructPackingSize();
		}

		#region OS properties

		/// <summary>True if runtime platform is Windows.</summary>
		public static bool IsWindows { get; }

		/// <summary>True if runtime platform is Linux (desktop/server).</summary>
		public static bool IsLinux { get; }

		/// <summary>True if runtime platform is macOS.</summary>
		public static bool IsMacOsX { get; }

		/// <summary>True if runtime platform is iOS.</summary>
		public static bool IsIOS { get; }

		/// <summary>True if runtime platform is tvOS.</summary>
		public static bool IsTvOS { get; }

		/// <summary>True if runtime platform is watchOS.</summary>
		public static bool IsWatchOS { get; }

		/// <summary>True if runtime platform is Android.</summary>
		public static bool IsAndroid { get; }

		/// <summary>True if runtime platform is running on WebAssembly in a browser.</summary>
		public static bool IsBrowser { get; }

		/// <summary>True if runtime platform is unknown or not explicitly handled above.</summary>
		public static bool IsUnknown { get; }

		#endregion

		#region Architecture properties

		/// <summary>True if process architecture is x86 (32-bit Intel/AMD).</summary>
		public static bool IsX86 { get; }

		/// <summary>True if process architecture is x64 (64-bit Intel/AMD).</summary>
		public static bool IsX64 { get; }

		/// <summary>True if process architecture is Arm (32-bit).</summary>
		public static bool IsArm { get; }

		/// <summary>True if process architecture is Arm64 (64-bit ARM).</summary>
		public static bool IsArm64 { get; }

		/// <summary>True if process architecture is WebAssembly.</summary>
		public static bool IsWasm { get; }

		/// <summary>True if process architecture is IBM s390x.</summary>
		public static bool IsS390x { get; }

		/// <summary>
		/// True if process architecture is unknown or not explicitly handled above.
		/// </summary>
		public static bool IsUnknownArchitecture { get; }

		/// <summary>True if 64-bit runtime is used (x64, Arm64, s390x, or other 64-bit).</summary>
		public static bool Uses64BitRuntime => IntPtr.Size == 8;

		/// <summary>True if 32-bit runtime is used (x86, Arm32, etc.).</summary>
		public static bool Uses32BitRuntime => IntPtr.Size == 4;

		#endregion

		#region NativeULongSize handling

		private static int _nativeULongSize;

		/// <summary>
		/// Size of native (unmanaged) 'ulong' (e.g., CK_ULONG in PKCS#11).
		/// Default logic:
		/// - On Linux/macOS (including iOS, Android) it depends on process bitness (4 bytes on 32-bit, 8 bytes on 64-bit).
		/// - On Windows it is 4 bytes, regardless of x86/x64.
		/// This value can be overridden by setter if needed.
		/// </summary>
		public static int NativeULongSize
		{
			get => _nativeULongSize;
			private set {
				if (value != 4 && value != 8)
					throw new ArgumentException("NativeULongSize must be 4 or 8.");
				_nativeULongSize = value;
			}
		}

		private static void InitializeNativeULongSize()
		{
			if (IsWindows)
			{
				// On Windows, CK_ULONG is always 4 bytes.
				_nativeULongSize = 4;
			}
			else
			{
				// On Linux, macOS, iOS, Android, typically 4 bytes in 32-bit, 8 bytes in 64-bit
				_nativeULongSize = IntPtr.Size;
			}
		}

		#endregion

		#region StructPackingSize handling

		private static int _structPackingSize = -1;

		/// <summary>
		/// Controls alignment of unmanaged struct fields:
		/// - On Windows, CRYPTOKI structs are usually packed with 1-byte alignment.
		/// - On Unix-like platforms, they are usually packed with default alignment (0).
		/// This can be overridden by setter if needed.
		/// </summary>
		public static int StructPackingSize
		{
			get => _structPackingSize;
			set {
				if (value != 0 && value != 1)
					throw new ArgumentException("StructPackingSize must be 0 or 1.");
				_structPackingSize = value;
			}
		}

		private static void InitializeStructPackingSize()
		{
			if (IsWindows)
			{
				_structPackingSize = 1;
			}
			else
			{
				// Linux, macOS, iOS, Android, etc. typically use default alignment (0).
				_structPackingSize = 0;
			}
		}

		#endregion
	}
}
