using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Utils.Objects;

/// <summary>
/// Provides methods for converting between <see cref="decimal"/>, <see cref="Int128"/>, <see cref="UInt128"/> 
/// and their corresponding byte representations. 
/// This class supplements <see cref="BitConverter"/> with conversions not natively supported.
/// </summary>
public static class BitConverterEx
{
    /// <summary>
    /// Converts a 16-byte <see cref="ReadOnlySpan{T}"/> of bytes into a <see cref="decimal"/>.
    /// </summary>
    /// <param name="bytes">
    /// A read-only span of at least 16 bytes that contains the binary representation 
    /// of a <see cref="decimal"/> value.
    /// </param>
    /// <returns>
    /// A <see cref="decimal"/> instance whose binary representation is taken from 
    /// the first 16 bytes of <paramref name="bytes"/>.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="bytes"/> does not contain exactly 16 bytes.
    /// </exception>
    public static decimal ToDecimal(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 16)
            throw new ArgumentException("A decimal must be created from exactly 16 bytes.", nameof(bytes));

        // Convert each group of 4 bytes to an int for the decimal constructor.
        int lo = bytes[0]
               | (bytes[1] << 8)
               | (bytes[2] << 16)
               | (bytes[3] << 24);

        int mid = bytes[4]
                | (bytes[5] << 8)
                | (bytes[6] << 16)
                | (bytes[7] << 24);

        int hi = bytes[8]
               | (bytes[9] << 8)
               | (bytes[10] << 16)
               | (bytes[11] << 24);

        int flags = bytes[12]
                  | (bytes[13] << 8)
                  | (bytes[14] << 16)
                  | (bytes[15] << 24);

        return new decimal(new[] { lo, mid, hi, flags });
    }

    /// <summary>
    /// Converts the specified <see cref="decimal"/> value into its 16-byte binary representation.
    /// </summary>
    /// <param name="d">The <see cref="decimal"/> value to convert.</param>
    /// <returns>A 16-byte array representing the specified <see cref="decimal"/>.</returns>
    public static byte[] GetBytes(decimal d)
    {
        byte[] bytes = new byte[16];
        int[] bits = decimal.GetBits(d);

        int lo = bits[0];
        int mid = bits[1];
        int hi = bits[2];
        int flags = bits[3];

        bytes[0] = (byte)lo;
        bytes[1] = (byte)(lo >> 8);
        bytes[2] = (byte)(lo >> 16);
        bytes[3] = (byte)(lo >> 24);
        bytes[4] = (byte)mid;
        bytes[5] = (byte)(mid >> 8);
        bytes[6] = (byte)(mid >> 16);
        bytes[7] = (byte)(mid >> 24);
        bytes[8] = (byte)hi;
        bytes[9] = (byte)(hi >> 8);
        bytes[10] = (byte)(hi >> 16);
        bytes[11] = (byte)(hi >> 24);
        bytes[12] = (byte)flags;
        bytes[13] = (byte)(flags >> 8);
        bytes[14] = (byte)(flags >> 16);
        bytes[15] = (byte)(flags >> 24);

        return bytes;
    }

    /// <summary>
    /// Converts an <see cref="Int128"/> value into its 16-byte binary representation.
    /// </summary>
    /// <param name="i">The <see cref="Int128"/> value to convert.</param>
    /// <returns>A 16-byte array representing the <see cref="Int128"/> value.</returns>
    public static unsafe byte[] GetBytes(Int128 i)
    {
        byte[] result = new byte[16];
        fixed (byte* ptr = result)
        {
            *(Int128*)ptr = i;
        }
        return result;
    }

    /// <summary>
    /// Converts a 16-byte <see cref="ReadOnlySpan{T}"/> of bytes into an <see cref="Int128"/>.
    /// </summary>
    /// <param name="bytes">
    /// A read-only span of exactly 16 bytes that contains the binary representation 
    /// of an <see cref="Int128"/> value.
    /// </param>
    /// <returns>An <see cref="Int128"/> parsed from the specified span.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="bytes"/> does not contain exactly 16 bytes.
    /// </exception>
    public static unsafe Int128 ToInt128(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 16)
            throw new ArgumentException("An Int128 must be created from exactly 16 bytes.", nameof(bytes));

        // Get the first byte reference, then convert to pointer.
        ref byte firstByte = ref MemoryMarshal.GetReference(bytes);
        return *(Int128*)Unsafe.AsPointer(ref firstByte);
    }

    /// <summary>
    /// Converts a <see cref="UInt128"/> value into its 16-byte binary representation.
    /// </summary>
    /// <param name="i">The <see cref="UInt128"/> value to convert.</param>
    /// <returns>A 16-byte array representing the <see cref="UInt128"/> value.</returns>
    public static unsafe byte[] GetBytes(UInt128 i)
    {
        byte[] result = new byte[16];
        fixed (byte* ptr = result)
        {
            *(UInt128*)ptr = i;
        }
        return result;
    }

    /// <summary>
    /// Converts a 16-byte <see cref="ReadOnlySpan{T}"/> of bytes into a <see cref="UInt128"/>.
    /// </summary>
    /// <param name="bytes">
    /// A read-only span of exactly 16 bytes that contains the binary representation 
    /// of a <see cref="UInt128"/> value.
    /// </param>
    /// <returns>A <see cref="UInt128"/> parsed from the specified span.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="bytes"/> does not contain exactly 16 bytes.
    /// </exception>
    public static unsafe UInt128 ToUInt128(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 16)
            throw new ArgumentException("A UInt128 must be created from exactly 16 bytes.", nameof(bytes));

        // Get the first byte reference, then convert to pointer.
        ref byte firstByte = ref MemoryMarshal.GetReference(bytes);
        return *(UInt128*)Unsafe.AsPointer(ref firstByte);
    }
}
