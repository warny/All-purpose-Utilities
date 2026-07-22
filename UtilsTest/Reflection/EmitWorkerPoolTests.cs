using System;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Utils.Reflection.Reflection.Emit;

namespace UtilsTest.Reflection;

/// <summary>
/// Validates <see cref="EmitWorkerPool"/>'s disposal semantics without starting a real isolated worker
/// process (spawning one is out of scope for automated tests here, same accepted limitation as
/// <c>EmitWorkerProxyTests</c>/<c>EmitWorkerProtocolTests</c>).
/// </summary>
[TestClass]
public class EmitWorkerPoolTests
{
    public interface ITinyInterface : IDisposable
    {
        int Foo(int value);
    }

    // ─── Item 7: timeout validation in pool constructor ───────────────────────────

    [TestMethod]
    public void Constructor_WithZeroLoadTimeout_ThrowsArgumentOutOfRange()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => new EmitWorkerPool(loadTimeout: TimeSpan.Zero));
    }

    [TestMethod]
    public void Constructor_WithNegativeCallTimeout_ThrowsArgumentOutOfRange()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => new EmitWorkerPool(callTimeout: TimeSpan.FromSeconds(-1)));
    }

    [TestMethod]
    public void Constructor_WithInfiniteLoadTimeout_ThrowsArgumentOutOfRange()
    {
        // Timeout.InfiniteTimeSpan is -1ms — not a valid positive finite duration.
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => new EmitWorkerPool(loadTimeout: System.Threading.Timeout.InfiniteTimeSpan));
    }

    [TestMethod]
    public void Constructor_WithValidPositiveTimeouts_DoesNotThrow()
    {
        using var pool = new EmitWorkerPool(
            loadTimeout: System.TimeSpan.FromSeconds(10),
            callTimeout: System.TimeSpan.FromSeconds(15));
        // No exception: validation passes.
    }

    [TestMethod]
    public void Constructor_WithNullTimeouts_DoesNotThrow()
    {
        using var pool = new EmitWorkerPool(loadTimeout: null, callTimeout: null);
        // Null means use the defaults — must be valid.
    }

    [TestMethod]
    public void Emit_AfterDispose_ThrowsObjectDisposedException()
    {
        var pool = new EmitWorkerPool();
        pool.Dispose();

        Assert.ThrowsException<ObjectDisposedException>(
            () => pool.Emit<ITinyInterface>("does-not-matter.dll", CallingConvention.Winapi));
    }

    [TestMethod]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var pool = new EmitWorkerPool();

        pool.Dispose();
        pool.Dispose();
    }

    [TestMethod]
    public void Dispose_WithoutEverCallingEmit_DoesNotThrow()
    {
        // No worker was ever started (lazy on first Emit<TInterface> call): disposing must be a no-op,
        // not attempt to shut down a worker that was never created.
        using var pool = new EmitWorkerPool();
    }
}
