using System;
using System.Collections.Generic;
using System.Text;
using Utils.IO.Serialization;
using Utils.Fonts.TTF;

namespace Utils.Fonts.TTF.Tables
{
	/// <summary>
	/// The accent attachment table (tag name: 'acnt') provides a space-efficient method of combining component glyphs into compound glyphs to form accents. 
	/// Accented glyphs are a very restricted subclass of compound glyphs. 
	/// </summary>
	/// <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6acnt.html"/>

	//[TTFTable(TrueTypeTableTypes.Tags.acnt)]
	public class AcntTable : TrueTypeTable
	{
		public AcntTable() : base(TrueTypeTableTypes.acnt) { }

		public int Version { get; set; } = 0x10000;
		public short FirstAccentGlyphIndex { get; set; }
		public short LastAccentGlyphIndex { get; set; }
		public int DescriptionOffset { get; set; }
		public int ExtensionOffset { get; set; }
		public int SecondaryOffset { get; set; }
		public Glyph.GlyphBase[] Glyphs { get; }
		public object[] Extension { get; }
		public object[] Accent { get; }

		public override void ReadData(Reader data)
		{
			Version = data.ReadInt16(true);
			FirstAccentGlyphIndex = data.ReadInt16(true);
			LastAccentGlyphIndex = data.ReadInt16(true);
			DescriptionOffset = data.ReadInt32(true);
			ExtensionOffset = data.ReadInt32(true);
			SecondaryOffset = data.ReadInt32(true);

		}

		public override void WriteData(Writer data)
		{
			
		}
	}
}
