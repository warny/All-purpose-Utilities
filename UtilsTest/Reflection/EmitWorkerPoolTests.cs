using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

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

    // ─── Finding #7: timeout validation in constructor ───────────────────────────

    [TestMethod]
    public void Constructor_WithPositiveTimeouts_DoesNotThrow()
    {
        using var pool = new EmitWorkerPool(
            loadTimeout: TimeSpan.FromSeconds(30),
            callTimeout: TimeSpan.FromSeconds(10));
    }

    [TestMethod]
    public void Constructor_WithNullTimeouts_DoesNotThrow()
    {
        using var pool = new EmitWorkerPool(loadTimeout: null, callTimeout: null);
    }

    [TestMethod]
    public void Constructor_WithZeroLoadTimeout_ThrowsArgumentOutOfRangeException()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => new EmitWorkerPool(loadTimeout: TimeSpan.Zero));
    }

    [TestMethod]
    public void Constructor_WithNegativeCallTimeout_ThrowsArgumentOutOfRangeException()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => new EmitWorkerPool(callTimeout: TimeSpan.FromSeconds(-1)));
    }

    // ─── Finding #15: EmitAsync with CancellationToken ───────────────────────────

    [TestMethod]
    public void EmitAsync_AlreadyCancelledToken_ThrowsImmediately()
    {
        using var pool = new EmitWorkerPool(callTimeout: TimeSpan.FromSeconds(1));

        using var stream = new ReleasableStream();
        pool.WorkerFactory = () => EmitWorkerProcess.CreateForTesting(
            new StreamReader(stream),
            new StreamWriter(new MemoryStream()) { AutoFlush = true },
            TimeSpan.FromSeconds(1));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Task<ITinyInterface> task = pool.EmitAsync<ITinyInterface>("any.dll", CallingConvention.Winapi, cts.Token);

        Assert.IsTrue(task.IsCompleted, "EmitAsync must complete synchronously for a pre-cancelled token.");
        Assert.IsTrue(task.IsCanceled, "EmitAsync task must be cancelled.");

        stream.Release();
    }

    [TestMethod]
    public void DisposeAsync_CompletesWithoutThrowing()
    {
        // Verifies that DisposeAsync works for the 'await using' pattern.
        var pool = new EmitWorkerPool();
        ValueTask task = pool.DisposeAsync();
        task.AsTask().Wait(TimeSpan.FromSeconds(5));
    }

    // ─── Finding #6: faulted worker is replaced, not reused ─────────────────────

    /// <summary>
    /// A <see cref="BlockingStream"/> whose <see cref="Release"/> method signals EOF so the reader loop
    /// inside <see cref="EmitWorkerProcess"/> exits and sets <c>connectionFault</c>.
    /// Shared with <c>EmitWorkerProcessTests</c> — kept here as a local private class so the two test
    /// classes stay independent.
    /// </summary>
    private sealed class ReleasableStream : Stream
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
        public void Release() => gate.Set();
        protected override void Dispose(bool disposing) { if (disposing) gate.Set(); base.Dispose(disposing); }
    }

    [TestMethod]
    public void GetOrStartWorker_FaultedWorker_IsDisposedAndReplacedOnNextCall()
    {
        int factoryCallCount = 0;

        using var pool = new EmitWorkerPool(callTimeout: TimeSpan.FromSeconds(1));

        // First worker — will be faulted; second worker — returned as replacement.
        using var stream1 = new ReleasableStream();
        EmitWorkerProcess? worker1 = null;
        EmitWorkerProcess? worker2 = null;
        using var stream2 = new ReleasableStream(); // blocking (never released) to keep reader alive

        pool.WorkerFactory = () =>
        {
            factoryCallCount++;
            if (factoryCallCount == 1)
            {
                worker1 = EmitWorkerProcess.CreateForTesting(
                    new StreamReader(stream1),
                    new StreamWriter(new MemoryStream()) { AutoFlush = true },
                    TimeSpan.FromSeconds(1));
                return worker1;
            }
            worker2 = EmitWorkerProcess.CreateForTesting(
                new StreamReader(stream2),
                new StreamWriter(new MemoryStream()) { AutoFlush = true },
                TimeSpan.FromSeconds(1));
            return worker2;
        };

        // Trigger first factory call.
        pool.GetCurrentWorker(); // Caches worker1.

        Assert.IsNotNull(worker1, "Factory must have been called once.");
        Assert.IsTrue(worker1.IsHealthy, "worker1 must be healthy initially.");

        // Fault worker1 by signalling EOF on its stream.
        stream1.Release();
        var deadline = System.Diagnostics.Stopwatch.StartNew();
        while (worker1.IsHealthy && deadline.Elapsed < TimeSpan.FromSeconds(5))
            Thread.Sleep(5);
        Assert.IsFalse(worker1.IsHealthy, "worker1 must become unhealthy after its stream reaches EOF.");

        // Trigger second factory call: the pool must detect the fault, dispose worker1, and create worker2.
        EmitWorkerProcess returned = pool.GetCurrentWorker();

        Assert.AreEqual(2, factoryCallCount, "Factory must be called a second time to replace the faulted worker.");
        Assert.IsNotNull(worker2, "worker2 must have been created by the factory.");
        Assert.AreSame(worker2, returned, "Pool must return the new replacement worker.");
        Assert.IsTrue(worker2.IsHealthy, "Replacement worker must be healthy.");

        stream2.Release(); // Let worker2's reader loop exit cleanly.
    }
}
