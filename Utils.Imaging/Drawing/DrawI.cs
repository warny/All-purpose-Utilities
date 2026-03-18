using System;
using System.Collections.Generic;
using System.Drawing;
using Utils.Imaging;
using Utils.Mathematics;
using System.Linq;
using Utils.Objects;

namespace Utils.Drawing
{
    /// <summary>
    /// Provides integer-based drawing helpers for image accessors.
    /// </summary>
    /// <typeparam name="T">Type of the pixel value handled by the drawing operations.</typeparam>
    /// <remarks>
    /// The <c>DrawI</c> class exposes low-level rasterization primitives.  It is intentionally conservative about
    /// bounds checking and delegates all color decisions to <see cref="IBrush{T}"/> implementations so that higher
    /// level helpers, such as <see cref="DrawF{T}"/>, can reuse the same pixel pipeline.
    /// </remarks>
    public class DrawI<T> : BaseDrawing<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DrawI{T}"/> class.
        /// </summary>
        /// <param name="imageAccessor">Accessor used to manipulate the underlying image.</param>
        /// <remarks>
        /// The accessor reference is stored for the whole lifetime of the helper so that callers can perform
        /// several drawing operations without recreating the wrapper.
        /// </remarks>
        public DrawI(IImageAccessor<T> imageAccessor) : base(imageAccessor) { }

        #region Point
        /// <summary>
        /// Draws a single pixel.
        /// </summary>
        /// <param name="point">Pixel coordinates.</param>
        /// <param name="color">Color applied to the pixel.</param>
        public void DrawPoint(Point point, T color) => DrawPoint(point.X, point.Y, color);

        /// <summary>
        /// Draws a single pixel.
        /// </summary>
        /// <param name="x">Horizontal coordinate.</param>
        /// <param name="y">Vertical coordinate.</param>
        /// <param name="color">Color applied to the pixel.</param>
        public void DrawPoint(int x, int y, T color)
        {
            if (x.Between(0, ImageAccessor.Width - 1) && y.Between(0, ImageAccessor.Height - 1))
            {
                ImageAccessor[x, y] = color;
            }
        }

        /// <summary>
        /// Draws the provided shape using the specified brush.
        /// </summary>
        /// <param name="draw">Brush generating the pixels to render.</param>
        /// <param name="drawable">Shape definition.</param>
        private void DrawShape(IBrush<T> draw, IDrawable drawable)
        {
            draw.Reset();
            var length = drawable.Length;

            foreach (var point in drawable.GetPoints(false))
            {
                foreach (var drawPoint in draw.Draw(point, point.Position / length))
                {
                    DrawPoint(drawPoint.Point.X, drawPoint.Point.Y, drawPoint.Color);
                }
            }
        }

        /// <summary>
        /// Draws the outline of one or more shapes using a solid color.
        /// </summary>
        /// <param name="color">Color applied to the outlines.</param>
        /// <param name="drawables">Shapes to draw.</param>
        public void DrawShape(T color, params IDrawable[] drawables)
            => DrawShape(new MapBrush<T>(color), (IEnumerable<IDrawable>)drawables);

        /// <summary>
        /// Draws the outline of shapes using a solid color.
        /// </summary>
        /// <param name="color">Color applied to the outlines.</param>
        /// <param name="drawables">Shapes to draw.</param>
        public void DrawShape(T color, IEnumerable<IDrawable> drawables)
            => DrawShape(new MapBrush<T>(color), drawables);

        /// <summary>
        /// Draws the outline of one or more shapes using a brush.
        /// The brush position is normalised independently per shape over [0, 1].
        /// </summary>
        /// <param name="color">Brush providing the colors along each outline.</param>
        /// <param name="drawables">Shapes to draw.</param>
        public void DrawShape(IBrush<T> color, params IDrawable[] drawables)
            => DrawShape(color, (IEnumerable<IDrawable>)drawables);

        /// <summary>
        /// Draws the outline of shapes using a brush.
        /// The brush position is normalised independently per shape over [0, 1].
        /// </summary>
        /// <param name="color">Brush providing the colors along each outline.</param>
        /// <param name="drawables">Shapes to draw.</param>
        public void DrawShape(IBrush<T> color, IEnumerable<IDrawable> drawables)
        {
            foreach (var drawable in drawables)
                DrawShape(color, drawable);
        }

