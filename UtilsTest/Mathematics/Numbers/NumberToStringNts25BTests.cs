using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-25B guards for the Conway tables localized to Indonesian, Malay, Swahili and Turkish spelling. Each language
/// adapts the four Conway tables with the mechanisms of its attested loans (ID/MS qu → ku, SW qu → kw, TR qu → k,
/// x → ks, soft c → s); the structure stays strict Conway-Guy-Wechsler (NTS-24). Attested forms are tested on their
/// own; the other expectations are a project-authorized productive extrapolation, written here as literal values.
/// </summary>
[TestClass]
public class NumberToStringNts25BTests
{
    /// <summary>Returns 10 raised to <paramref name="exponent"/>.</summary>
    private static BigInteger Pow10(int exponent) => BigInteger.Pow(10, exponent);

    /// <summary>Gets the scale of a built-in culture.</summary>
    /// <param name="culture">Culture name.</param>
    /// <returns>The configured number scale.</returns>
    private static NumberScale ScaleOf(string culture) => NumberToStringConverter.GetConverter(culture).Scale;

    /// <summary>
    /// Expected scale names by Conway index n, displayed at scale index n + 1 in the four languages (n = 1 and 2 are
    /// static names in ID, MS and TR). Literal values, never recomputed by the test.
    /// </summary>
    private static readonly Dictionary<string, (long N, string Name)[]> Reference = new()
    {
        ["ID"] =
        [
            (1, "juta"), (2, "miliar"), (3, "triliun"), (4, "kuadriliun"), (5, "kuintiliun"), (6, "sekstiliun"),
            (7, "septiliun"), (8, "oktiliun"), (9, "noniliun"), (10, "desiliun"), (11, "undesiliun"),
            (12, "duodesiliun"), (13, "tredesiliun"), (14, "kuatuordesiliun"), (15, "kuinkuadesiliun"),
            (16, "sedesiliun"), (17, "septendesiliun"), (18, "oktodesiliun"), (19, "novendesiliun"),
            (20, "vigintiliun"), (21, "unvigintiliun"), (30, "trigintiliun"), (40, "kuadragintiliun"),
            (50, "kuinkuagintiliun"), (60, "seksagintiliun"), (70, "septuagintiliun"), (80, "oktogintiliun"),
            (90, "nonagintiliun"), (100, "sentiliun"), (101, "unsentiliun"), (103, "tressentiliun"),
            (106, "sekssentiliun"), (186, "seksoktogintasentiliun"), (200, "dusentiliun"),
            (999, "novenonagintanongentiliun"), (1000, "miliniliun"), (1001, "milimiliun"), (1009, "milinoniliun"),
            (1010, "milidesiliun"), (1030, "militrigintiliun"), (1100, "milisentiliun"),
            (1999, "milinovenonagintanongentiliun"), (2000, "biliniliun"), (1_000_003, "milinilitriliun"),
        ],
        ["MS"] =
        [
            (1, "juta"), (2, "bilion"), (3, "trilion"), (4, "kuadrilion"), (5, "kuintilion"), (6, "sekstilion"),
            (7, "septilion"), (8, "oktilion"), (9, "nonilion"), (10, "desilion"), (11, "undesilion"),
            (12, "duodesilion"), (13, "tredesilion"), (14, "kuatuordesilion"), (15, "kuinkuadesilion"),
            (16, "sedesilion"), (17, "septendesilion"), (18, "oktodesilion"), (19, "novendesilion"),
            (20, "vigintilion"), (21, "unvigintilion"), (30, "trigintilion"), (40, "kuadragintilion"),
            (50, "kuinkuagintilion"), (60, "seksagintilion"), (70, "septuagintilion"), (80, "oktogintilion"),
            (90, "nonagintilion"), (100, "sentilion"), (101, "unsentilion"), (103, "tressentilion"),
            (106, "sekssentilion"), (186, "seksoktogintasentilion"), (200, "dusentilion"),
            (999, "novenonagintanongentilion"), (1000, "milinilion"), (1001, "milimilion"), (1009, "milinonilion"),
            (1010, "milidesilion"), (1030, "militrigintilion"), (1100, "milisentilion"),
            (1999, "milinovenonagintanongentilion"), (2000, "bilinilion"), (1_000_003, "milinilitrilion"),
        ],
        ["SW"] =
        [
            (1, "milioni"), (2, "bilioni"), (3, "trilioni"), (4, "kwadrilioni"), (5, "kwintilioni"),
            (6, "sekstilioni"), (7, "septilioni"), (8, "oktilioni"), (9, "nonilioni"), (10, "desilioni"),
            (11, "undesilioni"), (12, "duodesilioni"), (13, "tredesilioni"), (14, "kwatuordesilioni"),
            (15, "kwinkwadesilioni"), (16, "sedesilioni"), (17, "septendesilioni"), (18, "oktodesilioni"),
            (19, "novendesilioni"), (20, "vigintilioni"), (21, "unvigintilioni"), (30, "trigintilioni"),
            (40, "kwadragintilioni"), (50, "kwinkwagintilioni"), (60, "seksagintilioni"), (70, "septuagintilioni"),
            (80, "oktogintilioni"), (90, "nonagintilioni"), (100, "sentilioni"), (101, "unsentilioni"),
            (103, "tressentilioni"), (106, "sekssentilioni"), (186, "seksoktogintasentilioni"),
            (200, "dusentilioni"), (999, "novenonagintanongentilioni"), (1000, "milinilioni"),
            (1001, "milimilioni"), (1009, "milinonilioni"), (1010, "milidesilioni"), (1030, "militrigintilioni"),
            (1100, "milisentilioni"), (1999, "milinovenonagintanongentilioni"), (2000, "bilinilioni"),
            (1_000_003, "milinilitrilioni"),
        ],
        ["TR"] =
        [
            (1, "milyon"), (2, "milyar"), (3, "trilyon"), (4, "katrilyon"), (5, "kentilyon"), (6, "sekstilyon"),
            (7, "septilyon"), (8, "oktilyon"), (9, "nonilyon"), (10, "desilyon"), (11, "undesilyon"),
            (12, "dodesilyon"), (13, "tredesilyon"), (14, "katordesilyon"), (15, "kenkadesilyon"),
            (16, "sedesilyon"), (17, "septendesilyon"), (18, "oktodesilyon"), (19, "novendesilyon"),
            (20, "vigintilyon"), (21, "unvigintilyon"), (30, "trigintilyon"), (40, "katragintilyon"),
            (50, "kenkagintilyon"), (60, "seksagintilyon"), (70, "septuagintilyon"), (80, "oktogintilyon"),
            (90, "nonagintilyon"), (100, "sentilyon"), (101, "unsentilyon"), (103, "tressentilyon"),
            (106, "sekssentilyon"), (186, "seksoktogintasentilyon"), (200, "dusentilyon"),
            (999, "novenonagintanongentilyon"), (1000, "milnilyon"), (1001, "milmilyon"), (1009, "milnonilyon"),
            (1010, "mildesilyon"), (1030, "miltrigintilyon"), (1100, "milsentilyon"),
            (1999, "milnovenonagintanongentilyon"), (2000, "bilnilyon"), (1_000_003, "milniltrilyon"),
        ],
    };

