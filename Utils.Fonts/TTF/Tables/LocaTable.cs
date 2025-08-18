using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Utils.IO.Serialization;
using Utils.Objects;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// The 'loca' table stores the offsets to the locations of the glyphs in the font relative to the beginning of the 'glyf' table.
/// Its purpose is to provide quick access to the data for a particular glyph. For example, in the standard Macintosh glyph ordering,
/// the character A is the 76th glyph in a font. The 'loca' table stores the offset from the start of the 'glyf' table to the position
/// at which the data for each glyph can be found.
/// </summary>
/// <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6loca.html"/>
[TTFTable(TableTypes.Tags.LOCA, TableTypes.Tags.HEAD, TableTypes.Tags.MAXP)]
public class LocaTable : TrueTypeTable, IEnumerable<LocaRecord>
{
	private HeadTable headTable;
	private MaxpTable maxpTable;
	private int[] offsets;

	/// <summary>
	/// Gets the total number of glyphs in the font.
	/// </summary>
	public int GlyphCount => maxpTable.NumGlyphs;

	/// <summary>
	/// Gets a value indicating whether this is a long format loca table.
	/// </summary>
	public virtual bool IsLongFormat => headTable.IndexToLocFormat == 1;

	/// <summary>
	/// Initializes a new instance of the <see cref="LocaTable"/> class.
	/// </summary>
	protected internal LocaTable() : base(TableTypes.LOCA) { }

	/// <summary>
	/// Gets or sets the owning <see cref="TrueTypeFont"/>. When set, the required dependent tables are retrieved.
	/// </summary>
	public override TrueTypeFont TrueTypeFont
	{
		get => base.TrueTypeFont;
		protected internal set {
			base.TrueTypeFont = value;
			headTable = value.GetTable<HeadTable>(TableTypes.HEAD);
			maxpTable = value.GetTable<MaxpTable>(TableTypes.MAXP);
		}
	}

	/// <summary>
	/// Gets the offset and size of the glyph data for the glyph at the specified index.
	/// </summary>
	/// <param name="index">The zero-based glyph index.</param>
	/// <returns>
	/// A tuple where <c>offset</c> is the starting offset and <c>size</c> is the size (in bytes) of the glyph data.
	/// </returns>
	public (int offset, int size) this[int index]
	{
		get {
			index.ArgMustBeLesserThan(GlyphCount);
			return (offsets[index], offsets[index + 1] - offsets[index]);
		}
	}

	/// <summary>
	/// Gets the length (in bytes) of the loca table data.
	/// </summary>
	public override int Length
	{
		get {
			// In short format each offset is stored as a 16-bit value; in long format as a 32-bit value.
			return IsLongFormat ? offsets.Length << 2 : offsets.Length << 1;
		}
	}

	/// <summary>
	/// Writes the loca table data to the specified writer.
	/// </summary>
	/// <param name="data">The writer to which the table data is written.</param>
	public override void WriteData(NewWriter data)
	{
		if (IsLongFormat)
		{
			for (int i = 0; i < offsets.Length; i++)
			{
				data.WriteInt32(offsets[i], true);
			}
		}
		else
		{
			for (int i = 0; i < offsets.Length; i++)
			{
				data.WriteInt16((short)(offsets[i] >> 1), true);
			}
		}
	}

	/// <summary>
	/// Reads the loca table data from the specified reader.
	/// </summary>
	/// <param name="data">The reader from which the table data is read.</param>
	public override void ReadData(NewReader data)
	{
		if (IsLongFormat)
		{
			// Read GlyphCount + 1 offsets, each stored as an int.
			offsets = data.ReadArray<int>(GlyphCount + 1, true);
		}
		else
		{
			// Read GlyphCount + 1 offsets, each stored as a short, and convert to long format.
			var temp = data.ReadArray<short>(GlyphCount + 1, true);
			offsets = temp.Select(o => o << 1).ToArray();
		}
	}

	/// <summary>
	/// Returns an enumerator that iterates through the loca records.
	/// </summary>
	/// <returns>An enumerator of <see cref="LocaRecord"/>.</returns>
	public IEnumerator<LocaRecord> GetEnumerator()
	{
		IEnumerable<LocaRecord> EnumerateRecords()
		{
			for (int i = 0; i < offsets.Length - 1; i++)
			{
				yield return new LocaRecord(i, offsets[i], offsets[i + 1] - offsets[i]);
			}
		}
		return EnumerateRecords().GetEnumerator();
	}

	/// <summary>
	/// Returns an enumerator that iterates through the loca records.
	/// </summary>
	/// <returns>An enumerator.</returns>
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
