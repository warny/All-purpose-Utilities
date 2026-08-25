using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Utils.IO;
using Utils.Collections;

namespace UtilsTest.Streams;

[TestClass]
public class PartialStreamTests
{
    private class NonSeekableStream : Stream
    {
        private readonly MemoryStream _inner = new MemoryStream();
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
    }

    // ---- existing tests (unchanged) ----

    [TestMethod]
    public void ConstructorThrowsWhenStreamNotSeekable()
    {
        var stream = new NonSeekableStream();
        Assert.ThrowsExactly<ArgumentException>(() => new PartialStream(stream, 10));
    }

    [TestMethod]
    public void ReadRespectsBoundsAndBasePositionUnchanged()
    {
        byte[] data = new byte[100];
        for (int i = 0; i < data.Length; i++) data[i] = (byte)i;
        using MemoryStream baseStream = new MemoryStream(data);
        PartialStream ps = new PartialStream(baseStream, 50, 10);

        byte[] buffer = new byte[10];
        int read = ps.Read(buffer, 0, buffer.Length);

        var comparer = EnumerableEqualityComparer<byte>.Default;
        Assert.AreEqual(10, read);
        Assert.AreEqual(data.AsSpan(50, 10).ToArray(), buffer, comparer);
        Assert.AreEqual(0, baseStream.Position);
    }

    [TestMethod]
    public void WriteUpdatesUnderlyingStream()
    {
        byte[] baseData = new byte[20];
        using MemoryStream baseStream = new MemoryStream(baseData);
        PartialStream ps = new PartialStream(baseStream, 5, 10);

        byte[] toWrite = new byte[10];
        for (int i = 0; i < toWrite.Length; i++) toWrite[i] = (byte)(i + 1);
        ps.Write(toWrite, 0, toWrite.Length);

        Assert.AreEqual(10, ps.Position);
        Assert.AreEqual(0, baseStream.Position);
        var expected = new byte[20];
        System.Array.Copy(toWrite, 0, expected, 5, 10);
        var comparer = EnumerableEqualityComparer<byte>.Default;
        Assert.AreEqual(expected, baseStream.ToArray(), comparer);
    }

