using System.Collections.Generic;

namespace Utils.NumberToString;

/// <summary>
/// Digit tables used to render the <em>multiplier</em> of the scales covered by <see cref="OnScale"/>
/// (e.g. the "two" of "two thousand"), for languages whose multiplier of a scale noun is built with a
/// different grammar than the standalone cardinal. Every other value, including the units chunk (scale 0),
/// is rendered with the default <see cref="NumberToStringConverterOptions.Groups"/>.
/// </summary>
/// <remarks>
/// <para>The set replaces the default tables as a whole (all levels together), so a multiplier never
/// mixes the units of one table with the tens of another. Its digits carry their own
/// <see cref="DigitType.Fusions"/>. Everything after the multiplier text is unchanged: the scale noun is
/// still appended (see <see cref="NumberToStringConverterOptions.ScaleMultiplierPosition"/>), then the
/// <c>onScale</c> replacements and variant rules run with the numeric multiplier, then the triggers.
/// Whole-number <see cref="NumberToStringConverterOptions.Exceptions"/> are shared by every table.</para>
/// <para>Validation (at converter construction): the range is required, every covered index is at least
/// 1, two sets never cover the same index, and the levels are exactly those of the default
/// <see cref="NumberToStringConverterOptions.Groups"/>.</para>
/// </remarks>
/// <param name="OnScale">
/// The scale indices covered (1 = thousands, 2 = the next scale …), in the range syntax of
/// <see cref="NumberToStringConverter.ParseRangeExpression(string)"/> (e.g. <c>"1"</c>, <c>"2..4"</c>, <c>"1.."</c>).
/// </param>
/// <param name="Groups">The digit tables keyed by group level, like <see cref="NumberToStringConverterOptions.Groups"/>.</param>
public sealed record ScaleScopedGroups(string OnScale, IReadOnlyDictionary<int, DigitListType> Groups);

/// <summary>
/// Position of a scale multiplier relative to the scale noun it multiplies.
/// </summary>
public enum ScaleMultiplierPosition
{
    /// <summary>The multiplier precedes the scale noun (historical default): "two thousand".</summary>
    BeforeScale,

    /// <summary>The multiplier follows the scale noun, e.g. Ewe <c>akpe eve</c> ("thousand two" = 2000).</summary>
    AfterScale,
}
