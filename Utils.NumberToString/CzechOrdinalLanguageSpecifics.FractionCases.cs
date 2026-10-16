using System.Collections.Generic;
using System.Numerics;

namespace Utils.NumberToString;

/// <summary>
/// The declined cardinals of Czech fractions (NTS-40, NTS-41). The IJP ("Členění čísel, víceslovné číslovkové výrazy")
/// allows three ways of declining a multi-word numeral: every part (<i>před třemi sty šedesáti pěti lety</i>, <i>bez
/// tisíce osmi set čtyřiceti sedmi korun</i>), only the tens and units (<i>o milion tři sta padesát osm tisíc čtyři sta
/// dvaceti třech dokladech</i>), or none (in dictation and arithmetic). This class declines every part: the strategy the
/// IJP lists first, the one of the mathematical reading (E. Prandstetter, "Čtení matematických značek a výrazů", Naše
/// řeč 22, 1938, 78–86: <i>šedesát osm lomeno třemi sty padesáti šesti</i>), compositional, and the one NTS-39 already
/// used below 1000; <c>sto</c> followed by another numeral stays undeclined (IJP "Počítaný předmět po číslovkách":
/// <i>ve sto dvaceti případech</i>).
/// </summary>
public sealed partial class CzechOrdinalLanguageSpecifics
{
    // Every table below is in FractionCase order: nominative, genitive, dative, accusative, locative, instrumental.

    /// <summary>The feminine <c>jedna</c> (cs.wiktionary <c>jeden</c>; Prandstetter: <i>lomeno jednou celou</i>).</summary>
    private static readonly string[] s_oneFeminineCases = ["jedna", "jedné", "jedné", "jednu", "jedné", "jednou"];

    /// <summary>The masculine inanimate <c>jeden</c> (cs.wiktionary <c>jeden</c>), the multiplier of <c>milion</c>.</summary>
    private static readonly string[] s_oneMasculineCases = ["jeden", "jednoho", "jednomu", "jeden", "jednom", "jedním"];

    /// <summary>
    /// The cases of 2–4 (index = value; the nominative and accusative of 2 are the counting form <c>dva</c>): IJP
    /// <c>dva</c> (2. a 6. <c>dvou</c>, 3. a 7. <c>dvěma</c>), <c>tři</c> (<c>tří</c>, <c>třem</c>, <c>třech</c>,
    /// <c>třemi</c>), <c>čtyři</c> (<c>čtyř</c>, <c>čtyřem</c>, <c>čtyřech</c>, <c>čtyřmi</c>).
    /// </summary>
    private static readonly string[][] s_fewCases =
    [
        [],
        [],
        ["dva", "dvou", "dvěma", "dva", "dvou", "dvěma"],
        ["tři", "tří", "třem", "tři", "třech", "třemi"],
        ["čtyři", "čtyř", "čtyřem", "čtyři", "čtyřech", "čtyřmi"],
    ];

    private static readonly string[] s_cardinalUnits = ["", "jedna", "dva", "tři", "čtyři", "pět", "šest", "sedm", "osm", "devět"];
    private static readonly string[] s_cardinalTeens =
        ["deset", "jedenáct", "dvanáct", "třináct", "čtrnáct", "patnáct", "šestnáct", "sedmnáct", "osmnáct", "devatenáct"];
    private static readonly string[] s_cardinalTens = ["", "", "dvacet", "třicet", "čtyřicet", "padesát", "šedesát", "sedmdesát", "osmdesát", "devadesát"];
    private static readonly string[] s_cardinalHundreds = ["", "sto", "dvě stě", "tři sta", "čtyři sta", "pět set", "šest set", "sedm set", "osm set", "devět set"];

    /// <summary>The singular of <c>sto</c> standing alone (IJP <c>sto</c>: <c>sta</c>, <c>stu</c>, <c>stem</c>).</summary>
    private static readonly string[] s_hundredCases = ["sto", "sta", "stu", "sto", "stu", "stem"];

    /// <summary>The plural of <c>sto</c> after 2–9 in the oblique cases (IJP <c>sto</c>: <c>set</c>, <c>stům</c>, <c>stech</c>, <c>sty</c>).</summary>
    private static readonly string[] s_hundredsPlural = ["", "set", "stům", "", "stech", "sty"];

    /// <summary>The singular of <c>tisíc</c> (IJP <c>tisíc</c>).</summary>
    private static readonly string[] s_thousandSingular = ["tisíc", "tisíce", "tisíci", "tisíc", "tisíci", "tisícem"];

