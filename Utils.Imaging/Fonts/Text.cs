using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Utils.Drawing;
using Path = Utils.Drawing.Path;

namespace Utils.Fonts;

/// <summary>
/// Lays out a string of text as a set of filled glyph outlines and implements
/// <see cref="IDrawable"/> so that it can be passed directly to fill methods such as
/// <see cref="Utils.Drawing.DrawI{T}.FillShape2"/>.
/// </summary>
/// <remarks>
/// The class iterates over each character, retrieves its glyph via <see cref="IFont.GetGlyph"/>,
/// applies kerning corrections via <see cref="IFont.GetSpacingCorrection"/>, and collects the
/// resulting screen-space <see cref="Path"/> objects.  All paths are then exposed through the
/// <see cref="IDrawable"/> interface so that a single fill call covers the whole string.
///
/// UV coordinates produced by the fill are normalised over the bounding box of the entire text
/// block, making texture or gradient effects straightforward:
/// <code>
/// draw.FillShape2((u, v) => texture[(int)(u * w), (int)(v * h)], text);
/// </code>
/// </remarks>
public class Text : IDrawable
{
    private readonly List<Path> paths = [];

    /// <summary>
    /// Initializes a new <see cref="Text"/> instance using the font's own scale and baseline metrics.
    /// </summary>
    /// <param name="content">The string to render.</param>
    /// <param name="font">Font providing both glyph outlines and metrics (<see cref="IFont.Scale"/>, <see cref="IFont.BaseLineY"/>).</param>
    /// <param name="x">Horizontal pen position of the first character in pixels.</param>
    public Text(string content, IFont font, float x)
        : this(content, font, x, font.BaseLineY, font.Scale) { }


    /// <summary>
    /// Initializes a new <see cref="Text"/> instance by laying out <paramref name="content"/>
    /// using the provided font and transform parameters.
    /// </summary>
    /// <param name="content">The string to render.</param>
    /// <param name="font">Font used to retrieve glyphs and kerning data.</param>
    /// <param name="x">Horizontal pen position of the first character in pixels.</param>
    /// <param name="baselineY">
    /// Screen-space Y coordinate of the text baseline.
    /// Typically computed as <c>topY + ascent * scale</c> using the font's ascender value.
    /// </param>
    /// <param name="scale">
    /// Factor mapping font units to pixels, usually <c>desiredPixelHeight / unitsPerEm</c>.
    /// The Y axis is automatically flipped to convert from TrueType space (Y-up) to
    /// screen space (Y-down).
    /// </param>
    public Text(string content, IFont font, float x, float baselineY, float scale)
    {
        // TrueType Y axis points upward; screen Y points downward — flip Y.
        var glyphTransform = Matrix3x2.CreateScale(scale, -scale);

        char prev = '\0';
        foreach (char c in content)
        {
            if (prev != '\0')
                x += font.GetSpacingCorrection(prev, c) * scale;

            var glyph = font.GetGlyph(c);
            if (glyph != null)
            {
                var glyphPaths = new Paths<object>();
                glyphPaths.BeginDrawGlyph(x, baselineY, glyphTransform);
                glyph.ToGraphic(glyphPaths);
                glyphPaths.EndDrawGlyph();
                paths.AddRange(glyphPaths);
            }

            // Advance the pen by the hmtx advance width.  For characters with no outline
            // (whitespace, missing glyphs) fall back to ~0.4 em expressed in pixels.
            float advance = glyph != null ? glyph.Width * scale : 0f;
            x += advance > 0f ? advance : scale * 400f;

            prev = c;
        }
    }

    /// <summary>
    /// Gets the total length of all glyph outlines in pixels.
    /// </summary>
    public float Length => paths.SelectMany(p => p.GetSegments(false)).Sum(s => s.Length);

    /// <summary>
    /// Returns oriented points along all glyph outlines.
    /// </summary>
    /// <param name="closed">Whether each contour should be closed.</param>
    /// <param name="position">Starting accumulated length offset.</param>
    public IEnumerable<DrawPoint> GetPoints(bool closed, float position = 0)
    {
        foreach (var path in paths)
            foreach (var point in path.GetPoints(closed, position))
            {
                yield return point;
                position = point.Position;
            }
    }

    /// <summary>
    /// Returns all line segments composing the glyph outlines.
    /// </summary>
    /// <param name="closed">Whether each contour should be closed with a return segment.</param>
    public IEnumerable<Segment> GetSegments(bool closed)
    {
        foreach (var path in paths)
            foreach (var segment in path.GetSegments(closed))
                yield return segment;
    }
}
