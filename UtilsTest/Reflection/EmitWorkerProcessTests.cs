using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Utils.Reflection.ProcessIsolation;
using Utils.Reflection.Reflection.Emit;

namespace UtilsTest.Reflection;

/// <summary>
/// Validates the pure argument/permission-building logic extracted from
/// <see cref="EmitWorkerProcess.Start"/>, without spawning a real second process.
/// </summary>
[TestClass]
public class EmitWorkerProcessTests
{
    [TestMethod]
    public void CreateWorkerPermissions_AlwaysAllowsDiskRead()
    {
        Assert.IsTrue(EmitWorkerProcess.CreateWorkerPermissions().AllowDiskRead);
    }

    [TestMethod]
    public void CreateWorkerPermissions_AllowsDiskWrite_OnlyOnNonWindows()
    {
        bool allowDiskWrite = EmitWorkerProcess.CreateWorkerPermissions().AllowDiskWrite;

        Assert.AreEqual(!OperatingSystem.IsWindows(), allowDiskWrite);
    }

    [TestMethod]
    public void BuildWorkerArguments_SameExecutableAsEntryAssembly_OmitsAssemblyPath()
    {
        string entryAssemblyLocation = Assembly.GetEntryAssembly()!.Location;
        string exePath = System.IO.Path.ChangeExtension(entryAssemblyLocation, ".exe");

        string[] arguments = EmitWorkerProcess.BuildWorkerArguments(exePath, "pipe-name");

        CollectionAssert.AreEqual(
            new[] { "--utils-reflection-emit-worker", "pipe-name" },
            arguments);
    }

    [TestMethod]
    public void BuildWorkerArguments_GenericLauncher_PrependsEntryAssemblyPath()
    {
        string entryAssemblyLocation = Assembly.GetEntryAssembly()!.Location;

        string[] arguments = EmitWorkerProcess.BuildWorkerArguments("/usr/bin/dotnet", "pipe-name");

        CollectionAssert.AreEqual(
            new[] { entryAssemblyLocation, "--utils-reflection-emit-worker", "pipe-name" },
            arguments);
    }

    [TestMethod]
    public void DefaultLoadTimeout_Is30Seconds()
    {
        Assert.AreEqual(TimeSpan.FromSeconds(30), EmitWorkerProcess.DefaultLoadTimeout);
    }

    [TestMethod]
    public void DefaultCallTimeout_Is30Seconds()
    {
        Assert.AreEqual(TimeSpan.FromSeconds(30), EmitWorkerProcess.DefaultCallTimeout);
    }

    // ─── Item 41: worker retirement after abandoned calls ────────────────────────

    [TestMethod]
    public void MaxAbandonedCalls_IsPositive()
    {
        Assert.IsTrue(EmitWorkerProcess.MaxAbandonedCalls > 0,
            "MaxAbandonedCalls must be positive so the retirement threshold is reachable.");
    }

    [TestMethod]
    public void MaxAbandonedCalls_IsSmallEnoughToRetireUnreliableWorker()
    {
        // A very large threshold would never actually protect against state accumulation.
        Assert.IsTrue(EmitWorkerProcess.MaxAbandonedCalls <= 20,
            "MaxAbandonedCalls should be low enough to retire a consistently slow worker promptly.");
    }

    // ─── Item 37: fail-closed sandbox fallback ───────────────────────────────────

