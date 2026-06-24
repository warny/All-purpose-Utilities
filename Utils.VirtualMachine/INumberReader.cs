using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Utils.VirtualMachine;

/// <summary>
/// Provides methods to read numbers from a virtual machine <see cref="Context"/>.
/// </summary>
public interface INumberReader
{
    /// <summary>
    /// Reads a single byte from the supplied <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The execution context containing the instruction stream.</param>
    /// <returns>The next byte available in the context.</returns>
    byte ReadByte(Context context);

    /// <summary>
    /// Reads a single signed byte from the supplied <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The execution context containing the instruction stream.</param>
    /// <returns>The next <see cref="sbyte"/> available in the context.</returns>
    sbyte ReadSByte(Context context);

    /// <summary>
    /// Reads a 16-bit signed integer from the supplied <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The execution context containing the instruction stream.</param>
    /// <returns>The next <see cref="short"/> value available in the context.</returns>
    short ReadInt16(Context context);

    /// <summary>
    /// Reads a 32-bit signed integer from the supplied <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The execution context containing the instruction stream.</param>
    /// <returns>The next <see cref="int"/> value available in the context.</returns>
    int ReadInt32(Context context);

    /// <summary>
    /// Reads a 64-bit signed integer from the supplied <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The execution context containing the instruction stream.</param>
    /// <returns>The next <see cref="long"/> value available in the context.</returns>
    long ReadInt64(Context context);

    /// <summary>
    /// Reads a 16-bit unsigned integer from the supplied <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The execution context containing the instruction stream.</param>
    /// <returns>The next <see cref="ushort"/> value available in the context.</returns>
    ushort ReadUInt16(Context context);

    /// <summary>
    /// Reads a 32-bit unsigned integer from the supplied <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The execution context containing the instruction stream.</param>
    /// <returns>The next <see cref="uint"/> value available in the context.</returns>
    uint ReadUInt32(Context context);

    /// <summary>
    /// Reads a 64-bit unsigned integer from the supplied <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The execution context containing the instruction stream.</param>
    /// <returns>The next <see cref="ulong"/> value available in the context.</returns>
    ulong ReadUInt64(Context context);

    /// <summary>
    /// Reads a single-precision floating-point value from the supplied <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The execution context containing the instruction stream.</param>
    /// <returns>The next <see cref="float"/> value available in the context.</returns>
    float ReadSingle(Context context);

    /// <summary>
    /// Reads a double-precision floating-point value from the supplied <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The execution context containing the instruction stream.</param>
    /// <returns>The next <see cref="double"/> value available in the context.</returns>
    double ReadDouble(Context context);

    /// <summary>
    /// Reads an unsigned LEB128-encoded integer from the supplied <paramref name="context"/>.
    /// LEB128 is byte-order independent; this default implementation delegates to <see cref="ReadByte"/>.
    /// </summary>
    /// <param name="context">The execution context containing the instruction stream.</param>
    /// <returns>The unsigned integer decoded from the LEB128 byte sequence.</returns>
    ulong ReadULEB128(Context context)
    {
        ulong result = 0;
        int shift = 0;
        byte b;
        do
        {
            b = ReadByte(context);
            result |= (ulong)(b & 0x7F) << shift;
            shift += 7;
        }
        while ((b & 0x80) != 0);
        return result;
    }

    /// <summary>
    /// Reads a signed LEB128-encoded integer from the supplied <paramref name="context"/>.
    /// LEB128 is byte-order independent; this default implementation delegates to <see cref="ReadByte"/>.
    /// </summary>
    /// <param name="context">The execution context containing the instruction stream.</param>
    /// <returns>The signed integer decoded from the LEB128 byte sequence.</returns>
    long ReadSLEB128(Context context)
    {
        long result = 0;
        int shift = 0;
        byte b;
        do
        {
            b = ReadByte(context);
            result |= (long)(b & 0x7F) << shift;
            shift += 7;
        }
        while ((b & 0x80) != 0);
        if (shift < 64 && (b & 0x40) != 0)
            result |= -(1L << shift);
        return result;
    }
}

/// <summary>
/// Factory methods for obtaining <see cref="INumberReader"/> instances.
/// </summary>
public static class NumberReader
{
    private static INumberReader NormalReader { get; } = new NormalReader();
    private static INumberReader InvertedReader { get; } = new InvertedReader();

    /// <summary>
    /// Returns a reader configured for the specified endianness.
    /// </summary>
    /// <param name="littleEndian">True for little-endian reading; otherwise, false.</param>
    /// <returns>An <see cref="INumberReader"/> that reads values using the desired byte order.</returns>
    public static INumberReader GetReader(bool littleEndian)
    {
        // NormalReader when requested endianness matches the system's endianness (no byte-swap needed).
        return littleEndian == BitConverter.IsLittleEndian ? NormalReader : InvertedReader;
    }
}

/// <summary>
/// Reader implementation matching the system endianness.
/// Uses <see cref="MemoryMarshal.Read{T}"/> to read in native byte order without intermediate copies.
/// </summary>
internal class NormalReader : INumberReader
{
    /// <inheritdoc />
    public byte ReadByte(Context context)
        => context.Data.Span[context.InstructionPointer++];

