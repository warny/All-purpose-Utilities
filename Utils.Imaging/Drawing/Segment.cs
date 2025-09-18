using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Utils.Mathematics;

namespace Utils.Drawing
{
        /// <summary>
        /// Represents a straight line segment and exposes helper information used by raster drawing routines.
        /// </summary>
        public class Segment : IDrawable
        {
                /// <summary>
                /// Gets the starting point of the segment.
                /// </summary>
                public PointF Start { get; }

                /// <summary>
                /// Gets the ending point of the segment.
                /// </summary>
                public PointF End { get; }

                /// <summary>
                /// Gets the X-coordinate of the starting point.
                /// </summary>
                public float X1 => Start.X;

                /// <summary>
                /// Gets the Y-coordinate of the starting point.
                /// </summary>
                public float Y1 => Start.Y;

                /// <summary>
                /// Gets the X-coordinate of the ending point.
                /// </summary>
                public float X2 => End.X;

                /// <summary>
                /// Gets the Y-coordinate of the ending point.
                /// </summary>
                public float Y2 => End.Y;

                /// <summary>
                /// Gets the Euclidean length of the segment.
                /// </summary>
                public float Length { get; }

                /// <summary>
                /// Gets the sine of the segment's angle relative to the X axis.
                /// </summary>
                public float Sin { get; }

                /// <summary>
                /// Gets the cosine of the segment's angle relative to the X axis.
                /// </summary>
                public float Cos { get; }

                /// <summary>
                /// Initializes a new <see cref="Segment"/> using integer coordinates.
                /// </summary>
                /// <param name="x1">The X coordinate of the starting point.</param>
                /// <param name="y1">The Y coordinate of the starting point.</param>
                /// <param name="x2">The X coordinate of the ending point.</param>
                /// <param name="y2">The Y coordinate of the ending point.</param>
                public Segment(
                        int x1, int y1,
                        int x2, int y2
                ) : this(
                        new PointF(x1, y1),
                        new PointF(x2, y2)
                ) { }

                /// <summary>
                /// Initializes a new <see cref="Segment"/> using floating-point coordinates.
                /// </summary>
                /// <param name="x1">The X coordinate of the starting point.</param>
                /// <param name="y1">The Y coordinate of the starting point.</param>
                /// <param name="x2">The X coordinate of the ending point.</param>
                /// <param name="y2">The Y coordinate of the ending point.</param>
                public Segment(
                        float x1, float y1,
                        float x2, float y2
                ) : this(
                        new PointF(x1, y1),
                        new PointF(x2, y2)
                ) { }

                /// <summary>
                /// Initializes a new <see cref="Segment"/> between the specified points.
                /// </summary>
                /// <param name="start">The start point of the segment.</param>
                /// <param name="end">The end point of the segment.</param>
                public Segment(PointF start, PointF end) {
                        Start = start;
                        End = end;

                        var x = Start.X - End.X;
                        var y = Start.Y - End.Y;
                        Length = (float)Math.Sqrt(x * x + y * y);
                        Cos = (X2 - X1) / Length;
                        Sin = (Y2 - Y1) / Length;
                }

                /// <summary>
                /// Enumerates the rasterized points along the segment using a Bresenham-style algorithm.
                /// </summary>
                /// <param name="closed">Ignored for segments; provided for interface compatibility.</param>
                /// <param name="position">Starting position offset along the path.</param>
                /// <returns>Sequence of sampled drawing points.</returns>
                public IEnumerable<DrawPoint> GetPoints(bool closed, float position = 0)
                {
                        int x = (int)MathEx.Round(X1);
                        int y = (int)MathEx.Round(Y1);
                        int dx = (int)MathEx.Round(X2) - x;
                        int dy = (int)MathEx.Round(Y2) - y;

                        int xinc = (dx > 0) ? 1 : -1;
                        int yinc = (dy > 0) ? 1 : -1;
                        dx = Math.Abs(dx);
                        dy = Math.Abs(dy);

                        if (dx > dy)
                        {
                                yield return new DrawPoint(x, y, xinc, 0, Cos, Sin, position);
                                int cumul = dx / 2;
                                float dl = dx / Length;
                                for (int i = 1; i <= dx; i++)
                                {
                                        int verticalDirection = 0;
                                        x += xinc;
                                        cumul += dy;
                                        position += dl;
                                        if (cumul >= dx)
                                        {
                                                verticalDirection = yinc;
                                                cumul -= dx;
                                                y += yinc;
                                        }
                                        yield return new DrawPoint(x, y, xinc, verticalDirection, Sin, Cos, position);
                                }
                        }
                        else
                        {
                                yield return new DrawPoint(x, y, 0, yinc, Cos, Sin, position);
                                int cumul = dy / 2;
                                float dl = dy / Length;
                                for (int i = 1; i <= dy; i++)
                                {
                                        int horizontalDirection = 0;
                                        y += yinc;
                                        cumul += dx;
                                        position += dl;
                                        if (cumul >= dy)
                                        {
                                                horizontalDirection = xinc;
                                                cumul -= dy;
                                                x += xinc;
                                        }
                                        yield return new DrawPoint(x, y, horizontalDirection, yinc, Sin, Cos, position);
                                }
                        }
                }

                /// <summary>
                /// Returns this instance as the only segment in the sequence.
                /// </summary>
                /// <param name="closed">Ignored for segments; provided for interface compatibility.</param>
                /// <returns>The current segment.</returns>
                public IEnumerable<Segment> GetSegments(bool closed) => new[] { this };

                /// <inheritdoc />
                public override string ToString() => $"({Start}) ==> ({End})";

        }
}
