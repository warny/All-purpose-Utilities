namespace Utils.Fonts;

/// <summary>
/// Represents a generic font abstraction capable of retrieving glyphs and applying spacing corrections.
/// </summary>
public interface IFont
{
    /// <summary>
    /// Returns the glyph associated with a given character.
    /// </summary>
    /// <param name="c">The character to retrieve the glyph for.</param>
    /// <returns>An instance of <see cref="IGlyph"/> representing the character.</returns>
    IGlyph GetGlyph(char c);

    /// <summary>
    /// Returns the horizontal spacing correction between two adjacent characters.
    /// This can be used for kerning or other spacing adjustments.
    /// </summary>
    /// <param name="before">The preceding character.</param>
    /// <param name="after">The following character.</param>
    /// <returns>A float value representing the spacing correction in font units.</returns>
    float GetSpacingCorrection(char before, char after);
}

/// <summary>
/// Represents a glyph in a font, with dimensions and rendering capabilities.
/// </summary>
public interface IGlyph
{
    /// <summary>
    /// Gets the width of the glyph (advance width).
    /// </summary>
    float Width { get; }

    /// <summary>
    /// Gets the height of the glyph.
    /// </summary>
    float Height { get; }

    /// <summary>
    /// Gets the baseline offset of the glyph.
    /// </summary>
    float BaseLine { get; }

    /// <summary>
    /// Converts the glyph into graphic instructions using the given converter.
    /// </summary>
    /// <param name="graphicConverter">An implementation to receive drawing instructions.</param>
    void ToGraphic(IGraphicConverter graphicConverter);
}

/// <summary>
/// Defines a graphics drawing interface for rendering glyph vector outlines.
/// </summary>
public interface IGraphicConverter
{
    /// <summary>
    /// Starts a new drawing path at the given position.
    /// </summary>
    /// <param name="x">X coordinate in drawing units.</param>
    /// <param name="y">Y coordinate in drawing units.</param>
    void StartAt(float x, float y);

    /// <summary>
    /// Draws a straight line from the current position to the specified point.
    /// </summary>
    /// <param name="x">Target X coordinate.</param>
    /// <param name="y">Target Y coordinate.</param>
    void LineTo(float x, float y);

    /// <summary>
    /// Draws one or more Bézier curves from the current position using the given control points.
    /// </summary>
    /// <param name="points">An array of control and end points for the curve.</param>
    void BezierTo(params (float x, float y)[] points);
}
