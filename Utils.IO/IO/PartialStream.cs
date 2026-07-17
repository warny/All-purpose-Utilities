using System;
using System.IO;

namespace Utils.IO;

/// <summary>
/// Provides a view onto a restricted portion (a "slice") of an underlying <see cref="Stream"/>.
/// This partial stream starts at a specified offset within the base stream, and has a maximum length.
/// Reading or writing beyond that partial length is not permitted.
/// 
/// The <see cref="Position"/> property reflects the offset within the partial stream, not the offset
/// in the underlying stream. All read and write operations are bounded by the specified length.
/// The underlying stream's position is restored after each operation, so external consumers of the
/// base stream see no position change (though locking is used to ensure thread safety in concurrent scenarios).
/// 
/// By default, disposing this <see cref="PartialStream"/> does not close or dispose the underlying stream.
/// </summary>
public class PartialStream : Stream
{
    private readonly Stream baseStream;
    private readonly long startOffset;
    private long partialLength;
    private long partialPosition;

    // Verifies that startOffset + length <= long.MaxValue so that any absolute position
    // within the segment (startOffset + partialPosition, with partialPosition <= partialLength)
    // can never overflow a long.
    private static void ValidateRange(long startOffset, long length, string lengthParamName)
    {
        if (length > long.MaxValue - startOffset)
            throw new ArgumentOutOfRangeException(lengthParamName,
                "The partial stream range exceeds the maximum representable stream position.");
    }

    /// <summary>
    /// Creates a new partial stream starting at the base stream's current position.
    /// </summary>
    /// <param name="baseStream">The underlying stream to read from or write to.</param>
    /// <param name="length">The length (in bytes) of the accessible segment. Must be non-negative.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="baseStream"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if the <paramref name="baseStream"/> cannot seek.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="length"/> is negative or if the current stream position plus <paramref name="length"/> would overflow a <see langword="long"/>.</exception>
    public PartialStream(Stream baseStream, long length)
    {
        ArgumentNullException.ThrowIfNull(baseStream);
        if (!baseStream.CanSeek)
            throw new ArgumentException("The underlying stream must support seeking.", nameof(baseStream));
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be non-negative.");

        long position = baseStream.Position;
        ValidateRange(position, length, nameof(length));

        this.baseStream = baseStream;
        this.startOffset = position;
        this.partialLength = length;
        this.partialPosition = 0;
    }

    /// <summary>
    /// Creates a new partial stream starting at a specified position in the base stream.
    /// </summary>
    /// <param name="baseStream">The underlying stream to read from or write to.</param>
    /// <param name="position">The absolute position in <paramref name="baseStream"/> at which to start. Must be non-negative.</param>
    /// <param name="length">The length (in bytes) of the accessible segment. Must be non-negative.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="baseStream"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if the <paramref name="baseStream"/> cannot seek.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="position"/> or <paramref name="length"/> is negative, or if their sum would overflow a <see langword="long"/>.</exception>
    public PartialStream(Stream baseStream, long position, long length)
    {
        ArgumentNullException.ThrowIfNull(baseStream);
        if (!baseStream.CanSeek)
            throw new ArgumentException("The underlying stream must support seeking.", nameof(baseStream));
        if (position < 0)
            throw new ArgumentOutOfRangeException(nameof(position), "Position must be non-negative.");
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be non-negative.");
        ValidateRange(position, length, nameof(length));

        this.baseStream = baseStream;
        this.startOffset = position;
        this.partialLength = length;
        this.partialPosition = 0;
    }

    /// <summary>
    /// Gets a value indicating whether the stream supports reading.
    /// </summary>
    public override bool CanRead => baseStream.CanRead;

    /// <summary>
    /// Gets a value indicating whether the stream supports seeking.
    /// </summary>
    public override bool CanSeek => baseStream.CanSeek;

    /// <summary>
    /// Gets a value indicating whether the stream supports writing.
    /// </summary>
    public override bool CanWrite => baseStream.CanWrite;

    /// <summary>
    /// Gets the length of this partial view of the underlying stream.
    /// Setting this value does not change the length of the underlying stream.
    /// </summary>
    public override long Length => partialLength;

    /// <summary>
    /// Gets or sets the position within this partial stream. Must be in [0, <see cref="Length"/>].
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is negative or exceeds <see cref="Length"/>.</exception>
    public override long Position
    {
        get => partialPosition;
        set
        {
            if (value < 0 || value > partialLength)
                throw new ArgumentOutOfRangeException(nameof(value),
                    $"Position must be in [0, {partialLength}].");
            partialPosition = value;
        }
    }

