using System.Collections.Generic;
using System.Numerics;

namespace Utils.NumberToString;

/// <summary>
/// Shared ordinal formation for Slavic languages whose ordinals are adjectives agreeing in gender
/// and case. A language decomposes a number into an optional cardinal prefix and one or more
/// ordinal components given in their masculine nominative citation form; every ordinal component
/// is then declined for the requested gender and case.
/// </summary>
/// <remarks>
/// The default value of the gender dimension (and the masculine value) selects the masculine
/// citation form; the default case is the nominative. Only the <see cref="int"/> range is
/// implemented: larger values, and decompositions a language declines, fail closed with
/// <see cref="System.NotSupportedException"/> because no declarative fallback is configured.
/// </remarks>
public abstract class SlavicAdjectivalOrdinalLanguageSpecifics : INumberToStringLanguageSpecifics, IOrdinalLanguageSpecifics
{
    /// <summary>Grammatical gender index used by the ending tables.</summary>
    protected enum Gender
    {
        /// <summary>Masculine (also used for the citation form).</summary>
        Masculine,
        /// <summary>Feminine.</summary>
        Feminine,
        /// <summary>Neuter.</summary>
        Neuter,
    }

    /// <summary>Gets the canonical name of the gender dimension.</summary>
    protected virtual string GenderDimension => "gender";

    /// <summary>Gets the canonical name of the case dimension.</summary>
    protected virtual string CaseDimension => "case";

    /// <summary>Gets the dimension values that select the feminine forms.</summary>
    protected abstract string FeminineValue { get; }

    /// <summary>Gets the dimension values that select the neuter forms.</summary>
    protected abstract string NeuterValue { get; }

    /// <summary>Gets the case values in the column order of the ending tables (nominative first).</summary>
    protected abstract IReadOnlyList<string> CaseValues { get; }

    /// <summary>Decomposes a positive number into a cardinal prefix and its ordinal components.</summary>
    /// <param name="number">A positive integer.</param>
    /// <param name="prefix">The cardinal words preceding the ordinal components (may be empty).</param>
    /// <param name="components">The ordinal components in masculine nominative form.</param>
    /// <returns><see langword="false"/> when the language declines the value.</returns>
    protected abstract bool TryDecompose(int number, out string prefix, out IReadOnlyList<string> components);

    /// <summary>Declines a masculine nominative ordinal for a gender and case.</summary>
    /// <param name="citation">The masculine nominative form.</param>
    /// <param name="gender">The requested gender.</param>
    /// <param name="caseIndex">The case column index.</param>
    /// <returns>The declined form.</returns>
    protected abstract string Decline(string citation, Gender gender, int caseIndex);

    /// <inheritdoc />
    public string FinalizeWriting(string languageIdentifier, string text) => text;

    /// <inheritdoc />
    public bool TryConvertOrdinal(int number, IReadOnlyDictionary<string, string> activeVariants, out string? result)
    {
        result = null;
        if (number <= 0 || !TryDecompose(number, out string prefix, out IReadOnlyList<string> components))
            return false;

        Gender gender = Gender.Masculine;
        if (activeVariants.TryGetValue(GenderDimension, out string? genderValue))
        {
            if (genderValue == FeminineValue)
                gender = Gender.Feminine;
            else if (genderValue == NeuterValue)
                gender = Gender.Neuter;
        }

        int caseIndex = 0;
        if (activeVariants.TryGetValue(CaseDimension, out string? caseValue))
        {
            for (int i = 0; i < CaseValues.Count; i++)
            {
                if (CaseValues[i] == caseValue)
                    caseIndex = i;
            }
        }

        var words = new List<string>(components.Count + 1);
        if (prefix.Length > 0)
            words.Add(prefix);
        foreach (string component in components)
            words.Add(Decline(component, gender, caseIndex));
        result = string.Join(" ", words);
        return true;
    }

    /// <summary>Replaces the citation ending of a form with the ending of the requested cell.</summary>
    /// <param name="citation">The masculine nominative form.</param>
    /// <param name="citationEnding">The citation ending to strip.</param>
    /// <param name="endings">The endings table indexed by gender then case.</param>
    /// <param name="gender">The requested gender.</param>
    /// <param name="caseIndex">The case column index.</param>
    /// <returns>The declined form.</returns>
    protected static string ReplaceEnding(string citation, string citationEnding, string[][] endings, Gender gender, int caseIndex)
        => citation[..^citationEnding.Length] + endings[(int)gender][caseIndex];

    /// <summary>Splits a number into its milliard, million, thousand, and unit groups.</summary>
    /// <param name="number">A positive integer.</param>
    /// <returns>The four groups from the highest.</returns>
    protected static (int Milliards, int Millions, int Thousands, int Units) Groups(int number)
        => (number / 1_000_000_000, number / 1_000_000 % 1000, number / 1000 % 1000, number % 1000);

    /// <summary>
    /// The agreement class of a fraction numerator with its denominator noun or adjective: exactly one, exactly two to
    /// four, or another value (NTS-30H, NTS-39).
    /// </summary>
    private protected enum FractionNumeratorClass
    {
        /// <summary>The numerator is one (or, where the language says so, a compound ending in one).</summary>
        One,
        /// <summary>The numerator is exactly two, three or four.</summary>
        Few,
        /// <summary>Any other numerator: five and more, and the compounds (21, 22, 101 …) unless they read as one.</summary>
        Many,
    }

    /// <summary>
    /// Classifies a non-zero fraction numerator. Only the simple numerals two to four are <see cref="FractionNumeratorClass.Few"/>:
    /// a compound ending in two to four governs the genitive plural in Czech (IJP, "Počítaný předmět po číslovkách":
    /// <i>dvacet dva žáků</i>, the more common and natural form) and Slovak (Morfológia slovenského jazyka 1966:
    /// <i>dvadsaťdva jabĺk</i>), and the plural in Ukrainian (<i>двадцять дві шістдесятих</i>). A compound ending in one
    /// is <see cref="FractionNumeratorClass.Many"/> as well (<i>dvacet jedna žáků</i>; Slovak <i>dvadsaťjeden žien</i>)
    /// unless <paramref name="compoundEndingInOneIsSingular"/>, as in Ukrainian, where the denominator agrees with the
    /// last component (<i>тридцять одна сота</i>).
    /// </summary>
    /// <param name="numerator">The absolute numerator, at least one.</param>
    /// <param name="compoundEndingInOneIsSingular">Whether a compound ending in one (21, 101, not 11) reads as one.</param>
    /// <returns>The class.</returns>
    private protected static FractionNumeratorClass ClassifyFractionNumerator(BigInteger numerator, bool compoundEndingInOneIsSingular)
    {
        if (numerator.IsOne) return FractionNumeratorClass.One;
        if (numerator <= 4) return FractionNumeratorClass.Few;
        int lastTwo = (int)(numerator % 100);
        return compoundEndingInOneIsSingular && lastTwo % 10 == 1 && lastTwo != 11 ? FractionNumeratorClass.One : FractionNumeratorClass.Many;
    }

