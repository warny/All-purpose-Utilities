using System.Collections.Generic;
using System.Numerics;

namespace Utils.NumberToString;

/// <summary>
/// The oblique cases of Ukrainian fractions (NTS-40). Український правопис 2019, §107.1: a fraction "is declined as an
/// ordinary numeral" (<i>двом третім</i>, <i>трьома п'ятими</i>; <i>ділити на одну двадцяту</i>): the numerator as a
/// cardinal, the denominator as an ordinal. §105.6: every part of a compound cardinal is declined (<i>з чотирьохсот
/// сімдесяти восьми тисяч шестисот п'ятдесяти дев'яти</i>); §105.11: <c>тисяча</c>, <c>мільйон</c>, <c>мільярд</c>
/// decline as nouns; §106.2: only the last part of a compound ordinal is declined (<i>вісімдесят восьмого</i>). NTS-42 E
/// adds the zero numerator (<see cref="s_zeroCases"/>), NTS-42 F the generated scale nouns from 10^12, read from the
/// converter (<see cref="GetScaleNoun"/>).
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

    /// <summary>
    /// The singular of <c>нуль</c>, a masculine noun of the second declension, soft group (NTS-42 E). It is not in the
    /// Правопис 2019 §105 tables; it declines as a noun (Курс лекцій «Лінгвістичні основи документознавства», ПНТУ,
    /// Полтава 2015, p. 132: "нуль, тисяча, мільйон, мільярд, трильйон, квадрильйон … відмінюються як іменники
    /// відповідної відміни і групи"). The forms are the first ones of VESUM, as for <c>мільйон</c>: dative <c>нулеві</c>
    /// (VESUM <c>нулеві, нулю</c>), locative <c>нулю</c> (VESUM <c>нулю, нулі, нулеві</c>); the ПНТУ paradigm of
    /// <i>нуль цілих сім десятих</i> (p. 135) has both datives, <i>нулю (нулеві)</i>, and <i>на нулю (нулеві)</i>.
    /// </summary>
    private static readonly string[] s_zeroCases = ["нуль", "нуля", "нулеві", "нуль", "нулю", "нулем"];

    /// <summary><c>тисяча</c>, a feminine noun (§105.11; VESUM): <c>одна тисяча</c>, <c>дві тисячі</c>.</summary>
    private static readonly ScaleNoun s_thousand = new(
        ["тисяча", "тисячі", "тисячі", "тисячу", "тисячі", "тисячею"],
        ["тисячі", "тисяч", "тисячам", "тисячі", "тисячах", "тисячами"],
        Feminine: true);

    /// <summary><c>мільйон</c>, a masculine noun (§105.11; VESUM, first forms).</summary>
    private static readonly ScaleNoun s_million = new(
        ["мільйон", "мільйона", "мільйонові", "мільйон", "мільйоні", "мільйоном"],
        ["мільйони", "мільйонів", "мільйонам", "мільйони", "мільйонах", "мільйонами"],
        Feminine: false);

    /// <summary><c>мільярд</c>, a masculine noun (§105.11; VESUM, first forms).</summary>
    private static readonly ScaleNoun s_milliard = new(
        ["мільярд", "мільярда", "мільярдові", "мільярд", "мільярді", "мільярдом"],
        ["мільярди", "мільярдів", "мільярдам", "мільярди", "мільярдах", "мільярдами"],
        Feminine: false);

    /// <summary>The ordinal case columns (nominative, genitive, dative, accusative, instrumental, locative) by fraction case.</summary>
    private static readonly int[] s_ordinalColumn = [0, 1, 2, 3, 5, 4];

    /// <summary>The Ukrainian case order: the instrumental before the locative.</summary>
    private static readonly FractionCase[] s_ukrainianCaseOrder =
        [FractionCase.Nominative, FractionCase.Genitive, FractionCase.Dative, FractionCase.Accusative, FractionCase.Instrumental, FractionCase.Locative];

    /// <inheritdoc />
    private protected override IReadOnlyList<FractionCase> FractionCaseOrder => s_ukrainianCaseOrder;

    /// <summary>
    /// Returns the noun of a scale: <c>тисяча</c>, <c>мільйон</c> and <c>мільярд</c> from the tables above, every scale from
    /// <c>трильйон</c> (10^12, the first generated noun) from the case keys of the UK <c>&lt;GeneratedScaleForm&gt;</c>,
    /// through the converter (NTS-42 F, see <see cref="SlavicAdjectivalOrdinalLanguageSpecifics.ReadScaleNoun"/>).
    /// </summary>
    /// <param name="scale">The scale index, at least 1.</param>
    /// <param name="scaleNouns">The converter's scale nouns, or <see langword="null"/>.</param>
    /// <returns>The noun, or <see langword="null"/> when it has no source.</returns>
    private ScaleNoun? GetScaleNoun(int scale, IScaleNounSource? scaleNouns) => scale switch
    {
        1 => s_thousand,
        2 => s_million,
        3 => s_milliard,
        _ => ReadScaleNoun(scaleNouns, scale),
    };

    /// <summary>
    /// Builds the reading of a fraction in an oblique case (or the accusative): the numerator declined as a feminine
    /// cardinal (<see cref="DeclineCardinal"/>), the denominator ordinal declined after it. After a numerator that
    /// reads as one (one, or a compound ending in <c>одна</c>, as in the nominative) the denominator is the feminine
    /// singular in the same case (<i>ділити на одну двадцяту</i>; <i>двадцяти одній сотій</i>); after a numerator
    /// that is or ends in a noun, which governs the genitive (§105.11), the genitive plural in every case: a scale noun
    /// (<c>тисяча</c>, <c>мільйон</c>, <c>мільярд</c>, <c>трильйон</c> …: <i>одним трильйоном третіх</i>) or <c>нуль</c>,
    /// after which the counted word is not declined (ПНТУ 2015, p. 135: "Якщо десятковий дріб починається на нуль, то
    /// частина цілих не відмінюється": <i>нулем цілих</i>, against <i>трьома цілими</i>; hence <i>нулем третіх</i>);
    /// after any other numerator the plural in the same case (<i>двом третім</i>, <i>трьома п'ятими</i>), the accusative
    /// of an inanimate quantity being its nominative (<i>дві третіх</i>).
    /// </summary>
    /// <param name="numerator">The numerator (may be zero).</param>
    /// <param name="prefix">The undeclined words of the compound ordinal denominator (§106.2).</param>
    /// <param name="last">The last ordinal component of the denominator, masculine nominative.</param>
    /// <param name="fractionCase">The requested case, not the nominative.</param>
    /// <param name="scaleNouns">The converter's scale nouns, or <see langword="null"/> (no scale from 10^12 is then declined).</param>
    /// <param name="reading">Receives the reading.</param>
    /// <returns><see langword="false"/> when a scale noun of the numerator has no sourced form.</returns>
    private bool TryBuildObliqueFraction(BigInteger numerator, string prefix, string last, FractionCase fractionCase,
        IScaleNounSource? scaleNouns, out FractionDenominatorReading? reading)
    {
        reading = null;
        if (DeclineCardinal(numerator, fractionCase, scaleNouns) is not string numeratorText)
            return false;
        string word;
        if (!numerator.IsZero && ClassifyFractionNumerator(numerator, compoundEndingInOneIsSingular: true) == FractionNumeratorClass.One)
            word = Decline(last, Gender.Feminine, s_ordinalColumn[(int)fractionCase]);
        else
            word = PluralOrdinal(last, numerator % 1000 == 0 ? FractionCase.Genitive : fractionCase);
        reading = FractionDenominatorReading.WithNumerator(numeratorText, prefix.Length == 0 ? word : prefix + " " + word);
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
    /// Declines a cardinal for a case, every part declined (§105.6), the units feminine as a fraction numerator
    /// (<c>одна</c>, <c>дві</c>) and the groups spelled as the UK configuration spells them (<c>тисяча</c> alone for one
    /// thousand, <c>один мільйон</c>, <c>один трильйон</c>); zero is the noun <c>нуль</c> (<see cref="s_zeroCases"/>).
    /// The scale nouns are <see cref="GetScaleNoun"/>'s: from <c>трильйон</c> the generated nouns of the converter, whose
    /// paradigm the UK configuration declares (<i>трильйона, трильйонові, трильйоном</i>). The nominative is the cardinal
    /// of the configuration with the feminine units.
    /// </summary>
    /// <param name="number">The number, at least zero.</param>
    /// <param name="fractionCase">The case.</param>
    /// <param name="scaleNouns">The converter's scale nouns, or <see langword="null"/> (a scale from 10^12 then has no form).</param>
    /// <returns>The declined cardinal, or <see langword="null"/> when a scale noun has no sourced form.</returns>
    private string? DeclineCardinal(BigInteger number, FractionCase fractionCase, IScaleNounSource? scaleNouns)
    {
        if (number.IsZero)
            return s_zeroCases[(int)fractionCase];
        var groups = ThousandGroups(number);
        var words = new List<string>();
        for (int scale = groups.Count - 1; scale >= 1; scale--)
        {
            if (groups[scale] == 0)
                continue;
            if (GetScaleNoun(scale, scaleNouns) is not ScaleNoun noun
                || !AppendDeclinedScale(words, groups[scale], fractionCase, noun, bareOne: scale == 1))
                return null;
        }
        AppendDeclinedGroup(words, groups[0], fractionCase, feminine: true);
        return string.Join(" ", words);
    }

    /// <summary>
    /// Appends a declined scale group: the multiplier and the scale noun, which after a multiplier ending in one (not
    /// eleven) is singular, and otherwise is the nominative plural after two to four and the genitive plural after the
    /// other multipliers in the nominative and accusative (§105.11, as <see cref="AppendCardinalChunk"/>), and the
    /// plural of the requested case in the oblique cases (<i>чотирьохсот сімдесяти восьми тисяч</i>).
    /// </summary>
    /// <param name="words">The target word list.</param>
    /// <param name="group">The multiplier, 1–999.</param>
    /// <param name="fractionCase">The case.</param>
    /// <param name="noun">The scale noun (a feminine noun, <c>тисяча</c>, takes <c>одна</c>, <c>дві</c>).</param>
    /// <param name="bareOne">Whether a multiplier of one is left out (<c>тисяча</c>, not <c>одна тисяча</c>).</param>
    /// <returns><see langword="false"/> when the noun has no sourced form.</returns>
    private static bool AppendDeclinedScale(List<string> words, int group, FractionCase fractionCase, ScaleNoun noun, bool bareOne)
    {
        int lastTwo = group % 100;
        bool teen = lastTwo is >= 10 and < 20;
        int lastDigit = lastTwo % 10;
        string? form;
        if (!teen && lastDigit == 1)
            form = noun.Singular[(int)fractionCase];
        else if (fractionCase is not (FractionCase.Nominative or FractionCase.Accusative))
            form = noun.Plural[(int)fractionCase];
        else
            form = noun.Plural[!teen && lastDigit is >= 2 and <= 4 ? (int)FractionCase.Nominative : (int)FractionCase.Genitive];
        if (form is null)
            return false;
        if (group != 1 || !bareOne)
            AppendDeclinedGroup(words, group, fractionCase, noun.Feminine);
        words.Add(form);
        return true;
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
