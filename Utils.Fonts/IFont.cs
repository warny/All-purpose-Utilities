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
		void StartAt(float x, float y);
		void LineTo(float x, float y);
		void BezierTo(params (float x, float y)[] points);
	}
}