    /// <summary>Determines whether the caller's variants ask for the nominative, the only case a fraction reading supports.</summary>
    /// <param name="variants">The caller's effective variant query.</param>
    /// <returns><see langword="true"/> when no other case is requested.</returns>
    private protected bool IsNominativeRequested(IReadOnlyDictionary<string, string> variants)
        => !variants.TryGetValue(CaseDimension, out string? caseValue) || caseValue == CaseValues[0];

    /// <summary>The feminine gender forced on a fraction numerator (the fraction words are feminine).</summary>
    /// <returns>The forced variant set.</returns>
    private protected ForcedVariantSet FeminineNumerator() => NumeratorGender(FeminineValue);

    /// <summary>
    /// The forms of one fraction noun (Czech <c>třetina</c>, Slovak <c>tretina</c>) after a numerator of one (nominative
    /// singular), of exactly two to four (nominative plural) and of any other numerator (genitive plural).
    /// </summary>
    /// <param name="One">The nominative singular.</param>
    /// <param name="Few">The nominative plural.</param>
    /// <param name="Many">The genitive plural.</param>
    private protected readonly record struct FractionNounForms(string One, string Few, string Many);

    /// <summary>
    /// Builds the reading of a non-zero fraction whose denominator is a fraction noun of <paramref name="lexicon"/>: the
    /// noun in the form the numerator governs. One and two to four agree with the feminine noun (<c>jedna třetina</c>,
    /// <c>dvě třetiny</c>); with the genitive plural the numerator keeps its counting form, given by
    /// <paramref name="countingGender"/> (<c>pět třetin</c>, <c>dvacet dva třetin</c>, Slovak <c>dvadsaťdva tretín</c>).
    /// </summary>
    /// <param name="numerator">The absolute numerator, at least one.</param>
    /// <param name="denominator">The denominator.</param>
    /// <param name="lexicon">The sourced fraction nouns by denominator.</param>
    /// <param name="countingGender">The gender value of the counting form of the numerals.</param>
    /// <param name="reading">Receives the reading.</param>
    /// <returns><see langword="true"/> when <paramref name="lexicon"/> has a noun for <paramref name="denominator"/>.</returns>
    private protected bool TryBuildFractionNoun(BigInteger numerator, BigInteger denominator, IReadOnlyDictionary<long, FractionNounForms> lexicon,
        string countingGender, out FractionDenominatorReading? reading)
    {
        reading = null;
        if (denominator > long.MaxValue || !lexicon.TryGetValue((long)denominator, out var forms))
            return false;
        reading = ClassifyFractionNumerator(numerator, compoundEndingInOneIsSingular: false) switch
        {
            FractionNumeratorClass.One => new FractionDenominatorReading(forms.One, FeminineNumerator()),
            FractionNumeratorClass.Few => new FractionDenominatorReading(forms.Few, FeminineNumerator()),
            _ => new FractionDenominatorReading(forms.Many, NumeratorGender(countingGender)),
        };
        return true;
    }

    /// <summary>A gender forced on a fraction numerator.</summary>
    /// <param name="value">The gender value.</param>
    /// <returns>The forced variant set.</returns>
    private protected ForcedVariantSet NumeratorGender(string value) => ForcedVariantSet.Create((GenderDimension, value));

    /// <summary>
    /// Writes the instrumental of a number between 2 and 99 from the instrumental tables of a language, the tens and
    /// the unit of a compound as two words (Czech <c>dvaceti třemi</c>, Slovak <c>dvadsiatimi dvoma</c>). A final one
    /// has no instrumental (<see langword="null"/>): its gender in a bare numeral is not sourced (Czech), or the compound
    /// is indeclinable (Slovak <c>dvadsaťjeden</c>).
    /// </summary>
    /// <param name="number">A number between 0 and 99; 0 gives the empty string.</param>
    /// <param name="units">The instrumentals of 0–9 (0 and 1 unused).</param>
    /// <param name="teens">The instrumentals of 10–19.</param>
    /// <param name="tens">The instrumentals of the round tens (indexes 2–9).</param>
    /// <returns>The instrumental, or <see langword="null"/>.</returns>
    private protected static string? InstrumentalBelowHundred(int number, string[] units, string[] teens, string[] tens)
    {
        if (number == 0) return string.Empty;
        if (number is >= 10 and < 20) return teens[number - 10];
        int unit = number % 10;
        if (unit == 1) return null;
        if (number < 10) return units[unit];
        return unit == 0 ? tens[number / 10] : tens[number / 10] + " " + units[unit];
    }
}

