using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Utils.Drawing
{
	public class Segment : IDrawable
	{
		public PointF Start { get; }
		public PointF End { get; }

		public float X1 => Start.X;
		public float Y1 => Start.Y;
		public float X2 => End.X;
		public float Y2 => End.Y;
		public float Length { get; }
		public float Sin { get; }
		public float Cos { get; }

		public Segment(
			int x1, int y1, 
			int x2, int y2
		) : this(
			new PointF(x1, y1), 
			new PointF(x2, y2)
		) { }
		public Segment(
			float x1, float y1, 
			float x2, float y2
		) : this(
			new PointF(x1, y1), 
			new PointF(x2, y2)
		) { }
		public Segment(PointF start, PointF end) {
			Start = start;
			End = end;

			var x = Start.X - End.X;
			var y = Start.Y - End.Y;
			Length = (float)Math.Sqrt(x * x + y * y);
			Cos = (X2 - X1) / Length;
			Sin = (Y2 - Y1) / Length;
		}

		public IEnumerable<DrawPoint> GetPoints(bool closed, float position = 0)
		{
			int x = (int)X1;
			int y = (int)Y1;
			int dx = (int)X2 - x;
			int dy = (int)Y2 - y;

			int xinc = (dx > 0) ? 1 : -1;
			int yinc = (dy > 0) ? 1 : -1;
			dx = Math.Abs(dx);
			dy = Math.Abs(dy);

			if (dx > dy)
			{
				yield return new DrawPoint(x, y, xinc, 0, Cos, Sin, position);
				int cumul = dx / 2;
				float dl = dx / Length;
				for (int i = 1; i <= dx; i++)
				{
					int verticalDirection = 0;
					x += xinc;
					cumul += dy;
					position += dl;
					if (cumul >= dx)
					{
						verticalDirection = yinc;
						cumul -= dx;
						y += yinc;
					}
					yield return new DrawPoint(x, y, xinc, verticalDirection, Sin, Cos, position);
				}
			}
			else
			{
				yield return new DrawPoint(x, y, 0, yinc, Cos, Sin, position);
				int cumul = dy / 2;
				float dl = dy / Length;
				for (int i = 1; i <= dy; i++)
				{
					int horizontalDirection = 0;
					y += yinc;
					cumul += dx;
					position += dl;
					if (cumul >= dy)
					{
						horizontalDirection = xinc;
						cumul -= dy;
						x += xinc;
					}
					yield return new DrawPoint(x, y, horizontalDirection, yinc, Sin, Cos, position);
				}
			}
		}
		public IEnumerable<Segment> GetSegments(bool closed) => new [] { this };

		public override string ToString() => $"({Start}) ==> ({End})";

	}
}
