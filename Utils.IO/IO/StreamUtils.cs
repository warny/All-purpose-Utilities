using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Utils.IO;

/// <summary>
/// Provides extension and utility methods for working with <see cref="Stream"/> objects.
/// </summary>
public static class StreamUtils
{
	/// <summary>
	/// Reads exactly <paramref name="length"/> bytes from the given <see cref="Stream"/> 
	/// and returns them in a byte array.
	/// </summary>
	/// <param name="s">The source <see cref="Stream"/> to read from.</param>
	/// <param name="length">The number of bytes to read from the stream.</param>
	/// <param name="raiseException">
	/// If <c>true</c> and fewer than <paramref name="length"/> bytes could be read,
	/// throws an <see cref="EndOfStreamException"/>.
	/// </param>
	/// <returns>
	/// A byte array containing up to <paramref name="length"/> bytes read from the stream.  
	/// If <paramref name="raiseException"/> is <c>false</c> and the stream ends, 
	/// the returned array might be partially uninitialized for the remainder.
	/// </returns>
	/// <exception cref="EndOfStreamException">
	/// Thrown if <paramref name="raiseException"/> is <c>true</c> and the stream ends before 
	/// <paramref name="length"/> bytes could be read.
	/// </exception>
	public static byte[] ReadBytes(this Stream s, int length, bool raiseException = false)
	{
		byte[] buffer = new byte[length];
		int totalRead = 0;
		while (totalRead < length)
		{
			int bytesRead = s.Read(buffer, totalRead, length - totalRead);
			if (bytesRead == 0)
			{
				// No more data available (EOF)
				if (raiseException)
					throw new EndOfStreamException($"Could not read the requested {length} bytes from the stream.");
				break;
			}
			totalRead += bytesRead;
		}
		return buffer;
	}

	/// <summary>
	/// Reads all remaining data from the given <see cref="Stream"/> until EOF
	/// and returns it as a byte array.
	/// </summary>
	/// <param name="s">The source <see cref="Stream"/> to read from.</param>
	/// <returns>A byte array containing the entire remaining content of the stream.</returns>
	public static byte[] ReadToEnd(this Stream s)
	{
		const int bufferSize = 4096;
		byte[] buffer = new byte[bufferSize];

		using (var ms = new MemoryStream())
		{
			int bytesRead;
			while ((bytesRead = s.Read(buffer, 0, buffer.Length)) > 0)
			{
				ms.Write(buffer, 0, bytesRead);
			}
			return ms.ToArray();
		}
	}

	/// <summary>
	/// Copies all remaining data from the current <see cref="Stream"/> to the specified 
	/// destination <see cref="Stream"/> until EOF.
	/// </summary>
	/// <param name="source">The source <see cref="Stream"/> to read from.</param>
	/// <param name="destination">The target <see cref="Stream"/> to write to.</param>
	/// <param name="bufferSize">The size of the buffer used for copying (default is 81920).</param>
	public static void CopyToStream(this Stream source, Stream destination, int bufferSize = 81920)
	{
		if (source == null) throw new ArgumentNullException(nameof(source));
		if (destination == null) throw new ArgumentNullException(nameof(destination));
		if (!source.CanRead) throw new NotSupportedException("Source stream is not readable.");
		if (!destination.CanWrite) throw new NotSupportedException("Destination stream is not writable.");

		byte[] buffer = new byte[bufferSize];
		int bytesRead;
		while ((bytesRead = source.Read(buffer, 0, bufferSize)) > 0)
		{
			destination.Write(buffer, 0, bytesRead);
		}
	}

	/// <summary>
	/// Reads all remaining data from the current <see cref="Stream"/> 
	/// and returns a new <see cref="MemoryStream"/> containing that data.
	/// </summary>
	/// <param name="s">The source <see cref="Stream"/> to read from.</param>
	/// <returns>A <see cref="MemoryStream"/> that contains all bytes read from <paramref name="s"/>.</returns>
	public static MemoryStream ReadToMemoryStream(this Stream s)
	{
		var ms = new MemoryStream();
		s.CopyToStream(ms);
		ms.Position = 0;
		return ms;
	}

