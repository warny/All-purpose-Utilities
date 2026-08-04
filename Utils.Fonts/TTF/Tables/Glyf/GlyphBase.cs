using System;
using System.Collections.Generic;
using System.Linq;
using Utils.Fonts.TTF.Parsing;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables.Glyf;

/// <summary>
/// Represents the base class for a TrueType glyph.
/// </summary>
public class GlyphBase
{
    /// <summary>
    /// Gets a value indicating whether this glyph is a compound glyph.
    /// By default, a glyph is not compound.
    /// </summary>
    public virtual bool IsCompound => false;

    /// <summary>
    /// Gets or sets the number of contours in the glyph.
    /// </summary>
    public short NumContours { get; set; }

    /// <summary>
    /// Gets or sets the minimum X coordinate of the glyph.
    /// </summary>
    public short MinX { get; set; }

    /// <summary>
    /// Gets or sets the minimum Y coordinate of the glyph.
    /// </summary>
    public short MinY { get; set; }

    /// <summary>
    /// Gets or sets the maximum X coordinate of the glyph.
    /// </summary>
    public short MaxX { get; set; }

    /// <summary>
    /// Gets or sets the maximum Y coordinate of the glyph.
    /// </summary>
    public short MaxY { get; set; }

    /// <summary>
    /// Gets the contours of the glyph.
    /// Each contour is a sequence of <see cref="TTFPoint"/> objects.
    /// By default, this property is not initialized.
    /// </summary>
    public virtual IEnumerable<IEnumerable<TTFPoint>> Contours { get; }