    /// <summary>The plural of <c>tisíc</c> (IJP <c>tisíc</c>: 2. <c>tisíc</c>, 3. <c>tisícům</c>, 6. <c>tisících</c>, 7. <c>tisíci</c>).</summary>
    private static readonly string[] s_thousandPlural = ["tisíce", "tisíc", "tisícům", "tisíce", "tisících", "tisíci"];

    /// <summary>The singular of <c>milion</c> (IJP <c>milion</c>).</summary>
    private static readonly string[] s_millionSingular = ["milion", "milionu", "milionu", "milion", "milionu", "milionem"];

    /// <summary>The plural of <c>milion</c> (IJP <c>milion</c>).</summary>
    private static readonly string[] s_millionPlural = ["miliony", "milionů", "milionům", "miliony", "milionech", "miliony"];

    /// <summary>The singular of <c>miliarda</c> (IJP <c>miliarda</c>).</summary>
    private static readonly string[] s_milliardSingular = ["miliarda", "miliardy", "miliardě", "miliardu", "miliardě", "miliardou"];

    /// <summary>The plural of <c>miliarda</c> (IJP <c>miliarda</c>).</summary>
    private static readonly string[] s_milliardPlural = ["miliardy", "miliard", "miliardám", "miliardy", "miliardách", "miliardami"];

    /// <summary>The largest number this class declines: the milliards end the long scale below <c>bilion</c>.</summary>
    private const long MaxDeclinedNumber = 999_999_999_999;

    /// <summary>The gender of a numeral's one and two in the nominative and accusative.</summary>
    private enum NumeralGender
    {
        /// <summary>The masculine (<c>jeden</c>, <c>dva</c>): before <c>tisíc</c> and <c>milion</c>.</summary>
        Masculine,
        /// <summary>The feminine (<c>jedna</c>, <c>dvě</c>): a fraction numerator, before <c>miliarda</c>.</summary>
        Feminine,
    }

    /// <summary>
    /// Builds the reading of a fraction whose denominator has a fraction noun in a case other than the nominative. The
    /// numerals one to four agree with the noun (IJP "Počítaný předmět po číslovkách": <i>dvě ženy, dvou žen, dvěma
    /// ženám</i>); after the numerals 5–99 the noun takes the case of the phrase in the oblique cases (<i>pěti ženám, o
    /// pěti ženách, s pěti ženami</i>) and the genitive plural in the accusative, as in the nominative; after a numeral
    /// ending in <c>sto</c>, <c>tisíc</c>, <c>milion</c> or <c>miliarda</c> the noun keeps the genitive plural, the basic
    /// construction the IJP gives for these numerals (<i>tisíce žen</i>, <i>milionu měst</i>; <i>ke stu korun</i>). The
    /// noun follows its IJP paradigm (<c>třetina</c>: <c>třetině</c>, <c>třetinou</c>, <c>třetinám</c>,
    /// <c>třetinách</c>, <c>třetinami</c>). A compound numerator ending in <c>jedna</c> (21, 101) has no sourced
    /// oblique form (the IJP gives only <i>dvacet jeden žák</i> / <i>dvacet jedna žáků</i> in the nominative) and fails
    /// closed outside the accusative, as does a zero numerator.
    /// </summary>
    /// <param name="numerator">The numerator.</param>
    /// <param name="forms">The fraction noun.</param>
    /// <param name="fractionCase">The requested case, not the nominative.</param>
    /// <param name="reading">Receives the reading.</param>
    /// <returns><see langword="false"/> when the reading is not sourced.</returns>
    private static bool TryBuildObliqueNounFraction(BigInteger numerator, FractionNounForms forms, FractionCase fractionCase,
        out FractionDenominatorReading? reading)
    {
        reading = null;
        if (numerator.IsZero || numerator > MaxDeclinedNumber)
            return false;
        long value = (long)numerator;
        if (value == 1)
        {
            reading = FractionDenominatorReading.WithNumerator(s_oneFeminineCases[(int)fractionCase], DeclineFractionNoun(forms, plural: false, fractionCase));
            return true;
        }
        if (DeclineCardinal(value, fractionCase, NumeralGender.Feminine, finalOneInstrumental: false) is not string numeratorText)
            return false;
        bool genitivePlural = value > 4 && (fractionCase == FractionCase.Accusative || value % 100 == 0);
        string noun = DeclineFractionNoun(forms, plural: true, genitivePlural ? FractionCase.Genitive : fractionCase);
        reading = FractionDenominatorReading.WithNumerator(numeratorText, noun);
        return true;
    }

