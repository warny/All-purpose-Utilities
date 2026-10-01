using System;
using System.Collections.Generic;

namespace Utils.NumberToString;

/// <summary>
/// Restricts Croatian ordinals to verified forms. Croatian ordinals are produced by the declarative
/// XML rules (only the last component is ordinal: <c>tisuću prvi</c>, <c>tri tisuće sedamsto
/// trideset treći</c>), and the single scale units have lexical ordinals (<c>tisućiti</c>,
/// <c>milijunti</c>, <c>milijarditi</c>). Every other round scale value, i.e. any multiple of 1000
/// other than the verified lexical units 1 000, 1 000 000 and 1 000 000 000 (2000., 21 000.,
/// 2 000 000., 10^12., ...), would end with an inflected scale noun whose ordinal form was not
/// verified, so this plugin rejects it instead of letting a cardinal or invented form through.
/// </summary>
public sealed class CroatianOrdinalLanguageSpecifics : INumberToStringLanguageSpecifics, IOrdinalLanguageSpecifics
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
        if (number != 0 && number % 1000 == 0 && number is not (1_000 or 1_000_000 or 1_000_000_000))
            throw new NotSupportedException($"Croatian ordinal {number} is a round scale value other than the verified lexical units 1000, 1000000 and 1000000000.");
        return false;
    }
}
