using System;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// The 'hmtx' table contains metric information for the horizontal layout of each glyph in the font.
/// It begins with the hMetrics array, where each entry consists of the advance width and left side bearing for a glyph.
/// The number of long horizontal metrics is specified in the 'hhea' table, and in a monospaced font,
/// only one entry is required (but must still be present).
/// </summary>
/// <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6hmtx.html"/>
[TTFTable(TableTypes.Tags.HMTX, TableTypes.Tags.LOCA, TableTypes.Tags.MAXP)]
public class HmtxTable : TrueTypeTable
{
	/// <summary>
	/// Array containing the advance widths for the glyphs.
	/// </summary>
	internal short[] advanceWidths;

	/// <summary>
	/// Array containing the left side bearings for the glyphs.
	/// </summary>
	internal short[] leftSideBearings;

	/// <summary>
	/// Initializes a new instance of the <see cref="HmtxTable"/> class.
	/// </summary>
	protected internal HmtxTable() : base(TableTypes.HMTX) { }

	/// <summary>
	/// Gets or sets the owning TrueType font and retrieves the dependent tables needed to initialize this table.
	/// </summary>
	public override TrueTypeFont TrueTypeFont
	{
		get => base.TrueTypeFont;
		protected internal set {
			base.TrueTypeFont = value;
			MaxpTable maxpTable = TrueTypeFont.GetTable<MaxpTable>(TableTypes.MAXP);
			HheaTable hheaTable = TrueTypeFont.GetTable<HheaTable>(TableTypes.HHEA);
			advanceWidths = new short[hheaTable.NumOfLongHorMetrics];
			leftSideBearings = new short[maxpTable.NumGlyphs];
		}
	}

	/// <summary>
	/// Gets the total length (in bytes) of the hmtx table.
	/// </summary>
	public override int Length => (advanceWidths.Length * 2) + (leftSideBearings.Length * 2);

	/// <summary>
	/// Returns the advance width for the glyph at the specified index.
	/// If the index is beyond the advanceWidths array, the last available advance width is returned.
	/// </summary>
	/// <param name="i">The glyph index.</param>
	/// <returns>The advance width of the glyph.</returns>
	public virtual short GetAdvance(int i)
	{
		if (i < advanceWidths.Length)
		{
			return advanceWidths[i];
		}
		return advanceWidths[advanceWidths.Length - 1];
	}

	/// <summary>
	/// Returns the left side bearing for the glyph at the specified index.
	/// </summary>
	/// <param name="i">The glyph index.</param>
	/// <returns>The left side bearing of the glyph.</returns>
	public virtual short GetLeftSideBearing(int i)
	{
		return leftSideBearings[i];
	}

	/// <summary>
	/// Writes the hmtx table data to the specified writer.
	/// For each glyph, if an advance width is available, it is written followed by the left side bearing.
	/// </summary>
	/// <param name="data">The writer to which the data is written.</param>
	public override void WriteData(Writer data)
	{
		// The first part: advance widths for the first 'n' glyphs
		// (where n = number of long horizontal metrics from the hhea table)
		for (int i = 0; i < leftSideBearings.Length; i++)
		{
			if (i < advanceWidths.Length)
			{
				data.WriteInt16(advanceWidths[i], true);
			}
			data.WriteInt16(leftSideBearings[i], true);
		}
	}

	/// <summary>
	/// Reads the hmtx table data from the specified reader.
	/// </summary>
	/// <param name="data">The reader from which the data is read.</param>
	public override void ReadData(Reader data)
	{
		// Initialize arrays with zero.
		Array.Fill(advanceWidths, (short)0);
		Array.Fill(leftSideBearings, (short)0);

		// Read metric data for each glyph.
		for (int i = 0; i < leftSideBearings.Length; i++)
		{
			if (i < advanceWidths.Length)
			{
				advanceWidths[i] = data.ReadInt16(true);
			}
			leftSideBearings[i] = data.ReadInt16(true);
		}
	}
}
