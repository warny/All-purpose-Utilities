using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using Utils.Imaging;
using Utils.Mathematics;
using System.Linq;

namespace Utils.Drawing
{
        /// <summary>
        /// Provides floating-point drawing helpers on top of <see cref="DrawI{T}"/>.
        /// </summary>
        /// <typeparam name="T">Type of the pixel value handled by the drawing operations.</typeparam>
        /// <remarks>
        /// <para>
        /// The floating-point API mirrors the integer-based <see cref="DrawI{T}"/> helpers while transparently
        /// converting coordinates expressed in an arbitrary viewport to pixel positions.  This allows callers to
        /// operate in normalized or physical spaces without manually performing the conversions required by the
        /// underlying <see cref="IImageAccessor{T}"/> implementation.
        /// </para>
        /// <para>
        /// All conversions clamp to the image bounds when delegated to <see cref="Draw"/>, ensuring consistency
        /// with the low-level routines.
        /// </para>
        /// </remarks>
        public class DrawF<T> : BaseDrawing<T>
        {
                /// <summary>
                /// Gets the integer-based drawing helper that ultimately performs the rasterization work.
                /// </summary>
                public DrawI<T> Draw { get; }

                /// <summary>
                /// Gets the top coordinate of the drawing viewport in the caller-provided space.
                /// </summary>
                public float Top { get; }

                /// <summary>
                /// Gets the left coordinate of the drawing viewport in the caller-provided space.
                /// </summary>
                public float Left { get; }

                /// <summary>
                /// Gets the right coordinate of the drawing viewport in the caller-provided space.
                /// </summary>
                public float Right { get; }

                /// <summary>
                /// Gets the bottom coordinate of the drawing viewport in the caller-provided space.
                /// </summary>
                public float Down { get; }

                private readonly float hRatio, vRatio;
                private readonly float hPrecision, vPrecision;

                /// <summary>
                /// Initializes a new instance of the <see cref="DrawF{T}"/> class covering the entire image.
                /// </summary>
                /// <param name="imageAccessor">Accessor used to manipulate the underlying image.</param>
                /// <remarks>
                /// The viewport matches the full image bounds so one drawing unit equals one pixel.
                /// </remarks>
                public DrawF(IImageAccessor<T> imageAccessor) : this(imageAccessor, 0, 0, imageAccessor.Width, imageAccessor.Height) { }

                /// <summary>
                /// Initializes a new instance of the <see cref="DrawF{T}"/> class using a custom viewport.
                /// </summary>
                /// <param name="imageAccessor">Accessor used to manipulate the underlying image.</param>
                /// <param name="top">Top boundary of the viewport.</param>
                /// <param name="left">Left boundary of the viewport.</param>
                /// <param name="right">Right boundary of the viewport.</param>
                /// <param name="down">Bottom boundary of the viewport.</param>
                /// <remarks>
                /// The viewport boundaries define the coordinate space exposed to callers; the constructor computes
                /// the scaling factors that map the viewport to pixel positions.
                /// </remarks>
                public DrawF(IImageAccessor<T> imageAccessor, float top, float left, float right, float down) : base (imageAccessor)
                {
                        Draw = new DrawI<T>(imageAccessor);
                        Top = top;
                        Left = left;
                        Right = right;
                        Down = down;
                        hRatio = ImageAccessor.Width / (Right - Left);
                        vRatio = ImageAccessor.Height / (Down - Top);
                        hPrecision = Math.Abs(1 / hRatio);
                        vPrecision = Math.Abs(1 / vRatio);
                }

                /// <summary>
                /// Computes the integer pixel corresponding to the provided floating-point coordinates.
                /// </summary>
                /// <param name="p">Coordinates expressed in the viewport space.</param>
                /// <returns>The integer pixel location within the image.</returns>
                /// <remarks>
                /// The conversion applies the viewport scaling and truncates the result to the nearest lower integer,
                /// matching the behavior of <see cref="Math.Floor(double)"/> for positive coordinates.
                /// </remarks>
                public Point ComputePixelPosition(PointF p) => ComputePixelPosition(p.X, p.Y);

                /// <summary>
                /// Computes the integer pixel corresponding to the provided floating-point coordinates.
                /// </summary>
                /// <param name="x">Horizontal coordinate in the viewport space.</param>
                /// <param name="y">Vertical coordinate in the viewport space.</param>
                /// <returns>The integer pixel location within the image.</returns>
                /// <remarks>
                /// Values falling outside the viewport range result in negative or oversized pixel indices; the
                /// downstream <see cref="Draw"/> helper clamps them to the image dimensions when actually drawing.
                /// </remarks>
                public Point ComputePixelPosition(float x, float y)
                {
                        return new Point(
                                (int)((x - Left) * hRatio),
                                (int)((y - Top) * vRatio)
                        );
                }

