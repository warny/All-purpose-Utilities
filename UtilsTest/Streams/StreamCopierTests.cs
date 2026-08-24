using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Utils.Collections;
using Utils.IO;
using Utils.Randomization;

namespace UtilsTest.Streams;

[TestClass]
public class StreamCopierTests
{
    /// <summary>
    /// Stream copier test
    /// </summary>
    [TestMethod]
    public void StreamTest1()
    {
        using MemoryStream target1 = new MemoryStream();
        using MemoryStream target2 = new MemoryStream();
        var r = new Random();
        byte[] reference = r.NextBytes(10, 20);

        StreamCopier copier = new StreamCopier(target1, target2);

        copier.Write(reference, 0, reference.Length);
        copier.Flush();

        Assert.AreEqual(reference.Length, target1.Length);
        Assert.AreEqual(reference.Length, target2.Length);
        Assert.AreEqual(reference.Length, target1.Position);
        Assert.AreEqual(reference.Length, target2.Position);

        byte[] test1 = target1.ToArray();
        byte[] test2 = target2.ToArray();

        var comparer = EnumerableEqualityComparer<byte>.Default;

        Assert.AreEqual(reference, test1, comparer);
        Assert.AreEqual(reference, test2, comparer);
    }

    // ---- item 11: agregation des erreurs, toutes les cibles sont tentees ----

