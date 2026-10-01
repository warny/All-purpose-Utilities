using System;
using System.Collections.Generic;

namespace Utils.NumberToString;

/// <summary>
/// Produces Arabic ordinals above nineteen, in the same indefinite short nominative form without
/// the article as the configured ordinals 1–19. From 21 the unit is an ordinal agreeing in gender
/// and the tens stay cardinal, joined by <c>و</c> (<c>حادٍ وعشرون</c>, feminine
/// <c>حادية وعشرون</c>); round tens, one hundred, and one thousand use the cardinal word, which is
/// the ordinal form in Modern Standard Arabic (<c>العشرون</c>, <c>المائة</c>, <c>الألف</c> with the
/// article).
/// </summary>
/// <remarks>
/// Values below twenty are declined so the XML exceptions (<c>أول</c>, <c>حادي عشر</c>) apply.
/// Other values above 99 have no verified indefinite form in this contract and throw
/// <see cref="NotSupportedException"/>, rather than falling back to an unchanged cardinal.
/// </remarks>
public sealed class ArabicOrdinalLanguageSpecifics : INumberToStringLanguageSpecifics, IOrdinalLanguageSpecifics
{
    private static readonly string[] s_unitsMasculine = ["", "حادٍ", "ثانٍ", "ثالث", "رابع", "خامس", "سادس", "سابع", "ثامن", "تاسع"];
    private static readonly string[] s_unitsFeminine = ["", "حادية", "ثانية", "ثالثة", "رابعة", "خامسة", "سادسة", "سابعة", "ثامنة", "تاسعة"];
    private static readonly string[] s_tens = ["", "", "عشرون", "ثلاثون", "أربعون", "خمسون", "ستون", "سبعون", "ثمانون", "تسعون"];

    /// <inheritdoc />
    public string FinalizeWriting(string languageIdentifier, string text) => text;

    /// <inheritdoc />
    public bool TryConvertOrdinal(int number, IReadOnlyDictionary<string, string> activeVariants, out string? result)
    {
        result = null;
        if (number < 20)
            return false;
        if (number == 100)
            result = "مائة";
        else if (number == 1000)
            result = "ألف";
        else if (number < 100)
        {
            bool feminine = activeVariants.TryGetValue("gender", out string? gender) && gender == "muʾannath";
            int unit = number % 10;
            string tens = s_tens[number / 10];
            result = unit == 0 ? tens : (feminine ? s_unitsFeminine : s_unitsMasculine)[unit] + " و" + tens;
        }
        else
            throw new NotSupportedException($"Arabic ordinal {number} has no verified indefinite form; only 1-99, 100 and 1000 are supported.");
        return true;
    }

    /// <inheritdoc />
    public bool TryConvertOrdinal(long number, IReadOnlyDictionary<string, string> activeVariants, out string? result)
    {
        if (number > int.MaxValue)
            throw new NotSupportedException($"Arabic ordinal {number} has no verified indefinite form; only 1-99, 100 and 1000 are supported.");
        return TryConvertOrdinal((int)number, activeVariants, out result);
    }
}
