using System;
using System.Collections.Generic;

namespace Utils.NumberToString;

/// <summary>
/// Domain guard for Wolof ordinals (NTS-16). Every supported value is produced by the declarative XML
/// pipeline (the suppletive <c>bu njëkk</c> for 1 and the <c>-eel</c> suffix on the last element of the
/// cardinal for 2-999); this plugin never builds a form itself and only rejects the values whose
/// ordinal would rest on an unsettled base.
/// </summary>
/// <remarks>
/// <para>Rejected with <see cref="NotSupportedException"/>:</para>
/// <list type="bullet">
///   <item><description>zero — no consulted source attests an ordinal of zero (NTS-14, unchanged);</description></item>
///   <item><description>every value from a thousand — the configured thousands cardinal diverges from
///   the sources (no <c>ak</c> after <c>junni</c>, <c>benn junni</c> for 1000) and the vowel-final
///   <c>junni</c> + <c>-eel</c> is exemplified by a single compilation (<c>junneel</c>); tracked by
///   NTS-19.</description></item>
/// </list>
/// <para>The guard receives the absolute value, so negative values are bounded the same way.</para>
/// </remarks>
public sealed class WolofOrdinalLanguageSpecifics : INumberToStringLanguageSpecifics, IOrdinalLanguageSpecifics
{
    /// <summary>The first value whose cardinal involves <c>junni</c>.</summary>
    private const long OneThousand = 1000;

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
            throw new NotSupportedException("No attested ordinal form of zero exists for Wolof.");
        if (number >= OneThousand)
            throw new NotSupportedException(
                $"Wolof ordinal {number} is not supported: the thousands cardinal it would be built on is not settled (NTS-19).");
        // 1-999 are formed by the declarative XML pipeline.
        return false;
    }
}
