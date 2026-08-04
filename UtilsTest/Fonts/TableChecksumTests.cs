using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF.Parsing;

namespace UtilsTest.Fonts;

/// <summary>
/// Regression tests for <see cref="TableChecksum.ComputeTableChecksum"/>'s support for ranges
/// beyond <see cref="uint.MaxValue"/> bytes (TODO-2026-07-19-pass2.md item 26 review follow-up).
/// </summary>
[TestClass]
public class TableChecksumTests
{
    // Review fix: ComputeTableChecksum used to accept `uint length`, and TrueTypeFont.VerifyFontChecksum
    // clamped the whole-font range to Math.Min(fontLength, uint.MaxValue) before calling it -- silently
    // dropping any bytes beyond the first 4 GiB from the whole-font checksum, contradicting the
    // documented "covers the entire bounded font" contract for any TrueTypeFontParsingOptions.MaximumFontBytes
    // configuration above uint.MaxValue. `length` is now `long`, and this test proves the full range is
    // actually read: a synthetic stream (no real 4+ GiB backing buffer) reports a length just past
    // uint.MaxValue, is all-zero except for a single non-zero final word placed beyond the old 4 GiB
    // truncation point. Summing all-zero words never changes the running checksum, so the correct result
    // is exactly that final word's value (1) -- if the old truncation bug were still present, that word
    // would never be read and the result would incorrectly be 0.
    [TestMethod]
    [Timeout(180_000)]
    public void ComputeTableChecksum_RangeBeyondUInt32MaxValue_ReadsTheFullRange()
    {
        // The smallest word-aligned length past uint.MaxValue: uint.MaxValue itself is not a
        // multiple of 4 (4294967295 mod 4 == 3), so uint.MaxValue + 1 is the first multiple of 4
        // beyond it. The final 4-byte word, at [length-4, length), then starts exactly at
        // uint.MaxValue - 3 and ends at uint.MaxValue + 1 -- its very last byte (at index
        // uint.MaxValue, the first byte the old `Math.Min(fontLength, uint.MaxValue)` truncation
        // would have dropped) is the only non-zero byte in the whole stream.
        long length = (long)uint.MaxValue + 1;
        long finalWordOffset = length - 4;
        using var stream = new SparseZeroStream(length, finalWordOffset, [0, 0, 0, 1]); // big-endian value 1

        uint result = TableChecksum.ComputeTableChecksum(stream, offset: 0, length: length, zeroHeadChecksumAdjustment: false);

        // Every other word is all-zero, which never changes the running sum, so the correct result
        // is exactly this final word's value. Under the old bug, the length would have been
        // silently capped to uint.MaxValue, which is not a multiple of 4: the algorithm would then
        // have treated bytes [4294967292, 4294967295) as an incomplete final word, zero-padding the
        // 4th byte instead of reading the real (non-zero) one -- producing 0, not 1.
        Assert.AreEqual(1u, result);
    }

    [TestMethod]
    public void ComputeTableChecksum_NegativeLength_Throws()
    {
        using var stream = new MemoryStream();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => TableChecksum.ComputeTableChecksum(stream, 0, -1, false));
    }

    // Companion check: fixing the checksum's own range support (rather than capping the option) means
    // TrueTypeFontParsingOptions never needed an artificial uint.MaxValue ceiling on MaximumFontBytes.
    [TestMethod]
    public void ParsingOptions_MaximumFontBytesAboveUInt32MaxValue_IsNotRejectedByEnsureValid()
    {
        var options = new TrueTypeFontParsingOptions { MaximumFontBytes = (long)uint.MaxValue + 1_000_000 };
        // EnsureValid is internal; exercised indirectly through a real parse that must fail for an
        // unrelated reason (too-small buffer) rather than an options-validation error.
        var ex = Assert.ThrowsExactly<FontParseException>(() => Utils.Fonts.TTF.TrueTypeFont.ParseFont(ReadOnlySpan<byte>.Empty, options));
        Assert.AreEqual(FontDiagnosticCode.InvalidOffsetTable, ex.Diagnostic.Code);
    }

    /// <summary>
    /// A read-only, seekable stream that reports an arbitrary (potentially huge) <see cref="Length"/>
    /// without allocating a real backing buffer: every byte reads as zero, except for a single
    /// caller-specified byte range.
    /// </summary>
    private sealed class SparseZeroStream(long length, long nonZeroOffset, byte[] nonZeroBytes) : Stream
    {
        private long position;

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length { get; } = length;
        public override long Position { get => position; set => position = value; }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        // Overridden directly (not just the byte[] overload) so TableChecksum's per-word
        // Stream.Read(Span<byte>) calls avoid the base Stream implementation's array-pool
        // rent/copy/return round trip -- otherwise negligible, but this stream is read roughly
        // (uint.MaxValue + 1) / 4 times by the boundary test above, where that overhead adds up.
        public override int Read(Span<byte> buffer)
        {
            long remaining = Length - position;
            int n = (int)Math.Min(buffer.Length, Math.Max(0, remaining));
            for (int i = 0; i < n; i++)
            {
                long absolute = position + i;
                byte value = 0;
                if (absolute >= nonZeroOffset && absolute < nonZeroOffset + nonZeroBytes.Length)
                {
                    value = nonZeroBytes[absolute - nonZeroOffset];
                }
                buffer[i] = value;
            }
            position += n;
            return n;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => position + offset,
                SeekOrigin.End => Length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
            };
            return position;
        }

        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
