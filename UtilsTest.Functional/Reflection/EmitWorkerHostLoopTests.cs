using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

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
/// Interface exercising a native call slow enough to make concurrent execution of two calls
/// observable in a test (<c>Sleep</c>), used to verify <see cref="EmitWorkerHost.Run"/> genuinely
/// dispatches requests to the thread pool instead of handling them one at a time.
/// </summary>
public interface IKernel32Sleep : IDisposable
{
    /// <summary>Maps to the Win32 <c>Sleep</c> export.</summary>
    [External("Sleep")]
    void Sleep(uint milliseconds);
}

/// <summary>
/// Exercises <see cref="EmitWorkerHost.Run"/> end-to-end (Load, Call, Unload, Shutdown) against a real
/// native DLL, without spawning a second OS process. Drives the worker through an in-memory,
/// interactive request/response channel (<see cref="LineQueueTextReader"/>/<see cref="LineQueueTextWriter"/>)
/// rather than a pre-scripted <see cref="StringReader"/>: since <see cref="EmitWorkerHost.Run"/>
/// dispatches each request to the thread pool instead of handling it inline (item 34), a static script
/// cannot express "wait for this Load's response before sending a Call that depends on its handle" —
/// exactly what a real host (<see cref="EmitWorkerProcess"/>) does. Spawning the actual worker process
/// is out of scope for automated tests here, consistent with how
/// <c>PluginWorkerProcessContainairisationTests</c> only exercises the launch mechanics rather than a
/// full round trip through a second process.
/// </summary>
[TestClass]
public class EmitWorkerHostLoopTests
{
    /// <summary>
    /// A <see cref="TextReader"/> backed by a blocking queue: <see cref="ReadLine"/> blocks until a
    /// line is enqueued (or <see cref="Complete"/> is called), letting a test feed
    /// <see cref="EmitWorkerHost.Run"/> requests one at a time, interactively, from another thread —
    /// exactly like a real named pipe, but entirely in-memory.
    /// </summary>
    private sealed class LineQueueTextReader : TextReader
    {
        private readonly BlockingCollection<string?> lines = new();
        private string? _currentLine;
        private int _currentPos;

        public void Enqueue(string line) => lines.Add(line);

        /// <summary>Signals end of input: the next <see cref="Read"/> call returns <c>-1</c>.</summary>
        public void Complete() => lines.Add(null);

        // ReadLine() is kept for compatibility but is not called by ProtocolFraming.ReadBoundedLine.
        public override string? ReadLine() => lines.Take();