/// <summary>
/// Produces Czech ordinals. Every component of a compound is ordinal
/// (<c>dvacátý první</c>, <c>stý dvacátý první</c>, <c>tisící první</c>), declined for gender
/// (<c>standalone</c>/<c>mužský</c>, <c>ženský</c>, <c>střední</c>) and case (hard <c>-ý</c> and soft
/// <c>-í</c> adjective paradigms). Round thousands accept single-digit multipliers
/// (<c>dvoutisící</c>); larger round multipliers and round millions or milliards other than one
/// are declined.
/// </summary>
public sealed class CzechOrdinalLanguageSpecifics : SlavicAdjectivalOrdinalLanguageSpecifics, IFractionLanguageSpecifics
{
    /// <summary>
    /// The fraction nouns of the Internetová jazyková příručka (ÚJČ AV ČR), each an entry with its paradigm (NTS-30H,
    /// NTS-39): 2–20, the round tens, 100, 1000, 100 000, 10^6 and 10^9 (SSČ "zlomk."). The IJP has no entry for a
    /// compound denominator (<c>jednadvacetina</c>, <c>desetitisícina</c>): the lexicon is closed, not productive.
    /// </summary>
    private static readonly Dictionary<long, FractionNounForms> s_fractionNouns = new()
    {
        [2] = new("polovina", "poloviny", "polovin"),
        [3] = new("třetina", "třetiny", "třetin"),
        [4] = new("čtvrtina", "čtvrtiny", "čtvrtin"),
        [5] = new("pětina", "pětiny", "pětin"),
        [6] = new("šestina", "šestiny", "šestin"),
        [7] = new("sedmina", "sedminy", "sedmin"),
        [8] = new("osmina", "osminy", "osmin"),
        [9] = new("devítina", "devítiny", "devítin"),
        [10] = new("desetina", "desetiny", "desetin"),
        [11] = new("jedenáctina", "jedenáctiny", "jedenáctin"),
        [12] = new("dvanáctina", "dvanáctiny", "dvanáctin"),
        [13] = new("třináctina", "třináctiny", "třináctin"),
        [14] = new("čtrnáctina", "čtrnáctiny", "čtrnáctin"),
        [15] = new("patnáctina", "patnáctiny", "patnáctin"),
        [16] = new("šestnáctina", "šestnáctiny", "šestnáctin"),
        [17] = new("sedmnáctina", "sedmnáctiny", "sedmnáctin"),
        [18] = new("osmnáctina", "osmnáctiny", "osmnáctin"),
        [19] = new("devatenáctina", "devatenáctiny", "devatenáctin"),
        [20] = new("dvacetina", "dvacetiny", "dvacetin"),
        [30] = new("třicetina", "třicetiny", "třicetin"),
        [40] = new("čtyřicetina", "čtyřicetiny", "čtyřicetin"),
        [50] = new("padesátina", "padesátiny", "padesátin"),
        [60] = new("šedesátina", "šedesátiny", "šedesátin"),
        [70] = new("sedmdesátina", "sedmdesátiny", "sedmdesátin"),
        [80] = new("osmdesátina", "osmdesátiny", "osmdesátin"),
        [90] = new("devadesátina", "devadesátiny", "devadesátin"),
        [100] = new("setina", "setiny", "setin"),
        [1000] = new("tisícina", "tisíciny", "tisícin"),
        [100_000] = new("stotisícina", "stotisíciny", "stotisícin"),
        [1_000_000] = new("miliontina", "miliontiny", "miliontin"),
        [1_000_000_000] = new("miliardtina", "miliardtiny", "miliardtin"),
    };

    // Instrumentals of the IJP paradigms (dva → dvěma, tři → třemi, čtyři → čtyřmi, pět → pěti, deset → deseti,
    // jedenáct → jedenácti, dvacet → dvaceti, devadesát → devadesáti; "Počítaný předmět po číslovkách": the numerals
    // 5–99 share the paradigm of pět).
    private static readonly string[] s_instrumentalUnits = ["", "", "dvěma", "třemi", "čtyřmi", "pěti", "šesti", "sedmi", "osmi", "devíti"];
    private static readonly string[] s_instrumentalTeens =
        ["deseti", "jedenácti", "dvanácti", "třinácti", "čtrnácti", "patnácti", "šestnácti", "sedmnácti", "osmnácti", "devatenácti"];
    private static readonly string[] s_instrumentalTens =
        ["", "", "dvaceti", "třiceti", "čtyřiceti", "padesáti", "šedesáti", "sedmdesáti", "osmdesáti", "devadesáti"];

    /// <summary>The instrumental of the hundreds 200–900: the multiplier and <c>sty</c> (IJP: <c>třemi sty šedesáti pěti</c>).</summary>
    private static readonly string[] s_instrumentalHundreds =
        ["", "", "dvěma sty", "třemi sty", "čtyřmi sty", "pěti sty", "šesti sty", "sedmi sty", "osmi sty", "devíti sty"];

    /// <summary>
    /// Reads a fraction as the IJP does. A denominator with a fraction noun takes the noun in the form the numerator
    /// governs (<c>jedna třetina</c>, <c>dvě třetiny</c>, <c>pět sedmin</c>); a compound numerator governs the genitive
    /// plural and keeps its counting form (<c>dvacet jedna třetin</c>, <c>dvacet dva třetin</c>: IJP "Počítaný předmět po
    /// číslovkách", the genitive after <i>dvacet jeden … čtyři</i> being "more common and more natural"; "Členění čísel":
    /// <i>tisíc dvacet čtyři celé/celých</i>). Any other denominator from 2 to 999, and a zero numerator, take the
    /// mathematical reading <c>lomeno</c> + instrumental (IJP <c>lomit</c>: SSJČ <i>dvě lomeno třemi</i>, SSČ <i>pět lomeno
    /// dvěma</i>), every part of the denominator declined (<i>třemi sty šedesáti pěti</i>) but <c>sto</c> before another
    /// numeral (<i>ve sto dvaceti případech</i>); a numerator of one to four is feminine (<i>jedna a dvě jsou tři</i>, SSČ
    /// <c>dva</c>), a compound keeps its counting form, as in arithmetic operations (IJP "Členění čísel": <i>k tisíc sedm
    /// set dvacet dva korunám</i>).
    /// Without a sourced form the reading fails closed: a denominator of one, a denominator ending in one after the tens
    /// or hundreds (21, 101: the gender of a bare <c>jedna</c> in the instrumental is not sourced), from 1000 without a
    /// noun, and every case other than the nominative.
    /// </summary>
    /// <inheritdoc />
    public bool TryGetFractionDenominator(BigInteger numerator, BigInteger denominator, IReadOnlyDictionary<string, string> variants,
        out FractionDenominatorReading? reading)
    {
        reading = null;
        if (denominator < 2 || !IsNominativeRequested(variants))
            return false;
        if (!numerator.IsZero && TryBuildFractionNoun(numerator, denominator, s_fractionNouns, "standalone", out reading))
            return true;
        if (denominator > 999 || InstrumentalBelowThousand((int)denominator) is not string instrumental)
            return false;
        bool simple = !numerator.IsZero && ClassifyFractionNumerator(numerator, compoundEndingInOneIsSingular: false) != FractionNumeratorClass.Many;
        reading = new FractionDenominatorReading("lomeno " + instrumental, simple ? FeminineNumerator() : NumeratorGender("standalone"));
        return true;
    }

    /// <summary>
    /// Writes the instrumental of a number between 2 and 999, every part declined (IJP "Členění čísel": <i>třemi sty
    /// šedesáti pěti</i>) except <c>sto</c> followed by another numeral, which stays undeclined (IJP "Počítaný předmět
    /// po číslovkách": <i>ve sto dvaceti případech</i>); <c>sto</c> alone is <c>stem</c>.
    /// </summary>
    /// <param name="number">A number between 2 and 999.</param>
    /// <returns>The instrumental, or <see langword="null"/> when the number ends in one after the tens or hundreds.</returns>
    private static string? InstrumentalBelowThousand(int number)
    {
        int hundreds = number / 100;
        string? rest = InstrumentalBelowHundred(number % 100, s_instrumentalUnits, s_instrumentalTeens, s_instrumentalTens);
        if (rest == null || hundreds == 0)
            return rest;
        string head = hundreds == 1 ? (rest.Length == 0 ? "stem" : "sto") : s_instrumentalHundreds[hundreds];
        return rest.Length == 0 ? head : head + " " + rest;
    }

