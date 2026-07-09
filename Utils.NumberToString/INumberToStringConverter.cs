using System;
using System.Numerics;
using Utils.Numerics;

namespace Utils.NumberToString
{
    /// <summary>
    /// Converts numeric values into a formatted string representation while exposing
    /// the largest supported number for validation purposes.
    /// </summary>
    public interface INumberToStringConverter
    {
        /// <summary>
        /// Gets the largest integer value that can be converted reliably, or
        /// <see langword="null"/> if the converter does not impose a limit.
        /// </summary>
        BigInteger? MaxNumber { get; }

        /// <summary>
        /// Gets the declared variant dimensions for this language, each with its ordered
        /// list of valid values. The first value of each dimension is the default applied
        /// when no explicit variant parameter is supplied to a conversion method.
        /// Returns an empty list when the language declares no variants.
        /// Implementations that pre-date variant support may leave this at its default (empty).
        /// </summary>
        IReadOnlyList<NumberToStringConverter.VariantDimension> VariantDimensions
        {
            get => [];
        }

        /// <summary>
        /// Converts an arbitrarily large integer into its string representation.
        /// </summary>
        /// <param name="number">The value to convert.</param>
        /// <returns>The formatted number.</returns>
        string Convert(BigInteger number);

        /// <summary>
        /// Converts a 32-bit signed integer into its string representation.
        /// </summary>
        /// <param name="number">The value to convert.</param>
        /// <returns>The formatted number.</returns>
        string Convert(int number);

        /// <summary>
        /// Converts a 64-bit signed integer into its string representation.
        /// </summary>
        /// <param name="number">The value to convert.</param>
        /// <returns>The formatted number.</returns>
        string Convert(long number);

        /// <summary>
        /// Converts a decimal value into its string representation.
        /// </summary>
        /// <param name="number">The value to convert.</param>
        /// <returns>The formatted number.</returns>
        string Convert(decimal number);

        /// <summary>
        /// Converts a decimal value into its string representation, applying the specified
        /// variant parameters (e.g. <c>"gender=feminin"</c>).
        /// The default implementation ignores variant parameters and delegates to
        /// <see cref="Convert(decimal)"/>; implementations that support variants should override.
        /// </summary>
        /// <param name="number">The value to convert.</param>
        /// <param name="variants">Zero or more <c>"dimension=value"</c> strings.</param>
        /// <returns>The formatted number with the requested variants applied.</returns>
        string Convert(decimal number, params string[] variants) => Convert(number);

        /// <summary>
        /// Converts a decimal value into its string representation with a mandatory number of
        /// decimal digits, applying optional variant parameters.
        /// <para>
        /// When <paramref name="mandatoryDecimalDigits"/> is negative, the decimal part is shown
        /// as-is (same as <see cref="Convert(decimal, string[])"/>).
        /// When zero, the decimal part is suppressed (only the integer part is shown).
        /// When positive, the value is rounded to that many decimal places and the decimal part
        /// is always shown with exactly that many digits, padding with zeros if needed.
        /// </para>
        /// The default implementation ignores precision and variant parameters and delegates to
        /// <see cref="Convert(decimal)"/>; implementations should override.
        /// </summary>
        /// <param name="number">The value to convert.</param>
        /// <param name="mandatoryDecimalDigits">
        /// Number of decimal digits to always show. Negative = natural (as-is), 0 = integer only,
        /// positive = always show exactly N decimal digits (rounded and zero-padded).
        /// </param>
        /// <param name="variants">Zero or more <c>"dimension=value"</c> strings.</param>
        /// <returns>The formatted number with the requested precision and variants applied.</returns>
        string Convert(decimal number, int mandatoryDecimalDigits, params string[] variants) => Convert(number);

        /// <summary>
        /// Converts a decimal value into its string representation with a mandatory number of
        /// decimal digits, custom decimal formatting options, and optional variant parameters.
        /// The default implementation ignores all parameters and delegates to
        /// <see cref="Convert(decimal)"/>; implementations should override.
        /// </summary>
        /// <param name="number">The value to convert.</param>
        /// <param name="mandatoryDecimalDigits">
        /// Negative: show the decimal part as-is. Zero: suppress the decimal part entirely.
        /// Positive: round to N decimal places and always show exactly N digits (zero-padded).
        /// </param>
        /// <param name="options">
        /// Optional overrides for the decimal separator word (<see cref="DecimalFormatOptions.DecimalSeparator"/>),
        /// the denomination suffix (<see cref="DecimalFormatOptions.DecimalSuffix"/>), and zero-decimal
        /// suppression (<see cref="DecimalFormatOptions.OmitZeroDecimals"/>).
        /// Both <c>"(s)"</c> markers are pluralized: the separator against the integer part,
        /// the suffix against the decimal value.
        /// </param>
        /// <param name="variants">Zero or more <c>"dimension=value"</c> strings.</param>
        string Convert(decimal number, int mandatoryDecimalDigits, DecimalFormatOptions? options, params string[] variants) => Convert(number);

