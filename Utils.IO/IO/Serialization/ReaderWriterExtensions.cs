using System;
using System.IO;
using System.Text;

namespace Utils.IO.Serialization;

/// <summary>
/// Extension methods providing convenience helpers for <see cref="Reader"/> and <see cref="Writer"/>.
/// </summary>
public static class ReaderWriterExtensions
{
    /// <summary>
    /// Reads a fixed-length string from the reader using the provided encoding.
    /// </summary>
    /// <param name="reader">The reader to read from.</param>
    /// <param name="length">Number of bytes to read.</param>
    /// <param name="encoding">Encoding of the string.</param>
    /// <returns>The decoded string with trailing null or space characters removed.</returns>
    public static string ReadFixedLengthString(this Reader reader, int length, Encoding encoding)
    {
        var bytes = reader.ReadBytes(length);
        return encoding.GetString(bytes).TrimEnd('\0', ' ');
    }

    /// <summary>
    /// Reads a length-prefixed string from the reader.
    /// </summary>
    /// <param name="reader">The reader to read from.</param>
    /// <param name="encoding">Encoding of the string.</param>
    /// <param name="sizeLength">Number of bytes used to store the length. Must be 1, 2, or 4.</param>
    /// <returns>The decoded string.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="sizeLength"/> is not 1, 2, or 4.</exception>
    /// <exception cref="FormatException">Thrown when a four-byte prefix contains a negative length.</exception>
    public static string ReadVariableLengthString(this Reader reader, Encoding encoding, int sizeLength = sizeof(int))
        => ReadVariableLengthString(reader, encoding, sizeLength, int.MaxValue);

    /// <summary>
    /// Reads a length-prefixed string from the reader, enforcing a maximum byte length before allocation.
    /// </summary>
    /// <param name="reader">The reader to read from.</param>
    /// <param name="encoding">Encoding of the string.</param>
    /// <param name="sizeLength">Number of bytes used to store the length. Must be 1, 2, or 4.</param>
    /// <param name="maxByteLength">
    /// The maximum number of encoded bytes accepted for the payload. A length read from the stream that
    /// exceeds this value is rejected before any payload allocation.
    /// </param>
    /// <returns>The decoded string.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="sizeLength"/> is not 1, 2, or 4, when <paramref name="maxByteLength"/> is negative,
    /// or when the length read from the stream exceeds <paramref name="maxByteLength"/>.
    /// </exception>
    /// <exception cref="FormatException">Thrown when a four-byte prefix contains a negative length.</exception>
    public static string ReadVariableLengthString(this Reader reader, Encoding encoding, int sizeLength, int maxByteLength)
    {
        // Validate all arguments before performing any I/O so a bad call does not consume bytes.
        ValidateSizeLength(sizeLength);
        if (maxByteLength < 0)
            throw new ArgumentOutOfRangeException(nameof(maxByteLength), "The maximum byte length must be non-negative.");

        int length = sizeLength switch
        {
            1 => reader.ReadByte(),
            2 => reader.Read<UInt16>(),
            _ => reader.Read<Int32>()
        };

        // A four-byte prefix is signed on the wire; reject negative lengths explicitly.
        if (length < 0)
            throw new FormatException($"The length prefix contains a negative value ({length}).");
        // Enforce the configured maximum before allocating the payload buffer.
        if (length > maxByteLength)
            throw new ArgumentOutOfRangeException(nameof(maxByteLength), $"The declared length {length} exceeds the maximum allowed byte length {maxByteLength}.");

        return encoding.GetString(reader.ReadBytes(length));
    }

    /// <summary>
    /// Validates that a length-prefix width is one of the supported values (1, 2, or 4 bytes).
    /// </summary>
    /// <param name="sizeLength">The prefix width to validate.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="sizeLength"/> is not 1, 2, or 4.</exception>
    private static void ValidateSizeLength(int sizeLength)
    {
        if (sizeLength is not (1 or 2 or 4))
            throw new ArgumentOutOfRangeException(nameof(sizeLength), sizeLength, "The length prefix width must be 1, 2, or 4 bytes.");
    }

    /// <summary>
    /// Reads an array of values from the reader using the reader's configured endianness.
    /// </summary>
    /// <typeparam name="T">Type of elements to read.</typeparam>
    /// <param name="reader">The reader to read from.</param>
    /// <param name="count">Number of elements to read.</param>
    public static T[] ReadArray<T>(this Reader reader, int count)
    {
        var result = new T[count];
        for (int i = 0; i < count; i++)
            result[i] = reader.Read<T>();
        return result;
    }