                /// <summary>
                /// Computes the floating-point pixel location corresponding to the provided viewport coordinates.
                /// </summary>
                /// <param name="p">Coordinates expressed in the viewport space.</param>
                /// <returns>The floating-point pixel position inside the image.</returns>
                /// <remarks>
                /// The method preserves the fractional part, which is useful for anti-aliasing or sub-pixel sampling
                /// scenarios.
                /// </remarks>
                public PointF ComputePixelPositionF(PointF p) => ComputePixelPositionF(p.X, p.Y);

                /// <summary>
                /// Computes the floating-point pixel location corresponding to the provided viewport coordinates.
                /// </summary>
                /// <param name="x">Horizontal coordinate in the viewport space.</param>
                /// <param name="y">Vertical coordinate in the viewport space.</param>
                /// <returns>The floating-point pixel position inside the image.</returns>
                /// <remarks>
                /// Unlike <see cref="ComputePixelPosition(float, float)"/>, this overload returns sub-pixel precision
                /// so callers can implement their own interpolation strategies.
                /// </remarks>
                public PointF ComputePixelPositionF(float x, float y)
                {
                        return new PointF(
                                (x - Left) * hRatio,
                                (y - Top) * vRatio
                        );
                }

                #region Point
                /// <summary>
                /// Converts a pixel coordinate to the viewport space.
                /// </summary>
                /// <param name="p">Pixel coordinate.</param>
                /// <returns>The corresponding viewport coordinate.</returns>
                public PointF ComputePoint(PointF p) => ComputePoint(p.X, p.Y);

                /// <summary>
                /// Converts a pixel coordinate to the viewport space.
                /// </summary>
                /// <param name="x">Pixel horizontal coordinate.</param>
                /// <param name="y">Pixel vertical coordinate.</param>
                /// <returns>The corresponding viewport coordinate.</returns>
                public PointF ComputePoint(float x, float y)
                {
                        return new PointF(
                                x / hRatio + Left,
                                y / vRatio + Top
                        );
                }

                /// <summary>
                /// Draws a single point expressed in viewport coordinates.
                /// </summary>
                /// <param name="point">Point in viewport coordinates.</param>
                /// <param name="color">Color to draw.</param>
                public void DrawPoint(PointF point, T color) => DrawPoint(point.X, point.Y, color);

                /// <summary>
                /// Draws a single point expressed in viewport coordinates.
                /// </summary>
                /// <param name="x">Horizontal coordinate.</param>
                /// <param name="y">Vertical coordinate.</param>
                /// <param name="color">Color to draw.</param>
                public void DrawPoint(float x, float y, T color)
                {
                        var p = ComputePixelPosition(x, y);
                        Draw.DrawPoint(p.X, p.Y, color);
                }
		#endregion
                #region Line
                /// <summary>
                /// Draws a line between two viewport coordinates using a solid color.
                /// </summary>
                /// <param name="p1">Starting point in viewport coordinates.</param>
                /// <param name="p2">Ending point in viewport coordinates.</param>
                /// <param name="color">Color to apply.</param>
                public void DrawLine(PointF p1, PointF p2, T color) => DrawLine(p1.X, p1.Y, p2.X, p2.Y, color);

                /// <summary>
                /// Draws a line between two viewport coordinates using a solid color.
                /// </summary>
                /// <param name="x1">Starting point horizontal coordinate.</param>
                /// <param name="y1">Starting point vertical coordinate.</param>
                /// <param name="x2">Ending point horizontal coordinate.</param>
                /// <param name="y2">Ending point vertical coordinate.</param>
                /// <param name="color">Color to apply.</param>
                public void DrawLine(float x1, float y1, float x2, float y2, T color)
                {
                        var p1 = ComputePixelPosition(x1, y1);
                        var p2 = ComputePixelPosition(x2, y2);
                        Draw.DrawLine(p1, p2, color);
                }

                /// <summary>
                /// Draws a line between two viewport coordinates using a brush.
                /// </summary>
                /// <param name="p1">Starting point in viewport coordinates.</param>
                /// <param name="p2">Ending point in viewport coordinates.</param>
                /// <param name="color">Brush providing the color values.</param>
                public void DrawLine(PointF p1, PointF p2, IBrush<T> color) => DrawLine(p1.X, p1.Y, p2.X, p2.Y, color);

                /// <summary>
                /// Draws a line between two viewport coordinates using a brush.
                /// </summary>
                /// <param name="x1">Starting point horizontal coordinate.</param>
                /// <param name="y1">Starting point vertical coordinate.</param>
                /// <param name="x2">Ending point horizontal coordinate.</param>
                /// <param name="y2">Ending point vertical coordinate.</param>
                /// <param name="color">Brush providing the color values.</param>
                public void DrawLine(float x1, float y1, float x2, float y2, IBrush<T> color)
                {
                        var p1 = ComputePixelPosition(x1, y1);
                        var p2 = ComputePixelPosition(x2, y2);
                        Draw.DrawLine(p1, p2, color);
                }

