using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using Utils.Imaging;
using Utils.Mathematics;
using System.Linq;
using Utils.Collections;
using System.Diagnostics;
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
        /// Fills shapes using the even-odd rule with a UV color mapping.
        /// </summary>
        /// <param name="color">Delegate providing colors for each pixel.</param>
        /// <param name="drawables">Shapes to fill.</param>
        public void FillShape1(UVMap<T> color, params IDrawable[] drawables)
                => FillShape1(color, (IEnumerable<IDrawable>)drawables);

        /// <summary>
        /// Fills shapes using the even-odd rule with a UV color mapping.
        /// </summary>
        /// <param name="color">Delegate providing colors for each pixel.</param>
        /// <param name="drawables">Shapes to fill.</param>
        public void FillShape1(UVMap<T> color, IEnumerable<IDrawable> drawables)
        {
            var points = drawables.SelectMany(d => d.GetPoints(true));
            foreach (var linePoints in points.GroupBy(p => p.Y))
            {
                var orderedPoints = linePoints.OrderBy(p => p.X);
                int direction = 0;
                foreach (var pair in orderedPoints.SlideEnumerateBy(2))
                {
                    int y = linePoints.Key;
                    direction += pair[0].VerticalDirection;
                    DrawPoint(pair[0].X, y, color(pair[0].X, y));
                    if (direction != 0)
                    {
                        for (int x = pair[0].X; x <= pair[1].X; x++)
                        {
                            DrawPoint(x, y, color(x, y));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Fills shapes using the non-zero winding rule with a UV color mapping.
        /// </summary>
        /// <param name="color">Delegate providing colors for each pixel.</param>
        /// <param name="drawables">Shapes to fill.</param>
        public void FillShape2(UVMap<T> color, params IDrawable[] drawables)
                => FillShape2(color, (IEnumerable<IDrawable>)drawables);

        /// <summary>
        /// Fills shapes using the non-zero winding rule with a UV color mapping.
        /// </summary>
        /// <param name="color">Delegate providing colors for each pixel.</param>
        /// <param name="drawables">Shapes to fill.</param>
        public void FillShape2(UVMap<T> color, IEnumerable<IDrawable> drawables)
        {
            var points = drawables.SelectMany(d => d.GetPoints(true));
            foreach (var linePoints in points.GroupBy(p => p.Y))
            {
                var orderedPoints = linePoints.OrderBy(p => p.X);
                int direction = 0;
                foreach (var pair in orderedPoints.SlideEnumerateBy(2))
                {
                    int y = linePoints.Key;
                    direction += pair[0].VerticalDirection;
                    DrawPoint(pair[0].X, y, color(pair[0].X, y));
                    if (MathEx.Mod(direction, 2) != 0)
                    {
                        for (int x = pair[0].X; x <= pair[1].X; x++)
                        {
                            DrawPoint(x, y, color(x, y));
                        }
                    }
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