        /// <summary>
        /// Converts a double-precision floating-point value into its string representation.
        /// </summary>
        /// <param name="number">The value to convert. Must be a finite number.</param>
        /// <returns>The formatted number.</returns>
        /// <exception cref="ArgumentException"><paramref name="number"/> is <see cref="double.NaN"/> or infinite.</exception>
        string Convert(double number) => Convert(number, []);

        /// <summary>
        /// Converts a double-precision floating-point value into its string representation.
        /// Uses the round-trip format ("R") to convert to decimal, avoiding floating-point artifacts.
        /// </summary>
        /// <param name="number">The value to convert. Must be a finite number.</param>
        /// <param name="variants">Zero or more <c>"dimension=value"</c> strings.</param>
        /// <returns>The formatted number with the requested variants applied.</returns>
        /// <exception cref="ArgumentException"><paramref name="number"/> is <see cref="double.NaN"/> or infinite.</exception>
        string Convert(double number, params string[] variants)
        {
            if (double.IsNaN(number) || double.IsInfinity(number))
                throw new ArgumentException($"Cannot convert non-finite value '{number}' to a number in words.", nameof(number));

            if (decimal.TryParse(
                    number.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out decimal d))
                return Convert(d, variants);
            // Overflow (very large doubles): fall back to integer part only
            return Convert(new BigInteger(Math.Truncate(number)), variants);
        }

        /// <summary>
        /// Converts a single-precision floating-point value into its string representation.
        /// </summary>
        /// <param name="number">The value to convert. Must be a finite number.</param>
        /// <returns>The formatted number.</returns>
        /// <exception cref="ArgumentException"><paramref name="number"/> is <see cref="float.NaN"/> or infinite.</exception>
        string Convert(float number) => Convert(number, []);

        /// <summary>
        /// Converts a single-precision floating-point value into its string representation.
        /// </summary>
        /// <param name="number">The value to convert. Must be a finite number.</param>
        /// <param name="variants">Zero or more <c>"dimension=value"</c> strings.</param>
        /// <returns>The formatted number with the requested variants applied.</returns>
        /// <exception cref="ArgumentException"><paramref name="number"/> is <see cref="float.NaN"/> or infinite.</exception>
        string Convert(float number, params string[] variants)
            => Convert((double)number, variants);

        /// <summary>
        /// Converts a rational <see cref="Number"/> into its string representation.
        /// The default implementation converts only the integer part; implementations
        /// that support rational conversion should override this.
        /// </summary>
        /// <param name="number">The rational value to convert.</param>
        /// <returns>The formatted number.</returns>
        string Convert(Number number) => Convert(number.Numerator);

        /// <summary>
        /// Converts a rational <see cref="Number"/> into its string representation,
        /// applying the specified variant parameters.
        /// The default implementation applies variants to the integer part only, delegating to
        /// <see cref="Convert(BigInteger, string[])"/>; implementations with full rational
        /// variant support should override this.
        /// </summary>
        /// <param name="number">The rational value to convert.</param>
        /// <param name="variants">
        /// Zero or more <c>"dimension=value"</c> strings. Unrecognised dimensions fall back silently.
        /// </param>
        /// <returns>The formatted number with the requested variants applied.</returns>
        string Convert(Number number, params string[] variants) => Convert(number.Numerator, variants);

