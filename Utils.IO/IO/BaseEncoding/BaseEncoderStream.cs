using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Utils.IO.BaseEncoding;

/// <summary>
/// Stream that encodes written binary data into a base representation.
/// </summary>
public class BaseEncoderStream : Stream
{
    private int position;
    private int targetPosition;

    /// <summary>
    /// Gets the writer receiving the encoded characters.
    /// </summary>
    public TextWriter TargetWriter { get; }

    /// <summary>
    /// Gets the descriptor describing the base alphabet.
    /// </summary>
    protected IBaseDescriptor BaseDescriptor { get; }

    /// <summary>
    /// Gets the maximum number of characters per line, or -1 for unlimited.
    /// </summary>
    public int MaxDataWidth { get; }

    /// <summary>
    /// Gets the indentation applied after each separator.
    /// </summary>
    public int Indent { get; }

    private int Depth { get; }
    private int Mask { get; }

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => true;

    /// <inheritdoc />
    public override long Length { get; }

    /// <inheritdoc />
    public override long Position
    {
        get => position;
        set => throw new InvalidOperationException();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseEncoderStream"/> class.
    /// </summary>
    /// <param name="targetWriter">Writer receiving the encoded characters.</param>
    /// <param name="baseDescriptor">Descriptor defining the base alphabet.</param>
    /// <param name="maxDataWidth">Maximum number of characters per line, -1 for unlimited.</param>
    /// <param name="indent">Indentation applied after each separator.</param>
    public BaseEncoderStream(TextWriter targetWriter, IBaseDescriptor baseDescriptor, int maxDataWidth = -1, int indent = 0)
    {
        TargetWriter = targetWriter ?? throw new ArgumentNullException(nameof(targetWriter));
        BaseDescriptor = baseDescriptor ?? throw new ArgumentNullException(nameof(baseDescriptor));
        MaxDataWidth = maxDataWidth;
        Indent = indent;

        Depth = BaseDescriptor.BitsWidth;
        Mask = 0;
        for (int i = 0; i < Depth; i++)
        {
            Mask |= 1 << i;
        }
    }

    /// <inheritdoc />
    public override void Flush()
    {
        TargetWriter.Flush();
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new InvalidOperationException();
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new InvalidOperationException();
    }

    /// <inheritdoc />
    public override void SetLength(long value)
    {
        throw new InvalidOperationException();
    }

    private int value;
    private int shift;
    private int dataWidth;
    private bool _closed;
    // 0 = not disposed; 1 = disposed. Written atomically so DisposeAsync is idempotent under concurrency.
    private int _disposeState;

    /// <summary>
    /// Serializes concurrent asynchronous writes so encoder state is never interleaved across awaits.
    /// </summary>
    private readonly SemaphoreSlim _asyncGate = new(1, 1);

    /// <summary>
    /// Encodes the provided byte buffer and writes the resulting characters.
    /// </summary>
    /// <param name="buffer">Source buffer.</param>
    /// <param name="offset">Index of the first byte to read.</param>
    /// <param name="count">Number of bytes to read.</param>
    /// <exception cref="ObjectDisposedException">Thrown when writing after the stream is closed.</exception>
    public override void Write(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(_closed, this);
        Stream.ValidateBufferArguments(buffer, offset, count);
        EncodeCore(new ReadOnlySpan<byte>(buffer, offset, count));
    }

    /// <summary>
    /// Encodes the provided span of bytes and writes the resulting characters.
    /// </summary>
    /// <param name="buffer">Source span.</param>
    /// <exception cref="ObjectDisposedException">Thrown when writing after the stream is closed.</exception>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_closed, this);
        EncodeCore(buffer);
    }

