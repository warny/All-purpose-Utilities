﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using Utils.Imaging;
using Utils.Mathematics;
using System.Linq;
using Utils.Lists;
using System.Diagnostics;

namespace Utils.Drawing
{
	public class Draw<T>
	{
		public IImageAccessor<T> ImageAccessor { get; }
		public Draw(IImageAccessor<T> imageAccessor) {
			ImageAccessor = imageAccessor;
		}

		#region Point
		public void DrawPoint(Point point, T color) => DrawPoint(point.X, point.Y, color);
		public void DrawPoint(int x, int y, T color)
		{
			if (x.Between(0, ImageAccessor.Width-1) && y.Between(0, ImageAccessor.Height-1))
			{
				ImageAccessor[x, y] = color;
			}
		}

		private void DrawShape(Func<float, T> color, IDrawable drawable)
		{
			var length = drawable.Length;
			foreach (var point in drawable.GetPoints(false))
			{
				DrawPoint(point.X, point.Y, color(point.Position / length));
			}
		}

		private void FillShape1(Func<float, float, T> color, params IDrawable[] drawables)
		{
			var points = drawables.SelectMany(d => d.GetPoints(true));
			foreach (var linePoints in points.GroupBy(p => p.Y))
			{
				var orderedPoints = linePoints.OrderBy(p => p.X);
				int direction = 0;
				foreach (var pair in orderedPoints.EnumerateBy(2)) {
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

		private void FillShape2(Func<float, float, T> color, params IDrawable[] drawables)
		{
			var points = drawables.SelectMany(d => d.GetPoints(true));
			foreach (var linePoints in points.GroupBy(p => p.Y))
			{
				var orderedPoints = linePoints.OrderBy(p => p.X).ToArray();
				bool drawing = false;
				foreach (var pair in orderedPoints.EnumerateBy(2))
				{
					int y = linePoints.Key;
					if (pair[0].VerticalDirection != 0)
					{
						drawing = !drawing;
					}
					DrawPoint(pair[0].X, y, color(pair[0].X, y));
					if (drawing)
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
		public void DrawLine(Point p1, Point p2, T color) => DrawLine(p1.X, p1.Y, p2.X, p2.Y, i => color);
		public void DrawLine(int x1, int y1, int x2, int y2, T color) => DrawLine(x1, y1, x2, y2, i => color);
		public void DrawLine(Point p1, Point p2, Func<float, T> color) => DrawLine(p1.X, p1.Y, p2.X, p2.Y, color);
		public void DrawLine(int x1, int y1, int x2, int y2, Func<float, T> color)
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
			DrawShape(i => color, bezier);
		}

		public void DrawBezier(Func<float, T> color, params Point[] points) => DrawBezier(color, points.Select(p => new PointF(p.X, p.Y)).ToArray());
		internal void DrawBezier(Func<float, T> color, params PointF[] points)
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

		public void FillBezier(Func<float, float, T> color, params Point[] points) => FillBezier(color, points.Select(p => new PointF(p.X, p.Y)).ToArray());
		internal void FillBezier(Func<float, float, T> color, params PointF[] points)
		{
			var bezier = new Bezier(points);
			FillShape1(color, bezier);
		}

		#endregion
		#region Polygon
		public void DrawPolygon(T color, IEnumerable<Point> points) => DrawPolygon(color, points.ToArray());
		public void DrawPolygon(T color, params Point[] points)
		{
			Polygon polygon = new Polygon(points);
			DrawShape(i => color, polygon);
		}

		public void DrawPolygon(Func<float, T> color, IEnumerable<Point> points) => DrawPolygon(color, points.ToArray());
		public void DrawPolygon(Func<float, T> color, params Point[] points)
		{
			Polygon polygon = new Polygon(points);
			DrawShape(color, polygon);
		}

		public void FillPolygon1(T color, params Point[] points)
		{
			Polygon polygon = new Polygon(points);
			FillShape1((x, y) => color, polygon);
		}

		public void FillPolygon2(T color, params Point[] points)
		{
			Polygon polygon = new Polygon(points);
			FillShape2((x, y) => color, polygon);
		}

		#endregion
		#region Circle
		public void FillCircle(Point center, int radius, T color, double startAngle = 0, double endAngle = Math.PI * 2)
		{
			var ellipse = new Circle(center, radius, startAngle, endAngle);
			FillShape1((x, y) => color, ellipse);
		}

		public void FillCircle(Point center, int radius, Func<float, float, T> color, double startAngle = 0, double endAngle = Math.PI * 2)
		{
			var ellipse = new Circle(center, radius, startAngle, endAngle);
			FillShape1(color, ellipse);
		}

		public void DrawCircle(Point center, int radius, T color, double startAngle = 0, double endAngle = Math.PI * 2) 
			=> DrawEllipse(center, radius, radius, color, 0, startAngle, endAngle);

		public void DrawCircle(Point center, int radius, Func<float, T> color, double startAngle = 0, double endAngle = Math.PI * 2) 
			=> DrawEllipse(center, radius, radius, color, 0, startAngle, endAngle);

		public void DrawEllipse(Point center, int radius1, int radius2, T color, double orientation = 0, double startAngle = 0, double endAngle = Math.PI * 2)
		{
			var ellipse = new Circle(center, radius1, radius2, orientation, startAngle, endAngle);
			DrawShape(i=>color, ellipse);
		}

		public void DrawEllipse(Point center, int radius1, int radius2, Func<float, T> color, double orientation = 0, double startAngle = 0, double endAngle = Math.PI * 2)
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
