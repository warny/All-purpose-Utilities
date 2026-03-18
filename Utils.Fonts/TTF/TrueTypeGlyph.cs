using System;
using Utils.Fonts.TTF.Tables.Glyf;

namespace Utils.Fonts.TTF;

/// <summary>
/// Represents a TrueType glyph and implements the <see cref="IGlyph"/> interface.
/// This class acts as a wrapper around a <see cref="GlyphBase"/> instance, delegating
/// rendering and metric calculations.
/// </summary>
public class TrueTypeGlyph : IGlyph
{
    private readonly GlyphBase glyph;
    private readonly float advanceWidth;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrueTypeGlyph"/> class.
    /// </summary>
    /// <param name="glyph">The base glyph data. Cannot be null.</param>
    /// <param name="advanceWidth">
    /// Horizontal advance width in font units, taken from the <c>hmtx</c> table.
    /// This is the distance the pen advances after drawing this glyph.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="glyph"/> is null.</exception>
    public TrueTypeGlyph(GlyphBase glyph, float advanceWidth)
    {
        this.glyph = glyph ?? throw new ArgumentNullException(nameof(glyph));
        this.advanceWidth = advanceWidth;
    }

    /// <summary>
    /// Gets the horizontal advance width of the glyph in font units (from the <c>hmtx</c> table).
    /// </summary>
    public float Width => advanceWidth;

    /// <summary>
    /// Gets the height of the glyph, computed as the difference between the maximum and minimum Y coordinates.
    /// </summary>
    public float Height => glyph.MaxY - glyph.MinY;

    /// <summary>
    /// Gets the baseline offset of the glyph.
    /// For most TrueType fonts, the baseline is typically defined as 0.
    /// </summary>
    public float BaseLine => 0f;

    /// <summary>
    /// Converts the glyph to drawing instructions using the specified graphic converter.
    /// </summary>
    /// <param name="graphicConverter">
    /// The graphic converter that receives the drawing instructions.
    /// </param>
    public void ToGraphic(IGraphicConverter graphicConverter)
    {
        glyph.Render(graphicConverter);
    }
}
