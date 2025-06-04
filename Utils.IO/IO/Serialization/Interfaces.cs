using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Utils.IO.Serialization;

public interface IObjectReader
{
	Type[] Types { get; }
	bool Read(Reader reader, out object result);
}

public interface IObjectWriter
{
	Type[] Types { get; }
	bool Write(Writer writer, object obj);
}

/// <summary>
/// Interface representing a stream mapping that allows reading, seeking, and slicing operations on a stream.
/// </summary>
/// <typeparam name="T">The type that represents the sliced portion of the stream.</typeparam>
public interface IStreamMapping<T>
{
	/// <summary>
	/// Gets the number of bytes remaining in the stream.
	/// </summary>
	long BytesLeft { get; }

	/// <summary>
	/// Gets or sets the current position in the stream.
	/// </summary>
	long Position { get; set; }

	/// <summary>
	/// Gets the underlying stream.
	/// </summary>
	Stream Stream { get; }

	/// <summary>
	/// Pops the last pushed position from the position stack and seeks to that position.
	/// </summary>
	void Pop();

	/// <summary>
	/// Pushes the current position onto the position stack.
	/// </summary>
	void Push();

	/// <summary>
	/// Pushes the current position onto the position stack and seeks to a new position relative to the specified origin.
	/// </summary>
	/// <param name="offset">The byte offset relative to <paramref name="origin"/>.</param>
	/// <param name="origin">The origin from which to seek.</param>
	void Push(int offset, SeekOrigin origin);

	/// <summary>
	/// Seeks to the specified position in the stream relative to the given origin.
	/// </summary>
	/// <param name="offset">The byte offset relative to <paramref name="origin"/>.</param>
	/// <param name="origin">The origin from which to seek.</param>
	void Seek(int offset, SeekOrigin origin);

	/// <summary>
	/// Creates a slice of the current stream starting at the specified position and with the specified length.
	/// </summary>
	/// <param name="position">The start position of the slice in the stream.</param>
	/// <param name="length">The length of the slice.</param>
	/// <returns>A new instance of type <typeparamref name="T"/> that represents the slice of the stream.</returns>
	T Slice(long position, long length);
}


