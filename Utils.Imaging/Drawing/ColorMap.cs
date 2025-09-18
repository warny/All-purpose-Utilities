using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace Utils.Drawing
{
        /// <summary>
        /// Represents a function that maps a drawing position and offset to a color value.
        /// </summary>
        /// <typeparam name="T">Type of the color representation.</typeparam>
        /// <param name="position">Progress of the drawing operation.</param>
        /// <param name="offset">Offset relative to the current point.</param>
        /// <returns>The color produced for the provided coordinates.</returns>
        public delegate T DrawingColorMap<T>(float position, float offset);

        /// <summary>
        /// Represents a function that maps two-dimensional UV coordinates to a color value.
        /// </summary>
        /// <typeparam name="T">Type of the color representation.</typeparam>
        /// <param name="U">Horizontal coordinate within the texture.</param>
        /// <param name="V">Vertical coordinate within the texture.</param>
        /// <returns>The color associated with the UV coordinate.</returns>
        public delegate T UVMap<T>(float U, float V);

        /// <summary>
        /// Defines the contract for drawing brushes.
        /// </summary>
        /// <typeparam name="T">Type of the color representation.</typeparam>
        public interface IBrush<T> {
                /// <summary>
                /// Resets the brush internal state.
                /// </summary>
                void Reset();

                /// <summary>
                /// Generates the pixels that must be painted for a given draw point.
                /// </summary>
                /// <param name="point">Point being drawn.</param>
                /// <param name="position">Normalized position of the draw operation.</param>
                /// <returns>A sequence of coordinates with their associated color.</returns>
                IEnumerable<(Point Point, T Color)> Draw(DrawPoint point, float position);
        }


        /// <summary>
        /// Represents a brush that caches generated points to avoid drawing duplicates.
        /// </summary>
        /// <typeparam name="T">Type of the color representation.</typeparam>
        public class MapBrush<T> : IBrush<T>
        {
                /// <summary>
                /// Initializes a new instance of the <see cref="MapBrush{T}"/> class using a constant color.
                /// </summary>
                /// <param name="drawingColor">Color value produced for every point.</param>
                /// <param name="width">Width of the brush stroke.</param>
                public MapBrush(T drawingColor, float width = 1)
                {
                        DrawingColor = (p, s) => drawingColor;
                        Width = width;
                }

                /// <summary>
                /// Initializes a new instance of the <see cref="MapBrush{T}"/> class using a color mapping delegate.
                /// </summary>
                /// <param name="drawingColor">Delegate that produces colors for each sampled point.</param>
                /// <param name="width">Width of the brush stroke.</param>
                /// <exception cref="ArgumentNullException">Thrown when <paramref name="drawingColor"/> is <see langword="null"/>.</exception>
                public MapBrush(DrawingColorMap<T> drawingColor, float width = 1)
                {
                        DrawingColor = drawingColor ?? throw new ArgumentNullException(nameof(drawingColor));
                        Width = width;
                }

                /// <summary>
                /// Gets the delegate that produces colors for the brush.
                /// </summary>
                public DrawingColorMap<T> DrawingColor { get; }

                /// <summary>
                /// Gets the width of the brush stroke.
                /// </summary>
                public float Width { get; }

                private DrawPoint lastPoint = null;
                private Dictionary<Point, float> drawedPoints = null;

                /// <inheritdoc />
                public void Reset()
                {
                        lastPoint = null;
                        drawedPoints = new Dictionary<Point, float>();
                }

                /// <summary>
                /// Caches a point to avoid yielding duplicates.
                /// </summary>
                /// <param name="p">Point to cache.</param>
                /// <param name="offset">Distance from the stroke origin.</param>
                /// <returns><see langword="true"/> when the point should be emitted; otherwise <see langword="false"/>.</returns>
                private bool CachePoint(Point p, float offset)
                {
                        if (!drawedPoints.TryGetValue(p, out var drawedoffset) || drawedoffset > offset)
                        {
                                drawedPoints[p] = offset;
                                return true;
                        }

                        return false;
                }

                /// <inheritdoc />
                public IEnumerable<(Point Point, T Color)> Draw(DrawPoint point, float position)
                {
                        if (lastPoint is null)
                        {
                                lastPoint = point;
                                var returnPoint = new Point(lastPoint.X, lastPoint.Y);
                                if (CachePoint(returnPoint, 0))
                                {
                                        yield return (returnPoint, DrawingColor(position, 0));
                                }

                                yield break;
                        }

                        PointF pfl1 = new PointF(lastPoint.X, lastPoint.Y);
                        PointF pfl2 = new PointF(lastPoint.X, lastPoint.Y);
                        PointF pfn1 = new PointF(point.X, point.Y);
                        PointF pfn2 = new PointF(point.X, point.Y);

                        Point p2 = Point.Round(pfn2);
                        if (CachePoint(p2, 0))
                        {
                                yield return (p2, DrawingColor(position, 0));
                        }

                        /*
                        for (int offset = 0; offset < Width; offset++)
                        {
                                pfl1 = new PointF(pfl1.X + lastPoint.Cos, pfl1.Y + lastPoint.Sin);
                                pfl2 = new PointF(pfl2.X - lastPoint.Cos, pfl2.Y - lastPoint.Sin);
                                pfn1 = new PointF(pfn1.X + point.Cos, pfn1.Y + point.Sin);
                                pfn2 = new PointF(pfn2.X - point.Cos, pfn2.Y - point.Sin);

                        }
                        */
                }
        }
}
