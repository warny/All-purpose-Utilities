using System;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF
{

	/// <summary>
	/// The 'hmtx' table contains metric information for the horizontal layout each of the glyphs in the font. It begins with the hMetrics array. Each element in this array has 
	/// two parts: the advance width and left side bearing. The value numOfLongHorMetrics is taken from the 'hhea' (Horizontal Header) table. In a monospaced font, only one entry 
	/// is required but that entry may not be omitted.
	/// </summary>
	/// <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6hmtx.html"/>
	[TTFTable(TrueTypeTableTypes.Tags.hmtx,
		TrueTypeTableTypes.Tags.loca, TrueTypeTableTypes.Tags.maxp)]
	public class HmtxTable : TrueTypeTable
	{
		internal short[] advanceWidths;

		internal short[] leftSideBearings;

		public virtual short getAdvance(int i)
		{
			if (i < advanceWidths.Length)
			{
				return advanceWidths[i];
			}
			return advanceWidths[advanceWidths.Length - 1];
		}

		protected internal HmtxTable() : base(TrueTypeTableTypes.hmtx) { }

		public override TrueTypeFont TrueTypeFont
		{
			get => base.TrueTypeFont;
			protected internal set
			{
				base.TrueTypeFont = value;
				MaxpTable maxpTable = TrueTypeFont.GetTable<MaxpTable>(TrueTypeTableTypes.maxp);
				HheaTable hheaTable = TrueTypeFont.GetTable<HheaTable>(TrueTypeTableTypes.hhea);
				advanceWidths = new short[hheaTable.NumOfLongHorMetrics];
				leftSideBearings = new short[maxpTable.NumGlyphs];
			}
		}

		public override int Length => advanceWidths.Length * 2 + leftSideBearings.Length * 2;

		public virtual short getLeftSideBearing(int i)
		{
			return leftSideBearings[i];
		}

		public override void WriteData(Writer data)
		{
			for (int i = 0; i < leftSideBearings.Length; i++)
			{
				if (i < advanceWidths.Length)
				{
					data.WriteInt16(advanceWidths[i], true);
				}
				data.WriteInt16(leftSideBearings[i], true);
			}
		}

		public override void ReadData(Reader data)
		{
			Array.Fill<short>(this.advanceWidths, 0);
			Array.Fill<short>(this.leftSideBearings, 0);
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
}
