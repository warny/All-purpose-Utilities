using System.Collections.Generic;
using System.Numerics;

namespace Utils.NumberToString;

/// <summary>
/// The oblique cases of Ukrainian fractions (NTS-40). Український правопис 2019, §107.1: a fraction "is declined as an
/// ordinary numeral" (<i>двом третім</i>, <i>трьома п'ятими</i>; <i>ділити на одну двадцяту</i>): the numerator as a
/// cardinal, the denominator as an ordinal. §105.6: every part of a compound cardinal is declined (<i>з чотирьохсот
/// сімдесяти восьми тисяч шестисот п'ятдесяти дев'яти</i>); §105.11: <c>тисяча</c>, <c>мільйон</c>, <c>мільярд</c>
/// decline as nouns; §106.2: only the last part of a compound ordinal is declined (<i>вісімдесят восьмого</i>).
/// </summary>
public sealed partial class UkrainianOrdinalLanguageSpecifics
{
    // Every table below is in FractionCase order: nominative, genitive, dative, accusative, locative, instrumental.
    // The forms are the first ones of the Правопис 2019 §105 tables (п'яти before п'ятьом, п'ятьма before п'ятьома).

    /// <summary>The feminine <c>одна</c> (§105.1).</summary>
    private static readonly string[] s_oneFeminineCases = ["одна", "однієї", "одній", "одну", "одній", "однією"];

    /// <summary>The masculine <c>один</c> (§105.1), the multiplier of <c>мільйон</c> and <c>мільярд</c>.</summary>
    private static readonly string[] s_oneMasculineCases = ["один", "одного", "одному", "один", "одному", "одним"];

    /// <summary>The cases of 2–9 (index = value; the nominative and accusative of 2 are the masculine <c>два</c>), §105.2.</summary>
    private static readonly string[][] s_unitCases =
    [
        [],
        [],
        ["два", "двох", "двом", "два", "двох", "двома"],
        ["три", "трьох", "трьом", "три", "трьох", "трьома"],
        ["чотири", "чотирьох", "чотирьом", "чотири", "чотирьох", "чотирма"],
        ["п'ять", "п'яти", "п'яти", "п'ять", "п'яти", "п'ятьма"],
        ["шість", "шести", "шести", "шість", "шести", "шістьма"],
        ["сім", "семи", "семи", "сім", "семи", "сьома"],
        ["вісім", "восьми", "восьми", "вісім", "восьми", "вісьма"],
        ["дев'ять", "дев'яти", "дев'яти", "дев'ять", "дев'яти", "дев'ятьма"],
    ];

    /// <summary>
    /// The genitive of the hundreds 200–900 (index = hundreds digit), §105.5: <c>двохсот</c>, <c>п'ятисот</c>; the
    /// other hundreds follow these patterns (VESUM: <c>шестисот</c>, <c>семисот</c>, <c>восьмисот</c>, <c>дев'ятисот</c>).
    /// </summary>
    private static readonly string[] s_hundredsGenitive =
        ["", "", "двохсот", "трьохсот", "чотирьохсот", "п'ятисот", "шестисот", "семисот", "восьмисот", "дев'ятисот"];

    /// <summary>The dative of the hundreds 200–900 (§105.5: <c>двомстам</c>, <c>п'ятистам</c>).</summary>
    private static readonly string[] s_hundredsDative =
        ["", "", "двомстам", "трьомстам", "чотирьомстам", "п'ятистам", "шестистам", "семистам", "восьмистам", "дев'ятистам"];

    /// <summary>The locative of the hundreds 200–900 (§105.5: <c>на двохстах</c>, <c>на п'ятистах</c>).</summary>
    private static readonly string[] s_hundredsLocative =
        ["", "", "двохстах", "трьохстах", "чотирьохстах", "п'ятистах", "шестистах", "семистах", "восьмистах", "дев'ятистах"];