    /// <summary>
    /// Declines a fraction noun after its IJP paradigm (the <c>žena</c> pattern of every <c>-ina</c> noun: <c>třetina</c>,
    /// <c>třetiny</c>, <c>třetině</c>, <c>třetinu</c>, <c>třetině</c>, <c>třetinou</c>; plural <c>třetiny</c>,
    /// <c>třetin</c>, <c>třetinám</c>, <c>třetiny</c>, <c>třetinách</c>, <c>třetinami</c>).
    /// </summary>
    /// <param name="forms">The noun's nominative singular, nominative plural and genitive plural.</param>
    /// <param name="plural">Whether the plural is required.</param>
    /// <param name="fractionCase">The case.</param>
    /// <returns>The declined noun.</returns>
    private static string DeclineFractionNoun(FractionNounForms forms, bool plural, FractionCase fractionCase)
    {
        string stem = forms.One[..^1];
        if (!plural)
        {
            return fractionCase switch
            {
                FractionCase.Nominative => forms.One,
                FractionCase.Genitive => stem + "y",
                FractionCase.Accusative => stem + "u",
                FractionCase.Instrumental => stem + "ou",
                _ => stem + "ě",
            };
        }
        return fractionCase switch
        {
            FractionCase.Genitive => forms.Many,
            FractionCase.Dative => stem + "ám",
            FractionCase.Locative => stem + "ách",
            FractionCase.Instrumental => stem + "ami",
            _ => forms.Few,
        };
    }

    /// <summary>
    /// Declines a cardinal between 2 and 999 999 999 999 for a case, every part declined (see the class remarks): the
    /// scale nouns follow their IJP paradigms, singular after one and plural after the other multipliers (the
    /// nominative plural after two to four and the genitive plural after the other multipliers in the nominative and
    /// accusative, as the CS configuration writes them: <i>dva tisíce</i>, <i>pět milionů</i>, <i>dvacet dva
    /// milionů</i>; the plural of the requested case otherwise: <i>s třemi tisíci</i>, <i>před třemi sty tisíci
    /// lety</i>). The nominative and accusative keep the counting forms of a compound (<i>dvacet jedna</i>,
    /// <i>dvacet dva</i>). A <c>jedna</c> ending a compound or following a larger group (21, 101, 1001) has an oblique form
    /// only in the instrumental of the mathematical reading (<paramref name="finalOneInstrumental"/>: <c>jednou</c>, see
    /// <see cref="TryGetFractionDenominator"/>); a multiplier ending in one after the tens or hundreds (<c>dvacet jedna
    /// tisíc</c>) has none.
    /// </summary>
    /// <param name="number">The number, at least 2.</param>
    /// <param name="fractionCase">The case.</param>
    /// <param name="gender">The gender of a simple one or two in the nominative and accusative.</param>
    /// <param name="finalOneInstrumental">Whether a final <c>jedna</c> takes the feminine instrumental <c>jednou</c>.</param>
    /// <returns>The declined cardinal, or <see langword="null"/> when a part has no sourced form.</returns>
    private static string? DeclineCardinal(long number, FractionCase fractionCase, NumeralGender gender, bool finalOneInstrumental)
    {
        var words = new List<string>();
        bool ok = AppendDeclinedScale(words, (int)(number / 1_000_000_000 % 1000), fractionCase, s_milliardSingular, s_milliardPlural,
                NumeralGender.Feminine, bareOne: false)
            && AppendDeclinedScale(words, (int)(number / 1_000_000 % 1000), fractionCase, s_millionSingular, s_millionPlural,
                NumeralGender.Masculine, bareOne: false)
            && AppendDeclinedScale(words, (int)(number / 1000 % 1000), fractionCase, s_thousandSingular, s_thousandPlural,
                NumeralGender.Masculine, bareOne: true);
        if (!ok)
            return null;
        int units = (int)(number % 1000);
        if (units == 1 && words.Count > 0)
        {
            // A jedna after a larger group (1001, 1 000 001) ends the numeral as in a compound.
            if (fractionCase is FractionCase.Nominative or FractionCase.Accusative)
                words.Add("jedna");
            else if (fractionCase == FractionCase.Instrumental && finalOneInstrumental)
                words.Add("jednou");
            else
                return null;
        }
        else if (units > 0 && !AppendDeclinedGroup(words, units, fractionCase, gender, finalOneInstrumental))
            return null;
        return string.Join(" ", words);
    }

