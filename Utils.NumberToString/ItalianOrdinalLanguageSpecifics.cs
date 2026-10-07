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
/// <para>Supported domain: 1-1999 except 1110-1910, the round thousands 2000-999000 (NTS-13), 1010
/// (<c>millesimo decimo</c>) and 100001-100009 (<c>centomillesimoprimo</c> …) (NTS-15), and every
/// round multiple of milione, miliardo, bilione, biliardo and trilione that fits a <see cref="long"/>,
/// 10^6-9 × 10^18 (<c>milionesimo</c>, <c>duemilionesimo</c> … <c>trilionesimo</c>,
/// <c>novetrilionesimo</c>) (NTS-17, NTS-18).</para>
/// <para>Rejected with <see cref="NotSupportedException"/>. These are deliberate linguistic
/// limitations, not pending technical work: the engine could compose these values, but no consulted
/// normative source establishes a canonical composition or spelling.</para>
/// <list type="bullet">
///   <item><description>zero — the NTS-12 decision is unchanged (<c>zeresimo</c> is only attested in special, mathematical uses);</description></item>
///   <item><description>1110, 1210 … 1910 — two analytic splits are possible (<c>millesimo centodecimo</c>, <c>millecentesimo decimo</c>) and no source selects one;</description></item>
///   <item><description>the other non-round thousands above 1999 — Treccani attests the juxtaposition only on millesimo and centomillesimo, and treats the synthetic <c>centomiladuesimo</c> as a partitive;</description></item>
///   <item><description>the non-round values from a million — the only proposals (<c>milionesimoprimo</c>, <c>unmilioneunesimo</c>) are self-declared virtual extrapolations.</description></item>
/// </list>
/// </remarks>
public sealed class ItalianOrdinalLanguageSpecifics : INumberToStringLanguageSpecifics, IOrdinalLanguageSpecifics
{
    /// <summary>The first value formed on a scale noun (un milione).</summary>
    private const long OneMillion = 1_000_000;

    /// <summary>
    /// The units of the validated ordinal scales, ascending: milione, miliardo, bilione, biliardo and
    /// trilione. Trilione is the largest scale whose unit fits a <see cref="long"/>.
    /// </summary>
    private static readonly long[] ValidatedScaleUnits =
    [
        1_000_000,
        1_000_000_000,
        1_000_000_000_000,
        1_000_000_000_000_000,
        1_000_000_000_000_000_000,
    ];

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
        if (number >= OneMillion)
        {
            if (!IsRoundAtHighestScale(number))
                throw new NotSupportedException(
                    $"Italian ordinal {number} is not supported: no canonical compound ordinal of one million and above is established.");
            // Round multiples of milione … trilione fall through to <OrdinalScale>.
            return false;
        }
        if (number >= 2000 && number % 1000 != 0 && number is not (>= 100_001 and <= 100_009))
            throw new NotSupportedException(
                $"Italian ordinal {number} is not supported: no canonical form is established for this non-round thousand.");
        if (number is > 1010 and < 2000 && number % 100 == 10)
            throw new NotSupportedException(
                $"Italian ordinal {number} is not supported: no source selects the analytic split of the thousands ending in ten after 1010.");
        // Validated values fall through to the declarative XML pipeline.
        return false;
    }

    /// <summary>
    /// Determines whether a value from one million has every group below its highest scale
    /// (milione … trilione) equal to zero.
    /// </summary>
    /// <param name="number">A value of at least 10^6; any <see cref="long"/> is accepted without overflow.</param>
    /// <returns><see langword="true"/> when the value is a multiple of its highest scale unit.</returns>
    private static bool IsRoundAtHighestScale(long number)
    {
        long unit = OneMillion;
        foreach (long candidate in ValidatedScaleUnits)
        {
            if (candidate > number) break;
            unit = candidate;
        }
        return number % unit == 0;
    }
}
