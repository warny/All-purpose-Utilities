using System;

namespace Utils.IO.Serialization;

/// <summary>
/// Provides methods for reading primitive and complex values from a stream.
/// </summary>
public interface IReader
{
    /// <summary>
    /// Reads a value of the specified type from the underlying stream.
    /// </summary>
    /// <typeparam name="T">Type of the value to read.</typeparam>
    /// <returns>The value read from the stream.</returns>
    T Read<T>();

    /// <summary>
    /// Reads a value of the specified <paramref name="type"/> from the stream.
    /// </summary>
    /// <param name="type">Type of the value to read.</param>
    /// <returns>The value read from the stream.</returns>
    object Read(Type type);

    /// <summary>
    /// Reads a single byte from the stream.
    /// </summary>
    /// <returns>The byte read or -1 if at the end of the stream.</returns>
    int ReadByte();

    /// <summary>
    /// Reads a sequence of bytes from the stream.
    /// </summary>
    /// <param name="length">Number of bytes to read.</param>
    /// <returns>The bytes read.</returns>
    byte[] ReadBytes(int length);
}
