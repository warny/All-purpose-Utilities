namespace Utils.NumberToString;

/// <summary>
/// Chooses the form of an Arabic scale noun (e.g. ألف, "thousand") from its multiplier, following
/// the rule of the counted noun (تمييز العدد): the noun agrees with the last number written.
/// </summary>
/// <remarks>
/// <para>
/// Returns form keys only; the words are configured in XML (<c>&lt;ScaleForm&gt;</c>). With the
/// last number written taken as the multiplier's last two digits (Arabic writes 21-99 as a
/// single unit-and-tens element, "خمسة وأربعون"):
/// </para>
/// <list type="bullet">
///   <item><description><c>"singular"</c> — 1 (the noun alone, ألف) and whole hundreds (singular genitive after مائة, مائة ألف);</description></item>
///   <item><description><c>"dual"</c> — 2 (the dual alone, ألفان);</description></item>
///   <item><description><c>"plural"</c> — 3-10 (plural genitive, ثلاثة آلاف);</description></item>
///   <item><description><c>"singularAccusative"</c> — 11-99 (singular accusative, أحد عشر ألفًا).</description></item>
/// </list>
/// <para>
/// Hence 101 → singular (مائة وألف), 102 → dual (مائة وألفان), 103 and 110 → plural
/// (مائة وثلاثة آلاف), 111 and 121 → singularAccusative, 200 → singular (مائتا ألف). Sources:
/// Kalimah Center ("the counted noun follows the rules of the last number written"), al-Dirassa,
/// and the Virtual Arabic Language Academy (decision 29, "مئة وألف"). Removing the multiplier word
/// for 1 and 2 and the construct form of مائتان are text changes left to the configuration's
/// <c>onScale</c> replacements: this selector only chooses the key. Stateless and thread-safe.
/// </para>
/// </remarks>
public sealed class ArabicScaleLexicalFormSelector : ILexicalFormSelector
{
    /// <summary>Returns the form key governed by the multiplier in <paramref name="context"/>.</summary>
    /// <param name="context">The multiplier of the scale noun and the active variants (unused: the scale noun's form does not depend on the counted noun's gender).</param>
    /// <returns><c>"singular"</c>, <c>"dual"</c>, <c>"plural"</c> or <c>"singularAccusative"</c>.</returns>
    public string SelectForm(LexicalFormContext context)
    {
        int lastNumber = (int)(context.AbsoluteValue % 100);
        return lastNumber switch
        {
            0 or 1 => "singular",
            2 => "dual",
            <= 10 => "plural",
            _ => "singularAccusative",
        };
    }
}
