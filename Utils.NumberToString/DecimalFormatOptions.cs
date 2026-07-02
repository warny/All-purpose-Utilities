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
    /// When set, the decimal value is always converted as a whole number regardless of whether
    /// a <c>&lt;Fraction&gt;</c> entry is configured for that digit count.
    /// The <c>"(s)"</c> marker is pluralized against the decimal value.
    /// When <see langword="null"/>, the configured <c>&lt;Fraction&gt;</c> entry is used when available;
    /// otherwise the decimal part is read digit by digit.
    /// </summary>
    public string? DecimalSuffix { get; init; }

    /// <summary>
    /// When <see langword="true"/>, the decimal part is suppressed when it rounds to zero.
    /// Useful with a positive <c>mandatoryDecimalDigits</c> to avoid producing
    /// <c>"vingt et un euros zéro centime"</c> for a whole-number amount.
    /// </summary>
    public bool OmitZeroDecimals { get; init; }
}
