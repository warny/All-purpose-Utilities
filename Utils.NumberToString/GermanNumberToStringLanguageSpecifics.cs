using System;
using System.Text.RegularExpressions;

namespace Utils.NumberToString;

/// <summary>
/// Applies German writing-specific finalization rules to converted numeric texts.
/// </summary>
public sealed class GermanNumberToStringLanguageSpecifics : INumberToStringLanguageSpecifics
{
    /// <summary>
    /// Finalizes a converted German number text.
    /// </summary>
    /// <param name="languageIdentifier">The culture or language identifier currently being converted.</param>
    /// <param name="text">The text to finalize.</param>
    /// <returns>The finalized German text.</returns>
    public string FinalizeWriting(string languageIdentifier, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        // "ein Million" → "eine Million": scale names start with an uppercase letter
        // (firstLetterUpperCase="true"); this regex handles all generated levels (Billion,
        // Trillion …) without an explicit enumeration. Covered by XML Variants: Standalone
        // "ein" → "eins" and all case/gender inflections; this hook is only for scale words.
        return Regex.Replace(text, @"\bein (?<l>[A-Z])", "eine ${l}");
    }
}
