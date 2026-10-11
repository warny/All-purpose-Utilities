using System.Collections.Generic;

namespace Utils.NumberToString;

/// <summary>
/// Handles Polish ordinal conversion for numbers 20 and above, where compound forms
/// require both the tens and units components to carry ordinal endings — a transformation
/// the XML word-rule engine cannot perform because it only rewrites the last word.
/// Numbers 1–19 fall through to the XML pipeline (which carries full case/gender tables).
/// Since NTS-08 the plugin also builds 1000–1999; since NTS-28 it builds 2000–999 999 after PWN Poradnia
/// 16446: the thousands stay cardinal with their agreeing noun (<c>dwa tysiące piętnasty</c>) and a round
/// thousand with a multiplier of 2–99 is one word declined like <c>tysięczny</c>
/// (<c>dwudziestojedentysięczny</c>). The round thousands from 100 000 and the millions fail closed.
/// </summary>
public sealed class PolishOrdinalLanguageSpecifics : INumberToStringLanguageSpecifics, IOrdinalLanguageSpecifics
{
    // rodzaj index: 0=maskulin, 1=feminin, 2=nijaki, 3=plural_mos, 4=plural
    // przypadek index: 0=mianownik, 1=dopełniacz, 2=celownik, 3=biernik, 4=narzędnik, 5=miejscownik
    // Flat index = rodzaj * 6 + przypadek

    private static int RodzajIndex(string r) => r switch
    {
        "feminin"    => 1,
        "nijaki"     => 2,
        "plural_mos" => 3,
        "plural"     => 4,
        _            => 0
    };

    private static int PrzypadekIndex(string p) => p switch
    {
        "dopełniacz"  => 1,
        "celownik"    => 2,
        "biernik"     => 3,
        "narzędnik"   => 4,
        "miejscownik" => 5,
        _             => 0
    };

