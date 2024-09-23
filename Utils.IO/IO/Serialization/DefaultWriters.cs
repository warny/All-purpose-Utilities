using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Utils.Arrays;
using Utils.Mathematics;
using Utils.Objects;

namespace Utils.IO.Serialization;


public class RawWriter
	: IBasicWriter, IIntegerNumberWriters, IFloatingNumberWriters, IExtendedNumberWriters, IDateWriters, IStringWriters, IMiscelaneousWriters
{
	public Encoding Encoding { get; init; } = Encoding.UTF8;
	public bool BigIndian { get; init; } = false;

	public IEnumerable<Delegate> WriterDelegates =>
	[
		WriteBytesEnumeration,
		WriteBytes,
		WriteByte, WriteSByte,
		WriteShort, WriteUShort,
		WriteInt, WriteUInt,
		WriteLong, WriteULong,
		WriteSingle, WriteDouble, WriteDecimal, WriteHalf,
		WriteBigInteger, WriteUInt128, WriteInt128, WriteComplex,
		WriteDateTime, WriteDate, WriteTime, WriteTimeSpan,
		WriteString,
		WriteGuid, WriteBool,
	];

	public void WriteBytes(IWriter writer, ReadOnlySpan<byte> value)
	{
		WriteInt(writer, value.Length);
		writer.WriteBytes(value);
	}

	public void WriteNumber<T>(IWriter writer, T number) where T : struct, IBinaryInteger<T>
	{
		Span<byte> bytes = stackalloc byte[Marshal.SizeOf<T>()];

		if (BigIndian)
		{
			number.WriteBigEndian(bytes);
		}
		else
		{
			number.WriteLittleEndian(bytes);
		}
		writer.WriteBytes(bytes);
	}

	public void WriteByte(IWriter writer, byte value) => writer.WriteByte(value);
	public void WriteSByte(IWriter writer, sbyte value) => writer.WriteByte((byte)value);

	public void WriteBytesEnumeration(IWriter writer, IEnumerable<byte> value)
	{
		foreach (var b in value) writer.WriteByte(b);
	}

	protected void WriteNumberBytes(IWriter writer, byte[] bytes)
	{
		Span<byte> data = (BitConverter.IsLittleEndian ^ BigIndian) ? bytes.Reverse().ToArray() : bytes;
		writer.WriteBytes(data);
	}

	public void WriteShort(IWriter writer, short value) => WriteNumber(writer, value);
	public void WriteUShort(IWriter writer, ushort value) => WriteNumber(writer, value);
	public void WriteInt(IWriter writer, int value) => WriteNumber(writer, value);
	public void WriteUInt(IWriter writer, uint value) => WriteNumber(writer, value);
	public void WriteLong(IWriter writer, long value) => WriteNumber(writer, value);
	public void WriteULong(IWriter writer, ulong value) => WriteNumber(writer, value);
	public void WriteSingle(IWriter writer, float value) => WriteNumberBytes(writer, BitConverter.GetBytes(value));

	public void WriteDouble(IWriter writer, double value) => WriteNumberBytes(writer, BitConverter.GetBytes(value));
	public void WriteDecimal(IWriter writer, decimal value) => WriteNumberBytes(writer, BitConverterEx.GetBytes(value));
	public void WriteHalf(IWriter writer, Half value) => WriteNumberBytes(writer, BitConverter.GetBytes(value));

	public void WriteBigInteger(IWriter writer, BigInteger value)
	{
		var bytes = value.ToByteArray();
		WriteInt(writer, bytes.Length);
		writer.WriteBytes(bytes);
	}

	public void WriteInt128(IWriter writer, Int128 value) => writer.WriteBytes(BitConverterEx.GetBytes(value));

	public void WriteUInt128(IWriter writer, UInt128 value) => writer.WriteBytes(BitConverterEx.GetBytes(value));

	public void WriteComplex(IWriter writer, Complex value)
	{
		WriteDouble(writer, value.Real);
		WriteDouble(writer, value.Imaginary);
	}

	public void WriteString(IWriter writer, string value)
	{
		var data = Encoding.GetBytes(value);
		writer.Write(data.Length);
		writer.WriteBytes(data);
	}

	public void WriteChar(IWriter writer, char value)
	{
		var data = Encoding.GetBytes([value]);
		WriteByte(writer, (byte)data.Length);
		writer.WriteBytes(data);
	}

	public void WriteDateTime(IWriter writer, DateTime value) => WriteLong(writer, value.Ticks);

	public void WriteTime(IWriter writer, TimeOnly value) => WriteLong(writer, value.Ticks);

	public void WriteDate(IWriter writer, DateOnly value) => WriteInt(writer, (int)(value.ToDateTime(TimeOnly.MinValue) - DateTime.MinValue).TotalDays);

	public void WriteTimeSpan(IWriter writer, TimeSpan value) => WriteDouble(writer, value.TotalMicroseconds);

	public void WriteGuid(IWriter writer, Guid value) => writer.WriteBytes(value.ToByteArray());

	public void WriteBool(IWriter writer, bool value) => WriteByte(writer, value ? (byte)1 : (byte)0);
}

