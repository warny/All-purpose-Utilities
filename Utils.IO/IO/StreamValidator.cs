using System;
using System.IO;

namespace Utils.IO;

/// <summary>
/// A write-only buffering stream that defers writing to a target stream until
/// <see cref="Validate"/> is called. Data written to this stream is held in an
/// internal buffer. When <see cref="Validate"/> is invoked, the buffered data is
/// flushed into the underlying target stream. If <see cref="Discard"/> is called,
/// the buffered data is discarded without being written to the target.
///
/// <remarks>
/// <para>This class does not support reading or seeking.</para>
/// <para><b>Atomicity:</b> <see cref="Validate"/> is not atomic on arbitrary streams.
/// If the target throws mid-write, some bytes may already have been written.
/// For seekable targets the stream position is rolled back on failure, making
/// retry safe. For non-seekable targets the partial write is irrecoverable;
/// callers should treat the target as corrupted if <see cref="Validate"/> throws.</para>
/// <para>The underlying stream is not closed or disposed automatically when this
/// stream is disposed.</para>
/// </remarks>
/// </summary>
public class StreamValidator : Stream
{
    /// <summary>Default internal buffer size.</summary>
    private const int DefaultInitialCapacity = 65536;

    /// <summary>Internal buffer to accumulate data before validation.</summary>
    private byte[] buffer;

    /// <summary>Number of valid bytes in <see cref="buffer"/>.</summary>
    private int length;

    /// <summary>The underlying target stream to which data is ultimately written on validation.</summary>
    private readonly Stream target;

    /// <summary>Maximum number of bytes that may be buffered.</summary>
    private readonly int maxBufferSize;

    /// <summary>
    /// Creates a new instance of <see cref="StreamValidator"/> wrapping the specified target stream.
    /// </summary>
    /// <param name="target">The underlying stream where validated data will be written.</param>
    /// <param name="maxBufferSize">
    /// Maximum number of bytes that may be buffered before a <see cref="InvalidOperationException"/> is thrown.
    /// Defaults to <see cref="int.MaxValue"/> (no practical limit).
    /// </param>
    public StreamValidator(Stream target, int maxBufferSize = int.MaxValue)
    {
        this.target = target ?? throw new ArgumentNullException(nameof(target));
        if (maxBufferSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxBufferSize), "maxBufferSize must be positive.");
        this.maxBufferSize = maxBufferSize;
        buffer = new byte[Math.Min(DefaultInitialCapacity, maxBufferSize)];
    }

    /// <summary>
    /// Gets a value indicating whether the current stream supports reading. Always <see langword="false"/>.
    /// </summary>
    public override bool CanRead => false;

    /// <summary>
    /// Gets a value indicating whether the current stream supports seeking. Always <see langword="false"/>.
    /// </summary>
    public override bool CanSeek => false;

    /// <summary>
    /// Gets a value indicating whether the current stream supports writing. Always <see langword="true"/>.
    /// </summary>
    public override bool CanWrite => true;

    /// <summary>
    /// Gets the length of the underlying target stream. 
    /// This value does not include any currently unvalidated data in the buffer.
    /// </summary>
    public override long Length => target.Length;

    /// <summary>
    /// Gets or sets the position within the stream. 
    /// Getting returns the target stream's position; setting is not supported.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown when setting this property.</exception>
    public override long Position
    {
        get => target.Position;
        set => throw new NotSupportedException("StreamValidator does not support setting the position.");
    }

    /// <summary>
    /// Does nothing, as flushing this stream does not commit the buffered data. 
    /// Use <see cref="Validate"/> to commit data.
    /// </summary>
    public override void Flush()
    {
        // Intentionally left blank. We do not write the buffer to the target
        // unless Validate is explicitly called.
    }

    /// <summary>
    /// Commits the currently buffered data to the underlying target stream and then clears the buffer.
    /// For seekable targets, the write is rolled back on failure so the caller can retry safely.
    /// For non-seekable targets, a partial write is irrecoverable; see class remarks.
    /// </summary>
    /// <exception cref="IOException">Propagated from the target stream on write failure.</exception>
    public void Validate()
    {
        if (length == 0)
            return;

        if (target.CanSeek)
        {
            long savedPosition = target.Position;
            try
            {
                target.Write(buffer, 0, length);
                length = 0;
            }
            catch
            {
                target.Position = savedPosition;
                throw;
            }
        }
        else
        {
            target.Write(buffer, 0, length);
            length = 0;
        }
    }

    /// <summary>
    /// Discards the buffered data without writing it to the underlying target stream.
    /// </summary>
    public void Discard()
    {
        length = 0;
    }

    /// <summary>
    /// Reading is not supported. Always throws <see cref="NotSupportedException"/>.
    /// </summary>
    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException("StreamValidator does not support reading.");
    }

    /// <summary>
    /// Seeking is not supported. Always throws <see cref="NotSupportedException"/>.
    /// </summary>
    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException("StreamValidator does not support seeking.");
    }

    /// <summary>
    /// Setting the length of this stream is not supported. Always throws <see cref="NotSupportedException"/>.
    /// </summary>
    public override void SetLength(long value)
    {
        throw new NotSupportedException("StreamValidator does not support SetLength.");
    }

    /// <summary>
    /// Writes data into an internal buffer. The data is not written to the underlying 
    /// target stream until <see cref="Validate"/> is called.
    /// </summary>
    /// <param name="buffer">The buffer containing the data to write.</param>
    /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to begin copying bytes.</param>
    /// <param name="count">The number of bytes to write.</param>
    public override void Write(byte[] buffer, int offset, int count)
    {
        Stream.ValidateBufferArguments(buffer, offset, count);

        int nextPosition = checked(this.length + count);
        if (nextPosition > maxBufferSize)
            throw new InvalidOperationException(
                $"Buffer limit of {maxBufferSize} bytes would be exceeded.");

        if (nextPosition > this.buffer.Length)
        {
            int newCapacity = this.buffer.Length;
            while (newCapacity < nextPosition)
            {
                newCapacity = checked(newCapacity * 2);
                if (newCapacity > maxBufferSize)
                {
                    newCapacity = maxBufferSize;
                    break;
                }
            }
            byte[] newBuffer = new byte[newCapacity];
            Array.Copy(this.buffer, newBuffer, this.length);
            this.buffer = newBuffer;
        }

        Array.Copy(buffer, offset, this.buffer, this.length, count);
        this.length = nextPosition;
    }

    /// <summary>
    /// Disposes this instance, but does not automatically validate or discard the buffered data.
    /// The underlying target stream is not closed unless you override this to do so.
    /// </summary>
    /// <param name="disposing">Indicates whether this method is being called by <c>Dispose()</c> (true) or by a finalizer (false).</param>
    protected override void Dispose(bool disposing)
    {
        // By default, we don't validate or discard the remaining buffer automatically.
        // Also, we don't close or dispose the underlying target stream, but you could
        // override this behavior if desired.

        base.Dispose(disposing);
    }
}
