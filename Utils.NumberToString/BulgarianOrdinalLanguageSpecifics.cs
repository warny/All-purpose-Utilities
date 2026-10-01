using System.Collections.Generic;

namespace Utils.NumberToString;

/// <summary>
/// Produces Bulgarian ordinals. Only the last element of a compound is ordinal and, as in the
/// cardinal, the conjunction <c>и</c> precedes that last element (<c>двадесет и първи</c>,
/// <c>сто и първи</c>, <c>хиляда сто и втори</c>). Ordinals agree in gender: the masculine
/// citation form (<c>първи</c>, <c>стотен</c>) is used for the default <c>standalone</c> value and
/// for <c>masculine</c>; <c>feminine</c> and <c>neuter</c> derive <c>-а</c>/<c>-о</c>
/// (<c>първа</c>, <c>първо</c>) and <c>-на</c>/<c>-но</c> (<c>стотна</c>, <c>стотно</c>).
/// </summary>
/// <remarks>
/// The cardinal of the non-ordinal prefix is rebuilt here because <see cref="IOrdinalLanguageSpecifics"/>
/// receives no converter. Only the <see cref="int"/> range is implemented. Round multiples of a
/// thousand use the single-word compound (<c>двехиляден</c>) only when the multiplier is a single
/// word; multi-word multipliers of a round thousand, and round multiples of a million or a milliard
/// other than one, are declined (and therefore fail closed) because their compound adjective forms
/// were not verified.
/// </remarks>
public sealed class BulgarianOrdinalLanguageSpecifics : INumberToStringLanguageSpecifics, IOrdinalLanguageSpecifics
{
    /// <summary>Grammatical gender of the requested ordinal.</summary>
    private enum Gender { Masculine, Feminine, Neuter }

    private static readonly string[] s_unitsMasculine = ["", "един", "два", "три", "четири", "пет", "шест", "седем", "осем", "девет"];
    private static readonly string[] s_unitsFeminine = ["", "една", "две", "три", "четири", "пет", "шест", "седем", "осем", "девет"];
    private static readonly string[] s_teens =
        ["десет", "единадесет", "дванадесет", "тринадесет", "четиринадесет", "петнадесет", "шестнадесет", "седемнадесет", "осемнадесет", "деветнадесет"];
    private static readonly string[] s_tens = ["", "", "двадесет", "тридесет", "четиридесет", "петдесет", "шестдесет", "седемдесет", "осемдесет", "деветдесет"];
    private static readonly string[] s_hundreds = ["", "сто", "двеста", "триста", "четиристотин", "петстотин", "шестстотин", "седемстотин", "осемстотин", "деветстотин"];

    private static readonly string[] s_ordinalUnits = ["", "първи", "втори", "трети", "четвърти", "пети", "шести", "седми", "осми", "девети"];
    private static readonly string[] s_ordinalTeens =
        ["десети", "единадесети", "дванадесети", "тринадесети", "четиринадесети", "петнадесети", "шестнадесети", "седемнадесети", "осемнадесети", "деветнадесети"];
    private static readonly string[] s_ordinalTens = ["", "", "двадесети", "тридесети", "четиридесети", "петдесети", "шестдесети", "седемдесети", "осемдесети", "деветдесети"];
    private static readonly string[] s_ordinalHundreds = ["", "стотен", "двестотен", "тристотен", "четиристотен", "петстотен", "шестстотен", "седемстотен", "осемстотен", "деветстотен"];

    /// <inheritdoc />
    public string FinalizeWriting(string languageIdentifier, string text) => text;

    /// <inheritdoc />
    public bool TryConvertOrdinal(int number, IReadOnlyDictionary<string, string> activeVariants, out string? result)
    {
        result = null;
        if (number <= 0)
            return false;

        string? masculine = BuildMasculineOrdinal(number);
        if (masculine == null)
            return false;

        Gender gender = activeVariants.TryGetValue("gender", out string? value) ? value switch
        {
            "feminine" => Gender.Feminine,
            "neuter" => Gender.Neuter,
            _ => Gender.Masculine,
        } : Gender.Masculine;
        result = Inflect(masculine, gender);
        return true;
    }