    /// <summary>Appends a declined scale group: the multiplier and the scale noun in the form the multiplier governs.</summary>
    /// <param name="words">The target word list.</param>
    /// <param name="group">The multiplier (0–999; 0 appends nothing).</param>
    /// <param name="fractionCase">The case.</param>
    /// <param name="singular">The singular of the scale noun by case.</param>
    /// <param name="plural">The plural of the scale noun by case.</param>
    /// <param name="gender">The gender of the scale noun (the multiplier one or two agrees with it).</param>
    /// <param name="bareOne">Whether a multiplier of one is left out (<c>tisíc</c>, not <c>jeden tisíc</c>).</param>
    /// <returns><see langword="false"/> when the multiplier has no sourced form.</returns>
    private static bool AppendDeclinedScale(List<string> words, int group, FractionCase fractionCase, string[] singular, string[] plural,
        NumeralGender gender, bool bareOne)
    {
        if (group == 0)
            return true;
        int column = (int)fractionCase;
        bool direct = fractionCase is FractionCase.Nominative or FractionCase.Accusative;
        if (group == 1)
        {
            if (!bareOne)
                words.Add((gender == NumeralGender.Feminine ? s_oneFeminineCases : s_oneMasculineCases)[column]);
            words.Add(singular[column]);
            return true;
        }
        if (!AppendDeclinedGroup(words, group, fractionCase, gender, finalOneInstrumental: false))
            return false;
        words.Add(!direct ? plural[column] : group <= 4 ? plural[(int)FractionCase.Nominative] : plural[(int)FractionCase.Genitive]);
        return true;
    }

    /// <summary>
    /// Appends a group between 1 and 999 declined for a case: the hundreds (<c>sto</c> alone after its paradigm, before
    /// another numeral undeclined; 200–900 the multiplier and the plural of <c>sto</c>: <i>dvou set</i>, <i>dvěma
    /// stům</i>, <i>o dvou stech</i>, <i>třemi sty</i>), the tens and teens (the paradigm of <c>pět</c>: <c>dvaceti</c>,
    /// <c>jedenácti</c>) and the units, each declined.
    /// </summary>
    /// <param name="words">The target word list.</param>
    /// <param name="group">The group, 1–999.</param>
    /// <param name="fractionCase">The case.</param>
    /// <param name="gender">The gender of a simple one or two in the nominative and accusative.</param>
    /// <param name="finalOneInstrumental">Whether a final <c>jedna</c> after the tens or hundreds takes <c>jednou</c>.</param>
    /// <returns><see langword="false"/> when the group ends in an unsourced one.</returns>
    private static bool AppendDeclinedGroup(List<string> words, int group, FractionCase fractionCase, NumeralGender gender, bool finalOneInstrumental)
    {
        int column = (int)fractionCase;
        bool direct = fractionCase is FractionCase.Nominative or FractionCase.Accusative;
        int hundreds = group / 100;
        int remainder = group % 100;
        if (hundreds == 1)
            words.Add(remainder > 0 ? "sto" : s_hundredCases[column]);
        else if (hundreds > 1)
            words.Add(direct ? s_cardinalHundreds[hundreds] : DeclineUnit(hundreds, fractionCase) + " " + s_hundredsPlural[column]);
        if (remainder >= 10 && remainder < 20)
        {
            words.Add(direct ? s_cardinalTeens[remainder - 10] : s_instrumentalTeens[remainder - 10]);
            return true;
        }
        if (remainder >= 20)
            words.Add(direct ? s_cardinalTens[remainder / 10] : s_instrumentalTens[remainder / 10]);
        int unit = remainder % 10;
        if (unit == 0)
            return true;
        bool simple = group == unit;
        if (unit == 1 && !simple)
        {
            // The counting form ending a compound (dvacet jedna, sto jedna).
            if (direct)
                words.Add("jedna");
            else if (fractionCase == FractionCase.Instrumental && finalOneInstrumental)
                words.Add("jednou");
            else
                return false;
            return true;
        }
        if (simple && direct && unit <= 2 && gender == NumeralGender.Feminine)
            words.Add(unit == 1 ? s_oneFeminineCases[column] : "dvě");
        else if (unit == 1)
            words.Add((gender == NumeralGender.Feminine ? s_oneFeminineCases : s_oneMasculineCases)[column]);
        else
            words.Add(direct ? s_cardinalUnits[unit] : DeclineUnit(unit, fractionCase));
        return true;
    }

    /// <summary>Declines a unit from 2 to 9 in an oblique case (2–4 after their paradigms, 5–9 after <c>pět</c>: <c>pěti</c>).</summary>
    /// <param name="unit">The unit, 2–9.</param>
    /// <param name="fractionCase">An oblique case.</param>
    /// <returns>The declined unit.</returns>
    private static string DeclineUnit(int unit, FractionCase fractionCase)
        => unit <= 4 ? s_fewCases[unit][(int)fractionCase] : s_instrumentalUnits[unit];
}
