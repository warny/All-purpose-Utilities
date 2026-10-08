using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-25 (part A) guards for the localized Conway-Wechsler scales. RU, BG and UK use the Cyrillic Conway tables of
/// SCALE-SHORT-CYRILLIC (short scale with a static milliard), and SW and TR (whose tables NTS-25B localized) split their
/// former suffix into a junction (groupSeparator) and a suffix so that multi-group names keep the junction. The
/// higher names are a project-defined productive extension of the mi/bi/tri family each language has adopted, so
/// the tests check the mechanism and the consistency of the transliteration, not individually attested forms.
/// </summary>
[TestClass]
public class NumberToStringNts25Tests
{
    /// <summary>Matches any ASCII Latin letter.</summary>
    private static readonly Regex AsciiLatinLetter = new("[A-Za-z]", RegexOptions.CultureInvariant);

    /// <summary>Returns 10 raised to <paramref name="exponent"/>.</summary>
    private static BigInteger Pow10(int exponent) => BigInteger.Pow(10, exponent);

    /// <summary>Gets the scale of a built-in culture.</summary>
    /// <param name="culture">Culture name.</param>
    /// <returns>The configured number scale.</returns>
    private static NumberScale ScaleOf(string culture) => NumberToStringConverter.GetConverter(culture).Scale;

    /// <summary>
    /// Strict Conway-Guy-Wechsler names of the SCALE-SHORT-CYRILLIC tables keyed by Conway index n, with the "лли"
    /// junction and the "он" suffix. Literal values, never recomputed by the test; they mirror the Latin reference of
    /// <see cref="NumberScaleContractTests"/> letter for letter (c → ц, qu → кв, x → кс, terminal a → и before -лли-).
    /// </summary>
    private static readonly (long N, string Name)[] CyrillicReference =
    [
        (1, "миллион"), (2, "биллион"), (3, "триллион"), (4, "квадриллион"), (5, "квинтиллион"),
        (6, "секстиллион"), (7, "септиллион"), (8, "октиллион"), (9, "нониллион"), (10, "дециллион"),
        (11, "ундециллион"), (12, "дуодециллион"), (13, "тредециллион"), (14, "кваттуордециллион"),
        (15, "квинквадециллион"), (16, "седециллион"), (17, "септендециллион"), (18, "октодециллион"),
        (19, "новендециллион"), (20, "вигинтиллион"),
        (21, "унвигинтиллион"), (23, "тресвигинтиллион"), (26, "сесвигинтиллион"), (27, "септемвигинтиллион"),
        (29, "новемвигинтиллион"),
        (30, "тригинтиллион"), (33, "трестригинтиллион"), (36, "сестригинтиллион"), (37, "септентригинтиллион"),
        (40, "квадрагинтиллион"), (50, "квинквагинтиллион"), (60, "сексагинтиллион"), (63, "тресексагинтиллион"),
        (66, "сесексагинтиллион"), (70, "септуагинтиллион"), (80, "октогинтиллион"), (83, "тресоктогинтиллион"),
        (86, "сексоктогинтиллион"), (87, "септемоктогинтиллион"), (90, "нонагинтиллион"), (99, "новенонагинтиллион"),
        (100, "центиллион"), (101, "унцентиллион"), (103, "тресцентиллион"), (106, "сексцентиллион"),
        (107, "септенцентиллион"), (109, "новенцентиллион"), (110, "децицентиллион"), (111, "ундецицентиллион"),
        (130, "тригинтацентиллион"), (186, "сексоктогинтацентиллион"), (199, "новенонагинтацентиллион"),
        (200, "дуцентиллион"), (203, "тредуцентиллион"), (206, "седуцентиллион"), (207, "септендуцентиллион"),
        (209, "новендуцентиллион"), (306, "сестрецентиллион"), (803, "тресоктингентиллион"),
        (806, "сексоктингентиллион"), (807, "септемоктингентиллион"), (809, "новемоктингентиллион"),
        (999, "новенонагинтанонгентиллион"),
        (1000, "миллиниллион"), (1001, "миллимиллион"), (1009, "миллинониллион"), (1010, "миллидециллион"),
        (1030, "миллитригинтиллион"), (1100, "миллицентиллион"), (1999, "миллиновенонагинтанонгентиллион"),
        (2000, "биллиниллион"), (6560, "секстиллисексагинтаквингентиллион"), (30000, "тригинтиллиниллион"),
        (1000003, "миллиниллитриллион"),
    ];

