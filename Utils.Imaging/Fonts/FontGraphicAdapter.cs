using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Utils.Drawing;

namespace Utils.Fonts
{
	public class FontGraphicAdapter<T> : IGraphicConverter
	{
		private DrawF<T> drawing;

		public FontGraphicAdapter(DrawF<T> drawing)
		{
			this.drawing = drawing;
		}

		public T Color { get; set; }

		public void Line(float x1, float y1, float x2, float y2)
		{
			drawing.DrawLine(x1, y1, x2, y2, Color);
		}

		public void Spline(params (float x, float y)[] points)
		{
			drawing.DrawBezier(Color, points.Select(p => new PointF(p.x, p.y)).ToArray());
		}
	}
}