    /// <inheritdoc />
    public sbyte ReadSByte(Context context)
        => (sbyte)context.Data.Span[context.InstructionPointer++];

    /// <inheritdoc />
    public short ReadInt16(Context context)
    {
        var result = MemoryMarshal.Read<short>(context.Data.Span[context.InstructionPointer..]);
        context.InstructionPointer += sizeof(short);
        return result;
    }

    /// <inheritdoc />
    public int ReadInt32(Context context)
    {
        var result = MemoryMarshal.Read<int>(context.Data.Span[context.InstructionPointer..]);
        context.InstructionPointer += sizeof(int);
        return result;
    }

    /// <inheritdoc />
    public long ReadInt64(Context context)
    {
        var result = MemoryMarshal.Read<long>(context.Data.Span[context.InstructionPointer..]);
        context.InstructionPointer += sizeof(long);
        return result;
    }

    /// <inheritdoc />
    public ushort ReadUInt16(Context context)
    {
        var result = MemoryMarshal.Read<ushort>(context.Data.Span[context.InstructionPointer..]);
        context.InstructionPointer += sizeof(ushort);
        return result;
    }

    /// <inheritdoc />
    public uint ReadUInt32(Context context)
    {
        var result = MemoryMarshal.Read<uint>(context.Data.Span[context.InstructionPointer..]);
        context.InstructionPointer += sizeof(uint);
        return result;
    }

    /// <inheritdoc />
    public ulong ReadUInt64(Context context)
    {
        var result = MemoryMarshal.Read<ulong>(context.Data.Span[context.InstructionPointer..]);
        context.InstructionPointer += sizeof(ulong);
        return result;
    }

    /// <inheritdoc />
    public float ReadSingle(Context context)
    {
        var result = MemoryMarshal.Read<float>(context.Data.Span[context.InstructionPointer..]);
        context.InstructionPointer += sizeof(float);
        return result;
    }

    /// <inheritdoc />
    public double ReadDouble(Context context)
    {
        var result = MemoryMarshal.Read<double>(context.Data.Span[context.InstructionPointer..]);
        context.InstructionPointer += sizeof(double);
        return result;
    }
}

/// <summary>
/// Reader implementation that swaps endianness when reading values.
/// Uses <see cref="MemoryMarshal.Read{T}"/> to obtain the native-endian value, then
/// <see cref="BinaryPrimitives.ReverseEndianness"/> to invert byte order without heap allocation.
/// </summary>
internal class InvertedReader : INumberReader
{
    /// <inheritdoc />
    public byte ReadByte(Context context) => context.Data.Span[context.InstructionPointer++];

    /// <inheritdoc />
    public sbyte ReadSByte(Context context) => (sbyte)context.Data.Span[context.InstructionPointer++];

    /// <inheritdoc />
    public short ReadInt16(Context context)
    {
        var result = MemoryMarshal.Read<short>(context.Data.Span[context.InstructionPointer..]);
        context.InstructionPointer += sizeof(short);
        return BinaryPrimitives.ReverseEndianness(result);
    }

    /// <inheritdoc />
    public int ReadInt32(Context context)
    {
        var result = MemoryMarshal.Read<int>(context.Data.Span[context.InstructionPointer..]);
        context.InstructionPointer += sizeof(int);
        return BinaryPrimitives.ReverseEndianness(result);
    }

    /// <inheritdoc />
    public long ReadInt64(Context context)
    {
        var result = MemoryMarshal.Read<long>(context.Data.Span[context.InstructionPointer..]);
        context.InstructionPointer += sizeof(long);
        return BinaryPrimitives.ReverseEndianness(result);
    }

    /// <inheritdoc />
    public ushort ReadUInt16(Context context)
    {
        var result = MemoryMarshal.Read<ushort>(context.Data.Span[context.InstructionPointer..]);
        context.InstructionPointer += sizeof(ushort);
        return BinaryPrimitives.ReverseEndianness(result);
    }

    /// <inheritdoc />
    public uint ReadUInt32(Context context)
    {
        var result = MemoryMarshal.Read<uint>(context.Data.Span[context.InstructionPointer..]);
        context.InstructionPointer += sizeof(uint);
        return BinaryPrimitives.ReverseEndianness(result);
    }

    /// <inheritdoc />
    public ulong ReadUInt64(Context context)
    {
        var result = MemoryMarshal.Read<ulong>(context.Data.Span[context.InstructionPointer..]);
        context.InstructionPointer += sizeof(ulong);
        return BinaryPrimitives.ReverseEndianness(result);
    }

    /// <inheritdoc />
    public float ReadSingle(Context context)
    {
        var bits = MemoryMarshal.Read<uint>(context.Data.Span[context.InstructionPointer..]);
        context.InstructionPointer += sizeof(float);
        return BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReverseEndianness(bits));
    }

    /// <inheritdoc />
    public double ReadDouble(Context context)
    {
        var bits = MemoryMarshal.Read<ulong>(context.Data.Span[context.InstructionPointer..]);
        context.InstructionPointer += sizeof(double);
        return BitConverter.UInt64BitsToDouble(BinaryPrimitives.ReverseEndianness(bits));
    }
}