                #endregion
                #region Bezier
                /// <summary>
                /// Draws a Bézier curve using viewport coordinates.
                /// </summary>
                /// <param name="color">Color applied to the curve.</param>
                /// <param name="points">Control points expressed in viewport coordinates.</param>
                public void DrawBezier(T color, params PointF[] points) => Draw.DrawBezier(color, points.Select(p => ComputePixelPosition(p)).ToArray());

                //public void DrawBezier(Func<float, T> color, params PointF[] points) => Draw.DrawBezier(color, points.Select(p => ComputePixelPosition(p)).ToArray());
                #endregion
                #region Polygon
                /// <summary>
                /// Draws a polygon filled with a solid color.
                /// </summary>
                /// <param name="color">Color applied to the polygon.</param>
                /// <param name="points">Polygon vertices in viewport coordinates.</param>
                public void DrawPolygon(T color, IEnumerable<PointF> points) => Draw.DrawPolygon(color, points.Select(p => ComputePixelPosition(p)));
                /// <summary>
                /// Draws a polygon filled with a solid color.
                /// </summary>
                /// <param name="color">Color applied to the polygon.</param>
                /// <param name="points">Polygon vertices in viewport coordinates.</param>
                public void DrawPolygon(T color, params PointF[] points) => Draw.DrawPolygon(color, points.Select(p => ComputePixelPosition(p)).ToArray());
                /// <summary>
                /// Draws a polygon filled with a brush.
                /// </summary>
                /// <param name="color">Brush providing the polygon colors.</param>
                /// <param name="points">Polygon vertices in viewport coordinates.</param>
                public void DrawPolygon(IBrush<T> color, IEnumerable<PointF> points) => Draw.DrawPolygon(color, points.Select(p => ComputePixelPosition(p)));
                /// <summary>
                /// Draws a polygon filled with a brush.
                /// </summary>
                /// <param name="color">Brush providing the polygon colors.</param>
                /// <param name="points">Polygon vertices in viewport coordinates.</param>
                public void DrawPolygon(IBrush<T> color, params PointF[] points) => Draw.DrawPolygon(color, points.Select(p => ComputePixelPosition(p)).ToArray());
		#endregion
                #region Circle
                /// <summary>
                /// Fills a circle using viewport coordinates.
                /// </summary>
                /// <param name="center">Center of the circle.</param>
                /// <param name="radius">Radius of the circle.</param>
                /// <param name="color">Color applied to the circle.</param>
                /// <param name="startAngle">Start angle in radians.</param>
                /// <param name="endAngle">End angle in radians.</param>
                public void FillCircle(PointF center, float radius, T color, double startAngle = 0, double endAngle = Math.PI * 2)
                        => Draw.DrawEllipse(ComputePixelPosition(center), (int)(radius * hRatio), (int) (radius * vRatio), color, 0, startAngle, endAngle);

                /// <summary>
                /// Draws a circle outline using a brush.
                /// </summary>
                /// <param name="center">Center of the circle.</param>
                /// <param name="radius">Radius of the circle.</param>
                /// <param name="color">Brush providing the circle colors.</param>
                /// <param name="startAngle">Start angle in radians.</param>
                /// <param name="endAngle">End angle in radians.</param>
                public void DrawCircle(PointF center, float radius, IBrush<T> color, double startAngle = 0, double endAngle = Math.PI * 2)
                        => Draw.DrawEllipse(ComputePixelPosition(center), (int)(radius * hRatio), (int)(radius * vRatio), color, 0, startAngle, endAngle);

                /// <summary>
                /// Draws an ellipse outline with a solid color using viewport coordinates.
                /// </summary>
                /// <param name="center">Center of the ellipse.</param>
                /// <param name="radius1">Radius along the horizontal axis.</param>
                /// <param name="radius2">Radius along the vertical axis.</param>
                /// <param name="color">Color applied to the ellipse.</param>
                /// <param name="rotation">Rotation in radians.</param>
                /// <param name="startAngle">Start angle in radians.</param>
                /// <param name="endAngle">End angle in radians.</param>
                public void DrawEllipse(PointF center, float radius1, float radius2, T color, double rotation = 0, double startAngle = 0, double endAngle = Math.PI * 2)
                {
                        var points = ComputeEllipsePoints(center, radius1, radius2, rotation, startAngle, endAngle);
                        Draw.DrawPolygon(color, points);
                }