    // Units ordinal forms 1–9 (copied from XML to build compound ordinals).
    // Outer index = number - 1.  Inner = flat [rodzaj*6 + przypadek].
    private static readonly string[][] s_units =
    [
        // 1: pierwszy
        [
            "pierwszy","pierwszego","pierwszemu","pierwszy","pierwszym","pierwszym",
            "pierwsza","pierwszej","pierwszej","pierwszą","pierwszą","pierwszej",
            "pierwsze","pierwszego","pierwszemu","pierwsze","pierwszym","pierwszym",
            "pierwsi","pierwszych","pierwszym","pierwszych","pierwszymi","pierwszych",
            "pierwsze","pierwszych","pierwszym","pierwsze","pierwszymi","pierwszych",
        ],
        // 2: drugi
        [
            "drugi","drugiego","drugiemu","drugi","drugim","drugim",
            "druga","drugiej","drugiej","drugą","drugą","drugiej",
            "drugie","drugiego","drugiemu","drugie","drugim","drugim",
            "drudzy","drugich","drugim","drugich","drugimi","drugich",
            "drugie","drugich","drugim","drugie","drugimi","drugich",
        ],
        // 3: trzeci
        [
            "trzeci","trzeciego","trzeciemu","trzeci","trzecim","trzecim",
            "trzecia","trzeciej","trzeciej","trzecią","trzecią","trzeciej",
            "trzecie","trzeciego","trzeciemu","trzecie","trzecim","trzecim",
            "trzeci","trzecich","trzecim","trzecich","trzecimi","trzecich",
            "trzecie","trzecich","trzecim","trzecie","trzecimi","trzecich",
        ],
        // 4: czwarty
        [
            "czwarty","czwartego","czwartemu","czwarty","czwartym","czwartym",
            "czwarta","czwartej","czwartej","czwartą","czwartą","czwartej",
            "czwarte","czwartego","czwartemu","czwarte","czwartym","czwartym",
            "czwarci","czwartych","czwartym","czwartych","czwartymi","czwartych",
            "czwarte","czwartych","czwartym","czwarte","czwartymi","czwartych",
        ],
        // 5: piąty
        [
            "piąty","piątego","piątemu","piąty","piątym","piątym",
            "piąta","piątej","piątej","piątą","piątą","piątej",
            "piąte","piątego","piątemu","piąte","piątym","piątym",
            "piąci","piątych","piątym","piątych","piątymi","piątych",
            "piąte","piątych","piątym","piąte","piątymi","piątych",
        ],
        // 6: szósty
        [
            "szósty","szóstego","szóstemu","szósty","szóstym","szóstym",
            "szósta","szóstej","szóstej","szóstą","szóstą","szóstej",
            "szóste","szóstego","szóstemu","szóste","szóstym","szóstym",
            "szóści","szóstych","szóstym","szóstych","szóstymi","szóstych",
            "szóste","szóstych","szóstym","szóste","szóstymi","szóstych",
        ],
        // 7: siódmy
        [
            "siódmy","siódmego","siódmemu","siódmy","siódmym","siódmym",
            "siódma","siódmej","siódmej","siódmą","siódmą","siódmej",
            "siódme","siódmego","siódmemu","siódme","siódmym","siódmym",
            "siódmi","siódmych","siódmym","siódmych","siódmymi","siódmych",
            "siódme","siódmych","siódmym","siódme","siódmymi","siódmych",
        ],
        // 8: ósmy
        [
            "ósmy","ósmego","ósmemu","ósmy","ósmym","ósmym",
            "ósma","ósmej","ósmej","ósmą","ósmą","ósmej",
            "ósme","ósmego","ósmemu","ósme","ósmym","ósmym",
            "ósmi","ósmych","ósmym","ósmych","ósmymi","ósmych",
            "ósme","ósmych","ósmym","ósme","ósmymi","ósmych",
        ],
        // 9: dziewiąty
        [
            "dziewiąty","dziewiątego","dziewiątemu","dziewiąty","dziewiątym","dziewiątym",
            "dziewiąta","dziewiątej","dziewiątej","dziewiątą","dziewiątą","dziewiątej",
            "dziewiąte","dziewiątego","dziewiątemu","dziewiąte","dziewiątym","dziewiątym",
            "dziewiąci","dziewiątych","dziewiątym","dziewiątych","dziewiątymi","dziewiątych",
            "dziewiąte","dziewiątych","dziewiątym","dziewiąte","dziewiątymi","dziewiątych",
        ],
    ];

