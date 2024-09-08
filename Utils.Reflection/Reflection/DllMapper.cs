using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Utils.Reflection.Reflection.Emit;

namespace Utils.Reflection;

/// <summary>
/// A class to dynamically map unmanaged DLL functions to .NET properties or fields.
/// This class also handles platform differences (Windows vs Unix-based systems).
/// </summary>
public abstract class DllMapper : IDisposable
{
	#region Windows native methods

	private const int ERROR_BAD_EXE_FORMAT = 0xC1; // Error for mismatched architecture libraries

	private IntPtr dllHandle; // Handle to the loaded DLL

	[DllImport("kernel32", CallingConvention = CallingConvention.Winapi, SetLastError = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
	private static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPStr)] string lpFileName);

	[DllImport("kernel32", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool FreeLibrary(IntPtr hModule);

	[DllImport("kernel32", CallingConvention = CallingConvention.Winapi, SetLastError = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
	private static extern IntPtr GetProcAddress(IntPtr hModule, [MarshalAs(UnmanagedType.LPStr)] string lpProcName);

	#endregion

	#region Unix native methods

	internal const int RTLD_NOW_LINUX = 0x2;
	internal const int RTLD_LOCAL_LINUX = 0;
	internal const int RTLD_NOW_MACOSX = 0x2;
	internal const int RTLD_LOCAL_MACOSX = 0x4;

	[DllImport("libdl", CallingConvention = CallingConvention.Cdecl)]
	internal static extern IntPtr dlerror();

	[DllImport("libdl", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
	internal static extern IntPtr dlopen(string filename, int flag);

	[DllImport("libdl", CallingConvention = CallingConvention.Cdecl)]
	internal static extern int dlclose(IntPtr handle);

	[DllImport("libdl", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
	internal static extern IntPtr dlsym(IntPtr handle, string symbol);

	#endregion

	#region Function pointer retrieval

	/// <summary>
	/// Retrieves the function pointer for a specified unmanaged function.
	/// </summary>
	/// <param name="libraryHandle">Handle to the loaded library.</param>
	/// <param name="function">Name of the function.</param>
	/// <returns>Pointer to the function in the loaded library.</returns>
	private static IntPtr GetFunctionPointer(IntPtr libraryHandle, string function)
	{
		if (libraryHandle == IntPtr.Zero)
			throw new ArgumentNullException(nameof(libraryHandle));

		if (string.IsNullOrWhiteSpace(function))
			throw new ArgumentNullException(nameof(function));

		IntPtr functionPointer;

		if (Platform.IsLinux || Platform.IsMacOsX)
		{
			functionPointer = dlsym(libraryHandle, function);
			if (functionPointer == IntPtr.Zero)
			{
				IntPtr error = dlerror();
				string errorMessage = error == IntPtr.Zero
					? $"Unable to get pointer for function '{function}'"
					: $"Unable to get pointer for function '{function}'. Error: {Marshal.PtrToStringAnsi(error)}";

				throw new ExternalException(errorMessage);
			}
		}
		else
		{
			functionPointer = GetProcAddress(libraryHandle, function);
			if (functionPointer == IntPtr.Zero)
			{
				int win32Error = Marshal.GetLastWin32Error();
				throw new ExternalException(
					$"Unable to get pointer for function '{function}'. Win32 Error Code: 0x{win32Error:X8}, Error Detail: {new Win32Exception(win32Error).Message}",
					win32Error);
			}
		}

		return functionPointer;
	}

	#endregion

	private static readonly Type ExternalAttributeType = typeof(ExternalAttribute);

	/// <summary>
	/// Creates an instance of a derived <see cref="DllMapper"/> class and maps the specified DLL functions to the instance's properties and fields.
	/// </summary>
	/// <typeparam name="T">A class derived from <see cref="DllMapper"/>.</typeparam>
	/// <param name="dllPath">The path to the DLL to load.</param>
	/// <returns>An instance of the derived class.</returns>
	public static T Create<T>(string dllPath) where T : DllMapper, new()
	{
		var obj = new T();
		MapDLLToObject(dllPath, obj);
		return obj;
	}

	/// <summary>
	/// Maps the functions in a DLL to the properties and fields of the provided <see cref="DllMapper"/> object.
	/// </summary>
	/// <param name="dllPath">The path to the DLL.</param>
	/// <param name="obj">The object whose members are to be mapped to DLL functions.</param>
	private static void MapDLLToObject(string dllPath, DllMapper obj)
	{
		Type objType = obj.GetType();
		obj.dllHandle = LoadLibrary(dllPath);

		if (obj.dllHandle == IntPtr.Zero)
			throw new DllNotFoundException($"Unable to load the DLL: {dllPath}");

		foreach (var member in objType.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
		{
			var attr = (ExternalAttribute)member.GetCustomAttributes(ExternalAttributeType, true).FirstOrDefault();
			if (attr == null) continue;

			string functionName = attr.Name ?? member.Name;
			var functionPtr = GetFunctionPointer(obj.dllHandle, functionName);

			if (member is PropertyInfo prop)
			{
				var delegateFunction = Marshal.GetDelegateForFunctionPointer(functionPtr, prop.PropertyType);
				prop.SetValue(obj, delegateFunction);
			}
			else if (member is FieldInfo field)
			{
				var delegateFunction = Marshal.GetDelegateForFunctionPointer(functionPtr, field.FieldType);
				field.SetValue(obj, delegateFunction);
			}
		}
	}

	/// <summary>
	/// Dynamically emits a class and maps DLL functions to its members based on an interface.
	/// </summary>
	/// <typeparam name="I">The interface that defines the functions to map.</typeparam>
	/// <param name="dllPath">The path to the DLL.</param>
	/// <param name="callingConvention">The calling convention of the functions.</param>
	/// <returns>An instance of the emitted class implementing the interface <typeparamref name="I"/>.</returns>
	public static I Emit<I>(string dllPath, CallingConvention callingConvention) where I : class, IDisposable
	{
		var obj = EmitDllMappableClass.Emit<I>(callingConvention);
		MapDLLToObject(dllPath, (DllMapper)(object)obj);
		return obj;
	}

	/// <summary>
	/// Releases the resources used by the <see cref="DllMapper"/> class.
	/// </summary>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (dllHandle != IntPtr.Zero)
		{
			FreeLibrary(dllHandle);
			dllHandle = IntPtr.Zero;
		}
	}

	~DllMapper() => Dispose(false);
}

/// <summary>
/// Attribute used to mark properties or fields to be mapped to external unmanaged functions.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class ExternalAttribute : Attribute
{
	public ExternalAttribute() { }

	public ExternalAttribute(string name)
	{
		Name = name;
	}

	/// <summary>
	/// The name of the external function to map. If not provided, the member's name will be used.
	/// </summary>
	public string Name { get; }
}