public class CompressedIntWriter : IBasicWriter, IIntegerNumberWriters
{
	public IEnumerable<Delegate> WriterDelegates =>
	[
		WriteByte, WriteSByte,
		WriteShort, WriteUShort,
		WriteInt, WriteUInt,
		WriteLong, WriteULong
	];

	private void WriteNumber<T>(IWriter writer, T value) where T : struct, IBinaryInteger<T>
	{
		int sizeOfT = Marshal.SizeOf<T>();
		byte[] bytes = new byte[MathEx.Ceiling(sizeOfT, 7)];
		value.WriteBigEndian(bytes, 7 - sizeOfT % 7);
		int resultLength = bytes.Length * 8 / 7;

		Span<byte> result = stackalloc byte[resultLength]; // Enough space for 7-bit encoded values

		const int mask = 0b0111_1111;
		int shift = 0, targetPosition = 0;
		int temp = 0;

		foreach (var b in bytes)
		{
			temp = (temp << 8) | b;
			shift += 8;
			while (shift >= 7)
			{
				shift -= 7;
				result[targetPosition++] = (byte)((temp >> shift) & mask);
			}
		}
		result = result.TrimStart<byte>(0x00);

		if (result.Length == 0)
		{
			writer.WriteByte(0);
			return;
		}

		for (int i = 0; i < result.Length - 1; i++)
		{
			result[i] |= 0b1000_0000;
		}

		// Write the encoded bytes
		writer.WriteBytes(result);
	}

	public void WriteByte(IWriter writer, byte value) => writer.WriteByte(value);
	public void WriteInt(IWriter writer, int value) => WriteNumber(writer, value);
	public void WriteLong(IWriter writer, long value) => WriteNumber(writer, value);
	public void WriteSByte(IWriter writer, sbyte value) => writer.WriteByte((byte)value);
	public void WriteShort(IWriter writer, short value) => WriteNumber(writer, value);
	public void WriteUInt(IWriter writer, uint value) => WriteNumber(writer, value);
	public void WriteULong(IWriter writer, ulong value) => WriteNumber(writer, value);
	public void WriteUShort(IWriter writer, ushort value) => WriteNumber(writer, value);
}

public class UTF8IntWriter : IBasicWriter, IIntegerNumberWriters
{
	public IEnumerable<Delegate> WriterDelegates =>
	[
		WriteByte, WriteSByte,
		WriteShort, WriteUShort,
		WriteInt, WriteUInt,
		WriteLong, WriteULong
	];

	private void WriteNumber<T>(IWriter writer, T value) where T : IBinaryInteger<T>
	{
		Span<byte> bytes = stackalloc byte[Marshal.SizeOf<T>()];

		value.WriteBigEndian(bytes);
		bytes = bytes.TrimStart((byte)0);
		if (bytes.Length == 0) bytes = [0];
		int leadingZeros = byte.LeadingZeroCount(bytes[0]);

		int prefixLength = bytes.Length / 8;
		int bitsInLastByte = bytes.Length % 8 + 1;
		if (bitsInLastByte + 8 - leadingZeros >= 8)
		{
			bitsInLastByte++;
			if (bitsInLastByte == 8)
			{
				prefixLength++;
				bitsInLastByte = 1;
			}
		}

		for (var i = 0; i < prefixLength; i++)
		{
			WriteByte(writer, 0xFF);
		}
		int prefix = 0;
		for (int i = 0; i < bitsInLastByte - 1; i++)
		{
			prefix |= 0b1000_0000 >> i;
		}

		if (bitsInLastByte + 8 - leadingZeros > 8)
		{
			writer.WriteByte((byte)prefix);
		}
		else
		{
			bytes[0]  = (byte)(prefix | bytes[0]);
		}

		writer.WriteBytes(bytes);
	}

	public void WriteByte(IWriter writer, byte value) => writer.WriteByte(value);
	public void WriteInt(IWriter writer, int value) => WriteNumber(writer, value);
	public void WriteLong(IWriter writer, long value) => WriteNumber(writer, value);
	public void WriteSByte(IWriter writer, sbyte value) => writer.WriteByte((byte)value);
	public void WriteShort(IWriter writer, short value) => WriteNumber(writer, value);
	public void WriteUInt(IWriter writer, uint value) => WriteNumber(writer, value);
	public void WriteULong(IWriter writer, ulong value) => WriteNumber(writer, value);
	public void WriteUShort(IWriter writer, ushort value) => WriteNumber(writer, value);
}
