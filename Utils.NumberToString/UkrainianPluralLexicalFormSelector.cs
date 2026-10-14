namespace Utils.NumberToString;

/// <summary>
/// Chooses the form of a noun counted by a Ukrainian cardinal (e.g. a scale noun governed by its
/// multiplier) following the Unicode CLDR cardinal plural rules of <c>uk</c> for integers.
/// </summary>
/// <remarks>
/// <para>
/// Returns form keys only; the words are configured in XML (<c>&lt;ScaleForm&gt;</c>):
/// </para>
/// <list type="bullet">
///   <item><description><c>"one"</c> — the last digit is 1 and the last two digits are not 11 (1, 21, 101: тисяча, мільйон);</description></item>
///   <item><description><c>"few"</c> — the last digit is 2-4 and the last two digits are not 12-14 (2, 22, 104: тисячі, мільйони);</description></item>
///   <item><description><c>"many"</c> — every other integer, including 0 and 11-14 (5, 11, 12, 100: тисяч, мільйонів).</description></item>
/// </list>
/// <para>
/// Source: Unicode CLDR, "Language Plural Rules", uk, which shares its integer rule with ru
/// (<c>i % 10 = 1 and i % 100 != 11</c>; <c>i % 10 = 2..4 and i % 100 != 12..14</c>). A distinct type
/// keeps each configuration bound to its own language's rule. The rule only depends on the absolute
/// value; the variants are ignored. Stateless and thread-safe.
/// </para>
/// </remarks>
public sealed class UkrainianPluralLexicalFormSelector : ILexicalFormSelector
{
    /// <summary>Returns the CLDR plural category of the absolute value in <paramref name="context"/>.</summary>
    /// <param name="context">The governing value and the active variants (unused).</param>
    /// <returns><c>"one"</c>, <c>"few"</c> or <c>"many"</c>.</returns>
    public string SelectForm(LexicalFormContext context) => EastSlavicIntegerPluralRule.Select(context.AbsoluteValue);
}
