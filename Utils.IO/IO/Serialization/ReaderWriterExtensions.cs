using System;
using System.Text;

namespace Utils.IO.Serialization;

/// <summary>
/// Extension methods providing convenience helpers for <see cref="Reader"/> and <see cref="Writer"/>.
/// </summary>
public static class ReaderWriterExtensions
{
	/// <summary>
	/// Reads a fixed-length string from the reader using the provided encoding.
	/// </summary>
	/// <param name="reader">The reader to read from.</param>
	/// <param name="length">Number of bytes to read.</param>
	/// <param name="encoding">Encoding of the string.</param>
	/// <returns>The decoded string with trailing null or space characters removed.</returns>
	public static string ReadFixedLengthString(this Reader reader, int length, Encoding encoding)
	{
		var bytes = reader.ReadBytes(length);
		return encoding.GetString(bytes).TrimEnd('\0', ' ');
	}

	/// <summary>
	/// Reads a length-prefixed string from the reader.
	/// </summary>
	/// <param name="reader">The reader to read from.</param>
	/// <param name="encoding">Encoding of the string.</param>
	/// <param name="sizeLength">Number of bytes used to store the length.</param>
	public static string ReadVariableLengthString(this Reader reader, Encoding encoding, int sizeLength = sizeof(int))
	{
		int length = sizeLength switch
		{
			1 => reader.ReadByte(),
			2 => reader.Read<UInt16>(),
			_ => reader.Read<Int32>()
		};
		return encoding.GetString(reader.ReadBytes(length));
	}

	/// <summary>
	/// Reads an array of values from the reader.
	/// </summary>
	/// <typeparam name="T">Type of elements to read.</typeparam>
	/// <param name="reader">The reader to read from.</param>
	/// <param name="count">Number of elements to read.</param>
	/// <param name="bigEndian">Whether numeric values are stored in big-endian order.</param>
	public static T[] ReadArray<T>(this Reader reader, int count, bool bigEndian = false)
	{
		var result = new T[count];
		for (int i = 0; i < count; i++)
		{
			if (bigEndian)
			{
				result[i] = typeof(T) switch
				{
					Type t when t == typeof(short) => (T)(object)reader.Read<Int16>(),
					Type t when t == typeof(ushort) => (T)(object)reader.Read<UInt16>(),
					Type t when t == typeof(int) => (T)(object)reader.Read<Int32>(),
					Type t when t == typeof(uint) => (T)(object)reader.Read<UInt32>(),
					Type t when t == typeof(long) => (T)(object)reader.Read<Int64>(),
					_ => reader.Read<T>()
				};
			}
			else
			{
				result[i] = reader.Read<T>();
			}
		}
		return result;
	}

	/// <summary>
	/// Writes a fixed-length string using the provided encoding. The string is padded with zero bytes if necessary.
	/// </summary>
	/// <param name="writer">The writer to write to.</param>
	/// <param name="value">String value to write.</param>
	/// <param name="length">Desired length in bytes.</param>
	/// <param name="encoding">Encoding of the string.</param>
	public static void WriteFixedLengthString(this Writer writer, string value, int length, Encoding encoding)
	{
		var buffer = new byte[length];
		encoding.GetBytes(value, 0, value.Length, buffer, 0);
		writer.WriteBytes(buffer);
	}

	/// <summary>
	/// Writes a length-prefixed string to the writer.
	/// </summary>
	/// <param name="writer">The writer to write to.</param>
	/// <param name="value">String value to write.</param>
	/// <param name="encoding">Encoding of the string.</param>
	/// <param name="bigEndian">Whether the length is stored in big-endian order.</param>
	/// <param name="sizeLength">Number of bytes used to store the length.</param>
	public static void WriteVariableLengthString(this Writer writer, string value, Encoding encoding, bool bigEndian = false, int sizeLength = sizeof(int))
	{
		var bytes = encoding.GetBytes(value);
		switch (sizeLength)
		{
			case 1:
				writer.Write<Byte>((byte)bytes.Length);
				break;
			case 2:
				writer.Write<UInt16>((ushort)bytes.Length);
				break;
			default:
				writer.Write<Int32>(bytes.Length);
				break;
		}
		writer.WriteBytes(bytes);
	}
}