        public override int Read()
        {
            if (_currentLine is not null)
            {
                if (_currentPos < _currentLine.Length)
                    return _currentLine[_currentPos++];

                // Line exhausted — emit the newline terminator, then clear the buffer.
                _currentLine = null;
                _currentPos = 0;
                return '\n';
            }

            string? next = lines.Take();
            if (next is null) return -1;

            if (next.Length == 0) return '\n';

            _currentLine = next;
            _currentPos = 1;
            return next[0];
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lines.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// A <see cref="TextWriter"/> backed by a blocking queue: each <see cref="WriteLine(string?)"/> call
    /// enqueues the line, and a test can <see cref="TakeResponse"/> to block until a specific response
    /// (matched by <see cref="WorkerResponse.Id"/>) is available, regardless of the order responses
    /// actually arrive in (not guaranteed under concurrent dispatch — see the class remarks).
    /// </summary>
    private sealed class LineQueueTextWriter : TextWriter
    {
        private readonly BlockingCollection<string> lines = new();

        public override Encoding Encoding => Encoding.UTF8;

        public override void WriteLine(string? value) => lines.Add(value ?? string.Empty);

        /// <summary>Blocks until the response with the given id has been written, then returns it.</summary>
        public WorkerResponse TakeResponse(int id, TimeSpan timeout)
        {
            var deadline = Stopwatch.StartNew();
            while (deadline.Elapsed < timeout)
            {
                TimeSpan remaining = timeout - deadline.Elapsed;
                if (remaining <= TimeSpan.Zero || !lines.TryTake(out string? line, remaining))
                {
                    break;
                }

                var response = JsonSerializer.Deserialize<WorkerResponse>(line)!;
                if (response.Id == id)
                {
                    return response;
                }

                // Not the response this call is waiting for (another concurrently in-flight request's
                // response arrived first) — put it back for whichever TakeResponse call is waiting on it.
                lines.Add(line);
            }

            throw new TimeoutException($"No response with id {id} arrived within {timeout}.");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lines.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    [TestMethod]
    public void Run_HandlesLoadCallUnloadShutdown_ForRealNativeDll()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("This test requires kernel32.dll, available on Windows only.");
            return;
        }

        Type interfaceType = typeof(IKernel32ProcessId);
        MethodInfo method = interfaceType.GetMethod(nameof(IKernel32ProcessId.GetCurrentProcessId))!;

        using var input = new LineQueueTextReader();
        using var output = new LineQueueTextWriter();
        Task hostTask = Task.Run(() => EmitWorkerHost.Run(input, output));
        TimeSpan timeout = TimeSpan.FromSeconds(10);

        input.Enqueue(JsonSerializer.Serialize(new WorkerRequest
        {
            Id = 1,
            Kind = WorkerRequestKind.Load,
            InterfaceAssemblyPath = interfaceType.Assembly.Location,
            InterfaceTypeFullName = interfaceType.FullName,
            DllPath = "kernel32.dll",
            CallingConvention = CallingConvention.Winapi,
        }));
        WorkerResponse loadResponse = output.TakeResponse(1, timeout);
        Assert.IsTrue(loadResponse.Success, loadResponse.ErrorMessage);
        int handle = loadResponse.Handle;

        input.Enqueue(JsonSerializer.Serialize(new WorkerRequest
        {
            Id = 2, Kind = WorkerRequestKind.Call, Handle = handle,
            MethodMetadataToken = method.MetadataToken, ArgumentsJson = [],
        }));
        WorkerResponse callResponse = output.TakeResponse(2, timeout);
        Assert.IsTrue(callResponse.Success, callResponse.ErrorMessage);
        Assert.AreEqual((uint)Environment.ProcessId, JsonSerializer.Deserialize<uint>(callResponse.ReturnValueJson!));

        input.Enqueue(JsonSerializer.Serialize(new WorkerRequest { Id = 3, Kind = WorkerRequestKind.Unload, Handle = handle }));
        WorkerResponse unloadResponse = output.TakeResponse(3, timeout);
        Assert.IsTrue(unloadResponse.Success);

        input.Enqueue(JsonSerializer.Serialize(new WorkerRequest { Id = 4, Kind = WorkerRequestKind.Shutdown }));
        WorkerResponse shutdownResponse = output.TakeResponse(4, timeout);
        Assert.IsTrue(shutdownResponse.Success);

        Assert.IsTrue(hostTask.Wait(timeout));
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

        using var input = new LineQueueTextReader();
        using var output = new LineQueueTextWriter();
        Task hostTask = Task.Run(() => EmitWorkerHost.Run(input, output));
        TimeSpan timeout = TimeSpan.FromSeconds(10);

        input.Enqueue(JsonSerializer.Serialize(new WorkerRequest
        {
            Id = 1, Kind = WorkerRequestKind.Load,
            InterfaceAssemblyPath = processIdType.Assembly.Location, InterfaceTypeFullName = processIdType.FullName,
            DllPath = "kernel32.dll", CallingConvention = CallingConvention.Winapi,
        }));
        int processIdHandle = output.TakeResponse(1, timeout).Handle;

        input.Enqueue(JsonSerializer.Serialize(new WorkerRequest
        {
            Id = 2, Kind = WorkerRequestKind.Load,
            InterfaceAssemblyPath = tickCountType.Assembly.Location, InterfaceTypeFullName = tickCountType.FullName,
            DllPath = "kernel32.dll", CallingConvention = CallingConvention.Winapi,
        }));
        int tickCountHandle = output.TakeResponse(2, timeout).Handle;
        Assert.AreNotEqual(processIdHandle, tickCountHandle);

        input.Enqueue(JsonSerializer.Serialize(new WorkerRequest
        {
            Id = 3, Kind = WorkerRequestKind.Call, Handle = processIdHandle,
            MethodMetadataToken = processIdMethod.MetadataToken, ArgumentsJson = [],
        }));
        WorkerResponse callProcessIdResponse = output.TakeResponse(3, timeout);
        Assert.IsTrue(callProcessIdResponse.Success, callProcessIdResponse.ErrorMessage);
        Assert.AreEqual((uint)Environment.ProcessId, JsonSerializer.Deserialize<uint>(callProcessIdResponse.ReturnValueJson!));

        input.Enqueue(JsonSerializer.Serialize(new WorkerRequest
        {
            Id = 4, Kind = WorkerRequestKind.Call, Handle = tickCountHandle,
            MethodMetadataToken = tickCountMethod.MetadataToken, ArgumentsJson = [],
        }));
        Assert.IsTrue(output.TakeResponse(4, timeout).Success);

        input.Enqueue(JsonSerializer.Serialize(new WorkerRequest { Id = 5, Kind = WorkerRequestKind.Unload, Handle = processIdHandle }));
        Assert.IsTrue(output.TakeResponse(5, timeout).Success);

        // Handle for tick count must still work after unloading the unrelated process-id handle.
        input.Enqueue(JsonSerializer.Serialize(new WorkerRequest
        {
            Id = 6, Kind = WorkerRequestKind.Call, Handle = tickCountHandle,
            MethodMetadataToken = tickCountMethod.MetadataToken, ArgumentsJson = [],
        }));
        Assert.IsTrue(output.TakeResponse(6, timeout).Success);

        // The unloaded handle must be rejected, not silently misrouted to another loaded interface.
        input.Enqueue(JsonSerializer.Serialize(new WorkerRequest
        {
            Id = 7, Kind = WorkerRequestKind.Call, Handle = processIdHandle,
            MethodMetadataToken = processIdMethod.MetadataToken, ArgumentsJson = [],
        }));
        Assert.IsFalse(output.TakeResponse(7, timeout).Success);

        input.Enqueue(JsonSerializer.Serialize(new WorkerRequest { Id = 8, Kind = WorkerRequestKind.Shutdown }));
        Assert.IsTrue(output.TakeResponse(8, timeout).Success);
        Assert.IsTrue(hostTask.Wait(timeout));
    }

    [TestMethod]
    public void Run_ExecutesTwoSlowCallsConcurrently_NotSequentially()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("This test requires kernel32.dll, available on Windows only.");
            return;
        }

