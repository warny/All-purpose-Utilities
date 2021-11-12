using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace Utils.Drawing
{
	public delegate T DrawingColorMap<T>(float position, float offset);
	public delegate T UVMap<T>(float U, float V);

	public interface IBrush<T> {
		void Reset();
		IEnumerable<(Point Point, T Color)> Draw(DrawPoint point, float position);
	}


	public class MapBrush<T> : IBrush<T>
	{
		public MapBrush(T drawingColor, float width = 1)
		{
			DrawingColor = (p, s) => drawingColor;
			Width = width;
		}

		public MapBrush(DrawingColorMap<T> drawingColor, float width = 1)
		{
			DrawingColor = drawingColor ?? throw new ArgumentNullException(nameof(drawingColor));
			Width = width;
		}

		public DrawingColorMap<T> DrawingColor { get; }
		public float Width { get; }

		private DrawPoint lastPoint = null;
		private Dictionary<Point, float> drawedPoints = null;

		public void Reset() {
			lastPoint = null;
			drawedPoints = new Dictionary<Point, float> ();
		}

		private bool CachePoint(Point p, float offset) {
			if (!drawedPoints.TryGetValue(p, out var drawedoffset) || drawedoffset > offset)
			{
				drawedPoints[p] = offset;
				return true;
			}
			return false;
		}

		public IEnumerable<(Point Point, T Color)> Draw(DrawPoint point, float position)
		{
			if (lastPoint == null)
			{
				lastPoint = point;
				var returnPoint = new Point(lastPoint.X, lastPoint.Y);
				if (CachePoint(returnPoint, 0))
				{
					yield return (returnPoint, DrawingColor(position, 0));
				}
				yield break;
			}

			PointF pfl1 = new PointF(lastPoint.X, lastPoint.Y);
			PointF pfl2 = new PointF(lastPoint.X, lastPoint.Y);
			PointF pfn1 = new PointF(point.X, point.Y);
			PointF pfn2 = new PointF(point.X, point.Y);
			
			Point p2 = Point.Round(pfn2);
			if (CachePoint(p2, 0))
			{
				yield return (p2, DrawingColor(position, 0));
			}

			/*
			for (int offset = 0; offset < Width; offset++)
			{
				pfl1 = new PointF(pfl1.X + lastPoint.Cos, pfl1.Y + lastPoint.Sin);
				pfl2 = new PointF(pfl2.X - lastPoint.Cos, pfl2.Y - lastPoint.Sin);
				pfn1 = new PointF(pfn1.X + point.Cos, pfn1.Y + point.Sin);
				pfn2 = new PointF(pfn2.X - point.Cos, pfn2.Y - point.Sin);

			}
			*/
		}
	}
}
