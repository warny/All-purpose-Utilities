using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Utils.IO.BaseEncoding;
using Utils.Objects;

namespace Utils.IO.Serialization;

public class RawReader
	: IBasicReader, IIntegerNumberReaders, IFloatingNumberReaders, IExtendedNumberReaders, IDateReaders, IStringReaders, IMiscellaneousReaders
{
	public IEnumerable<Delegate> ReaderDelegates =>
	[
		ReadByte, ReadSByte,
		ReadShort, ReadUShort,
		ReadInt, ReadUInt,
		ReadLong, ReadULong,
		ReadSingle, ReadDouble, ReadDecimal, ReadHalf,
		ReadBigInteger, ReadInt128, ReadUInt128, ReadComplex,
		ReadDateTime, ReadDate, ReadTime, ReadTimeSpan,
		ReadString, ReadChar,
		ReadGuid, ReadBool
	];

	public Encoding Encoding { get; init; } = Encoding.UTF8;
	public bool BigIndian { get; init; } = false;


	// Integer reading methods
	public byte ReadByte(IReader reader) => (byte)reader.ReadByte();
	public sbyte ReadSByte(IReader reader) => (sbyte)reader.ReadByte();
	public short ReadShort(IReader reader) => ReadNumber<short>(reader, false);
	public ushort ReadUShort(IReader reader) => ReadNumber<ushort>(reader, true);
	public int ReadInt(IReader reader) => ReadNumber<int>(reader, false);
	public uint ReadUInt(IReader reader) => ReadNumber<uint>(reader, true);
	public long ReadLong(IReader reader) => ReadNumber<long>(reader, false);
	public ulong ReadULong(IReader reader) => ReadNumber<ulong>(reader, true);

	// Floating point number reading methods
	public float ReadSingle(IReader reader) => BitConverter.ToSingle(ReadNumberBytes(reader, sizeof(float)));
	public double ReadDouble(IReader reader) => BitConverter.ToDouble(ReadNumberBytes(reader, sizeof(double)));
	public decimal ReadDecimal(IReader reader) => BitConverterEx.ToDecimal(ReadNumberBytes(reader, sizeof(decimal)));
	public Half ReadHalf(IReader reader) => BitConverter.ToHalf(ReadNumberBytes(reader, Marshal.SizeOf(typeof(Half))));

	// Helper methods for reading numbers
	private T ReadNumber<T>(IReader reader, bool isUnsigned) where T : struct, IBinaryInteger<T>
	{
		int size = Marshal.SizeOf(typeof(T));
		byte[] bytes = reader.ReadBytes(size);
		T value = BigIndian 
			? T.ReadBigEndian(bytes, isUnsigned) 
			: T.ReadLittleEndian(bytes, isUnsigned);

		return value; 
	}

	private byte[] ReadNumberBytes(IReader reader, int length)
	{
		byte[] bytes = reader.ReadBytes(length);
		return (BitConverter.IsLittleEndian ^ BigIndian) ? [.. bytes.Reverse()] : bytes;

	}

	// Extended number reading methods

	public BigInteger ReadBigInteger(IReader reader)
	{
		int length = ReadInt(reader);
		byte[] bytes = reader.ReadBytes(length);
		return new BigInteger(bytes);
	}

	public Int128 ReadInt128(IReader reader) => BitConverterEx.ToInt128(reader.ReadBytes(16));
	public UInt128 ReadUInt128(IReader reader) => BitConverterEx.ToUInt128(reader.ReadBytes(16));

	public Complex ReadComplex(IReader reader)
	{
		double real = ReadDouble(reader);
		double imaginary = ReadDouble(reader);
		return new Complex(real, imaginary);
	}

	// Date and time reading methods
	public DateTime ReadDateTime(IReader reader) => new DateTime(ReadLong(reader));
	public TimeOnly ReadTime(IReader reader) => new TimeOnly(ReadLong(reader));
	public DateOnly ReadDate(IReader reader) => DateOnly.FromDayNumber(ReadInt(reader));
	public TimeSpan ReadTimeSpan(IReader reader) => TimeSpan.FromTicks(ReadLong(reader));

	// String and character reading methods
	public string ReadString(IReader reader)
	{
		int length = ReadInt(reader);
		byte[] bytes = reader.ReadBytes(length);
		return Encoding.GetString(bytes);
	}

	public char ReadChar(IReader reader)
	{
		byte[] bytes = reader.ReadBytes(sizeof(char));
		return BitConverter.ToChar(bytes);
	}

	// Miscellaneous reading methods
	public Guid ReadGuid(IReader reader) => new Guid(reader.ReadBytes(16));
	public bool ReadBool(IReader reader) => ReadByte(reader) == 1;

}

