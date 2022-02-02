using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// The 'glyf' table contains the data that defines the appearance of the glyphs in the font. This includes specification of the points that describe the contours 
/// that make up a glyph outline and the instructions that grid-fit that glyph. The 'glyf' table supports the definition of simple glyphs and compound glyphs, 
/// that is, glyphs that are made up of other glyphs.
/// </summary>
/// <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6glyf.html"/>
[TTFTable(TrueTypeTableTypes.Tags.glyf, 
	TrueTypeTableTypes.Tags.loca, TrueTypeTableTypes.Tags.maxp)]
public class GlyfTable : TrueTypeTable
{
	private Glyf.GlyphBase[] glyfs;
	private LocaTable loca;
	private MaxpTable maxp;

	public virtual Glyf.GlyphBase GetGlyph(int i) => glyfs[i];

	protected internal GlyfTable() : base(TrueTypeTableTypes.glyf) { }

	public override TrueTypeFont TrueTypeFont
	{
		get => base.TrueTypeFont;
		protected internal set
		{
			base.TrueTypeFont = value;
			loca = TrueTypeFont.GetTable<LocaTable>(TrueTypeTableTypes.loca);
			maxp = TrueTypeFont.GetTable<MaxpTable>(TrueTypeTableTypes.maxp);
			int numGlyphs = maxp.NumGlyphs;
			glyfs = new Glyf.GlyphBase[numGlyphs];
		}
	}

	public override int Length => glyfs.Sum(g => g.Length);

	public override void WriteData(Writer data)
	{
		foreach (var glyf in glyfs)
		{
			glyf?.WriteData(data);
		}
	}

	public override void ReadData(Reader data)
	{
		for (int i = 0; i < glyfs.Length; i++)
		{
			int offset = loca.GetOffset(i);
			int size = loca.GetSize(i);
			if (size != 0)
			{
				glyfs[i] = Glyf.GlyphBase.CreateGlyf(data.Slice(offset, size));
			}
		}
	}

	public override string ToString()
	{
		StringBuilder val = new StringBuilder();
		val.AppendLine($"    Glyf Table: ({glyfs.Length} glyphs)");
		val.AppendLine($"      Glyf 0: {(object)GetGlyph(0)}");
		return val.ToString();
	}
}