        // The whole point of item 34: two Call requests dispatched back-to-back, without waiting for
        // the first one's response, must run in parallel on the worker (Task.Run dispatch in
        // EmitWorkerHost.Run) rather than being handled one at a time. Each call sleeps 800ms; if they
        // ran sequentially this test would take >= 1600ms, comfortably distinguishable from the
        // concurrent case (~800ms) even accounting for scheduling jitter.
        Type interfaceType = typeof(IKernel32Sleep);
        MethodInfo method = interfaceType.GetMethod(nameof(IKernel32Sleep.Sleep))!;
        const uint sleepMilliseconds = 800;

        using var input = new LineQueueTextReader();
        using var output = new LineQueueTextWriter();
        Task hostTask = Task.Run(() => EmitWorkerHost.Run(input, output));
        TimeSpan timeout = TimeSpan.FromSeconds(10);

        input.Enqueue(JsonSerializer.Serialize(new WorkerRequest
        {
            Id = 1, Kind = WorkerRequestKind.Load,
            InterfaceAssemblyPath = interfaceType.Assembly.Location, InterfaceTypeFullName = interfaceType.FullName,
            DllPath = "kernel32.dll", CallingConvention = CallingConvention.Winapi,
        }));
        WorkerResponse loadResponse = output.TakeResponse(1, timeout);
        Assert.IsTrue(loadResponse.Success, loadResponse.ErrorMessage);
        int handle = loadResponse.Handle;

        string sleepArgumentJson = JsonSerializer.Serialize(sleepMilliseconds);
        var stopwatch = Stopwatch.StartNew();

