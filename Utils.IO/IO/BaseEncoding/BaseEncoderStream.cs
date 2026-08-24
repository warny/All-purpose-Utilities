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
    /// Gets the maximum number of encoded representation characters on one output line, or -1 if wrapping
    /// is disabled. Ordinary alphabet symbols, the final residual symbol written by <see cref="Close"/>, and
    /// padding/filler characters all count toward this limit; the separator and indentation inserted between
    /// lines do not.
    /// </summary>
    public int MaxDataWidth { get; }

    /// <summary>
    /// Gets the number of spaces inserted immediately after <see cref="IBaseDescriptor.Separator"/>, before
    /// the first character of the next wrapped line. Has no observable effect when <see cref="MaxDataWidth"/>
    /// is -1, since no separator is ever emitted in that case.
    /// </summary>
    public int Indent { get; }

    /// <summary>Precomputed indentation text written after each inter-line separator; empty when <see cref="Indent"/> is zero.</summary>
    private readonly string indentText;

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
        set => throw new NotSupportedException();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseEncoderStream"/> class.
    /// </summary>
    /// <param name="targetWriter">Writer receiving the encoded characters.</param>
    /// <param name="baseDescriptor">Descriptor defining the base alphabet.</param>
    /// <param name="maxDataWidth">
    /// Maximum number of encoded representation characters per output line: -1 disables wrapping, and every
    /// positive value is a strict per-line maximum. Zero and values below -1 are rejected.
    /// </param>
    /// <param name="indent">Number of spaces inserted after each inter-line separator. Must be non-negative.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="targetWriter"/> or <paramref name="baseDescriptor"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxDataWidth"/> is neither -1 nor a positive value, or when
    /// <paramref name="indent"/> is negative.
    /// </exception>
    public BaseEncoderStream(TextWriter targetWriter, IBaseDescriptor baseDescriptor, int maxDataWidth = -1, int indent = 0)
    {
        TargetWriter = targetWriter ?? throw new ArgumentNullException(nameof(targetWriter));
        BaseDescriptor = baseDescriptor ?? throw new ArgumentNullException(nameof(baseDescriptor));
        if (maxDataWidth != -1 && maxDataWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxDataWidth), maxDataWidth, "Must be -1 (unlimited) or a positive value.");
        if (indent < 0)
            throw new ArgumentOutOfRangeException(nameof(indent), indent, "Must be non-negative.");
        MaxDataWidth = maxDataWidth;
        Indent = indent;
        indentText = indent > 0 ? new string(' ', indent) : string.Empty;

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
    /// <exception cref="NotSupportedException">Always thrown; this stream advertises <see cref="CanRead"/> as <see langword="false"/>.</exception>
    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always thrown; this stream advertises <see cref="CanSeek"/> as <see langword="false"/>.</exception>
    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always thrown; this stream advertises <see cref="CanSeek"/> as <see langword="false"/>.</exception>
    public override void SetLength(long value)
    {
        throw new NotSupportedException();
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
    /// bytes eight bits at a time and emits a base symbol, through <see cref="WriteEncodedCharacter"/>,
    /// whenever <see cref="Depth"/> bits are available.
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
                WriteEncodedCharacter(BaseDescriptor[charIndex]);
            }
        }
    }

    /// <summary>
    /// Writes one encoded representation character — an alphabet symbol, the final residual symbol, or a
    /// filler character — wrapping onto a new formatted line first if the current line has already reached
    /// <see cref="MaxDataWidth"/>. Because wrapping is evaluated before writing <paramref name="character"/>,
    /// the separator and indentation are emitted only between two non-empty lines, never eagerly after the
    /// last character of the output. This is the single path used by <see cref="EncodeCore"/> and
    /// <see cref="Close"/>, so ordinary symbols, the final partial symbol, and padding all obey the same
    /// line-width contract.
    /// </summary>
    /// <param name="character">The encoded representation character to write.</param>
    private void WriteEncodedCharacter(char character)
    {
        if (MaxDataWidth != -1 && dataWidth == MaxDataWidth)
        {
            TargetWriter.Write(BaseDescriptor.Separator);
            TargetWriter.Write(indentText);
            dataWidth = 0;
        }

        TargetWriter.Write(character);
        dataWidth++;
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
            WriteEncodedCharacter(BaseDescriptor[charIndex]);
        }

        if (BaseDescriptor.Filler is not null && targetPosition % BaseDescriptor.FillerMod != 0)
        {
            // toFill is the padding-quantum count, unrelated to the presentation line width; only the
            // emission of each filler character is routed through the shared wrapping-aware helper.
            int toFill = BaseDescriptor.FillerMod - (targetPosition % BaseDescriptor.FillerMod) - 1;
            for (int i = 0; i < toFill; i++)
                WriteEncodedCharacter(BaseDescriptor.Filler.Value);
        }

        Flush();
        base.Close();
    }

    /// <summary>
    /// Asynchronously finalizes the encoding (flushing remaining bits and padding through <see cref="Close"/>)
    /// and then releases base resources. Safe to call more than once.
    /// The gate is acquired before finalization so that a concurrent
    /// <see cref="WriteAsync(ReadOnlyMemory{byte}, CancellationToken)"/> in progress
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
