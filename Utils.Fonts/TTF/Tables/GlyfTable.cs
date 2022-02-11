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
	private Glyph.GlyphBase[] glyphs;
	private LocaTable loca;
	private MaxpTable maxp;

	protected internal GlyfTable() : base(TrueTypeTableTypes.glyf) { }

	public override TrueTypeFont TrueTypeFont
	{
		get => base.TrueTypeFont;
		protected internal set
		{
			base.TrueTypeFont = value;
			loca = TrueTypeFont.GetTable<LocaTable>(TrueTypeTableTypes.loca);
			maxp = TrueTypeFont.GetTable<MaxpTable>(TrueTypeTableTypes.maxp);
		}
	}

	public override int Length => glyphs.Sum(g => g.Length);

	public virtual Glyph.GlyphBase GetGlyph(int i) => glyphs[i];

	public override void WriteData(Writer data)
	{
		foreach (var glyf in glyphs)
		{
			glyf?.WriteData(data);
		}
	}

	public override void ReadData(Reader data)
	{
		glyphs = new Glyph.GlyphBase[maxp.NumGlyphs];
		foreach ((int index, int offset, int size) in loca)
			if (size != 0)
			{
				glyphs[index] = Glyph.GlyphBase.CreateGlyf(data.Slice(offset, size), this);
			}
	}

	public override string ToString()
	{
		StringBuilder val = new StringBuilder();
		val.AppendLine($"    Glyf Table: ({glyphs.Length} glyphs)");
		val.AppendLine($"      Glyf 0: {GetGlyph(0)}");
		return val.ToString();
	}
}
