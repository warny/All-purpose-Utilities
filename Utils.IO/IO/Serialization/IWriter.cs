using System;

namespace Utils.IO.Serialization;

/// <summary>
/// Provides methods for writing primitive and complex values to a stream.
/// </summary>
public interface IWriter
{
	/// <summary>
	/// Writes a value of the specified type to the stream.
	/// </summary>
	/// <typeparam name="T">Type of the value to write.</typeparam>
	/// <param name="value">Value to write.</param>
	void Write<T>(T value);

	/// <summary>
	/// Writes an object to the stream.
	/// </summary>
	/// <param name="value">Object to write.</param>
	void Write(object value);

	/// <summary>
	/// Writes a single byte to the stream.
	/// </summary>
	/// <param name="value">Byte to write.</param>
	void WriteByte(byte value);

	/// <summary>
	/// Writes a sequence of bytes to the stream.
	/// </summary>
	/// <param name="bytes">Bytes to write.</param>
	void WriteBytes(ReadOnlySpan<byte> bytes);
}
