using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF.Parsing;

namespace UtilsTest.Fonts;

/// <summary>
/// Regression tests for <see cref="TableChecksum.ComputeTableChecksum"/>'s block-based reading
/// (TODO-2026-07-19-pass2.md item 26, review follow-up rounds 4-5). Fonts larger than
/// <see cref="uint.MaxValue"/> are rejected well before a checksum is ever computed (see
/// <c>TrueTypeFontDirectoryTests</c>), so these tests exercise realistic, bounded ranges -- proving
/// the pooled-buffer block reader produces exactly the same result as the byte-at-a-time algorithm
/// it replaced, including at block boundaries, without ever materializing the whole range at once.
/// </summary>
[TestClass]
public class TableChecksumTests
{
    [TestMethod]
    public void ComputeTableChecksum_NegativeLength_Throws()
    {
        using var stream = new MemoryStream();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => TableChecksum.ComputeTableChecksum(stream, 0, -1, false));
    }

    [TestMethod]
    public void ComputeTableChecksum_LengthAboveUInt32MaxValue_Throws()
    {
        using var stream = new MemoryStream();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            TableChecksum.ComputeTableChecksum(stream, 0, (long)uint.MaxValue + 1, false));
    }

    [TestMethod]
    public void ComputeTableChecksum_LengthMultipleOfFour_SumsEachWordExactly()
    {
        byte[] data = [0, 0, 0, 1, 0, 0, 0, 2]; // two words: 1, 2
        using var stream = new MemoryStream(data);

        uint result = TableChecksum.ComputeTableChecksum(stream, 0, data.Length, false);

        Assert.AreEqual(3u, result);
    }

    [TestMethod]
    public void ComputeTableChecksum_LengthNotMultipleOfFour_ZeroPadsFinalWord()
    {
        byte[] data = [0, 0, 0, 1, 0, 0, 0, 2, 0xFF]; // two full words (1, 2) + one dangling byte
        using var stream = new MemoryStream(data);

        uint result = TableChecksum.ComputeTableChecksum(stream, 0, data.Length, false);

        // The final word is [0xFF, 0, 0, 0] once zero-padded => 0xFF000000.
        Assert.AreEqual(3u + 0xFF000000u, result);
    }

    [TestMethod]
    public void ComputeTableChecksum_NonZeroOffset_StartsAtTheRequestedPosition()
    {
        byte[] data = [0xAA, 0xAA, 0xAA, 0xAA, 0, 0, 0, 5]; // junk word, then the real word (5)
        using var stream = new MemoryStream(data);

        uint result = TableChecksum.ComputeTableChecksum(stream, offset: 4, length: 4, false);

        Assert.AreEqual(5u, result);
    }

    [TestMethod]
    public void ComputeTableChecksum_PartialUnderlyingReads_StillProducesTheCorrectSum()
    {
        byte[] data = [0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 3];
        using var stream = new OneOrTwoBytesAtATimeStream(data);

        uint result = TableChecksum.ComputeTableChecksum(stream, 0, data.Length, false);

        Assert.AreEqual(6u, result);
    }

    [TestMethod]
    public void ComputeTableChecksum_RestoresStreamPositionAfterSuccess()
    {
        byte[] data = [0, 0, 0, 1, 0, 0, 0, 2];
        using var stream = new MemoryStream(data) { Position = 3 };

        TableChecksum.ComputeTableChecksum(stream, offset: 0, length: data.Length, false);

        Assert.AreEqual(3, stream.Position);
    }

    [TestMethod]
    public void ComputeTableChecksum_RestoresStreamPositionAfterException()
    {
        byte[] data = [0, 0, 0, 1]; // shorter than the requested length
        using var stream = new MemoryStream(data) { Position = 2 };

        Assert.ThrowsExactly<EndOfStreamException>(() =>
            TableChecksum.ComputeTableChecksum(stream, offset: 0, length: 100, false));

        Assert.AreEqual(2, stream.Position);
    }

    [TestMethod]
    public void ComputeTableChecksum_ZeroesHeadChecksumAdjustmentVirtually_WithoutMutatingSource()
    {
        // 'head' layout: 8 bytes of other fields, then the 4-byte checksumAdjustment (here: a
        // deliberately "wrong-looking" non-zero value, to prove it is excluded from the sum without
        // ever being overwritten), then 4 more bytes of other fields.
        byte[] data = [0, 0, 0, 1, 0, 0, 0, 2, 0xDE, 0xAD, 0xBE, 0xEF, 0, 0, 0, 3];
        byte[] originalCopy = (byte[])data.Clone();
        using var stream = new MemoryStream(data);

        uint result = TableChecksum.ComputeTableChecksum(stream, 0, data.Length, zeroHeadChecksumAdjustment: true);

        Assert.AreEqual(1u + 2u + 3u, result); // the checksumAdjustment word contributes 0, not 0xDEADBEEF
        CollectionAssert.AreEqual(originalCopy, data); // and the source bytes are untouched
    }

    [TestMethod]
    public void ComputeTableChecksum_MultipleBlocks_MatchesNaiveWordByWordReference()
    {
        // Larger than the internal pooled block size (64 KiB) and not a multiple of 4, so this
        // exercises both multiple block reads and the final zero-padded partial word, cross-checked
        // against an independent, straightforward reference implementation.
        var random = new Random(20260804);
        byte[] data = new byte[200_003];
        random.NextBytes(data);
        using var stream = new MemoryStream(data);

        uint result = TableChecksum.ComputeTableChecksum(stream, 0, data.Length, false);

        Assert.AreEqual(NaiveWordSum(data), result);
    }

    /// <summary>Reference implementation: sum every 4-byte big-endian word, zero-padding the final partial word.</summary>
    private static uint NaiveWordSum(byte[] data)
    {
        unchecked
        {
            uint sum = 0;
            for (int i = 0; i < data.Length; i += 4)
            {
                uint b0 = data[i];
                uint b1 = i + 1 < data.Length ? data[i + 1] : 0u;
                uint b2 = i + 2 < data.Length ? data[i + 2] : 0u;
                uint b3 = i + 3 < data.Length ? data[i + 3] : 0u;
                sum += (b0 << 24) | (b1 << 16) | (b2 << 8) | b3;
            }
            return sum;
        }
    }

    /// <summary>A stream that never returns more than 2 bytes from a single <see cref="Read(byte[], int, int)"/> call, to exercise <c>ComputeTableChecksum</c>'s tolerance of partial underlying reads (including reads smaller than a single 4-byte word).</summary>
    private sealed class OneOrTwoBytesAtATimeStream(byte[] data) : Stream
    {
        private readonly MemoryStream inner = new(data);
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, Math.Min(count, 2));
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