    // Tens ordinal forms: index 0=20th, 1=30th, …, 7=90th.
    // Hard-adjective -sty pattern; virile plural (plural_mos): -sty → -ści.
    private static readonly string[][] s_tens =
    [
        // 20: dwudziesty  (virile pl: dwudzieści)
        [
            "dwudziesty","dwudziestego","dwudziestemu","dwudziesty","dwudziestym","dwudziestym",
            "dwudziesta","dwudziestej","dwudziestej","dwudziestą","dwudziestą","dwudziestej",
            "dwudzieste","dwudziestego","dwudziestemu","dwudzieste","dwudziestym","dwudziestym",
            "dwudzieści","dwudziestych","dwudziestym","dwudziestych","dwudziestymi","dwudziestych",
            "dwudzieste","dwudziestych","dwudziestym","dwudzieste","dwudziestymi","dwudziestych",
        ],
        // 30: trzydziesty  (virile pl: trzydzieści)
        [
            "trzydziesty","trzydziestego","trzydziestemu","trzydziesty","trzydziestym","trzydziestym",
            "trzydziesta","trzydziestej","trzydziestej","trzydziestą","trzydziestą","trzydziestej",
            "trzydzieste","trzydziestego","trzydziestemu","trzydzieste","trzydziestym","trzydziestym",
            "trzydzieści","trzydziestych","trzydziestym","trzydziestych","trzydziestymi","trzydziestych",
            "trzydzieste","trzydziestych","trzydziestym","trzydzieste","trzydziestymi","trzydziestych",
        ],
        // 40: czterdziesty  (virile pl: czterdzieści)
        [
            "czterdziesty","czterdziestego","czterdziestemu","czterdziesty","czterdziestym","czterdziestym",
            "czterdziesta","czterdziestej","czterdziestej","czterdziestą","czterdziestą","czterdziestej",
            "czterdzieste","czterdziestego","czterdziestemu","czterdzieste","czterdziestym","czterdziestym",
            "czterdzieści","czterdziestych","czterdziestym","czterdziestych","czterdziestymi","czterdziestych",
            "czterdzieste","czterdziestych","czterdziestym","czterdzieste","czterdziestymi","czterdziestych",
        ],
        // 50: pięćdziesiąty  (virile pl: pięćdziesiąci)
        [
            "pięćdziesiąty","pięćdziesiątego","pięćdziesiątemu","pięćdziesiąty","pięćdziesiątym","pięćdziesiątym",
            "pięćdziesiąta","pięćdziesiątej","pięćdziesiątej","pięćdziesiątą","pięćdziesiątą","pięćdziesiątej",
            "pięćdziesiąte","pięćdziesiątego","pięćdziesiątemu","pięćdziesiąte","pięćdziesiątym","pięćdziesiątym",
            "pięćdziesiąci","pięćdziesiątych","pięćdziesiątym","pięćdziesiątych","pięćdziesiątymi","pięćdziesiątych",
            "pięćdziesiąte","pięćdziesiątych","pięćdziesiątym","pięćdziesiąte","pięćdziesiątymi","pięćdziesiątych",
        ],
        // 60: sześćdziesiąty  (virile pl: sześćdziesiąci)
        [
            "sześćdziesiąty","sześćdziesiątego","sześćdziesiątemu","sześćdziesiąty","sześćdziesiątym","sześćdziesiątym",
            "sześćdziesiąta","sześćdziesiątej","sześćdziesiątej","sześćdziesiątą","sześćdziesiątą","sześćdziesiątej",
            "sześćdziesiąte","sześćdziesiątego","sześćdziesiątemu","sześćdziesiąte","sześćdziesiątym","sześćdziesiątym",
            "sześćdziesiąci","sześćdziesiątych","sześćdziesiątym","sześćdziesiątych","sześćdziesiątymi","sześćdziesiątych",
            "sześćdziesiąte","sześćdziesiątych","sześćdziesiątym","sześćdziesiąte","sześćdziesiątymi","sześćdziesiątych",
        ],
        // 70: siedemdziesiąty  (virile pl: siedemdziesiąci)
        [
            "siedemdziesiąty","siedemdziesiątego","siedemdziesiątemu","siedemdziesiąty","siedemdziesiątym","siedemdziesiątym",
            "siedemdziesiąta","siedemdziesiątej","siedemdziesiątej","siedemdziesiątą","siedemdziesiątą","siedemdziesiątej",
            "siedemdziesiąte","siedemdziesiątego","siedemdziesiątemu","siedemdziesiąte","siedemdziesiątym","siedemdziesiątym",
            "siedemdziesiąci","siedemdziesiątych","siedemdziesiątym","siedemdziesiątych","siedemdziesiątymi","siedemdziesiątych",
            "siedemdziesiąte","siedemdziesiątych","siedemdziesiątym","siedemdziesiąte","siedemdziesiątymi","siedemdziesiątych",
        ],
        // 80: osiemdziesiąty  (virile pl: osiemdziesiąci)
        [
            "osiemdziesiąty","osiemdziesiątego","osiemdziesiątemu","osiemdziesiąty","osiemdziesiątym","osiemdziesiątym",
            "osiemdziesiąta","osiemdziesiątej","osiemdziesiątej","osiemdziesiątą","osiemdziesiątą","osiemdziesiątej",
            "osiemdziesiąte","osiemdziesiątego","osiemdziesiątemu","osiemdziesiąte","osiemdziesiątym","osiemdziesiątym",
            "osiemdziesiąci","osiemdziesiątych","osiemdziesiątym","osiemdziesiątych","osiemdziesiątymi","osiemdziesiątych",
            "osiemdziesiąte","osiemdziesiątych","osiemdziesiątym","osiemdziesiąte","osiemdziesiątymi","osiemdziesiątych",
        ],
        // 90: dziewięćdziesiąty  (virile pl: dziewięćdziesiąci)
        [
            "dziewięćdziesiąty","dziewięćdziesiątego","dziewięćdziesiątemu","dziewięćdziesiąty","dziewięćdziesiątym","dziewięćdziesiątym",
            "dziewięćdziesiąta","dziewięćdziesiątej","dziewięćdziesiątej","dziewięćdziesiątą","dziewięćdziesiątą","dziewięćdziesiątej",
            "dziewięćdziesiąte","dziewięćdziesiątego","dziewięćdziesiątemu","dziewięćdziesiąte","dziewięćdziesiątym","dziewięćdziesiątym",
            "dziewięćdziesiąci","dziewięćdziesiątych","dziewięćdziesiątym","dziewięćdziesiątych","dziewięćdziesiątymi","dziewięćdziesiątych",
            "dziewięćdziesiąte","dziewięćdziesiątych","dziewięćdziesiątym","dziewięćdziesiąte","dziewięćdziesiątymi","dziewięćdziesiątych",
        ],
    ];

