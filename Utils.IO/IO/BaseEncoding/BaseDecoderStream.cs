using System;
using System.IO;
using System.Linq;
using System.Text;
using Utils.Objects;

namespace Utils.IO.BaseEncoding;

/// <summary>
/// Writes base-encoded characters and produces binary output to an underlying <see cref="Stream"/>.
/// </summary>
public class BaseDecoderStream : TextWriter
{
    /// <summary>
    /// Gets the target binary stream.
    /// </summary>
    public Stream Stream { get; }

    /// <summary>
    /// Gets the descriptor that defines the base encoding alphabet.
    /// </summary>
    protected IBaseDescriptor BaseDescriptor { get; }

    private readonly char[] toIgnore;

    /// <inheritdoc />
    public override Encoding Encoding { get; }

    private int currentValue;
    private int dataLength;
    private int sourceLength;
    private int actualTargetLength;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseDecoderStream"/> class.
    /// </summary>
    /// <param name="stream">The target binary stream.</param>
    /// <param name="baseDescriptor">Descriptor defining the base alphabet.</param>
    public BaseDecoderStream(Stream stream, IBaseDescriptor baseDescriptor)
    {
        Stream = stream ?? throw new ArgumentNullException(nameof(stream));
        BaseDescriptor = baseDescriptor ?? throw new ArgumentNullException(nameof(baseDescriptor));

        toIgnore = BaseDescriptor.Filler is not null
            ? BaseDescriptor.Separator.Union(new[] { ' ', BaseDescriptor.Filler.Value }).ToArray()
            : BaseDescriptor.Separator.Union(new[] { ' ' }).ToArray();
    }

    /// <summary>
    /// Writes a single base character to the decoder.
    /// </summary>
    /// <param name="value">The encoded character.</param>
    public override void Write(char value)
    {
        if (value.In(toIgnore))
        {
            return;
        }

        sourceLength++;
        int charValue = BaseDescriptor[value];
        currentValue = (currentValue << BaseDescriptor.BitsWidth) | charValue;

        dataLength += BaseDescriptor.BitsWidth;

        // When at least one byte has been accumulated, write it to the output stream.
        if (dataLength >= 8)
        {
            actualTargetLength++;
            dataLength -= 8;
            Stream.WriteByte((byte)((currentValue >> dataLength) & 0xFF));
        }
    }

    /// <summary>
    /// Finalizes the decoding process and flushes remaining bits.
    /// </summary>
    public override void Close()
    {
        if (dataLength > 0)
        {
            int targetLength = (int)Math.Floor(sourceLength * BaseDescriptor.BitsWidth / 8d);
            if (actualTargetLength > targetLength)
            {
                // Align remaining bits and write the last byte.
                currentValue <<= BaseDescriptor.BitsWidth;
                dataLength += BaseDescriptor.BitsWidth - 8;
                Stream.WriteByte((byte)((currentValue >> dataLength) & 0xFF));
            }
        }

        Flush();
    }

    /// <inheritdoc />
    public override void Flush()
    {
        Stream.Flush();
    }
}
