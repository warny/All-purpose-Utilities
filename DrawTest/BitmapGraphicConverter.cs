using System.Drawing;
using System.Linq;
using System.Numerics;
using Utils.Drawing;
using Utils.Fonts;
using Utils.Imaging;

namespace DrawTest;

/// <summary>
/// An <see cref="IGraphicConverter"/> that rasterises glyph outlines directly onto a bitmap
/// using <see cref="DrawI{T}"/>.
/// </summary>
/// <remarks>
/// Each glyph rendering pass is bracketed by <see cref="BeginDrawGlyph"/> /
/// <see cref="EndDrawGlyph"/>, which set up the affine transform (scale, position) for
/// that glyph. Between those calls the pen-movement commands (<see cref="StartAt"/>,
/// <see cref="LineTo"/>, <see cref="BezierTo"/>, <see cref="ClosePath"/>) receive
/// glyph-local coordinates that are mapped to screen pixels via the active transform.
/// </remarks>
public class BitmapGraphicConverter : IGraphicConverter
{
    private readonly DrawI<ColorArgb32> draw;
    private readonly ColorArgb32 color;

    /// <summary>Combined affine transform active for the current glyph.</summary>
    private Matrix3x2 currentTransform = Matrix3x2.Identity;

    /// <summary>Current pen position in glyph-local coordinates.</summary>
    private PointF currentPoint;

    /// <summary>Start of the current subpath (used by <see cref="ClosePath"/>).</summary>
    private PointF subpathStart;

    /// <summary>
    /// Initialises a new <see cref="BitmapGraphicConverter"/> that draws with the given colour.
    /// </summary>
    /// <param name="draw">The low-level bitmap drawing surface.</param>
    /// <param name="color">Stroke colour for all glyph outlines.</param>
    public BitmapGraphicConverter(DrawI<ColorArgb32> draw, ColorArgb32 color)
    {
        this.draw = draw;
        this.color = color;
    }

    /// <summary>Transforms a glyph-local coordinate to an integer screen pixel.</summary>
    private Point ToPoint(float x, float y)
    {
        var v = Vector2.Transform(new Vector2(x, y), currentTransform);
        return new Point((int)MathF.Round(v.X), (int)MathF.Round(v.Y));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Builds <c>currentTransform = transform × Translation(x, y)</c> so that
    /// glyph-local coordinates are first scaled/rotated by <paramref name="transform"/>
    /// and then offset to the absolute position <paramref name="x"/>, <paramref name="y"/>.
    /// </remarks>
    public void BeginDrawGlyph(float x, float y, Matrix3x2 transform)
    {
        currentTransform = transform * Matrix3x2.CreateTranslation(x, y);
    }

    /// <inheritdoc/>
    public void EndDrawGlyph() => currentTransform = Matrix3x2.Identity;

    /// <inheritdoc/>
    public void StartAt(float x, float y)
    {
        currentPoint = new PointF(x, y);
        subpathStart = currentPoint;
    }

    /// <inheritdoc/>
    public void LineTo(float x, float y)
    {
        draw.DrawLine(ToPoint(currentPoint.X, currentPoint.Y), ToPoint(x, y), color);
        currentPoint = new PointF(x, y);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <paramref name="points"/> contains the control point(s) and the end point only —
    /// the start is the current pen position. <see cref="DrawI{T}.DrawBezier"/> expects
    /// all points, so the current position is prepended before the call.
    /// </remarks>
    public void BezierTo(params (float x, float y)[] points)
    {
        var pts = new[] { ToPoint(currentPoint.X, currentPoint.Y) }
            .Concat(points.Select(p => ToPoint(p.x, p.y)))
            .ToArray();
        draw.DrawBezier(color, pts);
        currentPoint = new PointF(points[^1].x, points[^1].y);
    }

    /// <inheritdoc/>
    public void ClosePath()
    {
        draw.DrawLine(
            ToPoint(currentPoint.X, currentPoint.Y),
            ToPoint(subpathStart.X, subpathStart.Y),
            color);
        currentPoint = subpathStart;
    }
}