    private class ThrowingStream : MemoryStream
    {
        public bool ShouldThrow { get; set; }
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (ShouldThrow) throw new IOException("Simulated write failure");
            base.Write(buffer, offset, count);
        }
    }

    [TestMethod]
    public void Write_AttemptsAllTargetsEvenWhenOneFails()
    {
        var good = new MemoryStream();
        var bad = new ThrowingStream { ShouldThrow = true };
        var copier = new StreamCopier(bad, good);

        var ex = Assert.ThrowsException<AggregateException>(() => copier.Write(new byte[] { 1, 2, 3 }, 0, 3));
        Assert.AreEqual(1, ex.InnerExceptions.Count);
        Assert.AreEqual(3, good.Length, "good target must have received the data");
    }

    [TestMethod]
    public void Flush_AttemptsAllTargetsEvenWhenOneFails()
    {
        var good1 = new MemoryStream();
        var bad = new ThrowingStream { ShouldThrow = false }; // will throw on Flush
        var good2 = new MemoryStream();

        var copier = new StreamCopier(good1, bad, good2);
        bad.ShouldThrow = false;
        copier.Write(new byte[] { 42 }, 0, 1);

        // Simulate flush failure
        bad.ShouldThrow = true;
        // Create a stream that fails only on flush
        var failFlushStream = new FailFlushStream();
        var copier2 = new StreamCopier(good1, failFlushStream, good2);

        var ex = Assert.ThrowsException<AggregateException>(() => copier2.Flush());
        Assert.AreEqual(1, ex.InnerExceptions.Count, "exactly the failing stream should contribute an exception");
    }

    private class FailFlushStream : MemoryStream
    {
        public override void Flush() => throw new IOException("Simulated flush failure");
    }

    [TestMethod]
    public void Add_RejectsNullStream()
    {
        var copier = new StreamCopier();
        Assert.ThrowsException<ArgumentNullException>(() => copier.Add(null!));
    }

    [TestMethod]
    public void Insert_RejectsNullStream()
    {
        var copier = new StreamCopier();
        Assert.ThrowsException<ArgumentNullException>(() => copier.Insert(0, null!));
    }

    // ---- item 30: span, async, cancellation, disposal ----

    [TestMethod]
    public void WriteSpan_BroadcastsToAllTargets()
    {
        using var t1 = new MemoryStream();
        using var t2 = new MemoryStream();
        var copier = new StreamCopier(t1, t2);
        ReadOnlySpan<byte> data = new byte[] { 1, 2, 3, 4 };
        copier.Write(data);
        var cmp = EnumerableEqualityComparer<byte>.Default;
        Assert.IsTrue(cmp.Equals(new byte[] { 1, 2, 3, 4 }, t1.ToArray()));
        Assert.IsTrue(cmp.Equals(new byte[] { 1, 2, 3, 4 }, t2.ToArray()));
    }

    [TestMethod]
    public async Task WriteAsync_BroadcastsToAllTargets()
    {
        using var t1 = new MemoryStream();
        using var t2 = new MemoryStream();
        var copier = new StreamCopier(t1, t2);
        await copier.WriteAsync(new byte[] { 5, 6, 7 }, 0, 3, CancellationToken.None);
        Assert.AreEqual(3, t1.Length);
        Assert.AreEqual(3, t2.Length);
    }

    [TestMethod]
    public async Task FlushAsync_AttemptsAllTargets()
    {
        using var t1 = new MemoryStream();
        using var t2 = new MemoryStream();
        var copier = new StreamCopier(t1, t2);
        await copier.WriteAsync(new byte[] { 1 }, 0, 1, CancellationToken.None);
        await copier.FlushAsync(CancellationToken.None); // must not throw
    }

    [TestMethod]
    public async Task WriteAsync_CancelledBeforeCall_ThrowsOperationCanceled()
    {
        using var t1 = new MemoryStream();
        var copier = new StreamCopier(t1);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(
            () => copier.WriteAsync(new byte[] { 1 }, 0, 1, cts.Token));
        Assert.AreEqual(0, t1.Length, "No data must be written when cancelled before the loop.");
    }

    [TestMethod]
    public async Task WriteAsync_OneTargetFails_AggregatesErrors()
    {
        var good = new MemoryStream();
        var bad = new ThrowingStream { ShouldThrow = true };
        var copier = new StreamCopier(bad, good);
        var ex = await Assert.ThrowsExceptionAsync<AggregateException>(
            () => copier.WriteAsync(new byte[] { 1, 2, 3 }, 0, 3, CancellationToken.None));
        Assert.AreEqual(1, ex.InnerExceptions.Count);
        Assert.AreEqual(3, good.Length, "good target must still have received the data");
    }

    [TestMethod]
    public void DisposeAsync_ClosesTargets_WhenConfigured()
    {
        var t1 = new MemoryStream();
        var t2 = new MemoryStream();
        var copier = new StreamCopier(closeAllTargetsOnDispose: true, t1, t2);
        copier.DisposeAsync().AsTask().Wait();
        Assert.ThrowsException<ObjectDisposedException>(() => t1.WriteByte(1));
        Assert.ThrowsException<ObjectDisposedException>(() => t2.WriteByte(1));
    }

    [TestMethod]
    public void DisposeAsync_LeavesTargetsOpen_WhenNotConfigured()
    {
        var t1 = new MemoryStream();
        var copier = new StreamCopier(closeAllTargetsOnDispose: false, t1);
        copier.DisposeAsync().AsTask().Wait();
        // Target must remain usable.
        t1.WriteByte(1);
        Assert.AreEqual(1, t1.Length);
    }

    [TestMethod]
    public void Dispose_IsIdempotent()
    {
        var t1 = new MemoryStream();
        var copier = new StreamCopier(closeAllTargetsOnDispose: true, t1);
        copier.Dispose();
        // Second dispose must not throw or re-run target disposal.
        copier.Dispose();
    }

    // ---- item 30: operations after dispose/disposeAsync throw ObjectDisposedException ----

    [TestMethod]
    public void Write_AfterDispose_ThrowsObjectDisposedException()
    {
        var copier = new StreamCopier(new MemoryStream());
        copier.Dispose();
        Assert.ThrowsException<ObjectDisposedException>(() => copier.Write(new byte[] { 1 }, 0, 1));
    }

    [TestMethod]
    public void Flush_AfterDispose_ThrowsObjectDisposedException()
    {
        var copier = new StreamCopier(new MemoryStream());
        copier.Dispose();
        Assert.ThrowsException<ObjectDisposedException>(() => copier.Flush());
    }

    [TestMethod]
    public async Task WriteAsync_AfterDisposeAsync_ThrowsObjectDisposedException()
    {
        var copier = new StreamCopier(new MemoryStream());
        await copier.DisposeAsync();
        await Assert.ThrowsExceptionAsync<ObjectDisposedException>(
            () => copier.WriteAsync(new byte[] { 1 }, 0, 1, CancellationToken.None));
    }

    // ---- IO-06/IO-07/IO-08: target validation, identity and post-dispose lifecycle ----

    /// <summary>A stream whose <see cref="Equals(object?)"/> always reports equality with any other instance of this type, to prove identity (not Equals) drives duplicate detection.</summary>
    private sealed class EqualsAlwaysTrueStream : MemoryStream
    {
        public override bool Equals(object? obj) => obj is EqualsAlwaysTrueStream;
        public override int GetHashCode() => 0;
    }

    /// <summary>A stream that records sync/async disposal attempts, optionally throwing.</summary>
    private sealed class TrackingDisposeStream : MemoryStream
    {
        public int DisposeCount { get; private set; }
        public int DisposeAsyncCount { get; private set; }
        public bool ShouldThrowOnDispose { get; set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing) DisposeCount++;
            base.Dispose(disposing);
            if (disposing && ShouldThrowOnDispose)
                throw new IOException("Simulated dispose failure");
        }

        public override async ValueTask DisposeAsync()
        {
            DisposeAsyncCount++;
            await base.DisposeAsync().ConfigureAwait(false);
            if (ShouldThrowOnDispose)
                throw new IOException("Simulated dispose failure");
        }
    }

    // -- constructor validation --

    /// <summary>Verifies the array-accepting constructor rejects a null array.</summary>
    [TestMethod]
    public void Constructor_RejectsNullArray()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new StreamCopier((Stream[])null!));
    }

    /// <summary>Verifies the constructor rejects a null element within an otherwise valid array.</summary>
    [TestMethod]
    public void Constructor_RejectsNullElement()
    {
        using MemoryStream s1 = new();
        Assert.ThrowsException<ArgumentNullException>(() => new StreamCopier(s1, null!));
    }

    /// <summary>Verifies the constructor rejects a non-writable target before any write is attempted.</summary>
    [TestMethod]
    public void Constructor_RejectsNonWritableTarget()
    {
        byte[] bytes = [1, 2, 3];
        using MemoryStream readOnly = new(bytes, writable: false);
        Assert.ThrowsException<ArgumentException>(() => new StreamCopier(readOnly));
    }

    /// <summary>Verifies the constructor rejects the same stream instance appearing twice in the array.</summary>
    [TestMethod]
    public void Constructor_RejectsDuplicateReference()
    {
        using MemoryStream s = new();
        Assert.ThrowsException<ArgumentException>(() => new StreamCopier(s, s));
    }

    /// <summary>Verifies two distinct writable targets still construct successfully.</summary>
    [TestMethod]
    public void Constructor_AllowsTwoDistinctWritableTargets()
    {
        using MemoryStream s1 = new();
        using MemoryStream s2 = new();
        using StreamCopier copier = new(s1, s2);
        Assert.AreEqual(2, copier.Count);
    }

    // -- Add / Insert --

    /// <summary>Verifies <see cref="StreamCopier.Add"/> rejects a non-writable target.</summary>
    [TestMethod]
    public void Add_RejectsNonWritableTarget()
    {
        byte[] bytes = [1, 2, 3];
        using MemoryStream readOnly = new(bytes, writable: false);
        using StreamCopier copier = new();
        Assert.ThrowsException<ArgumentException>(() => copier.Add(readOnly));
    }

    /// <summary>Verifies <see cref="StreamCopier.Add"/> rejects a target that is already registered.</summary>
    [TestMethod]
    public void Add_RejectsDuplicateReference()
    {
        using MemoryStream s = new();
        using StreamCopier copier = new();
        copier.Add(s);
        Assert.ThrowsException<ArgumentException>(() => copier.Add(s));
    }

    /// <summary>Verifies <see cref="StreamCopier.Add"/> rejects the copier registering itself.</summary>
    [TestMethod]
    public void Add_RejectsSelf()
    {
        using StreamCopier copier = new();
        Assert.ThrowsException<ArgumentException>(() => copier.Add(copier));
    }

    /// <summary>Verifies <see cref="StreamCopier.Insert"/> rejects a non-writable target.</summary>
    [TestMethod]
    public void Insert_RejectsNonWritableTarget()
    {
        byte[] bytes = [1, 2, 3];
        using MemoryStream readOnly = new(bytes, writable: false);
        using StreamCopier copier = new();
        Assert.ThrowsException<ArgumentException>(() => copier.Insert(0, readOnly));
    }

    /// <summary>Verifies <see cref="StreamCopier.Insert"/> rejects a target that is already registered.</summary>
    [TestMethod]
    public void Insert_RejectsDuplicateReference()
    {
        using MemoryStream s = new();
        using StreamCopier copier = new();
        copier.Add(s);
        Assert.ThrowsException<ArgumentException>(() => copier.Insert(0, s));
    }

    /// <summary>Verifies <see cref="StreamCopier.Insert"/> rejects the copier registering itself.</summary>
    [TestMethod]
    public void Insert_RejectsSelf()
    {
        using StreamCopier copier = new();
        Assert.ThrowsException<ArgumentException>(() => copier.Insert(0, copier));
    }

    // -- indexer setter --

    /// <summary>Verifies the indexer setter rejects a null replacement.</summary>
    [TestMethod]
    public void IndexerSetter_RejectsNull()
    {
        using MemoryStream s1 = new();
        using StreamCopier copier = new(s1);
        Assert.ThrowsException<ArgumentNullException>(() => copier[0] = null!);
    }

    /// <summary>Verifies the indexer setter rejects a non-writable replacement and leaves the original target in place.</summary>
    [TestMethod]
    public void IndexerSetter_RejectsNonWritableTarget()
    {
        byte[] bytes = [1, 2, 3];
        using MemoryStream s1 = new();
        using MemoryStream readOnly = new(bytes, writable: false);
        using StreamCopier copier = new(s1);
        Assert.ThrowsException<ArgumentException>(() => copier[0] = readOnly);
        Assert.AreSame(s1, copier[0], "A failed replacement must leave the original target unchanged.");
    }

    /// <summary>Verifies the indexer setter rejects a replacement already registered at another index, leaving the original target in place.</summary>
    [TestMethod]
    public void IndexerSetter_RejectsDuplicateAtOtherIndex()
    {
        using MemoryStream s1 = new();
        using MemoryStream s2 = new();
        using StreamCopier copier = new(s1, s2);
        Assert.ThrowsException<ArgumentException>(() => copier[0] = s2);
        Assert.AreSame(s1, copier[0], "A failed replacement must leave the original target unchanged.");
    }

    /// <summary>Verifies the indexer setter rejects the copier replacing a slot with itself.</summary>
    [TestMethod]
    public void IndexerSetter_RejectsSelf()
    {
        using MemoryStream s1 = new();
        using StreamCopier copier = new(s1);
        Assert.ThrowsException<ArgumentException>(() => copier[0] = copier);
    }

    /// <summary>Verifies replacing a slot with the exact reference already occupying it is allowed, not treated as a duplicate.</summary>
    [TestMethod]
    public void IndexerSetter_AllowsSameReferenceAtSameIndex()
    {
        using MemoryStream s1 = new();
        using StreamCopier copier = new(s1);
        copier[0] = s1;
        Assert.AreSame(s1, copier[0]);
    }

    // -- identity, not Equals --

    /// <summary>Verifies duplicate detection, <see cref="StreamCopier.IndexOf"/>, <see cref="StreamCopier.Contains"/> and <see cref="StreamCopier.Remove"/> all use reference identity rather than <see cref="object.Equals(object?)"/>.</summary>
    [TestMethod]
    public void DuplicateDetection_UsesReferenceIdentity_NotEquals()
    {
        using EqualsAlwaysTrueStream a = new();
        using EqualsAlwaysTrueStream b = new();
        Assert.IsTrue(a.Equals(b), "test fixture sanity check");
        Assert.IsFalse(ReferenceEquals(a, b), "test fixture sanity check");

        using StreamCopier copier = new(a, b);
        Assert.AreEqual(2, copier.Count, "distinct instances that compare Equals must both be registered");

        Assert.AreEqual(0, copier.IndexOf(a));
        Assert.AreEqual(1, copier.IndexOf(b));
        Assert.IsTrue(copier.Contains(a));
        Assert.IsTrue(copier.Contains(b));

        Assert.IsTrue(copier.Remove(a));
        Assert.AreEqual(1, copier.Count);
        Assert.IsFalse(copier.Contains(a));
        Assert.IsTrue(copier.Contains(b));
    }

    // -- CanWrite / IsReadOnly --

    /// <summary>Verifies <see cref="StreamCopier.CanWrite"/> and <see cref="StreamCopier.IsReadOnly"/> flip after synchronous disposal.</summary>
    [TestMethod]
    public void CanWrite_And_IsReadOnly_ReflectDisposalState_Sync()
    {
        using MemoryStream s1 = new();
        StreamCopier copier = new(s1);
        Assert.IsTrue(copier.CanWrite);
        Assert.IsFalse(copier.IsReadOnly);
        copier.Dispose();
        Assert.IsFalse(copier.CanWrite);
        Assert.IsTrue(copier.IsReadOnly);
    }

    /// <summary>Verifies <see cref="StreamCopier.CanWrite"/> and <see cref="StreamCopier.IsReadOnly"/> flip after asynchronous disposal.</summary>
    [TestMethod]
    public async Task CanWrite_And_IsReadOnly_ReflectDisposalState_Async()
    {
        using MemoryStream s1 = new();
        StreamCopier copier = new(s1);
        Assert.IsTrue(copier.CanWrite);
        Assert.IsFalse(copier.IsReadOnly);
        await copier.DisposeAsync();
        Assert.IsFalse(copier.CanWrite);
        Assert.IsTrue(copier.IsReadOnly);
    }

    /// <summary>Verifies a copier with zero targets is still a valid writable sink.</summary>
    [TestMethod]
    public void CanWrite_IsTrueForZeroTargetCopier()
    {
        using StreamCopier copier = new();
        Assert.IsTrue(copier.CanWrite);
    }

    // -- inspection after disposal --

    /// <summary>Verifies the full inspection surface (Count, indexer, Contains, IndexOf, CopyTo, enumeration) keeps working after disposal when targets are not owned.</summary>
    [TestMethod]
    public void Inspection_RemainsAvailableAfterDispose_WhenTargetsNotOwned()
    {
        MemoryStream s1 = new();
        MemoryStream s2 = new();
        StreamCopier copier = new(closeAllTargetsOnDispose: false, s1, s2);
        copier.Dispose();

        Assert.AreEqual(2, copier.Count);
        Assert.AreSame(s1, copier[0]);
        Assert.AreSame(s2, copier[1]);
        Assert.IsTrue(copier.Contains(s1));
        Assert.AreEqual(0, copier.IndexOf(s1));
        Stream[] copy = new Stream[2];
        copier.CopyTo(copy, 0);
        Assert.AreSame(s1, copy[0]);
        int enumerated = 0;
        foreach (Stream s in copier) enumerated++;
        Assert.AreEqual(2, enumerated);

        // Targets remain open since ownership was not requested.
        s1.WriteByte(1);
        Assert.AreEqual(1, s1.Length);
        s1.Dispose();
        s2.Dispose();
    }

    /// <summary>Verifies target references remain inspectable after disposal even when ownership caused the underlying streams themselves to be disposed.</summary>
    [TestMethod]
    public void Inspection_RemainsAvailableAfterDispose_WhenTargetsOwned()
    {
        MemoryStream s1 = new();
        MemoryStream s2 = new();
        StreamCopier copier = new(closeAllTargetsOnDispose: true, s1, s2);
        copier.Dispose();

        Assert.AreEqual(2, copier.Count);
        Assert.AreSame(s1, copier[0]);
        Assert.AreSame(s2, copier[1]);
        Assert.IsTrue(copier.Contains(s1));

        // Owned targets are themselves disposed even though their references remain registered.
        Assert.ThrowsException<ObjectDisposedException>(() => s1.WriteByte(1));
        Assert.ThrowsException<ObjectDisposedException>(() => s2.WriteByte(1));
    }

    // -- mutation after disposal --

    /// <summary>Verifies every mutating <see cref="IList{Stream}"/> member throws <see cref="ObjectDisposedException"/> after synchronous disposal.</summary>
    [TestMethod]
    public void Mutation_AfterDispose_AlwaysThrowsObjectDisposedException()
    {
        using MemoryStream s1 = new();
        using MemoryStream s2 = new();
        StreamCopier copier = new(s1, s2);
        copier.Dispose();

        Assert.ThrowsException<ObjectDisposedException>(() => copier.Add(new MemoryStream()));
        Assert.ThrowsException<ObjectDisposedException>(() => copier.Insert(0, new MemoryStream()));
        Assert.ThrowsException<ObjectDisposedException>(() => copier[0] = new MemoryStream());
        Assert.ThrowsException<ObjectDisposedException>(() => copier.Remove(s1));
        Assert.ThrowsException<ObjectDisposedException>(() => copier.RemoveAt(0));
        Assert.ThrowsException<ObjectDisposedException>(() => copier.Clear());
    }

    /// <summary>Verifies every mutating <see cref="IList{Stream}"/> member throws <see cref="ObjectDisposedException"/> after asynchronous disposal.</summary>
    [TestMethod]
    public async Task Mutation_AfterDisposeAsync_AlwaysThrowsObjectDisposedException()
    {
        using MemoryStream s1 = new();
        using MemoryStream s2 = new();
        StreamCopier copier = new(s1, s2);
        await copier.DisposeAsync();

        Assert.ThrowsException<ObjectDisposedException>(() => copier.Add(new MemoryStream()));
        Assert.ThrowsException<ObjectDisposedException>(() => copier.Insert(0, new MemoryStream()));
        Assert.ThrowsException<ObjectDisposedException>(() => copier[0] = new MemoryStream());
        Assert.ThrowsException<ObjectDisposedException>(() => copier.Remove(s1));
        Assert.ThrowsException<ObjectDisposedException>(() => copier.RemoveAt(0));
        Assert.ThrowsException<ObjectDisposedException>(() => copier.Clear());
    }

    // -- owned target disposal counts --

    /// <summary>Verifies an owned target is disposed exactly once across repeated calls to <see cref="StreamCopier.Dispose()"/>.</summary>
    [TestMethod]
    public void OwnedTarget_IsDisposedExactlyOnce_AcrossRepeatedSyncDispose()
    {
        TrackingDisposeStream t = new();
        StreamCopier copier = new(closeAllTargetsOnDispose: true, t);
        copier.Dispose();
        copier.Dispose();
        Assert.AreEqual(1, t.DisposeCount);
    }

    /// <summary>Verifies an owned target is disposed exactly once across repeated calls to <see cref="StreamCopier.DisposeAsync"/>.</summary>
    [TestMethod]
    public async Task OwnedTarget_IsDisposedExactlyOnce_AcrossRepeatedAsyncDispose()
    {
        TrackingDisposeStream t = new();
        StreamCopier copier = new(closeAllTargetsOnDispose: true, t);
        await copier.DisposeAsync();
        await copier.DisposeAsync();
        Assert.AreEqual(1, t.DisposeAsyncCount);
        // MemoryStream does not override DisposeAsync, so the framework's default
        // Stream.DisposeAsync() implementation synchronously calls Dispose(true) as part
        // of the single async disposal attempt; it must still not run a second time.
        Assert.AreEqual(1, t.DisposeCount);
    }

    /// <summary>Verifies an owned target is not disposed a second time when <see cref="StreamCopier.Dispose()"/> is followed by <see cref="StreamCopier.DisposeAsync"/>.</summary>
    [TestMethod]
    public async Task OwnedTarget_IsDisposedExactlyOnce_SyncThenAsync()
    {
        TrackingDisposeStream t = new();
        StreamCopier copier = new(closeAllTargetsOnDispose: true, t);
        copier.Dispose();
        await copier.DisposeAsync();
        Assert.AreEqual(1, t.DisposeCount);
        Assert.AreEqual(0, t.DisposeAsyncCount);
    }

    /// <summary>Verifies an owned target is not disposed a second time when <see cref="StreamCopier.DisposeAsync"/> is followed by <see cref="StreamCopier.Dispose()"/>.</summary>
    [TestMethod]
    public async Task OwnedTarget_IsDisposedExactlyOnce_AsyncThenSync()
    {
        TrackingDisposeStream t = new();
        StreamCopier copier = new(closeAllTargetsOnDispose: true, t);
        await copier.DisposeAsync();
        copier.Dispose();
        Assert.AreEqual(1, t.DisposeAsyncCount);
        // See the comment in the repeated-async-dispose test: the framework's default
        // Stream.DisposeAsync() already triggers Dispose(true) once as part of that call.
        Assert.AreEqual(1, t.DisposeCount);
    }

    // -- disposal failure --

    /// <summary>Verifies a failing owned target still lets every target be attempted, aggregates the failure, and leaves the copier fully disposed without retry on a second call.</summary>
    [TestMethod]
    public void Dispose_TargetFailure_AttemptsAllTargets_AggregatesAndLeavesCopierDisposed()
    {
        TrackingDisposeStream good1 = new();
        TrackingDisposeStream bad = new() { ShouldThrowOnDispose = true };
        TrackingDisposeStream good2 = new();
        StreamCopier copier = new(closeAllTargetsOnDispose: true, good1, bad, good2);

        AggregateException ex = Assert.ThrowsException<AggregateException>(() => copier.Dispose());
        Assert.AreEqual(1, ex.InnerExceptions.Count);
        Assert.AreEqual(1, good1.DisposeCount);
        Assert.AreEqual(1, bad.DisposeCount);
        Assert.AreEqual(1, good2.DisposeCount);

        Assert.IsFalse(copier.CanWrite);
        Assert.IsTrue(copier.IsReadOnly);
        Assert.AreEqual(3, copier.Count);
        Assert.AreSame(good1, copier[0]);

        // A second dispose must not retry target disposal or throw again.
        copier.Dispose();
        Assert.AreEqual(1, good1.DisposeCount);
        Assert.AreEqual(1, bad.DisposeCount);
        Assert.AreEqual(1, good2.DisposeCount);
    }

    // -- target becomes unwritable after registration --

    /// <summary>Verifies the copier's own <see cref="StreamCopier.CanWrite"/> does not depend on a registered target's external state, and that a failing target simply participates in the normal aggregate write failure.</summary>
    [TestMethod]
    public void CanWrite_StaysTrue_WhenARegisteredTargetIsExternallyDisposed()
    {
        MemoryStream target = new();
        using StreamCopier copier = new(closeAllTargetsOnDispose: false, target);
        target.Dispose();

        Assert.IsTrue(copier.CanWrite, "the copier's own capability does not depend on external target state");
        AggregateException ex = Assert.ThrowsException<AggregateException>(
            () => copier.Write(new byte[] { 1 }, 0, 1));
        Assert.AreEqual(1, ex.InnerExceptions.Count);
    }
}
