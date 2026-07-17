using System;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using Utils.Objects;

namespace Utils.IO.BaseEncoding;

/// <summary>
/// Writes base-encoded characters and produces binary output to an underlying <see cref="Stream"/>.
/// By default the decoder operates in strict mode: characters outside the alphabet, misplaced
/// padding and incomplete symbol groups are rejected. Pass <paramref name="strict"/> = false
/// to revert to the original permissive behaviour.
/// </summary>
public class BaseDecoderStream : TextWriter
{
    /// <summary>Gets the target binary stream.</summary>
    public Stream Stream { get; }

    /// <summary>Gets the descriptor that defines the base encoding alphabet.</summary>
    protected IBaseDescriptor BaseDescriptor { get; }

    private readonly char[] toIgnore;
    private readonly bool strict;

    /// <inheritdoc />
    public override Encoding Encoding { get; }

    private int currentValue;
    private int dataLength;
    private int sourceLength;
    private int actualTargetLength;

    // Strict-mode state: count filler characters seen so the count can be validated at Close()
    private int fillerCount;
    private bool _closed;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseDecoderStream"/> class.
    /// </summary>
    /// <param name="stream">The target binary stream.</param>
    /// <param name="baseDescriptor">Descriptor defining the base alphabet.</param>
    /// <param name="strict">
    /// When <see langword="true"/> (default), malformed input throws <see cref="FormatException"/>.
    /// When <see langword="false"/>, filler characters are silently ignored everywhere (legacy behaviour).
    /// </param>
    public BaseDecoderStream(Stream stream, IBaseDescriptor baseDescriptor, bool strict = true)
    {
        Stream = stream ?? throw new ArgumentNullException(nameof(stream));
        BaseDescriptor = baseDescriptor ?? throw new ArgumentNullException(nameof(baseDescriptor));
        this.strict = strict;

        toIgnore = BaseDescriptor.Filler is not null
            ? BaseDescriptor.Separator.Union(new[] { ' ', BaseDescriptor.Filler.Value }).ToArray()
            : BaseDescriptor.Separator.Union(new[] { ' ' }).ToArray();
    }

    /// <summary>
    /// Writes a single base character to the decoder.
    /// </summary>
    /// <param name="value">The encoded character.</param>
    /// <exception cref="ObjectDisposedException">Thrown when writing after the decoder is closed.</exception>
    /// <exception cref="FormatException">
    /// Thrown in strict mode when <paramref name="value"/> is not a valid alphabet character,
    /// or padding appears in an illegal position.
    /// </exception>
    public override void Write(char value)
    {
        ObjectDisposedException.ThrowIf(_closed, this);
        // Separator / whitespace: always ignored
        if (BaseDescriptor.Separator.Contains(value) || value == ' ')
            return;

        // Filler (padding) character
        if (BaseDescriptor.Filler.HasValue && value == BaseDescriptor.Filler.Value)
        {
            if (strict)
            {
                if (BaseDescriptor.FillerMod == 0)
                    throw new FormatException("This encoding does not use padding characters.");
                if (dataLength == 0 && sourceLength == 0)
                    throw new FormatException("Padding character at start of input.");
                fillerCount++;
            }
            // Permissive mode: ignore filler everywhere (legacy behaviour)
            return;
        }

        if (strict && fillerCount > 0)
            throw new FormatException("Data character encountered after padding.");

        sourceLength++;
        int charValue;
        try
        {
            charValue = BaseDescriptor[value];
        }
        catch (Exception)
        {
            if (strict)
                throw new FormatException($"Character '{value}' is not part of the encoding alphabet.");
            return;
        }

        currentValue = (currentValue << BaseDescriptor.BitsWidth) | charValue;
        dataLength += BaseDescriptor.BitsWidth;

        if (dataLength >= 8)
        {
            actualTargetLength++;
            dataLength -= 8;
            Stream.WriteByte((byte)((currentValue >> dataLength) & 0xFF));
        }
    }

    /// <summary>
    /// Finalizes the decoding process and flushes remaining bits.
    /// After the first call (successful or not) the instance is permanently condemned:
    /// any subsequent <see cref="Write(char)"/> call throws <see cref="ObjectDisposedException"/>.
    /// A second call to <see cref="Close"/> is always a no-op.
    /// TextWriter resources are released even when a <see cref="FormatException"/> is thrown.
    /// </summary>
    /// <exception cref="FormatException">
    /// Thrown in strict mode when the input length is incomplete, trailing bits are non-zero,
    /// or the padding count does not match the expected value.
    /// </exception>
    public override void Close()
    {
        if (_closed)
            return;
        // Condemn immediately so that any Write() after Close() — regardless of whether
        // the validation below succeeds — throws ObjectDisposedException.
        _closed = true;

        // Capture the validation exception so that cleanup exceptions from Flush/Close
        // cannot mask the original format error.
        ExceptionDispatchInfo? validationError = null;
        try
        {
            if (dataLength > 0)
            {
                int targetLength = (int)Math.Floor(sourceLength * BaseDescriptor.BitsWidth / 8d);
                if (actualTargetLength > targetLength)
                {
                    if (strict)
                    {
                        // Verify trailing bits are zero
                        int trailingBits = dataLength;
                        int mask = (1 << trailingBits) - 1;
                        if ((currentValue & mask) != 0)
                            throw new FormatException("Non-zero trailing bits in final quantum.");
                    }

                    // Align remaining bits and write the last byte
                    currentValue <<= BaseDescriptor.BitsWidth;
                    dataLength += BaseDescriptor.BitsWidth - 8;
                    Stream.WriteByte((byte)((currentValue >> dataLength) & 0xFF));
                }
            }

            // Strict: validate the exact number of padding characters
            if (strict && BaseDescriptor.Filler.HasValue && BaseDescriptor.FillerMod > 0)
            {
                int expectedFillerCount = (BaseDescriptor.FillerMod - (sourceLength % BaseDescriptor.FillerMod)) % BaseDescriptor.FillerMod;
                if (fillerCount != expectedFillerCount)
                    throw new FormatException($"Expected {expectedFillerCount} padding character(s) but received {fillerCount}.");
            }
        }
        catch (Exception ex)
        {
            validationError = ExceptionDispatchInfo.Capture(ex);
        }
        finally
        {
            try
            {
                Flush();
                base.Close();
            }
            catch when (validationError is not null)
            {
                // A cleanup exception must not replace the original validation error.
            }
        }

        // Re-throw outside the finally so the original stack trace is preserved.
        validationError?.Throw();
    }

    /// <inheritdoc />
    public override void Flush()
    {
        Stream.Flush();
    }
}
