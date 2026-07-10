using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Utils.Reflection;
using Utils.Reflection.Reflection.Emit;

namespace UtilsTest.Reflection;

/// <summary>
/// Interface used to drive <see cref="EmitWorkerHost"/> against a real native DLL
/// (<c>kernel32.dll</c>), exercising the same code path an isolated Emit worker process runs.
/// </summary>
public interface IKernel32ProcessId : IDisposable
{
    /// <summary>Maps to the Win32 <c>GetCurrentProcessId</c> export.</summary>
    [External("GetCurrentProcessId")]
    uint GetCurrentProcessId();
}

/// <summary>
/// Exercises <see cref="EmitWorkerHost.Run"/> end-to-end (Load, Call, Shutdown) against a real
/// native DLL, without spawning a second OS process. This validates the request/response protocol
/// and the reflection-based method dispatch that an isolated worker process runs once launched by
/// <see cref="EmitWorkerProcess"/>; spawning the actual worker process is out of scope for automated
/// tests here, consistent with how <c>PluginWorkerProcessContainairisationTests</c> only exercises
/// the launch mechanics rather than a full round trip through a second process.
/// </summary>
[TestClass]
public class EmitWorkerHostLoopTests
{
    [TestMethod]
    public void Run_HandlesLoadCallShutdown_ForRealNativeDll()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("This test requires kernel32.dll, available on Windows only.");
            return;
        }

        Type interfaceType = typeof(IKernel32ProcessId);
        MethodInfo method = interfaceType.GetMethod(nameof(IKernel32ProcessId.GetCurrentProcessId))!;

        var loadRequest = new WorkerRequest
        {
            Id = 1,
            Kind = WorkerRequestKind.Load,
            InterfaceAssemblyPath = interfaceType.Assembly.Location,
            InterfaceTypeFullName = interfaceType.FullName,
            DllPath = "kernel32.dll",
            CallingConvention = CallingConvention.Winapi,
        };

        var callRequest = new WorkerRequest
        {
            Id = 2,
            Kind = WorkerRequestKind.Call,
            MethodMetadataToken = method.MetadataToken,
            ArgumentsJson = [],
        };

        var shutdownRequest = new WorkerRequest { Id = 3, Kind = WorkerRequestKind.Shutdown };

        string script = string.Join('\n',
            JsonSerializer.Serialize(loadRequest),
            JsonSerializer.Serialize(callRequest),
            JsonSerializer.Serialize(shutdownRequest));

        using var input = new StringReader(script);
        using var output = new StringWriter();

        EmitWorkerHost.Run(input, output);

        string[] responseLines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual(3, responseLines.Length);

        var loadResponse = JsonSerializer.Deserialize<WorkerResponse>(responseLines[0])!;
        Assert.IsTrue(loadResponse.Success, loadResponse.ErrorMessage);

        var callResponse = JsonSerializer.Deserialize<WorkerResponse>(responseLines[1])!;
        Assert.IsTrue(callResponse.Success, callResponse.ErrorMessage);
        uint pid = JsonSerializer.Deserialize<uint>(callResponse.ReturnValueJson!);
        Assert.AreEqual((uint)Environment.ProcessId, pid);

        var shutdownResponse = JsonSerializer.Deserialize<WorkerResponse>(responseLines[2])!;
        Assert.IsTrue(shutdownResponse.Success);
    }

    [TestMethod]
    public void Run_ReportsFailure_WhenNativeDllCannotBeLoaded()
    {
        Type interfaceType = typeof(IKernel32ProcessId);

        var loadRequest = new WorkerRequest
        {
            Id = 1,
            Kind = WorkerRequestKind.Load,
            InterfaceAssemblyPath = interfaceType.Assembly.Location,
            InterfaceTypeFullName = interfaceType.FullName,
            DllPath = "this-native-library-does-not-exist.dll",
            CallingConvention = CallingConvention.Winapi,
        };

        using var input = new StringReader(JsonSerializer.Serialize(loadRequest));
        using var output = new StringWriter();

        EmitWorkerHost.Run(input, output);

        var response = JsonSerializer.Deserialize<WorkerResponse>(output.ToString().Trim())!;
        Assert.IsFalse(response.Success);
        Assert.IsFalse(string.IsNullOrEmpty(response.ErrorMessage));
    }
}