    /// <summary>
    /// The previous implementation swallowed any exception from <c>sandbox.StartProcess</c> and
    /// silently relaunched the worker as an unsandboxed child process. This test verifies that a
    /// sandbox launch failure now propagates, so the caller cannot inadvertently receive an
    /// unsandboxed process when a sandbox was requested.
    /// </summary>
    [TestMethod]
    public void StartWorkerProcess_SandboxLaunchFailure_PropagatesException()
    {
        MethodInfo method = typeof(EmitWorkerProcess)
            .GetMethod("StartWorkerProcess", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
            ?? throw new InvalidOperationException("Reflection: StartWorkerProcess not found.");

        IProcessContainer throwingContainer = new ThrowingProcessContainer();
        object?[] args = { "dummy.exe", "test-pipe", throwingContainer };

        var wrapped = Assert.ThrowsException<TargetInvocationException>(() => method.Invoke(null, args));

        Assert.IsInstanceOfType<InvalidOperationException>(wrapped.InnerException,
            "Sandbox launch failure must propagate as InvalidOperationException, not be swallowed.");

        // Verify the sandbox ref was NOT cleared to null — the old fallback code nulled it out;
        // the new code lets the exception propagate without touching the container reference.
        Assert.IsNotNull(args[2], "Sandbox reference must not be cleared on failure (fail-closed contract).");
    }

    // ─── Finding #7: timeout validation ─────────────────────────────────────────

    [TestMethod]
    public void ValidateTimeout_PositiveDuration_DoesNotThrow()
    {
        EmitWorkerProcess.ValidateTimeout(TimeSpan.FromSeconds(30), "timeout");
        EmitWorkerProcess.ValidateTimeout(TimeSpan.FromMilliseconds(1), "timeout");
        EmitWorkerProcess.ValidateTimeout(TimeSpan.FromMilliseconds(int.MaxValue), "timeout");
    }

    [TestMethod]
    public void ValidateTimeout_ZeroDuration_ThrowsArgumentOutOfRangeException()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => EmitWorkerProcess.ValidateTimeout(TimeSpan.Zero, "timeout"));
    }

    [TestMethod]
    public void ValidateTimeout_NegativeDuration_ThrowsArgumentOutOfRangeException()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => EmitWorkerProcess.ValidateTimeout(TimeSpan.FromMilliseconds(-1), "timeout"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => EmitWorkerProcess.ValidateTimeout(TimeSpan.FromSeconds(-100), "timeout"));
    }

    [TestMethod]
    public void ValidateTimeout_ExcessivelyLargeDuration_ThrowsArgumentOutOfRangeException()
    {
        // int.MaxValue ms is the limit; int.MaxValue + 1 ms must be rejected.
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => EmitWorkerProcess.ValidateTimeout(
                TimeSpan.FromMilliseconds((double)int.MaxValue + 1), "timeout"));
    }

    // ─── Finding #5: write failure must clean up pending entry immediately ────────

    [TestMethod]
    public void SendAndReceive_WriteFails_NoPendingEntryIsLeft()
    {
        // A stream that blocks Read() until released (keeps the reader loop alive) but throws on Write
        // (simulates a broken pipe after connection). This lets us verify that when the frame write
        // fails, SendAndReceive removes the pending entry immediately instead of leaving it registered
        // until the per-request timeout fires (which would incorrectly credit an abandoned-call count
        // increment to a request that was never sent).
        using var blockingRead = new BlockingStream();
        using var throwingWrite = new ThrowingStream();

        var reader = new StreamReader(blockingRead);
        // AutoFlush = true matches the real EmitWorkerProcess.Start behaviour and ensures every
        // WriteLine flushes immediately to the underlying stream, making the IOException visible.
        var writer = new StreamWriter(throwingWrite) { AutoFlush = true };

        using var process = EmitWorkerProcess.CreateForTesting(reader, writer, TimeSpan.FromSeconds(30));

        // Give the reader loop time to start and block on blockingRead.Read().
        Thread.Sleep(50);

        Assert.ThrowsException<InvalidOperationException>(
            () => process.LoadInterface(
                typeof(IDisposable), "does-not-matter.dll",
                CallingConvention.Winapi, TimeSpan.FromSeconds(30)));

        Assert.AreEqual(0, process.PendingCount,
            "The pending entry for the failed write must be removed immediately, " +
            "not left to expire via the per-request timeout.");
        Assert.AreEqual(0, process.AbandonedCallCount,
            "A frame that was never written must not increment the abandoned-call counter.");

        // Let the reader loop exit cleanly.
        blockingRead.Release();
    }

    // ─── Finding #6: IsHealthy reflects worker liveness ─────────────────────────

    [TestMethod]
    public void IsHealthy_NewWorker_IsTrue()
    {
        using var blockingRead = new BlockingStream();
        using var process = EmitWorkerProcess.CreateForTesting(
            new StreamReader(blockingRead),
            new StreamWriter(new MemoryStream()) { AutoFlush = true },
            TimeSpan.FromSeconds(1));

        Assert.IsTrue(process.IsHealthy, "A freshly created worker must be healthy.");

        blockingRead.Release();
    }

    [TestMethod]
    public void IsHealthy_AfterConnectionFault_IsFalse()
    {
        using var blockingRead = new BlockingStream();
        using var process = EmitWorkerProcess.CreateForTesting(
            new StreamReader(blockingRead),
            new StreamWriter(new MemoryStream()) { AutoFlush = true },
            TimeSpan.FromSeconds(1));

        // Release the blocking stream → reader loop sees EOF → sets connectionFault.
        blockingRead.Release();

        // Wait until the reader loop detects the EOF and marks the worker unhealthy.
        var deadline = Stopwatch.StartNew();
        while (process.IsHealthy && deadline.Elapsed < TimeSpan.FromSeconds(5))
            Thread.Sleep(5);

        Assert.IsFalse(process.IsHealthy,
            "Worker must become unhealthy once its connection stream reaches EOF.");
    }

    // ─── Finding #9: remote diagnostics suppressed by default ────────────────────

    /// <summary>A trivially supportable interface used as the Load target in diagnostics tests.</summary>
    private interface IDiagnosticsTestInterface
    {
        int GetValue();
    }

    [TestMethod]
    public void LoadInterface_ByDefault_OmitsRemoteDiagnosticsFromException()
    {
        using var responseStream = new EnqueueableStream();
        using var requestDrain = new StreamWriter(new MemoryStream()) { AutoFlush = true };
        using var process = EmitWorkerProcess.CreateForTesting(
            new StreamReader(responseStream), requestDrain, TimeSpan.FromSeconds(5));

        EmitWorkerInvocationException? caught = null;
        Task task = Task.Run(() =>
        {
            try { process.LoadInterface(typeof(IDiagnosticsTestInterface), "any.dll", CallingConvention.Winapi, TimeSpan.FromSeconds(5)); }
            catch (EmitWorkerInvocationException ex) { caught = ex; }
        });

        // Wait until SendAndReceive has registered the pending entry (Id=1).
        var deadline = Stopwatch.StartNew();
        while (process.PendingCount == 0 && deadline.Elapsed < TimeSpan.FromSeconds(5))
            Thread.Sleep(1);

        responseStream.Enqueue(JsonSerializer.Serialize(new WorkerResponse
        {
            Id = 1, Success = false,
            ErrorMessage = "Something failed",
            ErrorTypeName = "System.Exception",
            ErrorStackTrace = "   at Worker.DoWork() in C:\\worker\\source.cs:line 42",
        }));
        responseStream.Complete();

        task.Wait(TimeSpan.FromSeconds(5));
        Assert.IsNotNull(caught, "A failure response must throw EmitWorkerInvocationException.");
        Assert.IsNull(caught.RemoteStackTrace,
            "By default, RemoteStackTrace must be suppressed to avoid exposing worker-internal paths.");
        Assert.IsNull(caught.RemoteExceptionTypeName,
            "By default, RemoteExceptionTypeName must be suppressed to avoid exposing generated type names.");
    }

    [TestMethod]
    public void LoadInterface_WithDiagnosticsEnabled_IncludesRemoteDiagnostics()
    {
        using var responseStream = new EnqueueableStream();
        using var requestDrain = new StreamWriter(new MemoryStream()) { AutoFlush = true };
        using var process = EmitWorkerProcess.CreateForTesting(
            new StreamReader(responseStream), requestDrain, TimeSpan.FromSeconds(5), includeDiagnostics: true);

        EmitWorkerInvocationException? caught = null;
        Task task = Task.Run(() =>
        {
            try { process.LoadInterface(typeof(IDiagnosticsTestInterface), "any.dll", CallingConvention.Winapi, TimeSpan.FromSeconds(5)); }
            catch (EmitWorkerInvocationException ex) { caught = ex; }
        });

        var deadline = Stopwatch.StartNew();
        while (process.PendingCount == 0 && deadline.Elapsed < TimeSpan.FromSeconds(5))
            Thread.Sleep(1);

        const string expectedTrace = "   at Worker.DoWork() in C:\\worker\\source.cs:line 42";
        const string expectedType = "System.Exception";
        responseStream.Enqueue(JsonSerializer.Serialize(new WorkerResponse
        {
            Id = 1, Success = false,
            ErrorMessage = "Something failed",
            ErrorTypeName = expectedType,
            ErrorStackTrace = expectedTrace,
        }));
        responseStream.Complete();

        task.Wait(TimeSpan.FromSeconds(5));
        Assert.IsNotNull(caught, "A failure response must throw EmitWorkerInvocationException.");
        Assert.AreEqual(expectedTrace, caught.RemoteStackTrace,
            "With diagnostics enabled, RemoteStackTrace must be included.");
        Assert.AreEqual(expectedType, caught.RemoteExceptionTypeName,
            "With diagnostics enabled, RemoteExceptionTypeName must be included.");
    }

    // ─── Finding #15: async/cancellable load and unload APIs ─────────────────────

    [TestMethod]
    public void LoadInterfaceAsync_AlreadyCancelledToken_ThrowsImmediately()
    {
        using var blockingRead = new BlockingStream();
        using var process = EmitWorkerProcess.CreateForTesting(
            new StreamReader(blockingRead),
            new StreamWriter(new MemoryStream()) { AutoFlush = true },
            TimeSpan.FromSeconds(5));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var task = process.LoadInterfaceAsync(
            typeof(IDiagnosticsTestInterface), "any.dll",
            CallingConvention.Winapi, TimeSpan.FromSeconds(5), cts.Token);

        Assert.IsTrue(task.IsCompleted, "Task must complete synchronously for a pre-cancelled token.");
        Assert.IsTrue(task.IsCanceled, "Task must be cancelled, not faulted or successful.");

        blockingRead.Release();
    }

    [TestMethod]
    public void LoadInterfaceAsync_CancellationDuringWait_ThrowsOperationCanceledException()
    {
        // EnqueueableStream blocks until we enqueue a response — we cancel before delivering one.
        using var responseStream = new EnqueueableStream();
        using var requestDrain = new StreamWriter(new MemoryStream()) { AutoFlush = true };
        using var process = EmitWorkerProcess.CreateForTesting(
            new StreamReader(responseStream), requestDrain, TimeSpan.FromSeconds(30));

        Thread.Sleep(50);

        using var cts = new CancellationTokenSource();
        Task<int> task = process.LoadInterfaceAsync(
            typeof(IDiagnosticsTestInterface), "any.dll",
            CallingConvention.Winapi, TimeSpan.FromSeconds(30), cts.Token);

        // Wait until the request is registered as pending.
        var deadline = Stopwatch.StartNew();
        while (process.PendingCount == 0 && deadline.Elapsed < TimeSpan.FromSeconds(5))
            Thread.Sleep(1);

        cts.Cancel();

        // Wait for the task to complete without calling task.Wait() (which throws AggregateException
        // on a cancelled task).
        var waitDeadline = Stopwatch.StartNew();
        while (!task.IsCompleted && waitDeadline.Elapsed < TimeSpan.FromSeconds(5))
            Thread.Sleep(5);

        Assert.IsTrue(task.IsCompleted, "LoadInterfaceAsync must complete after cancellation.");
        Assert.IsTrue(task.IsCanceled,
            "LoadInterfaceAsync must be cancelled when the caller's token fires.");

        // Worker must not be retired as unhealthy — external cancellation is not a timeout.
        Assert.AreEqual(0, process.AbandonedCallCount,
            "An externally cancelled request must not increment the abandoned-call counter.");
        Assert.IsTrue(process.IsHealthy,
            "The worker must remain healthy after an externally cancelled load.");

        responseStream.Complete();
    }

    // ─── Finding #13: unsolicited response IDs trigger a connection fault ────────

    [TestMethod]
    public void RunReaderLoop_ResponseIdNeverSent_SetsConnectionFault()
    {
        // No requests have been sent, so nextId == 0. Any response ID is therefore impossible.
        using var responseStream = new EnqueueableStream();
        using var process = EmitWorkerProcess.CreateForTesting(
            new StreamReader(responseStream),
            new StreamWriter(new MemoryStream()) { AutoFlush = true },
            TimeSpan.FromSeconds(5));

        Thread.Sleep(50); // give the reader loop time to start

        Assert.IsTrue(process.IsHealthy, "Worker must be healthy before injecting the bad response.");

        responseStream.Enqueue(JsonSerializer.Serialize(new WorkerResponse { Id = 99999, Success = true }));
        responseStream.Complete();

        var deadline = Stopwatch.StartNew();
        while (process.IsHealthy && deadline.Elapsed < TimeSpan.FromSeconds(5))
            Thread.Sleep(5);

        Assert.IsFalse(process.IsHealthy,
            "Worker must become unhealthy when the peer sends a response for a never-issued request ID.");
    }

    [TestMethod]
    public void RunReaderLoop_LateResponseForTimedOutRequest_IsDroppedSilently()
    {
        // A request is sent with a very short timeout so it times out before the response
        // arrives. The worker must remain healthy when the late response is finally delivered,
        // because the ID is in the range of sent IDs — it is an expected late response.
        using var responseStream = new EnqueueableStream();
        using var requestDrain = new StreamWriter(new MemoryStream()) { AutoFlush = true };
        using var process = EmitWorkerProcess.CreateForTesting(
            new StreamReader(responseStream), requestDrain, TimeSpan.FromSeconds(5));

        Thread.Sleep(50);

        // SendAndReceive with a 50ms timeout — times out before we inject any response.
        Assert.ThrowsException<TimeoutException>(
            () => process.LoadInterface(
                typeof(IDiagnosticsTestInterface), "any.dll",
                CallingConvention.Winapi, TimeSpan.FromMilliseconds(50)));

        // ID 1 was sent but is no longer pending. Inject the late response now.
        responseStream.Enqueue(JsonSerializer.Serialize(new WorkerResponse { Id = 1, Success = true, Handle = 42 }));

        Thread.Sleep(100); // give the reader loop time to process the late response

        Assert.IsTrue(process.IsHealthy,
            "A late response for a formerly-pending (now timed-out) request must be silently dropped; " +
            "the worker must remain healthy.");

        responseStream.Complete();
    }

    /// <summary>Stub container that always throws to simulate a failed sandbox launch.</summary>
    private sealed class ThrowingProcessContainer : IProcessContainer
    {
        public Process StartProcess(string executablePath, IEnumerable<string> arguments)
            => throw new InvalidOperationException("Simulated sandbox launch failure.");

        public void GrantDirectoryReadAccess(string directoryPath) { }

        public bool TryGetSecurityIdentifier(out SecurityIdentifier? securityIdentifier)
        {
            securityIdentifier = null;
            return false;
        }

        public void Dispose() { }
    }

    /// <summary>Stream that blocks Read() calls until <see cref="Release"/> is called, simulating a live but idle pipe.</summary>
    private sealed class BlockingStream : Stream
    {
        private readonly ManualResetEventSlim gate = new(false);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) { gate.Wait(); return 0; }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        /// <summary>Unblocks Read(), which then returns 0 (end-of-stream).</summary>
        public void Release() => gate.Set();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                gate.Set(); // Unblock any waiting Read() so the reader loop can exit.
            base.Dispose(disposing);
        }
    }

    /// <summary>Stream whose Write method always throws, simulating a broken pipe.</summary>
    private sealed class ThrowingStream : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) =>
            throw new System.IO.IOException("Simulated broken-pipe write failure.");
    }

    /// <summary>
    /// Readable stream backed by a blocking queue: <see cref="Enqueue"/> lines to deliver them in
    /// order; call <see cref="Complete"/> to signal EOF. Used to feed pre-scripted responses to a
    /// <see cref="EmitWorkerProcess"/> reader loop in unit tests without timing on actual I/O.
    /// </summary>
    private sealed class EnqueueableStream : Stream
    {
        private readonly BlockingCollection<byte[]> chunks = new();
        private byte[]? current;
        private int pos;
        private bool chunksDisposed;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        /// <summary>Enqueues <paramref name="line"/> followed by a newline for the reader loop to consume.</summary>
        public void Enqueue(string line) => chunks.Add(Encoding.UTF8.GetBytes(line + "\n"));

        /// <summary>Signals end-of-stream: subsequent <see cref="Read"/> calls return 0 once all queued data is consumed.</summary>
        public void Complete() => chunks.CompleteAdding();

        public override int Read(byte[] buffer, int offset, int count)
        {
            while (current == null || pos >= current.Length)
            {
                if (!chunks.TryTake(out byte[]? next, Timeout.Infinite))
                    return 0;
                current = next;
                pos = 0;
            }

            int toCopy = Math.Min(count, current.Length - pos);
            System.Array.Copy(current, pos, buffer, offset, toCopy);
            pos += toCopy;
            return toCopy;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !chunksDisposed)
            {
                chunksDisposed = true;
                chunks.CompleteAdding();
                chunks.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
