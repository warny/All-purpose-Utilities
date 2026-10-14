using System.Numerics;

namespace Utils.NumberToString;

/// <summary>
/// The integer plural rule shared by the CLDR <c>ru</c> and <c>uk</c> locales: <c>"one"</c> for …1 except
/// …11, <c>"few"</c> for …2–4 except …12–14, <c>"many"</c> otherwise.
/// </summary>
/// <remarks>
/// Used by <see cref="RussianPluralLexicalFormSelector"/> and <see cref="UkrainianPluralLexicalFormSelector"/>.
/// </remarks>
internal static class EastSlavicIntegerPluralRule
{
    /// <summary>Returns the category of a non-negative integer.</summary>
    /// <param name="value">The absolute governing value.</param>
    /// <returns><c>"one"</c>, <c>"few"</c> or <c>"many"</c>.</returns>
    internal static string Select(BigInteger value)
    {
        int lastTwo = (int)(value % 100);
        int last = lastTwo % 10;
        if (last == 1 && lastTwo != 11) return "one";
        if (last is >= 2 and <= 4 && lastTwo is (< 12 or > 14)) return "few";
        return "many";
    }
}