    /// <summary>
    /// Resolves this glyph's contours as a leaf of a (possibly compound) glyph expansion, counting
    /// its points against the shared <paramref name="context"/> budget. The default implementation
    /// (used by every non-compound glyph) simply returns <see cref="Contours"/> after accounting
    /// for its points; <see cref="GlyphCompound"/> overrides this to recurse into its components
    /// instead, sharing the same context so depth/component/point budgets apply across the whole
    /// expansion rather than being reset at each level.
    /// </summary>
    /// <param name="context">The shared resolution context for the current top-level glyph expansion.</param>
    internal virtual IEnumerable<IEnumerable<TTFPoint>> ResolveContours(CompositeGlyphResolutionContext context)
    {
        var contours = Contours;
        if (contours is null)
        {
            yield break;
        }
        foreach (var contour in contours)
        {
            var points = contour.ToList();
            context.ExpandedPoints += points.Count;
            if (context.ExpandedPoints > context.Options.MaximumExpandedPoints)
            {
                FontParsingContext.Reject(FontDiagnosticCode.ResourceLimitExceeded,
                    $"Composite glyph resolution exceeds MaximumExpandedPoints ({context.Options.MaximumExpandedPoints}).");
            }
            yield return points;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GlyphBase"/> class.
    /// Protected internal to allow instantiation by derived classes.
    /// </summary>
    protected internal GlyphBase() { }

    /// <summary>
    /// Gets or sets the GlyfTable associated with this glyph.
    /// </summary>
    internal GlyfTable GlyfTable { get; set; }

    /// <summary>
    /// Gets the length (in bytes) of the glyph data.
    /// The default value is 10 (the fixed-size glyph header: numberOfContours + the bounding box).
    /// </summary>
    /// <remarks>
    /// Returns <see cref="int"/>, not <see cref="short"/>: a glyph's serialized size (points,
    /// flags, coordinate deltas, instructions, or -- for a compound glyph -- components) can
    /// legitimately exceed <see cref="short.MaxValue"/> bytes, and narrowing it to a signed 16-bit
    /// value would silently wrap to a smaller or negative length, corrupting 'glyf' layout and the
    /// 'loca' offsets derived from it.
    /// </remarks>
    public virtual int Length => 10;

    /// <summary>
    /// Creates a new glyph from the provided data.
    /// </summary>
    /// <param name="data">The reader from which to read the glyph data.</param>
    /// <param name="glyfTable">The associated GlyfTable.</param>
    /// <returns>
    /// An instance of <see cref="GlyphBase"/> or a derived type, depending on the glyph type.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown if an unknown glyph type is encountered.
    /// </exception>
    public static GlyphBase CreateGlyf(Reader data, GlyfTable glyfTable)
    {
        var numContours = data.Read<Int16>();
        GlyphBase glyf;
        if (numContours >= 0)
            glyf = new GlyphSimple();
        else if (numContours == -1)
        {
            glyf = new GlyphCompound();
        }
        else
        {
            throw new ArgumentException($"Unknown glyf type: {numContours}");
        }
        glyf.GlyfTable = glyfTable;
        glyf.NumContours = numContours;
        glyf.MinX = data.Read<Int16>();
        glyf.MinY = data.Read<Int16>();
        glyf.MaxX = data.Read<Int16>();
        glyf.MaxY = data.Read<Int16>();
        glyf.ReadData(data);
        return glyf;
    }

    /// <summary>
    /// Reads additional glyph-specific data from the provided reader.
    /// The default implementation does nothing; derived classes may override this.
    /// </summary>
    /// <param name="data">The reader from which to read the data.</param>
    public virtual void ReadData(Reader data) { }

    /// <summary>
    /// Writes the basic glyph data to the provided writer.
    /// </summary>
    /// <param name="data">The writer to which the glyph data is written.</param>
    public virtual void WriteData(Writer data)
    {
        data.Write<Int16>(NumContours);
        data.Write<Int16>(MinX);
        data.Write<Int16>(MinY);
        data.Write<Int16>(MaxX);
        data.Write<Int16>(MaxY);
    }

    /// <summary>
    /// Renders the glyph using the specified graphic converter.
    /// </summary>
    /// <param name="graphic">The graphic converter used to render the glyph.</param>
    public void Render(IGraphicConverter graphic)
    {
        // Contours is null for glyphs with no outline data (e.g. space).
        if (Contours == null) return;

        // Process each contour separately.
        foreach (var contour in Contours)
        {
            var pts = contour.ToList();
            if (pts.Count == 0)
                continue;

            // Begin at the first point.
            TTFPoint firstPoint = pts[0];
            graphic.StartAt(firstPoint.X, firstPoint.Y);

            // Initialize tracking variables.
            TTFPoint lastPoint = firstPoint;      // Last point processed.
            TTFPoint lastCurvePoint = firstPoint;   // Last point that was on-curve.

            // Process remaining points.
            for (int i = 1; i < pts.Count; i++)
            {
                TTFPoint current = pts[i];
                if (current.OnCurve)
                {
                    // When current point is on-curve:
                    // - If the previous point was on-curve (lastPoint equals lastCurvePoint),
                    //   simply draw a line.
                    // - Otherwise, draw a Bezier curve from the off-curve point to the current on-curve point.
                    if (lastPoint.Equals(lastCurvePoint))
                    {
                        graphic.LineTo(current.X, current.Y);
                    }
                    else
                    {
                        graphic.BezierTo((lastPoint.X, lastPoint.Y), (current.X, current.Y));
                    }
                    lastPoint = current;
                    lastCurvePoint = current;
                }
                else
                {
                    // When current point is off-curve:
                    // - If the previous point was also off-curve, compute an implied on-curve point
                    //   as the midpoint and draw a Bezier curve.
                    if (!lastPoint.OnCurve)
                    {
                        TTFPoint implied = TTFPoint.MidPoint(lastPoint, current);
                        graphic.BezierTo((lastPoint.X, lastPoint.Y), (implied.X, implied.Y));
                        lastCurvePoint = implied;
                    }
                    // Update lastPoint regardless.
                    lastPoint = current;
                }
            }

            // Close the contour. If the last point is off-curve, draw the final Bézier segment
            // to bring the pen to the first point before marking the contour as closed.
            if (!lastPoint.OnCurve)
            {
                graphic.BezierTo((lastPoint.X, lastPoint.Y), (firstPoint.X, firstPoint.Y));
            }
            graphic.ClosePath();
        }
    }
}