public class CompressedIntReader : IBasicReader, IIntegerNumberReaders
{
	public IEnumerable<Delegate> ReaderDelegates =>
	[
			ReadByte, ReadSByte,
			ReadShort, ReadUShort,
			ReadInt, ReadUInt,
			ReadLong, ReadULong
	];

	// Méthode principale pour lire un nombre compressé
	private T ReadNumber<T>(IReader reader, bool isUnsigned) where T : IBinaryInteger<T>
	{
		const int mask = 0b0111_1111;
		const int continueBit = 0b1000_0000;
		const int byteBits = 7;

		int currentByte;

		T value = default;
		while ((currentByte = reader.ReadByte()) != -1)
		{
			value <<= byteBits;
			value |= T.CreateChecked(currentByte & mask);
			if ((currentByte & continueBit) == 0) break;
		}
		return value;
	}

	public byte ReadByte(IReader reader) => (byte)reader.ReadByte();
	public sbyte ReadSByte(IReader reader) => (sbyte)reader.ReadByte();
	public short ReadShort(IReader reader) => ReadNumber<short>(reader, false);
	public ushort ReadUShort(IReader reader) => ReadNumber<ushort>(reader, true);
	public int ReadInt(IReader reader) => ReadNumber<int>(reader, false);
	public uint ReadUInt(IReader reader) => ReadNumber<uint>(reader, true);
	public long ReadLong(IReader reader) => ReadNumber<long>(reader, false);
	public ulong ReadULong(IReader reader) => ReadNumber<ulong>(reader, true);
}


public class UTF8IntReader : IBasicReader, IIntegerNumberReaders
{
	public IEnumerable<Delegate> ReaderDelegates =>
	[
			ReadByte, ReadSByte,
			ReadShort, ReadUShort,
			ReadInt, ReadUInt,
			ReadLong, ReadULong
	];

	/// <summary>
	/// Reads a number from the reader that was written using the UTF-8 like encoding.
	/// </summary>
	private T ReadNumber<T>(IReader reader, bool isUnsigned) where T : IBinaryInteger<T>
	{
		// First, we need to read the prefix bytes to determine how many bytes represent the number
		int totalBytes = 0;
		int prefixByte = 0;

		// Read and accumulate prefix bytes
		while ((prefixByte = reader.ReadByte()) == 0xFF)
		{
			totalBytes += 8;
		}

		// The last prefix byte is the one with non-0xFF content, indicating the number of bits
		if (prefixByte == -1)
		{
			throw new ReaderException("Unexpected end of reader while reading the prefix.");
		}

		// Calculate how many bits are used in the last prefix byte
		int prefixBitsInLastPrefixByte = byte.LeadingZeroCount((byte)~prefixByte);
		totalBytes += prefixBitsInLastPrefixByte;

		//read value on this last prefix byte 
		int mask = 0xFF;
		for (int i = 0; i <= prefixBitsInLastPrefixByte; i++)
		{
			mask &= 0b0111_1111 >> i;
		}
		T value = T.CreateChecked(prefixByte & mask);

		// Read the remaining bytes for the value itself
		var valueBytes = reader.ReadBytes(totalBytes - 1);
		foreach (var b in valueBytes)
		{
			value = value << 8 | T.CreateChecked(b);
		}
		return value;
	}

	public byte ReadByte(IReader reader) => (byte)reader.ReadByte();
	public sbyte ReadSByte(IReader reader) => (sbyte)reader.ReadByte();
	public short ReadShort(IReader reader) => ReadNumber<short>(reader, false);
	public ushort ReadUShort(IReader reader) => ReadNumber<ushort>(reader, true);
	public int ReadInt(IReader reader) => ReadNumber<int>(reader, false);
	public uint ReadUInt(IReader reader) => ReadNumber<uint>(reader, true);
	public long ReadLong(IReader reader) => ReadNumber<long>(reader, false);
	public ulong ReadULong(IReader reader) => ReadNumber<ulong>(reader, true);
}