    private static readonly string[] s_units = ["", "první", "druhý", "třetí", "čtvrtý", "pátý", "šestý", "sedmý", "osmý", "devátý"];
    private static readonly string[] s_teens =
        ["desátý", "jedenáctý", "dvanáctý", "třináctý", "čtrnáctý", "patnáctý", "šestnáctý", "sedmnáctý", "osmnáctý", "devatenáctý"];
    private static readonly string[] s_tens = ["", "", "dvacátý", "třicátý", "čtyřicátý", "padesátý", "šedesátý", "sedmdesátý", "osmdesátý", "devadesátý"];
    private static readonly string[] s_hundreds = ["", "stý", "dvoustý", "třístý", "čtyřstý", "pětistý", "šestistý", "sedmistý", "osmistý", "devítistý"];
    private static readonly string[] s_thousandPrefixes = ["", "", "dvou", "tří", "čtyř", "pěti", "šesti", "sedmi", "osmi", "devíti"];
    private static readonly string[] s_cases = ["nominativ", "genitiv", "dativ", "akuzativ", "lokál", "instrumentál"];

    // Hard (-ý) and soft (-í) adjective endings: [gender][nominative, genitive, dative, accusative, locative, instrumental].
    // The masculine accusative is the inanimate form.
    private static readonly string[][] s_hard =
    [
        ["ý", "ého", "ému", "ý", "ém", "ým"],
        ["á", "é", "é", "ou", "é", "ou"],
        ["é", "ého", "ému", "é", "ém", "ým"],
    ];
    private static readonly string[][] s_soft =
    [
        ["í", "ího", "ímu", "í", "ím", "ím"],
        ["í", "í", "í", "í", "í", "í"],
        ["í", "ího", "ímu", "í", "ím", "ím"],
    ];

    /// <inheritdoc />
    protected override string FeminineValue => "ženský";

    /// <inheritdoc />
    protected override string NeuterValue => "střední";

    /// <inheritdoc />
    protected override IReadOnlyList<string> CaseValues => s_cases;

    /// <inheritdoc />
    protected override string Decline(string citation, Gender gender, int caseIndex)
        => citation.EndsWith('ý')
            ? ReplaceEnding(citation, "ý", s_hard, gender, caseIndex)
            : ReplaceEnding(citation, "í", s_soft, gender, caseIndex);

    /// <inheritdoc />
    protected override bool TryDecompose(int number, out string prefix, out IReadOnlyList<string> components)
    {
        prefix = string.Empty;
        var parts = new List<string>();
        components = parts;
        var (milliards, millions, thousands, units) = Groups(number);
        if (milliards > 0)
        {
            if (milliards != 1)
                return false;
            parts.Add("miliardtý");
        }
        if (millions > 0)
        {
            if (millions != 1)
                return false;
            parts.Add("miliontý");
        }
        if (thousands > 0)
        {
            if (thousands > 9)
                return false;
            parts.Add(s_thousandPrefixes[thousands] + "tisící");
        }
        if (units > 0)
            AddGroup(parts, units);
        return true;
    }

    /// <summary>Adds the ordinal components of a group between 1 and 999.</summary>
    /// <param name="parts">The target component list.</param>
    /// <param name="group">A number between 1 and 999.</param>
    private static void AddGroup(List<string> parts, int group)
    {
        if (group / 100 > 0)
            parts.Add(s_hundreds[group / 100]);
        int remainder = group % 100;
        if (remainder >= 20)
        {
            parts.Add(s_tens[remainder / 10]);
            if (remainder % 10 > 0)
                parts.Add(s_units[remainder % 10]);
        }
        else if (remainder >= 10)
            parts.Add(s_teens[remainder - 10]);
        else if (remainder > 0)
            parts.Add(s_units[remainder]);
    }
}

/// <summary>
/// Produces Slovak ordinals. Tens and units are ordinal (<c>dvadsiaty prvý</c>); hundreds and
/// thousands of a compound stay cardinal, fused with a following unit (<c>stoprvý</c>,
/// <c>dvetisícdruhý</c>) and separate before tens (<c>päťsto dvadsiaty ôsmy</c>). Declined for gender
/// (<c>mužský</c>, <c>ženský</c>, <c>stredný</c>) and case with the long hard (<c>-ý</c>), the
/// short hard (<c>-y</c>, rhythmic law after a long syllable: <c>piaty</c>) and the soft (<c>-í</c>)
/// adjective paradigms. Values from one million upwards (except one million itself) and round or
/// compound thousands above 9999 are declined because their written forms were not verified.
/// </summary>
public sealed class SlovakOrdinalLanguageSpecifics : SlavicAdjectivalOrdinalLanguageSpecifics, IFractionLanguageSpecifics
{
    /// <summary>
    /// The Slovak fraction nouns (NTS-30H, NTS-39). Morfológia slovenského jazyka (1966), 332, gives the rule: from
    /// <c>pätina</c> on the suffix <c>-ina</c> on the cardinal (<i>jedenástina … dvadsatina, päťdesiatina, tisícina</i>),
    /// <c>tretina</c>, <c>štvrtina</c> on the ordinal, <c>stotina</c>, <c>milióntina</c>, <c>miliardtina</c> on an extended
    /// stem, <c>polovica</c> on <i>pol-</i>. The Krátky slovník slovenského jazyka (2003) gives the genitive singular
    /// (= nominative plural, <i>dve tretiny</i>, <i>dve polovice</i>) and genitive plural of 2–10, 12, 16, 17, 19, 20,
    /// 30, 40, 60, 70, 80, 100, 1000 and 10^6 (<i>tretina -y -tín</i>, <i>dvanástina -y -tin</i>, <i>tisícina -y -cin</i>);
    /// 11, 13, 14, 15, 18, 50, 90 and 10^9 follow the rule with the genitive plural of their pattern (<i>-nástin</i> as
    /// <i>dvanástin</i>, <i>-desiatin</i> as <i>šesťdesiatin</i>, <i>miliardtin</i> as <i>milióntin</i>). Compound
    /// denominators are built on the obsolescent inverse cardinal (<i>jedenadvadsatina</i>) and are not listed.
    /// </summary>
    private static readonly Dictionary<long, FractionNounForms> s_fractionNouns = new()
    {
        [2] = new("polovica", "polovice", "polovíc"),
        [3] = new("tretina", "tretiny", "tretín"),
        [4] = new("štvrtina", "štvrtiny", "štvrtín"),
        [5] = new("pätina", "pätiny", "pätín"),
        [6] = new("šestina", "šestiny", "šestín"),
        [7] = new("sedmina", "sedminy", "sedmín"),
        [8] = new("osmina", "osminy", "osmín"),
        [9] = new("devätina", "devätiny", "devätín"),
        [10] = new("desatina", "desatiny", "desatín"),
        [11] = new("jedenástina", "jedenástiny", "jedenástin"),
        [12] = new("dvanástina", "dvanástiny", "dvanástin"),
        [13] = new("trinástina", "trinástiny", "trinástin"),
        [14] = new("štrnástina", "štrnástiny", "štrnástin"),
        [15] = new("pätnástina", "pätnástiny", "pätnástin"),
        [16] = new("šestnástina", "šestnástiny", "šestnástin"),
        [17] = new("sedemnástina", "sedemnástiny", "sedemnástin"),
        [18] = new("osemnástina", "osemnástiny", "osemnástin"),
        [19] = new("devätnástina", "devätnástiny", "devätnástin"),
        [20] = new("dvadsatina", "dvadsatiny", "dvadsatín"),
        [30] = new("tridsatina", "tridsatiny", "tridsatín"),
        [40] = new("štyridsatina", "štyridsatiny", "štyridsatín"),
        [50] = new("päťdesiatina", "päťdesiatiny", "päťdesiatin"),
        [60] = new("šesťdesiatina", "šesťdesiatiny", "šesťdesiatin"),
        [70] = new("sedemdesiatina", "sedemdesiatiny", "sedemdesiatin"),
        [80] = new("osemdesiatina", "osemdesiatiny", "osemdesiatin"),
        [90] = new("deväťdesiatina", "deväťdesiatiny", "deväťdesiatin"),
        [100] = new("stotina", "stotiny", "stotín"),
        [1000] = new("tisícina", "tisíciny", "tisícin"),
        [1_000_000] = new("milióntina", "milióntiny", "milióntin"),
        [1_000_000_000] = new("miliardtina", "miliardtiny", "miliardtin"),
    };

