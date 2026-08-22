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

    /// <summary>Gets or sets the maximum accepted payload length for length-prefixed values.</summary>
    public int MaximumLength { get; init; } = int.MaxValue;

    // Integer reading methods
    /// <summary>Reads a byte from the reader.</summary>
    public byte ReadByte(IReader reader) => ReadRequiredByte(reader, nameof(Byte));

    /// <summary>Reads a signed byte from the reader.</summary>
    public sbyte ReadSByte(IReader reader) => unchecked((sbyte)ReadRequiredByte(reader, nameof(SByte)));

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
                ? (IReader reader) => T.ReadBigEndian(ReadExactly(reader, size, typeof(T).Name), isUnsigned)
                : (IReader reader) => T.ReadLittleEndian(ReadExactly(reader, size, typeof(T).Name), isUnsigned);
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
        byte[] bytes = ReadExactly(reader, size, typeof(T).Name);
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
        byte[] bytes = ReadExactly(reader, length, "numeric value");
        if (BitConverter.IsLittleEndian ^ BigEndian) bytes.Reverse();
        return bytes;
    }

    // Extended number reading methods
    /// <summary>
    /// Reads a <see cref="BigInteger"/> from a 32-bit byte-length prefix followed by a signed
    /// two's-complement payload whose byte order follows <see cref="BigEndian"/>.
    /// </summary>
    public BigInteger ReadBigInteger(IReader reader)
    {
        int length = ReadInt(reader);
        ValidateLength(length, nameof(BigInteger));
        byte[] bytes = ReadExactly(reader, length, nameof(BigInteger));
        return new BigInteger(bytes, isUnsigned: false, isBigEndian: BigEndian);
    }

    /// <summary>Reads a signed 128-bit integer whose fixed 16-byte representation follows <see cref="BigEndian"/>.</summary>
    public Int128 ReadInt128(IReader reader) => ReadNumber<Int128>(reader, false);

    /// <summary>Reads an unsigned 128-bit integer whose fixed 16-byte representation follows <see cref="BigEndian"/>.</summary>
    public UInt128 ReadUInt128(IReader reader) => ReadNumber<UInt128>(reader, true);

    /// <summary>Reads a complex number.</summary>
    public Complex ReadComplex(IReader reader)
    {
        double real = ReadDouble(reader);
        double imaginary = ReadDouble(reader);
        return new Complex(real, imaginary);
    }

    // Date and time reading methods
    /// <summary>Reads a <see cref="DateTime"/> value.</summary>
    public DateTime ReadDateTime(IReader reader) => DateTime.FromBinary(ReadLong(reader));

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
        ValidateLength(length, nameof(String));
        byte[] bytes = ReadExactly(reader, length, nameof(String));
        return Encoding.GetString(bytes);
    }

    /// <summary>Reads a single character as a 2-byte UTF-16 code unit, respecting <see cref="BigEndian"/>.</summary>
    public char ReadChar(IReader reader)
    {
        byte[] bytes = ReadExactly(reader, sizeof(char), nameof(Char));
        return BigEndian
            ? (char)((bytes[0] << 8) | bytes[1])
            : (char)(bytes[0] | (bytes[1] << 8));
    }

    // Miscellaneous reading methods
    /// <summary>
    /// Reads a <see cref="Guid"/> in canonical RFC/network byte layout, independently of the numeric
    /// <see cref="BigEndian"/> option.
    /// </summary>
    public Guid ReadGuid(IReader reader) => new Guid(ReadExactly(reader, 16, nameof(Guid)), bigEndian: true);

    /// <summary>Reads a boolean value.</summary>
    public bool ReadBool(IReader reader) => ReadByte(reader) == 1;

    /// <summary>Reads one required byte and rejects EOF rather than converting it to a value.</summary>
    private static byte ReadRequiredByte(IReader reader, string valueType)
    {
        int value = reader.ReadByte();
        if (value < 0) throw CreateEndOfStream(valueType, 1, 0);
        return (byte)value;
    }

    /// <summary>Reads exactly the requested binary payload, including across partial stream reads.</summary>
    private static byte[] ReadExactly(IReader reader, int length, string valueType)
    {
        byte[] result = new byte[length];
        int received = 0;
        while (received < length)
        {
            byte[] part = reader.ReadBytes(length - received);
            if (part.Length == 0) throw CreateEndOfStream(valueType, length, received);
            int copied = Math.Min(part.Length, length - received);
            part.AsSpan(0, copied).CopyTo(result.AsSpan(received));
            received += copied;
        }
        return result;
    }

    /// <summary>Creates the common exact-read failure used by every primitive converter.</summary>
    private static EndOfStreamException CreateEndOfStream(string valueType, int expected, int received) =>
        new($"Unexpected end of stream while reading {valueType}: expected {expected} bytes, received {received}.");

    /// <summary>Rejects invalid or unreasonably large length-prefixed payload declarations.</summary>
    private void ValidateLength(int length, string valueType)
    {
        if (length < 0 || length > MaximumLength)
            throw new InvalidDataException($"Invalid {valueType} payload length {length}; allowed range is 0..{MaximumLength}.");
    }
}
