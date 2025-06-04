using System;
using System.Collections.Generic;
using System.IO;

namespace Utils.IO.Serialization;

public class ReaderWriter
{
	public Stream Stream { get; }
	public Reader Reader { get; }
	public Writer Writer { get; }

	private readonly Stack<long> savedPositions = new Stack<long>();

	public ReaderWriter(Stream stream)
	{
		Stream = stream;
		Reader = new Reader(stream);
		Writer = new Writer(stream);
	}
	public ReaderWriter(Byte[] datas) : this(new MemoryStream(datas)) { }


	public void Push() => savedPositions.Push(Stream.Position);
	public void Pop() => Stream.Position = savedPositions.Pop();
	public long BytesLeft => Stream.Length - Stream.Position;
	public long Position {
		get => Stream.Position;
		set => Stream.Position = value; 
	}

	public void Seek(int offset, SeekOrigin seekOrigin = SeekOrigin.Current) => Stream.Seek(offset, seekOrigin);
	public ReaderWriter Slice(long position, long length)
	{
		PartialStream s = new PartialStream(Stream, position, length);
		return new ReaderWriter(s);
	}
}
