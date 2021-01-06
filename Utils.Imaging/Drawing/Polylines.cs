using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Utils.Lists;

namespace Utils.Drawing
{
	public class Polylines : IDrawable
	{
		private Segment[] Segments { get; }

		public float Length => Segments.Sum(s => s.Length);

		public Polylines(Point[] points)
		{
			Segments = points.EnumerateBy(2).Select(p => new Segment(p[0], p[1])).ToArray();
		}

		public IEnumerable<DrawPoint> GetPoints(bool closed, float position = 0)
		{
			IEnumerable<Segment> segments;
			if (closed)
			{
				segments = Segments.FollowedBy(new Segment(Segments.Last().End, Segments.First().Start));
			}
			else
			{
				segments = Segments;
			}

			foreach (var segment in segments)
			{
				foreach (var point in segment.GetPoints(false, position))
				{
					yield return point;
					position = point.Position;
				}
			}
		}

	}
}
