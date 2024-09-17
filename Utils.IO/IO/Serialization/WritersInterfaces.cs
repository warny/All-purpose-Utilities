using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace Utils.IO.Serialization;

public interface IWriter
{
	void Write<T> (T value);
	void Write(object value);
	void WriteByte(byte value);
	void WriteBytes(ReadOnlySpan<byte> bytes);
}


/// <summary>
/// Interface defining the general writer capabilities, providing a list of writer delegates.
/// </summary>
public interface IBasicWriter
{
	/// <summary>
	/// Gets the collection of writer delegates available for writing data types.
	/// </summary>
	IEnumerable<Delegate> WriterDelegates { get; }
}

/// <summary>
/// Interface for writing floating point numbers.
/// </summary>
public interface IFloatingNumberWriters
{
	/// <summary>
	/// Writes a double-precision floating point number to the stream.
	/// </summary>
	void WriteDouble(IWriter writer, double value);

	/// <summary>
	/// Writes a single-precision floating point number to the stream.
	/// </summary>
	void WriteSingle(IWriter writer, float value);

	/// <summary>
	/// Writes a decimal number to the stream.
	/// </summary>
	void WriteDecimal(IWriter writer, decimal value);

	/// <summary>
	/// Writes a half-precision floating point number to the stream.
	/// </summary>
	void WriteHalf(IWriter writer, Half value);
}

/// <summary>
/// Interface for writing integer numbers.
/// </summary>
public interface IIntegerNumberWriters
{
	/// <summary>
	/// Writes a byte to the stream.
	/// </summary>
	void WriteByte(IWriter writer, byte value);

	/// <summary>
	/// Writes a signed short (16-bit) to the stream.
	/// </summary>
	void WriteShort(IWriter writer, short value);

	/// <summary>
	/// Writes a signed integer (32-bit) to the stream.
	/// </summary>
	void WriteInt(IWriter writer, int value);

	/// <summary>
	/// Writes a signed long (64-bit) to the stream.
	/// </summary>
	void WriteLong(IWriter writer, long value);

	/// <summary>
	/// Writes a signed byte (8-bit) to the stream.
	/// </summary>
	void WriteSByte(IWriter writer, sbyte value);

	/// <summary>
	/// Writes an unsigned short (16-bit) to the stream.
	/// </summary>
	void WriteUShort(IWriter writer, ushort value);

	/// <summary>
	/// Writes an unsigned integer (32-bit) to the stream.
	/// </summary>
	void WriteUInt(IWriter writer, uint value);

	/// <summary>
	/// Writes an unsigned long (64-bit) to the stream.
	/// </summary>
	void WriteULong(IWriter writer, ulong value);
}

/// <summary>
/// Interface for writing extended numbers like BigInteger, Int128, UInt128, and complex numbers.
/// </summary>
public interface IExtendedNumberWriters
{
	/// <summary>
	/// Writes a BigInteger to the stream.
	/// </summary>
	void WriteBigInteger(IWriter writer, BigInteger value);

	/// <summary>
	/// Writes a signed 128-bit integer to the stream.
	/// </summary>
	void WriteInt128(IWriter writer, Int128 value);

	/// <summary>
	/// Writes an unsigned 128-bit integer to the stream.
	/// </summary>
	void WriteUInt128(IWriter writer, UInt128 value);

	/// <summary>
	/// Writes a complex number to the stream.
	/// </summary>
	void WriteComplex(IWriter writer, Complex value);
}

/// <summary>
/// Interface for writing date and time values.
/// </summary>
public interface IDateWriters
{
	/// <summary>
	/// Writes a DateTime value to the stream.
	/// </summary>
	void WriteDateTime(IWriter writer, DateTime value);

	/// <summary>
	/// Writes a TimeOnly value to the stream.
	/// </summary>
	void WriteTime(IWriter writer, TimeOnly value);

	/// <summary>
	/// Writes a DateOnly value to the stream.
	/// </summary>
	void WriteDate(IWriter writer, DateOnly value);

	/// <summary>
	/// Writes a TimeSpan value to the stream.
	/// </summary>
	void WriteTimeSpan(IWriter writer, TimeSpan value);
}

/// <summary>
/// Interface for writing string and character data.
/// </summary>
public interface IStringWriters
{
	/// <summary>
	/// Writes a string to the stream.
	/// </summary>
	void WriteString(IWriter writer, string value);

	/// <summary>
	/// Writes a character to the stream.
	/// </summary>
	void WriteChar(IWriter writer, char value);
}

/// <summary>
/// Interface for writing miscellaneous data types.
/// </summary>
public interface IMiscelaneousWriters
{
	/// <summary>
	/// Writes a GUID to the stream.
	/// </summary>
	void WriteGuid(IWriter writer, Guid value);

	/// <summary>
	/// Writes a boolean value to the stream.
	/// </summary>
	void WriteBool(IWriter writer, bool value);
}
