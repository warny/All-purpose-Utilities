using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Utils.Objects;

namespace Utils.IO.Serialization;

/// <summary>
/// Provides basic readers for primitive and common framework types.
/// Each method reads a value using the supplied <see cref="IReader"/>.
/// </summary>
public class RawReader
{
    /// <summary>
    /// Gets the delegates used to read built-in types.
    /// </summary>
    public IEnumerable<Delegate> ReaderDelegates =>
    [
        ReadByte, ReadSByte,
        CreateReadNumberDelegate<short>(), CreateReadNumberDelegate<ushort>(),
        CreateReadNumberDelegate<int>(), CreateReadNumberDelegate<uint>(),
        CreateReadNumberDelegate<long>(), CreateReadNumberDelegate<ulong>(),
        ReadSingle, ReadDouble, ReadDecimal, ReadHalf,
        ReadBigInteger, ReadInt128, ReadUInt128, ReadComplex,
        ReadDateTime, ReadDate, ReadTime, ReadTimeSpan,
        ReadString, ReadChar,
        ReadGuid, ReadBool
    ];

    /// <summary>
    /// Gets or sets the encoding used to read strings.
    /// </summary>
    public Encoding Encoding { get; init; } = Encoding.UTF8;

    /// <summary>
    /// Gets or sets a value indicating whether numbers are stored in big-endian format.
    /// </summary>
    public bool BigEndian { get; init; } = false;

    // Integer reading methods
    /// <summary>Reads a byte from the reader.</summary>
    public byte ReadByte(IReader reader) => (byte)reader.ReadByte();

    /// <summary>Reads a signed byte from the reader.</summary>
    public sbyte ReadSByte(IReader reader) => (sbyte)reader.ReadByte();

    /// <summary>Reads a 16-bit signed integer.</summary>
    public short ReadShort(IReader reader) => ReadNumber<short>(reader, false);

    /// <summary>Reads a 16-bit unsigned integer.</summary>
    public ushort ReadUShort(IReader reader) => ReadNumber<ushort>(reader, true);

    /// <summary>Reads a 32-bit signed integer.</summary>
    public int ReadInt(IReader reader) => ReadNumber<int>(reader, false);

    /// <summary>Reads a 32-bit unsigned integer.</summary>
    public uint ReadUInt(IReader reader) => ReadNumber<uint>(reader, true);

    /// <summary>Reads a 64-bit signed integer.</summary>
    public long ReadLong(IReader reader) => ReadNumber<long>(reader, false);

    /// <summary>Reads a 64-bit unsigned integer.</summary>
    public ulong ReadULong(IReader reader) => ReadNumber<ulong>(reader, true);

    // Floating point number reading methods
    /// <summary>Reads a single-precision floating point number.</summary>
    public float ReadSingle(IReader reader) => BitConverter.ToSingle(ReadNumberBytes(reader, sizeof(float)));

    /// <summary>Reads a double-precision floating point number.</summary>
    public double ReadDouble(IReader reader) => BitConverter.ToDouble(ReadNumberBytes(reader, sizeof(double)));

    /// <summary>Reads a decimal number.</summary>
    public decimal ReadDecimal(IReader reader) => BitConverterEx.ToDecimal(ReadNumberBytes(reader, sizeof(decimal)));

    /// <summary>Reads a half-precision floating point number.</summary>
    public Half ReadHalf(IReader reader) => BitConverter.ToHalf(ReadNumberBytes(reader, Marshal.SizeOf(typeof(Half))));

    /// <summary>
    /// Creates a delegate able to read a number of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Numeric type to read.</typeparam>
    /// <returns>A delegate that reads the specified numeric type.</returns>
    private Delegate CreateReadNumberDelegate<T>()
        where T : IBinaryInteger<T>
    {
        unchecked
        {
            var size = Marshal.SizeOf(typeof(T));
            var isUnsigned = T.Sign(T.Zero - T.One) == 1;

            Func<IReader, T> d = BigEndian
                ? (IReader reader) => T.ReadBigEndian(reader.ReadBytes(size), isUnsigned)
                : (IReader reader) => T.ReadLittleEndian(reader.ReadBytes(size), isUnsigned);
            return d;
        }
    }

