using System;
using System.Runtime.CompilerServices;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// The 'loca' table stores the offsets to the locations of the glyphs in the font relative to the beginning of the 'glyf' table. Its purpose is to provide quick access 
/// to the data for a particular character. For example, in the standard Macintosh glyph ordering, the character A is the 76th glyph in a font. The 'loca' table stores 
/// the offset from the start of the 'glyf' table to the position at which the data for each glyph can be found.
/// </summary>
/// <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6loca.html"/>
[TTFTable(TrueTypeTableTypes.Tags.loca,
		TrueTypeTableTypes.Tags.head, TrueTypeTableTypes.Tags.maxp)]
public class LocaTable : TrueTypeTable
{

	private int[] offsets;
	public virtual bool IsLongFormat { get; private set; }

	protected internal LocaTable() : base(TrueTypeTableTypes.loca) { }

	public override TrueTypeFont TrueTypeFont
	{
		get => base.TrueTypeFont;
		protected internal set
		{
			base.TrueTypeFont = value;
			HeadTable headTable = value.GetTable<HeadTable>(TrueTypeTableTypes.head);
			MaxpTable maxpTable = value.GetTable<MaxpTable>(TrueTypeTableTypes.maxp);
			IsLongFormat = headTable.IndexToLocFormat == 1;
			offsets = new int[maxpTable.NumGlyphs + 1];
		}
	}

	public virtual int GetOffset(int i) => offsets[i];
	public virtual int GetSize(int i) => offsets[i + 1] - offsets[i];

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
			for (int i = 0; i < offsets.Length; i++)
			{
				offsets[i] = data.ReadInt32(true);
			}
		}
		else
		{
			for (int i = 0; i < offsets.Length; i++)
			{
				offsets[i] = data.ReadInt16(true) << 1;
			}
		}
	}
}