    /// <summary>Builds the masculine ordinal of a positive number, or <see langword="null"/> when declined.</summary>
    /// <param name="number">A positive integer.</param>
    /// <returns>The masculine ordinal or <see langword="null"/>.</returns>
    private static string? BuildMasculineOrdinal(int number)
    {
        int units = number % 1000;
        int thousands = number / 1000 % 1000;
        int millions = number / 1_000_000 % 1000;
        int milliards = number / 1_000_000_000;

        var prefix = new List<string>();
        if (milliards > 0)
            prefix.Add(milliards == 1 ? "един милиард" : JoinElements(GroupElements(milliards, s_unitsMasculine)) + " милиарда");

        List<string> last;
        if (units > 0)
        {
            if (millions > 0)
                prefix.Add(MillionChunk(millions));
            if (thousands > 0)
                prefix.Add(ThousandChunk(thousands));
            last = GroupElements(units, s_unitsMasculine);
            last[^1] = OrdinalOfGroupEnd(units);
        }
        else if (thousands > 0)
        {
            if (millions > 0)
                prefix.Add(MillionChunk(millions));
            string? ordinal = RoundScaleOrdinal(thousands, "хиляден", s_unitsFeminine);
            if (ordinal == null)
                return null;
            last = [ordinal];
        }
        else if (millions > 0)
        {
            if (millions != 1)
                return null;
            last = ["милионен"];
        }
        else
        {
            if (milliards != 1)
                return null;
            return "милиарден";
        }

        string lastText = JoinElements(last);
        if (prefix.Count == 0)
            return lastText;
        string head = string.Join(" ", prefix);
        return last.Count == 1 ? head + " и " + lastText : head + " " + lastText;
    }

    /// <summary>Builds the cardinal chunk of a thousands group.</summary>
    /// <param name="thousands">The thousands multiplier (1–999).</param>
    /// <returns>The chunk text (<c>хиляда</c>, <c>две хиляди</c>).</returns>
    private static string ThousandChunk(int thousands)
        => thousands == 1 ? "хиляда" : JoinElements(GroupElements(thousands, s_unitsFeminine)) + " хиляди";

    /// <summary>Builds the cardinal chunk of a millions group.</summary>
    /// <param name="millions">The millions multiplier (1–999).</param>
    /// <returns>The chunk text (<c>един милион</c>, <c>два милиона</c>).</returns>
    private static string MillionChunk(int millions)
        => millions == 1 ? "един милион" : JoinElements(GroupElements(millions, s_unitsMasculine)) + " милиона";

    /// <summary>Builds the ordinal of a round scale, fusing a single-word multiplier (<c>двехиляден</c>).</summary>
    /// <param name="multiplier">The scale multiplier.</param>
    /// <param name="ordinal">The ordinal of the bare scale word.</param>
    /// <param name="units">The unit forms agreeing with the scale noun.</param>
    /// <returns>The ordinal, or <see langword="null"/> for an unverified multi-word multiplier.</returns>
    private static string? RoundScaleOrdinal(int multiplier, string ordinal, string[] units)
    {
        if (multiplier == 1)
            return ordinal;
        List<string> elements = GroupElements(multiplier, units);
        return elements.Count == 1 ? elements[0] + ordinal : null;
    }

    /// <summary>Gets the ordinal of the last element of a group between 1 and 999.</summary>
    /// <param name="group">A number between 1 and 999.</param>
    /// <returns>The masculine ordinal of the group's last element.</returns>
    private static string OrdinalOfGroupEnd(int group)
    {
        int remainder = group % 100;
        if (remainder == 0)
            return s_ordinalHundreds[group / 100];
        if (remainder < 10)
            return s_ordinalUnits[remainder];
        if (remainder < 20)
            return s_ordinalTeens[remainder - 10];
        return remainder % 10 == 0 ? s_ordinalTens[remainder / 10] : s_ordinalUnits[remainder % 10];
    }

    /// <summary>Splits a group between 1 and 999 into its spoken elements.</summary>
    /// <param name="group">A number between 1 and 999.</param>
    /// <param name="units">The unit forms agreeing with the counted noun.</param>
    /// <returns>The hundreds, tens, and units elements in order.</returns>
    private static List<string> GroupElements(int group, string[] units)
    {
        var elements = new List<string>();
        int hundreds = group / 100;
        int remainder = group % 100;
        if (hundreds > 0)
            elements.Add(s_hundreds[hundreds]);
        if (remainder >= 20)
        {
            elements.Add(s_tens[remainder / 10]);
            if (remainder % 10 > 0)
                elements.Add(units[remainder % 10]);
        }
        else if (remainder >= 10)
            elements.Add(s_teens[remainder - 10]);
        else if (remainder > 0)
            elements.Add(units[remainder]);
        return elements;
    }

    /// <summary>Joins spoken elements, placing <c>и</c> before the last one.</summary>
    /// <param name="elements">The elements in order.</param>
    /// <returns>The joined text.</returns>
    private static string JoinElements(List<string> elements)
        => elements.Count < 2
            ? string.Concat(elements)
            : string.Join(" ", elements.GetRange(0, elements.Count - 1)) + " и " + elements[^1];

    /// <summary>Inflects a masculine ordinal for gender.</summary>
    /// <param name="masculine">The masculine ordinal.</param>
    /// <param name="gender">The requested gender.</param>
    /// <returns>The inflected ordinal.</returns>
    private static string Inflect(string masculine, Gender gender)
    {
        if (gender == Gender.Masculine)
            return masculine;
        if (masculine.EndsWith("ен"))
            return masculine[..^2] + (gender == Gender.Feminine ? "на" : "но");
        return masculine[..^1] + (gender == Gender.Feminine ? "а" : "о");
    }
}
