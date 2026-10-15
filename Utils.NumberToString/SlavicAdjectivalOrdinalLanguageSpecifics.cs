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
    /// The agreement class of a fraction numerator with its denominator noun or adjective (NTS-30H): exactly one, two
    /// to four, or another value. A compound numerator ending in a unit whose agreement after the tens is not sourced
    /// for fractions (<see cref="FractionNumeratorClass.Unsupported"/>) fails closed.
    /// </summary>
    private protected enum FractionNumeratorClass
    {
        /// <summary>The numerator is exactly one.</summary>
        One,
        /// <summary>The numerator is two, three or four.</summary>
        Few,
        /// <summary>Any other supported numerator (five to twenty, round tens, hundreds, …).</summary>
        Many,
        /// <summary>A compound numerator whose agreement is not sourced (21, 22, 101 …): no reading.</summary>
        Unsupported,
    }

    /// <summary>
    /// Classifies a fraction numerator. A numerator above twenty ending in one of <paramref name="unsourcedFinalUnits"/>
    /// (and not in 11–19) is <see cref="FractionNumeratorClass.Unsupported"/>.
    /// </summary>
    /// <param name="numerator">The absolute numerator, at least one.</param>
    /// <param name="unsourcedFinalUnits">The final units whose compound agreement is not sourced.</param>
    /// <returns>The class.</returns>
    private protected static FractionNumeratorClass ClassifyFractionNumerator(BigInteger numerator, int[] unsourcedFinalUnits)
    {
        if (numerator.IsOne) return FractionNumeratorClass.One;
        if (numerator <= 4) return FractionNumeratorClass.Few;
        if (numerator <= 20) return FractionNumeratorClass.Many;
        int lastTwo = (int)(numerator % 100);
        int unit = lastTwo % 10;
        bool teen = lastTwo is >= 11 and <= 19;
        return !teen && System.Array.IndexOf(unsourcedFinalUnits, unit) >= 0 ? FractionNumeratorClass.Unsupported : FractionNumeratorClass.Many;
    }

    /// <summary>Determines whether the caller's variants ask for the nominative, the only case a fraction reading supports.</summary>
    /// <param name="variants">The caller's effective variant query.</param>
    /// <returns><see langword="true"/> when no other case is requested.</returns>
    private protected bool IsNominativeRequested(IReadOnlyDictionary<string, string> variants)
        => !variants.TryGetValue(CaseDimension, out string? caseValue) || caseValue == CaseValues[0];

    /// <summary>The feminine gender forced on a fraction numerator (the fraction words are feminine).</summary>
    /// <returns>The forced variant set.</returns>
    private protected ForcedVariantSet FeminineNumerator() => ForcedVariantSet.Create((GenderDimension, FeminineValue));

    /// <summary>
    /// The forms of one fraction noun (Czech <c>třetina</c>, Slovak <c>tretina</c>) after a numerator of one (nominative
    /// singular), of two to four (nominative plural) and of five and more (genitive plural); a form not established by
    /// the source is <see langword="null"/> and makes the numerators of that class fail closed.
    /// </summary>
    /// <param name="One">The nominative singular.</param>
    /// <param name="Few">The nominative plural, or <see langword="null"/>.</param>
    /// <param name="Many">The genitive plural, or <see langword="null"/>.</param>
    private protected readonly record struct FractionNounForms(string One, string? Few, string? Many);

    /// <summary>
    /// Builds the reading of a fraction whose denominator is a fraction noun of <paramref name="lexicon"/>: the noun in
    /// the form the numerator governs, the numerator forced to the feminine. Compound numerators ending in one to four
    /// fail closed, as do denominators absent from the lexicon, other cases than the nominative, and zero numerators.
    /// </summary>
    /// <param name="numerator">The absolute numerator.</param>
    /// <param name="denominator">The denominator.</param>
    /// <param name="variants">The caller's effective variant query.</param>
    /// <param name="lexicon">The sourced fraction nouns by denominator.</param>
    /// <param name="reading">Receives the reading.</param>
    /// <returns><see langword="true"/> when the fraction has a sourced reading.</returns>
    private protected bool TryBuildFractionNoun(BigInteger numerator, BigInteger denominator, IReadOnlyDictionary<string, string> variants,
        IReadOnlyDictionary<long, FractionNounForms> lexicon, out FractionDenominatorReading? reading)
    {
        reading = null;
        if (numerator.IsZero || denominator > long.MaxValue || !IsNominativeRequested(variants)
            || !lexicon.TryGetValue((long)denominator, out var forms))
            return false;
        string? word = ClassifyFractionNumerator(numerator, [1, 2, 3, 4]) switch
        {
            FractionNumeratorClass.One => forms.One,
            FractionNumeratorClass.Few => forms.Few,
            FractionNumeratorClass.Many => forms.Many,
            _ => null,
        };
        if (word == null) return false;
        reading = new FractionDenominatorReading(word, FeminineNumerator());
        return true;
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
    /// The fraction nouns established by the Internetová jazyková příručka (ÚJČ AV ČR) with their paradigm (NTS-30H):
    /// 1 → nominative singular, 2–4 → nominative plural, 5+ → genitive plural (jedna třetina, dvě třetiny, pět třetin).
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
        [100] = new("setina", "setiny", "setin"),
        [1000] = new("tisícina", "tisíciny", "tisícin"),
        [1_000_000] = new("miliontina", "miliontiny", "miliontin"),
    };

    /// <summary>
    /// Reads a fraction with its fraction noun (<c>jedna třetina</c>, <c>dvě třetiny</c>, <c>pět sedmin</c>,
    /// <c>jedna tisícina</c>): the ordinary reading of the IJP. The productive mathematical reading <c>lomeno</c> +
    /// instrumental cardinal (IJP <c>lomit</c>: <c>dvě lomeno třemi</c>) needs a cardinal instrumental the configuration does not
    /// have; denominators without an established noun therefore fail closed.
    /// </summary>
    /// <inheritdoc />
    public bool TryGetFractionDenominator(BigInteger numerator, BigInteger denominator, IReadOnlyDictionary<string, string> variants,
        out FractionDenominatorReading? reading)
        => TryBuildFractionNoun(numerator, denominator, variants, s_fractionNouns, out reading);

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
    /// The fraction nouns established for Slovak (NTS-30H): P. Ondrus, Slovenská reč 33 (1968), 40–41, gives the
    /// ordinary readings <c>jedna polovica</c>, <c>jedna tretina</c>, <c>pätina</c>, <c>jedna stotina</c>,
    /// <c>jedna tisícina</c>; the Krátky slovník slovenského jazyka and the Pravidlá slovenského pravopisu give the
    /// paradigm of <c>tretina -y -tín</c>. A plural not established by these sources is left <see langword="null"/>.
    /// </summary>
    private static readonly Dictionary<long, FractionNounForms> s_fractionNouns = new()
    {
        [2] = new("polovica", null, null),
        [3] = new("tretina", "tretiny", "tretín"),
        [5] = new("pätina", null, null),
        [100] = new("stotina", null, null),
        [1000] = new("tisícina", null, null),
    };

    /// <summary>
    /// Reads a fraction with its fraction noun (<c>jedna tretina</c>, <c>dve tretiny</c>, <c>päť tretín</c>), the
    /// everyday and arithmetic reading of Ondrus (1968). The specialist reading <c>lomené</c> + instrumental cardinal
    /// (<c>jedna lomené troma</c>) needs a cardinal instrumental the configuration does not have, and <c>ku</c> reads
    /// a ratio (<c>dvadsaťsedem má sa ku deviatim</c>), not a fraction; unestablished forms fail closed.
    /// </summary>
    /// <inheritdoc />
    public bool TryGetFractionDenominator(BigInteger numerator, BigInteger denominator, IReadOnlyDictionary<string, string> variants,
        out FractionDenominatorReading? reading)
        => TryBuildFractionNoun(numerator, denominator, variants, s_fractionNouns, out reading);

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
    /// The denominator is any ordinal this class forms (<c>одна тисячна</c>, <c>дві двадцять п'ятих</c>); compound
    /// numerators ending in <c>один</c> after the tens (21, 101) and other cases than the nominative fail closed.
    /// </summary>
    /// <inheritdoc />
    public bool TryGetFractionDenominator(BigInteger numerator, BigInteger denominator, IReadOnlyDictionary<string, string> variants,
        out FractionDenominatorReading? reading)
    {
        reading = null;
        if (numerator.IsZero || denominator < 2 || denominator > int.MaxValue || !IsNominativeRequested(variants))
            return false;
        var numeratorClass = ClassifyFractionNumerator(numerator, [1]);
        if (numeratorClass == FractionNumeratorClass.Unsupported
            || !TryDecompose((int)denominator, out string prefix, out IReadOnlyList<string> components))
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
