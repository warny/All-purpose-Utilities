using System;
using System.Collections.Generic;
using System.Text;
using Utils.Fonts.TTF.Tables.Glyph;

namespace Utils.Fonts.TTF
{
	public class TrueTypeGlyph : IGlyph
	{
		private GlyphBase glyph;

		public TrueTypeGlyph(GlyphBase glyph)
		{
			this.glyph = glyph ?? throw new ArgumentNullException(nameof(glyph));
		}

		public float Width { get; }
		public float Height { get; }
		public float BaseLine { get; }

		public void ToGraphic(IGraphicConverter graphicConverter)
		{
            glyph.Render(graphicConverter);
        }
    }
}
