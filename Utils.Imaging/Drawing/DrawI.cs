using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using Utils.Imaging;
using Utils.Mathematics;
using System.Linq;
using Utils.Collections;
using System.Diagnostics;
using Utils.Objects;

namespace Utils.Drawing
{
	public class DrawI<T> : BaseDrawing<T>
	{
		public DrawI(IImageAccessor<T> imageAccessor) : base(imageAccessor) { }

		#region Point
		public void DrawPoint(Point point, T color) => DrawPoint(point.X, point.Y, color);
		public void DrawPoint(int x, int y, T color)
		{
			if (x.Between(0, ImageAccessor.Width-1) && y.Between(0, ImageAccessor.Height-1))
			{
				ImageAccessor[x, y] = color;
			}
		}

		private void DrawShape(IBrush<T> draw, IDrawable drawable, float width = 1)
		{
			draw.Reset();
			var length = drawable.Length;

			foreach (var point in drawable.GetPoints(false))
			{
				foreach (var drawPoint in draw.Draw(point, point.Position / length))
				{
					DrawPoint(drawPoint.Point.X, drawPoint.Point.Y, drawPoint.Color);
				}
			}
		}

		public void FillShape1(UVMap<T> color, params IDrawable[] drawables)
			=> FillShape1(color, (IEnumerable<IDrawable>)drawables);

		public void FillShape1(UVMap<T> color, IEnumerable<IDrawable> drawables)
		{
			var points = drawables.SelectMany(d => d.GetPoints(true));
			foreach (var linePoints in points.GroupBy(p => p.Y))
			{
				var orderedPoints = linePoints.OrderBy(p => p.X);
				int direction = 0;
				foreach (var pair in orderedPoints.SlideEnumerateBy(2)) {
					int y = linePoints.Key;
					direction += pair[0].VerticalDirection;
					DrawPoint(pair[0].X, y, color(pair[0].X, y));
					if (direction != 0) {
						for (int x = pair[0].X; x <= pair[1].X; x++)
						{
							DrawPoint(x, y, color(x, y));
						}
					}
				}
			}
		}

		public void FillShape2(UVMap<T> color, params IDrawable[] drawables) 
			=> FillShape2(color, (IEnumerable<IDrawable>)drawables);

		public void FillShape2(UVMap<T> color, IEnumerable<IDrawable> drawables)
		{
			var points = drawables.SelectMany(d => d.GetPoints(true));
			foreach (var linePoints in points.GroupBy(p => p.Y))
			{
				var orderedPoints = linePoints.OrderBy(p => p.X);
				int direction = 0;
				foreach (var pair in orderedPoints.SlideEnumerateBy(2))
				{
					int y = linePoints.Key;
					direction += pair[0].VerticalDirection;
					DrawPoint(pair[0].X, y, color(pair[0].X, y));
					if (MathEx.Mod(direction, 2) != 0)
					{
						for (int x = pair[0].X; x <= pair[1].X; x++)
						{
							DrawPoint(x, y, color(x, y));
						}
					}
				}
			}
		}

		#endregion
		#region Line
		public void DrawLine(Point p1, Point p2, T color) => DrawLine(p1.X, p1.Y, p2.X, p2.Y, new MapBrush<T>(color));
		public void DrawLine(int x1, int y1, int x2, int y2, T color) => DrawLine(x1, y1, x2, y2, new MapBrush<T>(color));
		public void DrawLine(Point p1, Point p2, IBrush<T> color) => DrawLine(p1.X, p1.Y, p2.X, p2.Y, color);
		public void DrawLine(int x1, int y1, int x2, int y2, IBrush<T> color)
		{
			var segment = new Segment(x1, y1, x2, y2);
			DrawShape(color, segment);
		}
		#endregion
		#region Bezier
		public void DrawBezier(T color, params Point[] points) => DrawBezier(color, points.Select(p => new PointF(p.X, p.Y)).ToArray());
		internal void DrawBezier(T color, params PointF[] points)
		{
			var bezier = new Bezier(points);
			DrawShape(new MapBrush<T>(color), bezier);
		}

