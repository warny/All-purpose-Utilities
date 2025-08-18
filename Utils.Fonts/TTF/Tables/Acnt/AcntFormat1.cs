using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables.Acnt;

/// <summary>
/// Represents ACNT Format 1, an alternate format for accent attachment data in a TrueType font.
/// In this format, no extra data is stored beyond the primary glyph index (with the high bit set).
/// </summary>
internal class AcntFormat1 : AcntFormatBase
{
	/// <summary>
	/// Reads additional format-specific data from the specified reader.
	/// For Format 1, no additional data is expected.
	/// </summary>
	/// <param name="data">The reader from which to read the data.</param>
	public override void ReadData(NewReader data)
	{
		// No additional data is stored in Format 1.
		// If the specification is extended later, implement reading of extra fields here.
		PrimaryGlyphIndex = data.ReadInt16();
	}

	/// <summary>
	/// Writes the format-specific data to the specified writer.
	/// For Format 1, writes the primary glyph index with the high bit set.
	/// </summary>
	/// <param name="data">The writer to which to write the data.</param>
	public override void WriteData(NewWriter data)
	{
		// Write the primary glyph index with the high bit set to indicate Format 1.
		data.WriteInt16((short)(PrimaryGlyphIndex | 0x8000));
		// No additional data is stored in Format 1.
		// If the specification is extended later, implement writing of extra fields here.
	}
}
