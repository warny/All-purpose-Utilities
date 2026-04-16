using System;
using System.Text.RegularExpressions;

namespace Utils.Mathematics;

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

        string finalized = Regex.Replace(text, @"\bein (?<l>[A-Z])", "eine ${l}");
        if (finalized.EndsWith("ein", StringComparison.Ordinal))
        {
            finalized += "s";
        }

        return finalized;
    }
}