    // Hundreds ordinal forms: index 0=100th, 1=200th, …, 8=900th.
    // Adjective -ny pattern; virile plural: -ny → -ni.
    private static readonly string[][] s_hundreds =
    [
        // 100: setny  (virile pl: setni)
        [
            "setny","setnego","setnemu","setny","setnym","setnym",
            "setna","setnej","setnej","setną","setną","setnej",
            "setne","setnego","setnemu","setne","setnym","setnym",
            "setni","setnych","setnym","setnych","setnymi","setnych",
            "setne","setnych","setnym","setne","setnymi","setnych",
        ],
        // 200: dwusetny
        [
            "dwusetny","dwusetnego","dwusetnemu","dwusetny","dwusetnym","dwusetnym",
            "dwusetna","dwusetnej","dwusetnej","dwusetną","dwusetną","dwusetnej",
            "dwusetne","dwusetnego","dwusetnemu","dwusetne","dwusetnym","dwusetnym",
            "dwusetni","dwusetnych","dwusetnym","dwusetnych","dwusetnymi","dwusetnych",
            "dwusetne","dwusetnych","dwusetnym","dwusetne","dwusetnymi","dwusetnych",
        ],
        // 300: trzechsetny
        [
            "trzechsetny","trzechsetnego","trzechsetnemu","trzechsetny","trzechsetnym","trzechsetnym",
            "trzechsetna","trzechsetnej","trzechsetnej","trzechsetną","trzechsetną","trzechsetnej",
            "trzechsetne","trzechsetnego","trzechsetnemu","trzechsetne","trzechsetnym","trzechsetnym",
            "trzechsetni","trzechsetnych","trzechsetnym","trzechsetnych","trzechsetnymi","trzechsetnych",
            "trzechsetne","trzechsetnych","trzechsetnym","trzechsetne","trzechsetnymi","trzechsetnych",
        ],
        // 400: czterechsetny (NTS-32: WSJP PAN, hasło 39657, full declension; SJP PWN; SJP PWN has no czterosetny)
        [
            "czterechsetny","czterechsetnego","czterechsetnemu","czterechsetny","czterechsetnym","czterechsetnym",
            "czterechsetna","czterechsetnej","czterechsetnej","czterechsetną","czterechsetną","czterechsetnej",
            "czterechsetne","czterechsetnego","czterechsetnemu","czterechsetne","czterechsetnym","czterechsetnym",
            "czterechsetni","czterechsetnych","czterechsetnym","czterechsetnych","czterechsetnymi","czterechsetnych",
            "czterechsetne","czterechsetnych","czterechsetnym","czterechsetne","czterechsetnymi","czterechsetnych",
        ],
        // 500: pięćsetny
        [
            "pięćsetny","pięćsetnego","pięćsetnemu","pięćsetny","pięćsetnym","pięćsetnym",
            "pięćsetna","pięćsetnej","pięćsetnej","pięćsetną","pięćsetną","pięćsetnej",
            "pięćsetne","pięćsetnego","pięćsetnemu","pięćsetne","pięćsetnym","pięćsetnym",
            "pięćsetni","pięćsetnych","pięćsetnym","pięćsetnych","pięćsetnymi","pięćsetnych",
            "pięćsetne","pięćsetnych","pięćsetnym","pięćsetne","pięćsetnymi","pięćsetnych",
        ],
        // 600: sześćsetny
        [
            "sześćsetny","sześćsetnego","sześćsetnemu","sześćsetny","sześćsetnym","sześćsetnym",
            "sześćsetna","sześćsetnej","sześćsetnej","sześćsetną","sześćsetną","sześćsetnej",
            "sześćsetne","sześćsetnego","sześćsetnemu","sześćsetne","sześćsetnym","sześćsetnym",
            "sześćsetni","sześćsetnych","sześćsetnym","sześćsetnych","sześćsetnymi","sześćsetnych",
            "sześćsetne","sześćsetnych","sześćsetnym","sześćsetne","sześćsetnymi","sześćsetnych",
        ],
        // 700: siedemsetny
        [
            "siedemsetny","siedemsetnego","siedemsetnemu","siedemsetny","siedemsetnym","siedemsetnym",
            "siedemsetna","siedemsetnej","siedemsetnej","siedemsetną","siedemsetną","siedemsetnej",
            "siedemsetne","siedemsetnego","siedemsetnemu","siedemsetne","siedemsetnym","siedemsetnym",
            "siedemsetni","siedemsetnych","siedemsetnym","siedemsetnych","siedemsetnymi","siedemsetnych",
            "siedemsetne","siedemsetnych","siedemsetnym","siedemsetne","siedemsetnymi","siedemsetnych",
        ],
        // 800: osiemsetny
        [
            "osiemsetny","osiemsetnego","osiemsetnemu","osiemsetny","osiemsetnym","osiemsetnym",
            "osiemsetna","osiemsetnej","osiemsetnej","osiemsetną","osiemsetną","osiemsetnej",
            "osiemsetne","osiemsetnego","osiemsetnemu","osiemsetne","osiemsetnym","osiemsetnym",
            "osiemsetni","osiemsetnych","osiemsetnym","osiemsetnych","osiemsetnymi","osiemsetnych",
            "osiemsetne","osiemsetnych","osiemsetnym","osiemsetne","osiemsetnymi","osiemsetnych",
        ],
        // 900: dziewięćsetny
        [
            "dziewięćsetny","dziewięćsetnego","dziewięćsetnemu","dziewięćsetny","dziewięćsetnym","dziewięćsetnym",
            "dziewięćsetna","dziewięćsetnej","dziewięćsetnej","dziewięćsetną","dziewięćsetną","dziewięćsetnej",
            "dziewięćsetne","dziewięćsetnego","dziewięćsetnemu","dziewięćsetne","dziewięćsetnym","dziewięćsetnym",
            "dziewięćsetni","dziewięćsetnych","dziewięćsetnym","dziewięćsetnych","dziewięćsetnymi","dziewięćsetnych",
            "dziewięćsetne","dziewięćsetnych","dziewięćsetnym","dziewięćsetne","dziewięćsetnymi","dziewięćsetnych",
        ],
    ];

