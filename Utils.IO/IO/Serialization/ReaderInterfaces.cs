using System;
using System.Collections.Generic;
using System.Numerics;

namespace Utils.IO.Serialization;


public interface IReader
{
	T Read<T>();
	object Read(Type type);
	int ReadByte();
	byte[] ReadBytes(int length);

}

/// <summary>
/// Interface defining the general reader capabilities, providing a list of reader delegates.
/// </summary>
public interface IBasicReader
{
	/// <summary>
	/// Gets the collection of reader delegates available for reading data types.
	/// </summary>
	IEnumerable<Delegate> ReaderDelegates { get; }
}

/// <summary>
/// Interface for reading floating point numbers.
/// </summary>
public interface IFloatingNumberReaders
{
	double ReadDouble(IReader reader);
	float ReadSingle(IReader reader);
	decimal ReadDecimal(IReader reader);
	Half ReadHalf(IReader reader);
}

/// <summary>
/// Interface for reading integer numbers.
/// </summary>
public interface IIntegerNumberReaders
{
	byte ReadByte(IReader reader);
	short ReadShort(IReader reader);
	int ReadInt(IReader reader);
	long ReadLong(IReader reader);
	sbyte ReadSByte(IReader reader);
	ushort ReadUShort(IReader reader);
	uint ReadUInt(IReader reader);
	ulong ReadULong(IReader reader);
}

/// <summary>
/// Interface for reading extended numbers like BigInteger, Int128, UInt128, and complex numbers.
/// </summary>
public interface IExtendedNumberReaders
{
	BigInteger ReadBigInteger(IReader reader);
	Int128 ReadInt128(IReader reader);
	UInt128 ReadUInt128(IReader reader);
	Complex ReadComplex(IReader reader);
}

/// <summary>
/// Interface for reading date and time values.
/// </summary>
public interface IDateReaders
{
	DateTime ReadDateTime(IReader reader);
	TimeOnly ReadTime(IReader reader);
	DateOnly ReadDate(IReader reader);
	TimeSpan ReadTimeSpan(IReader reader);
}

/// <summary>
/// Interface for reading string and character data.
/// </summary>
public interface IStringReaders
{
	string ReadString(IReader reader);
	char ReadChar(IReader reader);
}

/// <summary>
/// Interface for reading miscellaneous data types.
/// </summary>
public interface IMiscellaneousReaders
{
	Guid ReadGuid(IReader reader);
	bool ReadBool(IReader reader);
}
