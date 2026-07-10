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
/// A second, distinct interface (different native export) used alongside
/// <see cref="IKernel32ProcessId"/> to verify that a single worker holding several loaded interfaces
/// (see <see cref="EmitWorkerPool"/>) routes each <see cref="WorkerRequestKind.Call"/> to the right one
/// by handle, instead of picking whichever was loaded most recently.
/// </summary>
public interface IKernel32TickCount : IDisposable
{
    /// <summary>Maps to the Win32 <c>GetTickCount</c> export.</summary>
    [External("GetTickCount")]
    uint GetTickCount();
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
            // The first Load in a fresh Run() call is always allocated handle 1 (nextHandle starts at 0).
            Handle = 1,
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
        Assert.AreEqual(1, loadResponse.Handle);

        var callResponse = JsonSerializer.Deserialize<WorkerResponse>(responseLines[1])!;
        Assert.IsTrue(callResponse.Success, callResponse.ErrorMessage);
        uint pid = JsonSerializer.Deserialize<uint>(callResponse.ReturnValueJson!);
        Assert.AreEqual((uint)Environment.ProcessId, pid);

        var shutdownResponse = JsonSerializer.Deserialize<WorkerResponse>(responseLines[2])!;
        Assert.IsTrue(shutdownResponse.Success);
    }

    [TestMethod]
    public void Run_RoutesCallsByHandle_WhenTwoInterfacesAreLoadedOnTheSameWorker()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("This test requires kernel32.dll, available on Windows only.");
            return;
        }

        // Exercises the item-32 protocol change (EmitWorkerPool): a single worker holding two
        // independently loaded interfaces must route each Call to the right one by handle, and
        // Unload-ing one must not disturb the other.
        Type processIdType = typeof(IKernel32ProcessId);
        MethodInfo processIdMethod = processIdType.GetMethod(nameof(IKernel32ProcessId.GetCurrentProcessId))!;
        Type tickCountType = typeof(IKernel32TickCount);
        MethodInfo tickCountMethod = tickCountType.GetMethod(nameof(IKernel32TickCount.GetTickCount))!;

        var loadProcessId = new WorkerRequest
        {
            Id = 1,
            Kind = WorkerRequestKind.Load,
            InterfaceAssemblyPath = processIdType.Assembly.Location,
            InterfaceTypeFullName = processIdType.FullName,
            DllPath = "kernel32.dll",
            CallingConvention = CallingConvention.Winapi,
        };

        var loadTickCount = new WorkerRequest
        {
            Id = 2,
            Kind = WorkerRequestKind.Load,
            InterfaceAssemblyPath = tickCountType.Assembly.Location,
            InterfaceTypeFullName = tickCountType.FullName,
            DllPath = "kernel32.dll",
            CallingConvention = CallingConvention.Winapi,
        };

        // Handles are allocated in Load order starting at 1: processId -> 1, tickCount -> 2.
        var callProcessId = new WorkerRequest
        {
            Id = 3, Kind = WorkerRequestKind.Call, Handle = 1,
            MethodMetadataToken = processIdMethod.MetadataToken, ArgumentsJson = [],
        };
        var callTickCount = new WorkerRequest
        {
            Id = 4, Kind = WorkerRequestKind.Call, Handle = 2,
            MethodMetadataToken = tickCountMethod.MetadataToken, ArgumentsJson = [],
        };
        var unloadProcessId = new WorkerRequest { Id = 5, Kind = WorkerRequestKind.Unload, Handle = 1 };
        // Handle 1 is gone now; tick count (handle 2) must still work.
        var callTickCountAgain = new WorkerRequest
        {
            Id = 6, Kind = WorkerRequestKind.Call, Handle = 2,
            MethodMetadataToken = tickCountMethod.MetadataToken, ArgumentsJson = [],
        };
        // Handle 1 is gone: a Call against it must fail, not silently hit the wrong interface.
        var callProcessIdAfterUnload = new WorkerRequest
        {
            Id = 7, Kind = WorkerRequestKind.Call, Handle = 1,
            MethodMetadataToken = processIdMethod.MetadataToken, ArgumentsJson = [],
        };
        var shutdownRequest = new WorkerRequest { Id = 8, Kind = WorkerRequestKind.Shutdown };

        string script = string.Join('\n',
            JsonSerializer.Serialize(loadProcessId),
            JsonSerializer.Serialize(loadTickCount),
            JsonSerializer.Serialize(callProcessId),
            JsonSerializer.Serialize(callTickCount),
            JsonSerializer.Serialize(unloadProcessId),
            JsonSerializer.Serialize(callTickCountAgain),
            JsonSerializer.Serialize(callProcessIdAfterUnload),
            JsonSerializer.Serialize(shutdownRequest));

        using var input = new StringReader(script);
        using var output = new StringWriter();

        EmitWorkerHost.Run(input, output);

        string[] responseLines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual(8, responseLines.Length);

        var loadProcessIdResponse = JsonSerializer.Deserialize<WorkerResponse>(responseLines[0])!;
        Assert.IsTrue(loadProcessIdResponse.Success, loadProcessIdResponse.ErrorMessage);
        Assert.AreEqual(1, loadProcessIdResponse.Handle);

        var loadTickCountResponse = JsonSerializer.Deserialize<WorkerResponse>(responseLines[1])!;
        Assert.IsTrue(loadTickCountResponse.Success, loadTickCountResponse.ErrorMessage);
        Assert.AreEqual(2, loadTickCountResponse.Handle);

        var callProcessIdResponse = JsonSerializer.Deserialize<WorkerResponse>(responseLines[2])!;
        Assert.IsTrue(callProcessIdResponse.Success, callProcessIdResponse.ErrorMessage);
        Assert.AreEqual((uint)Environment.ProcessId, JsonSerializer.Deserialize<uint>(callProcessIdResponse.ReturnValueJson!));

        var callTickCountResponse = JsonSerializer.Deserialize<WorkerResponse>(responseLines[3])!;
        Assert.IsTrue(callTickCountResponse.Success, callTickCountResponse.ErrorMessage);

        var unloadResponse = JsonSerializer.Deserialize<WorkerResponse>(responseLines[4])!;
        Assert.IsTrue(unloadResponse.Success);

        var callTickCountAgainResponse = JsonSerializer.Deserialize<WorkerResponse>(responseLines[5])!;
        Assert.IsTrue(callTickCountAgainResponse.Success, callTickCountAgainResponse.ErrorMessage);

        var callProcessIdAfterUnloadResponse = JsonSerializer.Deserialize<WorkerResponse>(responseLines[6])!;
        Assert.IsFalse(callProcessIdAfterUnloadResponse.Success);

        var shutdownResponse = JsonSerializer.Deserialize<WorkerResponse>(responseLines[7])!;
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
