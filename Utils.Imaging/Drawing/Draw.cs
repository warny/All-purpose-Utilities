using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using Utils.Imaging;
using Utils.Mathematics;
using System.Linq;

namespace Utils.Drawing
{
	public class Draw<T>
	{
		public IImageAccessor<T> ImageAccessor { get; }
		public float Top { get; }
		public float Left { get; }
		public float Right { get; }
		public float Down { get; }

		private readonly float hRatio, vRatio;
		private readonly float hPrecision, vPrecision;

		public Draw(IImageAccessor<T> imageAccessor) : this(imageAccessor, 0, 0, imageAccessor.Width, imageAccessor.Height) { }

		public Draw(IImageAccessor<T> imageAccessor, float top, float left, float right, float down)
		{
			ImageAccessor = imageAccessor;
			Top = top;
			Left = left;
			Right = right;
			Down = down;
			hRatio = ImageAccessor.Width / (Right - Left);
			vRatio = ImageAccessor.Height / (Down - Top);
			hPrecision = Math.Abs(1 / hRatio);
			vPrecision = Math.Abs(1 / vRatio);
		}

		public Point ComputePixelPosition(PointF p) => ComputePixelPosition(p.X, p.Y);
		public Point ComputePixelPosition(float x, float y)
		{
			return new Point(
				(int)((x - Left) * hRatio),
				(int)((y - Top) * vRatio)
			);
		}

		#region Point
		public PointF ComputePoint(Point p) => ComputePoint(p.X, p.Y);
		public PointF ComputePoint(int x, int y)
		{
			return new PointF(
				x / hRatio + Left,
				y / vRatio + Top
			);
		}

		public void DrawPoint(PointF point, T color) => DrawPoint(point.X, point.Y, color);
		public void DrawPoint(float x, float y, T color)
		{
			var p = ComputePixelPosition(x, y);
			DrawPoint(p.X, p.Y, color);
		}

		public void DrawPoint(Point point, T color) => DrawPoint(point.X, point.Y, color);
		public void DrawPoint(int x, int y, T color)
		{
			if (x.Between(0, ImageAccessor.Width) && y.Between(0, ImageAccessor.Height))
			{
				ImageAccessor[x, y] = color;
			}
		}
		#endregion
		#region Line
		public void DrawLine(PointF p1, PointF p2, T color) => DrawLine(p1.X, p1.Y, p2.X, p2.Y, color);
		public void DrawLine(float x1, float y1, float x2, float y2, T color)
		{
			var p1 = ComputePixelPosition(x1, y1);
			var p2 = ComputePixelPosition(x2, y2);
			DrawLine(p1.X, p1.Y, p2.X, p2.Y, color);
		}

		public void DrawLine(Point p1, Point p2, T color) => DrawLine(p1.X, p1.Y, p2.X, p2.Y, i => color);
		private void DrawLine(int x1, int y1, int x2, int y2, T color) => DrawLine(x1, y1, x2, y2, i => color);
		public void DrawLine(PointF p1, PointF p2, Func<float, T> color) => DrawLine(p1.X, p1.Y, p2.X, p2.Y, color);
		public void DrawLine(float x1, float y1, float x2, float y2, Func<float, T> color)
		{
			var p1 = ComputePixelPosition(x1, y1);
			var p2 = ComputePixelPosition(x2, y2);
			DrawLine(p1.X, p1.Y, p2.X, p2.Y, color);
		}