        /// <summary>
        /// Converts an arbitrarily large integer into its string representation,
        /// applying the specified variant parameters (e.g. <c>"gender=feminin"</c>,
        /// <c>"case=akkusativ"</c>). When no parameter is supplied the first declared
        /// value of each dimension is used as the default.
        /// The default implementation ignores variant parameters and delegates to
        /// <see cref="Convert(BigInteger)"/>; implementations that support variants should override.
        /// </summary>
        /// <param name="number">The value to convert.</param>
        /// <param name="variants">
        /// Zero or more <c>"dimension=value"</c> strings that select morphological variants.
        /// Unrecognised dimensions or values fall back to the default silently.
        /// </param>
        /// <returns>The formatted number with the requested variants applied.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="number"/> exceeds <see cref="MaxNumber"/> (implementations that declare one).
        /// </exception>
        string Convert(BigInteger number, params string[] variants) => Convert(number);

        /// <summary>
        /// Converts <paramref name="number"/> rounded to <paramref name="significantDigits"/> most significant
        /// digits into its string representation, applying optional variant parameters.
        /// Uses standard rounding (≥ 5 rounds up, &lt; 5 rounds down). For example, 123456789
        /// with 3 significant digits rounds to 123000000 before conversion.
        /// </summary>
        /// <param name="number">The value to convert.</param>
        /// <param name="significantDigits">Number of significant digits to keep. Must be ≥ 1.</param>
        /// <param name="variants">Zero or more <c>"dimension=value"</c> strings.</param>
        /// <returns>The formatted rounded number with the requested variants applied.</returns>
        string Convert(BigInteger number, int significantDigits, params string[] variants)
            => Convert(Utils.Mathematics.MathEx.RoundToSignificantDigits(number, significantDigits), variants);

        /// <summary>
        /// Converts a 32-bit signed integer into its string representation,
        /// applying the specified variant parameters.
        /// The default implementation ignores variant parameters and delegates to
        /// <see cref="Convert(int)"/>; implementations that support variants should override.
        /// </summary>
        /// <param name="number">The value to convert.</param>
        /// <param name="variants">Zero or more <c>"dimension=value"</c> strings.</param>
        /// <returns>The formatted number with the requested variants applied.</returns>
        string Convert(int number, params string[] variants) => Convert(number);

        /// <summary>
        /// Converts a 32-bit signed integer rounded to <paramref name="significantDigits"/> most
        /// significant digits into its string representation, applying optional variant parameters.
        /// The default implementation delegates to <see cref="Convert(BigInteger, int, string[])"/>.
        /// </summary>
        /// <param name="number">The value to convert.</param>
        /// <param name="significantDigits">Number of significant digits to keep. Must be ≥ 1.</param>
        /// <param name="variants">Zero or more <c>"dimension=value"</c> strings.</param>
        string Convert(int number, int significantDigits, params string[] variants)
            => Convert((BigInteger)number, significantDigits, variants);

        /// <summary>
        /// Converts a 64-bit signed integer into its string representation,
        /// applying the specified variant parameters.
        /// The default implementation ignores variant parameters and delegates to
        /// <see cref="Convert(long)"/>; implementations that support variants should override.
        /// </summary>
        /// <param name="number">The value to convert.</param>
        /// <param name="variants">Zero or more <c>"dimension=value"</c> strings.</param>
        /// <returns>The formatted number with the requested variants applied.</returns>
        string Convert(long number, params string[] variants) => Convert(number);

        /// <summary>
        /// Converts a 64-bit signed integer rounded to <paramref name="significantDigits"/> most
        /// significant digits into its string representation, applying optional variant parameters.
        /// The default implementation delegates to <see cref="Convert(BigInteger, int, string[])"/>.
        /// </summary>
        /// <param name="number">The value to convert.</param>
        /// <param name="significantDigits">Number of significant digits to keep. Must be ≥ 1.</param>
        /// <param name="variants">Zero or more <c>"dimension=value"</c> strings.</param>
        string Convert(long number, int significantDigits, params string[] variants)
            => Convert((BigInteger)number, significantDigits, variants);

        /// <summary>
        /// Gets a value indicating whether this converter supports ordinal conversion.
        /// When <see langword="false"/>, calling <see cref="ConvertOrdinal(int)"/> will throw
        /// <see cref="NotSupportedException"/>. The default is <see langword="false"/>;
        /// implementations that support ordinals should override this.
        /// </summary>
        bool SupportsOrdinals => false;

        /// <summary>
        /// Converts a positive integer into its ordinal string representation
        /// (e.g. 1 → "first", 2 → "second" in English).
        /// The default implementation throws <see cref="NotSupportedException"/>;
        /// implementations that support ordinal conversion should override this.
        /// </summary>
        /// <param name="number">The value to convert. Negative values use the minus template.</param>
        /// <returns>The ordinal string for <paramref name="number"/>.</returns>
        /// <exception cref="NotSupportedException">The converter does not support ordinal conversion (<see cref="SupportsOrdinals"/> is <see langword="false"/>).</exception>
        string ConvertOrdinal(int number)
            => throw new NotSupportedException("Ordinal conversion is not supported by this converter.");