        /// <summary>
        /// Fills shapes using the non-zero winding rule with a UV color mapping.
        /// </summary>
        /// <param name="color">
        /// Delegate that receives normalized UV coordinates in [0,1] (relative to the bounding box of
        /// all shapes) and returns the color for each pixel.  Pass a constant lambda such as
        /// <c>(u, v) => myColor</c> for a solid fill, or sample a texture via <c>(u, v) => texture[(int)(u * w), (int)(v * h)]</c>.
        /// </param>
        /// <param name="drawables">Shapes to fill.</param>
        public void FillShape1(UVMap<T> color, params IDrawable[] drawables)
                => FillShape1(color, (IEnumerable<IDrawable>)drawables);

        /// <summary>
        /// Fills shapes using the non-zero winding rule with a UV color mapping.
        /// </summary>
        /// <param name="color">
        /// Delegate that receives normalized UV coordinates in [0,1] (relative to the bounding box of
        /// all shapes) and returns the color for each pixel.
        /// </param>
        /// <param name="drawables">Shapes to fill.</param>
        public void FillShape1(UVMap<T> color, IEnumerable<IDrawable> drawables)
            => FillShapeCore(color, drawables, w => w != 0);

        /// <summary>
        /// Fills shapes using the even-odd winding rule with a UV color mapping.
        /// </summary>
        /// <param name="color">
        /// Delegate that receives normalized UV coordinates in [0,1] (relative to the bounding box of
        /// all shapes) and returns the color for each pixel.  Pass a constant lambda such as
        /// <c>(u, v) => myColor</c> for a solid fill, or sample a texture via <c>(u, v) => texture[(int)(u * w), (int)(v * h)]</c>.
        /// </param>
        /// <param name="drawables">Shapes to fill.</param>
        public void FillShape2(UVMap<T> color, params IDrawable[] drawables)
                => FillShape2(color, (IEnumerable<IDrawable>)drawables);

        /// <summary>
        /// Fills shapes using the even-odd winding rule with a UV color mapping.
        /// </summary>
        /// <param name="color">
        /// Delegate that receives normalized UV coordinates in [0,1] (relative to the bounding box of
        /// all shapes) and returns the color for each pixel.
        /// </param>
        /// <param name="drawables">Shapes to fill.</param>
        public void FillShape2(UVMap<T> color, IEnumerable<IDrawable> drawables)
            => FillShapeCore(color, drawables, w => MathEx.Mod(w, 2) != 0);

        /// <summary>
        /// Shared scan-line fill implementation using exact segment–scanline intersections.
        /// </summary>
        /// <param name="color">
        /// Delegate receiving normalized UV coordinates in [0,1] for each filled pixel, where
        /// (0,0) is the top-left corner and (1,1) is the bottom-right corner of the bounding box.
        /// </param>
        /// <param name="drawables">Shapes whose outlines define the fill region.</param>
        /// <param name="fillTest">
        /// Predicate on the accumulated winding number; returns <see langword="true"/> when a span
        /// between two consecutive intersections should be filled.
        /// </param>
        /// <remarks>
        /// Horizontal segments are skipped because they do not contribute intersections.
        /// Each segment uses the convention "include lower endpoint, exclude upper endpoint" so that
        /// shared vertices between consecutive segments are counted exactly once, preventing the
        /// winding-number corruption that the previous rasterized-point approach suffered from.
        /// After sorting, adjacent intersections closer than 0.5 px are merged: if their winding
        /// contributions cancel (spike / aller-retour along the same path) both are removed; otherwise
        /// they collapse into a single entry so that the winding count stays correct.
        /// </remarks>
        private void FillShapeCore(UVMap<T> color, IEnumerable<IDrawable> drawables, Func<int, bool> fillTest)
        {
            // Collect all non-horizontal segments from every drawable (closing each open path)
            var allSegments = drawables
                .SelectMany(d => d.GetSegments(true))
                .Where(s => s.Y1 != s.Y2)
                .ToList();

            if (allSegments.Count == 0) return;

            // Compute bounding box for UV normalization
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var seg in allSegments)
            {
                if (seg.X1 < minX) minX = seg.X1;
                if (seg.X1 > maxX) maxX = seg.X1;
                if (seg.X2 < minX) minX = seg.X2;
                if (seg.X2 > maxX) maxX = seg.X2;
                if (seg.Y1 < minY) minY = seg.Y1;
                if (seg.Y1 > maxY) maxY = seg.Y1;
                if (seg.Y2 < minY) minY = seg.Y2;
                if (seg.Y2 > maxY) maxY = seg.Y2;
            }