    // Cardinal hundreds (used as non-ordinal prefix in compound ordinals: "sto pierwszy").
    private static readonly string[] s_cardinalHundreds =
        ["sto", "dwieście", "trzysta", "czterysta", "pięćset", "sześćset", "siedemset", "osiemset", "dziewięćset"];

    /// <summary>Cardinal units 1–9 (masculine non-personal form, used before tysiące/tysięcy).</summary>
    private static readonly string[] s_cardinalUnits = ["", "jeden", "dwa", "trzy", "cztery", "pięć", "sześć", "siedem", "osiem", "dziewięć"];

    /// <summary>Cardinal 10–19.</summary>
    private static readonly string[] s_cardinalTeens =
        ["dziesięć", "jedenaście", "dwanaście", "trzynaście", "czternaście", "piętnaście", "szesnaście", "siedemnaście", "osiemnaście", "dziewiętnaście"];

    /// <summary>Cardinal round tens (index = tens digit).</summary>
    private static readonly string[] s_cardinalTens =
        ["", "", "dwadzieścia", "trzydzieści", "czterdzieści", "pięćdziesiąt", "sześćdziesiąt", "siedemdziesiąt", "osiemdziesiąt", "dziewięćdziesiąt"];

    /// <summary>
    /// Combining forms of 2–19 before <c>tysięczny</c> (index = value): PWN Poradnia 16446
    /// (<c>dziewiętnastotysięczny</c>, <c>dwudziestodwutysięczny</c>).
    /// </summary>
    private static readonly string?[] s_combiningUnitsAndTeens =
    [
        null, null, "dwu", "trzy", "cztero", "pięcio", "sześcio", "siedmio", "ośmio", "dziewięcio",
        "dziesięcio", "jedenasto", "dwunasto", "trzynasto", "czternasto", "piętnasto", "szesnasto", "siedemnasto", "osiemnasto", "dziewiętnasto",
    ];

