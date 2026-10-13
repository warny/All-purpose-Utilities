namespace Utils.NumberToString;

/// <summary>
/// Formatting options for the decimal part produced by
/// <see cref="INumberToStringConverter.Convert(decimal, int, DecimalFormatOptions?, string[])"/>.
/// </summary>
public sealed class DecimalFormatOptions
{
    /// <summary>
    /// Overrides the word used as decimal separator between the integer and decimal parts
    /// (e.g. <c>"euro(s)"</c> instead of the configured <c>"virgule"</c>).
    /// The <c>"(s)"</c> marker is pluralized against the integer part value.
    /// When <see langword="null"/>, the converter's configured decimal separator is used.
    /// </summary>
    public string? DecimalSeparator { get; init; }

    /// <summary>
    /// Overrides the denomination suffix appended after the decimal value
    /// (e.g. <c>"centime(s)"</c> instead of <c>"centième(s)"</c> from the language configuration).
    /// When set, the decimal value is always converted as a whole number after the decimal separator word,
    /// regardless of whether a <c>&lt;Fraction&gt;</c> entry is configured for that digit count and regardless of
    /// <see cref="ReadingMode"/>, which it takes precedence over.
    /// The <c>"(s)"</c> marker is pluralized against the decimal value.
    /// When <see langword="null"/>, <see cref="ReadingMode"/> decides how the decimal part is read.
    /// </summary>
    public string? DecimalSuffix { get; init; }

    /// <summary>
    /// Selects how the decimal part is read when no <see cref="DecimalSuffix"/> is given:
    /// <see cref="DecimalReadingMode.LanguageDefault"/> (default: the configured fraction, else the digits within the
    /// language's fallback limit, else <see cref="System.NotSupportedException"/>),
    /// <see cref="DecimalReadingMode.DigitByDigit"/> (every digit, a 0 read as the language's zero) or
    /// <see cref="DecimalReadingMode.MathematicalFraction"/> (a numerator over the named denominator 10^n, joined to a
    /// non-zero integer part by the language's mixed-number connector; <see cref="System.NotSupportedException"/> when
    /// the denominator or the needed connector is not configured).
    /// </summary>
    public DecimalReadingMode ReadingMode { get; init; } = DecimalReadingMode.LanguageDefault;

    /// <summary>
    /// When <see langword="true"/>, the decimal part is suppressed when it rounds to zero.
    /// Useful with a positive <c>mandatoryDecimalDigits</c> to avoid producing
    /// <c>"vingt et un euros zéro centime"</c> for a whole-number amount.
    /// </summary>
    public bool OmitZeroDecimals { get; init; }
}
