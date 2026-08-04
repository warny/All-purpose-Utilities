using System;
using System.Collections.Generic;
using System.IO;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables.CMap;

/// <summary>
/// Represents the base class for CMap (character to glyph mapping) formats in a TrueType font.
/// </summary>
public abstract class CMapFormatBase
{
    /// <summary>
    /// Gets the format type.
    /// </summary>
    public virtual short Format { get; private set; }

    /// <summary>
    /// Gets the language identifier.
    /// </summary>
    public virtual short Language { get; private set; }

    /// <summary>
    /// Gets the length (in bytes) of the CMap format data.
    /// </summary>
    public abstract int Length { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CMapFormatBase"/> class.
    /// </summary>
    /// <param name="format">The format type.</param>
    /// <param name="language">The language identifier.</param>
    protected CMapFormatBase(short format, short language)
    {
        Format = format;
        Language = language;
    }

    /// <summary>
    /// Creates an instance of a CMap format based on the specified format type.
    /// </summary>
    /// <param name="format">The format type.</param>
    /// <param name="language">The language identifier.</param>
    /// <returns>An instance of a derived <see cref="CMapFormatBase"/> class.</returns>
    /// <exception cref="NotSupportedException">Thrown if the format is not supported.</exception>
    public static CMapFormatBase CreateCMap(short format, short language) => format switch
    {
        0 => new CMapFormat0(language),
        4 => new CMapFormat4(language),
        _ => throw new NotSupportedException($"CMap format {format} is not supported.")
    };

    /// <summary>
    /// Maps a character to its corresponding glyph index.
    /// </summary>
    /// <param name="ch">The character to map.</param>
    /// <returns>The glyph index for the character.</returns>
    public abstract short Map(char ch);

    /// <summary>
    /// Performs a reverse mapping from a glyph index to the corresponding character.
    /// </summary>
    /// <param name="s">The glyph index.</param>
    /// <returns>The character corresponding to the glyph index.</returns>
    public abstract char ReverseMap(short s);

    /// <summary>
    /// Reads the format-specific data from the specified reader.
    /// </summary>
    /// <param name="i">The length (in bytes) of the data to read.</param>
    /// <param name="data">The reader to read data from.</param>
    public abstract void ReadData(int i, Reader data);

    /// <summary>
    /// Writes the format-specific data to the specified writer.
    /// </summary>
    /// <param name="data">The writer to write data to.</param>
    public abstract void WriteData(Writer data);

    /// <summary>
    /// Reads a CMap format record from the provided reader.
    /// </summary>
    /// <remarks>
    /// <paramref name="data"/> is bounded only to "the rest of the 'cmap' table from this
    /// subtable's own offset" -- not to any assumption about where the next subtable in the
    /// directory begins. The format's own declared <c>length</c> field (read here, per the TrueType
    /// spec's own per-subtable header) is the sole source of truth for how many bytes belong to
    /// this subtable: after validating it fits within what remains of <paramref name="data"/>, the
    /// format-specific reader is handed a reader re-bounded to exactly that <c>length</c> (not to
    /// whatever was left in <paramref name="data"/>), so it can never read into padding or an
    /// unrelated, non-contiguous subtable that merely happens to follow in the file.
    /// </remarks>
    /// <param name="data">The reader from which to read the CMap data.</param>
    /// <returns>An instance of <see cref="CMapFormatBase"/> containing the parsed data.</returns>
    public static CMapFormatBase GetMap(Reader data)
    {
        const int HeaderLength = 6; // format(UInt16) + length(UInt16) + language(UInt16)
        long available = data.BytesLeft;
        if (available < HeaderLength)
        {
            throw new InvalidDataException(
                $"cmap subtable header does not fit within the {available} byte(s) available from its offset to the end of cmap.");
        }
        var format = data.Read<Int16>();
        var length = data.Read<UInt16>();
        var language = data.Read<Int16>();
        if (length < HeaderLength || length > available)
        {
            throw new InvalidDataException(
                $"cmap subtable declares length {length}, which does not fit within the {available} byte(s) available from its offset to the end of cmap.");
        }
        // Re-bound to exactly [subtable start, subtable start + length), then seek past the header
        // already consumed above: format-specific readers count positions from the subtable's own
        // start (per the TrueType spec's idRangeOffset convention), matching `data`'s own frame.
        Reader exact = data.Slice(0, length);
        exact.Position = HeaderLength;
        CMapFormatBase cMap = CreateCMap(format, language);
        cMap?.ReadData(length, exact);
        return cMap;
    }

    /// <summary>
    /// Returns a string representation of the CMap format.
    /// </summary>
    /// <returns>A string describing the format, length, and language.</returns>
    public override string ToString() => $"        format: {Format}, length: {Length}, language: {Language}";
}