    /// <summary>The Cyrillic base generates every reference name with the NTS-24 algorithm unchanged.</summary>
    [TestMethod]
    public void CyrillicBase_ConwayWechsler_MatchesReferenceNames()
    {
        var scale = ScaleOf("SCALE-SHORT-CYRILLIC");

        Assert.IsTrue(scale.IsUnbounded);
        foreach (var (n, name) in CyrillicReference)
            Assert.AreEqual(name, scale.BuildDynamicName(n, "он"), $"Conway n {n}");
        Assert.AreEqual(
            "ундециллиниллисептуагинтасесцентиллисестригинтиллион",
            scale.BuildDynamicName(11_000_670_036, "он"));
    }

    /// <summary>The Cyrillic base keeps the Latin tables' structure: same component count and markers, Latin tables untouched.</summary>
    [TestMethod]
    public void CyrillicBase_DoesNotAlterTheLatinTables()
    {
        var latin = ScaleOf("SCALE-SHORT");

        Assert.AreEqual("(nxs)centi", latin.HundredsPrefixes![1]);
        Assert.AreEqual("se(xs)", latin.UnitsPrefixes![6]);
        Assert.AreEqual("sexcentillion", latin.BuildDynamicName(106, "on"));
        Assert.AreEqual("millinillion", latin.BuildDynamicName(1000, "on"));
    }

    /// <summary>A comma-separated marker list holds multi-letter linking consonants; without commas each letter is a marker.</summary>
    [TestMethod]
    public void LinkingMarkers_CommaSeparatedTokens_AreMultiLetter()
    {
        var scale = new NumberScale(
            [], ["он"], groupSeparator: "лли",
            scale0Prefixes: ["", "ми", "би", "три", "квадри", "квинти", "сексти", "септи", "окти", "нони"],
            unitsPrefixes: ["", "", "", "тре(с)", "", "", "се(кс,с)", "септе(мн)", "", ""],
            tensPrefixes: ["", "(н)деци", "(мс)вигинти", "", "", "", "", "", "(м,кс,с)октогинт[а|-illi=>и]", ""],
            hundredsPrefixes: ["", "(н,кс,с)центи", "", "", "", "", "", "", "", ""]);

        Assert.AreEqual("сексцентиллион", scale.BuildDynamicName(106, "он"));
        Assert.AreEqual("тресцентиллион", scale.BuildDynamicName(103, "он"));
        Assert.AreEqual("сесвигинтиллион", scale.BuildDynamicName(26, "он"));
        Assert.AreEqual("сексоктогинтиллион", scale.BuildDynamicName(86, "он"));
        Assert.AreEqual("септендециллион", scale.BuildDynamicName(17, "он"));
        Assert.AreEqual("седециллион", scale.BuildDynamicName(16, "он"));
    }

    /// <summary>A comma list is split on commas only: "(кс)" stays two one-letter markers, "(кс,)" is malformed.</summary>
    [TestMethod]
    public void LinkingMarkers_WithoutComma_StaySingleLetters()
    {
        string[] scale0 = ["", "a", "b", "c", "d", "e", "f", "g", "h", "i"];
        var scale = new NumberScale(
            [], ["Z"], groupSeparator: "-",
            scale0Prefixes: scale0,
            unitsPrefixes: ["", "u(кс)", "", "", "", "", "", "", "", ""],
            tensPrefixes: ["", "(с)t", "", "", "", "", "", "", "", ""],
            hundredsPrefixes: ["", "(кс)h", "", "", "", "", "", "", "", ""]);

        // u(кс) offers к then с: the tens component accepts с only, the hundreds component accepts к first.
        Assert.AreEqual("uсt-Z", scale.BuildDynamicName(11, "Z"));
        Assert.AreEqual("uкh-Z", scale.BuildDynamicName(101, "Z"));
    }

