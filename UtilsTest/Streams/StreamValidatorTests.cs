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

    // Throws before writing any bytes when ShouldThrow is true.
    private class ThrowingStream : MemoryStream
    {
        public bool ShouldThrow { get; set; }
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (ShouldThrow) throw new IOException("Simulated write failure");
            base.Write(buffer, offset, count);
        }
    }

    // Writes up to FailAfterBytes bytes, then throws — simulates a mid-write failure.
    private class PartialWriteThrowingStream : MemoryStream
    {
        public int FailAfterBytes { get; set; } = int.MaxValue;
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (count > FailAfterBytes)
            {
                base.Write(buffer, offset, FailAfterBytes);
                throw new IOException("Simulated partial write failure");
            }
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

        // Position must be restored; the buffer is retained so the caller can Discard or retry.
        // Retry is safe here only because ThrowingStream threw before any bytes reached the target;
        // in the general case (partial write) the target content is in an indeterminate state.
        Assert.AreEqual(1, target.Position, "target position must be rolled back");

        validator.Validate();
        Assert.AreEqual(4, target.Length);
    }

    [TestMethod]
    public void Validate_PartialWrite_RestoresPositionOnly_ContentAndBufferReflectPartialState()
    {
        // Arrange: stream that writes 2 bytes then throws
        var target = new PartialWriteThrowingStream();
        target.Write(new byte[] { 0xAA }, 0, 1); // pre-existing byte at index 0
        long positionBeforeValidate = target.Position; // = 1

        var validator = new StreamValidator(target);
        validator.Write(new byte[] { 1, 2, 3, 4, 5 }, 0, 5);

        target.FailAfterBytes = 2; // write 2 bytes then throw
        Assert.ThrowsException<IOException>(() => validator.Validate());

        // Position is restored to where it was before the (partial) write
        Assert.AreEqual(positionBeforeValidate, target.Position, "target position must be restored");

        // The 2 bytes that were written before the throw ARE still in the target —
        // this documents that position restoration does not undo content already written.
        byte[] content = target.ToArray();
        Assert.AreEqual(3, content.Length, "pre-existing byte + 2 partially written bytes");
        Assert.AreEqual(0xAA, content[0]);
        Assert.AreEqual(1,    content[1]);
        Assert.AreEqual(2,    content[2]);

        // The internal buffer is preserved; the caller may Discard to abandon
        target.FailAfterBytes = int.MaxValue;
        validator.Discard();
        validator.Validate(); // no-op: buffer was discarded
        Assert.AreEqual(3, target.Length, "after Discard, no further data written");
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
