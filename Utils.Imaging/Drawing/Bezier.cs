using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Utils.Collections;
using Utils.Mathematics;

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
		private PointF[] ComputeBezierPoints(params PointF[] points)
		{
			PointF ComputeBezierPoint(float position)
			{
				var n = points.Length - 1;
				var weights = MathEx.ComputePascalTriangleLine(n);

				PointF result = new PointF();

				float iPosition = 1 - position;

				for (int i = 0; i <= n; i++)
				{
					var b = weights[i] * (float)Math.Pow(iPosition, n - i) * (float)Math.Pow(position, i);
					result.X += b * points[i].X;
					result.Y += b * points[i].Y;
				}
				return result;

				//PointF[] computePoints(PointF[] source)
				//{
				//	var target = new PointF[source.Length - 1];
				//	for (int i = 0; i < target.Length; i++)
				//	{
				//		var point1 = source[i];
				//		var point2 = source[i + 1];
				//		target[i] = new PointF(
				//			(1f - position) * point1.X + position * point2.X,
				//			(1f - position) * point1.Y + position * point2.Y
				//		);
				//	}
				//	return target;
				//}

				//var result = points;
				//while (result.Length > 1)
				//{
				//	result = computePoints(result);
				//}
				//return result[0];
			}

			LinkedList<(PointF Point, float Position)> computedPoints = new ();

			for (float f = 0; f < 1; f += 0.1f) {
				computedPoints.AddLast((ComputeBezierPoint(f), f));
			}
			var last =computedPoints.AddLast((ComputeBezierPoint(1f), 1f));

			var current = computedPoints.First;
			while (current != last)
			{
				var next = current.Next;
				var newPosition = (current.Value.Position + next.Value.Position) / 2;
				var newPoint = ComputeBezierPoint(newPosition);

				float dx = next.Value.Point.X - newPoint.X;
				float dy = next.Value.Point.Y - newPoint.Y;
				if (/*dx.Between(-1f, 1f) || dy.Between(-1f, 1f) ||*/ (dx*dx + dy*dy) <= 1f) {
					current = next;
					continue;
				}
				computedPoints.AddAfter(current, (newPoint, newPosition));
			}

			return computedPoints.Select(p=>p.Point).ToArray();
		}

	}
}