    /// <summary>Malformed marker lists (empty token, leading, trailing or doubled comma, space) are rejected when the scale is built.</summary>
    [TestMethod]
    public void LinkingMarkers_MalformedLists_AreRejected()
    {
        string[] digits = ["", "a", "b", "c", "d", "e", "f", "g", "h", "i"];
        foreach (string entry in new[] { "се(кс,)", "се(,кс)", "се(к,,с)", "се()", "(,н)центи", "се(кс, с)", "се(кс,с)x" })
        {
            string[] units = ["", "a", "b", "c", "d", "e", entry, "g", "h", "i"];
            Assert.ThrowsExactly<ArgumentException>(() => new NumberScale(
                [], ["on"], scale0Prefixes: digits, unitsPrefixes: units, tensPrefixes: digits, hundredsPrefixes: digits),
                entry);
        }
    }

    /// <summary>RU: short scale with миллиард, then the Cyrillic Conway names; startIndex 2 maps scale n + 1 to Conway n.</summary>
    [TestMethod]
    public void Russian_ScaleNames_AreShortScaleCyrillicConway()
    {
        var ru = NumberToStringConverter.GetConverter("RU");

        Assert.AreEqual("один миллион", ru.Convert(Pow10(6)));
        Assert.AreEqual("один миллиард", ru.Convert(Pow10(9)));
        Assert.AreEqual("один триллион", ru.Convert(Pow10(12)));
        Assert.AreEqual("один квадриллион", ru.Convert(Pow10(15)));
        Assert.AreEqual("один квинтиллион", ru.Convert(Pow10(18)));
        Assert.AreEqual("один ундециллион", ru.Convert(Pow10(36)));
        Assert.AreEqual("один вигинтиллион", ru.Convert(Pow10(63)));

        var scale = ru.Scale;
        Assert.AreEqual("тригинтиллион", scale.GetScaleName(31));
        Assert.AreEqual("центиллион", scale.GetScaleName(101));
        Assert.AreEqual("миллиниллион", scale.GetScaleName(1001));
        Assert.AreEqual("миллиниллитриллион", scale.GetScaleName(1_000_004));
    }

    /// <summary>BG: милион, милиард, трилион…; the "ли" junction keeps the -i- between Conway groups; кватуор is simplified.</summary>
    [TestMethod]
    public void Bulgarian_ScaleNames_AreShortScaleCyrillicConway()
    {
        var bg = NumberToStringConverter.GetConverter("BG");

        Assert.AreEqual("едно милион", bg.Convert(Pow10(6)));
        Assert.AreEqual("едно милиард", bg.Convert(Pow10(9)));
        Assert.AreEqual("едно трилион", bg.Convert(Pow10(12)));
        Assert.AreEqual("едно квадрилион", bg.Convert(Pow10(15)));
        Assert.AreEqual("едно квинтилион", bg.Convert(Pow10(18)));

        var scale = bg.Scale;
        Assert.AreEqual("ундецилион", scale.GetScaleName(12));
        Assert.AreEqual("кватуордецилион", scale.GetScaleName(15));
        Assert.AreEqual("вигинтилион", scale.GetScaleName(21));
        Assert.AreEqual("тригинтилион", scale.GetScaleName(31));
        Assert.AreEqual("центилион", scale.GetScaleName(101));
        Assert.AreEqual("сексцентилион", scale.GetScaleName(107));
        Assert.AreEqual("милинилион", scale.GetScaleName(1001));
    }

