using System.Collections.Generic;
using System.Numerics;

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
    }
}