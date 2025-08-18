using System;
using System.Text;

namespace Utils.IO.Serialization;

/// <summary>
/// Extension methods providing convenience helpers for <see cref="NewReader"/> and <see cref="NewWriter"/>.
/// </summary>
public static class NewReaderWriterExtensions
{
        private static readonly RawReader LittleEndianReader = new() { BigIndian = false };
        private static readonly RawReader BigEndianReader = new() { BigIndian = true };
        private static readonly RawWriter LittleEndianWriter = new() { BigIndian = false };
        private static readonly RawWriter BigEndianWriter = new() { BigIndian = true };

        /// <summary>
        /// Reads a 16-bit signed integer from the reader using the specified endianness.
        /// </summary>
        /// <param name="reader">The reader to read from.</param>
        /// <param name="bigEndian">Whether to read the value in big-endian order.</param>
        public static short ReadInt16(this NewReader reader, bool bigEndian = false)
                => (bigEndian ? BigEndianReader : LittleEndianReader).ReadShort(reader);

        /// <summary>
        /// Reads a 32-bit signed integer from the reader using the specified endianness.
        /// </summary>
        /// <param name="reader">The reader to read from.</param>
        /// <param name="bigEndian">Whether to read the value in big-endian order.</param>
        public static int ReadInt32(this NewReader reader, bool bigEndian = false)
                => (bigEndian ? BigEndianReader : LittleEndianReader).ReadInt(reader);

        /// <summary>
        /// Reads a 16-bit unsigned integer from the reader using the specified endianness.
        /// </summary>
        /// <param name="reader">The reader to read from.</param>
        /// <param name="bigEndian">Whether to read the value in big-endian order.</param>
        public static ushort ReadUInt16(this NewReader reader, bool bigEndian = false)
                => (bigEndian ? BigEndianReader : LittleEndianReader).ReadUShort(reader);

        /// <summary>
        /// Reads a 32-bit unsigned integer from the reader using the specified endianness.
        /// </summary>
        /// <param name="reader">The reader to read from.</param>
        /// <param name="bigEndian">Whether to read the value in big-endian order.</param>
        public static uint ReadUInt32(this NewReader reader, bool bigEndian = false)
                => (bigEndian ? BigEndianReader : LittleEndianReader).ReadUInt(reader);

        /// <summary>
        /// Reads a 64-bit signed integer from the reader using the specified endianness.
        /// </summary>
        /// <param name="reader">The reader to read from.</param>
        /// <param name="bigEndian">Whether to read the value in big-endian order.</param>
        public static long ReadInt64(this NewReader reader, bool bigEndian = false)
                => (bigEndian ? BigEndianReader : LittleEndianReader).ReadLong(reader);

        /// <summary>
        /// Reads a fixed-length string from the reader using the provided encoding.
        /// </summary>
        /// <param name="reader">The reader to read from.</param>
        /// <param name="length">Number of bytes to read.</param>
        /// <param name="encoding">Encoding of the string.</param>
        /// <returns>The decoded string with trailing null or space characters removed.</returns>
        public static string ReadFixedLengthString(this NewReader reader, int length, Encoding encoding)
        {
                var bytes = reader.ReadBytes(length);
                return encoding.GetString(bytes).TrimEnd('\0', ' ');
        }

        /// <summary>
        /// Reads a length-prefixed string from the reader.
        /// </summary>
        /// <param name="reader">The reader to read from.</param>
        /// <param name="encoding">Encoding of the string.</param>
        /// <param name="bigEndian">Whether the length is stored in big-endian order.</param>
        public static string ReadVariableLengthString(this NewReader reader, Encoding encoding, bool bigEndian = false, int sizeLength = sizeof(int))
        {
                int length = sizeLength switch
                {
                        1 => reader.ReadByte(),
                        2 => reader.ReadUInt16(bigEndian),
                        _ => reader.ReadInt32(bigEndian)
                };
                return encoding.GetString(reader.ReadBytes(length));
        }

        /// <summary>
        /// Reads an array of values from the reader.
        /// </summary>
        /// <typeparam name="T">Type of elements to read.</typeparam>
        /// <param name="reader">The reader to read from.</param>
        /// <param name="count">Number of elements to read.</param>
        /// <param name="bigEndian">Whether numeric values are stored in big-endian order.</param>
        public static T[] ReadArray<T>(this NewReader reader, int count, bool bigEndian = false)
        {
                var result = new T[count];
                for (int i = 0; i < count; i++)
                {
                        if (bigEndian)
                        {
                                result[i] = typeof(T) switch
                                {
                                        Type t when t == typeof(short) => (T)(object)reader.ReadInt16(true),
                                        Type t when t == typeof(ushort) => (T)(object)reader.ReadUInt16(true),
                                        Type t when t == typeof(int) => (T)(object)reader.ReadInt32(true),
                                        Type t when t == typeof(uint) => (T)(object)reader.ReadUInt32(true),
                                        Type t when t == typeof(long) => (T)(object)reader.ReadInt64(true),
                                        _ => reader.Read<T>()
                                };
                        }
                        else
                        {
                                result[i] = reader.Read<T>();
                        }
                }
                return result;
        }

