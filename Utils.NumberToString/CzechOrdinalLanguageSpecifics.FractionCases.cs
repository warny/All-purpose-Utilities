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
/// <i>ve sto dvaceti případech</i>). A <c>jeden/jedna</c> ending a compound (21, 101, 1001, a multiplier 21 000) is
/// declined with the rest and agrees with what it counts, the counted noun then in the singular of the case (NTS-45, see
/// <see cref="FinalOne"/>).
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

    /// <summary><c>tisíc</c>, masculine (<i>dva tisíce</i>).</summary>
    private static readonly ScaleNoun s_thousand = new(s_thousandSingular, s_thousandPlural, Feminine: false);

    /// <summary><c>milion</c>, masculine inanimate (<i>jeden milion</i>).</summary>
    private static readonly ScaleNoun s_million = new(s_millionSingular, s_millionPlural, Feminine: false);

    /// <summary><c>miliarda</c>, feminine (<i>dvě miliardy</i>).</summary>
    private static readonly ScaleNoun s_milliard = new(s_milliardSingular, s_milliardPlural, Feminine: true);

    /// <summary>The gender of what a numeral counts: the gender of its one, and of its two in the nominative and accusative.</summary>
    private enum NumeralGender
    {
        /// <summary>The masculine (<c>jeden</c>, <c>dva</c>): before <c>tisíc</c>, <c>milion</c> and <c>bilion</c>.</summary>
        Masculine,
        /// <summary>The feminine (<c>jedna</c>, <c>dvě</c>): a fraction numerator or denominator, before <c>miliarda</c> and <c>biliarda</c>.</summary>
        Feminine,
    }

    /// <summary>
    /// Whether a number of at least two ends in a compound one (21, 101, 1001, 21 000 000 001; not 11, 111): the
    /// <c>jeden/jedna</c> that, declined, makes the counted noun agree with it in the singular (see <see cref="FinalOne"/>).
    /// </summary>
    /// <param name="number">The number, at least 2.</param>
    /// <returns><see langword="true"/> when the last word of the numeral is <c>jedna</c>.</returns>
    private static bool EndsInCompoundOne(BigInteger number) => number % 10 == 1 && number % 100 != 11;

    /// <summary>
    /// The <c>jeden/jedna</c> ending a compound numeral (21, 101, 1001, 1 000 001) or a multiplier (21 000, 101 000 000),
    /// shared by the numerators (NTS-42 A), the multipliers (NTS-42 B) and the <c>lomeno</c> denominators (NTS-41). The
    /// nominative and accusative keep the counting form <c>jedna</c> (<i>dvacet jedna třetin</i>, <i>dvacet jedna
    /// tisíc</i>: IJP "Počítaný předmět po číslovkách", the genitive "běžnější a přirozenější"). The other cases decline
    /// it with every other part (IJP "Členění čísel": <i>před třemi sty šedesáti pěti lety</i>) and make it agree with
    /// what it counts, as the IJP makes the counted noun agree with the last member of a compound when the noun is not in
    /// the genitive (<i>dvacet jeden žák</i>): the feminine of a fraction noun or of an arithmetic term (<c>jedné</c>,
    /// <c>jednou</c>), the gender of a scale noun (<c>jednomu tisíci</c>, <c>jedním milionem</c>, <c>jedné
    /// miliardě</c>); the counted noun is then in the singular of the case. Corpus examples attest this agreement in the
    /// oblique cases (ČNK, in "Sto jeden žák, či sto jedna žáků", ÚČJTK FF UK, 2009: <i>dožil se devadesáti jednoho
    /// roku</i>, <i>po sto jednom roce</i>, <i>sto padesát jedním hlasem</i>, the singular prevailing after 101 and
    /// 1001); the inverted <i>jednadvaceti</i> is not the configuration's word order and has no form for 101.
    /// </summary>
    /// <param name="fractionCase">The case.</param>
    /// <param name="gender">The gender of what the one counts.</param>
    /// <returns>The form of the one.</returns>
    private static string FinalOne(FractionCase fractionCase, NumeralGender gender)
        => fractionCase is FractionCase.Nominative or FractionCase.Accusative
            ? "jedna"
            : (gender == NumeralGender.Feminine ? s_oneFeminineCases : s_oneMasculineCases)[(int)fractionCase];

    /// <summary>
    /// Returns the noun of a scale: <c>tisíc</c>, <c>milion</c> and <c>miliarda</c> from the tables above, every scale from
    /// <c>bilion</c> (10^12, the first generated noun past <c>miliarda</c>) from the case keys of the CS
    /// <c>&lt;GeneratedScaleForm&gt;</c>, through the converter (NTS-42 F, see
    /// <see cref="SlavicAdjectivalOrdinalLanguageSpecifics.ReadScaleNoun"/>).
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
    /// Builds the reading of a fraction whose denominator has a fraction noun in a case other than the nominative. The
    /// numerals one to four agree with the noun (IJP "Počítaný předmět po číslovkách": <i>dvě ženy, dvou žen, dvěma
    /// ženám</i>); after the numerals 5–99 the noun takes the case of the phrase in the oblique cases (<i>pěti ženám, o
    /// pěti ženách, s pěti ženami</i>) and the genitive plural in the accusative, as in the nominative; after a numeral
    /// ending in <c>sto</c>, <c>tisíc</c> or a scale noun (<c>milion</c>, <c>miliarda</c>, <c>bilion</c> …) the noun keeps the genitive plural, the basic
    /// construction the IJP gives for these numerals (<i>tisíce žen</i>, <i>milionu měst</i>; <i>ke stu korun</i>). The
    /// noun follows its IJP paradigm (<c>třetina</c>: <c>třetině</c>, <c>třetinou</c>, <c>třetinám</c>,
    /// <c>třetinách</c>, <c>třetinami</c>). A compound numerator ending in <c>jedna</c> (21, 101, 1001, 10^12 + 1)
    /// declines its <c>jedna</c> with the noun in the singular of the case (NTS-42 A, see <see cref="FinalOne"/>):
    /// <i>dvaceti jedné setiny</i>, <i>sto jedné třetině</i>, <i>tisícem jednou třetinou</i>; the accusative keeps the
    /// counting form of the nominative (<i>dvacet jedna třetin</i>). A zero numerator fails closed: the usage of an
    /// indeclinable <i>nula</i> before a counted noun (<i>při nula stupních</i>) has no Czech normative source, and the
    /// IJP declines <c>nula</c> as a noun only (NTS-42 E).
    /// </summary>
    /// <param name="numerator">The numerator.</param>
    /// <param name="forms">The fraction noun.</param>
    /// <param name="fractionCase">The requested case, not the nominative.</param>
    /// <param name="scaleNouns">The converter's scale nouns, or <see langword="null"/> (no scale from 10^12 is then declined).</param>
    /// <param name="reading">Receives the reading.</param>
    /// <returns><see langword="false"/> when the reading is not sourced.</returns>
    private bool TryBuildObliqueNounFraction(BigInteger numerator, FractionNounForms forms, FractionCase fractionCase,
        IScaleNounSource? scaleNouns, out FractionDenominatorReading? reading)
    {
        reading = null;
        if (numerator.IsZero)
            return false;
        if (numerator.IsOne)
        {
            reading = FractionDenominatorReading.WithNumerator(s_oneFeminineCases[(int)fractionCase], DeclineFractionNoun(forms, plural: false, fractionCase));
            return true;
        }
        if (DeclineCardinal(numerator, fractionCase, NumeralGender.Feminine, scaleNouns) is not string numeratorText)
            return false;
        bool direct = fractionCase == FractionCase.Accusative;
        bool genitivePlural = numerator > 4 && (direct || numerator % 100 == 0);
        string noun = !direct && EndsInCompoundOne(numerator)
            ? DeclineFractionNoun(forms, plural: false, fractionCase)
            : DeclineFractionNoun(forms, plural: true, genitivePlural ? FractionCase.Genitive : fractionCase);
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
    /// Declines a cardinal of at least 2 for a case, every part declined (see the class remarks): the scale nouns follow
    /// their IJP paradigms (<c>tisíc</c>, <c>milion</c>, <c>miliarda</c> here; from <c>bilion</c> the generated nouns of
    /// the converter, whose IJP paradigms the CS configuration declares: <i>bilion, bilionu, bilionem</i>, <i>biliarda,
    /// biliardě, biliardou</i>), singular after one and plural after the other multipliers (the
    /// nominative plural after two to four and the genitive plural after the other multipliers in the nominative and
    /// accusative, as the CS configuration writes them: <i>dva tisíce</i>, <i>pět milionů</i>, <i>dvacet dva
    /// milionů</i>; the plural of the requested case otherwise: <i>s třemi tisíci</i>, <i>před třemi sty tisíci
    /// lety</i>). The nominative and accusative keep the counting forms of a compound (<i>dvacet jedna</i>,
    /// <i>dvacet dva</i>). A <c>jedna</c> ending the number or a multiplier, after the tens, the hundreds or a larger
    /// group (21, 101, 1001, 21 000), is <see cref="FinalOne"/>: in the oblique cases it agrees with what it counts, the
    /// feminine <paramref name="gender"/> of the fraction noun or of the arithmetic term (<c>jedné</c>; <c>jednou</c> in
    /// the instrumental of the mathematical reading, see <see cref="TryGetFractionDenominator"/>), or the gender of the
    /// scale noun after a multiplier, which is then in the singular of the case (<i>dvaceti jednomu tisíci</i>,
    /// <i>dvaceti jedním milionem</i>, <i>dvaceti jedné miliardě</i>).
    /// </summary>
    /// <param name="number">The number, at least 2.</param>
    /// <param name="fractionCase">The case.</param>
    /// <param name="gender">The gender of what the number counts (see <see cref="NumeralGender"/>).</param>
    /// <param name="scaleNouns">The converter's scale nouns, or <see langword="null"/> (a scale from 10^12 then has no form).</param>
    /// <returns>The declined cardinal, or <see langword="null"/> when a scale noun has no sourced form.</returns>
    private string? DeclineCardinal(BigInteger number, FractionCase fractionCase, NumeralGender gender, IScaleNounSource? scaleNouns)
    {
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
        int units = groups[0];
        if (units == 1 && words.Count > 0)
            // A jedna after a larger group (1001, 1 000 001) ends the numeral as in a compound.
            words.Add(FinalOne(fractionCase, gender));
        else if (units > 0)
            AppendDeclinedGroup(words, units, fractionCase, gender);
        return string.Join(" ", words);
    }

    /// <summary>
    /// Appends a declined scale group: the multiplier and the scale noun in the form the multiplier governs. A
    /// multiplier ending in a compound one (21, 101) agrees with the noun in an oblique case, the noun in the singular of
    /// the case (<i>dvaceti jednomu tisíci</i>, <i>sto jedním bilionem</i>, see <see cref="FinalOne"/>); in the
    /// nominative and accusative it keeps the counting form and the genitive plural (<i>dvacet jedna tisíc</i>).
    /// </summary>
    /// <param name="words">The target word list.</param>
    /// <param name="group">The multiplier, 1–999.</param>
    /// <param name="fractionCase">The case.</param>
    /// <param name="noun">The scale noun (the multiplier's one or two agrees with its gender).</param>
    /// <param name="bareOne">Whether a multiplier of one is left out (<c>tisíc</c>, not <c>jeden tisíc</c>).</param>
    /// <returns><see langword="false"/> when the noun has no sourced form.</returns>
    private static bool AppendDeclinedScale(List<string> words, int group, FractionCase fractionCase, ScaleNoun noun, bool bareOne)
    {
        int column = (int)fractionCase;
        bool direct = fractionCase is FractionCase.Nominative or FractionCase.Accusative;
        var gender = noun.Feminine ? NumeralGender.Feminine : NumeralGender.Masculine;
        if (group == 1)
        {
            if (noun.Singular[column] is not string singular)
                return false;
            if (!bareOne)
                words.Add((gender == NumeralGender.Feminine ? s_oneFeminineCases : s_oneMasculineCases)[column]);
            words.Add(singular);
            return true;
        }
        string? form = !direct && EndsInCompoundOne(group)
            ? noun.Singular[column]
            : noun.Plural[!direct ? column : group <= 4 ? (int)FractionCase.Nominative : (int)FractionCase.Genitive];
        if (form is null)
            return false;
        AppendDeclinedGroup(words, group, fractionCase, gender);
        words.Add(form);
        return true;
    }

    /// <summary>
    /// Appends a group between 1 and 999 declined for a case: the hundreds (<c>sto</c> alone after its paradigm, before
    /// another numeral undeclined; 200–900 the multiplier and the plural of <c>sto</c>: <i>dvou set</i>, <i>dvěma
    /// stům</i>, <i>o dvou stech</i>, <i>třemi sty</i>), the tens and teens (the paradigm of <c>pět</c>: <c>dvaceti</c>,
    /// <c>jedenácti</c>) and the units, each declined; a one after the tens or hundreds is <see cref="FinalOne"/>.
    /// </summary>
    /// <param name="words">The target word list.</param>
    /// <param name="group">The group, 1–999.</param>
    /// <param name="fractionCase">The case.</param>
    /// <param name="gender">The gender of what the group counts (see <see cref="NumeralGender"/>).</param>
    private static void AppendDeclinedGroup(List<string> words, int group, FractionCase fractionCase, NumeralGender gender)
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
            return;
        }
        if (remainder >= 20)
            words.Add(direct ? s_cardinalTens[remainder / 10] : s_instrumentalTens[remainder / 10]);
        int unit = remainder % 10;
        if (unit == 0)
            return;
        bool simple = group == unit;
        if (unit == 1 && !simple)
            // The one ending a compound (dvacet jedna, dvaceti jedné, sto jedním).
            words.Add(FinalOne(fractionCase, gender));
        else if (simple && direct && unit <= 2 && gender == NumeralGender.Feminine)
            words.Add(unit == 1 ? s_oneFeminineCases[column] : "dvě");
        else if (unit == 1)
            words.Add((gender == NumeralGender.Feminine ? s_oneFeminineCases : s_oneMasculineCases)[column]);
        else
            words.Add(direct ? s_cardinalUnits[unit] : DeclineUnit(unit, fractionCase));
    }

    /// <summary>Declines a unit from 2 to 9 in an oblique case (2–4 after their paradigms, 5–9 after <c>pět</c>: <c>pěti</c>).</summary>
    /// <param name="unit">The unit, 2–9.</param>
    /// <param name="fractionCase">An oblique case.</param>
    /// <returns>The declined unit.</returns>
    private static string DeclineUnit(int unit, FractionCase fractionCase)
        => unit <= 4 ? s_fewCases[unit][(int)fractionCase] : s_instrumentalUnits[unit];
}
