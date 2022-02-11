using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Utils.IO.Serialization;
using Utils.Objects;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// The 'loca' table stores the offsets to the locations of the glyphs in the font relative to the beginning of the 'glyf' table. Its purpose is to provide quick access 
/// to the data for a particular character. For example, in the standard Macintosh glyph ordering, the character A is the 76th glyph in a font. The 'loca' table stores 
/// the offset from the start of the 'glyf' table to the position at which the data for each glyph can be found.
/// </summary>
/// <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6loca.html"/>
[TTFTable(TrueTypeTableTypes.Tags.loca,
		TrueTypeTableTypes.Tags.head, TrueTypeTableTypes.Tags.maxp)]
public class LocaTable : TrueTypeTable, IEnumerable<LocaRecord>
{
	private HeadTable headTable;
	private MaxpTable maxpTable;

	private int[] offsets;
	public int GlyphCount => maxpTable.NumGlyphs;

	public virtual bool IsLongFormat => headTable.IndexToLocFormat == 1;

	protected internal LocaTable() : base(TrueTypeTableTypes.loca) { }

	public override TrueTypeFont TrueTypeFont
	{
		get => base.TrueTypeFont;
		protected internal set
		{
			base.TrueTypeFont = value;
			headTable = value.GetTable<HeadTable>(TrueTypeTableTypes.head);
			maxpTable = value.GetTable<MaxpTable>(TrueTypeTableTypes.maxp);
		}
	}

	public (int offset, int size) this[int index]
	{
		get
		{
			index.ArgMustBeLesserThan(GlyphCount);
			return (offsets[index], offsets[index + 1] - offsets[index]);
		}
	}
		
	public override int Length
	{
		get
		{
			if (!IsLongFormat)
			{
				return offsets.Length << 1;
			}
			return offsets.Length << 2;
		}
	}


	public override void WriteData(Writer data)
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

	public override void ReadData(Reader data)
	{
		if (IsLongFormat)
		{
			offsets = data.ReadArray<int>(GlyphCount + 1, true);
		}
		else
		{
			var temp = data.ReadArray<short>(GlyphCount + 1, true);
			offsets = temp.Select(o => o << 1).ToArray();
		}
	}

	public IEnumerator<LocaRecord> GetEnumerator()
	{
		IEnumerable<LocaRecord> enumerate ()
		{
			for (int i = 0; i < offsets.Length - 1; i++)
			{
				yield return new LocaRecord(i, offsets[i], offsets[i + 1] - offsets[i]);
			}
		}
		return enumerate().GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()	=> GetEnumerator();
}

