using System;
using System.Collections.Generic;

namespace Utils.NumberToString;

/// <summary>
/// Domain guard for Italian ordinals. Every supported value is produced by the declarative XML
/// pipeline (exceptions 1-10, then the <c>&lt;OrdinalStem&gt;</c> rules and the <c>-esimo</c>/<c>-esima</c>
/// suffix); this plugin never builds a form itself and only rejects the values whose ordinal is
/// not established.
/// </summary>
/// <remarks>
/// <para>Validated domain (NTS-13): 1-1999 except 1010-1910, and the round thousands 2000-999000
/// (<c>ventunesimo</c>, <c>ventitreesimo</c>, <c>ventiseiesimo</c>, <c>centunesimo</c>,
/// <c>centodecimo</c>, <c>milleunesimo</c>, <c>duemillesimo</c>, <c>centomillesimo</c>).</para>
/// <para>Rejected with <see cref="NotSupportedException"/>:</para>
/// <list type="bullet">
///   <item><description>zero — the NTS-12 decision is unchanged (<c>zeresimo</c> is only attested in special, mathematical uses);</description></item>
///   <item><description>the thousands ending in ten (1010, 1110 … 1910) — dieci keeps its lexical <c>decimo</c> in compounds, and only the analytic <c>millesimo decimo</c> is attested (TODO NTS-15);</description></item>
///   <item><description>non-round thousands above 1999 — no consulted source establishes a canonical synthetic form, and Treccani gives the analytic <c>centomillesimoprimo</c> for 100001 (TODO NTS-15);</description></item>
///   <item><description>one million and above — the Italian cardinals are known to be wrong there (TODO NTS-14).</description></item>
/// </list>
/// </remarks>
public sealed class ItalianOrdinalLanguageSpecifics : INumberToStringLanguageSpecifics, IOrdinalLanguageSpecifics
{
    /// <summary>The first value whose Italian cardinal is outside the validated domain (NTS-14).</summary>
    private const long OneMillion = 1_000_000;

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
            throw new NotSupportedException(
                $"Italian ordinal {number} is not supported: the Italian cardinals of one million and above are not validated (NTS-14).");
        if (number >= 2000 && number % 1000 != 0)
            throw new NotSupportedException(
                $"Italian ordinal {number} is not supported: no canonical form is established for non-round thousands above 1999 (NTS-15).");
        if (number > 1000 && number % 100 == 10)
            throw new NotSupportedException(
                $"Italian ordinal {number} is not supported: only the analytic 'millesimo decimo' type is attested for thousands ending in ten (NTS-15).");
        // Validated values fall through to the declarative XML pipeline.
        return false;
    }
}