        input.Enqueue(JsonSerializer.Serialize(new WorkerRequest
        {
            Id = 2, Kind = WorkerRequestKind.Call, Handle = handle,
            MethodMetadataToken = method.MetadataToken, ArgumentsJson = [sleepArgumentJson],
        }));
        input.Enqueue(JsonSerializer.Serialize(new WorkerRequest
        {
            Id = 3, Kind = WorkerRequestKind.Call, Handle = handle,
            MethodMetadataToken = method.MetadataToken, ArgumentsJson = [sleepArgumentJson],
        }));

        WorkerResponse first = output.TakeResponse(2, timeout);
        WorkerResponse second = output.TakeResponse(3, timeout);
        stopwatch.Stop();

        Assert.IsTrue(first.Success, first.ErrorMessage);
        Assert.IsTrue(second.Success, second.ErrorMessage);
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < sleepMilliseconds * 2,
            $"Two {sleepMilliseconds}ms calls took {stopwatch.ElapsedMilliseconds}ms — expected well under " +
            $"{sleepMilliseconds * 2}ms if they ran concurrently instead of one after the other.");

        input.Enqueue(JsonSerializer.Serialize(new WorkerRequest { Id = 4, Kind = WorkerRequestKind.Shutdown }));
        Assert.IsTrue(output.TakeResponse(4, timeout).Success);
        Assert.IsTrue(hostTask.Wait(timeout));
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

    // ─── Finding #2: per-handle call leasing prevents Unload/Call race ──────────

