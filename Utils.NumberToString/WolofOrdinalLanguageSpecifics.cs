using System;
using System.Collections.Generic;

namespace Utils.NumberToString;

/// <summary>
/// Domain guard for Wolof ordinals. Every supported value is produced by the declarative XML pipeline
/// (the suppletive <c>bu njëkk</c> for 1 and the <c>-éel</c> suffix on the last element of the
/// cardinal otherwise); this plugin never builds a form itself and only rejects the values whose
/// ordinal is not established.
/// </summary>
/// <remarks>
/// <para>Rejected with <see cref="NotSupportedException"/>, as deliberate linguistic limitations:</para>
/// <list type="bullet">
///   <item><description>zero — no consulted source attests an ordinal of zero;</description></item>
///   <item><description>the round thousands (1000, 2000 … 999000), whose last element is the
///   vowel-final <c>junni</c>: the only examples of its ordinal disagree (Omniglot <c>junneel</c>,
///   vowel dropped; Janga Wolof <c>junniéél</c>, vowel kept) and no academic or normative source
///   settles the junction.</description></item>
///   <item><description>every value from a million (NTS-21): the cardinals reach milyoŋ, milyaar and the
///   productive Conway names, but no consulted source exemplifies an ordinal above the thousands, so the
///   ordinal domain stays the one audited by NTS-16/NTS-19 (1 to 999 999).</description></item>
/// </list>
/// <para>The guard receives the absolute value, so negative values are bounded the same way.</para>
/// </remarks>
public sealed class WolofOrdinalLanguageSpecifics : INumberToStringLanguageSpecifics, IOrdinalLanguageSpecifics
{
    /// <summary>The value of the scale noun <c>junni</c>.</summary>
    private const long OneThousand = 1000;

    /// <summary>The value of the scale noun <c>milyoŋ</c>, the first value without an audited ordinal.</summary>
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
            throw new NotSupportedException("No attested ordinal form of zero exists for Wolof.");
        if (number >= OneMillion || number <= -OneMillion)
            throw new NotSupportedException(
                $"Wolof ordinal {number} is not supported: no ordinal from a million (milyoŋ) is established.");
        if (number % OneThousand == 0)
            throw new NotSupportedException(
                $"Wolof ordinal {number} is not supported: the ordinal of a cardinal ending in junni is not established.");
        // Every other value is formed by the declarative XML pipeline.
        return false;
    }
}
