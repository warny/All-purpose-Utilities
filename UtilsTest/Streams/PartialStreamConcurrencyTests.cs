using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Utils.IO;

namespace UtilsTest.Streams;

/// <summary>Verifies deterministic synchronization of mutable partial-stream state.</summary>
[TestClass]
public sealed class PartialStreamConcurrencyTests
{
    /// <summary>Ensures two writes that individually fit cannot jointly exceed the slice.</summary>
    [TestMethod]
    public async Task ConcurrentWrites_ValidateBoundsInsideGate()
    {
        using var partial = new PartialStream(new MemoryStream(new byte[16]), 0, 10);
        using var barrier = new Barrier(2);
        Task<Exception?>[] writes = Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
        {
            barrier.SignalAndWait();
            try { partial.Write(new byte[6]); return null; }
            catch (Exception error) { return error; }
        })).ToArray();

        Exception?[] results = await Task.WhenAll(writes);
        Assert.AreEqual(1, results.Count(error => error is null));
        Assert.AreEqual(1, results.Count(error => error is ArgumentOutOfRangeException));
        Assert.AreEqual(6, partial.Position);
    }

    /// <summary>Ensures cancellation while waiting for the async operation gate is observed.</summary>
    [TestMethod]
    public async Task WriteAsync_CancellationBeforeGateAcquisitionIsObserved()
    {
        using var stream = new BlockingWriteStream(16);
        await using var partial = new PartialStream(stream, 0, 16);
        Task first = partial.WriteAsync(new byte[4]).AsTask();
        await stream.WriteEntered.Task;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try
        {
            await partial.WriteAsync(new byte[1], cancellation.Token);
            Assert.Fail("The canceled gate acquisition unexpectedly succeeded.");
        }
        catch (OperationCanceledException)
        {
            // TaskCanceledException is an allowed cancellation representation.
        }
        stream.AllowWrite.TrySetResult();
        await first;
        Assert.AreEqual(4, partial.Position);
    }

    /// <summary>Ensures Position waits for an active write and observes its committed state.</summary>
    [TestMethod]
    public async Task Position_DuringWriteIsSerialized()
    {
        using var stream = new BlockingWriteStream(16);
        await using var partial = new PartialStream(stream, 0, 16);
        Task write = partial.WriteAsync(new byte[5]).AsTask();
        await stream.WriteEntered.Task;
        var positionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<long> position = Task.Run(() => { positionStarted.SetResult(); return partial.Position; });
        await positionStarted.Task;
        Assert.IsFalse(position.IsCompleted);
        stream.AllowWrite.TrySetResult();
        await write;
        Assert.AreEqual(5, await position);
    }

    /// <summary>Ensures seek and length changes wait for an active operation before mutating state.</summary>
    [TestMethod]
    public async Task SeekAndSetLength_DuringWriteAreSerialized()
    {
        using var stream = new BlockingWriteStream(16);
        await using var partial = new PartialStream(stream, 0, 16);
        Task write = partial.WriteAsync(new byte[5]).AsTask();
        await stream.WriteEntered.Task;
        var mutationsStarted = new CountdownEvent(2);
        Task<long> seek = Task.Run(() => { mutationsStarted.Signal(); return partial.Seek(1, SeekOrigin.Begin); });
        Task resize = Task.Run(() => { mutationsStarted.Signal(); partial.SetLength(8); });
        mutationsStarted.Wait();
        Assert.IsFalse(seek.IsCompleted);
        Assert.IsFalse(resize.IsCompleted);
        stream.AllowWrite.TrySetResult();
        await write;
        await Task.WhenAll(seek, resize);
        Assert.IsTrue(partial.Position <= partial.Length);
    }

    /// <summary>Ensures cancellation inside the underlying async write does not advance logical position.</summary>
    [TestMethod]
    public async Task CancellationDuringUnderlyingWrite_DoesNotAdvancePosition()
    {
        using var stream = new BlockingWriteStream(16);
        await using var partial = new PartialStream(stream, 0, 16);
        using var cancellation = new CancellationTokenSource();
        Task write = partial.WriteAsync(new byte[5], cancellation.Token).AsTask();
        await stream.WriteEntered.Task;
        cancellation.Cancel();
        try { await write; Assert.Fail("The underlying canceled write unexpectedly succeeded."); }
        catch (OperationCanceledException) { }
        Assert.AreEqual(0, partial.Position);
        Assert.AreEqual(0, stream.Position);
    }

    /// <summary>Instrumented seekable stream whose async write completion is controlled by the test.</summary>
    private sealed class BlockingWriteStream : MemoryStream
    {
        /// <summary>Initializes a fixed-size writable stream.</summary>
        internal BlockingWriteStream(int length) : base(new byte[length]) { }

        /// <summary>Gets the signal published after the underlying write has started.</summary>
        internal TaskCompletionSource WriteEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets the signal used to release the underlying write.</summary>
        internal TaskCompletionSource AllowWrite { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Waits for the deterministic release signal before writing to memory.</summary>
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            WriteEntered.TrySetResult();
            await AllowWrite.Task.WaitAsync(cancellationToken);
            await base.WriteAsync(buffer, cancellationToken);
        }
    }
}
