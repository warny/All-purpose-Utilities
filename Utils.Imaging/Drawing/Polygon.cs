using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Utils.Collections;

namespace Utils.Drawing
{
    /// <summary>
    /// Represents a closed polygon composed of connected segments.
    /// </summary>
    public class Polygon : IDrawable
    {
        /// <summary>
        /// Gets the segments making up the polygon perimeter.
        /// </summary>
        private Segment[] Segments { get; }

        /// <summary>
        /// Gets the total length of the polygon perimeter.
        /// </summary>
        public float Length => Segments.Sum(s => s.Length);

        /// <summary>
        /// Initializes a new <see cref="Polygon"/> from a variable number of vertices.
        /// </summary>
        /// <param name="points">The polygon vertices in drawing order.</param>
        public Polygon(params PointF[] points) : this((IEnumerable<PointF>)points) { }

        /// <summary>
        /// Initializes a new <see cref="Polygon"/> from an enumerable vertex sequence.
        /// </summary>
        /// <param name="points">The polygon vertices in drawing order.</param>
        public Polygon(IEnumerable<PointF> points)
        {
            Segments = points.SlideEnumerateBy(2).Select(p => new Segment(p[0], p[1])).FollowedBy(new Segment(points.Last(), points.First())).ToArray();
        }

        /// <summary>
        /// Enumerates all raster points along the polygon edges.
        /// </summary>
        /// <param name="closed">Ignored because polygons are always closed.</param>
        /// <param name="position">Initial position offset along the path.</param>
        /// <returns>The sampled drawing points.</returns>
        public IEnumerable<DrawPoint> GetPoints(bool closed, float position = 0)
        {
            return Segments.SelectMany(s => s.GetPoints(false));
        }

        /// <summary>
        /// Returns the polygon segments.
        /// </summary>
        /// <param name="closed">Ignored because polygons are always closed.</param>
        /// <returns>The sequence of polygon segments.</returns>
        public IEnumerable<Segment> GetSegments(bool closed) => Segments;
    }
}
