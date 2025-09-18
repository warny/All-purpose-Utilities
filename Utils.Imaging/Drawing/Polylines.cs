using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Utils.Collections;

namespace Utils.Drawing
{
        /// <summary>
        /// Represents a sequence of connected segments that are not necessarily closed.
        /// </summary>
        public class Polylines : IDrawable
        {
                /// <summary>
                /// Gets the set of segments forming the polyline.
                /// </summary>
                private Segment[] Segments { get; }

                /// <summary>
                /// Gets the total length of all segments.
                /// </summary>
                public float Length => Segments.Sum(s => s.Length);

                /// <summary>
                /// Initializes a new <see cref="Polylines"/> instance from the supplied points.
                /// </summary>
                /// <param name="points">Ordered vertices composing the polyline.</param>
                public Polylines(PointF[] points)
                {
                        Segments = points.SlideEnumerateBy(2).Select(p => new Segment(p[0], p[1])).ToArray();
                }

                /// <summary>
                /// Enumerates all raster points along the polyline.
                /// </summary>
                /// <param name="closed">Indicates whether an extra segment should close the path.</param>
                /// <param name="position">Initial position offset along the path.</param>
                /// <returns>The sampled drawing points.</returns>
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

                /// <summary>
                /// Enumerates the composing segments, optionally closing the polyline.
                /// </summary>
                /// <param name="closed">If <see langword="true"/>, adds a segment from the end to the beginning.</param>
                /// <returns>The sequence of segments.</returns>
                public IEnumerable<Segment> GetSegments(bool closed)
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

                        return segments;
                }
        }
}
