namespace Utils.NumberToString;

/// <summary>
/// Describes how the ordinal of a round scale value is formed: a value whose highest non-zero group is
/// at a scale covered by <see cref="Scales"/> and whose lower groups are all zero (multiplier × scale
/// unit, e.g. 2 000 000 = 2 × million).
/// </summary>
/// <remarks>
/// <para>The ordinal is then built from the scale noun rather than from the assembled cardinal: a
/// multiplier of one is dropped (the singular scale noun alone), a larger multiplier is its own
/// cardinal (rendered with the caller's variants) followed by <see cref="MultiplierSeparator"/> and
/// the singular scale noun. That text then goes through the usual ordinal transformation (ordinal
/// replacements, word rules, stems, removeTrailing, the effective suffix of the selected variant).
/// Whole-number ordinal exceptions keep precedence.</para>
/// <para>A value is matched at its highest scale only: 10^9 is the ordinal of the billion scale, never
/// a thousand times the million scale.</para>
/// </remarks>
/// <param name="Scales">
/// The scale indices covered (1 = thousands, 2 = the next scale …), in the range syntax of
/// <see cref="NumberToStringConverter.ParseRangeExpression(string)"/> (e.g. <c>"2..4"</c>). Every
/// index must be at least 1 and nameable by the converter's scale; two rules must not cover the same
/// index.
/// </param>
/// <param name="MultiplierSeparator">The text between the multiplier and the scale noun; may be empty.</param>
public sealed record OrdinalScaleRule(string Scales, string MultiplierSeparator);