        /// <summary>
        /// Converts a positive integer into its ordinal string representation,
        /// applying the specified variant parameters (e.g. <c>"gender=femenino"</c>).
        /// The default implementation ignores variants and delegates to <see cref="ConvertOrdinal(int)"/>.
        /// </summary>
        /// <param name="number">The value to convert. Negative values use the minus template.</param>
        /// <param name="variants">Zero or more <c>"dimension=value"</c> strings.</param>
        /// <returns>The ordinal string for <paramref name="number"/>.</returns>
        /// <exception cref="NotSupportedException">The converter does not support ordinal conversion (<see cref="SupportsOrdinals"/> is <see langword="false"/>).</exception>
        string ConvertOrdinal(int number, params string[] variants)
            => ConvertOrdinal(number);

        /// <summary>
        /// Converts a 64-bit integer into its ordinal string representation.
        /// The default implementation delegates to <see cref="ConvertOrdinal(int, string[])"/> via a checked
        /// cast; values outside the <c>int</c> range throw <see cref="OverflowException"/>.
        /// Implementations that natively support large ordinals should override this.
        /// </summary>
        /// <param name="number">The value to convert. Negative values use the minus template.</param>
        /// <returns>The ordinal string for <paramref name="number"/>.</returns>
        /// <exception cref="OverflowException"><paramref name="number"/> is outside the <see cref="int"/> range.</exception>
        /// <exception cref="NotSupportedException">The converter does not support ordinal conversion (<see cref="SupportsOrdinals"/> is <see langword="false"/>).</exception>
        string ConvertOrdinal(long number)
            => ConvertOrdinal(number, []);

        /// <summary>
        /// Converts a 64-bit integer into its ordinal string representation,
        /// applying the specified variant parameters.
        /// The default implementation delegates to <see cref="ConvertOrdinal(int, string[])"/> via a checked
        /// cast; values outside the <c>int</c> range throw <see cref="OverflowException"/>.
        /// Implementations that natively support large ordinals should override this.
        /// </summary>
        /// <param name="number">The value to convert. Negative values use the minus template.</param>
        /// <param name="variants">Zero or more <c>"dimension=value"</c> strings.</param>
        /// <returns>The ordinal string for <paramref name="number"/>.</returns>
        /// <exception cref="OverflowException"><paramref name="number"/> is outside the <see cref="int"/> range.</exception>
        /// <exception cref="NotSupportedException">The converter does not support ordinal conversion (<see cref="SupportsOrdinals"/> is <see langword="false"/>).</exception>
        string ConvertOrdinal(long number, params string[] variants)
            => ConvertOrdinal(checked((int)number), variants);

        /// <summary>
        /// Converts an arbitrarily large integer into its ordinal string representation.
        /// The default implementation delegates to <see cref="ConvertOrdinal(long, string[])"/> via a checked
        /// cast; values outside the <c>long</c> range throw <see cref="OverflowException"/>.
        /// </summary>
        /// <param name="number">The value to convert. Negative values use the minus template.</param>
        /// <returns>The ordinal string for <paramref name="number"/>.</returns>
        /// <exception cref="OverflowException"><paramref name="number"/> is outside the <see cref="long"/> or <see cref="int"/> range.</exception>
        /// <exception cref="NotSupportedException">The converter does not support ordinal conversion (<see cref="SupportsOrdinals"/> is <see langword="false"/>).</exception>
        string ConvertOrdinal(BigInteger number)
            => ConvertOrdinal(checked((long)number), []);

        /// <summary>
        /// Converts an arbitrarily large integer into its ordinal string representation,
        /// applying the specified variant parameters.
        /// The default implementation delegates to <see cref="ConvertOrdinal(long, string[])"/> via a checked
        /// cast; values outside the <c>long</c> range throw <see cref="OverflowException"/>.
        /// </summary>
        /// <param name="number">The value to convert. Negative values use the minus template.</param>
        /// <param name="variants">Zero or more <c>"dimension=value"</c> strings.</param>
        /// <returns>The ordinal string for <paramref name="number"/>.</returns>
        /// <exception cref="OverflowException"><paramref name="number"/> is outside the <see cref="long"/> or <see cref="int"/> range.</exception>
        /// <exception cref="NotSupportedException">The converter does not support ordinal conversion (<see cref="SupportsOrdinals"/> is <see langword="false"/>).</exception>
        string ConvertOrdinal(BigInteger number, params string[] variants)
            => ConvertOrdinal(checked((long)number), variants);