    // Instrumentals (Morfológia slovenského jazyka 1966: paradigm of päť, I. piatimi, for 5–99 except the compounds with
    // -jeden; I. štyrmi; Krátky slovník: dva I. dvoma, tri I. troma, genitives šiestich … deväťdesiatich).
    private static readonly string[] s_instrumentalUnits = ["", "", "dvoma", "troma", "štyrmi", "piatimi", "šiestimi", "siedmimi", "ôsmimi", "deviatimi"];
    private static readonly string[] s_instrumentalTeens =
        ["desiatimi", "jedenástimi", "dvanástimi", "trinástimi", "štrnástimi", "pätnástimi", "šestnástimi", "sedemnástimi", "osemnástimi", "devätnástimi"];
    private static readonly string[] s_instrumentalTens =
        ["", "", "dvadsiatimi", "tridsiatimi", "štyridsiatimi", "päťdesiatimi", "šesťdesiatimi", "sedemdesiatimi", "osemdesiatimi", "deväťdesiatimi"];

    /// <summary>
    /// Reads a fraction as Slovak does. A denominator with a fraction noun takes the noun in the form the numerator
    /// governs (<c>jedna tretina</c>, <c>dve tretiny</c>, <c>päť tretín</c>, Ondrus 1968, Morfológia: <i>dve šestiny a
    /// tri šestiny je päť šestín</i>); a compound numerator keeps its base form for every gender and governs the
    /// genitive plural (Jarošová, Slovenská reč 86, 2021: <i>dvadsaťdva žien</i>, also for 102, 122; Páleníková, JÚĽŠ,
    /// 2026: <i>dvadsaťjeden žien</i>, not <i>dvadsaťjedna</i>); <c>nula</c> governs the genitive plural as well
    /// (Morfológia: <i>nula stupňov</i>, <i>nula celých</i>). Any other denominator takes the mathematical reading of
    /// Ondrus (1968), §10: <c>lomené</c> + instrumental for a declinable numeral (<i>jedna lomené troma</i>, <i>sto
    /// delené piatimi</i>, Jarošová <i>dvadsiatimi dvoma</i>), <c>lomené číslom</c> + the absolute form for an
    /// indeclinable one (<i>jedna lomené číslom dvadsaťjeden</i>, <i>jedna lomené číslom sto</i>; <c>sto</c>, <c>tisíc</c>
    /// and their compounds, the compounds with <c>-jeden</c>), the numerator in its absolute form (<i>jedna</i>,
    /// <i>dva</i>). A denominator of one, a denominator from 10^6 without a noun and every case other than the nominative
    /// fail closed; <c>ku</c> reads a ratio, not a fraction.
    /// </summary>
    /// <inheritdoc />
    public bool TryGetFractionDenominator(BigInteger numerator, BigInteger denominator, IReadOnlyDictionary<string, string> variants,
        out FractionDenominatorReading? reading)
    {
        reading = null;
        if (denominator < 2 || !IsNominativeRequested(variants))
            return false;
        if (numerator.IsZero && denominator <= long.MaxValue && s_fractionNouns.TryGetValue((long)denominator, out var forms))
        {
            reading = new FractionDenominatorReading(forms.Many);
            return true;
        }
        if (!numerator.IsZero && TryBuildFractionNoun(numerator, denominator, s_fractionNouns, "mužský", out reading))
            return true;
        if (denominator > 999_999)
            return false;
        int value = (int)denominator;
        string denominatorText = value < 100 && InstrumentalBelowHundred(value, s_instrumentalUnits, s_instrumentalTeens, s_instrumentalTens) is string instrumental
            ? instrumental
            : "číslom " + Cardinal(value);
        reading = new FractionDenominatorReading("lomené " + denominatorText, NumeratorGender(numerator.IsOne ? "ženský" : "mužský"));
        return true;
    }

    private static readonly string[] s_cardinalUnits = ["", "jeden", "dva", "tri", "štyri", "päť", "šesť", "sedem", "osem", "deväť"];
    private static readonly string[] s_cardinalTeens =
        ["desať", "jedenásť", "dvanásť", "trinásť", "štrnásť", "pätnásť", "šestnásť", "sedemnásť", "osemnásť", "devätnásť"];
    private static readonly string[] s_cardinalTens = ["", "", "dvadsať", "tridsať", "štyridsať", "päťdesiat", "šesťdesiat", "sedemdesiat", "osemdesiat", "deväťdesiat"];