    /// <summary>UK: мільйон, мільярд, трильйон…; Ukrainian spelling of the four tables (і/и, кватуор, ні).</summary>
    [TestMethod]
    public void Ukrainian_ScaleNames_AreShortScaleCyrillicConway()
    {
        var uk = NumberToStringConverter.GetConverter("UK");

        Assert.AreEqual("один мільйон", uk.Convert(Pow10(6)));
        Assert.AreEqual("один мільярд", uk.Convert(Pow10(9)));
        Assert.AreEqual("один трильйон", uk.Convert(Pow10(12)));
        Assert.AreEqual("один квадрильйон", uk.Convert(Pow10(15)));
        Assert.AreEqual("один квінтильйон", uk.Convert(Pow10(18)));

        var scale = uk.Scale;
        Assert.AreEqual("ундецильйон(ів)", scale.GetScaleName(12));
        Assert.AreEqual("кватуордецильйон(ів)", scale.GetScaleName(15));
        Assert.AreEqual("квінквадецильйон(ів)", scale.GetScaleName(16));
        Assert.AreEqual("вігінтильйон(ів)", scale.GetScaleName(21));
        Assert.AreEqual("сесвігінтильйон(ів)", scale.GetScaleName(27));
        Assert.AreEqual("тригінтильйон(ів)", scale.GetScaleName(31));
        Assert.AreEqual("квінквагінтильйон(ів)", scale.GetScaleName(51));
        Assert.AreEqual("центильйон(ів)", scale.GetScaleName(101));
        Assert.AreEqual("сексцентильйон(ів)", scale.GetScaleName(107));
        Assert.AreEqual("тригінтацентильйон(ів)", scale.GetScaleName(131));
        Assert.AreEqual("квінгентильйон(ів)", scale.GetScaleName(501));
        // Same junction between groups and before the suffix: мі+ль+ні+ль+йон (accepted limitation).
        Assert.AreEqual("мільнільйон(ів)", scale.GetScaleName(1001));
        Assert.AreEqual("мільнільтрильйон(ів)", scale.GetScaleName(1_000_004));
    }

    /// <summary>No generated RU, BG or UK scale name, nor any cardinal using it, contains an ASCII Latin letter.</summary>
    [TestMethod]
    [DataRow("RU")]
    [DataRow("BG")]
    [DataRow("BG-BG")]
    [DataRow("UK")]
    [DataRow("UK-UA")]
    [DataRow("SCALE-SHORT-CYRILLIC")]
    public void CyrillicScales_ContainNoLatinLetter(string culture)
    {
        var scale = ScaleOf(culture);

        foreach (int index in Enumerable.Range(1, 2100).Concat([9999, 100_001, 1_000_004, 1_234_568, int.MaxValue]))
        {
            string name = scale.GetScaleName(index);
            Assert.IsFalse(AsciiLatinLetter.IsMatch(name), $"{culture} scale {index}: {name}");
        }
        foreach (long n in new long[] { 11_000_670_036, 999_999_999_999, 106_106_106 })
        {
            string name = scale.BuildDynamicName(n, "");
            Assert.IsFalse(AsciiLatinLetter.IsMatch(name), $"{culture} Conway n {n}: {name}");
        }
        if (culture.StartsWith("SCALE", StringComparison.Ordinal)) return;

        var converter = NumberToStringConverter.GetConverter(culture);
        foreach (int exponent in new[] { 36, 63, 321, 2421, 3003, 3333 })
        {
            string text = converter.Convert(Pow10(exponent) * 7 + 106);
            Assert.IsFalse(AsciiLatinLetter.IsMatch(text), $"{culture} 7·10^{exponent} + 106: {text}");
        }
    }

