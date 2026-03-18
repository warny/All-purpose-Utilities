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
        #region Stroke

        /// <summary>Default miter ratio used when no explicit limit is supplied.</summary>
        private const float DefaultMiterLimit = 4f;

        /// <summary>
        /// Draws one or more shapes with a given stroke width and join style, using a
        /// color function that receives the arc length from the start and the signed
        /// perpendicular distance from the centre line.
        /// </summary>
        /// <param name="color">
        /// Delegate <c>(arcLength, perpDist) → T</c>:
        /// <list type="bullet">
        ///   <item><description><c>arcLength</c> — accumulated path length from the start of the shape to the current pixel, in pixels.</description></item>
        ///   <item><description><c>perpDist</c> — signed perpendicular distance from the centre line, in pixels.
        ///     Positive values are on the left side of the stroke direction; negative on the right.
        ///     Ranges in <c>[−width/2, +width/2]</c> along segments; uses <c>outerSign × distanceFromJoint</c> in join areas.</description></item>
        /// </list>
        /// </param>
        /// <param name="width">Total stroke width in pixels.</param>
        /// <param name="join">Join style applied at every vertex where two segments meet.</param>
        /// <param name="drawables">Shapes to stroke.</param>
        public void DrawShapeThick(Func<float, float, T> color, float width, JoinStyle join, params IDrawable[] drawables)
            => DrawShapeThick(color, width, join, DefaultMiterLimit, (IEnumerable<IDrawable>)drawables);

        /// <summary>
        /// Draws shapes with a given stroke width, join style and explicit miter limit.
        /// </summary>
        /// <param name="color">Color delegate — see <see cref="DrawShapeThick(Func{float,float,T},float,JoinStyle,IDrawable[])"/>.</param>
        /// <param name="width">Total stroke width in pixels.</param>
        /// <param name="join">Join style.</param>
        /// <param name="miterLimit">
        /// For <see cref="JoinStyle.Miter"/>: maximum allowed ratio of miter length to
        /// <c>width/2</c>.  When exceeded the join falls back to <see cref="JoinStyle.Bevel"/>.
        /// Ignored for other join styles.
        /// </param>
        /// <param name="drawables">Shapes to stroke.</param>
        public void DrawShapeThick(Func<float, float, T> color, float width, JoinStyle join, float miterLimit, params IDrawable[] drawables)
            => DrawShapeThick(color, width, join, miterLimit, (IEnumerable<IDrawable>)drawables);

        /// <inheritdoc cref="DrawShapeThick(Func{float,float,T},float,JoinStyle,IDrawable[])"/>
        public void DrawShapeThick(Func<float, float, T> color, float width, JoinStyle join, IEnumerable<IDrawable> drawables)
            => DrawShapeThick(color, width, join, DefaultMiterLimit, drawables);

        /// <inheritdoc cref="DrawShapeThick(Func{float,float,T},float,JoinStyle,float,IDrawable[])"/>
        public void DrawShapeThick(Func<float, float, T> color, float width, JoinStyle join, float miterLimit, IEnumerable<IDrawable> drawables)
        {
            float halfWidth = width * 0.5f;
            foreach (var drawable in drawables)
                DrawShapeThickCore(color, drawable, halfWidth, join, miterLimit);
        }

        /// <summary>
        /// Draws shapes with a uniform solid colour and the given stroke width.
        /// </summary>
        /// <param name="color">Solid color applied to every stroke pixel.</param>
        /// <param name="width">Total stroke width in pixels.</param>
        /// <param name="join">Join style.</param>
        /// <param name="drawables">Shapes to stroke.</param>
        public void DrawShapeThick(T color, float width, JoinStyle join, params IDrawable[] drawables)
            => DrawShapeThick((_, _) => color, width, join, drawables);

        /// <summary>
        /// Draws shapes with a uniform solid colour, given stroke width and explicit miter limit.
        /// </summary>
        /// <param name="color">Solid color applied to every stroke pixel.</param>
        /// <param name="width">Total stroke width in pixels.</param>
        /// <param name="join">Join style.</param>
        /// <param name="miterLimit">Maximum miter-to-half-width ratio before falling back to bevel.</param>
        /// <param name="drawables">Shapes to stroke.</param>
        public void DrawShapeThick(T color, float width, JoinStyle join, float miterLimit, params IDrawable[] drawables)
            => DrawShapeThick((_, _) => color, width, join, miterLimit, drawables);

        /// <inheritdoc cref="DrawShapeThick(T,float,JoinStyle,IDrawable[])"/>
        public void DrawShapeThick(T color, float width, JoinStyle join, IEnumerable<IDrawable> drawables)
            => DrawShapeThick((_, _) => color, width, join, drawables);

        /// <inheritdoc cref="DrawShapeThick(T,float,JoinStyle,float,IDrawable[])"/>
        public void DrawShapeThick(T color, float width, JoinStyle join, float miterLimit, IEnumerable<IDrawable> drawables)
            => DrawShapeThick((_, _) => color, width, join, miterLimit, drawables);

        // ── Core ──────────────────────────────────────────────────────────────

        /// <summary>Rasterizes one drawable as a thick stroke.</summary>
        private void DrawShapeThickCore(
            Func<float, float, T> colorFunc,
            IDrawable drawable,
            float halfWidth,
            JoinStyle joinStyle,
            float miterLimit)
        {
            var segments = drawable.GetSegments(false)
                .Where(s => s.Length > 1e-6f)
                .ToList();
            if (segments.Count == 0) return;

            float arcPos = 0f;
            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                RasterizeStripSegment(colorFunc, seg, arcPos, halfWidth);

                if (i + 1 < segments.Count)
                {
                    var next = segments[i + 1];
                    // Only join when the two segments actually share an endpoint.
                    if (MathF.Abs(seg.X2 - next.X1) < 0.5f && MathF.Abs(seg.Y2 - next.Y1) < 0.5f)
                        RasterizeStrokeJoin(colorFunc, seg, next, arcPos + seg.Length, halfWidth, joinStyle, miterLimit);
                }
                arcPos += seg.Length;
            }
        }

        // ── Segment strip ─────────────────────────────────────────────────────

        /// <summary>
        /// Fills the rectangular strip of <paramref name="seg"/> expanded by <paramref name="halfWidth"/>
        /// on each side.  For each pixel the arc position and signed perpendicular distance are computed
        /// analytically from the segment's unit tangent and normal.
        /// </summary>
        private void RasterizeStripSegment(
            Func<float, float, T> colorFunc,
            Segment seg,
            float arcStart,
            float halfWidth)
        {
            // Segment.Cos = (X2-X1)/Length, Segment.Sin = (Y2-Y1)/Length
            float tdx = seg.Cos, tdy = seg.Sin;   // unit tangent
            float nx = -seg.Sin, ny = seg.Cos;    // unit normal (left side)
            float len = seg.Length;

            int xMin = Math.Max(0,
                (int)MathF.Floor(MathF.Min(seg.X1, seg.X2) - halfWidth));
            int xMax = Math.Min(ImageAccessor.Width - 1,
                (int)MathF.Ceiling(MathF.Max(seg.X1, seg.X2) + halfWidth));
            int yMin = Math.Max(0,
                (int)MathF.Floor(MathF.Min(seg.Y1, seg.Y2) - halfWidth));
            int yMax = Math.Min(ImageAccessor.Height - 1,
                (int)MathF.Ceiling(MathF.Max(seg.Y1, seg.Y2) + halfWidth));

            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    float px = x - seg.X1, py = y - seg.Y1;
                    float t = px * tdx + py * tdy;  // along-tangent distance
                    float d = px * nx + py * ny;    // signed perpendicular distance
                    if (t >= 0f && t <= len && MathF.Abs(d) <= halfWidth)
                        DrawPoint(x, y, colorFunc(arcStart + t, d));
                }
            }
        }

        // ── Join ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Fills the gap at the join between two connected segments according to
        /// <paramref name="joinStyle"/>.
        /// </summary>
        private void RasterizeStrokeJoin(
            Func<float, float, T> colorFunc,
            Segment seg1,
            Segment seg2,
            float arcPos,
            float halfWidth,
            JoinStyle joinStyle,
            float miterLimit)
        {
            float nx1 = -seg1.Sin, ny1 = seg1.Cos;  // left normal seg1
            float nx2 = -seg2.Sin, ny2 = seg2.Cos;  // left normal seg2
            float px = seg1.X2, py = seg1.Y2;

            // Cross product of unit tangents: positive → left turn, negative → right turn.
            float cross = seg1.Cos * seg2.Sin - seg1.Sin * seg2.Cos;
            if (MathF.Abs(cross) < 1e-6f) return; // collinear — no gap

            // The gap is on the outer side (+normal for left turn, −normal for right turn).
            float outerSign = cross > 0f ? 1f : -1f;

            // Outer corner positions at the join for each segment.
            float ox1x = px + outerSign * nx1 * halfWidth;
            float ox1y = py + outerSign * ny1 * halfWidth;
            float ox2x = px + outerSign * nx2 * halfWidth;
            float ox2y = py + outerSign * ny2 * halfWidth;

            switch (joinStyle)
            {
                case JoinStyle.Bevel:
                    RasterizeJoinTriangle(colorFunc, px, py, ox1x, ox1y, ox2x, ox2y, arcPos, halfWidth, outerSign);
                    break;

                case JoinStyle.Round:
                    RasterizeJoinRound(colorFunc, px, py, ox1x, ox1y, ox2x, ox2y, arcPos, halfWidth, outerSign);
                    break;

                case JoinStyle.Miter:
                default:
                {
                    // Bisector of the two outer normals.
                    float bx = outerSign * nx1 + outerSign * nx2;
                    float by = outerSign * ny1 + outerSign * ny2;
                    float blen = MathF.Sqrt(bx * bx + by * by);

                    bool fallback = blen < 1e-6f;
                    if (!fallback)
                    {
                        bx /= blen; by /= blen;
                        float cosHalf = bx * (outerSign * nx1) + by * (outerSign * ny1);
                        fallback = cosHalf < 1e-6f || (halfWidth / cosHalf) > miterLimit * halfWidth;
                        if (!fallback)
                        {
                            float miterLen = halfWidth / cosHalf;
                            float mx = px + bx * miterLen;
                            float my = py + by * miterLen;
                            // Fan: (P, ox1, M) + (P, M, ox2)
                            RasterizeJoinTriangle(colorFunc, px, py, ox1x, ox1y, mx, my, arcPos, halfWidth, outerSign);
                            RasterizeJoinTriangle(colorFunc, px, py, mx, my, ox2x, ox2y, arcPos, halfWidth, outerSign);
                        }
                    }
                    if (fallback)
                        RasterizeJoinTriangle(colorFunc, px, py, ox1x, ox1y, ox2x, ox2y, arcPos, halfWidth, outerSign);
                    break;
                }
            }
        }

        /// <summary>
        /// Rasterizes a triangle (P, A, B) as part of a stroke join.
        /// The signed distance passed to the color function is
        /// <c>outerSign × distanceFromP</c>, clamped to <c>halfWidth</c>.
        /// </summary>
        private void RasterizeJoinTriangle(
            Func<float, float, T> colorFunc,
            float px, float py,
            float ax, float ay,
            float bx, float by,
            float arcPos,
            float halfWidth,
            float outerSign)
        {
            int xMin = Math.Max(0,
                (int)MathF.Floor(MathF.Min(px, MathF.Min(ax, bx))));
            int xMax = Math.Min(ImageAccessor.Width - 1,
                (int)MathF.Ceiling(MathF.Max(px, MathF.Max(ax, bx))));
            int yMin = Math.Max(0,
                (int)MathF.Floor(MathF.Min(py, MathF.Min(ay, by))));
            int yMax = Math.Min(ImageAccessor.Height - 1,
                (int)MathF.Ceiling(MathF.Max(py, MathF.Max(ay, by))));

            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    if (!PointInTriangle(x, y, px, py, ax, ay, bx, by)) continue;
                    float dx = x - px, dy = y - py;
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    DrawPoint(x, y, colorFunc(arcPos, outerSign * MathF.Min(dist, halfWidth)));
                }
            }
        }

        /// <summary>
        /// Rasterizes the circular-arc join area, sweeping from the direction of
        /// outer corner 1 to outer corner 2 around the join point P.
        /// </summary>
        private void RasterizeJoinRound(
            Func<float, float, T> colorFunc,
            float px, float py,
            float ox1x, float ox1y,
            float ox2x, float ox2y,
            float arcPos,
            float halfWidth,
            float outerSign)
        {
            int xMin = Math.Max(0, (int)MathF.Floor(px - halfWidth));
            int xMax = Math.Min(ImageAccessor.Width - 1, (int)MathF.Ceiling(px + halfWidth));
            int yMin = Math.Max(0, (int)MathF.Floor(py - halfWidth));
            int yMax = Math.Min(ImageAccessor.Height - 1, (int)MathF.Ceiling(py + halfWidth));

            float a1 = MathF.Atan2(ox1y - py, ox1x - px);
            float a2 = MathF.Atan2(ox2y - py, ox2x - px);

            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    float dx = x - px, dy = y - py;
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    if (dist > halfWidth) continue;
                    float angle = MathF.Atan2(dy, dx);
                    if (IsAngleInArc(angle, a1, a2))
                        DrawPoint(x, y, colorFunc(arcPos, outerSign * dist));
                }
            }
        }

        // ── Geometry helpers ─────────────────────────────────────────────────

        /// <summary>
        /// Returns <see langword="true"/> when point (px, py) lies inside or on the
        /// boundary of the triangle (ax,ay)–(bx,by)–(cx,cy).
        /// Uses the sign-of-cross-product (barycentric) test.
        /// </summary>
        private static bool PointInTriangle(
            float px, float py,
            float ax, float ay,
            float bx, float by,
            float cx, float cy)
        {
            float d1 = (px - bx) * (ay - by) - (ax - bx) * (py - by);
            float d2 = (px - cx) * (by - cy) - (bx - cx) * (py - cy);
            float d3 = (px - ax) * (cy - ay) - (cx - ax) * (py - ay);
            bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNeg && hasPos);
        }

        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="angle"/> lies in the
        /// arc from <paramref name="a1"/> to <paramref name="a2"/>, sweeping through
        /// the shorter arc (≤ π) in the direction determined by <c>a2 − a1</c>.
        /// </summary>
        private static bool IsAngleInArc(float angle, float a1, float a2)
        {
            float span = NormalizeAnglePi(a2 - a1);
            float delta = NormalizeAnglePi(angle - a1);
            return span >= 0f
                ? delta >= 0f && delta <= span
                : delta <= 0f && delta >= span;
        }

        /// <summary>Normalises an angle to the half-open interval (−π, π].</summary>
        private static float NormalizeAnglePi(float a)
        {
            while (a > MathF.PI) a -= 2f * MathF.PI;
            while (a <= -MathF.PI) a += 2f * MathF.PI;
            return a;
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
