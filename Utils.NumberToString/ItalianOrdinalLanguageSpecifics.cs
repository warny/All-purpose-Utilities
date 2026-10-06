using System;
using System.Collections.Generic;

namespace Utils.NumberToString;

/// <summary>
/// Domain guard for Italian ordinals. Every supported value is produced by the declarative XML
/// pipeline (exceptions 1-10, the <c>&lt;OrdinalStem&gt;</c> rules and the <c>-esimo</c>/<c>-esima</c>
/// suffix, <c>&lt;OrdinalComposition&gt;</c> for the analytic forms and <c>&lt;OrdinalScale&gt;</c> for the
/// round values from a million); this plugin never builds a form itself and only rejects the values
/// whose ordinal is not established.
/// </summary>
/// <remarks>
/// <para>Validated domain: 1-1999 except 1110-1910, the round thousands 2000-999000 (NTS-13), 1010
/// (<c>millesimo decimo</c>) and 100001-100009 (<c>centomillesimoprimo</c> …) (NTS-15), and the round
/// multiples of milione, miliardo and bilione, 10^6-999 × 10^12 (<c>milionesimo</c>,
/// <c>duemilionesimo</c>, <c>miliardesimo</c>, <c>bilionesimo</c>) (NTS-17).</para>
/// <para>Rejected with <see cref="NotSupportedException"/>:</para>
/// <list type="bullet">
///   <item><description>zero — the NTS-12 decision is unchanged (<c>zeresimo</c> is only attested in special, mathematical uses);</description></item>
///   <item><description>1110, 1210 … 1910 — two analytic splits are possible (<c>millesimo centodecimo</c>, <c>millecentesimo decimo</c>) and none is attested (TODO NTS-15);</description></item>
///   <item><description>the other non-round thousands above 1999 — no consulted source establishes a canonical form, and Treccani ("centomillesimo") treats the synthetic <c>centomiladuesimo</c> as a partitive (TODO NTS-15);</description></item>
///   <item><description>the non-round values from a million, and every value from a biliardo (10^15) — no compound form and no biliardo/trilione ordinal is sourced (TODO NTS-18).</description></item>
/// </list>
/// </remarks>
public sealed class ItalianOrdinalLanguageSpecifics : INumberToStringLanguageSpecifics, IOrdinalLanguageSpecifics
{
    /// <summary>The first value formed on a scale noun (un milione).</summary>
    private const long OneMillion = 1_000_000;

    /// <summary>The first value outside the validated round scales (un biliardo).</summary>
    private const long OneBiliardo = 1_000_000_000_000_000;

    /// <inheritdoc />
    public string FinalizeWriting(string languageIdentifier, string text) => text;

    /// <inheritdoc />
    public bool TryConvertOrdinal(int number, IReadOnlyDictionary<string, string> activeVariants, out string? result)
        => TryConvertOrdinal((long)number, activeVariants, out result);

    /// <inheritdoc />
    public bool TryConvertOrdinal(long number, IReadOnlyDictionary<string, string> activeVariants, out string? result)
    {
        result = null;
        if (number == 0)
            throw new NotSupportedException("Italian has no ordinal form for zero in the supported domain.");
        if (number >= OneBiliardo)
            throw new NotSupportedException(
                $"Italian ordinal {number} is not supported: no ordinal of a biliardo and above is validated (NTS-18).");
        if (number >= OneMillion)
        {
            if (!IsRoundAtHighestScale(number))
                throw new NotSupportedException(
                    $"Italian ordinal {number} is not supported: no compound ordinal of one million and above is validated (NTS-18).");
            // Round multiples of milione, miliardo and bilione fall through to <OrdinalScale>.
            return false;
        }
        if (number >= 2000 && number % 1000 != 0 && number is not (>= 100_001 and <= 100_009))
            throw new NotSupportedException(
                $"Italian ordinal {number} is not supported: no canonical form is established for this non-round thousand (NTS-15).");
        if (number is > 1010 and < 2000 && number % 100 == 10)
            throw new NotSupportedException(
                $"Italian ordinal {number} is not supported: no analytic form is attested for the thousands ending in ten after 1010 (NTS-15).");
        // Validated values fall through to the declarative XML pipeline.
        return false;
    }

    /// <summary>
    /// Determines whether a value between one million and one biliardo has every group below its
    /// highest scale (milione, miliardo or bilione) equal to zero.
    /// </summary>
    /// <param name="number">A value in [10^6, 10^15).</param>
    /// <returns><see langword="true"/> when the value is a multiple of its highest scale unit.</returns>
    private static bool IsRoundAtHighestScale(long number)
    {
        long unit = number >= 1_000_000_000_000 ? 1_000_000_000_000
            : number >= 1_000_000_000 ? 1_000_000_000
            : OneMillion;
        return number % unit == 0;
    }
}