    /// <summary>
    /// Writes the absolute (masculine) cardinal of a number between 2 and 999 999 as the SK configuration does (PSP,
    /// Jarošová 2021): tens and units in one word (<c>dvadsaťdva</c>), a thousands multiplier joined to <c>tisíc</c>
    /// (<c>dvetisíc</c>, <c>stodvadsaťjedentisíc</c>), the hundreds apart from the rest of the last group
    /// (<c>sto dva</c>, <c>tisíc deväťsto deväťdesiatdeväť</c>).
    /// </summary>
    /// <param name="number">A number between 2 and 999 999.</param>
    /// <returns>The cardinal.</returns>
    private static string Cardinal(int number)
    {
        int thousands = number / 1000;
        string units = Group(number % 1000, joined: false);
        if (thousands == 0)
            return units;
        string head = thousands switch { 1 => "tisíc", 2 => "dvetisíc", _ => Group(thousands, joined: true) + "tisíc" };
        return units.Length == 0 ? head : head + " " + units;
    }

    /// <summary>Writes a group between 0 and 999, the hundreds joined to the rest or apart from it.</summary>
    /// <param name="group">The group value.</param>
    /// <param name="joined">Whether the hundreds are joined to the tens and units (in a thousands multiplier).</param>
    /// <returns>The group text (empty for 0).</returns>
    private static string Group(int group, bool joined)
    {
        int remainder = group % 100;
        string rest = remainder switch
        {
            0 => string.Empty,
            < 10 => s_cardinalUnits[remainder],
            < 20 => s_cardinalTeens[remainder - 10],
            _ => s_cardinalTens[remainder / 10] + s_cardinalUnits[remainder % 10],
        };
        string hundreds = s_cardinalHundreds[group / 100];
        if (hundreds.Length == 0 || rest.Length == 0 || joined)
            return hundreds + rest;
        return hundreds + " " + rest;
    }

    private static readonly string[] s_units = ["", "prvý", "druhý", "tretí", "štvrtý", "piaty", "šiesty", "siedmy", "ôsmy", "deviaty"];
    private static readonly string[] s_teens =
        ["desiaty", "jedenásty", "dvanásty", "trinásty", "štrnásty", "pätnásty", "šestnásty", "sedemnásty", "osemnásty", "devätnásty"];
    private static readonly string[] s_tens = ["", "", "dvadsiaty", "tridsiaty", "štyridsiaty", "päťdesiaty", "šesťdesiaty", "sedemdesiaty", "osemdesiaty", "deväťdesiaty"];
    private static readonly string[] s_cases = ["nominatív", "genitív", "datív", "akuzatív", "lokál", "inštrumentál"];

    // [gender][nominative, genitive, dative, accusative, locative, instrumental]; masculine accusative is inanimate.
    private static readonly string[][] s_hardLong =
    [
        ["ý", "ého", "ému", "ý", "om", "ým"],
        ["á", "ej", "ej", "ú", "ej", "ou"],
        ["é", "ého", "ému", "é", "om", "ým"],
    ];
    private static readonly string[][] s_hardShort =
    [
        ["y", "eho", "emu", "y", "om", "ym"],
        ["a", "ej", "ej", "u", "ej", "ou"],
        ["e", "eho", "emu", "e", "om", "ym"],
    ];
    private static readonly string[][] s_softLong =
    [
        ["í", "ieho", "iemu", "í", "om", "ím"],
        ["ia", "ej", "ej", "iu", "ej", "ou"],
        ["ie", "ieho", "iemu", "ie", "om", "ím"],
    ];

    // Soft paradigm shortened after a long syllable (rhythmic law): tisíci, tisíca, tisíce.
    private static readonly string[][] s_softShort =
    [
        ["i", "eho", "emu", "i", "om", "im"],
        ["a", "ej", "ej", "u", "ej", "ou"],
        ["e", "eho", "emu", "e", "om", "im"],
    ];

    /// <inheritdoc />
    protected override string FeminineValue => "ženský";

    /// <inheritdoc />
    protected override string NeuterValue => "stredný";

    /// <inheritdoc />
    protected override IReadOnlyList<string> CaseValues => s_cases;

    /// <inheritdoc />
    protected override string Decline(string citation, Gender gender, int caseIndex)
    {
        if (citation.EndsWith('ý'))
            return ReplaceEnding(citation, "ý", s_hardLong, gender, caseIndex);
        if (citation.EndsWith('y'))
            return ReplaceEnding(citation, "y", s_hardShort, gender, caseIndex);
        if (citation.EndsWith('í'))
            return ReplaceEnding(citation, "í", s_softLong, gender, caseIndex);
        return ReplaceEnding(citation, "i", s_softShort, gender, caseIndex);
    }

    /// <inheritdoc />
    protected override bool TryDecompose(int number, out string prefix, out IReadOnlyList<string> components)
    {
        // Slovak spelling rules (JÚĽŠ SAV, as summarized by teraz.sk "Ako písať jednoslovné a
        // viacslovné číslovky"): hundreds and thousands of a compound stay cardinal; when only units
        // follow them the whole ordinal is one word (stoprvý, dvetisícdruhý), otherwise the cardinal
        // part is separate (päťsto dvadsiaty ôsmy). Round hundreds are dvojstý, trojstý, ...
        prefix = string.Empty;
        var parts = new List<string>();
        components = parts;
        var (milliards, millions, thousands, units) = Groups(number);
        if (milliards > 0 || millions > 0)
        {
            if (number != 1_000_000)
                return false;
            parts.Add("miliónty");
            return true;
        }
        if (thousands > 9)
            return false;

        int hundreds = units / 100;
        int remainder = units % 100;
        if (remainder == 0)
        {
            if (hundreds == 0)
            {
                if (thousands != 1)
                    return false;
                parts.Add("tisíci");
                return true;
            }
            string round = s_roundHundreds[hundreds];
            parts.Add(thousands == 0 ? round : s_cardinalThousands[thousands] + round);
            return true;
        }

        string cardinal = s_cardinalThousands[thousands] + s_cardinalHundreds[hundreds];
        if (remainder < 10)
        {
            parts.Add(cardinal + s_units[remainder]);
            return true;
        }
        prefix = cardinal;
        if (remainder >= 20)
        {
            parts.Add(s_tens[remainder / 10]);
            if (remainder % 10 > 0)
                parts.Add(s_units[remainder % 10]);
        }
        else
            parts.Add(s_teens[remainder - 10]);
        return true;
    }

    private static readonly string[] s_roundHundreds = ["", "stý", "dvojstý", "trojstý", "štvorstý", "päťstý", "šesťstý", "sedemstý", "osemstý", "deväťstý"];
    private static readonly string[] s_cardinalHundreds = ["", "sto", "dvesto", "tristo", "štyristo", "päťsto", "šesťsto", "sedemsto", "osemsto", "deväťsto"];
    private static readonly string[] s_cardinalThousands = ["", "tisíc", "dvetisíc", "tritisíc", "štyritisíc", "päťtisíc", "šesťtisíc", "sedemtisíc", "osemtisíc", "deväťtisíc"];
}

