using System.Numerics;

namespace Utils.NumberToString;

/// <summary>
/// Chooses the form of a noun counted by a Czech or Slovak cardinal (e.g. a scale noun governed by
/// its multiplier) following the Unicode CLDR cardinal plural rules of <c>cs</c> and <c>sk</c> for
/// integers.
/// </summary>
/// <remarks>
/// <para>
/// Returns form keys only; the words are configured in XML (<c>&lt;ScaleForm&gt;</c>):
/// </para>
/// <list type="bullet">
///   <item><description><c>"one"</c> — exactly 1 (CS milion, SK milión);</description></item>
///   <item><description><c>"few"</c> — exactly 2, 3 or 4 (CS dva miliony, SK štyri milióny);</description></item>
///   <item><description><c>"other"</c> — every other integer, including 0, 21, 22–24 and 102 (CS pět milionů,
///   dvacet dva milionů; SK päť miliónov, dvadsaťdva miliónov).</description></item>
/// </list>
/// <para>
/// Unlike Polish, a compound ending in 2–4 takes the "other" form: the Czech handbook (Internetová
/// jazyková příručka, ÚJČ AV ČR, "Počítaný předmět po číslovkách") calls the genitive after
/// <i>dvacet dva/tři/čtyři</i> "more common and more natural", and the Slovak rule writes the
/// compound as one indeclinable word governing the genitive plural (<i>dvadsaťdva žien</i>,
/// Jarošová, Slovenská reč 86/2, 2021). Source of the categories: Unicode CLDR, "Language Plural
/// Rules", cs sk (<c>i = 1 and v = 0</c>; <c>i = 2..4 and v = 0</c>; integers otherwise "other", "many"
/// being reserved for decimals). The rule only depends on the absolute value; the variants are
/// ignored. Stateless and thread-safe.
/// </para>
/// </remarks>
public sealed class CzechSlovakPluralLexicalFormSelector : ILexicalFormSelector
{
    /// <summary>Returns the CLDR integer plural category of the absolute value in <paramref name="context"/>.</summary>
    /// <param name="context">The governing value and the active variants (unused).</param>
    /// <returns><c>"one"</c>, <c>"few"</c> or <c>"other"</c>.</returns>
    public string SelectForm(LexicalFormContext context)
    {
        BigInteger value = context.AbsoluteValue;
        if (value.IsOne) return "one";
        if (value >= 2 && value <= 4) return "few";
        return "other";
    }
}
