using System;
using System.Collections.Generic;
using System.IO;

namespace Utils.IO.Serialization;

/// <summary>
/// Helper class exposing a <see cref="Serialization.Reader"/> and a <see cref="Serialization.Writer"/> over the same stream.
/// </summary>
public class ReaderWriter
{
	/// <summary>
	/// Gets the underlying stream.
	/// </summary>
	public Stream Stream { get; }

	/// <summary>
	/// Gets the reader used to read from the stream.
	/// </summary>
	public Reader Reader { get; }

	/// <summary>
	/// Gets the writer used to write to the stream.
	/// </summary>
	public Writer Writer { get; }

	private readonly Stack<long> savedPositions = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="ReaderWriter"/> class for the specified stream.
	/// </summary>
	/// <param name="stream">Stream to read from and write to.</param>
	public ReaderWriter(Stream stream)
	{
		Stream = stream;
		Reader = new Reader(stream);
		Writer = new Writer(stream);
	}

	/// <summary>
	/// Initializes a new instance from the provided data array.
	/// </summary>
	/// <param name="datas">Byte array used to create the stream.</param>
	public ReaderWriter(byte[] datas) : this(new MemoryStream(datas)) { }

	/// <summary>
	/// Saves the current stream position onto an internal stack.
	/// </summary>
	public void Push() => savedPositions.Push(Stream.Position);

	/// <summary>
	/// Restores the last saved stream position.
	/// </summary>
	public void Pop() => Stream.Position = savedPositions.Pop();

	/// <summary>
	/// Gets the number of bytes remaining in the stream.
	/// </summary>
	public long BytesLeft => Stream.Length - Stream.Position;

	/// <summary>
	/// Gets or sets the current position within the stream.
	/// </summary>
	public long Position
	{
		get => Stream.Position;
		set => Stream.Position = value;
	}

	/// <summary>
	/// Moves the position within the stream.
	/// </summary>
	/// <param name="offset">The byte offset to seek.</param>
	/// <param name="seekOrigin">Specifies the beginning, the end, or the current position as a reference point.</param>
	public void Seek(int offset, SeekOrigin seekOrigin = SeekOrigin.Current) => Stream.Seek(offset, seekOrigin);

	/// <summary>
	/// Creates a reader/writer operating on a slice of the current stream.
	/// </summary>
	/// <param name="position">Start position of the slice.</param>
	/// <param name="length">Length of the slice.</param>
	/// <returns>A new <see cref="ReaderWriter"/> limited to the specified region.</returns>
	public ReaderWriter Slice(long position, long length)
	{
		PartialStream s = new PartialStream(Stream, position, length);
		return new ReaderWriter(s);
	}
}