/// <summary>
/// Produces Ukrainian ordinals. Only the last component of a compound is ordinal and follows the
/// cardinal of the preceding part (<c>двадцять перший</c>, <c>сто перший</c>,
/// <c>тисяча перший</c>); it is declined for gender (<c>чоловічий</c>, <c>жіночий</c>,
/// <c>середній</c>) and case with the hard (<c>-ий</c>) and soft (<c>-ій</c>) adjective
/// paradigms. Round thousands, millions and milliards are one word with the multiplier in the genitive
/// (<c>двохтисячний</c>, <c>шістдесятип'ятимільйонний</c>, <c>двохмільярдний</c>; NTS-28, Правопис 2019 §38) for
/// the multipliers 1–89 except a final <c>один</c> after the tens; the other round multipliers are declined.
/// </summary>
/// <remarks>The apostrophe is the ASCII one used by the Ukrainian cardinal configuration (<c>п'ять</c>).</remarks>
public sealed class UkrainianOrdinalLanguageSpecifics : SlavicAdjectivalOrdinalLanguageSpecifics, IFractionLanguageSpecifics
{
    /// <summary>
    /// Reads a fraction as Український правопис 2019, §107.1, does: the numerator in the feminine, the denominator the
    /// ordinal in the feminine nominative singular after one (<c>одна третя</c>, <c>одна четверта</c>) and in the
    /// genitive plural otherwise (<c>дві третіх</c>, <c>три четвертих</c>, <c>п'ять других</c>, <c>дев'ять десятих</c>).
    /// The denominator agrees as a noun with the implied <c>частина</c> (І. Літвінова, Українська мова 6 кл., 2022), so
    /// after a compound it agrees with the last component: singular after a compound ending in <c>одна</c>
    /// (<c>тридцять одна сота</c>: "якщо … числівник дробової частини … закінчується на одиницю, то весь запис треба
    /// ставити в називний відмінок"), genitive plural after one ending in two to four (<c>двадцять дві шістдесятих</c>,
    /// <c>тридцять чотири сорок третіх</c>, Літвінова). The denominator is any ordinal this class forms (<c>одна
    /// тисячна</c>, <c>дві двадцять п'ятих</c>). A zero numerator, a denominator of one and the cases other than the
    /// nominative (§107: the numerator is declined as a cardinal, <c>двом третім</c>) fail closed.
    /// </summary>
    /// <inheritdoc />
    public bool TryGetFractionDenominator(BigInteger numerator, BigInteger denominator, IReadOnlyDictionary<string, string> variants,
        out FractionDenominatorReading? reading)
    {
        reading = null;
        if (numerator.IsZero || denominator < 2 || denominator > int.MaxValue || !IsNominativeRequested(variants))
            return false;
        var numeratorClass = ClassifyFractionNumerator(numerator, compoundEndingInOneIsSingular: true);
        if (!TryDecompose((int)denominator, out string prefix, out IReadOnlyList<string> components))
            return false;
        string last = components[^1];
        string word = numeratorClass == FractionNumeratorClass.One ? Decline(last, Gender.Feminine, 0) : GenitivePlural(last);
        reading = new FractionDenominatorReading(prefix.Length == 0 ? word : prefix + " " + word, FeminineNumerator());
        return true;
    }

    /// <summary>The genitive plural of an ordinal (hard <c>-их</c>, soft <c>-іх</c>: <c>третіх</c>, <c>десятих</c>).</summary>
    /// <param name="citation">The masculine nominative form.</param>
    /// <returns>The genitive plural.</returns>
    private static string GenitivePlural(string citation)
        => citation.EndsWith("ій") ? citation[..^2] + "іх" : citation[..^2] + "их";

    private static readonly string[] s_cardinalUnits = ["", "один", "два", "три", "чотири", "п'ять", "шість", "сім", "вісім", "дев'ять"];
    private static readonly string[] s_cardinalUnitsFeminine = ["", "одна", "дві", "три", "чотири", "п'ять", "шість", "сім", "вісім", "дев'ять"];
    private static readonly string[] s_cardinalTeens =
        ["десять", "одинадцять", "дванадцять", "тринадцять", "чотирнадцять", "п'ятнадцять", "шістнадцять", "сімнадцять", "вісімнадцять", "дев'ятнадцять"];
    private static readonly string[] s_cardinalTens = ["", "", "двадцять", "тридцять", "сорок", "п'ятдесят", "шістдесят", "сімдесят", "вісімдесят", "дев'яносто"];
    private static readonly string[] s_cardinalHundreds = ["", "сто", "двісті", "триста", "чотириста", "п'ятсот", "шістсот", "сімсот", "вісімсот", "дев'ятсот"];

    private static readonly string[] s_units = ["", "перший", "другий", "третій", "четвертий", "п'ятий", "шостий", "сьомий", "восьмий", "дев'ятий"];
    private static readonly string[] s_teens =
        ["десятий", "одинадцятий", "дванадцятий", "тринадцятий", "чотирнадцятий", "п'ятнадцятий", "шістнадцятий", "сімнадцятий", "вісімнадцятий", "дев'ятнадцятий"];
    private static readonly string[] s_tens = ["", "", "двадцятий", "тридцятий", "сороковий", "п'ятдесятий", "шістдесятий", "сімдесятий", "вісімдесятий", "дев'яностий"];
    private static readonly string[] s_hundreds = ["", "сотий", "двохсотий", "трьохсотий", "чотирьохсотий", "п'ятисотий", "шестисотий", "семисотий", "восьмисотий", "дев'ятисотий"];
    /// <summary>Genitive combining forms of 0–10 before -тисячний/-мільйонний/-мільярдний (0 and 1 have none).</summary>
    private static readonly string[] s_thousandPrefixes = ["", "", "двох", "трьох", "чотирьох", "п'яти", "шести", "семи", "восьми", "дев'яти", "десяти"];

    /// <summary>Genitive combining forms of 11–19 (index = value − 10; index 0 unused).</summary>
    private static readonly string[] s_teenPrefixes =
        ["", "одинадцяти", "дванадцяти", "тринадцяти", "чотирнадцяти", "п'ятнадцяти", "шістнадцяти", "сімнадцяти", "вісімнадцяти", "дев'ятнадцяти"];

    /// <summary>Genitive combining forms of the round tens 20–80 (index = tens digit); 90 is not established.</summary>
    private static readonly string[] s_tensPrefixes = ["", "", "двадцяти", "тридцяти", "сорока", "п'ятдесяти", "шістдесяти", "сімдесяти", "вісімдесяти"];
    private static readonly string[] s_cases = ["називний", "родовий", "давальний", "знахідний", "орудний", "місцевий"];

