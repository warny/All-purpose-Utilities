using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Utils.IO.BaseEncoding;

/// <summary>
/// Describes a base encoding by exposing lookup tables and formatting options.
/// </summary>
public interface IBaseDescriptor
{
    /// <summary>
    /// Gets the numeric value associated with a base character.
    /// </summary>
    /// <param name="c">The character to translate.</param>
    int this[char c] { get; }

    /// <summary>
    /// Gets the base character corresponding to a numeric index.
    /// </summary>
    /// <param name="index">The index to translate.</param>
    char this[int index] { get; }

    /// <summary>
    /// Gets the number of bits represented by a single encoded character.
    /// </summary>
    int BitsWidth { get; }

    /// <summary>
    /// Gets the separator inserted when wrapping encoded data.
    /// </summary>
    string Separator { get; }

    /// <summary>
    /// Gets the optional padding character.
    /// </summary>
    char? Filler { get; }

    /// <summary>
    /// Gets the modulo value used to determine when padding is required.
    /// </summary>
    int FillerMod { get; }
}

/// <summary>
/// Converts data to and from a textual base representation.
/// </summary>
public interface IBaseConverter
{
    /// <summary>
    /// Encodes binary data into a textual representation.
    /// </summary>
    /// <param name="datas">The data to encode.</param>
    /// <param name="maxDataWidth">Maximum number of characters per line; -1 for no limit.</param>
    /// <param name="indent">Number of spaces appended after each separator.</param>
    /// <returns>The encoded text.</returns>
    string ToString(byte[] datas, int maxDataWidth = -1, int indent = 0);

    /// <summary>
    /// Decodes a textual representation into binary data.
    /// </summary>
    /// <param name="baseEncodedDatas">The encoded text.</param>
    /// <returns>The decoded binary data.</returns>
    byte[] FromString(string baseEncodedDatas);
}

