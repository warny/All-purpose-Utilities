using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Utils.Collections;

namespace Utils.Drawing
{
	public class Polygon : IDrawable
	{
		private Segment[] Segments { get; }

		public float Length => Segments.Sum(s => s.Length);

		public Polygon(params PointF[] points) : this((IEnumerable<PointF>)points) { }

		public Polygon(IEnumerable<PointF> points)
		{
			Segments = points.SlideEnumerateBy(2).Select(p=>new Segment(p[0], p[1])).FollowedBy(new Segment(points.Last(), points.First())).ToArray();
		}

		public IEnumerable<DrawPoint> GetPoints(bool closed, float position = 0)
		{
			return Segments.SelectMany(s => s.GetPoints(false));
		}

		public IEnumerable<Segment> GetSegments(bool closed) => Segments;
	}
}
