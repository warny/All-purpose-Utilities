using System;
using System.Collections;
using System.Collections.Generic;

namespace Utils.Fonts
{
	public interface IFont
	{
		IGlyph GetGlyph(char c);
		float GetSpacingCorrection(char defore, char after);
	}

	public interface IGlyph
	{
		float Width { get; }
		float Height { get; }
		float BaseLine { get; }
		void ToGraphic(IGraphicConverter graphicConverter);
	}

	public interface IGraphicConverter
	{
		void Line(float x1, float y1, float x2, float y2);
		void Spline(params (float x, float y)[] points);
	}
}
