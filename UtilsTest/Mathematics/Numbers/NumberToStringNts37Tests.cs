using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-37: paired scales (<see cref="NumberScale.PairedScalesFrom"/>, XML <c>pairedScalesFrom</c>) and the Finnish
/// large numbers they enable. From the configured index on, only every other scale is named and its multiplier
/// spans two groups, read as a complete cardinal: Finnish 10^15 is <c>tuhat biljoonaa</c>, a thousand times
/// <c>biljoona</c>, since <c>biljardi</c> is not a Finnish numeral (Kielitoimiston sanakirja). The first half pins the
/// language-independent engine contract on a synthetic language; the second half pins the sourced Finnish
/// forms: long-scale names localized from the Conway-Wechsler system, multiplier agreement, case forms, the
/// case forms of zero and the ordinals from 2000 (sources in <c>docs/NTS-08-linguistic-sources.md</c>, NTS-37
/// section).
/// </summary>
[TestClass]
public class NumberToStringNts37Tests
{
    // ===== synthetic engine ==========================================================================

    /// <summary>Unit words of the synthetic language.</summary>
    private static readonly string[] Units = ["", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"];

    /// <summary>Tens words of the synthetic language.</summary>
    private static readonly string[] Tens = ["", "ten", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

    /// <summary>Base prefixes of the synthetic generated scales (aZ, bZ, cZ …).</summary>
    private static readonly string[] Prefixes = ["", "a", "b", "c", "d", "e", "f", "g", "h", "i"];

    /// <summary>Language specifics counting the finalizations of public results.</summary>
    private sealed class CountingSpecifics : INumberToStringLanguageSpecifics
    {
        /// <summary>The number of finalizations.</summary>
        public int Calls;

        /// <inheritdoc />
        public string FinalizeWriting(string languageIdentifier, string text)
        {
            Interlocked.Increment(ref Calls);
            return text;
        }
    }

    /// <summary>Creates a unique culture name.</summary>
    private static string NewName() => $"NTS37-{Guid.NewGuid():N}";

    /// <summary>Renders a ten-entry digit table as XML digits.</summary>
    /// <param name="table">The table.</param>
    /// <returns>The digit elements.</returns>
    private static string Digits(string[] table) => string.Concat(table.Select((s, i) => $"""<Digit digit="{i}" string="{s}" />"""));

    /// <summary>
    /// Renders a synthetic language: groups of two digits, scale 1 named "x", scale 2 named "Y", generated scales
    /// "aZ", "bZ" … from scale 3 (prefix value 1, 2 …); tens composed with a space ("twenty one"). The prefix
    /// tables stop at 9, so the scale is bounded by <paramref name="maxNumber"/>.
    /// </summary>
    /// <param name="culture">The culture identifier.</param>
    /// <param name="scaleAttributes">Attributes appended to the NumberScale element.</param>
    /// <param name="scaleContent">Elements appended inside the NumberScale.</param>
    /// <param name="languageContent">Elements appended inside the Language (Replacements …).</param>
    /// <param name="maxNumber">The maximum number (sixteen digits: groups 0 to 7).</param>
    /// <returns>The Language element.</returns>
    private static string Language(string culture, string scaleAttributes = "", string scaleContent = "", string languageContent = "", string maxNumber = "9999999999999999")
    {
        string tens = string.Concat(Tens.Select((t, i) => i == 0
            ? """<Digit digit="0" string="" buildString="*" />"""
            : $"""<Digit digit="{i}" string="{t}" buildString="{t} *" />"""));
        return $"""
              <Language groupSize="2" separator=" " groupSeparator="" zero="zero" minus="minus *" decimalSeparator="point" maxNumber="{maxNumber}">
                <Culture>{culture}</Culture>
                <Groups>
                  <Group level="1">{Digits(Units)}</Group>
                  <Group level="2">{tens}</Group>
                </Groups>
                <NumberScale firstLetterUpperCase="false" groupSeparator="" {scaleAttributes}>
                  <StaticNames><Scale value="0" string="" /><Scale value="1" string="x" /><Scale value="2" string="Y" /></StaticNames>
                  <Suffixes><Suffix>Z</Suffix></Suffixes>
                  <Scale0Prefixes>{Digits(Prefixes)}</Scale0Prefixes>
                  {scaleContent}
                </NumberScale>
                {languageContent}
              </Language>
            """;
    }

    /// <summary>Wraps Language elements in a configuration document.</summary>
    /// <param name="languages">The Language elements.</param>
    /// <returns>The document.</returns>
    private static string Document(params string[] languages) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
        {string.Concat(languages)}
        </Numbers>
        """;

    /// <summary>Builds the synthetic converter.</summary>
    /// <param name="scaleAttributes">Attributes appended to the NumberScale element.</param>
    /// <param name="scaleContent">Elements appended inside the NumberScale.</param>
    /// <param name="languageContent">Elements appended inside the Language.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Build(string scaleAttributes = "", string scaleContent = "", string languageContent = "")
    {
        string culture = NewName();
        return NumberToStringConverter.ReadConfiguration(Document(Language(culture, scaleAttributes, scaleContent, languageContent)))[culture];
    }

    /// <summary>Builds the synthetic converter whose scales are paired from scale 2 (Y).</summary>
    /// <param name="scaleAttributes">Further attributes appended to the NumberScale element.</param>
    /// <param name="scaleContent">Elements appended inside the NumberScale.</param>
    /// <param name="languageContent">Elements appended inside the Language.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Paired(string scaleAttributes = "", string scaleContent = "", string languageContent = "")
        => Build("""pairedScalesFrom="2" """ + scaleAttributes, scaleContent, languageContent);

    /// <summary>Returns <paramref name="multiplier"/> × 100^<paramref name="scale"/> (the synthetic groups have two digits).</summary>
    /// <param name="multiplier">The group multiplier.</param>
    /// <param name="scale">The scale index.</param>
    /// <returns>The value.</returns>
    private static BigInteger S(long multiplier, int scale) => multiplier * BigInteger.Pow(100, scale);

    /// <summary>Returns the form <paramref name="key"/> of <paramref name="forms"/>.</summary>
    /// <param name="forms">The forms.</param>
    /// <param name="key">The form key.</param>
    /// <returns>The form.</returns>
    private static string Form(LexicalFormSet forms, string key)
    {
        Assert.IsTrue(forms.TryGetForm(key, out string value), key);
        return value;
    }

    /// <summary>Without the attribute every scale is named and every multiplier is one group (historical behaviour).</summary>
    [TestMethod]
    public void Synthetic_Absent_NamesEveryScale()
    {
        var converter = Build();
        Assert.IsNull(converter.Scale.PairedScalesFrom);
        Assert.AreEqual("one aZ", converter.Convert(S(1, 3)));
        Assert.AreEqual("two bZ three aZ", converter.Convert(S(2, 4) + S(3, 3)));
        Assert.AreEqual("aZ", converter.Scale.GetScaleName(3));
    }

    /// <summary>
    /// From the configured index, only every other scale is named; the scale above a named one is read as the
    /// upper half of its multiplier, which is converted as a complete cardinal (thousands of the named scale).
    /// </summary>
    [TestMethod]
    public void Synthetic_PairedScales_ReadTheUpperGroupInTheMultiplier()
    {
        var converter = Paired();

        Assert.AreEqual(2, converter.Scale.PairedScalesFrom);
        // A multiplier held by the lower group alone is rendered as before.
        Assert.AreEqual("one Y", converter.Convert(S(1, 2)));
        Assert.AreEqual("ninety nine Y", converter.Convert(S(99, 2)));
        // The upper group joins the multiplier: 100 Y is "one x Y", not a scale of its own.
        Assert.AreEqual("one x Y", converter.Convert(S(1, 3)));
        Assert.AreEqual("four x Y", converter.Convert(S(4, 3)));
        Assert.AreEqual("twenty one x five Y", converter.Convert(S(21, 3) + S(5, 2)));
        Assert.AreEqual("ninety nine x ninety nine Y", converter.Convert(S(99, 3) + S(99, 2)));
        // The next named scale is two indices higher, with its own two-group multiplier.
        Assert.AreEqual("two bZ", converter.Convert(S(2, 4)));
        Assert.AreEqual("two x three bZ", converter.Convert(S(2, 5) + S(3, 4)));
        Assert.AreEqual("ninety nine x dZ", converter.Convert(S(99, 7)));
        // Groups below the first paired scale are untouched; every named group is separated as usual.
        Assert.AreEqual("two x three bZ four x five Y six x seven",
            converter.Convert(S(2, 5) + S(3, 4) + S(4, 3) + S(5, 2) + S(6, 1) + 7));
        Assert.AreEqual("two bZ seven", converter.Convert(S(2, 4) + 7));
        Assert.AreEqual("minus one x Y", converter.Convert(-S(1, 3)));
    }

    /// <summary>The upper scale of a pair has no name of its own; the named scales keep theirs.</summary>
    [TestMethod]
    public void Synthetic_PairedScales_UpperScaleHasNoName()
    {
        var scale = Paired().Scale;

        Assert.AreEqual("Y", scale.GetScaleName(2));
        Assert.AreEqual("bZ", scale.GetScaleName(4));
        Assert.AreEqual("dZ", scale.GetScaleName(6));
        foreach (int index in new[] { 3, 5, 7 })
        {
            Assert.IsFalse(scale.CanNameGroup(index), index.ToString());
            Assert.ThrowsExactly<InvalidOperationException>(() => scale.GetScaleName(index), index.ToString());
        }
        Assert.IsTrue(scale.CanNameGroup(2));
        Assert.IsTrue(scale.CanNameGroup(4));
    }

    /// <summary>The scale noun agrees with the whole multiplier, exact and generated forms alike.</summary>
    [TestMethod]
    public void Synthetic_PairedScales_FormsAgreeWithTheWholeMultiplier()
    {
        var converter = Paired(scaleContent: """
            <ScaleForm scale="2"><Forms><Form key="singular" value="Y" /><Form key="plural" value="Ys" /></Forms></ScaleForm>
            <GeneratedScaleForm suffix="0"><Forms><Form key="singular" value="Z" /><Form key="plural" value="Zs" /></Forms></GeneratedScaleForm>
            """);

        Assert.AreEqual("one Y", converter.Convert(S(1, 2)));
        Assert.AreEqual("two Ys", converter.Convert(S(2, 2)));
        Assert.AreEqual("one x Ys", converter.Convert(S(1, 3)), "multiplier 100, not 0");
        Assert.AreEqual("one x one Ys", converter.Convert(S(1, 3) + S(1, 2)), "multiplier 101, not 1");
        Assert.AreEqual("one bZ", converter.Convert(S(1, 4)));
        Assert.AreEqual("one x bZs", converter.Convert(S(1, 5)));
    }

    /// <summary>A scale-scoped replacement sees the whole multiplier as its group value.</summary>
    [TestMethod]
    public void Synthetic_PairedScales_ReplacementsSeeTheWholeMultiplier()
    {
        var converter = Paired(languageContent: """
            <Replacements><Replacement oldValue="one" newValue="a single" scope="StartsWith" onScale="2.." onValue="1" /></Replacements>
            """);

        Assert.AreEqual("a single Y", converter.Convert(S(1, 2)));
        Assert.AreEqual("a single bZ", converter.Convert(S(1, 4)));
        Assert.AreEqual("one x Y", converter.Convert(S(1, 3)), "multiplier 100");
        Assert.AreEqual("one x", converter.Convert(S(1, 1)), "scale 1 is not covered");
    }

    /// <summary>The multiplier separators apply to the paired scale and, inside its multiplier, to the lower scales.</summary>
    [TestMethod]
    public void Synthetic_PairedScales_UseTheMultiplierSeparators()
    {
        var converter = Paired("""multiplierSeparator="_" """, """<ScaleForm scale="1" multiplierSeparator="" />""");

        Assert.AreEqual("two_Y", converter.Convert(S(2, 2)));
        Assert.AreEqual("onex_Y", converter.Convert(S(1, 3)));
        Assert.AreEqual("twox three_bZ", converter.Convert(S(2, 5) + S(3, 4)));
    }

    /// <summary>A bounded scale only needs names for the named scales: the upper scales of the pairs are skipped.</summary>
    [TestMethod]
    public void Synthetic_PairedScales_BoundedDomainSkipsTheUpperScales()
    {
        var converter = Paired();

        Assert.AreEqual(BigInteger.Parse("9999999999999999"), converter.MaxNumber);
        Assert.AreEqual("ninety nine x ninety nine dZ ninety nine x ninety nine bZ ninety nine x ninety nine Y ninety nine x ninety nine",
            converter.Convert(BigInteger.Parse("9999999999999999")));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => converter.Convert(BigInteger.Parse("10000000000000000")));
    }

    /// <summary>baseOn: an absent attribute inherits, an explicit one replaces.</summary>
    [TestMethod]
    public void Synthetic_BaseOn_InheritsAndReplaces()
    {
        string parent = NewName(), inheriting = NewName(), replacing = NewName();
        var converters = NumberToStringConverter.ReadConfiguration(Document(
            Language(parent, """pairedScalesFrom="2" """),
            $"""<Language baseOn="{parent}"><Culture>{inheriting}</Culture></Language>""",
            $"""<Language baseOn="{parent}"><Culture>{replacing}</Culture><NumberScale pairedScalesFrom="4" /></Language>"""));

        Assert.AreEqual("one x Y", converters[parent].Convert(S(1, 3)));
        Assert.AreEqual(2, converters[inheriting].Scale.PairedScalesFrom);
        Assert.AreEqual("one x Y", converters[inheriting].Convert(S(1, 3)));
        Assert.AreEqual(4, converters[replacing].Scale.PairedScalesFrom);
        Assert.AreEqual("one aZ", converters[replacing].Convert(S(1, 3)));
        Assert.AreEqual("one x bZ", converters[replacing].Convert(S(1, 5)));
    }

    /// <summary>Programmatic construction and cloning carry the setting; invalid indices are rejected.</summary>
    [TestMethod]
    public void Programmatic_ConstructionCloneAndValidation()
    {
        var source = Build();
        var converter = new NumberToStringConverter(new NumberToStringConverterOptions(source)
        {
            Scale = new NumberScale(["", "x", "Y"], ["Z"], groupSeparator: "", scale0Prefixes: Prefixes) { PairedScalesFrom = 2 },
        });
        Assert.AreEqual("one x Y", converter.Convert(S(1, 3)));

        var clone = new NumberToStringConverter(new NumberToStringConverterOptions(converter));
        Assert.AreEqual(2, clone.Scale.PairedScalesFrom);
        Assert.AreEqual("two x three bZ", clone.Convert(S(2, 5) + S(3, 4)));

        foreach (int index in new[] { 0, -1 })
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new NumberScale(["", "x"], ["Z"]) { PairedScalesFrom = index }, index.ToString());
        Assert.Throws<Exception>(() => Build("""pairedScalesFrom="0" """));
    }

    /// <summary>Pairing introduces no intermediate finalization: one public result, one call.</summary>
    [TestMethod]
    public void Finalization_RunsOncePerPublicResult()
    {
        var specifics = new CountingSpecifics();
        var converter = new NumberToStringConverter(new NumberToStringConverterOptions(Paired()) { LanguageSpecifics = specifics });

        Assert.AreEqual("two x three bZ four x five Y", converter.Convert(S(2, 5) + S(3, 4) + S(4, 3) + S(5, 2)));
        Assert.AreEqual(1, specifics.Calls);
    }

    /// <summary>Finnish is the only built-in language that pairs its scales.</summary>
    [TestMethod]
    public void BuiltIn_OnlyFinnishPairsItsScales()
    {
        string[] cultures = ["AR", "BG", "CA", "ca-ES-valencia", "CS", "DA", "DE", "de-CH", "EE", "EL", "EN", "EN-GB", "ES", "EU", "FA",
            "FR", "FR-be", "FR-ca", "FR-ch", "GL", "HE", "HI", "HR", "HU", "ID", "IT", "JA", "KO", "MS", "NL", "NO", "PL", "PT",
            "RO", "RU", "SCALE-LONG", "SCALE-SHORT", "SCALE-SHORT-CYRILLIC", "SK", "SV", "SW", "TR", "UK", "VN", "WO", "ZH", "ZU"];
        foreach (string culture in cultures)
            Assert.IsNull(NumberToStringConverter.GetConverter(culture).Scale.PairedScalesFrom, culture);
        Assert.AreEqual(4, FI.Scale.PairedScalesFrom);
    }

    // ===== Finnish: large-scale names ================================================================

    /// <summary>The Finnish converter.</summary>
    private static NumberToStringConverter FI => NumberToStringConverter.GetConverter("FI");

    /// <summary>Returns 10^<paramref name="exponent"/>.</summary>
    /// <param name="exponent">The exponent.</param>
    /// <returns>The power of ten.</returns>
    private static BigInteger P(int exponent) => BigInteger.Pow(10, exponent);

    /// <summary>
    /// The long scale (Kielitoimiston sanakirja: miljoona; miljardi "tuhat miljoonaa"; biljoona "miljoona miljoonaa
    /// eli tuhat miljardia"; triljoona "miljoona biljoonaa"; kvadriljoona "miljoona triljoonaa (10^24)";
    /// kvintiljoona "miljoona kvadriljoonaa (10^30)"), extended by the localized Conway-Wechsler prefixes.
    /// </summary>
    [DataTestMethod]
    [DataRow(2, "miljoona")]
    [DataRow(3, "miljardi")]
    [DataRow(4, "biljoona")]
    [DataRow(6, "triljoona")]
    [DataRow(8, "kvadriljoona")]
    [DataRow(10, "kvintiljoona")]
    [DataRow(12, "sekstiljoona")]
    [DataRow(14, "septiljoona")]
    [DataRow(16, "oktiljoona")]
    [DataRow(18, "noniljoona")]
    [DataRow(20, "desiljoona")]
    public void Finnish_ScaleNames_Anchors(int scale, string expected)
        => Assert.AreEqual(expected, FI.Scale.GetScaleName(scale));

    /// <summary>
    /// The productive names (scale index = 2 × Conway index): units, tens and hundreds components, the linking
    /// consonants (se + ks/s, tre + s, septe + m/n) and the grouped names above 999, in Finnish spelling
    /// (qu → kv, x → ks, c → k before a back vowel or a consonant and s before e/i, as in desi-, senti-).
    /// </summary>
    [DataTestMethod]
    [DataRow(11, "undesiljoona")]
    [DataRow(12, "duodesiljoona")]
    [DataRow(14, "kvattuordesiljoona")]
    [DataRow(15, "kvinkvadesiljoona")]
    [DataRow(16, "sedesiljoona")]
    [DataRow(17, "septendesiljoona")]
    [DataRow(18, "oktodesiljoona")]
    [DataRow(20, "vigintiljoona")]
    [DataRow(23, "tresvigintiljoona")]
    [DataRow(26, "sesvigintiljoona")]
    [DataRow(27, "septemvigintiljoona")]
    [DataRow(30, "trigintiljoona")]
    [DataRow(34, "kvattuortrigintiljoona")]
    [DataRow(45, "kvinkvakvadragintiljoona")]
    [DataRow(60, "seksagintiljoona")]
    [DataRow(68, "oktoseksagintiljoona")]
    [DataRow(80, "oktogintiljoona")]
    [DataRow(86, "seksoktogintiljoona")]
    [DataRow(100, "sentiljoona")]
    [DataRow(103, "tressentiljoona")]
    [DataRow(106, "sekssentiljoona")]
    [DataRow(136, "sestrigintasentiljoona")]
    [DataRow(200, "dusentiljoona")]
    [DataRow(300, "tresentiljoona")]
    [DataRow(400, "kvadringentiljoona")]
    [DataRow(500, "kvingentiljoona")]
    [DataRow(600, "sessentiljoona")]
    [DataRow(700, "septingentiljoona")]
    [DataRow(800, "oktingentiljoona")]
    [DataRow(900, "nongentiljoona")]
    [DataRow(999, "novenonagintanongentiljoona")]
    [DataRow(1000, "milliniljoona")]
    [DataRow(1001, "millimiljoona")]
    [DataRow(2010, "billidesiljoona")]
    public void Finnish_ScaleNames_Productive(int conwayIndex, string expected)
    {
        Assert.AreEqual(expected, FI.Scale.GetScaleName(2 * conwayIndex));
        Assert.AreEqual(expected, FI.Convert(P(6 * conwayIndex)));
    }

    /// <summary>
    /// No Latin spelling leaks into a Finnish name, and the -ljardi family is never produced above miljardi: 10^15,
    /// 10^21 … are thousands of the -ljoona below (fi.wikipedia "Suurten lukujen nimet": biljardi, triljardi
    /// "eivät ole normien mukaisia"; Kielitoimiston sanakirja: biljardi is the game only).
    /// </summary>
    [TestMethod]
    public void Finnish_ScaleNames_SpellingGuards()
    {
        for (int conway = 1; conway <= 1100; conway++)
        {
            string name = FI.Scale.GetScaleName(2 * conway);
            Assert.IsTrue(name.EndsWith("ljoona", StringComparison.Ordinal), name);
            Assert.IsFalse(name.IndexOfAny(['c', 'q', 'x']) >= 0, name);
            Assert.IsFalse(name.Contains("lljoona", StringComparison.Ordinal), name);
            Assert.IsFalse(name.Contains("lijoona", StringComparison.Ordinal), name);
        }
        foreach (int scale in new[] { 5, 7, 9, 11, 21 })
            Assert.IsFalse(FI.Scale.CanNameGroup(scale), scale.ToString());
        foreach (int exponent in new[] { 15, 21, 27, 33 })
        {
            string text = FI.Convert(P(exponent));
            Assert.IsFalse(text.Contains("ljardi", StringComparison.Ordinal), text);
            Assert.IsTrue(text.StartsWith("tuhat ", StringComparison.Ordinal), text);
        }
        Assert.IsFalse(FI.Convert(P(24)).Contains("quadri", StringComparison.Ordinal));
        Assert.IsFalse(FI.Convert(P(30)).Contains("quinti", StringComparison.Ordinal));
    }

    /// <summary>The Finnish scale configuration: long-scale suffixes, generated forms per suffix family, unbounded domain.</summary>
    [TestMethod]
    public void Finnish_ScaleConfiguration()
    {
        Assert.IsNull(FI.MaxNumber);
        Assert.IsTrue(FI.Scale.IsUnbounded);
        CollectionAssert.AreEqual(new[] { "joona", "jardi" }, FI.Scale.ScaleSuffixes.ToArray());
        Assert.AreEqual("l", FI.Scale.SuffixSeparator);
        Assert.AreEqual(" ", FI.Scale.MultiplierSeparator);
        Assert.AreEqual("", FI.ScaleFormMultiplierSeparators[1]);
        Assert.IsFalse(FI.ScaleFormMultiplierSeparators.ContainsKey(2));
        Assert.AreEqual("joona", Form(FI.GeneratedScaleForms[0].Forms, "singular"));
        Assert.AreEqual("joonaa", Form(FI.GeneratedScaleForms[0].Forms, "plural"));
        Assert.AreEqual("jardi", Form(FI.GeneratedScaleForms[1].Forms, "singular"));
        Assert.AreEqual("jardia", Form(FI.GeneratedScaleForms[1].Forms, "plural"));
        Assert.AreEqual("tuhat", Form(FI.ScaleForms[1], "singular"));
        Assert.AreEqual("tuhatta", Form(FI.ScaleForms[1], "plural"));
    }

    // ===== Finnish: cardinals ========================================================================

    /// <summary>
    /// Multiplier agreement and composition. Kielitoimiston sanakirja: "Hän peri miljoona markkaa", "miljardi
    /// euroa" (one is not said before the noun), "viisi miljoonaa asukasta" (partitive after any other multiplier);
    /// Kielitoimiston ohjepankki "numeroiden ryhmittely": miljoona, miljardi … usually stand apart from their
    /// multiplier ("kolme miljoonaa", "neljäkymmentäyhdeksän miljoonaa", "sata miljoonaa"), one space between groups.
    /// </summary>
    [DataTestMethod]
    [DataRow("1000000", "miljoona")]
    [DataRow("1000001", "miljoona yksi")]
    [DataRow("1001000", "miljoona tuhat")]
    [DataRow("2000000", "kaksi miljoonaa")]
    [DataRow("3000000", "kolme miljoonaa")]
    [DataRow("5000000", "viisi miljoonaa")]
    [DataRow("10000000", "kymmenen miljoonaa")]
    [DataRow("11000000", "yksitoista miljoonaa")]
    [DataRow("21000000", "kaksikymmentäyksi miljoonaa")]
    [DataRow("49000000", "neljäkymmentäyhdeksän miljoonaa")]
    [DataRow("100000000", "sata miljoonaa")]
    [DataRow("101000000", "satayksi miljoonaa")]
    [DataRow("121000000", "satakaksikymmentäyksi miljoonaa")]
    [DataRow("999999999", "yhdeksänsataayhdeksänkymmentäyhdeksän miljoonaa yhdeksänsataayhdeksänkymmentäyhdeksäntuhatta yhdeksänsataayhdeksänkymmentäyhdeksän")]
    [DataRow("2001021", "kaksi miljoonaa tuhat kaksikymmentäyksi")]
    [DataRow("5426674", "viisi miljoonaa neljäsataakaksikymmentäkuusituhatta kuusisataaseitsemänkymmentäneljä")]
    [DataRow("21121000", "kaksikymmentäyksi miljoonaa satakaksikymmentäyksituhatta")]
    [DataRow("1000000000", "miljardi")]
    [DataRow("2000000000", "kaksi miljardia")]
    [DataRow("10000000000", "kymmenen miljardia")]
    [DataRow("21000000000", "kaksikymmentäyksi miljardia")]
    [DataRow("2003000000", "kaksi miljardia kolme miljoonaa")]
    [DataRow("1000000000000", "biljoona")]
    [DataRow("2000000000000", "kaksi biljoonaa")]
    [DataRow("1000000000000000", "tuhat biljoonaa")]
    [DataRow("2000000000000000", "kaksituhatta biljoonaa")]
    [DataRow("1001000000000000", "tuhat yksi biljoonaa")]
    [DataRow("2003000000000000", "kaksituhatta kolme biljoonaa")]
    [DataRow("999999000000000000", "yhdeksänsataayhdeksänkymmentäyhdeksäntuhatta yhdeksänsataayhdeksänkymmentäyhdeksän biljoonaa")]
    [DataRow("1000000000000000000", "triljoona")]
    [DataRow("1000000000000000000000", "tuhat triljoonaa")]
    [DataRow("1000000000000000000000000", "kvadriljoona")]
    [DataRow("2000000000000000000000000", "kaksi kvadriljoonaa")]
    [DataRow("21000000000000000000000000", "kaksikymmentäyksi kvadriljoonaa")]
    [DataRow("121000000000000000000000000", "satakaksikymmentäyksi kvadriljoonaa")]
    [DataRow("1000000000000000000000000000", "tuhat kvadriljoonaa")]
    [DataRow("1000000000000000000000000000000", "kvintiljoona")]
    [DataRow("2003004005006", "kaksi biljoonaa kolme miljardia neljä miljoonaa viisituhatta kuusi")]
    [DataRow("7002003004005006", "seitsemäntuhatta kaksi biljoonaa kolme miljardia neljä miljoonaa viisituhatta kuusi")]
    public void Finnish_Cardinals(string value, string expected)
    {
        Assert.AreEqual(expected, FI.Convert(BigInteger.Parse(value)));
        Assert.AreEqual(expected, FI.Convert(BigInteger.Parse(value), "sijamuoto=nominatiivi"));
    }

    /// <summary>
    /// The thousands keep their NTS-30E junction (kaksituhatta) while every higher scale stands apart, generated
    /// scales included; a group boundary always is one space, never a thousand junction.
    /// </summary>
    [TestMethod]
    public void Finnish_Boundaries()
    {
        Assert.AreEqual("kaksituhatta", FI.Convert(2000));
        Assert.AreEqual("kaksi miljoonaa", FI.Convert(2_000_000));
        Assert.AreEqual("kaksi biljoonaa", FI.Convert(2_000_000_000_000));
        Assert.AreEqual("kaksi sekstiljoonaa", FI.Convert(2 * P(36)));
        Assert.AreEqual("kaksi milliniljoonaa", FI.Convert(2 * P(6000)));
        string mixed = FI.Convert(BigInteger.Parse("2003004005006"));
        // kaksi_biljoonaa_kolme_miljardia_neljä_miljoonaa_viisituhatta_kuusi
        Assert.AreEqual(7, mixed.Count(c => c == ' '), mixed);
        Assert.IsFalse(mixed.Contains("  ", StringComparison.Ordinal), mixed);
        Assert.IsFalse(FI.Convert(BigInteger.Parse("2000005000")).Contains("kaksi miljardiaviisi", StringComparison.Ordinal));
        Assert.AreEqual("kaksi miljardia viisituhatta", FI.Convert(BigInteger.Parse("2000005000")));
    }

    /// <summary>
    /// The case forms of large numbers: every component inflects (Kielitoimiston ohjepankki "peruslukujen
    /// taivuttaminen" and "pitkät luvut ääneen luettaessa": 29 153:een kahteenkymmeneenyhdeksääntuhanteen
    /// sataanviiteenkymmeneenkolmeen), the scale nouns with their dictionary paradigms (Kielitoimiston sanakirja:
    /// miljoonan, miljoonaa; miljardin, miljardia; biljoonan, biljoonaa; triljoonan; kvadriljoonan).
    /// </summary>
    [DataTestMethod]
    [DataRow("1000000", "miljoonaa", "miljoonan")]
    [DataRow("2000000", "kahta miljoonaa", "kahden miljoonan")]
    [DataRow("21000000", "kahtakymmentäyhtä miljoonaa", "kahdenkymmenenyhden miljoonan")]
    [DataRow("100000000", "sataa miljoonaa", "sadan miljoonan")]
    [DataRow("1000000000", "miljardia", "miljardin")]
    [DataRow("2000000000", "kahta miljardia", "kahden miljardin")]
    [DataRow("1000000000000", "biljoonaa", "biljoonan")]
    [DataRow("2000000000000", "kahta biljoonaa", "kahden biljoonan")]
    [DataRow("1000000000000000", "tuhatta biljoonaa", "tuhannen biljoonan")]
    [DataRow("1000000000000000000", "triljoonaa", "triljoonan")]
    [DataRow("2000000000000000000000000", "kahta kvadriljoonaa", "kahden kvadriljoonan")]
    [DataRow("2001021", "kahta miljoonaa tuhatta kahtakymmentäyhtä", "kahden miljoonan tuhannen kahdenkymmenenyhden")]
    [DataRow("21121000", "kahtakymmentäyhtä miljoonaa sataakahtakymmentäyhtätuhatta", "kahdenkymmenenyhden miljoonan sadankahdenkymmenenyhdentuhannen")]
    [DataRow("2003004005006", "kahta biljoonaa kolmea miljardia neljää miljoonaa viittätuhatta kuutta", "kahden biljoonan kolmen miljardin neljän miljoonan viidentuhannen kuuden")]
    public void Finnish_Cases(string value, string partitive, string genitive)
    {
        Assert.AreEqual(partitive, FI.Convert(BigInteger.Parse(value), "sijamuoto=partitiivi"));
        Assert.AreEqual(genitive, FI.Convert(BigInteger.Parse(value), "sijamuoto=genetiivi"));
    }

    /// <summary>The small-number case matrix of NTS-30E is unchanged.</summary>
    [DataTestMethod]
    [DataRow(1, "yksi", "yhtä", "yhden")]
    [DataRow(2, "kaksi", "kahta", "kahden")]
    [DataRow(21, "kaksikymmentäyksi", "kahtakymmentäyhtä", "kahdenkymmenenyhden")]
    [DataRow(100, "sata", "sataa", "sadan")]
    [DataRow(1000, "tuhat", "tuhatta", "tuhannen")]
    [DataRow(2000, "kaksituhatta", "kahtatuhatta", "kahdentuhannen")]
    [DataRow(21000, "kaksikymmentäyksituhatta", "kahtakymmentäyhtätuhatta", "kahdenkymmenenyhdentuhannen")]
    public void Finnish_Cases_SmallNumbersUnchanged(int value, string nominative, string partitive, string genitive)
    {
        Assert.AreEqual(nominative, FI.Convert(value));
        Assert.AreEqual(partitive, FI.Convert(value, "sijamuoto=partitiivi"));
        Assert.AreEqual(genitive, FI.Convert(value, "sijamuoto=genetiivi"));
    }

    /// <summary>
    /// The case forms of zero (Kielitoimiston sanakirja, nolla, inflection type 10: yks. gen. nollan, yks. part.
    /// nollaa; "Miljoonassa on kuusi nollaa").
    /// </summary>
    [TestMethod]
    public void Finnish_Zero()
    {
        Assert.AreEqual("nolla", FI.Convert(0));
        Assert.AreEqual("nolla", FI.Convert(0, "sijamuoto=nominatiivi"));
        Assert.AreEqual("nollaa", FI.Convert(0, "sijamuoto=partitiivi"));
        Assert.AreEqual("nollan", FI.Convert(0, "sijamuoto=genetiivi"));
        // Only the whole zero is concerned: no other cardinal contains the word.
        Assert.AreEqual("nolla pilkku viisi", FI.Convert(0.5m));
        Assert.AreEqual("kymmenen", FI.Convert(10, "sijamuoto=genetiivi"));
    }

    /// <summary>The composite surfaces reuse the unbounded cardinal.</summary>
    [TestMethod]
    public void Finnish_CompositeSurfaces()
    {
        Assert.AreEqual("miinus kaksi miljoonaa", FI.Convert(-2_000_000));
        Assert.AreEqual("miinus tuhat biljoonaa", FI.Convert(-P(15)));
        Assert.AreEqual("kaksi miljoonaa pilkku viisi", FI.Convert(2_000_000.5m));
        Assert.AreEqual("kaksi miljoonaa pilkku viisi seitsemän",
            FI.Convert(2_000_000.57m, -1, new DecimalFormatOptions { ReadingMode = DecimalReadingMode.DigitByDigit }));
        Assert.AreEqual("yksi yli miljoona", FI.ConvertFraction(1, 1_000_000));
        Assert.AreEqual("kaksi miljardia yli kolme", FI.Convert(new Utils.Numerics.Number(2_000_000_000, 3)));
        Assert.AreEqual("kaksituhatta kaksikymmentäyksi", FI.ConvertYear(2021));
        var euro = new CurrencyDefinition { UnitSingular = "euro", UnitPlural = "euroa", SubunitSingular = "sentti", SubunitPlural = "senttiä", Connector = "ja" };
        Assert.AreEqual("kaksi miljoonaa euroa", FI.ConvertCurrency(2_000_000m, euro));
        Assert.AreEqual("miljardi euroa", FI.ConvertCurrency(1_000_000_000m, euro));
        Assert.AreEqual("puoli kaksitoista", FI.ConvertClockTime(new TimeOnly(11, 30)));
    }

    // ===== Finnish: ordinals =========================================================================

    /// <summary>
    /// Kielitoimiston ohjepankki "järjestysluvut": the ordinal marker recurs in every part; "yhdes" and "kahdes"
    /// inside a larger number (also Kielitoimiston sanakirja, yhdes / kahdes: "järjestysluku suuremman luvun osana");
    /// "pitkät luvut ääneen luettaessa": 3 760. kolmastuhannes seitsemässadaskuudeskymmenes, 3 400. kolmastuhannes
    /// neljässadas (one space between groups, multiplier and tuhannes in one word); "järjestyslukujen taivuttaminen":
    /// 1 000 000:s miljoonas.
    /// </summary>
    [DataTestMethod]
    [DataRow(1000L, "tuhannes")]
    [DataRow(1001L, "tuhannes ensimmäinen")]
    [DataRow(1002L, "tuhannes toinen")]
    [DataRow(1500L, "tuhannes viidessadas")]
    [DataRow(1999L, "tuhannes yhdeksässadasyhdeksäskymmenesyhdeksäs")]
    [DataRow(2000L, "kahdestuhannes")]
    [DataRow(2001L, "kahdestuhannes ensimmäinen")]
    [DataRow(3000L, "kolmastuhannes")]
    [DataRow(3400L, "kolmastuhannes neljässadas")]
    [DataRow(3760L, "kolmastuhannes seitsemässadaskuudeskymmenes")]
    [DataRow(10000L, "kymmenestuhannes")]
    [DataRow(11000L, "yhdestoistatuhannes")]
    [DataRow(12000L, "kahdestoistatuhannes")]
    [DataRow(20000L, "kahdeskymmenestuhannes")]
    [DataRow(21000L, "kahdeskymmenesyhdestuhannes")]
    [DataRow(22000L, "kahdeskymmeneskahdestuhannes")]
    [DataRow(100000L, "sadastuhannes")]
    [DataRow(101000L, "sadasyhdestuhannes")]
    [DataRow(200000L, "kahdessadastuhannes")]
    [DataRow(999999L, "yhdeksässadasyhdeksäskymmenesyhdeksästuhannes yhdeksässadasyhdeksäskymmenesyhdeksäs")]
    [DataRow(1000000L, "miljoonas")]
    [DataRow(1000001L, "miljoonas ensimmäinen")]
    [DataRow(1003760L, "miljoonas kolmastuhannes seitsemässadaskuudeskymmenes")]
    [DataRow(1999999L, "miljoonas yhdeksässadasyhdeksäskymmenesyhdeksästuhannes yhdeksässadasyhdeksäskymmenesyhdeksäs")]
    public void Finnish_Ordinals(long value, string expected)
        => Assert.AreEqual(expected, FI.ConvertOrdinal(value));

    /// <summary>The ordinals below 1000 keep their NTS-08/NTS-28 forms.</summary>
    [DataTestMethod]
    [DataRow(0L, "nollas")]
    [DataRow(1L, "ensimmäinen")]
    [DataRow(2L, "toinen")]
    [DataRow(3L, "kolmas")]
    [DataRow(10L, "kymmenes")]
    [DataRow(11L, "yhdestoista")]
    [DataRow(12L, "kahdestoista")]
    [DataRow(19L, "yhdeksästoista")]
    [DataRow(20L, "kahdeskymmenes")]
    [DataRow(21L, "kahdeskymmenesensimmäinen")]
    [DataRow(22L, "kahdeskymmenestoinen")]
    [DataRow(55L, "viideskymmenesviides")]
    [DataRow(100L, "sadas")]
    [DataRow(101L, "sadasensimmäinen")]
    [DataRow(111L, "sadasyhdestoista")]
    [DataRow(121L, "sadaskahdeskymmenesensimmäinen")]
    [DataRow(200L, "kahdessadas")]
    [DataRow(500L, "viidessadas")]
    [DataRow(999L, "yhdeksässadasyhdeksäskymmenesyhdeksäs")]
    public void Finnish_Ordinals_BelowAThousandUnchanged(long value, string expected)
        => Assert.AreEqual(expected, FI.ConvertOrdinal(value));

    /// <summary>
    /// Deliberately fail-closed: a million multiplied by more than one (no source shows "kahdes miljoonas" or its
    /// spacing), the milliard and every higher scale (miljardis is absent from the Kielitoimiston sanakirja and the
    /// ohjepankki; no generated -ljoonas ordinal is attested), and every inflected ordinal.
    /// </summary>
    [TestMethod]
    public void Finnish_Ordinals_FailClosed()
    {
        foreach (long value in new[] { 2_000_000L, 2_000_001L, 21_000_000L, 999_999_999L, 1_000_000_000L, 1_000_000_000_000L, 1_000_000_000_000_000_000L })
            Assert.ThrowsExactly<NotSupportedException>(() => FI.ConvertOrdinal(value), value.ToString());
        foreach (long value in new[] { 3L, 2000L, 1_000_000L })
        {
            Assert.ThrowsExactly<NotSupportedException>(() => FI.ConvertOrdinal(value, "sijamuoto=genetiivi"), value.ToString());
            Assert.ThrowsExactly<NotSupportedException>(() => FI.ConvertOrdinal(value, "sijamuoto=partitiivi"), value.ToString());
        }
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => FI.ConvertOrdinal(P(30)));
    }
}
