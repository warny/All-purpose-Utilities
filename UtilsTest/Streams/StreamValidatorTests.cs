using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using Utils.IO;
using Utils.Collections;

namespace UtilsTest.Streams;

[TestClass]
public class StreamValidatorTests
{
    // ---- existing tests ----

    [TestMethod]
    public void ValidateWritesBufferedData()
    {
        using MemoryStream target = new MemoryStream();
        StreamValidator validator = new StreamValidator(target);
        byte[] data = { 1, 2, 3, 4 };
        validator.Write(data, 0, data.Length);
        Assert.AreEqual(0, target.Length);
        validator.Validate();
        var comparer = EnumerableEqualityComparer<byte>.Default;
        Assert.AreEqual(data, target.ToArray(), comparer);
    }

    [TestMethod]
    public void DiscardClearsBuffer()
    {
        using MemoryStream target = new MemoryStream();
        StreamValidator validator = new StreamValidator(target);
        validator.Write(new byte[] { 1, 2, 3 }, 0, 3);
        validator.Discard();
        validator.Validate();
        Assert.AreEqual(0, target.Length);
    }

    [TestMethod]
    public void MultipleValidationsAppendData()
    {
        using MemoryStream target = new MemoryStream();
        StreamValidator validator = new StreamValidator(target);
        byte[] data1 = { 1, 2 };
        byte[] data2 = { 3, 4 };
        validator.Write(data1, 0, data1.Length);
        validator.Validate();
        validator.Write(data2, 0, data2.Length);
        validator.Validate();
        var expected = new byte[] { 1, 2, 3, 4 };
        var comparer = EnumerableEqualityComparer<byte>.Default;
        Assert.AreEqual(expected, target.ToArray(), comparer);
    }

    // ---- item 5: limite de taille + arithmetique ----

    [TestMethod]
    public void WriteRejectsNegativeMaxBufferSize()
    {
        using var ms = new MemoryStream();
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new StreamValidator(ms, 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new StreamValidator(ms, -1));
    }

    [TestMethod]
    public void WriteThrowsWhenMaxBufferSizeExceeded()
    {
        using var ms = new MemoryStream();
        var validator = new StreamValidator(ms, maxBufferSize: 5);
        validator.Write(new byte[5], 0, 5);
        Assert.ThrowsException<InvalidOperationException>(() => validator.Write(new byte[1], 0, 1));
    }

    [TestMethod]
    public void BufferGrowsCorrectlyWithinLimit()
    {
        using var ms = new MemoryStream();
        var validator = new StreamValidator(ms, maxBufferSize: 1024);
        byte[] chunk = new byte[100];
        for (int i = 0; i < 10; i++)
            validator.Write(chunk, 0, chunk.Length);
        validator.Validate();
        Assert.AreEqual(1000, ms.Length);
    }

    // ---- item 6: rollback pour les cibles seekables ----

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
    public void Validate_RollsBackSeekableTargetOnFailure()
    {
        var target = new ThrowingStream();
        // Write some legit data first so Position is not 0
        target.Write(new byte[] { 0xFF }, 0, 1);

        var validator = new StreamValidator(target);
        validator.Write(new byte[] { 1, 2, 3 }, 0, 3);

        target.ShouldThrow = true;
        Assert.ThrowsException<IOException>(() => validator.Validate());
        target.ShouldThrow = false;

        // Position must be restored; buffer must still contain the data for a retry
        Assert.AreEqual(1, target.Position, "target position must be rolled back");

        // Retry must succeed
        validator.Validate();
        Assert.AreEqual(4, target.Length);
    }

    [TestMethod]
    public void Validate_EmptyBufferIsNoop()
    {
        using var ms = new MemoryStream();
        var validator = new StreamValidator(ms);
        validator.Validate(); // must not throw
        Assert.AreEqual(0, ms.Length);
    }
}