    /// <summary>
    /// Writes a fixed-length string using the provided encoding. The string is padded with zero bytes if necessary.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">String value to write.</param>
    /// <param name="length">Desired length in bytes.</param>
    /// <param name="encoding">Encoding of the string.</param>
    public static void WriteFixedLengthString(this Writer writer, string value, int length, Encoding encoding)
    {
        var buffer = new byte[length];
        encoding.GetBytes(value, 0, value.Length, buffer, 0);
        writer.WriteBytes(buffer);
    }

    /// <summary>
    /// Maximum number of bytes a 64-bit LEB128 value may occupy (ceil(64 / 7) = 10).
    /// </summary>
    private const int Leb128MaxBytes = 10;

    /// <summary>
    /// Reads an unsigned LEB128-encoded integer from the reader.
    /// </summary>
    /// <remarks>
    /// The reader is bounded to 10 bytes for a 64-bit value. The tenth byte must have its continuation
    /// bit clear and a payload of at most <c>0x01</c> (a single high bit), because 9 groups of 7 bits
    /// already cover 63 bits. Overlong encodings such as <c>80 00</c> for zero are rejected: a group whose
    /// payload is zero and whose continuation bit is clear is only valid when it is the first byte.
    /// </remarks>
    /// <param name="reader">The reader to read from.</param>
    /// <returns>The unsigned integer decoded from the LEB128 byte sequence.</returns>
    /// <exception cref="EndOfStreamException">Thrown when the stream ends before the value is complete.</exception>
    /// <exception cref="FormatException">Thrown when the encoding is non-canonical (overlong).</exception>
    /// <exception cref="OverflowException">Thrown when the encoded value does not fit in a <see cref="ulong"/>.</exception>
    public static ulong ReadULEB128(this IReader reader)
    {
        ulong result = 0;
        int shift = 0;
        int byteCount = 0;
        int raw;
        while (true)
        {
            raw = reader.ReadByte();
            if (raw < 0)
                throw new EndOfStreamException("Unexpected end of stream reading ULEB128.");
            byteCount++;

            int payload = raw & 0x7F;
            bool more = (raw & 0x80) != 0;

            if (byteCount == Leb128MaxBytes)
            {
                // Only the lowest bit remains available (63 bits already consumed by 9 groups).
                if (more)
                    throw new OverflowException("ULEB128 value exceeds 64 bits (continuation bit set on the tenth byte).");
                if (payload > 0x01)
                    throw new OverflowException("ULEB128 value exceeds 64 bits (tenth byte payload greater than 0x01).");
            }
            else if (byteCount > Leb128MaxBytes)
            {
                // Guarded by the continuation check above, but kept as a defensive bound.
                throw new OverflowException("ULEB128 value exceeds 64 bits.");
            }

            result |= (ulong)payload << shift;

            if (!more)
            {
                // Reject a non-canonical trailing zero group (e.g. 80 00). A zero payload with the
                // continuation bit clear is only canonical when it is the very first byte.
                if (byteCount > 1 && payload == 0)
                    throw new FormatException("Non-canonical (overlong) ULEB128 encoding.");
                return result;
            }

            shift += 7;
        }
    }