    /// <summary>Combining forms of the round tens before <c>tysięczny</c> (index = tens digit): <c>dwudziesto</c>.</summary>
    private static readonly string?[] s_combiningTens =
        [null, null, "dwudziesto", "trzydziesto", "czterdziesto", "pięćdziesięcio", "sześćdziesięcio", "siedemdziesięcio", "osiemdziesięcio", "dziewięćdziesięcio"];

    /// <summary>Stems of the ordinals 10–19, declined like <c>piąty</c> (index = value − 10).</summary>
    private static readonly string[] s_teenStems =
        ["dziesią", "jedenas", "dwunas", "trzynas", "czternas", "piętnas", "szesnas", "siedemnas", "osiemnas", "dziewiętnas"];

    /// <summary>
    /// Virile plural nominatives of the ordinals 10–19 (index = value − 10), as configured in the XML: the consonant
    /// alternates before <c>-i</c> (<c>piętnasty</c> → <c>piętnaści</c>), so they are not the stem plus the ending of
    /// <c>piąci</c> (which would give <c>piętnasci</c>).
    /// </summary>
    private static readonly string[] s_teenVirileNominatives =
        ["dziesiąci", "jedenaści", "dwunaści", "trzynaści", "czternaści", "piętnaści", "szesnaści", "siedemnaści", "osiemnaści", "dziewiętnaści"];

    /// <inheritdoc />
    public string FinalizeWriting(string languageIdentifier, string text) => text;

    /// <inheritdoc />
    /// <remarks>Values above <see cref="int"/> are outside the validated domain (1–999 999) and fail closed.</remarks>
    public bool TryConvertOrdinal(long number, IReadOnlyDictionary<string, string> activeVariants, out string? result)
    {
        if (number > int.MaxValue)
            throw new System.NotSupportedException(
                $"Polish ordinal {number} is not supported: the validated ordinal domain is 1–999999 except the round thousands from 100000.");
        return TryConvertOrdinal((int)number, activeVariants, out result);
    }

