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
    /// <param name="sizeLength">Number of bytes used to store the length.</param>
    public static string ReadVariableLengthString(this Reader reader, Encoding encoding, int sizeLength = sizeof(int))
    {
        int length = sizeLength switch
        {
            1 => reader.ReadByte(),
            2 => reader.Read<UInt16>(),
            _ => reader.Read<Int32>()
        };
        return encoding.GetString(reader.ReadBytes(length));
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
    /// Reads an unsigned LEB128-encoded integer from the reader.
    /// </summary>
    /// <param name="reader">The reader to read from.</param>
    /// <returns>The unsigned integer decoded from the LEB128 byte sequence.</returns>
    /// <exception cref="EndOfStreamException">Thrown when the stream ends before the value is complete.</exception>
    public static ulong ReadULEB128(this IReader reader)
    {
        ulong result = 0;
        int shift = 0;
        int raw;
        do
        {
            raw = reader.ReadByte();
            if (raw < 0) throw new EndOfStreamException("Unexpected end of stream reading ULEB128.");
            result |= (ulong)(raw & 0x7F) << shift;
            shift += 7;
        }
        while ((raw & 0x80) != 0);
        return result;
    }

    /// <summary>
    /// Reads a signed LEB128-encoded integer from the reader.
    /// </summary>
    /// <param name="reader">The reader to read from.</param>
    /// <returns>The signed integer decoded from the LEB128 byte sequence.</returns>
    /// <exception cref="EndOfStreamException">Thrown when the stream ends before the value is complete.</exception>
    public static long ReadSLEB128(this IReader reader)
    {
        long result = 0;
        int shift = 0;
        int raw;
        do
        {
            raw = reader.ReadByte();
            if (raw < 0) throw new EndOfStreamException("Unexpected end of stream reading SLEB128.");
            result |= (long)(raw & 0x7F) << shift;
            shift += 7;
        }
        while ((raw & 0x80) != 0);
        if (shift < 64 && (raw & 0x40) != 0)
            result |= -(1L << shift);
        return result;
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
    public static void WriteVariableLengthString(this Writer writer, string value, Encoding encoding, int sizeLength = sizeof(int))
    {
        var bytes = encoding.GetBytes(value);
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