/// <summary>
/// Base implementation for <see cref="IBaseDescriptor"/> and <see cref="IBaseConverter"/>.
/// </summary>
public abstract class BaseDescriptorBase : IBaseDescriptor, IBaseConverter
{
    private readonly char[] chars;
    private readonly Dictionary<char, int> reversed;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseDescriptorBase"/> class.
    /// </summary>
    /// <param name="chars">The characters used for encoding.</param>
    /// <param name="separator">The separator inserted after each line.</param>
    /// <param name="filler">Optional padding character.</param>
    /// <param name="fillerMod">Modulo value used for padding.</param>
    protected BaseDescriptorBase(string chars, string separator, char? filler = null, int fillerMod = 0)
        : this(CopyAlphabet(chars), separator, filler, fillerMod)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseDescriptorBase"/> class.
    /// </summary>
    /// <param name="chars">The characters used for encoding.</param>
    /// <param name="separator">The separator inserted after each line.</param>
    /// <param name="filler">Optional padding character.</param>
    /// <param name="fillerMod">Modulo value used for padding.</param>
    protected BaseDescriptorBase(char[] chars, string separator, char? filler = null, int fillerMod = 0)
    {
        ArgumentNullException.ThrowIfNull(chars);
        this.chars = chars.ToArray();

        int depth = 0;
        int length = this.chars.Length;
        if (length > 256)
        {
            throw new ArgumentOutOfRangeException(nameof(chars), "Transformation characters length must be less than or equal to 256.");
        }

        if (length <= 1 || (length & (length - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chars), "Transformation characters length must be a power of two.");
        }

        // Each encoded character must represent an integral number of bits, so calculate the
        // width only after verifying that the alphabet length is an exact power of two.
        while (length > 1)
        {
            length >>= 1;
            depth++;
        }

        BitsWidth = depth;
        Separator = separator ?? Environment.NewLine;
        Filler = filler;
        FillerMod = fillerMod;

        ValidateAlphabet(this.chars, Separator);
        ValidateFiller(this.chars, Separator, filler, fillerMod, BitsWidth);
        reversed = this.chars.Select((c, i) => new KeyValuePair<char, int>(c, i)).ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    /// <summary>
    /// Validates and copies a string alphabet before constructor delegation.
    /// </summary>
    /// <param name="chars">The alphabet to copy.</param>
    /// <returns>A new character array containing the alphabet.</returns>
    private static char[] CopyAlphabet(string chars)
    {
        ArgumentNullException.ThrowIfNull(chars);
        return chars.ToArray();
    }

    /// <summary>
    /// Ensures that alphabet symbols are unique and are not ignored by the decoder.
    /// </summary>
    /// <param name="chars">The alphabet to validate.</param>
    /// <param name="separator">The effective separator used by the decoder.</param>
    private static void ValidateAlphabet(char[] chars, string separator)
    {
        var symbols = new HashSet<char>();
        foreach (char character in chars)
        {
            if (!symbols.Add(character))
                throw new ArgumentException($"The encoding alphabet contains duplicate character '{character}'.", nameof(chars));

            if (character == ' ' || separator.Contains(character))
                throw new ArgumentException($"The encoding alphabet cannot contain separator or whitespace character '{character}'.", nameof(chars));
        }
    }

    /// <summary>
    /// Ensures that padding is distinguishable from data and uses a byte-aligned quantum.
    /// </summary>
    /// <param name="chars">The validated encoding alphabet.</param>
    /// <param name="separator">The effective separator used by the decoder.</param>
    /// <param name="filler">The optional padding character.</param>
    /// <param name="fillerMod">The configured padding quantum.</param>
    /// <param name="bitsWidth">The number of bits represented by an alphabet symbol.</param>
    private static void ValidateFiller(char[] chars, string separator, char? filler, int fillerMod, int bitsWidth)
    {
        if (!filler.HasValue)
        {
            // FillerMod has no effect when padding is disabled. Preserve compatibility with
            // historical descriptors that supplied an otherwise arbitrary value here.
            return;
        }

        char fillerCharacter = filler.Value;
        if (chars.Contains(fillerCharacter))
            throw new ArgumentException($"The filler character '{fillerCharacter}' cannot be part of the encoding alphabet.", nameof(filler));
        if (fillerCharacter == ' ' || separator.Contains(fillerCharacter))
            throw new ArgumentException($"The filler character '{fillerCharacter}' cannot be a separator or whitespace character.", nameof(filler));

        int expectedFillerMod = 8 / GreatestCommonDivisor(8, bitsWidth);
        if (fillerMod != expectedFillerMod)
            throw new ArgumentOutOfRangeException(nameof(fillerMod), $"FillerMod must be {expectedFillerMod} for an encoding with BitsWidth {bitsWidth}.");
    }

    /// <summary>
    /// Calculates the greatest common divisor of two positive integers using Euclid's algorithm.
    /// </summary>
    /// <param name="left">The first integer.</param>
    /// <param name="right">The second integer.</param>
    /// <returns>The greatest common divisor.</returns>
    private static int GreatestCommonDivisor(int left, int right)
    {
        while (right != 0)
        {
            int remainder = left % right;
            left = right;
            right = remainder;
        }

        return left;
    }

    /// <inheritdoc />
    public int this[char c] => reversed[c];

    /// <inheritdoc />
    public char this[int index] => chars[index];

    /// <inheritdoc />
    public int BitsWidth { get; }

    /// <inheritdoc />
    public string Separator { get; }

    /// <inheritdoc />
    public char? Filler { get; }

    /// <inheritdoc />
    public int FillerMod { get; }

    /// <inheritdoc />
    public byte[] FromString(string baseEncodedDatas)
    {
        using var target = new MemoryStream();
        using var decoderStream = new BaseDecoderStream(target, this);
        decoderStream.Write(baseEncodedDatas);
        decoderStream.Flush();
        decoderStream.Close();
        return target.ToArray();
    }

    /// <inheritdoc />
    public string ToString(byte[] datas, int maxDataWidth = -1, int indent = 0)
    {
        using var target = new StringWriter();
        using var encoderStream = new BaseEncoderStream(target, this, maxDataWidth, indent);
        encoderStream.Write(datas, 0, datas.Length);
        encoderStream.Flush();
        encoderStream.Close();
        return target.ToString();
    }
}

/// <summary>
/// Provides predefined base encoding descriptors.
/// </summary>
public static class Bases
{
    /// <summary>
    /// Descriptor for base-16 (hexadecimal) encoding.
    /// </summary>
    public class Base16Descriptor : BaseDescriptorBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Base16Descriptor"/> class.
        /// </summary>
        public Base16Descriptor() : base("0123456789ABCDEF", Environment.NewLine, null)
        {
        }
    }

    /// <summary>
    /// Descriptor for base-32 encoding.
    /// </summary>
    public class Base32Descriptor : BaseDescriptorBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Base32Descriptor"/> class.
        /// </summary>
        public Base32Descriptor() : base("ABCDEFGHIJKLMNOPQRSTUVWXYZ234567", Environment.NewLine, '=', 8)
        {
        }
    }

    /// <summary>
    /// Descriptor for base-64 encoding.
    /// </summary>
    public class Base64Descriptor : BaseDescriptorBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Base64Descriptor"/> class.
        /// </summary>
        public Base64Descriptor() : base("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/", Environment.NewLine, '=', 4)
        {
        }
    }

    /// <summary>
    /// Gets a base-16 descriptor.
    /// </summary>
    public static BaseDescriptorBase Base16 { get; } = new Base16Descriptor();

    /// <summary>
    /// Gets a base-32 descriptor.
    /// </summary>
    public static BaseDescriptorBase Base32 { get; } = new Base32Descriptor();

    /// <summary>
    /// Gets a base-64 descriptor.
    /// </summary>
    public static BaseDescriptorBase Base64 { get; } = new Base64Descriptor();
}