    /// <summary>
    /// The instrumental of the hundreds 200–900: the instrumental of the multiplier and <c>-стами</c> (§105.5:
    /// <c>двомастами</c>, <c>п'ятьмастами</c>; VESUM: <c>трьомастами</c>, <c>чотирмастами</c>, <c>шістьмастами</c>,
    /// <c>сьомастами</c>, <c>вісьмастами</c>, <c>дев'ятьмастами</c>).
    /// </summary>
    private static readonly string[] s_hundredsInstrumental =
        ["", "", "двомастами", "трьомастами", "чотирмастами", "п'ятьмастами", "шістьмастами", "сьомастами", "вісьмастами", "дев'ятьмастами"];

    /// <summary>The singular of <c>тисяча</c> (a feminine noun, §105.11; VESUM).</summary>
    private static readonly string[] s_thousandSingular = ["тисяча", "тисячі", "тисячі", "тисячу", "тисячі", "тисячею"];

    /// <summary>The plural of <c>тисяча</c>.</summary>
    private static readonly string[] s_thousandPlural = ["тисячі", "тисяч", "тисячам", "тисячі", "тисячах", "тисячами"];

    /// <summary>The singular of <c>мільйон</c> (a masculine noun, §105.11; VESUM, first forms).</summary>
    private static readonly string[] s_millionSingular = ["мільйон", "мільйона", "мільйонові", "мільйон", "мільйоні", "мільйоном"];

    /// <summary>The plural of <c>мільйон</c>.</summary>
    private static readonly string[] s_millionPlural = ["мільйони", "мільйонів", "мільйонам", "мільйони", "мільйонах", "мільйонами"];

    /// <summary>The singular of <c>мільярд</c> (a masculine noun, §105.11; VESUM, first forms).</summary>
    private static readonly string[] s_milliardSingular = ["мільярд", "мільярда", "мільярдові", "мільярд", "мільярді", "мільярдом"];

    /// <summary>The plural of <c>мільярд</c>.</summary>
    private static readonly string[] s_milliardPlural = ["мільярди", "мільярдів", "мільярдам", "мільярди", "мільярдах", "мільярдами"];

    /// <summary>The ordinal case columns (nominative, genitive, dative, accusative, instrumental, locative) by fraction case.</summary>
    private static readonly int[] s_ordinalColumn = [0, 1, 2, 3, 5, 4];

    /// <summary>The Ukrainian case order: the instrumental before the locative.</summary>
    private static readonly FractionCase[] s_ukrainianCaseOrder =
        [FractionCase.Nominative, FractionCase.Genitive, FractionCase.Dative, FractionCase.Accusative, FractionCase.Instrumental, FractionCase.Locative];

    /// <inheritdoc />
    private protected override IReadOnlyList<FractionCase> FractionCaseOrder => s_ukrainianCaseOrder;

    /// <summary>The largest numerator of an oblique fraction: the milliards end the scales this class declines.</summary>
    private const long MaxDeclinedNumerator = 999_999_999_999;

    /// <summary>
    /// Builds the reading of a fraction in an oblique case (or the accusative): the numerator declined as a feminine
    /// cardinal (<see cref="DeclineCardinal"/>), the denominator ordinal declined after it. After a numerator that
    /// reads as one (one, or a compound ending in <c>одна</c>, as in the nominative) the denominator is the feminine
    /// singular in the same case (<i>ділити на одну двадцяту</i>; <i>двадцяти одній сотій</i>); after a numerator
    /// ending in a scale noun (<c>тисяча</c>, <c>мільйон</c>, <c>мільярд</c>), which governs the genitive as a noun
    /// (§105.11), the genitive plural; after any other numerator the plural in the same case (<i>двом третім</i>,
    /// <i>трьома п'ятими</i>), the accusative of an inanimate quantity being its nominative (<i>дві третіх</i>).
    /// </summary>
    /// <param name="numerator">The numerator, at least one.</param>
    /// <param name="prefix">The undeclined words of the compound ordinal denominator (§106.2).</param>
    /// <param name="last">The last ordinal component of the denominator, masculine nominative.</param>
    /// <param name="fractionCase">The requested case, not the nominative.</param>
    /// <param name="reading">Receives the reading.</param>
    /// <returns><see langword="false"/> when the numerator is beyond the milliards.</returns>
    private bool TryBuildObliqueFraction(BigInteger numerator, string prefix, string last, FractionCase fractionCase,
        out FractionDenominatorReading? reading)
    {
        reading = null;
        if (numerator > MaxDeclinedNumerator)
            return false;
        long value = (long)numerator;
        string word;
        if (ClassifyFractionNumerator(numerator, compoundEndingInOneIsSingular: true) == FractionNumeratorClass.One)
            word = Decline(last, Gender.Feminine, s_ordinalColumn[(int)fractionCase]);
        else
            word = PluralOrdinal(last, value % 1000 == 0 ? FractionCase.Genitive : fractionCase);
        reading = FractionDenominatorReading.WithNumerator(DeclineCardinal(value, fractionCase), prefix.Length == 0 ? word : prefix + " " + word);
        return true;
    }