    /// <summary>
    /// Core encoding loop shared by every synchronous and asynchronous write path. It consumes the source
    /// bytes eight bits at a time, emits a base symbol whenever <see cref="Depth"/> bits are available, and
    /// inserts the configured line separator and indentation when <see cref="MaxDataWidth"/> is reached.
    /// </summary>
    /// <param name="data">The binary data to encode.</param>
    private void EncodeCore(ReadOnlySpan<byte> data)
    {
        for (int idx = 0; idx < data.Length; idx++)
        {
            byte b = data[idx];
            position++;
            value = (value << 8) | b;
            shift += 8;
            while (shift >= Depth)
            {
                shift -= Depth;
                targetPosition++;
                var charIndex = (value >> shift) & Mask;
                TargetWriter.Write(BaseDescriptor[charIndex]);

                if (MaxDataWidth != -1)
                {
                    dataWidth++;
                    // wrap when the next symbol would exceed the configured width
                    if (dataWidth >= MaxDataWidth)
                    {
                        dataWidth = 0;
                        TargetWriter.Write(BaseDescriptor.Separator);
                        TargetWriter.Write(new string(' ', Indent));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Asynchronously encodes the specified buffer range. Encoding is CPU-bound and runs synchronously under
    /// a serialization gate; the underlying writer is not flushed here. Cancellation observed before encoding
    /// starts leaves the encoder state unchanged.
    /// </summary>
    /// <param name="buffer">The source buffer.</param>
    /// <param name="offset">The offset of the first byte to encode.</param>
    /// <param name="count">The number of bytes to encode.</param>
    /// <param name="cancellationToken">A token observed before encoding begins.</param>
    /// <returns>A task that completes when the data has been encoded.</returns>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => WriteAsync(new ReadOnlyMemory<byte>(buffer, offset, count), cancellationToken).AsTask();

    /// <summary>
    /// Asynchronously encodes the specified memory buffer. Encoding is CPU-bound and runs synchronously under
    /// a serialization gate; the underlying writer is not flushed here. Cancellation observed before encoding
    /// starts leaves the encoder state unchanged.
    /// </summary>
    /// <param name="buffer">The source buffer.</param>
    /// <param name="cancellationToken">A token observed before encoding begins.</param>
    /// <returns>A task that completes when the data has been encoded.</returns>
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_closed, this);
        // A cancelled call must not modify state, so check before acquiring the gate or encoding.
        cancellationToken.ThrowIfCancellationRequested();
        await _asyncGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_closed, this);
            cancellationToken.ThrowIfCancellationRequested();
            EncodeCore(buffer.Span);
        }
        finally
        {
            _asyncGate.Release();
        }
    }

    /// <summary>
    /// Asynchronously flushes the underlying writer.
    /// </summary>
    /// <param name="cancellationToken">A token observed before flushing.</param>
    /// <returns>A task that completes when the writer has been flushed.</returns>
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _asyncGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await TargetWriter.FlushAsync().ConfigureAwait(false);
        }
        finally
        {
            _asyncGate.Release();
        }
    }

    /// <summary>
    /// Finalizes the encoding process, writing remaining bits and padding.
    /// Calling this method more than once is safe; subsequent calls are no-ops.
    /// </summary>
    public override void Close()
    {
        if (_closed)
            return;
        _closed = true;

        if (shift > 0)
        {
            var charIndex = (value << (Depth - shift)) & Mask;
            TargetWriter.Write(BaseDescriptor[charIndex]);
        }

        if (BaseDescriptor.Filler is not null && targetPosition % BaseDescriptor.FillerMod != 0)
        {
            int toFill = BaseDescriptor.FillerMod - (targetPosition % BaseDescriptor.FillerMod) - 1;
            TargetWriter.Write(new string(BaseDescriptor.Filler.Value, toFill));
        }

        Flush();
        base.Close();
    }

    /// <summary>
    /// Asynchronously finalizes the encoding (flushing remaining bits and padding through <see cref="Close"/>)
    /// and then releases base resources. Safe to call more than once.
    /// The gate is acquired before finalization so that a concurrent <see cref="WriteAsync"/> in progress
    /// completes before the padding and the <see cref="SemaphoreSlim"/> are disposed.
    /// </summary>
    /// <returns>A task that completes once finalization has run.</returns>
    public override async ValueTask DisposeAsync()
    {
        // Atomically claim the dispose slot; any subsequent caller returns immediately.
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
            return;

        // Acquire the gate to ensure any concurrent async write completes before we finalize.
        // Close() itself is synchronous and idempotent; it must run while we hold the gate.
        await _asyncGate.WaitAsync().ConfigureAwait(false);
        try
        {
            Close();
        }
        finally
        {
            _asyncGate.Release();
        }
        await base.DisposeAsync().ConfigureAwait(false);
        _asyncGate.Dispose();
        GC.SuppressFinalize(this);
    }
}

