using System.Globalization;
using Utils.Range;

namespace Utils.NumberToString;

/// <summary>
/// Range checks shared by the declarative connector rules (<see cref="CurrencyNounConnectorRule"/>,
/// <see cref="ScaleConnectorRule"/>): emptiness and negative coverage, decided without enumerating a range whose bound
/// may be open.
/// </summary>
internal static class ConnectorRangeChecks
{
    /// <summary>The values below one (<c>..0</c>).</summary>
    internal static readonly IntRange<long> BelowOne = NumberToStringConverter.ParseRangeExpression("..0");

    /// <summary>
    /// Determines whether <paramref name="range"/> covers no value, without enumerating it (an open bound cannot be
    /// enumerated): an empty range has no interval in its canonical form.
    /// </summary>
    /// <param name="range">The range to test.</param>
    /// <returns><see langword="true"/> when the range is empty.</returns>
    internal static bool IsEmpty(IntRange<long> range) => string.IsNullOrEmpty(range.ToString(null, CultureInfo.InvariantCulture));

    /// <summary>
    /// Determines whether <paramref name="range"/> covers a value below one.
    /// </summary>
    /// <param name="range">The range to test.</param>
    /// <returns><see langword="true"/> when a value below one is covered.</returns>
    internal static bool CoversBelowOne(IntRange<long> range) => !IsEmpty(range & BelowOne);

    /// <summary>
    /// Determines whether <paramref name="range"/> covers a negative value: its part at or below zero is more than zero
    /// itself. The intersection with <c>..0</c> is used rather than a difference or a complement, which do not handle
    /// an open lower bound.
    /// </summary>
    /// <param name="range">The range to test.</param>
    /// <returns><see langword="true"/> when a negative value is covered.</returns>
    internal static bool CoversNegative(IntRange<long> range)
    {
        string nonPositive = (range & BelowOne).ToString(null, CultureInfo.InvariantCulture) ?? "";
        return nonPositive.Length > 0 && nonPositive != "0";
    }

    /// <summary>
    /// Determines whether <paramref name="range"/> covers no residue modulo <paramref name="modulo"/>, i.e. no value in
    /// <c>0..modulo-1</c>.
    /// </summary>
    /// <param name="range">The range to test.</param>
    /// <param name="modulo">The modulus (at least 2).</param>
    /// <returns><see langword="true"/> when the range covers no residue.</returns>
    internal static bool CoversNoResidue(IntRange<long> range, long modulo)
    {
        var residues = NumberToStringConverter.ParseRangeExpression($"0..{(modulo - 1).ToString(CultureInfo.InvariantCulture)}");
        return IsEmpty(range & residues);
    }
}