    /// <summary>
    /// Reads a number of type <typeparamref name="T"/> from the reader.
    /// </summary>
    /// <typeparam name="T">Numeric type to read.</typeparam>
    /// <param name="reader">Source reader.</param>
    /// <param name="isUnsigned">Indicates whether the number is unsigned.</param>
    /// <returns>The value read from the stream.</returns>
    private T ReadNumber<T>(IReader reader, bool isUnsigned) where T : struct, IBinaryInteger<T>
    {
        int size = Marshal.SizeOf(typeof(T));
        byte[] bytes = reader.ReadBytes(size);
        T value = BigEndian
            ? T.ReadBigEndian(bytes, isUnsigned)
            : T.ReadLittleEndian(bytes, isUnsigned);
        return value;
    }

    /// <summary>
    /// Reads a sequence of bytes representing a numeric value.
    /// </summary>
    /// <param name="reader">Source reader.</param>
    /// <param name="length">Number of bytes to read.</param>
    /// <returns>The bytes representing the number.</returns>
    private byte[] ReadNumberBytes(IReader reader, int length)
    {
        byte[] bytes = reader.ReadBytes(length);
        if (BitConverter.IsLittleEndian ^ BigEndian) bytes.Reverse();
		return bytes;
    }

    // Extended number reading methods
    /// <summary>Reads a <see cref="BigInteger"/> value.</summary>
    public BigInteger ReadBigInteger(IReader reader)
    {
        int length = ReadInt(reader);
        byte[] bytes = reader.ReadBytes(length);
        return new BigInteger(bytes);
    }

    /// <summary>Reads a signed 128-bit integer.</summary>
    public Int128 ReadInt128(IReader reader) => BitConverterEx.ToInt128(reader.ReadBytes(16));

    /// <summary>Reads an unsigned 128-bit integer.</summary>
    public UInt128 ReadUInt128(IReader reader) => BitConverterEx.ToUInt128(reader.ReadBytes(16));

    /// <summary>Reads a complex number.</summary>
    public Complex ReadComplex(IReader reader)
    {
        double real = ReadDouble(reader);
        double imaginary = ReadDouble(reader);
        return new Complex(real, imaginary);
    }

    // Date and time reading methods
    /// <summary>Reads a <see cref="DateTime"/> value.</summary>
    public DateTime ReadDateTime(IReader reader) => new DateTime(ReadLong(reader));

    /// <summary>Reads a <see cref="TimeOnly"/> value.</summary>
    public TimeOnly ReadTime(IReader reader) => new TimeOnly(ReadLong(reader));

    /// <summary>Reads a <see cref="DateOnly"/> value.</summary>
    public DateOnly ReadDate(IReader reader) => DateOnly.FromDayNumber(ReadInt(reader));

    /// <summary>Reads a <see cref="TimeSpan"/> value.</summary>
    public TimeSpan ReadTimeSpan(IReader reader) => TimeSpan.FromTicks(ReadLong(reader));

    // String and character reading methods
    /// <summary>Reads a string prefixed with its length.</summary>
    public string ReadString(IReader reader)
    {
        int length = ReadInt(reader);
        byte[] bytes = reader.ReadBytes(length);
        return Encoding.GetString(bytes);
    }

    /// <summary>Reads a single character.</summary>
    public char ReadChar(IReader reader)
    {
        byte[] bytes = reader.ReadBytes(sizeof(char));
        return BitConverter.ToChar(bytes);
    }

    // Miscellaneous reading methods
    /// <summary>Reads a <see cref="Guid"/>.</summary>
    public Guid ReadGuid(IReader reader) => new Guid(reader.ReadBytes(16));

    /// <summary>Reads a boolean value.</summary>
    public bool ReadBool(IReader reader) => ReadByte(reader) == 1;
}
