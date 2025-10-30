using System.Globalization;

namespace Utils.Dates;

/// <summary>
/// Provides culture specific <see cref="DateFormulaLanguage"/> instances.
/// </summary>
public interface IDateFormulaLanguageProvider
{
    /// <summary>Retrieves the language configuration for a culture.</summary>
    /// <param name="culture">Culture to obtain configuration for.</param>
    /// <returns>The language configuration.</returns>
    DateFormulaLanguage GetLanguage(CultureInfo culture);
}
