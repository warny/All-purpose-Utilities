using System.Numerics;
using Utils.Numerics;

namespace Utils.Mathematics
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
        /// </summary>
        IReadOnlyList<NumberToStringConverter.VariantDimension> VariantDimensions { get; }

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
        /// Converts a rational <see cref="Number"/> into its string representation.
        /// </summary>
        /// <param name="number">The rational value to convert.</param>
        /// <returns>The formatted number.</returns>
        string Convert(Number number);

        /// <summary>
        /// Converts an arbitrarily large integer into its string representation,
        /// applying the specified variant parameters (e.g. <c>"gender=feminin"</c>,
        /// <c>"case=akkusativ"</c>). When no parameter is supplied the first declared
        /// value of each dimension is used as the default.
        /// </summary>
        /// <param name="number">The value to convert.</param>
        /// <param name="variants">
        /// Zero or more <c>"dimension=value"</c> strings that select morphological variants.
        /// Unrecognised dimensions or values fall back to the default silently.
        /// </param>
        /// <returns>The formatted number with the requested variants applied.</returns>
        string Convert(BigInteger number, params string[] variants);

        /// <summary>
        /// Converts a 32-bit signed integer into its string representation,
        /// applying the specified variant parameters.
        /// </summary>
        /// <param name="number">The value to convert.</param>
        /// <param name="variants">Zero or more <c>"dimension=value"</c> strings.</param>
        /// <returns>The formatted number with the requested variants applied.</returns>
        string Convert(int number, params string[] variants);

        /// <summary>
        /// Converts a 64-bit signed integer into its string representation,
        /// applying the specified variant parameters.
        /// </summary>
        /// <param name="number">The value to convert.</param>
        /// <param name="variants">Zero or more <c>"dimension=value"</c> strings.</param>
        /// <returns>The formatted number with the requested variants applied.</returns>
        string Convert(long number, params string[] variants);

        /// <summary>
        /// Converts a positive integer into its ordinal string representation
        /// (e.g. 1 → "first", 2 → "second" in English).
        /// </summary>
        /// <param name="number">The value to convert. Negative values use the minus template.</param>
        /// <returns>The ordinal string for <paramref name="number"/>.</returns>
        string ConvertOrdinal(int number);
    }
}
