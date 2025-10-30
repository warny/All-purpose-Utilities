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
    private readonly long startOffset;  // The absolute position in baseStream where this partial segment begins
    private long partialLength;         // The maximum accessible length in this partial segment
    private long partialPosition;       // The current position within this partial segment

    /// <summary>
    /// Creates a new partial stream starting at the base stream's current position.
    /// The length of this partial stream is specified by <paramref name="length"/>.
    /// </summary>
    /// <param name="baseStream">The underlying stream to read from or write to.</param>
    /// <param name="length">The length (in bytes) of the accessible segment.</param>
    /// <exception cref="ArgumentException">Thrown if the <paramref name="baseStream"/> cannot seek.</exception>
    public PartialStream(Stream baseStream, long length)
    {
        this.baseStream = baseStream;
        this.startOffset = baseStream.Position;
        this.partialLength = length;
        this.partialPosition = 0;
        EnsureSeekable();
    }

    /// <summary>
    /// Creates a new partial stream starting at a specified position in the base stream.
    /// The length of this partial stream is specified by <paramref name="length"/>.
    /// </summary>
    /// <param name="baseStream">The underlying stream to read from or write to.</param>
    /// <param name="position">The position in <paramref name="baseStream"/> at which to start.</param>
    /// <param name="length">The length (in bytes) of the accessible segment.</param>
    /// <exception cref="ArgumentException">Thrown if the <paramref name="baseStream"/> cannot seek.</exception>
    public PartialStream(Stream baseStream, long position, long length)
    {
        this.baseStream = baseStream;
        this.startOffset = position;
        this.partialLength = length;
        this.partialPosition = 0;
        EnsureSeekable();
    }

    /// <summary>
    /// Checks if the underlying stream is seekable; throws an exception otherwise.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if the underlying stream is not seekable.</exception>
    private void EnsureSeekable()
    {
        if (!baseStream.CanSeek)
        {
            throw new ArgumentException("The underlying stream must support seeking.");
        }
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
    /// Gets or sets the position within this partial stream. If set beyond
    /// the bounds of the partial stream, it will be clamped to [0..Length].
    /// </summary>
    public override long Position
    {
        get => partialPosition;
        set
        {
            if (value < 0)
            {
                partialPosition = 0;
            }
            else if (value > partialLength)
            {
                partialPosition = partialLength;
            }
            else
            {
                partialPosition = value;
            }
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
        lock (baseStream)
        {
            // Temporarily store the original position of the underlying stream
            long originalBasePosition = baseStream.Position;

            // Move to the corresponding absolute position in the underlying stream
            baseStream.Position = startOffset + partialPosition;

            // Calculate how many bytes we can actually read without exceeding partialLength
            long maxReadable = partialLength - partialPosition;
            if (maxReadable <= 0)
            {
                // Already at or beyond the partial range
                baseStream.Position = originalBasePosition;
                return 0;
            }

            if (count > maxReadable)
            {
                count = (int)maxReadable;
            }

            // Perform the read
            int bytesRead = baseStream.Read(buffer, offset, count);

            // Advance the partial stream position
            partialPosition += bytesRead;

            // Restore underlying stream position
            baseStream.Position = originalBasePosition;

            return bytesRead;
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
        long newPosition;
        switch (origin)
        {
            case SeekOrigin.Begin:
                newPosition = offset;
                break;
            case SeekOrigin.Current:
                newPosition = partialPosition + offset;
                break;
            case SeekOrigin.End:
                newPosition = partialLength + offset;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(origin), "Invalid seek origin.");
        }

        // Clamp within [0..partialLength]
        if (newPosition < 0) newPosition = 0;
        if (newPosition > partialLength) newPosition = partialLength;

        partialPosition = newPosition;
        return partialPosition;
    }

    /// <summary>
    /// Sets the length of the partial stream. This does not affect the underlying stream length,
    /// but changes the maximum accessible range of this partial view.
    /// </summary>
    /// <param name="value">The desired length of this partial stream.</param>
    public override void SetLength(long value)
    {
        // Clamp current position if necessary
        if (partialPosition > value)
        {
            partialPosition = value;
        }

        partialLength = value;
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
        lock (baseStream)
        {
            long originalBasePosition = baseStream.Position;

            baseStream.Position = startOffset + partialPosition;

            // Ensure we do not exceed the partial stream length
            if (partialPosition + count > partialLength)
            {
                throw new ArgumentOutOfRangeException(nameof(count),
                    "Attempted to write beyond the bounds of the partial stream.");
            }

            baseStream.Write(buffer, offset, count);

            // Advance the partial position
            partialPosition += count;

            // Restore underlying stream position
            baseStream.Position = originalBasePosition;
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
