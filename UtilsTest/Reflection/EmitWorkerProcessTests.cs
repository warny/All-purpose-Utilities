using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;

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

    // ─── Item 7: timeout validation before resource allocation ───────────────────

    [TestMethod]
    public void ValidateTimeout_AcceptsPositiveFiniteDuration()
    {
        TimeSpan result = EmitWorkerProcess.ValidateTimeout(
            TimeSpan.FromSeconds(5), EmitWorkerProcess.DefaultCallTimeout, "test");
        Assert.AreEqual(TimeSpan.FromSeconds(5), result);
    }

    [TestMethod]
    public void ValidateTimeout_UsesDefaultWhenNull()
    {
        TimeSpan result = EmitWorkerProcess.ValidateTimeout(
            null, EmitWorkerProcess.DefaultCallTimeout, "test");
        Assert.AreEqual(EmitWorkerProcess.DefaultCallTimeout, result);
    }

    [TestMethod]
    public void ValidateTimeout_ThrowsOnZero()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => EmitWorkerProcess.ValidateTimeout(TimeSpan.Zero, EmitWorkerProcess.DefaultCallTimeout, "test"));
    }

    [TestMethod]
    public void ValidateTimeout_ThrowsOnNegative()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => EmitWorkerProcess.ValidateTimeout(TimeSpan.FromSeconds(-1), EmitWorkerProcess.DefaultCallTimeout, "test"));
    }

    [TestMethod]
    public void ValidateTimeout_ThrowsOnInfiniteTimeSpan()
    {
        // Timeout.InfiniteTimeSpan is -1ms, which is negative — must be rejected.
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => EmitWorkerProcess.ValidateTimeout(Timeout.InfiniteTimeSpan, EmitWorkerProcess.DefaultCallTimeout, "test"));
    }

    [TestMethod]
    public void ValidateTimeout_ThrowsWhenExceedsMaximum()
    {
        TimeSpan tooLarge = TimeSpan.FromMilliseconds((double)int.MaxValue + 1);
        Assert.ThrowsException<ArgumentOutOfRangeException>(
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

    // ─── Item 15: async lifecycle APIs ───────────────────────────────────────────

    [TestMethod]
    public void EmitWorkerProcess_ImplementsIAsyncDisposable()
    {
        // Verify that the class declares IAsyncDisposable so callers in async contexts
        // can avoid blocking a thread during the Shutdown round-trip.
        Assert.IsTrue(typeof(IAsyncDisposable).IsAssignableFrom(typeof(EmitWorkerProcess)),
            "EmitWorkerProcess must implement IAsyncDisposable (item 15).");
    }

    [TestMethod]
    public void InvokeMethodAsync_MethodExists_ReturnsTaskOfObject()
    {
        MethodInfo? method = typeof(EmitWorkerProcess).GetMethod(
            "InvokeMethodAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(method, "InvokeMethodAsync must be declared on EmitWorkerProcess.");
        Assert.IsTrue(
            typeof(System.Threading.Tasks.Task<object?>).IsAssignableFrom(method.ReturnType),
            $"InvokeMethodAsync must return Task<object?>, found {method.ReturnType}.");
    }

    [TestMethod]
    public void LoadInterfaceAsync_MethodExists_ReturnsTaskOfInt()
    {
        MethodInfo? method = typeof(EmitWorkerProcess).GetMethod(
            "LoadInterfaceAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(method, "LoadInterfaceAsync must be declared on EmitWorkerProcess.");
        Assert.IsTrue(
            typeof(System.Threading.Tasks.Task<int>).IsAssignableFrom(method.ReturnType),
            $"LoadInterfaceAsync must return Task<int>, found {method.ReturnType}.");
    }

    [TestMethod]
    public void DisposeAsync_MethodExists_ReturnsValueTask()
    {
        MethodInfo? method = typeof(EmitWorkerProcess).GetMethod("DisposeAsync");

        Assert.IsNotNull(method, "DisposeAsync must be declared on EmitWorkerProcess.");
        Assert.AreEqual(typeof(ValueTask), method.ReturnType,
            "DisposeAsync must return ValueTask.");
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