        /// <summary>
        /// Converts a decimal currency amount to words using the supplied currency definition.
        /// The default implementation throws <see cref="NotSupportedException"/>;
        /// implementations that support currency conversion should override this.
        /// </summary>
        /// <param name="amount">The amount to convert.</param>
        /// <param name="currency">The currency names and configuration.</param>
        /// <returns>The amount expressed as words.</returns>
        /// <exception cref="NotSupportedException">The converter does not support currency conversion.</exception>
        string ConvertCurrency(decimal amount, CurrencyDefinition currency)
            => throw new NotSupportedException("Currency conversion is not supported by this converter.");

        /// <summary>
        /// Converts a decimal currency amount to words using the supplied currency definition,
        /// applying morphological variant parameters to the number words
        /// (e.g. <c>"gender=feminin"</c> for languages that inflect numerals by gender).
        /// The default implementation ignores variant parameters and delegates to
        /// <see cref="ConvertCurrency(decimal, CurrencyDefinition)"/>.
        /// </summary>
        /// <param name="amount">The amount to convert.</param>
        /// <param name="currency">The currency names and configuration.</param>
        /// <param name="variants">Zero or more <c>"dimension=value"</c> strings.</param>
        /// <returns>The amount expressed as words with the requested variants applied.</returns>
        /// <exception cref="NotSupportedException">The converter does not support currency conversion.</exception>
        string ConvertCurrency(decimal amount, CurrencyDefinition currency, params string[] variants)
            => ConvertCurrency(amount, currency);

        /// <summary>
        /// Converts a year number into its spoken string representation.
        /// For languages with a <c>&lt;YearFormat&gt;</c> configuration, years within declared
        /// split ranges are read as two halves (e.g. 1984 → "nineteen eighty-four" in English).
        /// For all other years or unconfigured languages, falls back to <see cref="Convert(int)"/>.
        /// </summary>
        /// <param name="year">The year to convert (negative values use the minus template).</param>
        /// <returns>The spoken form of the year.</returns>
        string ConvertYear(int year) => Convert(year);

        /// <summary>
        /// Converts a year number into its spoken string representation,
        /// applying the specified variant parameters to the number words.
        /// The default implementation ignores variants and delegates to <see cref="ConvertYear(int)"/>.
        /// </summary>
        /// <param name="year">The year to convert (negative values use the minus template).</param>
        /// <param name="variants">Zero or more <c>"dimension=value"</c> strings.</param>
        /// <returns>The spoken form of the year with the requested variants applied.</returns>
        string ConvertYear(int year, params string[] variants) => ConvertYear(year);

        /// <summary>
        /// Converts the fraction <paramref name="numerator"/>/<paramref name="denominator"/> to its
        /// string representation. The default implementation concatenates the two cardinal conversions
        /// with <c>/</c>; language-specific implementations may use named fraction forms (e.g. "un tiers").
        /// </summary>
        string ConvertFraction(BigInteger numerator, BigInteger denominator, params string[] variants)
            => $"{Convert(numerator, variants)} / {Convert(denominator, variants)}";

        /// <summary>
        /// Converts the fraction <paramref name="numerator"/>/<paramref name="denominator"/> to its
        /// string representation. Delegates to <see cref="ConvertFraction(BigInteger, BigInteger, string[])"/>.
        /// </summary>
        /// <param name="numerator">The numerator of the fraction.</param>
        /// <param name="denominator">The denominator of the fraction.</param>
        /// <param name="variants">Zero or more <c>"dimension=value"</c> strings.</param>
        /// <returns>The textual representation of the fraction.</returns>
        string ConvertFraction(int numerator, int denominator, params string[] variants)
            => ConvertFraction((BigInteger)numerator, (BigInteger)denominator, variants);

