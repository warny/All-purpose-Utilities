using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Utils.Collections;

namespace Utils.Drawing
{
        /// <summary>
        /// Represents a drawable path composed of straight and Bézier segments.
        /// </summary>
        public class Path : IDrawable
        {
                private readonly List<Segment> segments = new ();

                private PointF startPoint;
                private PointF lastPoint;

                /// <summary>
                /// Gets the total length of the path.
                /// </summary>
                public float Length { get; }

                /// <summary>
                /// Initializes a new <see cref="Path"/> starting at the specified point.
                /// </summary>
                /// <param name="startPoint">The initial drawing position.</param>
                public Path(PointF startPoint)
                {
                        this.startPoint = startPoint;
                        this.lastPoint = startPoint;
                }

                /// <summary>
                /// Adds a straight segment to the path.
                /// </summary>
                /// <param name="p">The end point of the new segment.</param>
                /// <returns>The current <see cref="Path"/> instance.</returns>
                public Path LineTo(PointF p)
                {
                        Segment segment = new Segment(lastPoint, p);
                        lastPoint = p;
                        segments.Add(segment);
                        return this;
                }

                /// <summary>
                /// Adds a Bézier curve to the path.
                /// </summary>
                /// <param name="points">Control points and end point defining the curve.</param>
                /// <returns>The current <see cref="Path"/> instance.</returns>
                public Path BezierTo(params PointF[] points)
                {
                        Bezier bezier = new Bezier(points.PrecededBy(lastPoint).ToArray());
                        segments.AddRange(bezier.GetSegments(false));
                        lastPoint = points.Last();
                        return this;
                }

                /// <summary>
                /// Enumerates all raster points across the path segments.
                /// </summary>
                /// <param name="closed">Indicates whether a closing segment should be added.</param>
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
                /// Returns the segments composing the path, optionally closing it.
                /// </summary>
                /// <param name="closed">If <see langword="true"/>, appends a segment to return to the starting point.</param>
                /// <returns>The sequence of path segments.</returns>
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
