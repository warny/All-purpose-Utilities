namespace Utils.Mathematics;

/// <summary>
/// Default no-op implementation used when no language-specific finalization is configured.
/// </summary>
public sealed class DefaultNumberToStringLanguageSpecifics : INumberToStringLanguageSpecifics
{
    /// <summary>
    /// Returns the input text unchanged.
    /// </summary>
    /// <param name="languageIdentifier">The current language identifier.</param>
    /// <param name="text">The text to finalize.</param>
    /// <returns>The unmodified <paramref name="text"/>.</returns>
    public string FinalizeWriting(string languageIdentifier, string text) => text;
}
