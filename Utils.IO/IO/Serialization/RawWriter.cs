using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Utils.Objects;

namespace Utils.IO.Serialization;

/// <summary>
/// Provides basic writers for primitive and common framework types.
/// Each method writes a value using the supplied <see cref="IWriter"/>.
/// </summary>
public class RawWriter
{
    /// <summary>
    /// Gets the delegates used to write built-in types.
    /// </summary>
    public IEnumerable<Delegate> WriterDelegates =>
    [
        WriteBytesEnumeration,
        WriteBytes,
        WriteByte, WriteSByte,
        CreateWriteNumberDelegate<short>(), CreateWriteNumberDelegate<ushort>(),
        CreateWriteNumberDelegate<int>(), CreateWriteNumberDelegate<uint>(),
        CreateWriteNumberDelegate<long>(), CreateWriteNumberDelegate<ulong>(),
        WriteSingle, WriteDouble, WriteDecimal, WriteHalf,
        WriteBigInteger, WriteUInt128, WriteInt128, WriteComplex,
        WriteDateTime, WriteDate, WriteTime, WriteTimeSpan,
        WriteString,
        WriteGuid, WriteBool
    ];

    /// <summary>
    /// Gets or sets the encoding used to write strings.
    /// </summary>
    public Encoding Encoding { get; init; } = Encoding.UTF8;

    /// <summary>
    /// Gets or sets a value indicating whether numbers are written in big-endian format.
    /// </summary>
    public bool BigEndian { get; init; } = false;

    /// <summary>
    /// Writes a sequence of bytes preceded by its length.
    /// </summary>
    /// <param name="writer">Destination writer.</param>
    /// <param name="value">Bytes to write.</param>
    public void WriteBytes(IWriter writer, ReadOnlySpan<byte> value)
    {
        WriteInt(writer, value.Length);
        writer.WriteBytes(value);
    }

    /// <summary>
    /// Creates a delegate able to write a number of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Numeric type to write.</typeparam>
    /// <returns>A delegate that writes the specified numeric type.</returns>
    private Delegate CreateWriteNumberDelegate<T>()
        where T : IBinaryInteger<T>
    {
        unchecked
        {
            var size = Marshal.SizeOf(typeof(T));
            var isUnsigned = T.Sign(T.Zero - T.One) == 1;

            Action<IWriter, T> d = BigEndian
                ? (IWriter writer, T number) =>
                {
                    Span<byte> bytes = stackalloc byte[Marshal.SizeOf<T>()];
                    number.WriteBigEndian(bytes);
                    writer.WriteBytes(bytes);
                }
            : (IWriter writer, T number) =>
            {
                Span<byte> bytes = stackalloc byte[Marshal.SizeOf<T>()];
                number.WriteLittleEndian(bytes);
                writer.WriteBytes(bytes);
            };

            return d;
        }
    }

    /// <summary>
    /// Writes a numeric value to the stream.
    /// </summary>
    /// <typeparam name="T">Numeric type to write.</typeparam>
    /// <param name="writer">Destination writer.</param>
    /// <param name="number">Value to write.</param>
    public void WriteNumber<T>(IWriter writer, T number) where T : struct, IBinaryInteger<T>
    {
        Span<byte> bytes = stackalloc byte[Marshal.SizeOf<T>()];

        if (BigEndian)
        {
            number.WriteBigEndian(bytes);
        }
        else
        {
            number.WriteLittleEndian(bytes);
        }
        writer.WriteBytes(bytes);
    }

    /// <summary>Writes a single byte.</summary>
    public void WriteByte(IWriter writer, byte value) => writer.WriteByte(value);

    /// <summary>Writes a signed byte.</summary>
    public void WriteSByte(IWriter writer, sbyte value) => writer.WriteByte((byte)value);

    /// <summary>Writes each byte of the enumeration individually.</summary>
    public void WriteBytesEnumeration(IWriter writer, IEnumerable<byte> value)
    {
        foreach (var b in value) writer.WriteByte(b);
    }

    /// <summary>
    /// Writes bytes adjusting for endianness.
    /// </summary>
    /// <param name="writer">Destination writer.</param>
    /// <param name="bytes">Bytes to write.</param>
    private void WriteNumberBytes(IWriter writer, byte[] bytes)
    {
        if (BitConverter.IsLittleEndian ^ BigEndian) bytes.Reverse();
		Span<byte> data = bytes;
        writer.WriteBytes(data);
    }

