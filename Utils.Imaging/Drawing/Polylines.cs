using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Utils.Lists;

namespace Utils.Drawing
{
	public class Polylines
	{
		private Segment[] Segments { get; }

		public float Length => Segments.Sum(s => s.Length);

		public Polylines(Point[] points)
		{
			Segments = points.EnumerateBy(2).Select(p => new Segment(p[0], p[1])).ToArray();
		}

		public IEnumerable<DrawPoint> GetPoints(bool closed, float position = 0)
		{
			if (closed)
			{
				return Segments.FollowedBy(new Segment(Segments.Last().End, Segments.First().Start)).SelectMany(s => s.GetPoints(false));
			}
			else
			{
				return Segments.SelectMany(s => s.GetPoints(false));
			}
		}

	}
}
