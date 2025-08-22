using System.IO;

namespace Utils.IO.Serialization;

/// <summary>
/// Represents a stream that supports reading, seeking and slicing operations.
/// </summary>
/// <typeparam name="T">Type returned when creating a slice of the stream.</typeparam>
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
	/// Pops the last pushed position and seeks to it.
	/// </summary>
	void Pop();

	/// <summary>
	/// Pushes the current position onto the internal stack.
	/// </summary>
	void Push();

	/// <summary>
	/// Pushes the current position and seeks relative to the specified origin.
	/// </summary>
	/// <param name="offset">Byte offset relative to <paramref name="origin"/>.</param>
	/// <param name="origin">Origin from which to seek.</param>
	void Push(int offset, SeekOrigin origin);

	/// <summary>
	/// Moves the position within the stream.
	/// </summary>
	/// <param name="offset">Byte offset relative to <paramref name="origin"/>.</param>
	/// <param name="origin">Origin from which to seek.</param>
	void Seek(int offset, SeekOrigin origin);

	/// <summary>
	/// Creates a slice of the stream starting at <paramref name="position"/> with the specified <paramref name="length"/>.
	/// </summary>
	/// <param name="position">Start position of the slice.</param>
	/// <param name="length">Length of the slice.</param>
	/// <returns>A new instance of <typeparamref name="T"/> limited to the slice.</returns>
	T Slice(long position, long length);
}
