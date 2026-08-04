using System;
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
    /// Computes the checksum of <paramref name="length"/> bytes starting at <paramref name="offset"/>
    /// in <paramref name="stream"/>, per the TrueType/OpenType algorithm: the sum, as an unsigned
    /// 32-bit big-endian word accumulator, of every 4-byte word in the range, with the final partial
    /// word (if any) padded with zero bytes.
    /// </summary>
    /// <param name="stream">The seekable stream to read from. Its position is saved and restored.</param>
    /// <param name="offset">The absolute byte offset, within <paramref name="stream"/>, of the range to checksum.</param>
    /// <param name="length">The byte length of the range to checksum.</param>
    /// <param name="zeroHeadChecksumAdjustment">
    /// When <see langword="true"/>, the 4-byte word at <see cref="HeadChecksumAdjustmentOffset"/>
    /// relative to <paramref name="offset"/> is treated as zero for the purpose of the sum, without
    /// ever writing to the stream. Pass <see langword="true"/> only when checksumming the 'head'
    /// table's own data in isolation (matching the wire format's own convention for how a 'head'
    /// table's per-table checksum is computed).
    /// </param>
    /// <returns>The computed checksum.</returns>
    public static uint ComputeTableChecksum(Stream stream, long offset, uint length, bool zeroHeadChecksumAdjustment)
    {
        ArgumentNullException.ThrowIfNull(stream);
        long originalPosition = stream.Position;
        try
        {
            stream.Position = offset;
            unchecked
            {
                uint result = 0;
                long remaining = length;
                long wordOffset = 0;
                Span<byte> word = stackalloc byte[4];
                while (remaining > 0)
                {
                    int toRead = (int)Math.Min(4, remaining);
                    word.Clear();
                    ReadExactly(stream, word[..toRead]);
                    if (zeroHeadChecksumAdjustment && wordOffset == HeadChecksumAdjustmentOffset)
                    {
                        word.Clear();
                    }
                    result += (uint)((word[0] << 24) | (word[1] << 16) | (word[2] << 8) | word[3]);
                    remaining -= toRead;
                    wordOffset += 4;
                }
                return result;
            }
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

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
