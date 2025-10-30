using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Drawing
{
    /// <summary>
    /// Describes an object that can be converted into drawing primitives such as
    /// segments or oriented points.
    /// </summary>
    public interface IDrawable
    {
        /// <summary>
        /// Gets the total length of the drawable object.
        /// </summary>
        float Length { get; }

        /// <summary>
        /// Returns oriented points that describe the shape.
        /// </summary>
        /// <param name="closed">Indicates whether the returned points should form a closed loop.</param>
        /// <param name="position">Starting accumulated length value for the first point.</param>
        /// <returns>Enumeration of oriented points.</returns>
        IEnumerable<DrawPoint> GetPoints(bool closed, float position = 0);

        /// <summary>
        /// Returns line segments that describe the shape.
        /// </summary>
        /// <param name="closed">Indicates whether the returned segments should form a closed loop.</param>
        /// <returns>Enumeration of line segments.</returns>
        IEnumerable<Segment> GetSegments(bool closed);
    }
}
