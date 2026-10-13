namespace Utils.NumberToString;

/// <summary>
/// Selects how <see cref="INumberToStringConverter.Convert(decimal, int, DecimalFormatOptions?, string[])"/> reads the
/// decimal part of a number. A caller <see cref="DecimalFormatOptions.DecimalSuffix"/> takes precedence over every mode.
/// </summary>
public enum DecimalReadingMode
{
    /// <summary>
    /// The language decides: the configured <c>&lt;Fraction&gt;</c> for the number of decimal digits when there is one
    /// (<c>vingt et un virgule cinq centièmes</c>), otherwise the digits one by one when the language allows it at
    /// that precision (<c>decimalDigitByDigitMaxDigits</c>, unlimited when absent), otherwise
    /// <see cref="System.NotSupportedException"/>.
    /// </summary>
    LanguageDefault,

    /// <summary>
    /// Every decimal digit is read after the decimal separator word, a 0 with the language's standalone zero
    /// (<c>zéro virgule zéro zéro zéro un</c>), even when a <c>&lt;Fraction&gt;</c> is configured and whatever the
    /// language's fallback limit.
    /// </summary>
    DigitByDigit,

    /// <summary>
    /// The decimal part is read as a numerator over the named denominator 10^n of its <c>n</c> digits, without the
    /// decimal separator word: the fraction alone for a zero integer part (<c>un dix-millième</c>), joined to a non-zero
    /// integer part by the language's <c>mixedFractionSeparator</c> (<c>deux et un dix-millième</c>). A zero decimal
    /// part is not read. Throws <see cref="System.NotSupportedException"/> when the language names no denominator for
    /// that digit count, or declares no mixed-number connector and the integer part is not zero.
    /// </summary>
    MathematicalFraction,
}