    /// <summary>
    /// Declines the plural of an ordinal (VESUM: <c>третіх</c>, <c>третім</c>, <c>третіми</c>; <c>п'ятих</c>,
    /// <c>п'ятим</c>, <c>п'ятими</c>); the nominative and accusative after a quantity are the genitive (§107.1:
    /// <i>дві третіх</i>, <i>три п'ятих</i>).
    /// </summary>
    /// <param name="citation">The masculine nominative singular.</param>
    /// <param name="fractionCase">The case.</param>
    /// <returns>The plural form.</returns>
    private static string PluralOrdinal(string citation, FractionCase fractionCase)
    {
        string ending = fractionCase switch
        {
            FractionCase.Dative => "м",
            FractionCase.Instrumental => "ми",
            _ => "х",
        };
        return citation.EndsWith("ій") ? citation[..^2] + "і" + ending : citation[..^2] + "и" + ending;
    }

    /// <summary>
    /// Declines a cardinal between 1 and 999 999 999 999 for a case, every part declined (§105.6), the units feminine as
    /// a fraction numerator (<c>одна</c>, <c>дві</c>) and the groups spelled as the UK configuration spells them
    /// (<c>тисяча</c> alone for one thousand, <c>один мільйон</c>, <c>один мільярд</c>). The nominative is the cardinal
    /// of the configuration with the feminine units.
    /// </summary>
    /// <param name="number">The number.</param>
    /// <param name="fractionCase">The case.</param>
    /// <returns>The declined cardinal.</returns>
    private static string DeclineCardinal(long number, FractionCase fractionCase)
    {
        var words = new List<string>();
        AppendDeclinedScale(words, (int)(number / 1_000_000_000 % 1000), fractionCase, s_milliardSingular, s_milliardPlural, feminine: false, bareOne: false);
        AppendDeclinedScale(words, (int)(number / 1_000_000 % 1000), fractionCase, s_millionSingular, s_millionPlural, feminine: false, bareOne: false);
        AppendDeclinedScale(words, (int)(number / 1000 % 1000), fractionCase, s_thousandSingular, s_thousandPlural, feminine: true, bareOne: true);
        AppendDeclinedGroup(words, (int)(number % 1000), fractionCase, feminine: true);
        return string.Join(" ", words);
    }

    /// <summary>
    /// Appends a declined scale group: the multiplier and the scale noun, which after a multiplier ending in one (not
    /// eleven) is singular, and otherwise is the nominative plural after two to four and the genitive plural after the
    /// other multipliers in the nominative and accusative (§105.11, as <see cref="AppendCardinalChunk"/>), and the
    /// plural of the requested case in the oblique cases (<i>чотирьохсот сімдесяти восьми тисяч</i>).
    /// </summary>
    /// <param name="words">The target word list.</param>
    /// <param name="group">The multiplier (0–999; 0 appends nothing).</param>
    /// <param name="fractionCase">The case.</param>
    /// <param name="singular">The singular of the scale noun by case.</param>
    /// <param name="plural">The plural of the scale noun by case.</param>
    /// <param name="feminine">Whether the scale noun is feminine (<c>тисяча</c>: <c>одна</c>, <c>дві</c>).</param>
    /// <param name="bareOne">Whether a multiplier of one is left out (<c>тисяча</c>, not <c>одна тисяча</c>).</param>
    private static void AppendDeclinedScale(List<string> words, int group, FractionCase fractionCase, string[] singular, string[] plural,
        bool feminine, bool bareOne)
    {
        if (group == 0)
            return;
        if (group != 1 || !bareOne)
            AppendDeclinedGroup(words, group, fractionCase, feminine);
        int lastTwo = group % 100;
        bool teen = lastTwo is >= 10 and < 20;
        int lastDigit = lastTwo % 10;
        if (!teen && lastDigit == 1)
            words.Add(singular[(int)fractionCase]);
        else if (fractionCase is not (FractionCase.Nominative or FractionCase.Accusative))
            words.Add(plural[(int)fractionCase]);
        else
            words.Add(!teen && lastDigit is >= 2 and <= 4 ? plural[(int)FractionCase.Nominative] : plural[(int)FractionCase.Genitive]);
    }