    /// <summary>Every reference index produces its literal name in the four languages and their regional cultures.</summary>
    [TestMethod]
    [DataRow("ID", "ID")]
    [DataRow("ID-ID", "ID")]
    [DataRow("MS", "MS")]
    [DataRow("MS-MY", "MS")]
    [DataRow("SW", "SW")]
    [DataRow("SW-KE", "SW")]
    [DataRow("SW-TZ", "SW")]
    [DataRow("TR", "TR")]
    [DataRow("TR-TR", "TR")]
    public void LocalizedConway_MatchesReferenceNames(string culture, string reference)
    {
        var scale = ScaleOf(culture);

        foreach (var (n, name) in Reference[reference])
            Assert.AreEqual(name, scale.GetScaleName(checked((int)n + 1)), $"{culture} Conway n {n}");
    }

    /// <summary>The attested local forms, checked on their own because they take priority over any extrapolation.</summary>
    [TestMethod]
    [DataRow("ID", "triliun,kuadriliun,kuintiliun,sekstiliun,septiliun,oktiliun,noniliun,desiliun")]
    [DataRow("MS", "trilion,kuadrilion,kuintilion,sekstilion,septilion,oktilion")]
    [DataRow("SW", "trilioni,kwadrilioni,kwintilioni,sekstilioni,septilioni,oktilioni,nonilioni,desilioni")]
    [DataRow("TR", "trilyon,katrilyon,kentilyon,sekstilyon,septilyon,oktilyon,nonilyon,desilyon")]
    public void AttestedForms_AreProducedFromTheTrillion(string culture, string forms)
    {
        var scale = ScaleOf(culture);
        string[] expected = forms.Split(',');

        // n = 3 (the trillion, 10^12) is scale index 4 in the four languages.
        for (int i = 0; i < expected.Length; i++)
            Assert.AreEqual(expected[i], scale.GetScaleName(4 + i), $"{culture} 10^{12 + 3 * i}");
    }

