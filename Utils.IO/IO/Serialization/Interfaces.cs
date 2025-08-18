using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Utils.IO.Serialization;

/// <summary>
/// Defines a reader capable of deserializing objects of specific types using a generic <see cref="IReader"/>.
/// </summary>
public interface IObjectReader
{
        /// <summary>
        /// Gets the types that this reader can deserialize.
        /// </summary>
        Type[] Types { get; }

        /// <summary>
        /// Attempts to read an object from the supplied <paramref name="reader"/>.
        /// </summary>
        /// <param name="reader">Source reader providing the data stream.</param>
        /// <param name="result">When this method returns, contains the deserialized object.</param>
        /// <returns><c>true</c> if the object was successfully read; otherwise, <c>false</c>.</returns>
        bool Read(IReader reader, out object result);
}

/// <summary>
/// Defines a writer capable of serializing objects of specific types using a generic <see cref="IWriter"/>.
/// </summary>
public interface IObjectWriter
{
        /// <summary>
        /// Gets the types that this writer can serialize.
        /// </summary>
        Type[] Types { get; }

        /// <summary>
        /// Writes an object to the supplied <paramref name="writer"/>.
        /// </summary>
        /// <param name="writer">Destination writer receiving the serialized data.</param>
        /// <param name="obj">Object instance to write.</param>
        /// <returns><c>true</c> if the object was successfully written; otherwise, <c>false</c>.</returns>
        bool Write(IWriter writer, object obj);
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


