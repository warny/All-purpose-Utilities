using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Utils.Lists;
using Utils.Mathematics;

namespace Utils.Drawing
{
	public class Bezier : IDrawable
	{
		public PointF[] Points { get; }

		private Segment[] segments;
		private float length = -1;

		public Bezier(Point[] points)
			: this(points.Select(p => new PointF(p.X, p.Y)).ToArray()) { }
		public Bezier(PointF[] points)
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
				if (segments == null)
				{

					List<Segment> result = new List<Segment>();
					var computedPoints = ComputeBezierPoints(Points.Select(p => new PointF(p.X, p.Y)).ToArray());

					foreach (var points in computedPoints.EnumerateBy(2))
					{
						var start = Point.Round(points[0]);
						var end = Point.Round(points[1]);

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

			foreach (var segment in Segments)
			{
				foreach (var drawPoint in segment.GetPoints(false, position))
				{
					position = drawPoint.Position;
					yield return drawPoint;
				}
			}
			if (closed)
			{
				foreach (var drawPoint in  (new Segment(Point.Truncate(Points[0]), Point.Truncate(Points.Last())).GetPoints(false, position)))
				{
					yield return drawPoint;
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="points"></param>
		/// <returns></returns>
		private IEnumerable<PointF> ComputeBezierPoints(params PointF[] points)
		{
			PointF ComputeBezierPoint(float position)
			{
				PointF[] computePoints(PointF[] source)
				{
					var target = new PointF[source.Length - 1];
					for (int i = 0; i < target.Length; i++)
					{
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

			IEnumerable<(PointF p, float position)> computeIntermediatePoints(PointF p1, float pos1, PointF p2, float pos2)
			{
				if ((p1.X - p2.X).Between(-1, 1) && (p1.Y - p2.Y).Between(-1, 1)) { yield break; }

				var pos = (pos1 + pos2) / 2;
				var p = ComputeBezierPoint(pos);
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

			var pstart = ComputeBezierPoint(0f);
			var pmid = ComputeBezierPoint(0.5f);
			var pend = ComputeBezierPoint(1f);

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


	}
}
