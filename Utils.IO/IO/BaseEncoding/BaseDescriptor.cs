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
        : this(chars.ToArray(), separator, filler, fillerMod)
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
        this.chars = chars.ToArray();
        reversed = this.chars.Select((c, i) => new KeyValuePair<char, int>(c, i)).ToDictionary(kv => kv.Key, kv => kv.Value);
        Separator = separator ?? Environment.NewLine;
        Filler = filler;
        FillerMod = fillerMod;

        int depth = 0;
        int length = this.chars.Length;
        if (length > 256)
        {
            throw new ArgumentOutOfRangeException(nameof(chars), "Transformation characters length must be less than or equal to 256.");
        }

        // Ensure the number of characters is a power of two.
        while (length > 1)
        {
            length >>= 1;
            depth++;
        }

        if (length != 1)
        {
            throw new ArgumentOutOfRangeException(nameof(chars), "Transformation characters length must be a power of two.");
        }

        BitsWidth = depth;
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
