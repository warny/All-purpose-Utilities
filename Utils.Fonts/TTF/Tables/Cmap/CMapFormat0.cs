using System;
using Utils.IO.Serialization;
using Utils.Objects;

namespace Utils.Fonts.TTF.Tables.CMap;

/// <summary>
/// Represents CMap Format 0 for mapping characters to glyph indices using a simple 256-byte table.
/// </summary>
public class CMapFormat0 : CMapFormatBase
{
    private byte[] glyphIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="CMapFormat0"/> class.
    /// This constructor initializes an identity mapping for 256 possible characters.
    /// </summary>
    /// <param name="language">The language identifier.</param>
    protected internal CMapFormat0(short language)
        : base(0, language)
    {
        byte[] array = new byte[256];
        for (int i = 0; i < array.Length; i++)
        {
            array[i] = (byte)i;
        }
        MapBytes = array;
    }

    /// <summary>
    /// Sets the mapping for a source byte to a destination glyph index.
    /// </summary>
    /// <param name="src">The source byte (character code).</param>
    /// <param name="dest">The destination glyph index.</param>
    public virtual void SetMap(byte src, byte dest)
    {
        int i = src & 0xFF;
        glyphIndex[i] = dest;
    }

    /// <summary>
    /// Gets the total length (in bytes) of this CMap format.
    /// This format is always 262 bytes long.
    /// </summary>
    public override short Length => 262;

    /// <summary>
    /// Gets or sets the 256-byte mapping table.
    /// </summary>
    public virtual byte[] MapBytes
    {
        get => glyphIndex;
        set
        {
            value.ArgMustBeOfSize(256);
            glyphIndex = value;
        }
    }

    /// <summary>
    /// Maps a character to its corresponding glyph index.
    /// Returns 0 if the character code is outside the valid range (0 to 255).
    /// </summary>
    /// <param name="ch">The character to map.</param>
    /// <returns>The glyph index for the character.</returns>
    public override short Map(char ch)
    {
        if (ch < '\0' || ch > (char)255)
        {
            return 0;
        }
        return glyphIndex[ch];
    }

    /// <summary>
    /// Performs a reverse mapping from a glyph index to the corresponding character.
    /// Returns the null character if no matching mapping is found.
    /// </summary>
    /// <param name="s">The glyph index to reverse map.</param>
    /// <returns>The corresponding character, or '\0' if not found.</returns>
    public override char ReverseMap(short s)
    {
        for (int i = 0; i < glyphIndex.Length; i++)
        {
            if (glyphIndex[i] == s)
            {
                return (char)i;
            }
        }
        return '\0';
    }

    /// <summary>
    /// Writes the CMap Format 0 data to the specified writer.
    /// </summary>
    /// <param name="data">The writer to which the data is written.</param>
    public override void WriteData(Writer data)
    {
        data.Write<Int16>(Format);
        data.Write<Int16>(Length);
        data.Write<Int16>(Language);
        data.Write<byte[]>(MapBytes);
    }

    /// <summary>
    /// Reads the CMap Format 0 data from the specified reader.
    /// Expects exactly 262 bytes (including header fields) and 256 bytes for the mapping table.
    /// </summary>
    /// <param name="i">The expected length of the data (should be 262).</param>
    /// <param name="data">The reader from which the data is read.</param>
    /// <exception cref="ArgumentException">
    /// Thrown if the data length is not 262 or if there are insufficient bytes to read the mapping table.
    /// </exception>
    public override void ReadData(int i, Reader data)
    {
        i.ArgMustBeEqualsTo(262);
        if (data.BytesLeft < 256)
        {
            throw new ArgumentException("Wrong amount of data for CMap format 0", nameof(data));
        }
        MapBytes = data.ReadBytes(256);
    }
}