    [TestMethod]
    public void Run_UnloadWhileCallInProgress_WaitsForCallBeforeDisposing()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("This test requires kernel32.dll, available on Windows only.");
            return;
        }

        // Sends a long-running Call (Sleep 500ms) and an Unload for the same handle concurrently.
        // With the per-handle call-lease (TryBeginCall/EndCall), the Unload must wait for the
        // sleeping call to complete before it can dispose the native handle, and both responses
        // must indicate success. Without the lease the Unload could dispose the handle while the
        // call is still executing its delegate — a use-after-free of the native library.
        Type sleepType = typeof(IKernel32Sleep);
        MethodInfo sleepMethod = sleepType.GetMethod(nameof(IKernel32Sleep.Sleep))!;
        TimeSpan timeout = TimeSpan.FromSeconds(10);
        const uint sleepMs = 500;

        using var input = new LineQueueTextReader();
        using var output = new LineQueueTextWriter();
        Task hostTask = Task.Run(() => EmitWorkerHost.Run(input, output));

        input.Enqueue(JsonSerializer.Serialize(new WorkerRequest
        {
            Id = 1, Kind = WorkerRequestKind.Load,
            InterfaceAssemblyPath = sleepType.Assembly.Location, InterfaceTypeFullName = sleepType.FullName,
            DllPath = "kernel32.dll", CallingConvention = CallingConvention.Winapi,
        }));
        int handle = output.TakeResponse(1, timeout).Handle;

        // Enqueue a slow Call and an Unload back-to-back without waiting for the Call to respond.
        input.Enqueue(JsonSerializer.Serialize(new WorkerRequest
        {
            Id = 2, Kind = WorkerRequestKind.Call, Handle = handle,
            MethodMetadataToken = sleepMethod.MetadataToken,
            ArgumentsJson = [JsonSerializer.Serialize(sleepMs)],
        }));
        input.Enqueue(JsonSerializer.Serialize(new WorkerRequest { Id = 3, Kind = WorkerRequestKind.Unload, Handle = handle }));

        WorkerResponse callResponse = output.TakeResponse(2, timeout);
        WorkerResponse unloadResponse = output.TakeResponse(3, timeout);

        Assert.IsTrue(callResponse.Success, $"Call must succeed: {callResponse.ErrorMessage}");
        Assert.IsTrue(unloadResponse.Success, $"Unload must succeed: {unloadResponse.ErrorMessage}");

        input.Enqueue(JsonSerializer.Serialize(new WorkerRequest { Id = 4, Kind = WorkerRequestKind.Shutdown }));
        Assert.IsTrue(output.TakeResponse(4, timeout).Success);
        Assert.IsTrue(hostTask.Wait(timeout));
    }

    // ─── Finding #3: Shutdown truthfulness ───────────────────────────────────────

    [TestMethod]
    public void Run_Shutdown_ReturnsSuccessWhenAllCallsDrained()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("This test requires kernel32.dll, available on Windows only.");
            return;
        }

        Type interfaceType = typeof(IKernel32ProcessId);
        MethodInfo method = interfaceType.GetMethod(nameof(IKernel32ProcessId.GetCurrentProcessId))!;
        TimeSpan timeout = TimeSpan.FromSeconds(10);

        using var input = new LineQueueTextReader();
        using var output = new LineQueueTextWriter();
        Task hostTask = Task.Run(() => EmitWorkerHost.Run(input, output));

        input.Enqueue(JsonSerializer.Serialize(new WorkerRequest
        {
            Id = 1, Kind = WorkerRequestKind.Load,
            InterfaceAssemblyPath = interfaceType.Assembly.Location,
            InterfaceTypeFullName = interfaceType.FullName,
            DllPath = "kernel32.dll", CallingConvention = CallingConvention.Winapi,
        }));
        WorkerResponse loadResponse = output.TakeResponse(1, timeout);
        Assert.IsTrue(loadResponse.Success, loadResponse.ErrorMessage);

        // Shutdown without Unloading: drain is trivially complete (no active calls at this point).
        // The Shutdown response must be Success = true.
        input.Enqueue(JsonSerializer.Serialize(new WorkerRequest { Id = 2, Kind = WorkerRequestKind.Shutdown }));
        WorkerResponse shutdownResponse = output.TakeResponse(2, timeout);
        Assert.IsTrue(shutdownResponse.Success,
            "Shutdown with no active calls must produce Success = true (drain is complete).");
        Assert.IsNull(shutdownResponse.ErrorMessage,
            "A successful shutdown must carry no error message.");

        Assert.IsTrue(hostTask.Wait(timeout));
    }

    // ─── Finding #4: loaded mappings disposed when worker loop ends ───────────────

    [TestMethod]
    public void Run_LoadedInterfaceNotUnloaded_IsDisposedOnShutdown()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("This test requires kernel32.dll, available on Windows only.");
            return;
        }

        // Loads a kernel32 interface, then shuts down WITHOUT sending an Unload request.
        // The fix: the finally block in EmitWorkerHost.Run disposes the remaining mapping.
        // Observable here as: Run returns without throwing (prior to the fix, a resource leak;
        // post-fix, deterministic cleanup — difficult to observe without touching native internals,
        // but the critical contract is that the host process is not left holding a dangling reference).
        Type interfaceType = typeof(IKernel32ProcessId);
        TimeSpan timeout = TimeSpan.FromSeconds(10);

        using var input = new LineQueueTextReader();
        using var output = new LineQueueTextWriter();
        Task hostTask = Task.Run(() => EmitWorkerHost.Run(input, output));

        input.Enqueue(JsonSerializer.Serialize(new WorkerRequest
        {
            Id = 1, Kind = WorkerRequestKind.Load,
            InterfaceAssemblyPath = interfaceType.Assembly.Location,
            InterfaceTypeFullName = interfaceType.FullName,
            DllPath = "kernel32.dll", CallingConvention = CallingConvention.Winapi,
        }));
        Assert.IsTrue(output.TakeResponse(1, timeout).Success);

        // Shutdown without Unload — the interface is still in the loaded dictionary.
        input.Enqueue(JsonSerializer.Serialize(new WorkerRequest { Id = 2, Kind = WorkerRequestKind.Shutdown }));
        WorkerResponse shutdownResponse = output.TakeResponse(2, timeout);
        Assert.IsTrue(shutdownResponse.Success,
            "Shutdown must complete gracefully even when interfaces were not explicitly Unloaded.");

        // Run must return (not hang) and must not throw.
        Assert.IsTrue(hostTask.Wait(timeout),
            "EmitWorkerHost.Run must return after Shutdown even when interfaces remain loaded.");
    }
}