    [TestMethod]
    public void WriteBeyondBoundsThrows()
    {
        using MemoryStream baseStream = new MemoryStream(new byte[10]);
        PartialStream ps = new PartialStream(baseStream, 0, 5);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ps.Write(new byte[6], 0, 6));
    }

    // ---- item 3: base position restored on failure paths ----

    [TestMethod]
    public void Read_BasePositionRestoredAfterArgumentException()
    {
        using var ms = new MemoryStream(new byte[20]);
        ms.Position = 7;
        var ps = new PartialStream(ms, 0, 10);
        // Pass a bad offset so ValidateBufferArguments throws before any seek
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ps.Read(new byte[4], -1, 2));
        Assert.AreEqual(7, ms.Position, "base position must be unchanged after failed Read");
    }

    [TestMethod]
    public void Write_BasePositionRestoredAfterBoundsViolation()
    {
        using var ms = new MemoryStream(new byte[20]);
        ms.Position = 5;
        var ps = new PartialStream(ms, 0, 3);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ps.Write(new byte[5], 0, 5));
        Assert.AreEqual(5, ms.Position, "base position must be unchanged after failed Write");
    }

    // ---- item 4: constructor validation ----

    [TestMethod]
    public void ConstructorThrowsWhenStreamIsNull()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new PartialStream(null!, 10));
        Assert.ThrowsExactly<ArgumentNullException>(() => new PartialStream(null!, 0, 10));
    }

    [TestMethod]
    public void ConstructorThrowsForNegativeLength()
    {
        using var ms = new MemoryStream(new byte[20]);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PartialStream(ms, -1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PartialStream(ms, 0, -1));
    }

    [TestMethod]
    public void ConstructorThrowsForNegativePosition()
    {
        using var ms = new MemoryStream(new byte[20]);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PartialStream(ms, -5, 10));
    }

    [TestMethod]
    public void ConstructorThrowsForPositionLengthOverflow()
    {
        // The two-argument constructor validates startOffset + length at construction time.
        using var ms = new MemoryStream(new byte[20]);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new PartialStream(ms, long.MaxValue, 1));
    }

    [TestMethod]
    public void Constructor1_ThrowsWhenCurrentPositionPlusLengthOverflows()
    {
        // The one-argument constructor reads baseStream.Position as startOffset and
        // must validate that startOffset + length fits in a long.
        var vs = new VirtualSeekableStream(long.MaxValue);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new PartialStream(vs, 1),
            "startOffset(=long.MaxValue) + length(=1) must be rejected");
    }

    [TestMethod]
    public void SetLength_ThrowsWhenRangeWouldOverflow()
    {
        // startOffset = 1, so setting partialLength = long.MaxValue would make
        // startOffset + partialLength overflow — must be rejected.
        var vs = new VirtualSeekableStream();
        var ps = new PartialStream(vs, 1, 5);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ps.SetLength(long.MaxValue),
            "startOffset(=1) + newLength(=long.MaxValue) must be rejected");
    }

    // ---- item 4: Position setter throws instead of clamping ----

    [TestMethod]
    public void PositionSetterThrowsForNegativeValue()
    {
        using var ms = new MemoryStream(new byte[20]);
        var ps = new PartialStream(ms, 0, 10);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ps.Position = -1);
    }

    [TestMethod]
    public void PositionSetterThrowsForValueBeyondLength()
    {
        using var ms = new MemoryStream(new byte[20]);
        var ps = new PartialStream(ms, 0, 10);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ps.Position = 11);
    }

    // ---- item 4: Seek throws instead of clamping ----

    [TestMethod]
    public void SeekThrowsBeforeBeginning()
    {
        using var ms = new MemoryStream(new byte[20]);
        var ps = new PartialStream(ms, 0, 10);
        Assert.ThrowsExactly<IOException>(() => ps.Seek(-1, SeekOrigin.Begin));
    }

    [TestMethod]
    public void SeekThrowsPastEnd()
    {
        using var ms = new MemoryStream(new byte[20]);
        var ps = new PartialStream(ms, 0, 10);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ps.Seek(11, SeekOrigin.Begin));
    }

    // ---- item 4: SetLength rejects negative ----

    [TestMethod]
    public void SetLengthThrowsForNegativeValue()
    {
        using var ms = new MemoryStream(new byte[20]);
        var ps = new PartialStream(ms, 0, 10);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ps.SetLength(-1));
    }

    // ---- arithmetic overflow near long.MaxValue ----

    // A seekable stream backed by no real storage; Position and Length are purely virtual.
    // Used to test boundary arithmetic without allocating gigabytes of memory.
    private class VirtualSeekableStream : Stream
    {
        private long _position;
        public VirtualSeekableStream(long position = 0) => _position = position;
        public override bool CanRead  => true;
        public override bool CanSeek  => true;
        public override bool CanWrite => true;
        public override long Length   => long.MaxValue;
        public override long Position { get => _position; set => _position = value; }
        public override void Flush() { }
        public override int  Read(byte[] buffer, int offset, int count) => count;
        public override long Seek(long offset, SeekOrigin origin) { _position = offset; return _position; }
        public override void SetLength(long value) { }
        public override void Write(byte[] buffer, int offset, int count) { }
    }

    [TestMethod]
    public void Write_BoundsCheckDoesNotOverflow_NearMaxValue()
    {
        // partialLength = long.MaxValue - 2; position set to the very end
        // With the old check (partialPosition + count > partialLength), the left-hand side
        // overflows to a large negative value when count is positive, bypassing the guard.
        // The new check (count > partialLength - partialPosition) is immune to this overflow.
        var vs = new VirtualSeekableStream();
        long bigLength = long.MaxValue - 2;
        var ps = new PartialStream(vs, 0, bigLength);
        ps.Position = bigLength; // at the very end of the segment

        // count=5 exceeds the zero remaining bytes; must throw even though old arithmetic overflowed
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ps.Write(new byte[5], 0, 5),
            "Write past the end must be rejected even when partialPosition + count overflows");
    }

    [TestMethod]
    public void Seek_Current_OverflowThrows()
    {
        var vs = new VirtualSeekableStream();
        var ps = new PartialStream(vs, 0, long.MaxValue - 1);
        ps.Position = long.MaxValue / 2;
        // offset so large that partialPosition + offset overflows long
        Assert.ThrowsExactly<OverflowException>(
            () => ps.Seek(long.MaxValue, SeekOrigin.Current),
            "Arithmetic overflow in SeekOrigin.Current must propagate as OverflowException");
    }

    [TestMethod]
    public void Seek_End_OverflowThrows()
    {
        var vs = new VirtualSeekableStream();
        var ps = new PartialStream(vs, 0, long.MaxValue - 1);
        // offset so large that partialLength + offset overflows long
        Assert.ThrowsExactly<OverflowException>(
            () => ps.Seek(long.MaxValue, SeekOrigin.End),
            "Arithmetic overflow in SeekOrigin.End must propagate as OverflowException");
    }

    // ---- IO-09: Flush/FlushAsync participate in the shared per-base-stream operation gate ----

    /// <summary>
    /// A seekable <see cref="MemoryStream"/> that instruments <see cref="Write(byte[], int, int)"/>,
    /// <see cref="Flush"/> and <see cref="FlushAsync(CancellationToken)"/> so tests can deterministically
    /// prove ordering between concurrent <see cref="PartialStream"/> operations sharing this base stream,
    /// instead of relying on wall-clock timing.
    /// </summary>
    private sealed class GateProbeStream : MemoryStream
    {
        private readonly object logLock = new();
        private readonly System.Collections.Generic.List<string> events = new();

        /// <summary>Set by an overridden <see cref="Write(byte[], int, int)"/> as soon as it is entered, before it blocks.</summary>
        public ManualResetEventSlim WriteEntered { get; } = new(false);

        /// <summary>Blocks an in-progress <see cref="Write(byte[], int, int)"/> until the test releases it.</summary>
        public ManualResetEventSlim AllowWriteToComplete { get; } = new(false);

        /// <summary>When <see langword="true"/>, <see cref="Flush"/> and <see cref="FlushAsync(CancellationToken)"/> throw instead of flushing.</summary>
        public bool ThrowOnFlush { get; set; }

        /// <summary>The <see cref="Stream.Position"/> observed by the most recent <see cref="Flush"/> call.</summary>
        public long? FlushObservedPosition { get; private set; }

        /// <summary>The <see cref="Stream.Position"/> observed by the most recent <see cref="FlushAsync(CancellationToken)"/> call.</summary>
        public long? FlushAsyncObservedPosition { get; private set; }

        /// <summary>Creates the instrumented base stream backed by <paramref name="buffer"/>, exactly like a regular writable <see cref="MemoryStream"/>.</summary>
        public GateProbeStream(byte[] buffer) : base(buffer) { }

        /// <summary>Returns a thread-safe snapshot of the entry/exit events recorded so far, in order.</summary>
        public string[] EventsSnapshot { get { lock (logLock) return events.ToArray(); } }

        /// <summary>Appends an entry/exit event name to the shared log under the log lock, so ordering can be asserted deterministically.</summary>
        private void Log(string name) { lock (logLock) events.Add(name); }

        /// <summary>
        /// Records entry, then blocks on <see cref="AllowWriteToComplete"/> before delegating to the base
        /// <see cref="MemoryStream"/> write, so a test can hold the caller's <see cref="PartialStream"/>
        /// operation gate open for a controlled duration and observe ordering against a concurrent flush.
        /// </summary>
        public override void Write(byte[] buffer, int offset, int count)
        {
            Log("WriteEntered");
            WriteEntered.Set();
            AllowWriteToComplete.Wait();
            base.Write(buffer, offset, count);
            Log("WriteExited");
        }

        /// <summary>
        /// Records the observed <see cref="Stream.Position"/> and an entry event, then either throws a
        /// simulated failure (when <see cref="ThrowOnFlush"/> is set) or delegates to the base
        /// <see cref="MemoryStream"/> flush.
        /// </summary>
        public override void Flush()
        {
            FlushObservedPosition = Position;
            Log("FlushEntered");
            if (ThrowOnFlush) throw new IOException("Simulated flush failure.");
            base.Flush();
        }

        /// <summary>
        /// Records the observed <see cref="Stream.Position"/> and an entry event, then either throws a
        /// simulated failure (when <see cref="ThrowOnFlush"/> is set) or performs the flush synchronously
        /// via the base <see cref="MemoryStream"/>. Calls <see cref="MemoryStream.Flush"/> non-virtually
        /// rather than <c>base.FlushAsync</c> so this override is not spuriously re-entered (see inline
        /// comment below).
        /// </summary>
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            FlushAsyncObservedPosition = Position;
            Log("FlushAsyncEntered");
            if (ThrowOnFlush) throw new IOException("Simulated flush failure.");
            // Call the non-virtual base implementation directly: the default Stream.FlushAsync
            // (inherited by MemoryStream) invokes the virtual Flush(), which would otherwise
            // re-enter this class's own Flush() override and log a spurious extra event.
            base.Flush();
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// The core IO-09 regression: while one <see cref="PartialStream"/> view holds the shared gate inside
    /// a blocked <see cref="PartialStream.Write(byte[], int, int)"/>, another view's <see cref="PartialStream.Flush"/>
    /// over the same base stream must wait, and must observe the base stream's restored position rather
    /// than the write's temporary repositioning.
    /// </summary>
    [TestMethod]
    public void Flush_WaitsForConcurrentWriteOnAnotherSliceOverSameBaseStream()
    {
        using GateProbeStream baseStream = new(new byte[100]);
        baseStream.Position = 3;

        PartialStream sliceA = new(baseStream, position: 20, length: 10);
        PartialStream sliceB = new(baseStream, position: 0, length: 100);

        Task writeTask = Task.Run(() => sliceA.Write([1, 2, 3], 0, 3));
        Assert.IsTrue(baseStream.WriteEntered.Wait(TimeSpan.FromSeconds(5)), "the write must reach the base stream before the test proceeds.");

        Task flushTask = Task.Run(() => sliceB.Flush());
        Assert.IsFalse(baseStream.EventsSnapshot.Contains("FlushEntered"), "Flush must not enter while the write holds the shared gate.");

        baseStream.AllowWriteToComplete.Set();
        Assert.IsTrue(writeTask.Wait(TimeSpan.FromSeconds(5)), "the write must complete once released.");
        Assert.IsTrue(flushTask.Wait(TimeSpan.FromSeconds(5)), "the flush must complete once the gate is released.");

        CollectionAssert.AreEqual(new[] { "WriteEntered", "WriteExited", "FlushEntered" }, baseStream.EventsSnapshot);
        Assert.AreEqual(3, baseStream.FlushObservedPosition, "Flush must observe the restored base position, not sliceA's temporary offset.");
        Assert.AreEqual(3, baseStream.Position, "the base position must be restored once both operations complete.");
    }

    /// <summary>
    /// The async counterpart of the core IO-09 regression: <see cref="PartialStream.FlushAsync(CancellationToken)"/>
    /// must wait behind a concurrent, blocked <see cref="PartialStream.Write(byte[], int, int)"/> on another view
    /// over the same base stream, proving sync/async interoperability through the same <see cref="SemaphoreSlim"/>.
    /// </summary>
    [TestMethod]
    public async Task FlushAsync_WaitsForConcurrentWriteOnAnotherSliceOverSameBaseStream()
    {
        using GateProbeStream baseStream = new(new byte[100]);
        baseStream.Position = 3;

        PartialStream sliceA = new(baseStream, position: 20, length: 10);
        PartialStream sliceB = new(baseStream, position: 0, length: 100);

        Task writeTask = Task.Run(() => sliceA.Write([1, 2, 3], 0, 3));
        Assert.IsTrue(baseStream.WriteEntered.Wait(TimeSpan.FromSeconds(5)), "the write must reach the base stream before the test proceeds.");

        Task flushAsyncTask = sliceB.FlushAsync(CancellationToken.None);
        Assert.IsFalse(baseStream.EventsSnapshot.Contains("FlushAsyncEntered"), "FlushAsync must not enter while the write holds the shared gate.");

        baseStream.AllowWriteToComplete.Set();
        Assert.IsTrue(writeTask.Wait(TimeSpan.FromSeconds(5)), "the write must complete once released.");
        await flushAsyncTask.WaitAsync(TimeSpan.FromSeconds(5));

        CollectionAssert.AreEqual(new[] { "WriteEntered", "WriteExited", "FlushAsyncEntered" }, baseStream.EventsSnapshot);
        Assert.AreEqual(3, baseStream.FlushAsyncObservedPosition, "FlushAsync must observe the restored base position, not sliceA's temporary offset.");
    }

    /// <summary>
    /// Cancellation while <see cref="PartialStream.FlushAsync(CancellationToken)"/> is waiting for the shared gate
    /// must never invoke the underlying <see cref="Stream.FlushAsync(CancellationToken)"/>, and must not leave the
    /// gate over-released or otherwise unusable for the next operation.
    /// </summary>
    [TestMethod]
    public async Task FlushAsync_CancelledWhileWaitingForGate_NeverInvokesBaseFlushAsync_AndLeavesGateUsable()
    {
        using GateProbeStream baseStream = new(new byte[20]);

        PartialStream sliceA = new(baseStream, position: 0, length: 10);
        PartialStream sliceB = new(baseStream, position: 0, length: 10);

        Task writeTask = Task.Run(() => sliceA.Write([1], 0, 1));
        Assert.IsTrue(baseStream.WriteEntered.Wait(TimeSpan.FromSeconds(5)), "the write must reach the base stream before the test proceeds.");

        using CancellationTokenSource cts = new();
        Task flushAsyncTask = sliceB.FlushAsync(cts.Token);
        cts.Cancel();

        // Observe the cancellation while the write is still holding the gate, before releasing it.
        // This avoids racing the cancellation against the write's eventual Release().
        OperationCanceledException? observed = null;
        try { await flushAsyncTask; }
        catch (OperationCanceledException ex) { observed = ex; }
        Assert.IsNotNull(observed, "cancellation while waiting for the gate must surface as OperationCanceledException.");
        Assert.IsFalse(baseStream.EventsSnapshot.Contains("FlushAsyncEntered"), "a cancelled wait must never invoke the underlying FlushAsync.");

        baseStream.AllowWriteToComplete.Set();
        Assert.IsTrue(writeTask.Wait(TimeSpan.FromSeconds(5)), "the write must complete once released.");

        // The gate must be healthy: a subsequent operation must proceed without deadlocking or throwing.
        await sliceB.FlushAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(baseStream.EventsSnapshot.Contains("FlushAsyncEntered"));
    }

    /// <summary>A throwing <see cref="Stream.Flush"/> must still release the shared gate for the next operation.</summary>
    [TestMethod]
    public void Flush_WhenUnderlyingFlushThrows_StillReleasesGateForSubsequentOperations()
    {
        using GateProbeStream baseStream = new(new byte[20]) { ThrowOnFlush = true };
        PartialStream sliceA = new(baseStream, position: 0, length: 10);
        PartialStream sliceB = new(baseStream, position: 0, length: 10);

        Assert.ThrowsExactly<IOException>(() => sliceA.Flush());

        baseStream.ThrowOnFlush = false;
        sliceB.Flush();
        Assert.AreEqual(2, baseStream.EventsSnapshot.Count(e => e == "FlushEntered"));
    }

    /// <summary>A throwing <see cref="Stream.FlushAsync(CancellationToken)"/> must still release the shared gate for the next operation.</summary>
    [TestMethod]
    public async Task FlushAsync_WhenUnderlyingFlushAsyncThrows_StillReleasesGateForSubsequentOperations()
    {
        using GateProbeStream baseStream = new(new byte[20]) { ThrowOnFlush = true };
        PartialStream sliceA = new(baseStream, position: 0, length: 10);
        PartialStream sliceB = new(baseStream, position: 0, length: 10);

        await Assert.ThrowsExactlyAsync<IOException>(() => sliceA.FlushAsync(CancellationToken.None));

        baseStream.ThrowOnFlush = false;
        await sliceB.FlushAsync(CancellationToken.None);
        Assert.AreEqual(2, baseStream.EventsSnapshot.Count(e => e == "FlushAsyncEntered"));
    }

    /// <summary>The shared gate is keyed per base <see cref="Stream"/> identity; unrelated base streams must never block each other.</summary>
    [TestMethod]
    public void Flush_OnDifferentBaseStream_DoesNotWaitForUnrelatedBaseStreamOperation()
    {
        using GateProbeStream baseA = new(new byte[20]);
        using MemoryStream baseB = new(new byte[20]);

        PartialStream a = new(baseA, position: 0, length: 10);
        PartialStream b = new(baseB, position: 0, length: 10);

        Task writeTask = Task.Run(() => a.Write([1], 0, 1));
        Assert.IsTrue(baseA.WriteEntered.Wait(TimeSpan.FromSeconds(5)), "the write must reach base A before the test proceeds.");

        Task flushOnBTask = Task.Run(() => b.Flush());
        Assert.IsTrue(flushOnBTask.Wait(TimeSpan.FromSeconds(5)), "flushing a view over an unrelated base stream must not be blocked by base A's held gate.");

        baseA.AllowWriteToComplete.Set();
        Assert.IsTrue(writeTask.Wait(TimeSpan.FromSeconds(5)));
    }

    /// <summary>Outside a concurrent operation, <see cref="PartialStream.Flush"/> must not reposition anything.</summary>
    [TestMethod]
    public void Flush_DoesNotChangePartialPositionLengthOrBasePosition()
    {
        using MemoryStream baseStream = new(new byte[20]);
        baseStream.Position = 6;
        PartialStream ps = new(baseStream, position: 2, length: 10);
        ps.Position = 4;

        ps.Flush();

        Assert.AreEqual(4, ps.Position);
        Assert.AreEqual(10, ps.Length);
        Assert.AreEqual(6, baseStream.Position);
    }

    /// <summary>Outside a concurrent operation, <see cref="PartialStream.FlushAsync(CancellationToken)"/> must not reposition anything.</summary>
    [TestMethod]
    public async Task FlushAsync_DoesNotChangePartialPositionLengthOrBasePosition()
    {
        using MemoryStream baseStream = new(new byte[20]);
        baseStream.Position = 6;
        PartialStream ps = new(baseStream, position: 2, length: 10);
        ps.Position = 4;

        await ps.FlushAsync(CancellationToken.None);

        Assert.AreEqual(4, ps.Position);
        Assert.AreEqual(10, ps.Length);
        Assert.AreEqual(6, baseStream.Position);
    }
}