    /// <summary>
    /// Appends a group between 1 and 999 declined for a case: the hundreds (<c>сто</c> → <c>ста</c>, §105.7; 200–900,
    /// §105.5), the tens (<c>сорок</c> → <c>сорока</c>, <c>дев'яносто</c> → <c>дев'яноста</c>, §105.7; the others as
    /// <c>п'ять</c> and <c>п'ятдесят</c>, §105.3–4) and the units (§105.1–2).
    /// </summary>
    /// <param name="words">The target word list.</param>
    /// <param name="group">The group (0 appends nothing).</param>
    /// <param name="fractionCase">The case.</param>
    /// <param name="feminine">Whether one and two take their feminine forms.</param>
    private static void AppendDeclinedGroup(List<string> words, int group, FractionCase fractionCase, bool feminine)
    {
        int column = (int)fractionCase;
        bool direct = fractionCase is FractionCase.Nominative or FractionCase.Accusative;
        int hundreds = group / 100;
        int remainder = group % 100;
        if (hundreds == 1)
            words.Add(direct ? "сто" : "ста");
        else if (hundreds > 1)
        {
            words.Add(fractionCase switch
            {
                FractionCase.Genitive => s_hundredsGenitive[hundreds],
                FractionCase.Dative => s_hundredsDative[hundreds],
                FractionCase.Locative => s_hundredsLocative[hundreds],
                FractionCase.Instrumental => s_hundredsInstrumental[hundreds],
                _ => s_cardinalHundreds[hundreds],
            });
        }
        if (remainder >= 10 && remainder < 20)
        {
            words.Add(DeclineLikeFive(s_cardinalTeens[remainder - 10], fractionCase));
            return;
        }
        if (remainder >= 20)
        {
            int tens = remainder / 10;
            words.Add(tens switch
            {
                4 => direct ? "сорок" : "сорока",
                9 => direct ? "дев'яносто" : "дев'яноста",
                _ => DeclineLikeFive(s_cardinalTens[tens], fractionCase),
            });
        }
        int unit = remainder % 10;
        if (unit == 0)
            return;
        if (unit == 1)
            words.Add((feminine ? s_oneFeminineCases : s_oneMasculineCases)[column]);
        else if (unit == 2 && feminine && direct)
            words.Add("дві");
        else
            words.Add(s_unitCases[unit][column]);
    }

    /// <summary>
    /// Declines a numeral of the <c>п'ять</c> pattern ending in <c>-ть</c> or <c>-т</c> (10–19, 20, 30, 50–80): the
    /// genitive, dative and locative in <c>-и</c>, the instrumental in <c>-ьма</c> (§105.3–4: <c>десяти</c>,
    /// <c>двадцятьма</c>, <c>п'ятдесяти</c>, <c>п'ятдесятьма</c>).
    /// </summary>
    /// <param name="nominative">The nominative.</param>
    /// <param name="fractionCase">The case.</param>
    /// <returns>The declined form.</returns>
    private static string DeclineLikeFive(string nominative, FractionCase fractionCase)
    {
        string stem = nominative.EndsWith('ь') ? nominative[..^1] : nominative;
        return fractionCase switch
        {
            FractionCase.Nominative or FractionCase.Accusative => nominative,
            FractionCase.Instrumental => stem + "ьма",
            _ => stem + "и",
        };
    }
}