	/// <summary>
	/// Reads all remaining bytes from the given <see cref="Stream"/> as text
	/// using the specified <see cref="Encoding"/>.
	/// </summary>
	/// <param name="s">The source <see cref="Stream"/> to read from.</param>
	/// <param name="encoding">
	/// The character encoding to use. If <c>null</c>, defaults to <see cref="Encoding.UTF8"/>.
	/// </param>
	/// <returns>A string containing all text read from the stream.</returns>
	public static string ReadAllText(this Stream s, Encoding encoding = null)
	{
		encoding ??= Encoding.UTF8;
		using (var reader = new StreamReader(s, encoding, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
		{
			return reader.ReadToEnd();
		}
	}

	/// <summary>
	/// Writes the specified data to the stream.
	/// </summary>
	/// <param name="s">The <see cref="Stream"/> to write into.</param>
	/// <param name="data">The byte array to write.</param>
	public static void WriteBytes(this Stream s, byte[] data)
	{
		if (s == null) throw new ArgumentNullException(nameof(s));
		if (data == null) throw new ArgumentNullException(nameof(data));

		s.Write(data, 0, data.Length);
	}

	/// <summary>
	/// Writes the specified text to the stream using the given <see cref="Encoding"/>.
	/// </summary>
	/// <param name="s">The <see cref="Stream"/> to write into.</param>
	/// <param name="text">The string to write.</param>
	/// <param name="encoding">
	/// The character encoding to use. If <c>null</c>, defaults to <see cref="Encoding.UTF8"/>.
	/// </param>
	public static void WriteAllText(this Stream s, string text, Encoding encoding = null)
	{
		if (s == null) throw new ArgumentNullException(nameof(s));
		if (text == null) throw new ArgumentNullException(nameof(text));
		encoding ??= Encoding.UTF8;

		using (var writer = new StreamWriter(s, encoding, bufferSize: 1024, leaveOpen: true))
		{
			writer.Write(text);
		}
	}

	/// <summary>
	/// Reads line-by-line from a text-based stream until EOF. 
	/// Each line is returned as an element in the resulting sequence.
	/// </summary>
	/// <param name="s">The <see cref="Stream"/> to read from.</param>
	/// <param name="encoding">
	/// The character encoding to use. If <c>null</c>, defaults to <see cref="Encoding.UTF8"/>.
	/// </param>
	/// <returns>
	/// An <see cref="IEnumerable{String}"/> of lines read from the stream.
	/// The stream is read lazily; re-iterating this enumerable will not re-read the stream.
	/// </returns>
	public static IEnumerable<string> ReadLines(this Stream s, Encoding encoding = null)
	{
		if (s == null) throw new ArgumentNullException(nameof(s));
		encoding ??= Encoding.UTF8;

		using (var reader = new StreamReader(s, encoding, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
		{
			string line;
			while ((line = reader.ReadLine()) != null)
			{
				yield return line;
			}
		}
	}

	/// <summary>
	/// Reads and returns the next "block" from the stream, delimited by the specified byte sequence.
	/// The returned array does not include the delimiter itself. If the delimiter is not found
	/// before EOF, all remaining bytes in the stream are returned.
	/// 
	/// This method can be called repeatedly to read multiple blocks from the same stream.
	/// </summary>
	/// <param name="s">The <see cref="Stream"/> from which to read the block.</param>
	/// <param name="blockSeparator">A byte array representing the block delimiter.</param>
	/// <returns>
	/// A byte array containing the next block of data, not including the delimiter. 
	/// If EOF is reached before finding the delimiter, returns all remaining data (which could be empty).
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="blockSeparator"/> is <c>null</c>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown if <paramref name="blockSeparator"/> is empty (cannot search for an empty delimiter).
	/// </exception>
	public static byte[] ReadBlock(this Stream s, byte[] blockSeparator)
	{
		if (blockSeparator == null)
			throw new ArgumentNullException(nameof(blockSeparator), "Block separator cannot be null.");

		if (blockSeparator.Length == 0)
			throw new ArgumentException("Block separator cannot be an empty array.", nameof(blockSeparator));

		// We'll store the data in a MemoryStream as we read it.
		// We'll also keep track of the last bytes read to detect the separator.
		using var ms = new MemoryStream();
		var separatorQueue = new Queue<byte>(blockSeparator.Length);

		while (true)
		{
			int readByte = s.ReadByte();
			if (readByte == -1)
			{
				// Reached end of stream => return whatever we have so far
				return ms.ToArray();
			}

			// Write the current byte to the memory stream
			ms.WriteByte((byte)readByte);

			// Keep track of up to the last 'blockSeparator.Length' bytes
			separatorQueue.Enqueue((byte)readByte);
			if (separatorQueue.Count > blockSeparator.Length)
			{
				separatorQueue.Dequeue();
			}

			// If the queue holds exactly the separator length, check for match
			if (separatorQueue.Count == blockSeparator.Length)
			{
				bool matched = true;
				int i = 0;
				foreach (byte b in separatorQueue)
				{
					if (blockSeparator[i++] != b)
					{
						matched = false;
						break;
					}
				}

				if (matched)
				{
					// The block ends right before the block separator
					// => remove the separator from the MemoryStream content
					long newLength = ms.Length - blockSeparator.Length;
					if (newLength < 0) newLength = 0; // Safety check

					ms.SetLength(newLength);
					return ms.ToArray();
				}
			}
		}
	}
}
