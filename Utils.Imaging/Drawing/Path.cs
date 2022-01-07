using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Utils.Collections;

namespace Utils.Drawing
{
	public class Path : IDrawable
	{
		private readonly List<Segment> segments;

		private Point startPoint;
		private Point lastPoint;

		public float Length { get; }

		public Path(Point startPoint)
		{
			this.startPoint = startPoint;
			this.lastPoint = startPoint;
		}

		public Path LineTo(Point p)
		{
			Segment segment = new Segment(lastPoint, p);
			lastPoint = p;
			segments.Add(segment);
			return this;
		}

		public Path BezierTo(params Point[] points)
		{
			Bezier bezier = new Bezier(points.PrecededBy(lastPoint).ToArray());
			segments.AddRange(bezier.GetSegments(false));
			lastPoint = points.Last();
			return this;
		}

		//public Path BezierThenLineTo(Point start, Point end)
		//{



		//	Bezier bezier = new Bezier(lastPoint, start);
		//	Segment segment = new Segment(start, end);
		//	segments.AddRange(bezier.GetSegments(false));
		//	segments.Add(segment);
		//	lastPoint = end;
		//	return this;
		//}

		public IEnumerable<DrawPoint> GetPoints(bool closed, float position = 0)
		{
			foreach (var segment in GetSegments(closed))
			{
				foreach (var point in segment.GetPoints(false, position))
				{
					yield return point;
					position = point.Position;
				}
			}
		}

		public IEnumerable<Segment> GetSegments(bool closed)
		{
			if (closed)
			{
				return this.segments.FollowedBy(new Segment(this.lastPoint, this.startPoint));
			}
			else
			{
				return this.segments;
			}
		}
	}
}