    /// <summary>SW: "li" junction + "oni" suffix keep milioni…trilioni and join Conway groups with "li".</summary>
    [TestMethod]
    public void Swahili_ConwayGroups_KeepTheLiJunction()
    {
        var sw = NumberToStringConverter.GetConverter("SW");
        var scale = sw.Scale;

        // Static names "" and elfu, no index offset: scale n + 1 is Conway n.
        (int N, string Name)[] expected =
        [
            (1, "milioni"), (2, "bilioni"), (3, "trilioni"), (4, "kwadrilioni"), (10, "desilioni"),
            (20, "vigintilioni"), (30, "trigintilioni"), (100, "sentilioni"), (1000, "milinilioni"),
            (1001, "milimilioni"), (1_000_003, "milinilitrilioni"),
        ];
        foreach (var (n, name) in expected)
            Assert.AreEqual(name, scale.GetScaleName(n + 1), $"Conway n {n}");

        Assert.AreEqual("moja milioni", sw.Convert(Pow10(6)));
        Assert.AreEqual("moja trilioni", sw.Convert(Pow10(12)));
        Assert.AreEqual("moja milinilioni", sw.Convert(Pow10(3003)));
    }

    /// <summary>TR: "l" junction + "yon" suffix keep trilyon, katrilyon… and join Conway groups with "l".</summary>
    [TestMethod]
    public void Turkish_ConwayGroups_KeepTheLJunction()
    {
        var tr = NumberToStringConverter.GetConverter("TR");
        var scale = tr.Scale;

        Assert.AreEqual("milyon", scale.GetScaleName(2));
        Assert.AreEqual("milyar", scale.GetScaleName(3));
        // Four static names and startIndex 2: scale n + 1 is Conway n from n = 3.
        (int N, string Name)[] expected =
        [
            (3, "trilyon"), (4, "katrilyon"), (5, "kentilyon"), (10, "desilyon"), (20, "vigintilyon"),
            (30, "trigintilyon"), (100, "sentilyon"), (1000, "milnilyon"), (1001, "milmilyon"),
            (1_000_003, "milniltrilyon"),
        ];
        foreach (var (n, name) in expected)
            Assert.AreEqual(name, scale.GetScaleName(n + 1), $"Conway n {n}");

        Assert.AreEqual("bir trilyon", tr.Convert(Pow10(12)));
        Assert.AreEqual("bir milnilyon", tr.Convert(Pow10(3003)));
    }

    /// <summary>TR case variants inflect static and generated scale names alike (last vowel o: -u, -a).</summary>
    [TestMethod]
    [DataRow(12, "case=accusative", "bir trilyonu")]
    [DataRow(12, "case=dative", "bir trilyona")]
    [DataRow(15, "case=accusative", "bir katrilyonu")]
    [DataRow(15, "case=dative", "bir katrilyona")]
    [DataRow(21, "case=accusative", "bir sekstilyonu")]
    [DataRow(21, "case=dative", "bir sekstilyona")]
    [DataRow(3003, "case=accusative", "bir milnilyonu")]
    [DataRow(3003, "case=dative", "bir milnilyona")]
    [DataRow(6, "case=accusative", "bir milyonu")]
    [DataRow(9, "case=dative", "bir milyara")]
    public void Turkish_CaseVariants_InflectGeneratedScaleNames(int exponent, string variants, string expected)
    {
        Assert.AreEqual(expected, NumberToStringConverter.GetConverter("TR").Convert(Pow10(exponent), variants));
    }

    /// <summary>The five cultures of NTS-25A, and their regional cultures, stay unbounded.</summary>
    [TestMethod]
    [DataRow("RU")]
    [DataRow("BG")]
    [DataRow("BG-BG")]
    [DataRow("UK")]
    [DataRow("UK-UA")]
    [DataRow("SW")]
    [DataRow("SW-KE")]
    [DataRow("SW-TZ")]
    [DataRow("TR")]
    [DataRow("TR-TR")]
    public void Nts25ACultures_AreUnbounded(string culture)
    {
        var scale = ScaleOf(culture);

        Assert.IsTrue(scale.IsUnbounded, culture);
        Assert.IsTrue(scale.CanNameGroup(int.MaxValue), culture);
    }
}
