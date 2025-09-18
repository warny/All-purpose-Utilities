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
        /// <summary>
        /// Represents a Bézier curve that can be rasterized to points or line segments.
        /// </summary>
        public class Bezier : IDrawable
        {
                /// <summary>
                /// Gets the control points defining the Bézier curve.
                /// </summary>
                public PointF[] Points { get; }

                private Segment[] segments;
                private float length = -1;

                /// <summary>
                /// Initializes a new instance of the <see cref="Bezier"/> class using integer points.
                /// </summary>
                /// <param name="points">Control points defining the curve.</param>
                public Bezier(params Point[] points)
                        : this(points.Select(p => new PointF(p.X, p.Y)).ToArray()) { }

                /// <summary>
                /// Initializes a new instance of the <see cref="Bezier"/> class using floating point coordinates.
                /// </summary>
                /// <param name="points">Control points defining the curve.</param>
                public Bezier(params PointF[] points)
                {
                        Points = points;
                }

                /// <summary>
                /// Gets the total length of the curve approximated by its generated segments.
                /// </summary>
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
                /// Gets or creates the cached segments approximating the curve.
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
                /// Rasterizes the curve into oriented points.
                /// </summary>
                /// <param name="closed">Indicates whether the returned points should form a closed path.</param>
                /// <param name="position">Starting accumulated length value for the first point.</param>
                /// <returns>Enumeration of oriented points describing the curve.</returns>
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

                /// <summary>
                /// Returns a set of segments approximating the curve.
                /// </summary>
                /// <param name="closed">Indicates whether a closing segment should be added.</param>
                /// <returns>Enumeration of segments representing the curve.</returns>
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
                /// Computes a set of interpolated points describing the Bézier curve using the
                /// De Casteljau algorithm.
                /// </summary>
                /// <param name="points">Control points used to build the curve.</param>
                /// <returns>Points describing the curve.</returns>
                private IEnumerable<PointF> ComputeBezierPoints(params PointF[] points)
                {
                        var n = points.Length - 1;

                        var divisions = points.SlideEnumerateBy(2).Sum(p => Math.Max(Math.Abs(p[0].X - p[1].X), Math.Abs(p[0].Y - p[1].Y)));
                        float initialsteps = 1 / divisions;

                        PointF lastPoint = ComputeBezierPoint(0, points, n);
                        yield return lastPoint;
                        for (float f = initialsteps; f < 1; f += initialsteps)
                        {
                                var newPoint = ComputeBezierPoint(f, points, n);
                                float dx = lastPoint.X - newPoint.X;
                                float dy = lastPoint.Y - newPoint.Y;
                                if ((dx * dx + dy * dy) < 1f)
                                {
                                        continue;
                                }

                                yield return newPoint;
                                lastPoint = newPoint;
                        }
                }

                /// <summary>
                /// Computes a single interpolated point for the provided progress value.
                /// </summary>
                /// <param name="t">Progress along the curve in the range [0, 1].</param>
                /// <param name="controlPoints">Control points defining the Bézier curve.</param>
                /// <param name="degree">Degree of the curve.</param>
                /// <returns>The interpolated point.</returns>
                private static PointF ComputeBezierPoint(float t, PointF[] controlPoints, int degree)
                {
                        PointF[] newPoints = (PointF[])controlPoints.Clone();
                        var u = 1 - t;
                        for (int i = 1; i <= degree; i++)
                        {
                                for (int j = 0; j <= degree - i; j++)
                                {
                                        newPoints[j] = new PointF(u * newPoints[j].X + t * newPoints[j + 1].X, u * newPoints[j].Y + t * newPoints[j + 1].Y);
                                }
                        }

                        return newPoints[0];
                }

	}
}