		public void DrawLine(Point p1, Point p2, Func<float, T> color) => DrawLine(p1.X, p1.Y, p2.X, p2.Y, color);
		private void DrawLine(int x1, int y1, int x2, int y2, Func<float, T> color)
		{
			const int precision = 1024;
			Func<int, float> colorGradient;

			if (Math.Abs(x1 - x2) > Math.Abs(y1 - y2))
			{
				if (x1 > x2)
				{
					(x1, y1, x2, y2) = (x2, y2, x1, y1);
					colorGradient = i => 1 - (float)(i - x1) / (float)(x2 - x1);
				}
				else
				{
					colorGradient = i => (float)(i - x1) / (float)(x2 - x1);
				}
				if (y1 == y2)
				{
					if (!y1.Between(0, ImageAccessor.Height - 1)) { return; }
					x1 = Math.Max(x1, 0);
					x2 = Math.Min(x2, ImageAccessor.Width - 1);
					for (int x = x1; x <= x2; x++)
					{
						ImageAccessor[x, y1] = color(colorGradient(x));
					}
				}
				else
				{
					int slope = precision * (y1 - y2) / (x1 - x2);
					int k = (y1 * precision - slope * x1);
					x1 = Math.Max(x1, 0);
					x2 = Math.Min(x2, ImageAccessor.Width - 1);
					for (int x = x1; x <= x2; x++)
					{
						int y = (x * slope + k) / precision;
						if (y.Between(0, ImageAccessor.Height - 1))
						{
							ImageAccessor[x, y] = color(colorGradient(x));
						}
					}
				}
			}
			else
			{
				if (y1 > y2)
				{
					(x1, y1, x2, y2) = (x2, y2, x1, y1);
					colorGradient = i => 1 - (float)(i - y1) / (float)(y2 - y1);
				}
				else
				{
					colorGradient = i => (float)(i - y1) / (float)(y2 - y1);
				}
				if (x1 == x2)
				{
					if (!x1.Between(0, ImageAccessor.Width - 1)) { return; }
					y1 = Math.Max(y1, 0);
					y2 = Math.Min(y2, ImageAccessor.Height - 1);
					for (int y = y1; y <= y2; y++)
					{
						ImageAccessor[x1, y] = color(colorGradient(y));
					}
				}
				else
				{
					int slope = precision * (x1 - x2) / (y1 - y2);
					int k = x1 * precision - slope * y1;
					y1 = Math.Max(y1, 0);
					y2 = Math.Min(y2, ImageAccessor.Height - 1);
					for (int y = y1; y <= y2; y++)
					{
						int x = (y * slope + k) / precision;
						if (x.Between(0, ImageAccessor.Width - 1))
						{
							ImageAccessor[x, y] = color(colorGradient(x));
						}
					}
				}
			}
		}
		#endregion
		#region Bezier
		private PointF ComputeBezierPoint(float position, params PointF[] points)
		{
			PointF[] computePoints(PointF[] source) {
				var target = new PointF[source.Length - 1];
				for (int i = 0; i < target.Length; i++) {
					var point1 = source[i];
					var point2 = source[i + 1];
					target[i] = new PointF(
						(1f - position) * point1.X + position * point2.X,
						(1f - position) * point1.Y + position * point2.Y
					);
				}
				return target;
			}

			var result = points;
			while (result.Length > 1)
			{
				result = computePoints(result);
			}
			return result[0];
		}

		private IEnumerable<PointF> ComputeBezierPoints(params PointF[] points)
		{
			IEnumerable<(PointF p, float position)> computeIntermediatePoints(PointF p1, float pos1, PointF p2, float pos2)
			{
				if ((p1.X - p2.X).Between(-hRatio, hRatio) && (p1.Y - p2.Y).Between(-vRatio, vRatio)) { yield break; }

				var pos = (pos1 + pos2) / 2;
				var p = ComputePixelPosition(ComputeBezierPoint(pos, points));
				foreach (var point in computeIntermediatePoints(p1, pos1, p, pos))
				{
					yield return point;
				}
				foreach (var point in computeIntermediatePoints(p, pos, p2, pos2))
				{
					yield return point;
				}
				yield return (p2, pos2);
			}

			var pstart = ComputeBezierPoint(0f, points);
			var pmid = ComputeBezierPoint(0.5f, points);
			var pend = ComputeBezierPoint(1f, points);

			yield return pstart;
			foreach (var point in computeIntermediatePoints(pstart, 0, pmid, 0.5f))
			{
				yield return point.p;
			}
			foreach (var point in computeIntermediatePoints(pmid, 0.5f, pend, 1))
			{
				yield return point.p;
			}
		}

		public void DrawBezier(T color, params Point[] points) => DrawBezier(color, points.Select(p => ComputePoint(p)).ToArray());
		public void DrawBezier(T color, params PointF[] points)
		{
			var bpoints = ComputeBezierPoints(points);
			DrawPolygon(color, bpoints);
		}

		public void DrawBezier(Func<float, T> color, params Point[] points) => DrawBezier(color, points.Select(p => ComputePoint(p)).ToArray());
		public void DrawBezier(Func<float, T> color, params PointF[] points)
		{
			var bpoints = ComputeBezierPoints(points);
			DrawPolygon(color, bpoints);
		}
		#endregion
		#region Polygon
		public void DrawPolygon(T color, IEnumerable<PointF> points) => DrawPolygon(color, points.Select(p => ComputePixelPosition(p)));
		public void DrawPolygon(T color, params PointF[] points) => DrawPolygon(color, points.Select(p => ComputePixelPosition(p)).ToArray());
		public void DrawPolygon(T color, IEnumerable<Point> points) => DrawPolygon(color, points.ToArray());
		public void DrawPolygon(T color, params Point[] points)
		{
			for (int i = 0; i < points.Length - 1; i++)
			{
				DrawLine(points[i], points[i + 1], color);
			}
		}

