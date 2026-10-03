using System;
using System.Collections.Generic;

namespace Utils.NumberToString;

/// <summary>
/// Restricts Italian ordinals to the forms the declarative XML rules produce correctly: the
/// irregular 1-10 (<c>primo</c> … <c>decimo</c>), the teens 11-19 (<c>undicesimo</c> …
/// <c>diciannovesimo</c>), the round tens 20-90 (<c>ventesimo</c> … <c>novantesimo</c>),
/// <c>centesimo</c> and <c>millesimo</c>.
/// </summary>
/// <remarks>
/// Italian compound cardinals are written as one word (<c>ventuno</c>, <c>ventitré</c>,
/// <c>centottanta</c>). Their ordinal drops the final vowel of the soldered word except after
/// <c>tre</c> and <c>sei</c> (<c>ventunesimo</c>, <c>ventitreesimo</c>, <c>ventiseiesimo</c>), a
/// vowel-specific stem rule that the declarative suffix pipeline (one <c>removeTrailing</c>
/// string and whole-word rules) cannot express. Rather than letting a mechanical suffix produce
/// an invented form such as <c>ventunoesimo</c>, every other value, including zero, is rejected.
/// See TODO NTS-13.
/// </remarks>
public sealed class ItalianOrdinalLanguageSpecifics : INumberToStringLanguageSpecifics, IOrdinalLanguageSpecifics
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
        if (!IsVerified(number))
            throw new NotSupportedException(
                $"Italian ordinal {number} would be formed by a mechanical suffix on a soldered cardinal; only 1-20, the round tens, 100 and 1000 are verified.");
        // Verified values fall through to the declarative XML pipeline.
        return false;
    }

    /// <summary>Determines whether the declarative pipeline produces a verified ordinal for <paramref name="number"/>.</summary>
    /// <param name="number">The non-negative ordinal value.</param>
    /// <returns><see langword="true"/> for 1-20, 30-90 by tens, 100 and 1000.</returns>
    private static bool IsVerified(long number)
        => number is >= 1 and <= 20
            || (number is >= 30 and <= 90 && number % 10 == 0)
            || number is 100 or 1000;
}