        /// <summary>
        /// Writes a 16-bit signed integer using the specified endianness.
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="value">Value to write.</param>
        /// <param name="bigEndian">Whether to write the value in big-endian order.</param>
        public static void WriteInt16(this NewWriter writer, short value, bool bigEndian = false)
                => (bigEndian ? BigEndianWriter : LittleEndianWriter).WriteShort(writer, value);

        /// <summary>
        /// Writes a 32-bit signed integer using the specified endianness.
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="value">Value to write.</param>
        /// <param name="bigEndian">Whether to write the value in big-endian order.</param>
        public static void WriteInt32(this NewWriter writer, int value, bool bigEndian = false)
                => (bigEndian ? BigEndianWriter : LittleEndianWriter).WriteInt(writer, value);

        /// <summary>
        /// Writes a 16-bit unsigned integer using the specified endianness.
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="value">Value to write.</param>
        /// <param name="bigEndian">Whether to write the value in big-endian order.</param>
        public static void WriteUInt16(this NewWriter writer, ushort value, bool bigEndian = false)
                => (bigEndian ? BigEndianWriter : LittleEndianWriter).WriteUShort(writer, value);

        /// <summary>
        /// Writes a 32-bit unsigned integer using the specified endianness.
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="value">Value to write.</param>
        /// <param name="bigEndian">Whether to write the value in big-endian order.</param>
        public static void WriteUInt32(this NewWriter writer, uint value, bool bigEndian = false)
                => (bigEndian ? BigEndianWriter : LittleEndianWriter).WriteUInt(writer, value);

        /// <summary>
        /// Writes a 64-bit signed integer using the specified endianness.
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="value">Value to write.</param>
        /// <param name="bigEndian">Whether to write the value in big-endian order.</param>
        public static void WriteInt64(this NewWriter writer, long value, bool bigEndian = false)
                => (bigEndian ? BigEndianWriter : LittleEndianWriter).WriteLong(writer, value);

        /// <summary>
        /// Writes a fixed-length string using the provided encoding. The string is padded with zero bytes if necessary.
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="value">String value to write.</param>
        /// <param name="length">Desired length in bytes.</param>
        /// <param name="encoding">Encoding of the string.</param>
        public static void WriteFixedLengthString(this NewWriter writer, string value, int length, Encoding encoding)
        {
                var buffer = new byte[length];
                encoding.GetBytes(value, 0, value.Length, buffer, 0);
                writer.WriteBytes(buffer);
        }

        /// <summary>
        /// Writes a length-prefixed string to the writer.
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="value">String value to write.</param>
        /// <param name="encoding">Encoding of the string.</param>
        /// <param name="bigEndian">Whether the length is stored in big-endian order.</param>
        public static void WriteVariableLengthString(this NewWriter writer, string value, Encoding encoding, bool bigEndian = false, int sizeLength = sizeof(int))
        {
                var bytes = encoding.GetBytes(value);
                switch (sizeLength)
                {
                        case 1:
                                writer.WriteByte((byte)bytes.Length);
                                break;
                        case 2:
                                writer.WriteUInt16((ushort)bytes.Length, bigEndian);
                                break;
                        default:
                                writer.WriteInt32(bytes.Length, bigEndian);
                                break;
                }
                writer.WriteBytes(bytes);
        }

        /// <summary>
        /// Writes an array of values to the writer.
        /// </summary>
        /// <typeparam name="T">Type of elements to write.</typeparam>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="values">Array of values to write.</param>
        /// <param name="bigEndian">Whether numeric values are written in big-endian order.</param>
        public static void WriteArray<T>(this NewWriter writer, T[] values, bool bigEndian = false)
        {
                foreach (var v in values)
                {
                        switch (v)
                        {
                                case short s:
                                        writer.WriteInt16(s, bigEndian);
                                        break;
                                case ushort us:
                                        writer.WriteUInt16(us, bigEndian);
                                        break;
                                case int i:
                                        writer.WriteInt32(i, bigEndian);
                                        break;
                                case uint ui:
                                        writer.WriteUInt32(ui, bigEndian);
                                        break;
                                case long l:
                                        writer.WriteInt64(l, bigEndian);
                                        break;
                                default:
                                        writer.Write(v);
                                        break;
                        }
                }
        }

        /// <summary>
        /// Writes a <see cref="DateTime"/> value as ticks using the specified endianness.
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="value">Date to write.</param>
        /// <param name="bigEndian">Whether to write in big-endian order.</param>
        public static void WriteDateTime(this NewWriter writer, DateTime value, bool bigEndian = false)
                => writer.WriteInt64(value.Ticks, bigEndian);

        /// <summary>
        /// Reads a <see cref="DateTime"/> value from the reader.
        /// </summary>
        /// <param name="reader">The reader to read from.</param>
        /// <param name="bigEndian">Whether the value is stored in big-endian order.</param>
        public static DateTime ReadDateTime(this NewReader reader, bool bigEndian = false)
                => new DateTime(reader.ReadInt64(bigEndian));
}

