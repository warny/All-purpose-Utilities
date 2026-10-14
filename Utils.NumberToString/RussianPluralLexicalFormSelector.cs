using System.Numerics;

namespace Utils.NumberToString;

/// <summary>
/// Chooses the form of a noun counted by a Russian cardinal (e.g. a scale noun governed by its
/// multiplier) following the Unicode CLDR cardinal plural rules of <c>ru</c> for integers.
/// </summary>
/// <remarks>
/// <para>
/// Returns form keys only; the words are configured in XML (<c>&lt;ScaleForm&gt;</c>):
/// </para>
/// <list type="bullet">
///   <item><description><c>"one"</c> — the last digit is 1 and the last two digits are not 11 (1, 21, 101: тысяча);</description></item>
///   <item><description><c>"few"</c> — the last digit is 2-4 and the last two digits are not 12-14 (2, 22, 104: тысячи);</description></item>
///   <item><description><c>"many"</c> — every other integer, including 0 and 11-14 (5, 11, 12, 100: тысяч).</description></item>
/// </list>
/// <para>
/// Source: Unicode CLDR, "Language Plural Rules", ru (<c>i % 10 = 1 and i % 100 != 11</c>;
/// <c>i % 10 = 2..4 and i % 100 != 12..14</c>). The rule only depends on the absolute value; the
/// variants are ignored. Stateless and thread-safe. CLDR gives Ukrainian the same integer rule; see
/// <see cref="UkrainianPluralLexicalFormSelector"/>, which shares this implementation.
/// </para>
/// </remarks>
public sealed class RussianPluralLexicalFormSelector : ILexicalFormSelector
{
    /// <summary>Returns the CLDR plural category of the absolute value in <paramref name="context"/>.</summary>
    /// <param name="context">The governing value and the active variants (unused).</param>
    /// <returns><c>"one"</c>, <c>"few"</c> or <c>"many"</c>.</returns>
    public string SelectForm(LexicalFormContext context) => EastSlavicIntegerPluralRule.Select(context.AbsoluteValue);
}

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

/// <summary>
/// The integer plural rule shared by the CLDR <c>ru</c> and <c>uk</c> locales: <c>"one"</c> for …1 except
/// …11, <c>"few"</c> for …2–4 except …12–14, <c>"many"</c> otherwise.
/// </summary>
internal static class EastSlavicIntegerPluralRule
{
    /// <summary>Returns the category of a non-negative integer.</summary>
    /// <param name="value">The absolute governing value.</param>
    /// <returns><c>"one"</c>, <c>"few"</c> or <c>"many"</c>.</returns>
    internal static string Select(BigInteger value)
    {
        int lastTwo = (int)(value % 100);
        int last = lastTwo % 10;
        if (last == 1 && lastTwo != 11) return "one";
        if (last is >= 2 and <= 4 && lastTwo is (< 12 or > 14)) return "few";
        return "many";
    }
}
