using System.Collections.Generic;

namespace Utils.NumberToString;

/// <summary>
/// Provides language-specific ordinal conversion for cases where ordinal formation
/// cannot be expressed through the XML configuration alone (e.g. languages with
/// complex morphological agreement or root-pattern transformations).
/// </summary>
/// <remarks>
/// Implement this interface alongside <see cref="INumberToStringLanguageSpecifics"/>
/// on the same class. When the configured <c>LanguageSpecifics</c> object implements
/// this interface, <see cref="NumberToStringConverter.ConvertOrdinal(int, string[])"/>
/// invokes <see cref="TryConvertOrdinal"/> before consulting the XML pipeline.
/// Returning <see langword="true"/> short-circuits exceptions, word rules, and suffix.
///
/// Example use cases: Polish (case/gender/number agreement), Russian (same),
/// Arabic (root-pattern transformations), German (full adjectival declension).
/// </remarks>
public interface IOrdinalLanguageSpecifics
{
    /// <summary>
    /// Attempts to convert a non-negative integer to its ordinal string representation.
    /// </summary>
    /// <param name="number">The absolute value of the number to convert (always ≥ 0).</param>
    /// <param name="activeVariants">
    /// The active dimension constraints, e.g. <c>{ "gender" → "feminin", "case" → "dativ" }</c>.
    /// Populated by merging the caller's explicit parameters with dimension defaults.
    /// </param>
    /// <param name="result">
    /// The ordinal string when this method returns <see langword="true"/>;
    /// otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> to use <paramref name="result"/> directly;
    /// <see langword="false"/> to fall through to the XML-based ordinal pipeline.
    /// </returns>
    bool TryConvertOrdinal(int number, IReadOnlyDictionary<string, string> activeVariants, out string? result);
}
