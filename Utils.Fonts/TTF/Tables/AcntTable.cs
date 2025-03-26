using System;
using System.Text;
using Utils.IO.Serialization;
using Utils.Fonts.TTF;
using Utils.Fonts.TTF.Tables.Glyf;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// The accent attachment table (tag: 'acnt') provides a space‑efficient method of combining component glyphs into compound glyphs to form accents.
/// Accented glyphs are a very restricted subclass of compound glyphs.
/// </summary>
/// <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6acnt.html"/>
[TTFTable(TableTypes.Tags.ACNT)]
public class AcntTable : TrueTypeTable
{
	/// <summary>
	/// Initializes a new instance of the <see cref="AcntTable"/> class.
	/// </summary>
	public AcntTable() : base(TableTypes.ACNT) { }

	/// <summary>
	/// Gets or sets the table version.
	/// </summary>
	public short Version { get; set; } = 0x0001;

	/// <summary>
	/// Gets or sets the index of the first accent glyph.
	/// </summary>
	public short FirstAccentGlyphIndex { get; set; }

	/// <summary>
	/// Gets or sets the index of the last accent glyph.
	/// </summary>
	public short LastAccentGlyphIndex { get; set; }

	/// <summary>
	/// Gets or sets the offset to the description sub-table.
	/// </summary>
	public int DescriptionOffset { get; set; }

	/// <summary>
	/// Gets or sets the offset to the extension sub-table.
	/// </summary>
	public int ExtensionOffset { get; set; }

	/// <summary>
	/// Gets or sets the offset to the secondary sub-table.
	/// </summary>
	public int SecondaryOffset { get; set; }

	/// <summary>
	/// Gets or sets the array of glyph definitions for accented glyphs.
	/// (The structure of these entries requires further specification.)
	/// </summary>
	public GlyphBase[] Glyphs { get; set; }

	/// <summary>
	/// Gets or sets the extension data.
	/// (The type and structure of this array is not yet specified.)
	/// </summary>
	public object[] Extension { get; set; }

	/// <summary>
	/// Gets or sets the accent attachment data.
	/// (The type and structure of this array is not yet specified.)
	/// </summary>
	public object[] Accent { get; set; }

	/// <summary>
	/// Reads the ACNT table data from the specified reader.
	/// </summary>
	/// <param name="data">The reader from which to read the ACNT table data.</param>
	public override void ReadData(Reader data)
	{
		Version = data.ReadInt16(true);
		FirstAccentGlyphIndex = data.ReadInt16(true);
		LastAccentGlyphIndex = data.ReadInt16(true);
		DescriptionOffset = data.ReadInt32(true);
		ExtensionOffset = data.ReadInt32(true);
		SecondaryOffset = data.ReadInt32(true);

		// TODO: Implement reading of the Glyphs, Extension, and Accent sub-tables based on the offsets.
	}

	/// <summary>
	/// Writes the ACNT table data to the specified writer.
	/// </summary>
	/// <param name="data">The writer to which the ACNT table data is written.</param>
	public override void WriteData(Writer data)
	{
		data.WriteInt16(Version, true);
		data.WriteInt16(FirstAccentGlyphIndex, true);
		data.WriteInt16(LastAccentGlyphIndex, true);
		data.WriteInt32(DescriptionOffset, true);
		data.WriteInt32(ExtensionOffset, true);
		data.WriteInt32(SecondaryOffset, true);

		// TODO: Implement writing of the Glyphs, Extension, and Accent sub-tables.
	}
}
