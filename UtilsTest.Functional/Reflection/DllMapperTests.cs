using System;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Utils.Reflection;

namespace UtilsTest.Reflection;

/// <summary>
/// Defines the portable native scalar operation used by the interface mapper test.
/// </summary>
public interface INativeMath : IDisposable
{
    /// <summary>
    /// Returns the absolute value of <paramref name="value"/>.
    /// </summary>
    [External("abs")]
    int Abs(int value);
}

/// <summary>
/// Maps the platform C runtime absolute-value function through a mapper class.
/// </summary>
public class NativeMathMapper : LibraryMapper
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int AbsDelegate(int value);

#pragma warning disable CS0649, IDE0044
    [External("abs")]
    private AbsDelegate abs = null!;
#pragma warning restore CS0649, IDE0044

    /// <summary>
    /// Returns the absolute value of <paramref name="value"/> through the native C runtime.
    /// </summary>
    public int Abs(int value) => abs(value);
}

/// <summary>
/// Validates native library mapping against a portable C-runtime function.
/// </summary>
[TestClass]
public class DllMapperTests
{
    /// <summary>
    /// Verifies direct in-process mapping of a trusted interface.
    /// </summary>
    [TestMethod]
    public void MapFromInterfaceTest()
    {
        string nativeLibrary = GetNativeLibrary();
#pragma warning disable UTILSREFL001 // This test deliberately validates the trusted in-process mapping API.
        using INativeMath mapper = LibraryMapper.EmitInProcess<INativeMath>(nativeLibrary, CallingConvention.Cdecl);
#pragma warning restore UTILSREFL001

        Assert.AreEqual(42, mapper.Abs(-42));
    }

    /// <summary>
    /// Verifies native mapping into a concrete <see cref="LibraryMapper"/> subclass.
    /// </summary>
    [TestMethod]
    public void MapFromClassTest()
    {
        string nativeLibrary = GetNativeLibrary();
        using NativeMathMapper mapper = LibraryMapper.Create<NativeMathMapper>(nativeLibrary);

        Assert.AreEqual(42, mapper.Abs(-42));
    }

    /// <summary>
    /// Gets the platform C runtime library containing the portable <c>abs</c> export.
    /// </summary>
    private static string GetNativeLibrary()
    {
        if (OperatingSystem.IsWindows())
            return "msvcrt.dll";
        if (OperatingSystem.IsLinux())
            return "libc.so.6";
        if (OperatingSystem.IsMacOS())
            return "/usr/lib/libSystem.B.dylib";

        Assert.Inconclusive("The platform C runtime library is not known for this operating system.");
        return string.Empty;
    }
}