    /// <inheritdoc />
    public bool TryConvertOrdinal(int number, IReadOnlyDictionary<string, string> activeVariants, out string? result)
    {
        // NTS-28: 2000-999 999, except the round thousands from 100 000 (PWN Poradnia 16446 decomposes them
        // in an unconfirmed way); the million is outside the cardinal domain.
        if (number >= 1_000_000 || number >= 100_000 && number % 1000 == 0)
            throw new System.NotSupportedException(
                $"Polish ordinal {number} is not supported: the validated ordinal domain is 1–999999 except the round thousands from 100000.");
        if (number < 20) { result = null; return false; }

        int ri = RodzajIndex(activeVariants.TryGetValue("rodzaj", out var r) ? r : "maskulin");
        int pi = PrzypadekIndex(activeVariants.TryGetValue("przypadek", out var p) ? p : "mianownik");

        result = number >= 2000 ? GetThousandsForm(number, ri, pi) : GetForm(number, ri, pi);
        return result is not null;
    }

    /// <summary>
    /// Builds an ordinal from 2000 to 999 999 (NTS-28, PWN Poradnia 16446). A round thousand is one word, the
    /// combining form of its multiplier (2–99) before <c>tysięczny</c>; otherwise the thousands stay cardinal
    /// with their agreeing noun and the rest is ordinal as below 1000 (<c>dwa tysiące piętnasty</c>). A rest
    /// whose hundreds precede 11–19 has no form here and goes to the XML pipeline, whose last-word rules give
    /// the same text on the NTS-27 cardinal (<c>dwa tysiące sto piętnasty</c>).
    /// </summary>
    /// <param name="number">The value, 2000–999 999 (round thousands below 100 000 only).</param>
    /// <param name="ri">The gender (rodzaj) index.</param>
    /// <param name="pi">The case (przypadek) index.</param>
    /// <returns>The ordinal, or <see langword="null"/> to let the XML pipeline form it.</returns>
    private static string? GetThousandsForm(int number, int ri, int pi)
    {
        int thousands = number / 1000;
        int lower = number % 1000;
        if (lower == 0)
            return CombiningForm(thousands) + ThousandForm(ri, pi);
        string? lowerOrdinal = lower < 20 ? LowOrdinal(lower, ri, pi) : GetForm(lower, ri, pi);
        return lowerOrdinal is null ? null : ThousandsCardinal(thousands) + " " + lowerOrdinal;
    }

    /// <summary>
    /// Returns the ordinal of 1–19 for the requested form. Alone these values are formed by the XML pipeline;
    /// after the thousands the plugin declines them itself: 1–9 from the unit table, 10–19 like the hard
    /// adjective <c>piąty</c> (<c>dziesiąty</c>, <c>piętnastego</c>), except the virile plural nominative, whose
    /// consonant alternates and which is taken whole (<c>piętnaści</c>).
    /// </summary>
    /// <param name="value">The value, 1–19.</param>
    /// <param name="ri">The gender (rodzaj) index.</param>
    /// <param name="pi">The case (przypadek) index.</param>
    /// <returns>The declined ordinal.</returns>
    private static string LowOrdinal(int value, int ri, int pi)
    {
        if (value < 10) return s_units[value - 1][ri * 6 + pi];
        const int virilePlural = 3, nominative = 0;
        if (ri == virilePlural && pi == nominative) return s_teenVirileNominatives[value - 10];
        // Every other form of "piąty" starts with "pią" and ends with the hard-adjective ending the teens share.
        return s_teenStems[value - 10] + s_units[4][ri * 6 + pi][3..];
    }

