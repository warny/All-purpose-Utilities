using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Utils.Reflection
{
	public abstract class DllMapper : IDisposable
	{
		#region windows native methods
		/// <summary>
		/// Error indicating an attempt to load unmanaged library designated for a different architecture
		/// </summary>
		private const int ERROR_BAD_EXE_FORMAT = 0xC1;

		/// <summary>
		/// Pointeur vers la librairie
		/// </summary>
		private IntPtr dllHandle;

		/// <summary>
		/// Loads the specified module into the address space of the calling process.
		/// </summary>
		/// <param name="lpFileName">The name of the module.</param>
		/// <returns>If the function succeeds, the return value is a handle to the module. If the function fails, the return value is NULL.</returns>
		[DllImport("kernel32", CallingConvention = CallingConvention.Winapi, SetLastError = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
		private static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPStr)] string lpFileName);

		/// <summary>
		/// Frees the loaded dynamic-link library (DLL) module and, if necessary, decrements its reference count.
		/// </summary>
		/// <param name="hModule">A handle to the loaded library module.</param>
		/// <returns>If the function succeeds, the return value is nonzero. If the function fails, the return value is zero.</returns>
		[DllImport("kernel32", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool FreeLibrary(IntPtr hModule);

		/// <summary>
		/// Retrieves the address of an exported function or variable from the specified dynamic-link library (DLL).
		/// </summary>
		/// <param name="hModule">A handle to the DLL module that contains the function or variable.</param>
		/// <param name="lpProcName">The function or variable name, or the function's ordinal value.</param>
		/// <returns>If the function succeeds, the return value is the address of the exported function or variable. If the function fails, the return value is NULL.</returns>
		[DllImport("kernel32", CallingConvention = CallingConvention.Winapi, SetLastError = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
		private static extern IntPtr GetProcAddress(IntPtr hModule, [MarshalAs(UnmanagedType.LPStr)] string lpProcName);
		#endregion
		#region unix native methods

		/// <summary>
		/// Immediately resolve all symbols
		/// </summary>
		internal const int RTLD_NOW_LINUX = 0x2;

		/// <summary>
		/// Resolved symbols are not available for subsequently loaded libraries
		/// </summary>
		internal const int RTLD_LOCAL_LINUX = 0;

		/// <summary>
		/// Immediately resolve all symbols
		/// </summary>
		internal const int RTLD_NOW_MACOSX = 0x2;

		/// <summary>
		/// Resolved symbols are not available for subsequently loaded libraries
		/// </summary>
		internal const int RTLD_LOCAL_MACOSX = 0x4;

		/// <summary>
		/// Human readable string describing the most recent error that occurred from dlopen(), dlsym() or dlclose() since the last call to dlerror().
		/// </summary>
		/// <returns>Human readable string describing the most recent error or NULL if no errors have occurred since initialization or since it was last called.</returns>
		[DllImport("libdl", CallingConvention = CallingConvention.Cdecl)]
		internal static extern IntPtr dlerror();

		/// <summary>
		/// Loads the dynamic library
		/// </summary>
		/// <param name='filename'>Library filename.</param>
		/// <param name='flag'>RTLD_LAZY for lazy function call binding or RTLD_NOW immediate function call binding.</param>
		/// <returns>Handle for the dynamic library if successful, IntPtr.Zero otherwise.</returns>
		[DllImport("libdl", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
		internal static extern IntPtr dlopen(string filename, int flag);

		/// <summary>
		/// Decrements the reference count on the dynamic library handle. If the reference count drops to zero and no other loaded libraries use symbols in it, then the dynamic library is unloaded.
		/// </summary>
		/// <param name='handle'>Handle for the dynamic library.</param>
		/// <returns>Returns 0 on success, and nonzero on error.</returns>
		[DllImport("libdl", CallingConvention = CallingConvention.Cdecl)]
		internal static extern int dlclose(IntPtr handle);

		/// <summary>
		/// Returns the address where the symbol is loaded into memory.
		/// </summary>
		/// <param name='handle'>Handle for the dynamic library.</param>
		/// <param name='symbol'>Name of symbol that should be addressed.</param>
		/// <returns>Returns 0 on success, and nonzero on error.</returns>
		[DllImport("libdl", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
		internal static extern IntPtr dlsym(IntPtr handle, string symbol);
		#endregion
		#region GetFunctionPointer
		/// <summary>
		/// Gets function pointer for specified unmanaged function
		/// </summary>
		/// <param name='libraryHandle'>Dynamic library handle</param>
		/// <param name='function'>Function name</param>
		/// <returns>The function pointer for specified unmanaged function</returns>
		private static IntPtr GetFunctionPointer(IntPtr libraryHandle, string function)
		{
			if (libraryHandle == IntPtr.Zero)
				throw new ArgumentNullException("libraryHandle");

			if (string.IsNullOrEmpty(function))
				throw new ArgumentNullException("function");

			IntPtr functionPointer;

			if (Platform.IsLinux || Platform.IsMacOsX)
			{
				functionPointer = DllMapper.dlsym(libraryHandle, function);
				if (functionPointer == IntPtr.Zero)
				{
					IntPtr error = DllMapper.dlerror();
					if (error == IntPtr.Zero)
						throw new ExternalException(string.Format("Unable to get pointer for {0} function", function));
					else
						throw new ExternalException(string.Format("Unable to get pointer for {0} function. Error detail: {1}", function, Marshal.PtrToStringAnsi(error)));
				}
			}
			else
			{
				functionPointer = DllMapper.GetProcAddress(libraryHandle, function);
				if (functionPointer == IntPtr.Zero)
				{
					int win32Error = Marshal.GetLastWin32Error();
					throw new ExternalException(string.Format("Unable to get pointer for {0} function. Error code: 0x{1:X8}. Error detail: {2}", function, win32Error, new Win32Exception(win32Error).Message), win32Error);
				}
			}

			return functionPointer;
		}
		#endregion

		private static readonly Type externalAttributeType = typeof(ExternalAttribute);
		private static readonly Type delegateType = typeof(Delegate);
		public static T Create<T>(string dllPath) 
			where T : DllMapper, new()
		{
			var obj = new T();
			Type t = typeof(T);

			obj.dllHandle = LoadLibrary(dllPath);

			foreach (var member in t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.GetProperty | BindingFlags.Instance))
			{
				var attr = (ExternalAttribute)member.GetCustomAttributes(externalAttributeType, true).FirstOrDefault();
				if (attr == null) continue;
				string functionName = attr.Name ?? member.Name;
				var functionPtr = GetFunctionPointer(obj.dllHandle, functionName);
				if (member is PropertyInfo prop)
				{
					var delegateFunction = Marshal.GetDelegateForFunctionPointer(functionPtr, prop.PropertyType);
					prop.SetValue(obj, delegateFunction);
				} else if (member is FieldInfo field) {
					var delegateFunction = Marshal.GetDelegateForFunctionPointer(functionPtr, field.FieldType);
					field.SetValue(obj, delegateFunction);
				}
			}

			return obj;
		}
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		protected virtual void Dispose(bool disposing)
		{
			FreeLibrary(dllHandle);
		}

		~DllMapper() => Dispose(false);
	}

	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
	public class ExternalAttribute : Attribute
	{
		public ExternalAttribute()
		{
			this.Name = null;
		}

		public ExternalAttribute(string name)
		{
			this.Name = name;
		}

		public string Name { get; }
	}
}