    /// <summary>
    /// Reads a signed LEB128-encoded integer from the reader.
    /// </summary>
    /// <remarks>
    /// The reader is bounded to 10 bytes for a 64-bit value. On the tenth byte the bits above the sign
    /// bit (bit 62 of the accumulated value) must be a valid sign extension: all zero for a non-negative
    /// value and all one for a negative value. Overlong encodings — a trailing <c>00</c> group extending a
    /// non-negative value or a trailing <c>7F</c> group extending a negative value — are rejected as
    /// non-canonical.
    /// </remarks>
    /// <param name="reader">The reader to read from.</param>
    /// <returns>The signed integer decoded from the LEB128 byte sequence.</returns>
    /// <exception cref="EndOfStreamException">Thrown when the stream ends before the value is complete.</exception>
    /// <exception cref="FormatException">Thrown when the encoding is non-canonical (overlong).</exception>
    /// <exception cref="OverflowException">Thrown when the encoded value does not fit in a <see cref="long"/>.</exception>
    public static long ReadSLEB128(this IReader reader)
    {
        long result = 0;
        int shift = 0;
        int byteCount = 0;
        int previousPayload = 0;
        int raw;
        while (true)
        {
            raw = reader.ReadByte();
            if (raw < 0)
                throw new EndOfStreamException("Unexpected end of stream reading SLEB128.");
            byteCount++;

            int payload = raw & 0x7F;
            bool more = (raw & 0x80) != 0;

            if (byteCount == Leb128MaxBytes)
            {
                if (more)
                    throw new OverflowException("SLEB128 value exceeds 64 bits (continuation bit set on the tenth byte).");
                // 9 groups fill 63 bits (bits 0..62). Only bit 63 is representable on the tenth byte;
                // the remaining payload bits must be a valid sign extension of that single bit:
                // 0x00 for a non-negative value or 0x7F for a negative value.
                int signBit = payload & 0x01;
                int expected = signBit == 0 ? 0x00 : 0x7F;
                if (payload != expected)
                    throw new OverflowException("SLEB128 value exceeds 64 bits (invalid sign extension on the tenth byte).");
            }
            else if (byteCount > Leb128MaxBytes)
            {
                throw new OverflowException("SLEB128 value exceeds 64 bits.");
            }

            result |= (long)payload << shift;
            shift += 7;

            if (!more)
            {
                // Apply sign extension for the terminating group when it is representable.
                if (shift < 64 && (raw & 0x40) != 0)
                    result |= -(1L << shift);

                // Reject overlong encodings: a redundant final group that only repeats the sign already
                // established by the previous group's high (0x40) bit. The tenth byte is never redundant
                // because it carries the otherwise unrepresentable 64th bit.
                if (byteCount > 1 && byteCount < Leb128MaxBytes)
                {
                    bool previousSignSet = (previousPayload & 0x40) != 0;
                    if (payload == 0x00 && !previousSignSet)
                        throw new FormatException("Non-canonical (overlong) SLEB128 encoding of a non-negative value.");
                    if (payload == 0x7F && previousSignSet)
                        throw new FormatException("Non-canonical (overlong) SLEB128 encoding of a negative value.");
                }

                return result;
            }

            previousPayload = payload;
        }
    }

    /// <summary>
    /// Writes an unsigned LEB128-encoded integer to the writer.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The value to encode.</param>
    public static void WriteULEB128(this IWriter writer, ulong value)
    {
        do
        {
            byte b = (byte)(value & 0x7F);
            value >>= 7;
            if (value != 0) b |= 0x80;
            writer.WriteByte(b);
        }
        while (value != 0);
    }

    /// <summary>
    /// Writes a signed LEB128-encoded integer to the writer.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The value to encode.</param>
    public static void WriteSLEB128(this IWriter writer, long value)
    {
        bool more;
        do
        {
            byte b = (byte)(value & 0x7F);
            value >>= 7;
            more = !((value == 0 && (b & 0x40) == 0) || (value == -1 && (b & 0x40) != 0));
            if (more) b |= 0x80;
            writer.WriteByte(b);
        }
        while (more);
    }

    /// <summary>
    /// Writes a length-prefixed string to the writer using the writer's configured endianness.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">String value to write.</param>
    /// <param name="encoding">Encoding of the string.</param>
    /// <param name="sizeLength">Number of bytes used to store the length prefix (1, 2, or 4).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="sizeLength"/> is not 1, 2, or 4, or when the encoded byte length does not
    /// fit in the selected prefix width. No bytes are written when this exception is thrown.
    /// </exception>
    public static void WriteVariableLengthString(this Writer writer, string value, Encoding encoding, int sizeLength = sizeof(int))
    {
        // Validate the prefix width before touching the writer so an invalid call performs no I/O.
        ValidateSizeLength(sizeLength);

        // Encode first and verify the length fits the prefix so the failure happens before any partial write.
        var bytes = encoding.GetBytes(value);
        long max = sizeLength switch
        {
            1 => byte.MaxValue,
            2 => ushort.MaxValue,
            _ => int.MaxValue
        };
        if (bytes.Length > max)
            throw new ArgumentOutOfRangeException(nameof(value), $"The encoded string is {bytes.Length} bytes, which does not fit in a {sizeLength}-byte length prefix (maximum {max}).");

        switch (sizeLength)
        {
            case 1:
                writer.Write<Byte>((byte)bytes.Length);
                break;
            case 2:
                writer.Write<UInt16>((ushort)bytes.Length);
                break;
            default:
                writer.Write<Int32>(bytes.Length);
                break;
        }
        writer.WriteBytes(bytes);
    }
}