    // [gender][nominative, genitive, dative, accusative, instrumental, locative]; masculine accusative is inanimate.
    private static readonly string[][] s_hard =
    [
        ["ий", "ого", "ому", "ий", "им", "ому"],
        ["а", "ої", "ій", "у", "ою", "ій"],
        ["е", "ого", "ому", "е", "им", "ому"],
    ];
    private static readonly string[][] s_soft =
    [
        ["ій", "ього", "ьому", "ій", "ім", "ьому"],
        ["я", "ьої", "ій", "ю", "ьою", "ій"],
        ["є", "ього", "ьому", "є", "ім", "ьому"],
    ];

    /// <inheritdoc />
    protected override string FeminineValue => "жіночий";

    /// <inheritdoc />
    protected override string NeuterValue => "середній";

    /// <inheritdoc />
    protected override IReadOnlyList<string> CaseValues => s_cases;

    /// <inheritdoc />
    protected override string Decline(string citation, Gender gender, int caseIndex)
        => citation.EndsWith("ій")
            ? ReplaceEnding(citation, "ій", s_soft, gender, caseIndex)
            : ReplaceEnding(citation, "ий", s_hard, gender, caseIndex);

    /// <inheritdoc />
    protected override bool TryDecompose(int number, out string prefix, out IReadOnlyList<string> components)
    {
        var (milliards, millions, thousands, units) = Groups(number);
        var words = new List<string>();
        string last;
        if (units > 0)
        {
            AppendCardinalChunk(words, milliards, "мільярд", "мільярди", "мільярдів", feminine: false);
            AppendCardinalChunk(words, millions, "мільйон", "мільйони", "мільйонів", feminine: false);
            AppendCardinalChunk(words, thousands, "тисяча", "тисячі", "тисяч", feminine: true);
            int remainder = units % 100;
            if (remainder == 0)
                last = s_hundreds[units / 100];
            else
            {
                if (units / 100 > 0)
                    words.Add(s_cardinalHundreds[units / 100]);
                if (remainder < 10)
                    last = s_units[remainder];
                else if (remainder < 20)
                    last = s_teens[remainder - 10];
                else if (remainder % 10 == 0)
                    last = s_tens[remainder / 10];
                else
                {
                    words.Add(s_cardinalTens[remainder / 10]);
                    last = s_units[remainder % 10];
                }
            }
        }
        else if (thousands > 0)
        {
            if (!TryCombining(thousands, out string combining))
            {
                prefix = string.Empty;
                components = [];
                return false;
            }
            AppendCardinalChunk(words, milliards, "мільярд", "мільярди", "мільярдів", feminine: false);
            AppendCardinalChunk(words, millions, "мільйон", "мільйони", "мільйонів", feminine: false);
            last = combining + "тисячний";
        }
        else if (millions > 0 && TryCombining(millions, out string millionsCombining))
        {
            AppendCardinalChunk(words, milliards, "мільярд", "мільярди", "мільярдів", feminine: false);
            last = millionsCombining + "мільйонний";
        }
        else if (millions == 0 && TryCombining(milliards, out string milliardsCombining))
            last = milliardsCombining + "мільярдний";
        else
        {
            prefix = string.Empty;
            components = [];
            return false;
        }

        prefix = string.Join(" ", words);
        components = [last];
        return true;
    }

    /// <summary>
    /// Writes the genitive combining form of the multiplier of a one-word round ordinal (Український правопис
    /// 2019, §38.1.2: двохтисячний, десятитисячний, п'ятдесятимільйонний, шістдесятип'ятимільйонний,
    /// семимільярдний). One gives the empty form (тисячний, мільйонний). Only the multipliers whose form the
    /// paragraph establishes are accepted: 1–89 except a final один after the tens (21, 31 …); 90 and above
    /// (дев'яноста or дев'яносто, and hundreds that §38 keeps in the nominative in п'ятсоттридцятитисячний)
    /// are declined.
    /// </summary>
    /// <param name="multiplier">The multiplier, 1–999.</param>
    /// <param name="combining">Receives the combining form.</param>
    /// <returns><see langword="true"/> when the multiplier is accepted.</returns>
    private static bool TryCombining(int multiplier, out string combining)
    {
        combining = string.Empty;
        if (multiplier == 1) return true;
        if (multiplier is < 2 or >= 90) return false;
        if (multiplier <= 10) { combining = s_thousandPrefixes[multiplier]; return true; }
        if (multiplier < 20) { combining = s_teenPrefixes[multiplier - 10]; return true; }
        int units = multiplier % 10;
        if (units == 1) return false;
        combining = s_tensPrefixes[multiplier / 10] + (units == 0 ? string.Empty : s_thousandPrefixes[units]);
        return true;
    }

    /// <summary>Appends the cardinal chunk of a scale group, choosing the scale noun by count.</summary>
    /// <param name="words">The target word list.</param>
    /// <param name="group">The group multiplier (0–999).</param>
    /// <param name="one">The scale noun after 1.</param>
    /// <param name="few">The scale noun after 2–4.</param>
    /// <param name="many">The scale noun after 0 and 5–20.</param>
    /// <param name="feminine">Whether the scale noun is feminine (тисяча); it selects the feminine units (одна, дві).</param>
    private static void AppendCardinalChunk(List<string> words, int group, string one, string few, string many, bool feminine)
    {
        if (group == 0)
            return;
        if (group == 1)
        {
            // A single thousand, million or milliard is the bare noun (Український правопис 2019, §38:
            // "мільйон трьохтисячний", "мільярд чотирьохмільйонний").
            words.Add(one);
            return;
        }
        int hundreds = group / 100;
        int remainder = group % 100;
        if (hundreds > 0)
            words.Add(s_cardinalHundreds[hundreds]);
        int lastDigit = remainder % 10;
        if (remainder >= 20)
        {
            words.Add(s_cardinalTens[remainder / 10]);
            if (lastDigit > 0)
                words.Add((feminine ? s_cardinalUnitsFeminine : s_cardinalUnits)[lastDigit]);
        }
        else if (remainder >= 10)
            words.Add(s_cardinalTeens[remainder - 10]);
        else if (remainder > 0)
            words.Add((feminine ? s_cardinalUnitsFeminine : s_cardinalUnits)[remainder]);
        bool teen = remainder is >= 10 and < 20;
        words.Add(!teen && lastDigit == 1 ? one : !teen && lastDigit is >= 2 and <= 4 ? few : many);
    }
}