		public void DrawPolygon(Func<float, T> color, IEnumerable<PointF> points) => DrawPolygon(color, points.Select(p => ComputePixelPosition(p)));
		public void DrawPolygon(Func<float, T> color, params PointF[] points) => DrawPolygon(color, points.Select(p => ComputePixelPosition(p)).ToArray());
		public void DrawPolygon(Func<float, T> color, IEnumerable<Point> points) => DrawPolygon(color, points.ToArray());
		public void DrawPolygon(Func<float, T> color, params Point[] points)
		{
			float totalLength = 0;
			for (int i = 0; i < points.Length - 1; i++)
			{
				var X = points[i].X - points[i + 1].X;
				var Y = points[i].Y - points[i + 1].Y;
				totalLength += (float)Math.Sqrt(X * X + Y * Y);
			}

			float length = 0;
			for (int i = 0; i < points.Length - 1; i++)
			{
				var X = points[i].X - points[i + 1].X;
				var Y = points[i].Y - points[i + 1].Y;
				var lineLength = (float)Math.Sqrt(X * X + Y * Y);
				DrawLine(points[i], points[i + 1], f => color(f * lineLength / totalLength + length));
				length += lineLength;
			}
		}
		#endregion
		#region Circle
		public void FillCircle(Point center, int radius, T color, double startAngle = 0, double endAngle = Math.PI * 2)
		{
			var points = ComputeCirclePoints(center, radius, startAngle, endAngle);
			foreach (var point in points)
			{
				DrawLine(center, point, color);
			}
		}

		public void DrawCircle(Point center, int radius, T color, double startAngle = 0, double endAngle = Math.PI * 2)
		{
			var points = ComputeCirclePoints(center, radius, startAngle, endAngle);
			DrawPolygon(color, points.ToArray());
		}

		public void DrawCircle(Point center, int radius, Func<float, T> color, double startAngle = 0, double endAngle = Math.PI * 2)
		{
			var points = ComputeCirclePoints(center, radius, startAngle, endAngle);
			DrawPolygon(color, points.ToArray());
		}

		private IEnumerable<Point> ComputeCirclePoints(Point center, int radius, double startAngle, double endAngle)
		{
			double angle = endAngle - startAngle;
			if (Math.Abs(angle) > Math.PI * 2)
			{
				startAngle = 0;
				endAngle = Math.PI * 2;
			}
			var angularResolution = angle / (radius * Math.PI * 2);

			Func<double, bool> test;
			if (startAngle > endAngle)
			{
				test = alpha => alpha >= endAngle;
			}
			else
			{
				test = alpha => alpha <= endAngle;
			}

			List<Point> points = new List<Point>();

			int deltaX = (int)(Math.Sin(startAngle) * radius);
			int deltaY = (int)(Math.Cos(startAngle) * radius);
			Point lastPoint = new Point(center.X + deltaX, center.Y + deltaY);
			yield return lastPoint;

			for (double a = startAngle + angularResolution; test(a); a += angularResolution)
			{
				deltaX = (int)(Math.Sin(a) * radius);
				deltaY = (int)(Math.Cos(a) * radius);
				var newPoint = new Point(center.X + deltaX, center.Y + deltaY);
				if (newPoint.X == lastPoint.X && newPoint.Y == lastPoint.Y) continue;
				lastPoint = newPoint;
				yield return newPoint;
			}
		}
		#endregion
		#region Rectangle
		public void FillRectangle(RectangleF r, T color)
			=> FillRectangle(r.Top, r.Left, r.Bottom, r.Right, color);
		public void FillRectangle(PointF p1, PointF p2, T color)
			=> FillRectangle(p1.X, p1.Y, p2.X, p2.Y, color);
		public void FillRectangle(float x1, float y1, float x2, float y2, T color)
		{
			var p1 = ComputePixelPosition(x1, y1);
			var p2 = ComputePixelPosition(x2, y2);
			FillRectangle(p1.X, p1.Y, p2.X, p2.Y, color);
		}

		public void FillRectangle(Rectangle r, T color)
			=> FillRectangle(r.Top, r.Left, r.Bottom, r.Right, color);
		public void FillRectangle(Point p1, Point p2, T color)
			=> FillRectangle(p1.X, p1.Y, p2.X, p2.Y, color);
		public void FillRectangle(int x1, int y1, int x2, int y2, T color)
		{
			if (x1 > x2) { (x1, x2) = (x2, x1); }
			if (y1 > y2) { (y1, y2) = (y2, y1); }

			for (int y = y1; y <= y2; y++)
			{
				for (int x = x1; x <= x2; x++)
				{
					ImageAccessor[x, y] = color;
				}
			}
		}
		#endregion

	}
}
