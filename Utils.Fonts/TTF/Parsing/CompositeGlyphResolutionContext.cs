using System.Collections.Generic;

namespace Utils.Fonts.TTF.Parsing;

/// <summary>
/// Tracks the shared, bounded state used while resolving a compound glyph's outline through a
/// (possibly multi-level) graph of component references. A single instance is created per
/// top-level <c>Contours</c> resolution and threaded through every recursive call, so that depth,
/// component, and point budgets apply to the whole expansion rather than being reset at each
/// recursion level -- and so that a cycle (a glyph that, directly or indirectly, references
/// itself) can be detected via <see cref="ActiveGlyphs"/> before it causes unbounded recursion.
/// </summary>
internal sealed class CompositeGlyphResolutionContext
{
    /// <summary>
    /// Gets the options supplying the resolution budgets
    /// (<see cref="TrueTypeFontParsingOptions.MaximumCompositeDepth"/>,
    /// <see cref="TrueTypeFontParsingOptions.MaximumExpandedComponents"/>,
    /// <see cref="TrueTypeFontParsingOptions.MaximumExpandedPoints"/>).
    /// </summary>
    public required TrueTypeFontParsingOptions Options { get; init; }

    /// <summary>
    /// Gets the stack of glyph indices currently being resolved, innermost last. A component
    /// whose target glyph index is already on this stack would close a cycle.
    /// </summary>
    public Stack<ushort> ActiveGlyphs { get; } = new();

    /// <summary>
    /// Gets or sets the current recursion depth, checked against
    /// <see cref="TrueTypeFontParsingOptions.MaximumCompositeDepth"/> before each descent.
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    /// Gets or sets the total number of components expanded so far across the whole resolution,
    /// checked against <see cref="TrueTypeFontParsingOptions.MaximumExpandedComponents"/>.
    /// </summary>
    public int ExpandedComponents { get; set; }

    /// <summary>
    /// Gets or sets the total number of points produced so far across the whole resolution,
    /// checked against <see cref="TrueTypeFontParsingOptions.MaximumExpandedPoints"/>.
    /// </summary>
    public int ExpandedPoints { get; set; }
}
