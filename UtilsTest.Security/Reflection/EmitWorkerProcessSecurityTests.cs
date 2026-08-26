using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Utils.Reflection.ProcessIsolation;
using Utils.Reflection.Reflection.Emit;

namespace UtilsTest.Security.Reflection;

/// <summary>
/// Validates the sandbox permissions, resource-allocation guards (timeouts, abandoned-call
/// retirement), and fail-closed sandbox fallback behavior of <see cref="EmitWorkerProcess"/>,
/// without spawning a real second process.
/// </summary>
[TestClass]
public class EmitWorkerProcessSecurityTests
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

    // ─── Item 7: timeout validation before resource allocation ───────────────────

    [TestMethod]
    public void ValidateTimeout_ThrowsOnZero()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => EmitWorkerProcess.ValidateTimeout(TimeSpan.Zero, EmitWorkerProcess.DefaultCallTimeout, "test"));
    }

    [TestMethod]
    public void ValidateTimeout_ThrowsOnNegative()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => EmitWorkerProcess.ValidateTimeout(TimeSpan.FromSeconds(-1), EmitWorkerProcess.DefaultCallTimeout, "test"));
    }

    [TestMethod]
    public void ValidateTimeout_ThrowsOnInfiniteTimeSpan()
    {
        // Timeout.InfiniteTimeSpan is -1ms, which is negative — must be rejected.
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => EmitWorkerProcess.ValidateTimeout(Timeout.InfiniteTimeSpan, EmitWorkerProcess.DefaultCallTimeout, "test"));
    }

    [TestMethod]
    public void ValidateTimeout_ThrowsWhenExceedsMaximum()
    {
        TimeSpan tooLarge = TimeSpan.FromMilliseconds((double)int.MaxValue + 1);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => EmitWorkerProcess.ValidateTimeout(tooLarge, EmitWorkerProcess.DefaultCallTimeout, "test"));
    }

    [TestMethod]
    public void ValidateTimeout_AcceptsMaximumSupportedValue()
    {
        // int.MaxValue milliseconds is the largest value CancellationTokenSource accepts.
        TimeSpan maxSupported = TimeSpan.FromMilliseconds(int.MaxValue);
        TimeSpan result = EmitWorkerProcess.ValidateTimeout(maxSupported, EmitWorkerProcess.DefaultCallTimeout, "test");
        Assert.AreEqual(maxSupported, result);
    }

    // ─── Review #495 point 3: worker retirement on orphaned Load ─────────────────

    [TestMethod]
    public void LoadInterfaceAsync_HasRetireAfterOrphanedLoad_PrivateMethod()
    {
        // Verify the retirement helper exists — its runtime behavior is covered by integration tests.
        MethodInfo? method = typeof(EmitWorkerProcess).GetMethod(
            "RetireAfterOrphanedLoad",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(method, "RetireAfterOrphanedLoad must be declared on EmitWorkerProcess.");
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

        var wrapped = Assert.ThrowsExactly<TargetInvocationException>(() => method.Invoke(null, args));

        Assert.IsInstanceOfType<InvalidOperationException>(wrapped.InnerException,
            "Sandbox launch failure must propagate as InvalidOperationException, not be swallowed.");

        // Verify the sandbox ref was NOT cleared to null — the old fallback code nulled it out;
        // the new code lets the exception propagate without touching the container reference.
        Assert.IsNotNull(args[2], "Sandbox reference must not be cleared on failure (fail-closed contract).");
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
}