    /// <summary>The attested Turkish forms above the decillion that strict Conway shares with the dictionary series.</summary>
    [TestMethod]
    public void Turkish_AttestedHigherForms_AreProduced()
    {
        var scale = ScaleOf("TR");

        // tr.wikipedia "Büyük sayıların adları": Conway n -> name; scale index = n + 1.
        (int N, string Name)[] attested =
        [
            (11, "undesilyon"), (12, "dodesilyon"), (13, "tredesilyon"), (14, "katordesilyon"),
            (17, "septendesilyon"), (18, "oktodesilyon"), (20, "vigintilyon"), (30, "trigintilyon"),
            (40, "katragintilyon"), (50, "kenkagintilyon"), (100, "sentilyon"),
        ];
        foreach (var (n, name) in attested)
            Assert.AreEqual(name, scale.GetScaleName(n + 1), $"Conway n {n}");
    }

    /// <summary>MS shares the four Indonesian Conway tables through baseOn and differs only by its suffix and statics.</summary>
    [TestMethod]
    public void Malay_InheritsTheIndonesianTables()
    {
        var id = ScaleOf("ID");
        var ms = ScaleOf("MS");

        CollectionAssert.AreEqual(id.Scale0Prefixes!.ToArray(), ms.Scale0Prefixes!.ToArray());
        CollectionAssert.AreEqual(id.UnitsPrefixes!.ToArray(), ms.UnitsPrefixes!.ToArray());
        CollectionAssert.AreEqual(id.TensPrefixes!.ToArray(), ms.TensPrefixes!.ToArray());
        CollectionAssert.AreEqual(id.HundredsPrefixes!.ToArray(), ms.HundredsPrefixes!.ToArray());
        CollectionAssert.AreEqual(new[] { "un" }, id.ScaleSuffixes.ToArray());
        CollectionAssert.AreEqual(new[] { "on" }, ms.ScaleSuffixes.ToArray());
    }

    /// <summary>
    /// The prioritized two-letter link ks is inserted whole: sex- never regresses to x, k or s alone before an x-marked
    /// component (centi, octoginta, octingenti), while s stays the link before an s-only component (viginti).
    /// </summary>
    [TestMethod]
    [DataRow("ID", "iun")]
    [DataRow("MS", "ion")]
    [DataRow("SW", "ioni")]
    [DataRow("TR", "yon")]
    public void TwoLetterLink_KsIsInsertedWhole(string culture, string ending)
    {
        var scale = ScaleOf(culture);

        Assert.AreEqual("sekssentil" + ending, scale.GetScaleName(107));
        Assert.AreEqual("seksoktogintil" + ending, scale.GetScaleName(87));
        StringAssert.StartsWith(scale.GetScaleName(807), "seksoktingentil");
        StringAssert.StartsWith(scale.GetScaleName(27), "sesvigintil");
        StringAssert.StartsWith(scale.GetScaleName(104), "tressentil");
    }

