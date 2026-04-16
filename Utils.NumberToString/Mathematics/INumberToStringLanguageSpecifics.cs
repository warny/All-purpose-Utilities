using System;

namespace Utils.Mathematics;

/// <summary>
/// Defines language-specific post-processing behavior applied after number-to-text conversion.
/// </summary>
public interface INumberToStringLanguageSpecifics
{
    /// <summary>
    /// Finalizes a converted text according to language-specific writing rules.
    /// </summary>
    /// <param name="languageIdentifier">The culture or language identifier currently being converted.</param>
    /// <param name="text">The converted text to finalize.</param>
    /// <returns>The finalized text.</returns>
    string FinalizeWriting(string languageIdentifier, string text);
}
