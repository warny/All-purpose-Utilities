using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace Utils.Drawing
{
	public class Segment : IDrawable
	{
		public Point Start { get; }
		public Point End { get; }

		public int X1 => Start.X;
		public int Y1 => Start.Y;
		public int X2 => End.X;
		public int Y2 => End.Y;
		public float Length { get; }
		public float Sin{get; } 
		public float Cos { get; }

		public Segment(int x1, int y1, int x2, int y2) : this(new Point(x1, y1), new Point(x2, y2)) { }
		public Segment(Point start, Point end) {
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
			int x = X1;
			int y = Y1;
			int dx = X2 - X1;
			int dy = Y2 - Y1;

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
		public override string ToString()
			=> $"({Start}) ==> ({End})";
	}
}