    /// <summary>Generated names use only letters of the language's alphabet over a broad range of indices.</summary>
    [TestMethod]
    [DataRow("SW", "q,x,c")]
    [DataRow("SW-KE", "q,x,c")]
    [DataRow("SW-TZ", "q,x,c")]
    [DataRow("TR", "q,x,c")]
    [DataRow("TR-TR", "q,x,c")]
    [DataRow("ID", "qu,x,c")]
    [DataRow("ID-ID", "qu,x,c")]
    [DataRow("MS", "qu,x,c")]
    [DataRow("MS-MY", "qu,x,c")]
    public void GeneratedNames_UseTheLocalAlphabet(string culture, string forbidden)
    {
        var scale = ScaleOf(culture);
        string[] letters = forbidden.Split(',');

        IEnumerable<string> names = Enumerable.Range(2, 2100)
            .Concat([9999, 100_001, 1_000_004, 1_234_568, int.MaxValue])
            .Select(scale.GetScaleName)
            .Concat(new long[] { 11_000_670_036, 999_999_999_999, 106_186_806 }.Select(n => scale.BuildDynamicName(n, "")));
        foreach (string name in names)
            foreach (string letter in letters)
                Assert.IsFalse(name.Contains(letter, StringComparison.Ordinal), $"{culture}: '{letter}' in {name}");
    }

    /// <summary>TR case variants inflect the localized and grouped names (last vowel o: accusative -u, dative -a).</summary>
    [TestMethod]
    [DataRow(21, "case=nominative", "bir sekstilyon")]
    [DataRow(21, "case=accusative", "bir sekstilyonu")]
    [DataRow(21, "case=dative", "bir sekstilyona")]
    [DataRow(33, "case=nominative", "bir desilyon")]
    [DataRow(33, "case=accusative", "bir desilyonu")]
    [DataRow(33, "case=dative", "bir desilyona")]
    [DataRow(63, "case=nominative", "bir vigintilyon")]
    [DataRow(63, "case=accusative", "bir vigintilyonu")]
    [DataRow(63, "case=dative", "bir vigintilyona")]
    [DataRow(3003, "case=nominative", "bir milnilyon")]
    [DataRow(3003, "case=accusative", "bir milnilyonu")]
    [DataRow(3003, "case=dative", "bir milnilyona")]
    public void Turkish_CaseVariants_InflectLocalizedNames(int exponent, string variants, string expected)
    {
        Assert.AreEqual(expected, NumberToStringConverter.GetConverter("TR").Convert(Pow10(exponent), variants));
    }

    /// <summary>Cardinals use the localized names (10^33 is n = 10, 10^321 is n = 106).</summary>
    [TestMethod]
    [DataRow("ID", "satu desiliun", "satu sekssentiliun")]
    [DataRow("MS", "satu desilion", "satu sekssentilion")]
    [DataRow("SW", "moja desilioni", "moja sekssentilioni")]
    [DataRow("TR", "bir desilyon", "bir sekssentilyon")]
    public void Cardinals_UseTheLocalizedNames(string culture, string decillion, string n106)
    {
        var converter = NumberToStringConverter.GetConverter(culture);

        Assert.AreEqual(decillion, converter.Convert(Pow10(33)));
        Assert.AreEqual(n106, converter.Convert(Pow10(321)));
    }

    /// <summary>The four NTS-25B languages and their regional cultures stay unbounded.</summary>
    [TestMethod]
    [DataRow("ID")]
    [DataRow("ID-ID")]
    [DataRow("MS")]
    [DataRow("MS-MY")]
    [DataRow("SW")]
    [DataRow("TR")]
    public void Nts25BCultures_AreUnbounded(string culture)
    {
        var scale = ScaleOf(culture);

        Assert.IsTrue(scale.IsUnbounded, culture);
        Assert.IsTrue(scale.CanNameGroup(int.MaxValue), culture);
    }
}
