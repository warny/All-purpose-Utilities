using System;
using System.Collections.Generic;
using System.Text;
using Utils.Fonts.TTF.Tables.Glyf;

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
            if (glyph is GlyphSimple glyphSimple)
            {
                SimpleGlyphToGraphic(graphicConverter, glyphSimple);
            }
            else if (glyph is GlyphCompound glyphCompound)
            {
                CompoundGlyphToGraphic(graphicConverter, glyphCompound);
            }
            else
            {
                throw new NotSupportedException($"{glyph.GetType()} can't be displayed") ;
            }
        }

        private void SimpleGlyphToGraphic(IGraphicConverter graphicConverter, GlyphSimple glyph) {

            var p = 0;
            var c = 0;
            var first = 1;

            //while (p <  .points.length)
            //{
            //    var point = glyph.points[p];
            //    if (first === 1)
            //    {
            //        ctx.moveTo(point.x, point.y);
            //        first = 0;
            //    }
            //    else
            //    {
            //        ctx.lineTo(point.x, point.y);
            //    }

            //    if (p === glyph.contourEnds[c])
            //    {
            //        c += 1;
            //        first = 1;
            //    }

            //    p += 1;
            //}
        }
        private void CompoundGlyphToGraphic(IGraphicConverter graphicConverter, GlyphCompound glyph)
        {

        }
    }
}
