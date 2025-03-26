using System;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables.Acnt;

/// <summary>
/// Represents the ACNT format 0, which provides specific accent attachment information 
/// for a TrueType font glyph.
/// </summary>
public class AcntFormat0 : AcntFormatBase
{
	/// <summary>
	/// Gets or sets the primary attachment point.
	/// </summary>
	public byte PrimaryAttachmentPoint { get; set; }

	/// <summary>
	/// Gets or sets the secondary information index.
	/// </summary>
	public byte SecondaryInfoIndex { get; set; }

	/// <summary>
	/// Reads the ACNT format 0 data from the specified reader.
	/// </summary>
	/// <param name="data">The reader from which to read the data.</param>
	public override void ReadData(Reader data)
	{
		PrimaryAttachmentPoint = data.ReadByte();
		SecondaryInfoIndex = data.ReadByte();
	}

	/// <summary>
	/// Writes the ACNT format 0 data to the specified writer.
	/// </summary>
	/// <param name="data">The writer to which to write the data.</param>
	public override void WriteData(Writer data)
	{
		data.WriteInt16(PrimaryGlyphIndex);
		data.WriteByte(PrimaryAttachmentPoint);
		data.WriteByte(SecondaryInfoIndex);
	}
}
