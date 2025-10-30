using System;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables.Acnt;

/// <summary>
/// Serves as the base class for ACNT (Accent) formats in a TrueType font table.
/// </summary>
public abstract class AcntFormatBase
{
    /// <summary>
    /// Gets the primary glyph index parsed from the record.
    /// </summary>
    public short PrimaryGlyphIndex { get; protected set; }

    /// <summary>
    /// Creates an ACNT format instance based on the specified description and index value.
    /// </summary>
    /// <param name="descriptionAndIndex">
    /// A 16-bit value containing a 2-bit description (in the high bits) and an index (in the lower 14 bits).
    /// </param>
    /// <returns>
    /// An instance of <see cref="AcntFormatBase"/> corresponding to the description, or <c>null</c> if the description is not recognized.
    /// </returns>
    private static AcntFormatBase CreateActn(short descriptionAndIndex) =>
        // Extract the high 2 bits as the description.
        (descriptionAndIndex >> 14) switch
        {
            0 => new AcntFormat0(),
            1 => new AcntFormat1(),
            _ => null,
        };

    /// <summary>
    /// Reads ACNT format data from the specified reader and returns an instance of <see cref="AcntFormatBase"/>.
    /// </summary>
    /// <param name="reader">The reader from which to read the ACNT format data.</param>
    /// <returns>An <see cref="AcntFormatBase"/> instance containing the parsed data.</returns>
    public static AcntFormatBase GetActn(Reader reader)
    {
        // Read a 16-bit value that contains both the description and the primary glyph index.
        var descriptionAndIndex = reader.Read<Int16>();
        var result = CreateActn(descriptionAndIndex);
        result.PrimaryGlyphIndex = (short)(descriptionAndIndex & 0x7FFF);
        result.ReadData(reader);
        return result;
    }

    /// <summary>
    /// Reads additional format-specific data from the specified reader.
    /// </summary>
    /// <param name="data">The reader from which to read the format-specific data.</param>
    public abstract void ReadData(Reader data);

    /// <summary>
    /// Writes the format-specific data to the specified writer.
    /// </summary>
    /// <param name="data">The writer to which the format-specific data is written.</param>
    public abstract void WriteData(Writer data);
}
