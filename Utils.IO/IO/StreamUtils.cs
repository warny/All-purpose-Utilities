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
    /// When <paramref name="raiseException"/> is <see langword="false"/> and the stream ends early,
    /// only the bytes actually read are returned (no zero-padding).
    /// </summary>
    /// <param name="s">The source <see cref="Stream"/> to read from.</param>
    /// <param name="length">The number of bytes to read from the stream.</param>
    /// <param name="raiseException">
    /// If <see langword="true"/> and fewer than <paramref name="length"/> bytes could be read,
    /// throws an <see cref="EndOfStreamException"/>.
    /// If <see langword="false"/>, returns only the bytes that were actually read.
    /// </param>
    /// <returns>
    /// A byte array of exactly <paramref name="length"/> bytes when successful, or a shorter array
    /// when <paramref name="raiseException"/> is <see langword="false"/> and the stream ends early.
    /// </returns>
    /// <exception cref="EndOfStreamException">
    /// Thrown if <paramref name="raiseException"/> is <see langword="true"/> and the stream ends before
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
                if (raiseException)
                    throw new EndOfStreamException($"Could not read the requested {length} bytes from the stream.");
                // Return only the bytes actually read — no zero-padding
                return buffer[..totalRead];
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
    /// <param name="maxBytes">
    /// Optional maximum number of bytes to read. Throws <see cref="InvalidOperationException"/>
    /// if the stream contains more data than this limit. <see langword="null"/> means no limit.
    /// </param>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="maxBytes"/> is exceeded.</exception>
    public static byte[] ReadToEnd(this Stream s, int? maxBytes = null)
    {
        const int bufferSize = 4096;
        byte[] buffer = new byte[bufferSize];

        using var ms = new MemoryStream();
        int bytesRead;
        while ((bytesRead = s.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (maxBytes.HasValue && ms.Length + bytesRead > maxBytes.Value)
                throw new InvalidOperationException($"Stream exceeds the maximum allowed size of {maxBytes.Value} bytes.");
            ms.Write(buffer, 0, bytesRead);
        }
        return ms.ToArray();
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
        if (bufferSize <= 0) throw new ArgumentOutOfRangeException(nameof(bufferSize), "Buffer size must be positive.");
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
    /// The source stream is left open. The returned stream is positioned at 0 on success.
    /// </summary>
    /// <param name="s">The source <see cref="Stream"/> to read from.</param>
    /// <param name="maxBytes">
    /// Optional maximum number of bytes to read. When the source contains more data than this limit,
    /// an <see cref="InvalidOperationException"/> is thrown and the temporary buffer is disposed.
    /// <see langword="null"/> means no limit.
    /// </param>
    /// <returns>A <see cref="MemoryStream"/> that contains all bytes read from <paramref name="s"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxBytes"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="maxBytes"/> is exceeded.</exception>
    public static MemoryStream ReadToMemoryStream(this Stream s, int? maxBytes = null)
    {
        if (s == null) throw new ArgumentNullException(nameof(s));
        if (maxBytes.HasValue && maxBytes.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(maxBytes), "The maximum byte count must be non-negative.");

        const int bufferSize = 4096;
        byte[] buffer = new byte[bufferSize];
        var ms = new MemoryStream();
        try
        {
            int bytesRead;
            while ((bytesRead = s.Read(buffer, 0, buffer.Length)) > 0)
            {
                // Enforce the limit before writing each block so an oversized stream never materializes fully.
                if (maxBytes.HasValue && ms.Length + bytesRead > maxBytes.Value)
                    throw new InvalidOperationException($"Stream exceeds the maximum allowed size of {maxBytes.Value} bytes.");
                ms.Write(buffer, 0, bytesRead);
            }
        }
        catch
        {
            ms.Dispose();
            throw;
        }
        ms.Position = 0;
        return ms;
    }

    /// <summary>
    /// Reads all remaining bytes from the given <see cref="Stream"/> as text
    /// using the specified <see cref="Encoding"/>. The source stream is left open.
    /// </summary>
    /// <param name="s">The source <see cref="Stream"/> to read from.</param>
    /// <param name="encoding">
    /// The character encoding to use. If <c>null</c>, defaults to <see cref="Encoding.UTF8"/>.
    /// Byte-order-mark detection remains enabled.
    /// </param>
    /// <param name="maxBytes">
    /// Optional maximum number of encoded bytes to read from the source. When exceeded, an
    /// <see cref="InvalidOperationException"/> is thrown. <see langword="null"/> means no limit.
    /// </param>
    /// <returns>A string containing all text read from the stream.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxBytes"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="maxBytes"/> is exceeded.</exception>
    public static string ReadAllText(this Stream s, Encoding encoding = null, int? maxBytes = null)
    {
        if (s == null) throw new ArgumentNullException(nameof(s));
        encoding ??= Encoding.UTF8;
        if (maxBytes.HasValue && maxBytes.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(maxBytes), "The maximum byte count must be non-negative.");

        // Bound the encoded bytes first (preserving the BOM), then decode with the same detection logic.
        Stream source = s;
        MemoryStream bounded = null;
        if (maxBytes.HasValue)
            source = bounded = s.ReadToMemoryStream(maxBytes.Value);
        try
        {
            using var reader = new StreamReader(source, encoding, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
            return reader.ReadToEnd();
        }
        finally
        {
            bounded?.Dispose();
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
        // Circular buffer: avoids Queue enumerator allocation on every byte read.
        int sepLen = blockSeparator.Length;
        byte[] window = new byte[sepLen];
        int writePos = 0;   // next slot to write into
        int filled   = 0;   // number of valid bytes currently in window

        while (true)
        {
            int readByte = s.ReadByte();
            if (readByte == -1)
                return ms.ToArray();

            ms.WriteByte((byte)readByte);

            window[writePos] = (byte)readByte;
            writePos = (writePos + 1) % sepLen;
            if (filled < sepLen) filled++;

            if (filled == sepLen)
            {
                bool matched = true;
                for (int i = 0; i < sepLen; i++)
                {
                    if (window[(writePos + i) % sepLen] != blockSeparator[i])
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched)
                {
                    long newLength = ms.Length - sepLen;
                    if (newLength < 0) newLength = 0;
                    ms.SetLength(newLength);
                    return ms.ToArray();
                }
            }
        }
    }

    /// <summary>
    /// Reads and returns the next "block" from the stream, delimited by the specified byte sequence,
    /// enforcing a maximum number of bytes scanned. The returned array does not include the delimiter.
    /// The delimiter is located using the Knuth-Morris-Pratt algorithm over 4096-byte chunks rather than
    /// reading one byte at a time.
    /// </summary>
    /// <remarks>
    /// Unlike the unbounded <see cref="ReadBlock(Stream, byte[])"/> overload, this method treats a missing
    /// delimiter as an error: if the stream reaches EOF before the delimiter is found, an
    /// <see cref="EndOfStreamException"/> is thrown. If more than <paramref name="maxBytes"/> bytes are read
    /// before the delimiter appears, an <see cref="InvalidOperationException"/> is thrown.
    /// </remarks>
    /// <param name="s">The <see cref="Stream"/> from which to read the block.</param>
    /// <param name="blockSeparator">A byte array representing the block delimiter.</param>
    /// <param name="maxBytes">The maximum number of bytes to read before the delimiter must appear.</param>
    /// <returns>A byte array containing the next block of data, not including the delimiter.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="blockSeparator"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="blockSeparator"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="maxBytes"/> is negative.</exception>
    /// <exception cref="EndOfStreamException">Thrown if EOF is reached before the delimiter is found.</exception>
    /// <exception cref="InvalidOperationException">Thrown if <paramref name="maxBytes"/> is exceeded before the delimiter is found.</exception>
    public static byte[] ReadBlock(this Stream s, byte[] blockSeparator, int maxBytes)
    {
        if (s == null)
            throw new ArgumentNullException(nameof(s));
        if (blockSeparator == null)
            throw new ArgumentNullException(nameof(blockSeparator), "Block separator cannot be null.");
        if (blockSeparator.Length == 0)
            throw new ArgumentException("Block separator cannot be an empty array.", nameof(blockSeparator));
        if (maxBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(maxBytes), "The maximum byte count must be non-negative.");

        int sepLen = blockSeparator.Length;
        int[] failure = BuildKmpFailureTable(blockSeparator);

        const int chunkSize = 4096;
        byte[] chunk = new byte[chunkSize];
        using var ms = new MemoryStream();
        long totalRead = 0;
        int matchLength = 0; // number of separator bytes currently matched

        while (true)
        {
            int read = s.Read(chunk, 0, chunkSize);
            if (read == 0)
                throw new EndOfStreamException("End of stream reached before the block separator was found.");

            for (int i = 0; i < read; i++)
            {
                byte b = chunk[i];
                totalRead++;
                if (totalRead > maxBytes)
                    throw new InvalidOperationException($"Block exceeds the maximum allowed size of {maxBytes} bytes before the separator was found.");

                ms.WriteByte(b);

                // KMP transition: fall back on mismatch, advance on match.
                while (matchLength > 0 && b != blockSeparator[matchLength])
                    matchLength = failure[matchLength - 1];
                if (b == blockSeparator[matchLength])
                    matchLength++;

                if (matchLength == sepLen)
                {
                    // Strip the separator from the accumulated buffer and return the block.
                    long newLength = ms.Length - sepLen;
                    if (newLength < 0) newLength = 0;
                    ms.SetLength(newLength);
                    return ms.ToArray();
                }
            }
        }
    }

    /// <summary>
    /// Builds the Knuth-Morris-Pratt failure (prefix) table for a delimiter pattern.
    /// Entry <c>i</c> holds the length of the longest proper prefix of the pattern that is also a suffix
    /// of the first <c>i + 1</c> bytes, enabling linear-time delimiter scanning without backtracking the input.
    /// </summary>
    /// <param name="pattern">The delimiter pattern to preprocess.</param>
    /// <returns>The failure table with one entry per pattern byte.</returns>
    private static int[] BuildKmpFailureTable(byte[] pattern)
    {
        int[] failure = new int[pattern.Length];
        int length = 0;
        for (int i = 1; i < pattern.Length; i++)
        {
            while (length > 0 && pattern[i] != pattern[length])
                length = failure[length - 1];
            if (pattern[i] == pattern[length])
                length++;
            failure[i] = length;
        }
        return failure;
    }
}
