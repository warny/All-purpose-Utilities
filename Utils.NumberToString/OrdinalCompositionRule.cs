namespace Utils.NumberToString;

/// <summary>
/// Describes an analytic ordinal: the ordinal of a value covered by <see cref="For"/> is the ordinal
/// of its head (value − value mod <see cref="Divisor"/>) followed by <see cref="Separator"/> and the
/// ordinal of its tail (value mod <see cref="Divisor"/>), e.g. 1010 → ordinal(1000) + " " +
/// ordinal(10).
/// </summary>
/// <remarks>
/// <para>The split is numeric, never textual: each part goes through the complete ordinal pipeline
/// again (ordinal plugin, exceptions, other compositions, <see cref="OrdinalScaleRule"/>, the
/// cardinal-based transformation) with the caller's variants, so both parts agree. Raw adjustment,
/// end triggers and language finalization run once on the composed result.</para>
/// <para>A value whose head or tail is zero is not composed and continues through the rest of the
/// pipeline. Both parts are strictly smaller than the value, so the recursion always terminates.
/// Whole-number ordinal exceptions keep precedence.</para>
/// </remarks>
/// <param name="For">
/// The values covered, in the range syntax of
/// <see cref="NumberToStringConverter.ParseRangeExpression(string)"/> (e.g. <c>"100001..100009"</c>).
/// Every value must be at least 1; the ranges of two rules must not overlap.
/// </param>
/// <param name="Divisor">The divisor separating head and tail; must be at least 2.</param>
/// <param name="Separator">The text between the two ordinals; may be empty for a soldered form.</param>
public sealed record OrdinalCompositionRule(string For, long Divisor, string Separator);
