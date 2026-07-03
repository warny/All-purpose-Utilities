using System.Collections.Generic;

namespace Utils.NumberToString;

/// <summary>
/// Handles Polish ordinal conversion for numbers 20 and above, where compound forms
/// require both the tens and units components to carry ordinal endings — a transformation
/// the XML word-rule engine cannot perform because it only rewrites the last word.
/// Numbers 1–19 fall through to the XML pipeline (which carries full case/gender tables).
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
        // 400: czterosetny
        [
            "czterosetny","czterosetnego","czterosetnemu","czterosetny","czterosetnym","czterosetnym",
            "czterosetna","czterosetnej","czterosetnej","czterosetną","czterosetną","czterosetnej",
            "czterosetne","czterosetnego","czterosetnemu","czterosetne","czterosetnym","czterosetnym",
            "czterosetni","czterosetnych","czterosetnym","czterosetnych","czterosetnymi","czterosetnych",
            "czterosetne","czterosetnych","czterosetnym","czterosetne","czterosetnymi","czterosetnych",
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

    /// <inheritdoc />
    public string FinalizeWriting(string languageIdentifier, string text) => text;

    /// <inheritdoc />
    public bool TryConvertOrdinal(int number, IReadOnlyDictionary<string, string> activeVariants, out string? result)
    {
        if (number < 20) { result = null; return false; }

        int ri = RodzajIndex(activeVariants.TryGetValue("rodzaj", out var r) ? r : "maskulin");
        int pi = PrzypadekIndex(activeVariants.TryGetValue("przypadek", out var p) ? p : "mianownik");

        result = GetForm(number, ri, pi);
        return result is not null;
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

        // 1000+: fall through to XML (tysięczny etc. covered by word rules).
        return null;
    }
}
