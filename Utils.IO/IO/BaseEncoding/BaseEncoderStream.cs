using System;
using System.IO;
using System.Linq;

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

    /// <summary>
    /// Encodes the provided byte buffer and writes the resulting characters.
    /// </summary>
    /// <param name="buffer">Source buffer.</param>
    /// <param name="offset">Index of the first byte to read.</param>
    /// <param name="count">Number of bytes to read.</param>
    public override void Write(byte[] buffer, int offset, int count)
    {
        foreach (var b in buffer.Skip(offset).Take(count))
        {
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
                    if (dataWidth > MaxDataWidth)
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
    /// Finalizes the encoding process, writing remaining bits and padding.
    /// </summary>
    public override void Close()
    {
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
}