    /// <summary>Writes a 16-bit signed integer.</summary>
    public void WriteShort(IWriter writer, short value) => WriteNumber(writer, value);

    /// <summary>Writes a 16-bit unsigned integer.</summary>
    public void WriteUShort(IWriter writer, ushort value) => WriteNumber(writer, value);

    /// <summary>Writes a 32-bit signed integer.</summary>
    public void WriteInt(IWriter writer, int value) => WriteNumber(writer, value);

    /// <summary>Writes a 32-bit unsigned integer.</summary>
    public void WriteUInt(IWriter writer, uint value) => WriteNumber(writer, value);

    /// <summary>Writes a 64-bit signed integer.</summary>
    public void WriteLong(IWriter writer, long value) => WriteNumber(writer, value);

    /// <summary>Writes a 64-bit unsigned integer.</summary>
    public void WriteULong(IWriter writer, ulong value) => WriteNumber(writer, value);

    /// <summary>Writes a single-precision floating point number.</summary>
    public void WriteSingle(IWriter writer, float value) => WriteNumberBytes(writer, BitConverter.GetBytes(value));

    /// <summary>Writes a double-precision floating point number.</summary>
    public void WriteDouble(IWriter writer, double value) => WriteNumberBytes(writer, BitConverter.GetBytes(value));

    /// <summary>Writes a decimal number.</summary>
    public void WriteDecimal(IWriter writer, decimal value) => WriteNumberBytes(writer, BitConverterEx.GetBytes(value));

    /// <summary>Writes a half-precision floating point number.</summary>
    public void WriteHalf(IWriter writer, Half value) => WriteNumberBytes(writer, BitConverter.GetBytes(value));

    /// <summary>Writes a <see cref="BigInteger"/>.</summary>
    public void WriteBigInteger(IWriter writer, BigInteger value)
    {
        var bytes = value.ToByteArray();
        WriteInt(writer, bytes.Length);
        writer.WriteBytes(bytes);
    }

    /// <summary>Writes a signed 128-bit integer.</summary>
    public void WriteInt128(IWriter writer, Int128 value) => writer.WriteBytes(BitConverterEx.GetBytes(value));

    /// <summary>Writes an unsigned 128-bit integer.</summary>
    public void WriteUInt128(IWriter writer, UInt128 value) => writer.WriteBytes(BitConverterEx.GetBytes(value));

    /// <summary>Writes a complex number.</summary>
    public void WriteComplex(IWriter writer, Complex value)
    {
        WriteDouble(writer, value.Real);
        WriteDouble(writer, value.Imaginary);
    }

    /// <summary>Writes a string prefixed with its length.</summary>
    public void WriteString(IWriter writer, string value)
    {
        var data = Encoding.GetBytes(value);
        writer.Write(data.Length);
        writer.WriteBytes(data);
    }

    /// <summary>Writes a single character.</summary>
    public void WriteChar(IWriter writer, char value)
    {
        var data = Encoding.GetBytes(new[] { value });
        WriteByte(writer, (byte)data.Length);
        writer.WriteBytes(data);
    }

    /// <summary>Writes a <see cref="DateTime"/> value.</summary>
    public void WriteDateTime(IWriter writer, DateTime value) => WriteLong(writer, value.Ticks);

    /// <summary>Writes a <see cref="TimeOnly"/> value.</summary>
    public void WriteTime(IWriter writer, TimeOnly value) => WriteLong(writer, value.Ticks);

    /// <summary>Writes a <see cref="DateOnly"/> value.</summary>
    public void WriteDate(IWriter writer, DateOnly value) => WriteInt(writer, (int)(value.ToDateTime(TimeOnly.MinValue) - DateTime.MinValue).TotalDays);

    /// <summary>Writes a <see cref="TimeSpan"/> value.</summary>
    public void WriteTimeSpan(IWriter writer, TimeSpan value) => WriteDouble(writer, value.TotalMicroseconds);

    /// <summary>Writes a <see cref="Guid"/>.</summary>
    public void WriteGuid(IWriter writer, Guid value) => writer.WriteBytes(value.ToByteArray());

    /// <summary>Writes a boolean value.</summary>
    public void WriteBool(IWriter writer, bool value) => WriteByte(writer, value ? (byte)1 : (byte)0);
}