            float rangeX = maxX - minX;
            float rangeY = maxY - minY;
            int yStart = (int)Math.Ceiling(minY);
            int yEnd = (int)Math.Floor(maxY);

            var intersections = new List<(float x, int winding)>();

            for (int y = yStart; y <= yEnd; y++)
            {
                float fy = y;
                intersections.Clear();

                foreach (var seg in allSegments)
                {
                    float y1 = seg.Y1, y2 = seg.Y2;
                    float yMin = y1 < y2 ? y1 : y2;
                    float yMax = y1 < y2 ? y2 : y1;

                    // Include lower endpoint, exclude upper endpoint:
                    // ensures shared vertices between consecutive segments are counted once.
                    if (fy < yMin || fy >= yMax) continue;

                    float t = (fy - y1) / (y2 - y1);
                    float xi = seg.X1 + t * (seg.X2 - seg.X1);
                    int winding = y2 > y1 ? 1 : -1;
                    intersections.Add((xi, winding));
                }

                if (intersections.Count == 0) continue;

                intersections.Sort((a, b) => a.x.CompareTo(b.x));

                // Merge coincident intersections (< 0.5 px apart) to handle spikes
                // (aller + retour along the same path) and floating-point near-duplicates.
                // Adjacent entries whose windings sum to zero are both removed (they cancel);
                // those with a non-zero sum are collapsed into a single entry at the left X.
                // A forward while-loop is used so that i stays in place after a removal,
                // naturally pointing to the next unprocessed element without going out of range.
                {
                    int i = 0;
                    while (i < intersections.Count - 1)
                    {
                        if (intersections[i + 1].x - intersections[i].x < 0.5f)
                        {
                            int merged = intersections[i].winding + intersections[i + 1].winding;
                            intersections.RemoveAt(i + 1);
                            if (merged == 0)
                                intersections.RemoveAt(i);
                            else
                                intersections[i] = (intersections[i].x, merged);
                            // Do not advance i: re-check the new neighbour in case it is also close.
                        }
                        else
                        {
                            i++;
                        }
                    }
                }

                if (intersections.Count == 0) continue;

                // Walk intersections left-to-right, maintaining running winding sum.
                // The span between intersection[i] and intersection[i+1] is filled when
                // fillTest(windingSum after crossing intersection[i]) is true.
                int windingSum = 0;
                bool first = true;
                float prevX = 0f;
                float v = rangeY > 0f ? (fy - minY) / rangeY : 0f;

                foreach (var (xi, winding) in intersections)
                {
                    if (!first && fillTest(windingSum))
                    {
                        int xFrom = (int)Math.Ceiling(prevX);
                        int xTo = (int)Math.Floor(xi);
                        for (int x = xFrom; x <= xTo; x++)
                        {
                            float u = rangeX > 0f ? (x - minX) / rangeX : 0f;
                            DrawPoint(x, y, color(u, v));
                        }
                    }
                    windingSum += winding;
                    prevX = xi;
                    first = false;
                }
            }
        }

        #endregion
        #region Line
        /// <summary>
        /// Draws a line between two pixels using a solid color.
        /// </summary>
        /// <param name="p1">Starting point.</param>
        /// <param name="p2">Ending point.</param>
        /// <param name="color">Color applied to the line.</param>
        public void DrawLine(Point p1, Point p2, T color) => DrawLine(p1.X, p1.Y, p2.X, p2.Y, new MapBrush<T>(color));

        /// <summary>
        /// Draws a line between two pixels using a solid color.
        /// </summary>
        /// <param name="x1">Starting point horizontal coordinate.</param>
        /// <param name="y1">Starting point vertical coordinate.</param>
        /// <param name="x2">Ending point horizontal coordinate.</param>
        /// <param name="y2">Ending point vertical coordinate.</param>
        /// <param name="color">Color applied to the line.</param>
        public void DrawLine(int x1, int y1, int x2, int y2, T color) => DrawLine(x1, y1, x2, y2, new MapBrush<T>(color));

        /// <summary>
        /// Draws a line between two pixels using a brush.
        /// </summary>
        /// <param name="p1">Starting point.</param>
        /// <param name="p2">Ending point.</param>
        /// <param name="color">Brush providing the colors.</param>
        public void DrawLine(Point p1, Point p2, IBrush<T> color) => DrawLine(p1.X, p1.Y, p2.X, p2.Y, color);

        /// <summary>
        /// Draws a line between two pixels using a brush.
        /// </summary>
        /// <param name="x1">Starting point horizontal coordinate.</param>
        /// <param name="y1">Starting point vertical coordinate.</param>
        /// <param name="x2">Ending point horizontal coordinate.</param>
        /// <param name="y2">Ending point vertical coordinate.</param>
        /// <param name="color">Brush providing the colors.</param>
        public void DrawLine(int x1, int y1, int x2, int y2, IBrush<T> color)
        {
            var segment = new Segment(x1, y1, x2, y2);
            DrawShape(color, segment);
        }
        #endregion
        #region Bezier
        /// <summary>
        /// Draws a Bézier curve using integer coordinates.
        /// </summary>
        /// <param name="color">Color applied to the curve.</param>
        /// <param name="points">Control points in pixel coordinates.</param>
        public void DrawBezier(T color, params Point[] points) => DrawBezier(color, points.Select(p => new PointF(p.X, p.Y)).ToArray());

        /// <summary>
        /// Draws a Bézier curve using floating-point coordinates.
        /// </summary>
        /// <param name="color">Color applied to the curve.</param>
        /// <param name="points">Control points.</param>
        internal void DrawBezier(T color, params PointF[] points)
        {
            var bezier = new Bezier(points);
            DrawShape(new MapBrush<T>(color), bezier);
        }

        /// <summary>
        /// Draws a Bézier curve using a brush.
        /// </summary>
        /// <param name="color">Brush providing the colors.</param>
        /// <param name="points">Control points.</param>
        public void DrawBezier(MapBrush<T> color, params Point[] points) => DrawBezier(color, points.Select(p => new PointF(p.X, p.Y)).ToArray());

        /// <summary>
        /// Draws a Bézier curve using a brush.
        /// </summary>
        /// <param name="color">Brush providing the colors.</param>
        /// <param name="points">Control points.</param>
        internal void DrawBezier(MapBrush<T> color, params PointF[] points)
        {
            var bezier = new Bezier(points);
            DrawShape(color, bezier);
        }

        /// <summary>
        /// Fills the area enclosed by a Bézier curve with a solid color.
        /// </summary>
        /// <param name="color">Color applied to the curve interior.</param>
        /// <param name="points">Control points.</param>
        public void FillBezier(T color, params Point[] points) => FillBezier(color, points.Select(p => new PointF(p.X, p.Y)).ToArray());

        /// <summary>
        /// Fills the area enclosed by a Bézier curve with a solid color.
        /// </summary>
        /// <param name="color">Color applied to the curve interior.</param>
        /// <param name="points">Control points.</param>
        internal void FillBezier(T color, params PointF[] points)
        {
            var bezier = new Bezier(points);
            FillShape1((x, y) => color, bezier);
        }

        /// <summary>
        /// Fills the area enclosed by a Bézier curve using a UV mapping.
        /// </summary>
        /// <param name="color">Delegate providing colors for each pixel.</param>
        /// <param name="points">Control points.</param>
        public void FillBezier(UVMap<T> color, params Point[] points) => FillBezier(color, points.Select(p => new PointF(p.X, p.Y)).ToArray());

        /// <summary>
        /// Fills the area enclosed by a Bézier curve using a UV mapping.
        /// </summary>
        /// <param name="color">Delegate providing colors for each pixel.</param>
        /// <param name="points">Control points.</param>
        internal void FillBezier(UVMap<T> color, params PointF[] points)
        {
            var bezier = new Bezier(points);
            FillShape1(color, bezier);
        }

        #endregion
        #region Polygon
        /// <summary>
        /// Draws a polygon filled with a solid color.
        /// </summary>
        /// <param name="color">Color applied to the polygon.</param>
        /// <param name="points">Polygon vertices.</param>
        public void DrawPolygon(T color, IEnumerable<Point> points) => DrawPolygon(color, points.ToArray());

        /// <summary>
        /// Draws a polygon filled with a solid color.
        /// </summary>
        /// <param name="color">Color applied to the polygon.</param>
        /// <param name="points">Polygon vertices.</param>
        public void DrawPolygon(T color, params Point[] points)
        {
            Polygon polygon = new Polygon(points.Select(p => (PointF)p));
            DrawShape(new MapBrush<T>(color), polygon);
        }

        /// <summary>
        /// Draws a polygon filled with a brush.
        /// </summary>
        /// <param name="color">Brush providing the polygon colors.</param>
        /// <param name="points">Polygon vertices.</param>
        public void DrawPolygon(IBrush<T> color, IEnumerable<Point> points) => DrawPolygon(color, points.ToArray());

        /// <summary>
        /// Draws a polygon filled with a brush.
        /// </summary>
        /// <param name="color">Brush providing the polygon colors.</param>
        /// <param name="points">Polygon vertices.</param>
        public void DrawPolygon(IBrush<T> color, params Point[] points)
        {
            Polygon polygon = new Polygon(points.Select(p => (PointF)p));
            DrawShape(color, polygon);
        }

        /// <summary>
        /// Fills a polygon using the even-odd rule and a solid color.
        /// </summary>
        /// <param name="color">Color applied to the polygon interior.</param>
        /// <param name="points">Polygon vertices.</param>
        public void FillPolygon1(T color, params Point[] points)
        {
            Polygon polygon = new Polygon(points.Select(p => (PointF)p));
            FillShape1((x, y) => color, polygon);
        }

        /// <summary>
        /// Fills a polygon using the non-zero winding rule and a solid color.
        /// </summary>
        /// <param name="color">Color applied to the polygon interior.</param>
        /// <param name="points">Polygon vertices.</param>
        public void FillPolygon2(T color, params Point[] points)
        {
            Polygon polygon = new Polygon(points.Select(p => (PointF)p));
            FillShape2((x, y) => color, polygon);
        }

        #endregion
        #region Circle
        /// <summary>
        /// Fills a circular arc with a solid color.
        /// </summary>
        /// <param name="center">Center of the circle.</param>
        /// <param name="radius">Circle radius in pixels.</param>
        /// <param name="color">Color applied to the circle interior.</param>
        /// <param name="startAngle">Start angle in radians.</param>
        /// <param name="endAngle">End angle in radians.</param>
        public void FillCircle(Point center, int radius, T color, double startAngle = 0, double endAngle = Math.PI * 2)
        {
            var ellipse = new Circle(center, radius, startAngle, endAngle);
            FillShape1((x, y) => color, ellipse);
        }

        /// <summary>
        /// Fills a circular arc using a UV color mapping.
        /// </summary>
        /// <param name="center">Center of the circle.</param>
        /// <param name="radius">Circle radius in pixels.</param>
        /// <param name="color">Delegate providing colors for each pixel.</param>
        /// <param name="startAngle">Start angle in radians.</param>
        /// <param name="endAngle">End angle in radians.</param>
        public void FillCircle(Point center, int radius, UVMap<T> color, double startAngle = 0, double endAngle = Math.PI * 2)
        {
            var ellipse = new Circle(center, radius, startAngle, endAngle);
            FillShape1(color, ellipse);
        }

        /// <summary>
        /// Draws a circle outline with a solid color.
        /// </summary>
        /// <param name="center">Center of the circle.</param>
        /// <param name="radius">Circle radius in pixels.</param>
        /// <param name="color">Color applied to the circle outline.</param>
        /// <param name="startAngle">Start angle in radians.</param>
        /// <param name="endAngle">End angle in radians.</param>
        public void DrawCircle(Point center, int radius, T color, double startAngle = 0, double endAngle = Math.PI * 2)
                => DrawEllipse(center, radius, radius, color, 0, startAngle, endAngle);

        /// <summary>
        /// Draws a circle outline with a brush.
        /// </summary>
        /// <param name="center">Center of the circle.</param>
        /// <param name="radius">Circle radius in pixels.</param>
        /// <param name="color">Brush providing the circle colors.</param>
        /// <param name="startAngle">Start angle in radians.</param>
        /// <param name="endAngle">End angle in radians.</param>
        public void DrawCircle(Point center, int radius, IBrush<T> color, double startAngle = 0, double endAngle = Math.PI * 2)
                => DrawEllipse(center, radius, radius, color, 0, startAngle, endAngle);

        /// <summary>
        /// Draws an ellipse outline with a solid color.
        /// </summary>
        /// <param name="center">Center of the ellipse.</param>
        /// <param name="radius1">Horizontal radius in pixels.</param>
        /// <param name="radius2">Vertical radius in pixels.</param>
        /// <param name="color">Color applied to the ellipse outline.</param>
        /// <param name="orientation">Ellipse rotation in radians.</param>
        /// <param name="startAngle">Start angle in radians.</param>
        /// <param name="endAngle">End angle in radians.</param>
        public void DrawEllipse(Point center, int radius1, int radius2, T color, double orientation = 0, double startAngle = 0, double endAngle = Math.PI * 2)
        {
            var ellipse = new Circle(center, radius1, radius2, orientation, startAngle, endAngle);
            DrawShape(new MapBrush<T>(color), ellipse);
        }

        /// <summary>
        /// Draws an ellipse outline with a brush.
        /// </summary>
        /// <param name="center">Center of the ellipse.</param>
        /// <param name="radius1">Horizontal radius in pixels.</param>
        /// <param name="radius2">Vertical radius in pixels.</param>
        /// <param name="color">Brush providing the ellipse colors.</param>
        /// <param name="orientation">Ellipse rotation in radians.</param>
        /// <param name="startAngle">Start angle in radians.</param>
        /// <param name="endAngle">End angle in radians.</param>
        public void DrawEllipse(Point center, int radius1, int radius2, IBrush<T> color, double orientation = 0, double startAngle = 0, double endAngle = Math.PI * 2)
        {
            var ellipse = new Circle(center, radius1, radius2, orientation, startAngle, endAngle);
            DrawShape(color, ellipse);
        }

        #endregion
        #region Rectangle
        /// <summary>
        /// Fills a rectangle with a solid color.
        /// </summary>
        /// <param name="r">Rectangle to fill.</param>
        /// <param name="color">Color applied to the rectangle.</param>
        public void FillRectangle(Rectangle r, T color)
                => FillRectangle(r.Top, r.Left, r.Bottom, r.Right, color);

        /// <summary>
        /// Fills a rectangle with a solid color.
        /// </summary>
        /// <param name="p1">First corner of the rectangle.</param>
        /// <param name="p2">Opposite corner of the rectangle.</param>
        /// <param name="color">Color applied to the rectangle.</param>
        public void FillRectangle(Point p1, Point p2, T color)
                => FillRectangle(p1.X, p1.Y, p2.X, p2.Y, color);

        /// <summary>
        /// Fills a rectangle with a solid color.
        /// </summary>
        /// <param name="x1">First corner horizontal coordinate.</param>
        /// <param name="y1">First corner vertical coordinate.</param>
        /// <param name="x2">Opposite corner horizontal coordinate.</param>
        /// <param name="y2">Opposite corner vertical coordinate.</param>
        /// <param name="color">Color applied to the rectangle.</param>
        public void FillRectangle(int x1, int y1, int x2, int y2, T color)
        {
            FillPolygon1(color, new Point(x1, y1), new Point(x2, y1), new Point(x2, y2), new Point(x1, y2));
        }
        #endregion

    }
}
