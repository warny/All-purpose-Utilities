using System;
using System.Collections.Generic;

namespace Utils.NumberToString;

/// <summary>
/// Shared domain guard for languages whose productive ordinal suffix or prefix would mechanically
/// form an ordinal of zero that no consulted source attests (NTS-14: Catalan "zeroè"; NTS-20: Ewe
/// "naneke o" + "-lia"; Wolof uses <see cref="WolofOrdinalLanguageSpecifics"/>). Zero is rejected with <see cref="NotSupportedException"/>, like the
/// languages without any zero formation (NTS-12); every other value falls through to the
/// declarative ordinal pipeline unchanged. Builds no text and finalizes nothing.
/// </summary>
/// <remarks>
/// This is a per-language decision, deliberately opt-in through <c>&lt;LanguageSpecifics&gt;</c>:
/// the NTS-12 contract still accepts a suffix or prefix formation of zero in the languages that do
/// not reference this guard.
/// </remarks>
public sealed class ZeroOrdinalUnsupportedLanguageSpecifics : INumberToStringLanguageSpecifics, IOrdinalLanguageSpecifics
{
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
            throw new NotSupportedException("No attested ordinal form of zero exists for this language.");
        // Every other value is formed by the declarative XML pipeline.
        return false;
    }
}
