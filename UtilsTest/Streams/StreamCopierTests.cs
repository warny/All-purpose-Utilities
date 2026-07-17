using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
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
}
