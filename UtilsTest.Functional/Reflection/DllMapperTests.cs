using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Utils.Reflection;
using UtilsTest.NativeInterop;

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
    /// Verifies the public isolated mapper API through a controlled executable that handles worker startup.
    /// </summary>
    [TestMethod]
    public async Task MapFromInterfaceInIsolatedWorkerTest()
    {
        string executableName = OperatingSystem.IsWindows()
            ? "UtilsTest.LibraryMapperHost.exe"
            : "UtilsTest.LibraryMapperHost";
        string executablePath = Path.Combine(AppContext.BaseDirectory, "LibraryMapperTestHost", executableName);
        _ = GetNativeLibrary();

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(executablePath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        process.StartInfo.Environment["DOTNET_ROOT"] = GetDotnetRoot();

        Assert.IsTrue(process.Start(), "The controlled LibraryMapper host did not start.");
        Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        string standardOutput = await standardOutputTask;
        string standardError = await standardErrorTask;
        Assert.AreEqual(0, process.ExitCode, standardError);
        Assert.AreEqual("42", standardOutput.Trim());
    }

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
    /// Gets the .NET installation root from the runtime directory used by the current test process.
    /// </summary>
    private static string GetDotnetRoot()
    {
        return Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", ".."));
    }

    /// <summary>
    /// Gets the platform C runtime library containing the portable <c>abs</c> export.
    /// </summary>
    private static string GetNativeLibrary()
    {
        try
        {
            return NativeRuntimeLibraryResolver.ResolveWithExport("abs");
        }
        catch (PlatformNotSupportedException exception)
        {
            Assert.Inconclusive(exception.Message);
            return string.Empty;
        }
    }
}