                /// <summary>
                /// Draws an ellipse outline with a brush using viewport coordinates.
                /// </summary>
                /// <param name="center">Center of the ellipse.</param>
                /// <param name="radius1">Radius along the horizontal axis.</param>
                /// <param name="radius2">Radius along the vertical axis.</param>
                /// <param name="color">Brush providing the ellipse colors.</param>
                /// <param name="rotation">Rotation in radians.</param>
                /// <param name="startAngle">Start angle in radians.</param>
                /// <param name="endAngle">End angle in radians.</param>
                public void DrawEllipse(PointF center, float radius1, float radius2, IBrush<T> color, double rotation = 0, double startAngle = 0, double endAngle = Math.PI * 2)
                {
                        var points = ComputeEllipsePoints(center, radius1, radius2, rotation, startAngle, endAngle);
                        Draw.DrawPolygon(color, points);
                }

                /// <summary>
                /// Computes the pixel coordinates used to approximate an ellipse arc.
                /// </summary>
                /// <param name="centerF">Center of the ellipse in viewport coordinates.</param>
                /// <param name="radius1">Radius along the horizontal axis.</param>
                /// <param name="radius2">Radius along the vertical axis.</param>
                /// <param name="rotation">Rotation in radians.</param>
                /// <param name="startAngle">Start angle in radians.</param>
                /// <param name="endAngle">End angle in radians.</param>
                /// <returns>A sequence of points describing the ellipse.</returns>
                private IEnumerable<Point> ComputeEllipsePoints(PointF centerF, float radius1, float radius2, double rotation, double startAngle, double endAngle)
		{
			Point center = ComputePixelPosition(centerF);
			double angle = endAngle - startAngle;
			if (Math.Abs(angle) > Math.PI * 2)
			{
				startAngle = 0;
				endAngle = Math.PI * 2;
			}
			var angularResolution = angle / (Math.Max(radius1, radius2) * Math.PI * 2);

			var cosR = Math.Cos(rotation);
			var sinR = Math.Sin(rotation);

			Func<double, bool> test;
			if (startAngle > endAngle)
			{
				test = alpha => alpha >= endAngle;
			}
			else
			{
				test = alpha => alpha <= endAngle;
			}

			double delta1 = Math.Sin(startAngle) * radius1;
			double delta2 = Math.Cos(startAngle) * radius2;

			int deltaX = (int)((cosR * delta1 + sinR * delta2) * hRatio);
			int deltaY = (int)((sinR * delta1 + cosR * delta2) * vRatio);

			Point lastPoint = new Point(center.X + deltaX, center.Y + deltaY);
			yield return lastPoint;

			for (double a = startAngle + angularResolution; test(a); a += angularResolution)
			{
				delta1 = Math.Sin(a) * radius1;
				delta2 = Math.Cos(a) * radius2;

				deltaX = (int)(cosR * delta1 + sinR * delta2);
				deltaY = (int)(sinR * delta1 + cosR * delta2);

				var newPoint = new Point(center.X + deltaX, center.Y + deltaY);
				if (newPoint.X == lastPoint.X && newPoint.Y == lastPoint.Y) continue;
				lastPoint = newPoint;
				yield return newPoint;
			}
		}

		#endregion
                #region Rectangle
                /// <summary>
                /// Fills a rectangle defined in viewport coordinates.
                /// </summary>
                /// <param name="r">Rectangle to fill.</param>
                /// <param name="color">Color applied to the rectangle.</param>
                public void FillRectangle(RectangleF r, T color)
                        => FillRectangle(r.Top, r.Left, r.Bottom, r.Right, color);

                /// <summary>
                /// Fills a rectangle defined by two viewport points.
                /// </summary>
                /// <param name="p1">First point of the rectangle.</param>
                /// <param name="p2">Opposite corner of the rectangle.</param>
                /// <param name="color">Color applied to the rectangle.</param>
                public void FillRectangle(PointF p1, PointF p2, T color)
                        => FillRectangle(p1.X, p1.Y, p2.X, p2.Y, color);

                /// <summary>
                /// Fills a rectangle defined by its viewport coordinates.
                /// </summary>
                /// <param name="x1">First corner horizontal coordinate.</param>
                /// <param name="y1">First corner vertical coordinate.</param>
                /// <param name="x2">Opposite corner horizontal coordinate.</param>
                /// <param name="y2">Opposite corner vertical coordinate.</param>
                /// <param name="color">Color applied to the rectangle.</param>
                public void FillRectangle(float x1, float y1, float x2, float y2, T color)
                {
                        var p1 = ComputePixelPosition(x1, y1);
                        var p2 = ComputePixelPosition(x2, y2);
                        Draw.FillRectangle(p1.X, p1.Y, p2.X, p2.Y, color);
                }
                #endregion

	}
}