    /// <summary>Writes the combining form of a multiplier of 2–99 before <c>tysięczny</c>.</summary>
    /// <param name="multiplier">The multiplier, 2–99.</param>
    /// <returns>The combining form (e.g. <c>dwudziestojeden</c>, <c>pięcio</c>).</returns>
    private static string CombiningForm(int multiplier)
    {
        if (multiplier < 20) return s_combiningUnitsAndTeens[multiplier]!;
        int units = multiplier % 10;
        // PWN Poradnia 16446: dwudziestojedentysięczny, dwudziestodwutysięczny.
        string unitsForm = units switch { 0 => string.Empty, 1 => "jeden", _ => s_combiningUnitsAndTeens[units]! };
        return s_combiningTens[multiplier / 10] + unitsForm;
    }

    /// <summary>Writes the cardinal of a thousands group with its agreeing noun (dwa tysiące, pięć tysięcy).</summary>
    /// <param name="thousands">The thousands multiplier, 2–999.</param>
    /// <returns>The cardinal words.</returns>
    private static string ThousandsCardinal(int thousands)
    {
        var words = new List<string>();
        if (thousands / 100 > 0) words.Add(s_cardinalHundreds[thousands / 100 - 1]);
        int rest = thousands % 100;
        if (rest is >= 10 and < 20) words.Add(s_cardinalTeens[rest - 10]);
        else
        {
            if (rest >= 20) words.Add(s_cardinalTens[rest / 10]);
            if (rest % 10 > 0) words.Add(s_cardinalUnits[rest % 10]);
        }
        // Unicode CLDR pl / SJP PWN (NTS-27): tysiące after 2-4 except 12-14, tysięcy otherwise (never tysiąc).
        int last = rest % 10;
        bool few = last is >= 2 and <= 4 && rest is (< 12 or > 14);
        words.Add(few ? "tysiące" : "tysięcy");
        return string.Join(" ", words);
    }

    private static string? GetForm(int number, int ri, int pi)
    {
        if (number is >= 1 and <= 9)
            return s_units[number - 1][ri * 6 + pi];

        if (number < 20)
            return null; // 11-19: XML handles

        if (number < 100)
        {
            int tensIdx = number / 10 - 2; // 20→0, 30→1, …, 90→7
            int units   = number % 10;
            string tensForm = s_tens[tensIdx][ri * 6 + pi];
            if (units == 0) return tensForm;
            return tensForm + " " + s_units[units - 1][ri * 6 + pi];
        }

        if (number < 1000)
        {
            int hundredsIdx = number / 100 - 1; // 100→0, …, 900→8
            int lower       = number % 100;
            if (lower == 0) return s_hundreds[hundredsIdx][ri * 6 + pi];
            // Hundreds stays cardinal, remainder takes full ordinal.
            string cardinal = s_cardinalHundreds[hundredsIdx];
            string lowerOrdinal = GetForm(lower, ri, pi);
            return lowerOrdinal is null ? null : cardinal + " " + lowerOrdinal;
        }

        if (number < 2000)
        {
            // NTS-08, ZPE "Odmiana liczebnika i zaimka": "tysięczny", "tysiąc osiemset pierwszy",
            // "tysiąc osiemsetny", "tysiąc pięćset dwudziesty piąty" — the thousand stays cardinal and the
            // rest is ordinal as below 1000; 1000 itself declines like "setny".
            int lower = number - 1000;
            if (lower == 0) return ThousandForm(ri, pi);
            string? lowerOrdinal = GetForm(lower, ri, pi);
            return lowerOrdinal is null ? null : "tysiąc " + lowerOrdinal;
        }

        return null;
    }

    /// <summary>Declines the ordinal of one thousand (<c>tysięczny</c>) like <c>setny</c>.</summary>
    /// <param name="ri">The gender (rodzaj) index.</param>
    /// <param name="pi">The case (przypadek) index.</param>
    /// <returns>The declined form of <c>tysięczny</c>.</returns>
    private static string ThousandForm(int ri, int pi)
    {
        // Every form of "setny" starts with "set"; the ordinal of a thousand shares its adjectival endings.
        string hundredForm = s_hundreds[0][ri * 6 + pi];
        return "tysięcz" + hundredForm[3..];
    }
}