        /// <summary>
        /// Converts the fraction <paramref name="numerator"/>/<paramref name="denominator"/> to its
        /// string representation. Delegates to <see cref="ConvertFraction(BigInteger, BigInteger, string[])"/>.
        /// </summary>
        /// <param name="numerator">The numerator of the fraction.</param>
        /// <param name="denominator">The denominator of the fraction.</param>
        /// <param name="variants">Zero or more <c>"dimension=value"</c> strings.</param>
        /// <returns>The textual representation of the fraction.</returns>
        string ConvertFraction(long numerator, long denominator, params string[] variants)
            => ConvertFraction((BigInteger)numerator, (BigInteger)denominator, variants);

        /// <summary>
        /// When <see langword="true"/>, <see cref="ConvertMultiplicative"/> produces a result;
        /// when <see langword="false"/>, it throws <see cref="NotSupportedException"/>.
        /// </summary>
        bool SupportsMultiplicative => false;

        /// <summary>
        /// Converts a multiplier to its spoken multiplicative form (e.g. 2 → "twice", 3 → "trois fois").
        /// Throws <see cref="NotSupportedException"/> when the language has no multiplicative configuration.
        /// </summary>
        /// <exception cref="NotSupportedException">The converter has no <c>&lt;Multiplicatives&gt;</c> configuration (<see cref="SupportsMultiplicative"/> is <see langword="false"/>).</exception>
        string ConvertMultiplicative(int multiplier, params string[] variants)
            => throw new NotSupportedException("This converter does not support multiplicative forms.");

        /// <summary>
        /// When <see langword="true"/>, <see cref="Convert(TimeSpan, string[])"/> and
        /// <see cref="Convert(TimeOnly, string[])"/> produce a result.
        /// </summary>
        bool SupportsTimeConversion => false;

        /// <summary>
        /// When <see langword="true"/>, <see cref="Convert(DateOnly, string[])"/> and
        /// <see cref="Convert(DateTime, string[])"/> produce a result.
        /// </summary>
        bool SupportsDateConversion => false;

        /// <summary>
        /// Converts a <see cref="TimeSpan"/> duration to its spoken form
        /// (e.g. "two hours thirty minutes five seconds").
        /// Requires <c>&lt;TimeUnits&gt;</c> in the XML configuration.
        /// </summary>
        /// <exception cref="NotSupportedException">The converter has no <c>&lt;TimeUnits&gt;</c> configuration (<see cref="SupportsTimeConversion"/> is <see langword="false"/>).</exception>
        string Convert(TimeSpan duration, params string[] variants)
            => throw new NotSupportedException("Time conversion requires <TimeUnits> in the XML configuration.");

        /// <summary>
        /// Converts a <see cref="TimeOnly"/> time-of-day to its spoken form
        /// (e.g. "quatorze heures trente").
        /// Requires <c>&lt;TimeUnits&gt;</c> in the XML configuration.
        /// </summary>
        /// <exception cref="NotSupportedException">The converter has no <c>&lt;TimeUnits&gt;</c> configuration (<see cref="SupportsTimeConversion"/> is <see langword="false"/>).</exception>
        string Convert(TimeOnly time, params string[] variants)
            => throw new NotSupportedException("Time conversion requires <TimeUnits> in the XML configuration.");

        /// <summary>
        /// Converts a <see cref="DateOnly"/> date to its spoken form
        /// (e.g. "le deux juillet deux mille vingt-six").
        /// Requires <c>&lt;DateFormat&gt;</c> in the XML configuration.
        /// Month names are read from <see cref="System.Globalization.CultureInfo"/>.
        /// </summary>
        /// <exception cref="NotSupportedException">The converter has no <c>&lt;DateFormat&gt;</c> configuration (<see cref="SupportsDateConversion"/> is <see langword="false"/>).</exception>
        string Convert(DateOnly date, params string[] variants)
            => throw new NotSupportedException("Date conversion requires <DateFormat> in the XML configuration.");

        /// <summary>
        /// Converts a <see cref="DateTime"/> to its spoken form by combining
        /// <see cref="Convert(DateOnly, string[])"/> and <see cref="Convert(TimeOnly, string[])"/>.
        /// </summary>
        /// <exception cref="NotSupportedException">The converter has no <c>&lt;DateFormat&gt;</c> or <c>&lt;TimeUnits&gt;</c> configuration.</exception>
        string Convert(DateTime dateTime, params string[] variants)
            => throw new NotSupportedException("Date/time conversion requires <DateFormat> and <TimeUnits> in the XML configuration.");
    }
}
