using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using Utils.Imaging;
using Utils.Mathematics;
using System.Linq;

namespace Utils.Drawing
{
	public class DrawF<T>
	{
		public Draw<T> Draw { get; }
		public IImageAccessor<T> ImageAccessor => Draw.ImageAccessor;
		public float Top { get; }
		public float Left { get; }
		public float Right { get; }
		public float Down { get; }

		private readonly float hRatio, vRatio;
		private readonly float hPrecision, vPrecision;

		public DrawF(IImageAccessor<T> imageAccessor) : this(imageAccessor, 0, 0, imageAccessor.Width, imageAccessor.Height) { }

		public DrawF(IImageAccessor<T> imageAccessor, float top, float left, float right, float down)
		{
			Draw = new Draw<T>(imageAccessor);
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

		public PointF ComputePixelPositionF(PointF p) => ComputePixelPositionF(p.X, p.Y);
		public PointF ComputePixelPositionF(float x, float y)
		{
			return new PointF(
				(x - Left) * hRatio,
				(y - Top) * vRatio
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
			Draw.DrawPoint(p.X, p.Y, color);
		}
		#endregion
		#region Line
		public void DrawLine(PointF p1, PointF p2, T color) => DrawLine(p1.X, p1.Y, p2.X, p2.Y, color);
		public void DrawLine(float x1, float y1, float x2, float y2, T color)
		{
			var p1 = ComputePixelPosition(x1, y1);
			var p2 = ComputePixelPosition(x2, y2);
			Draw.DrawLine(p1, p2, color);
		}

		public void DrawLine(PointF p1, PointF p2, Func<float, T> color) => DrawLine(p1.X, p1.Y, p2.X, p2.Y, color);
		public void DrawLine(float x1, float y1, float x2, float y2, Func<float, T> color)
		{
			var p1 = ComputePixelPosition(x1, y1);
			var p2 = ComputePixelPosition(x2, y2);
			Draw.DrawLine(p1, p2, color);
		}

		#endregion
		#region Bezier
		public void DrawBezier(T color, params PointF[] points) => Draw.DrawBezier(color, points.Select(p => ComputePixelPosition(p)).ToArray());

		//public void DrawBezier(Func<float, T> color, params PointF[] points) => Draw.DrawBezier(color, points.Select(p => ComputePixelPosition(p)).ToArray());
		#endregion
		#region Polygon
		public void DrawPolygon(T color, IEnumerable<PointF> points) => Draw.DrawPolygon(color, points.Select(p => ComputePixelPosition(p)));
		public void DrawPolygon(T color, params PointF[] points) => Draw.DrawPolygon(color, points.Select(p => ComputePixelPosition(p)).ToArray());
		public void DrawPolygon(Func<float, T> color, IEnumerable<PointF> points) => Draw.DrawPolygon(color, points.Select(p => ComputePixelPosition(p)));
		public void DrawPolygon(Func<float, T> color, params PointF[] points) => Draw.DrawPolygon(color, points.Select(p => ComputePixelPosition(p)).ToArray());
		#endregion
		#region Circle
		public void FillCircle(PointF center, float radius, T color, double startAngle = 0, double endAngle = Math.PI * 2)
			=> Draw.DrawEllipse(ComputePixelPosition(center), (int)(radius * hRatio), (int) (radius * vRatio), color, 0, startAngle, endAngle); 
		public void DrawCircle(PointF center, float radius, Func<float, T> color, double startAngle = 0, double endAngle = Math.PI * 2)
			=> Draw.DrawEllipse(ComputePixelPosition(center), (int)(radius * hRatio), (int)(radius * vRatio), color, 0, startAngle, endAngle);

		public void DrawEllipse(PointF center, float radius1, float radius2, T color, double rotation = 0, double startAngle = 0, double endAngle = Math.PI * 2)
		{
			var points = ComputeEllipsePoints(center, radius1, radius2, rotation, startAngle, endAngle);
			Draw.DrawPolygon(color, points);
		}

		public void DrawEllipse(PointF center, float radius1, float radius2, Func<float, T> color, double rotation = 0, double startAngle = 0, double endAngle = Math.PI * 2)
		{
			var points = ComputeEllipsePoints(center, radius1, radius2, rotation, startAngle, endAngle);
			Draw.DrawPolygon(color, points);
		}

		private IEnumerable<Point> ComputeEllipsePoints(PointF centerF, float radius1, float radius2, double rotation, double startAngle, double endAngle)
		{
			Point center = ComputePixelPosition(centerF);
			double angle = endAngle - startAngle;
			if (Math.Abs(angle) > Math.PI * 2)
			{
				startAngle = 0;
				endAngle = Math.PI * 2;
			}
			var angularResolution = angle / (Math.Max(radius1, radius2) * Math.PI * 2);

			var cosR = Math.Cos(rotation);
			var sinR = Math.Sin(rotation);

			Func<double, bool> test;
			if (startAngle > endAngle)
			{
				test = alpha => alpha >= endAngle;
			}
			else
			{
				test = alpha => alpha <= endAngle;
			}

			double delta1 = Math.Sin(startAngle) * radius1;
			double delta2 = Math.Cos(startAngle) * radius2;

			int deltaX = (int)((cosR * delta1 + sinR * delta2) * hRatio);
			int deltaY = (int)((sinR * delta1 + cosR * delta2) * vRatio);

			Point lastPoint = new Point(center.X + deltaX, center.Y + deltaY);
			yield return lastPoint;

			for (double a = startAngle + angularResolution; test(a); a += angularResolution)
			{
				delta1 = Math.Sin(a) * radius1;
				delta2 = Math.Cos(a) * radius2;

				deltaX = (int)(cosR * delta1 + sinR * delta2);
				deltaY = (int)(sinR * delta1 + cosR * delta2);

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
			Draw.FillRectangle(p1.X, p1.Y, p2.X, p2.Y, color);
		}
		#endregion

	}
}
