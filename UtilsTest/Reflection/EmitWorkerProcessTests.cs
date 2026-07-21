using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;

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
}
