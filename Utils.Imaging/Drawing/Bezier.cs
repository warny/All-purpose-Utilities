using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Utils.Collections;
using Utils.Mathematics;
using static System.Net.Mime.MediaTypeNames;

namespace Utils.Drawing
{
	public class Bezier : IDrawable
	{
		public PointF[] Points { get; }

		private Segment[] segments;
		private float length = -1;

		public Bezier(params Point[] points)
			: this(points.Select(p => new PointF(p.X, p.Y)).ToArray()) { }
		public Bezier(params PointF[] points)
		{
			Points = points;
		}

		public float Length
		{
			get
			{
				if (length == -1)
				{
					length = Segments.Sum(s => s.Length);
				}
				return length;
			}
		}

		/// <summary>
		/// Simplification of shape to segments
		/// </summary>
		private Segment[] Segments {
			get {
				if (segments is null)
				{

					List<Segment> result = new List<Segment>();
					var computedPoints = ComputeBezierPoints(Points.Select(p => new PointF(p.X, p.Y)).ToArray());

					foreach (var points in computedPoints.SlideEnumerateBy(2))
					{
						var start = Point.Round(points[0]);
						var end = Point.Round(points[1]);
						if (start.X == end.X && start.Y == end.Y) continue;
						result.Add(new Segment(start, end));
					}
					segments = result.ToArray();
				}
				return segments;
			}
		}

		/// <summary>
		/// Rasterization to oriented points coordinates
		/// </summary>
		/// <param name="position"></param>
		/// <returns></returns>
		public IEnumerable<DrawPoint> GetPoints(bool closed, float position = 0)
		{
			foreach (var segment in GetSegments(closed))
			{
				foreach (var drawPoint in segment.GetPoints(false, position))
				{
					position = drawPoint.Position;
					yield return drawPoint;
				}
			}
		}

		public IEnumerable<Segment> GetSegments(bool closed)
		{
			foreach (var segment in Segments)
			{
				yield return segment;
			}
			if (closed)
			{
				yield return new Segment(Point.Truncate(Points[0]), Point.Truncate(Points.Last()));
			}
		}

		/// <summary>
		/// Compute the points for bezier curve
		/// </summary>
		/// <param name="points"></param>
		/// <returns></returns>
		private IEnumerable<PointF> ComputeBezierPoints(params PointF[] points)
		{
			var n = points.Length - 1;

			PointF ComputeBezierPoint(float t)
			{
				PointF[] newPoints = (PointF[])points.Clone();
				var u = 1 - t;
				for (int i = 1; i <= n; i++)
				{
					for (int j = 0; j <= n - i; j++)
					{
						newPoints[j] = new PointF(u * newPoints[j].X + t * newPoints[j + 1].X, u * newPoints[j].Y + t * newPoints[j + 1].Y);
					}
				}
				return newPoints[0];
			}

			var divisions = points.SlideEnumerateBy(2).Sum(p => Math.Max(Math.Abs(p[0].X - p[1].X), Math.Abs(p[0].Y - p[1].Y)));
			float initialsteps = 1 / divisions;

			PointF lastPoint = ComputeBezierPoint(0);
			yield return lastPoint;
			for (float f = initialsteps; f < 1; f += initialsteps)
			{
				var newPoint = ComputeBezierPoint(f);
				float dx = lastPoint.X - newPoint.X;
				float dy = lastPoint.Y - newPoint.Y;
				if ((dx * dx + dy * dy) < 1f) { continue; }
				yield return newPoint;
				lastPoint = newPoint;
			}
		}

	}
}
