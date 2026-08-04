using System;
using System.Buffers;
using System.IO;

namespace Utils.Fonts.TTF.Parsing;

/// <summary>
/// Computes SFNT table checksums by reading directly from a seekable stream, without ever writing
/// to it. Replaces the previous approach (<c>TrueTypeFont.ComputeChecksum</c>), which routed the
/// data through a <see cref="Utils.IO.Serialization.ReaderWriter"/> and temporarily zeroed the
/// 'head' table's <c>checksumAdjustment</c> word <em>in the underlying stream</em> before reading
/// it back -- a data race and a correctness hazard for any caller still holding a reference to
/// that stream or its backing buffer.
/// </summary>
internal static class TableChecksum
{
    /// <summary>Byte offset of 'head'.checksumAdjustment within the table's own data, per the TrueType spec.</summary>
    private const int HeadChecksumAdjustmentOffset = 8;

    /// <summary>
    /// Size of the pooled read buffer. Chosen well within the 32-128 KiB range: large enough that
    /// even a multi-megabyte table/font is read in a small, bounded number of <see cref="Stream.Read(Span{byte})"/>
    /// calls, small enough to stay a trivial, short-lived rental regardless of how many checksums a
    /// single font parse computes.
    /// </summary>
    private const int BlockSize = 64 * 1024;

    /// <summary>
    /// Computes the checksum of <paramref name="length"/> bytes starting at <paramref name="offset"/>
    /// in <paramref name="stream"/>, per the TrueType/OpenType algorithm: the sum, as an unchecked
    /// (intentionally wrapping) unsigned 32-bit big-endian word accumulator, of every 4-byte word in
    /// the range, with the final partial word (if any) padded with zero bytes. Reads in bounded
    /// <see cref="BlockSize"/> blocks via a pooled buffer -- never the whole range at once -- so
    /// callers can safely checksum a multi-megabyte table or an entire font without a matching
    /// multi-megabyte allocation.
    /// </summary>
    /// <param name="stream">The seekable stream to read from. Its position is saved and restored, even if reading throws.</param>
    /// <param name="offset">The absolute byte offset, within <paramref name="stream"/>, of the range to checksum.</param>
    /// <param name="length">
    /// The byte length of the range to checksum. Must be in <c>[0, uint.MaxValue]</c>: the SFNT
    /// checksum algorithm is only ever applied to UInt32-addressable ranges (an individual table's
    /// length is itself a UInt32 field, and callers must reject a font longer than
    /// <see cref="uint.MaxValue"/> before ever computing a whole-font checksum over it -- see
    /// <see cref="TrueTypeFontParsingOptions.MaximumFontBytes"/>). A caller passing a longer range
    /// has a bug upstream; this throws rather than silently truncating it.
    /// </param>
    /// <param name="zeroHeadChecksumAdjustment">
    /// When <see langword="true"/>, the 4-byte word at <see cref="HeadChecksumAdjustmentOffset"/>
    /// relative to <paramref name="offset"/> is treated as zero for the purpose of the sum, without
    /// ever writing to the stream. Pass <see langword="true"/> only when checksumming the 'head'
    /// table's own data in isolation (matching the wire format's own convention for how a 'head'
    /// table's per-table checksum is computed).
    /// </param>
    /// <returns>The computed checksum.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative or exceeds <see cref="uint.MaxValue"/>.</exception>
    /// <exception cref="EndOfStreamException"><paramref name="stream"/> has fewer than <paramref name="offset"/> + <paramref name="length"/> bytes available.</exception>
    public static uint ComputeTableChecksum(Stream stream, long offset, long length, bool zeroHeadChecksumAdjustment)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (length < 0 || length > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(length), length,
                $"length must be in the range [0, {uint.MaxValue}] (UInt32-addressable, per the SFNT checksum algorithm).");
        }

        long originalPosition = stream.Position;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(BlockSize);
        try
        {
            stream.Position = offset;
            unchecked
            {
                uint result = 0;
                long remaining = length;
                long consumed = 0; // Byte offset (relative to `offset`) of the word currently being assembled.
                Span<byte> carry = stackalloc byte[4];
                int carryCount = 0; // Valid leftover bytes in carry[0..carryCount), not yet a full word.

                while (remaining > 0)
                {
                    int blockLength = (int)Math.Min(BlockSize, remaining);
                    ReadExactly(stream, buffer.AsSpan(0, blockLength));
                    remaining -= blockLength;

                    int i = 0;
                    if (carryCount > 0)
                    {
                        // Complete the word left over from the previous block using bytes from the
                        // start of this one -- a UInt32 word may straddle a block boundary.
                        int need = Math.Min(4 - carryCount, blockLength);
                        buffer.AsSpan(0, need).CopyTo(carry[carryCount..]);
                        carryCount += need;
                        i = need;
                        if (carryCount == 4)
                        {
                            result += ShouldZero(zeroHeadChecksumAdjustment, consumed) ? 0u : WordValue(carry);
                            consumed += 4;
                            carryCount = 0;
                        }
                    }

                    while (i + 4 <= blockLength)
                    {
                        var word = buffer.AsSpan(i, 4);
                        result += ShouldZero(zeroHeadChecksumAdjustment, consumed) ? 0u : WordValue(word);
                        consumed += 4;
                        i += 4;
                    }

                    int leftover = blockLength - i;
                    if (leftover > 0)
                    {
                        buffer.AsSpan(i, leftover).CopyTo(carry);
                        carryCount = leftover;
                    }
                }

                if (carryCount > 0)
                {
                    // Final partial word: complete with zero padding, per the algorithm.
                    carry[carryCount..].Clear();
                    result += ShouldZero(zeroHeadChecksumAdjustment, consumed) ? 0u : WordValue(carry);
                }

                return result;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            stream.Position = originalPosition;
        }
    }

    /// <summary>Interprets a 4-byte span as a big-endian <see cref="uint"/>.</summary>
    private static uint WordValue(ReadOnlySpan<byte> word) =>
        unchecked((uint)((word[0] << 24) | (word[1] << 16) | (word[2] << 8) | word[3]));

    /// <summary>Whether the word currently being assembled is the virtual 'head'.checksumAdjustment word.</summary>
    private static bool ShouldZero(bool zeroHeadChecksumAdjustment, long wordOffset) =>
        zeroHeadChecksumAdjustment && wordOffset == HeadChecksumAdjustmentOffset;

    /// <summary>Reads exactly <paramref name="buffer"/>.Length bytes, tolerating partial underlying reads.</summary>
    private static void ReadExactly(Stream stream, Span<byte> buffer)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int read = stream.Read(buffer[total..]);
            if (read == 0)
            {
                throw new EndOfStreamException("Unexpected end of stream while computing a table checksum.");
            }
            total += read;
        }
    }
}