    /// <summary>
    /// Clears any buffers for this stream and causes any buffered data
    /// to be written to the underlying device.
    /// </summary>
    public override void Flush()
    {
        baseStream.Flush();
    }

    /// <summary>
    /// Reads a sequence of bytes from this partial stream and advances the position 
    /// within this partial stream by the number of bytes read.
    /// </summary>
    /// <param name="buffer">The buffer to store the read bytes.</param>
    /// <param name="offset">The byte offset in <paramref name="buffer"/> at which to begin storing data.</param>
    /// <param name="count">The maximum number of bytes to read.</param>
    /// <returns>The total number of bytes read into <paramref name="buffer"/>. This might be less than
    /// the number of bytes requested if that many bytes are not currently available,
    /// or if the read is constrained by the partial stream length.</returns>
    public override int Read(byte[] buffer, int offset, int count)
    {
        Stream.ValidateBufferArguments(buffer, offset, count);
        lock (baseStream)
        {
            long originalBasePosition = baseStream.Position;
            try
            {
                long maxReadable = partialLength - partialPosition;
                if (maxReadable <= 0)
                    return 0;

                if (count > maxReadable)
                    count = (int)maxReadable;

                baseStream.Position = startOffset + partialPosition;
                int bytesRead = baseStream.Read(buffer, offset, count);
                partialPosition += bytesRead;
                return bytesRead;
            }
            finally
            {
                baseStream.Position = originalBasePosition;
            }
        }
    }

    /// <summary>
    /// Sets the position within this partial stream.
    /// </summary>
    /// <param name="offset">A byte offset relative to the <paramref name="origin"/> parameter.</param>
    /// <param name="origin">A value of type <see cref="SeekOrigin"/> indicating the reference point.</param>
    /// <returns>The new position within this partial stream.</returns>
    public override long Seek(long offset, SeekOrigin origin)
    {
        long newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => checked(partialPosition + offset),
            SeekOrigin.End => checked(partialLength + offset),
            _ => throw new ArgumentOutOfRangeException(nameof(origin), "Invalid seek origin.")
        };

        if (newPosition < 0)
            throw new IOException("An attempt was made to seek before the beginning of the stream.");
        if (newPosition > partialLength)
            throw new ArgumentOutOfRangeException(nameof(offset),
                "Seek would position past the end of the partial stream.");

        partialPosition = newPosition;
        return partialPosition;
    }

    /// <summary>
    /// Sets the length of the partial stream. This does not affect the underlying stream length,
    /// but changes the maximum accessible range of this partial view.
    /// </summary>
    /// <param name="value">The desired length of this partial stream.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="value"/> is negative or if <c>startOffset + value</c> would overflow a <see langword="long"/>.</exception>
    public override void SetLength(long value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Length must be non-negative.");
        ValidateRange(startOffset, value, nameof(value));
        partialLength = value;
        if (partialPosition > partialLength)
            partialPosition = partialLength;
    }

    /// <summary>
    /// Writes a sequence of bytes to this partial stream and advances the position 
    /// within this partial stream by the number of bytes written.
    /// </summary>
    /// <param name="buffer">The buffer containing the data to write.</param>
    /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to begin copying data.</param>
    /// <param name="count">The number of bytes to write.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if writing the specified number of bytes would exceed
    /// the <see cref="Length"/> of the partial stream.</exception>
    public override void Write(byte[] buffer, int offset, int count)
    {
        Stream.ValidateBufferArguments(buffer, offset, count);
        if (count > partialLength - partialPosition)
            throw new ArgumentOutOfRangeException(nameof(count),
                "Attempted to write beyond the bounds of the partial stream.");

        lock (baseStream)
        {
            long originalBasePosition = baseStream.Position;
            try
            {
                baseStream.Position = startOffset + partialPosition;
                baseStream.Write(buffer, offset, count);
                partialPosition += count;
            }
            finally
            {
                baseStream.Position = originalBasePosition;
            }
        }
    }

    /// <summary>
    /// Disposes the partial stream. By default, this does not close or dispose the underlying stream.
    /// If you need to dispose the base stream, do so externally.
    /// </summary>
    /// <param name="disposing">
    /// True to release both managed and unmanaged resources; false to release only unmanaged resources.
    /// </param>
    protected override void Dispose(bool disposing)
    {
        // Intentionally do not dispose the underlying stream.
        // If you wish to close the base stream when this partial stream is disposed,
        // call baseStream.Dispose() or baseStream.Close() here.
        base.Dispose(disposing);
    }
}