		public void DrawBezier(MapBrush<T> color, params Point[] points) => DrawBezier(color, points.Select(p => new PointF(p.X, p.Y)).ToArray());
		internal void DrawBezier(MapBrush<T> color, params PointF[] points)
		{
			var bezier = new Bezier(points);
			DrawShape(color, bezier);
		}

		public void FillBezier(T color, params Point[] points) => FillBezier(color, points.Select(p => new PointF(p.X, p.Y)).ToArray());
		internal void FillBezier(T color, params PointF[] points)
		{
			var bezier = new Bezier(points);
			FillShape1((x,y) => color, bezier);
		}

		public void FillBezier(UVMap<T> color, params Point[] points) => FillBezier(color, points.Select(p => new PointF(p.X, p.Y)).ToArray());
		internal void FillBezier(UVMap<T> color, params PointF[] points)
		{
			var bezier = new Bezier(points);
			FillShape1(color, bezier);
		}

		#endregion
		#region Polygon
		public void DrawPolygon(T color, IEnumerable<Point> points) => DrawPolygon(color, points.ToArray());
		public void DrawPolygon(T color, params Point[] points)
		{
			Polygon polygon = new Polygon(points.Select(p=>(PointF)p));
			DrawShape(new MapBrush<T>(color), polygon);
		}

		public void DrawPolygon(IBrush<T> color, IEnumerable<Point> points) => DrawPolygon(color, points.ToArray());
		public void DrawPolygon(IBrush<T> color, params Point[] points)
		{
			Polygon polygon = new Polygon(points.Select(p => (PointF)p));
			DrawShape(color, polygon);
		}

		public void FillPolygon1(T color, params Point[] points)
		{
			Polygon polygon = new Polygon(points.Select(p => (PointF)p));
			FillShape1((x, y) => color, polygon);
		}

		public void FillPolygon2(T color, params Point[] points)
		{
			Polygon polygon = new Polygon(points.Select(p => (PointF)p));
			FillShape2((x, y) => color, polygon);
		}

		#endregion
		#region Circle
		public void FillCircle(Point center, int radius, T color, double startAngle = 0, double endAngle = Math.PI * 2)
		{
			var ellipse = new Circle(center, radius, startAngle, endAngle);
			FillShape1((x, y) => color, ellipse);
		}

		public void FillCircle(Point center, int radius, UVMap<T> color, double startAngle = 0, double endAngle = Math.PI * 2)
		{
			var ellipse = new Circle(center, radius, startAngle, endAngle);
			FillShape1(color, ellipse);
		}

		public void DrawCircle(Point center, int radius, T color, double startAngle = 0, double endAngle = Math.PI * 2) 
			=> DrawEllipse(center, radius, radius, color, 0, startAngle, endAngle);

		public void DrawCircle(Point center, int radius, IBrush<T> color, double startAngle = 0, double endAngle = Math.PI * 2) 
			=> DrawEllipse(center, radius, radius, color, 0, startAngle, endAngle);

		public void DrawEllipse(Point center, int radius1, int radius2, T color, double orientation = 0, double startAngle = 0, double endAngle = Math.PI * 2)
		{
			var ellipse = new Circle(center, radius1, radius2, orientation, startAngle, endAngle);
			DrawShape(new MapBrush<T>(color), ellipse);
		}

		public void DrawEllipse(Point center, int radius1, int radius2, IBrush<T> color, double orientation = 0, double startAngle = 0, double endAngle = Math.PI * 2)
		{
			var ellipse = new Circle(center, radius1, radius2, orientation, startAngle, endAngle);
			DrawShape(color, ellipse);
		}

		#endregion
		#region Rectangle
		public void FillRectangle(Rectangle r, T color)
			=> FillRectangle(r.Top, r.Left, r.Bottom, r.Right, color);
		public void FillRectangle(Point p1, Point p2, T color)
			=> FillRectangle(p1.X, p1.Y, p2.X, p2.Y, color);
		public void FillRectangle(int x1, int y1, int x2, int y2, T color)
		{
			FillPolygon1(color, new Point(x1, y1), new Point(x2, y1), new Point(x2, y2), new Point(x1, y2));
		}
		#endregion

	}
}
