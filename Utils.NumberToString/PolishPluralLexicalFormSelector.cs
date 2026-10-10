using System.Numerics;

namespace Utils.NumberToString;

/// <summary>
/// Chooses the form of a noun counted by a Polish cardinal (e.g. a scale noun governed by its
/// multiplier) following the Unicode CLDR cardinal plural rules of <c>pl</c> for integers.
/// </summary>
/// <remarks>
/// <para>
/// Returns form keys only; the words are configured in XML (<c>&lt;ScaleForm&gt;</c>):
/// </para>
/// <list type="bullet">
///   <item><description><c>"one"</c> — exactly 1 (tysiąc);</description></item>
///   <item><description><c>"few"</c> — the last digit is 2-4 and the last two digits are not 12-14 (2, 22, 102: tysiące);</description></item>
///   <item><description><c>"many"</c> — every other integer, including 0, 21 and 101 (5, 12, 21: tysięcy).</description></item>
/// </list>
/// <para>
/// Unlike Russian, a compound ending in one takes the "many" form (dwadzieścia jeden tysięcy). Source:
/// Unicode CLDR, "Language Plural Rules", pl (<c>i = 1</c>; <c>i % 10 = 2..4 and i % 100 != 12..14</c>).
/// The rule only depends on the absolute value; the variants are ignored. Stateless and thread-safe.
/// </para>
/// </remarks>
public sealed class PolishPluralLexicalFormSelector : ILexicalFormSelector
{
    /// <summary>Returns the CLDR plural category of the absolute value in <paramref name="context"/>.</summary>
    /// <param name="context">The governing value and the active variants (unused).</param>
    /// <returns><c>"one"</c>, <c>"few"</c> or <c>"many"</c>.</returns>
    public string SelectForm(LexicalFormContext context)
    {
        BigInteger value = context.AbsoluteValue;
        if (value.IsOne) return "one";
        int lastTwo = (int)(value % 100);
        int last = lastTwo % 10;
        if (last is >= 2 and <= 4 && lastTwo is (< 12 or > 14)) return "few";
        return "many";
    }
}
