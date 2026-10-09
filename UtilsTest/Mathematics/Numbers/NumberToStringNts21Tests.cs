using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-21 guards for the Wolof large scales. Only milyoŋ (10^6) and milyaar (10^9) are attested; their pair is the long
/// scale, which the library extends productively with the strict Conway-Guy-Wechsler tables spelled with Wolof letter
/// values (qu → kw, x → ks, v → w, soft c → s, hard c → k, -on → -yoŋ, -ard → -yaar). The higher names are a library
/// convention, so the tests pin the mechanism and the transliteration, not individually attested usage.
/// </summary>
[TestClass]
public class NumberToStringNts21Tests
{
    /// <summary>Variant query without any active dimension.</summary>
    private static readonly IReadOnlyDictionary<string, string> NoVariants = ImmutableDictionary<string, string>.Empty;

    /// <summary>Gets the Wolof converter.</summary>
    private static NumberToStringConverter Wolof => NumberToStringConverter.GetConverter("WO");

    /// <summary>
    /// Wolof Conway names keyed by Conway index n: the -yoŋ member (10^(6n)) and the -yaar member (10^(6n+3)) of each
    /// long-scale pair. Literal values, never recomputed by the test.
    /// </summary>
    private static readonly (long N, string Yon, string Yaar)[] Reference =
    [
        (1, "milyoŋ", "milyaar"), (2, "bilyoŋ", "bilyaar"), (3, "trilyoŋ", "trilyaar"),
        (4, "kwadrilyoŋ", "kwadrilyaar"), (5, "kwintilyoŋ", "kwintilyaar"), (6, "sekstilyoŋ", "sekstilyaar"),
        (7, "septilyoŋ", "septilyaar"), (8, "oktilyoŋ", "oktilyaar"), (9, "nonilyoŋ", "nonilyaar"),
        (10, "desilyoŋ", "desilyaar"), (11, "undesilyoŋ", "undesilyaar"), (14, "kwattuordesilyoŋ", "kwattuordesilyaar"),
        (16, "sedesilyoŋ", "sedesilyaar"), (17, "septendesilyoŋ", "septendesilyaar"), (19, "nowendesilyoŋ", "nowendesilyaar"),
        (20, "wigintilyoŋ", "wigintilyaar"), (23, "treswigintilyoŋ", "treswigintilyaar"),
        (26, "seswigintilyoŋ", "seswigintilyaar"), (29, "nowemwigintilyoŋ", "nowemwigintilyaar"),
        (30, "trigintilyoŋ", "trigintilyaar"), (40, "kwadragintilyoŋ", "kwadragintilyaar"),
        (50, "kwinkwagintilyoŋ", "kwinkwagintilyaar"), (60, "seksagintilyoŋ", "seksagintilyaar"),
        (70, "septuagintilyoŋ", "septuagintilyaar"), (80, "oktogintilyoŋ", "oktogintilyaar"),
        (86, "seksoktogintilyoŋ", "seksoktogintilyaar"), (90, "nonagintilyoŋ", "nonagintilyaar"),
        (99, "nowenonagintilyoŋ", "nowenonagintilyaar"), (100, "sentilyoŋ", "sentilyaar"),
        (103, "tressentilyoŋ", "tressentilyaar"), (106, "sekssentilyoŋ", "sekssentilyaar"),
        (107, "septensentilyoŋ", "septensentilyaar"), (109, "nowensentilyoŋ", "nowensentilyaar"),
        (130, "trigintasentilyoŋ", "trigintasentilyaar"), (200, "dusentilyoŋ", "dusentilyaar"),
        (306, "sestresentilyoŋ", "sestresentilyaar"), (806, "seksoktingentilyoŋ", "seksoktingentilyaar"),
        (999, "nowenonagintanongentilyoŋ", "nowenonagintanongentilyaar"),
    ];

    /// <summary>
    /// Grouped names above n = 999: groups joined by "li", an empty group "ni", the last group joined to the suffix by
    /// "l" (1000 = mi + li + ni + l + yoŋ). Literal values.
    /// </summary>
    private static readonly (long N, string Yon, string Yaar)[] GroupedReference =
    [
        (1000, "milinilyoŋ", "milinilyaar"), (1001, "milimilyoŋ", "milimilyaar"), (1003, "militrilyoŋ", "militrilyaar"),
        (1999, "milinowenonagintanongentilyoŋ", "milinowenonagintanongentilyaar"), (2000, "bilinilyoŋ", "bilinilyaar"),
        (1_000_003, "milinilitrilyoŋ", "milinilitrilyaar"),
        (11_000_670_036, "undesiliniliseptuagintasessentilisestrigintilyoŋ", "undesiliniliseptuagintasessentilisestrigintilyaar"),
    ];

    /// <summary>The Wolof scale names every Conway index up to 999 with the reference -yoŋ and -yaar names.</summary>
    [TestMethod]
    public void Wolof_ConwayNames_MatchReference()
    {
        var scale = Wolof.Scale;
        foreach (var (n, yon, yaar) in Reference)
        {
            Assert.AreEqual(yon, scale.BuildDynamicName(n, "yoŋ"), $"Conway n {n}");
            Assert.AreEqual(yaar, scale.BuildDynamicName(n, "yaar"), $"Conway n {n}");
        }
    }

    /// <summary>Above n = 999 the names follow the NTS-24 grouping: li between groups, ni for an empty one, l before the suffix.</summary>
    [TestMethod]
    public void Wolof_GroupedConwayNames_MatchReference()
    {
        var scale = Wolof.Scale;
        foreach (var (n, yon, yaar) in GroupedReference)
        {
            Assert.AreEqual(yon, scale.BuildDynamicName(n, "yoŋ"), $"Conway n {n}");
            Assert.AreEqual(yaar, scale.BuildDynamicName(n, "yaar"), $"Conway n {n}");
        }
    }

    /// <summary>
    /// Long scale: group index 2n (10^(6n)) is the -yoŋ name of Conway n and group index 2n + 1 its -yaar name; the
    /// thousand stays the static junni.
    /// </summary>
    [TestMethod]
    public void Wolof_ScaleIndices_FollowTheLongScale()
    {
        var scale = Wolof.Scale;

        Assert.AreEqual("", scale.GetScaleName(0));
        Assert.AreEqual("junni", scale.GetScaleName(1));
        foreach (var (n, yon, yaar) in Reference.Concat(GroupedReference).Where(r => 2 * r.N + 1 <= int.MaxValue))
        {
            Assert.AreEqual(yon, scale.GetScaleName((int)(2 * n)), $"group {2 * n}");
            Assert.AreEqual(yaar, scale.GetScaleName((int)(2 * n + 1)), $"group {2 * n + 1}");
        }
    }

    /// <summary>The maxNumber limit is gone: the Wolof scale is unbounded and names any group index.</summary>
    [TestMethod]
    public void Wolof_IsUnbounded()
    {
        var converter = Wolof;

        Assert.IsNull(converter.MaxNumber);
        Assert.IsTrue(converter.Scale.IsUnbounded);
        Assert.IsTrue(converter.Scale.CanNameGroup(int.MaxValue));
        Assert.AreEqual("benn milyoŋ", converter.Convert(BigInteger.Pow(10, 6)));
        Assert.AreEqual("benn sentilyaar", converter.Convert(BigInteger.Pow(10, 603)));
    }

    /// <summary>
    /// Alphabet invariant: no generated name contains q, v, x or c (Wolof letter values), none falls back to the Latin
    /// illi/illion/illiard spellings, and every name ends with yoŋ or yaar. Covers n 1..2100 and high grouped indices.
    /// </summary>
    [TestMethod]
    public void Wolof_GeneratedNames_UseWolofLetterValues()
    {
        var scale = Wolof.Scale;
        long[] high = [9_999, 10_000, 123_456, 999_999, 1_000_000, 1_000_003, 106_106_106, 999_999_999_999, long.MaxValue];
        var failures = new List<string>();
        foreach (long n in Enumerable.Range(1, 2100).Select(i => (long)i).Concat(high))
        {
            foreach (string suffix in new[] { "yoŋ", "yaar" })
            {
                string name = scale.BuildDynamicName(n, suffix);
                if (name.IndexOfAny(['q', 'v', 'x', 'c']) >= 0 || name.Contains("lli")
                    || !name.EndsWith("l" + suffix, StringComparison.Ordinal))
                    failures.Add($"{n}: {name}");
            }
        }

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures.Take(50)));
    }

    /// <summary>The Conway x is spelled ks and never reduced to k, s or x (n = 106 family, 86, 806, se(ks,s) before s).</summary>
    [TestMethod]
    public void Wolof_ConwayX_IsSpelledKs()
    {
        var scale = Wolof.Scale;

        Assert.AreEqual("sekssentilyoŋ", scale.BuildDynamicName(106, "yoŋ"));
        Assert.AreEqual("sekssentilyaar", scale.BuildDynamicName(106, "yaar"));
        Assert.AreEqual("milisekssentilyoŋ", scale.BuildDynamicName(1106, "yoŋ"));
        Assert.AreEqual("seksoktogintilyoŋ", scale.BuildDynamicName(86, "yoŋ"));
        Assert.AreEqual("seksoktingentilyoŋ", scale.BuildDynamicName(806, "yoŋ"));
        Assert.AreEqual("sekstilyoŋ", scale.BuildDynamicName(6, "yoŋ"));
        // se(ks,s) takes s before a component that accepts s only, and nothing before desi.
        Assert.AreEqual("seswigintilyoŋ", scale.BuildDynamicName(26, "yoŋ"));
        Assert.AreEqual("sedesilyoŋ", scale.BuildDynamicName(16, "yoŋ"));
    }

    /// <summary>The Wolof tables are the localized spellings of the strict Conway tables (marker structure kept).</summary>
    [TestMethod]
    public void Wolof_Tables_AreLocalizedConway()
    {
        var scale = Wolof.Scale;

        CollectionAssert.AreEqual(
            new[] { "", "mi", "bi", "tri", "kwadri", "kwinti", "seksti", "septi", "okti", "noni" }, scale.Scale0Prefixes!.ToArray());
        CollectionAssert.AreEqual(
            new[] { "", "un", "duo", "tre(s)", "kwattuor", "kwinkwa", "se(ks,s)", "septe(mn)", "okto", "nowe(mn)" },
            scale.UnitsPrefixes!.ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                "", "(n)desi", "(ms)wiginti", "(ns)trigint[a|-illi=>i]", "(ns)kwadragint[a|-illi=>i]",
                "(ns)kwinkwagint[a|-illi=>i]", "(n)seksagint[a|-illi=>i]", "(n)septuagint[a|-illi=>i]",
                "(m,ks,s)oktogint[a|-illi=>i]", "nonagint[a|-illi=>i]",
            },
            scale.TensPrefixes!.ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                "", "(n,ks,s)senti", "(n)dusenti", "(ns)tresenti", "(ns)kwadringenti", "(ns)kwingenti", "(n)sessenti",
                "(n)septingenti", "(m,ks,s)oktingenti", "nongenti",
            },
            scale.HundredsPrefixes!.ToArray());
        CollectionAssert.AreEqual(new[] { "yoŋ", "yaar" }, scale.ScaleSuffixes.ToArray());
        Assert.AreEqual("l", scale.SuffixSeparator);
    }

    /// <summary>
    /// Complete cardinals: "ak" before a lower part and the connective -i on the last element of a multiplier above one,
    /// as for téeméer and junni (NTS-19). Attested: benn, ñaari, juróomi, fukki milyoŋ (Wolof Bible wolmbs, Mt 25),
    /// juróom fukki milyoŋ and juróomi milyaar (Wolof Ajami Reader); the other values apply the rule productively.
    /// </summary>
    [TestMethod]
    [DataRow("1000000", "benn milyoŋ")]
    [DataRow("1000000000", "benn milyaar")]
    [DataRow("5000000", "juróomi milyoŋ")]
    [DataRow("50000000", "juróom fukki milyoŋ")]
    [DataRow("5000000000", "juróomi milyaar")]
    [DataRow("1000000000000", "benn bilyoŋ")]
    [DataRow("1000000000000000", "benn bilyaar")]
    [DataRow("2000000", "ñaari milyoŋ")]
    [DataRow("2000000000", "ñaari milyaar")]
    [DataRow("1000001", "benn milyoŋ ak benn")]
    [DataRow("1001000", "benn milyoŋ ak junni")]
    [DataRow("1001001", "benn milyoŋ ak junni ak benn")]
    [DataRow("2002000", "ñaari milyoŋ ak ñaari junni")]
    [DataRow("6000000", "juróom benni milyoŋ")]
    [DataRow("10000000", "fukki milyoŋ")]
    [DataRow("11000000", "fukk ak benni milyoŋ")]
    [DataRow("20000000", "ñaar fukki milyoŋ")]
    [DataRow("21000000", "ñaar fukk ak benni milyoŋ")]
    [DataRow("100000000", "téeméeri milyoŋ")]
    [DataRow("101000000", "téeméer ak benni milyoŋ")]
    [DataRow("200000000", "ñaari téeméeri milyoŋ")]
    [DataRow("1000000000000000000", "benn trilyoŋ")]
    [DataRow("3000000000000000000000", "ñetti trilyaar")]
    [DataRow("-2000000", "minus ñaari milyoŋ")]
    public void Wolof_Cardinals_FromAMillion(string number, string expected)
        => Assert.AreEqual(expected, Wolof.Convert(BigInteger.Parse(number)));

    /// <summary>
    /// Mechanical guard: for every multiplier 1..999 of milyoŋ and of milyaar, the text before the scale name equals the
    /// multiplier of junni (same -i rule), except that one is the bare "benn" (junni alone has no multiplier).
    /// </summary>
    [TestMethod]
    public void Wolof_MillionMultipliers_MatchTheThousandMultipliers()
    {
        var converter = Wolof;
        var failures = new List<string>();
        for (int multiplier = 1; multiplier <= 999; multiplier++)
        {
            string thousand = converter.Convert(multiplier * 1000L);
            string expected = multiplier == 1 ? "benn" : thousand[..^" junni".Length];
            foreach (var (power, name) in new[] { (6, "milyoŋ"), (9, "milyaar"), (12, "bilyoŋ") })
            {
                string text = converter.Convert(multiplier * BigInteger.Pow(10, power));
                if (text != expected + " " + name)
                    failures.Add($"{multiplier}×10^{power}: '{text}' (junni: '{thousand}')");
            }
        }

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures.Take(50)));
    }

    /// <summary>The cardinals below a million are unchanged by NTS-21 (sampled; the full 0..999999 sweep was dumped).</summary>
    [TestMethod]
    [DataRow(999_999L, "juróom ñenti téeméer ak juróom ñent fukk ak juróom ñenti junni ak juróom ñenti téeméer ak juróom ñent fukk ak juróom ñent")]
    [DataRow(1000L, "junni")]
    [DataRow(21_000L, "ñaar fukk ak benni junni")]
    [DataRow(200_000L, "ñaari téeméeri junni")]
    public void Wolof_CardinalsBelowAMillion_AreUnchanged(long number, string expected)
        => Assert.AreEqual(expected, Wolof.Convert(number));

    /// <summary>
    /// The ordinal domain stays 1..999999 (NTS-16/NTS-19): no ordinal from a million is established, so the guard
    /// rejects it although the cardinal now exists.
    /// </summary>
    [TestMethod]
    public void Wolof_Ordinals_FromAMillion_AreRejected()
    {
        var guard = new WolofOrdinalLanguageSpecifics();
        foreach (long rejected in new long[] { 1_000_000, 1_000_001, 2_000_000, 123_456_789, long.MaxValue })
        {
            Assert.Throws<NotSupportedException>(() => guard.TryConvertOrdinal(rejected, NoVariants, out _), rejected.ToString());
            Assert.Throws<NotSupportedException>(() => Wolof.ConvertOrdinal(rejected), rejected.ToString());
            Assert.Throws<NotSupportedException>(() => Wolof.ConvertOrdinal(-rejected), (-rejected).ToString());
        }
        Assert.IsFalse(guard.TryConvertOrdinal(999_999, NoVariants, out string? result));
        Assert.IsNull(result);
        Assert.AreEqual(
            "juróom ñenti téeméer ak juróom ñent fukk ak juróom ñenti junni ak juróom ñenti téeméer ak juróom ñent fukk ak juróom ñentéel",
            Wolof.ConvertOrdinal(999_999));
    }

    /// <summary>NTS-09: a baseOn="WO" child without its own Trigger inherits the Wolof -i trigger.</summary>
    [TestMethod]
    public void Nts09_BaseOnWolof_InheritsTheTrigger()
    {
        string culture = "X-NTS09-WO-" + Guid.NewGuid().ToString("N")[..8];
        string document = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
              <Language baseOn="WO"><Culture>{culture}</Culture></Language>
            </Numbers>
            """;
        var child = NumberToStringConverter.ReadConfiguration(document)[culture];

        Assert.AreEqual(Wolof.Triggers.Count, child.Triggers.Count);
        foreach (string number in new[] { "1000000", "2000000", "5000000", "50000000", "21000000", "5000000000", "999999" })
            Assert.AreEqual(Wolof.Convert(BigInteger.Parse(number)), child.Convert(BigInteger.Parse(number)), number);
        Assert.AreEqual("ñaari milyoŋ", child.Convert(2_000_000));
        Assert.AreEqual("juróom fukki milyoŋ", child.Convert(50_000_000));
    }

    /// <summary>NTS-09: without any Trigger a child inherits the base's triggers; with one it replaces the whole list.</summary>
    [TestMethod]
    public void Nts09_Triggers_InheritedWhenAbsent_ReplacedWhenDeclared()
    {
        string id = Guid.NewGuid().ToString("N")[..8];
        string parent = "X-NTS09-P-" + id, inheriting = "X-NTS09-I-" + id, replacing = "X-NTS09-R-" + id;
        string document = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
              <Language groupSize="3" separator=" " groupSeparator="" zero="zero" minus="minus *" decimalSeparator="point" maxNumber="9">
                <Culture>{parent}</Culture>
                <Groups>
                  <Group level="1"><Digit digit="0" string="" /><Digit digit="1" string="one" /><Digit digit="2" string="two" /><Digit digit="3" string="three" /><Digit digit="4" string="four" /><Digit digit="5" string="five" /><Digit digit="6" string="six" /><Digit digit="7" string="seven" /><Digit digit="8" string="eight" /><Digit digit="9" string="nine" /></Group>
                </Groups>
                <NumberScale firstLetterUpperCase="false"><StaticNames><Scale value="0" string="" /></StaticNames></NumberScale>
                <Trigger executeAt="end"><Replace from="one" to="ONE" /></Trigger>
              </Language>
              <Language baseOn="{parent}"><Culture>{inheriting}</Culture></Language>
              <Language baseOn="{parent}">
                <Culture>{replacing}</Culture>
                <Trigger executeAt="end"><Replace from="two" to="TWO" /></Trigger>
              </Language>
            </Numbers>
            """;
        var converters = NumberToStringConverter.ReadConfiguration(document);

        Assert.AreEqual("ONE", converters[parent].Convert(1));
        Assert.AreEqual("ONE", converters[inheriting].Convert(1));
        Assert.AreEqual("one", converters[replacing].Convert(1));
        Assert.AreEqual("TWO", converters[replacing].Convert(2));
    }

    /// <summary>suffixSeparator: absent, it is the group separator (every other language); set, it joins only the suffix.</summary>
    [TestMethod]
    public void SuffixSeparator_DefaultsToTheGroupSeparator()
    {
        string[] scale0 = ["", "mi", "bi", "tri", "kwadri", "kwinti", "seksti", "septi", "okti", "noni"];
        var plain = new NumberScale([], ["on"], groupSeparator: "lli", scale0Prefixes: scale0);
        var split = new NumberScale([], ["on"], groupSeparator: "lli", scale0Prefixes: scale0) { SuffixSeparator = "l" };
        var glued = new NumberScale([], ["on"], groupSeparator: "-", scale0Prefixes: scale0) { SuffixSeparator = "" };

        Assert.IsNull(plain.SuffixSeparator);
        Assert.AreEqual("million", plain.BuildDynamicName(1, "on"));
        Assert.AreEqual("millinillion", plain.BuildDynamicName(1000, "on"));
        Assert.AreEqual("milon", split.BuildDynamicName(1, "on"));
        Assert.AreEqual("millinilon", split.BuildDynamicName(1000, "on"));
        Assert.AreEqual("millitrilon", split.BuildDynamicName(1003, "on"));
        Assert.AreEqual("mion", glued.BuildDynamicName(1, "on"));
        Assert.AreEqual("mi-ni-trion", glued.BuildDynamicName(1_000_003, "on"));
        // The shipped languages other than Wolof keep the group separator before the suffix.
        Assert.IsNull(NumberToStringConverter.GetConverter("EN").Scale.SuffixSeparator);
        Assert.IsNull(NumberToStringConverter.GetConverter("TR").Scale.SuffixSeparator);
    }

    /// <summary>XML: suffixSeparator is read, inherited through baseOn, and overridden by an explicit child value (even empty).</summary>
    [TestMethod]
    public void SuffixSeparator_Xml_IsReadAndInherited()
    {
        string id = Guid.NewGuid().ToString("N")[..8];
        string parent = "X-NTS21-P-" + id, child = "X-NTS21-C-" + id, empty = "X-NTS21-E-" + id;
        string[] units = ["", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"];
        string level2 = string.Concat(units.Select((u, i) => i == 0
            ? """<Digit digit="0" string="" buildString="*" />"""
            : $"""<Digit digit="{i}" string="{u}ty" buildString="{u}ty *" />"""));
        string level3 = string.Concat(units.Select((u, i) => i == 0
            ? """<Digit digit="0" string="" buildString="*" />"""
            : $"""<Digit digit="{i}" string="{u} hundred" buildString="{u} hundred *" />"""));
        string document = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
              <Language groupSize="3" separator=" " groupSeparator="" zero="zero" minus="minus *" decimalSeparator="point" maxNumber="999999999999999">
                <Culture>{parent}</Culture>
                <Groups>
                  <Group level="1"><Digit digit="0" string="" /><Digit digit="1" string="one" /><Digit digit="2" string="two" /><Digit digit="3" string="three" /><Digit digit="4" string="four" /><Digit digit="5" string="five" /><Digit digit="6" string="six" /><Digit digit="7" string="seven" /><Digit digit="8" string="eight" /><Digit digit="9" string="nine" /></Group>
                  <Group level="2">{level2}</Group>
                  <Group level="3">{level3}</Group>
                </Groups>
                <NumberScale firstLetterUpperCase="false" groupSeparator="li" suffixSeparator="l">
                  <StaticNames><Scale value="0" string="" /><Scale value="1" string="thousand" /></StaticNames>
                  <Suffixes><Suffix>yoŋ</Suffix></Suffixes>
                  <Scale0Prefixes><Digit digit="0" string="" /><Digit digit="1" string="mi" /><Digit digit="2" string="bi" /><Digit digit="3" string="tri" /><Digit digit="4" string="kwadri" /><Digit digit="5" string="kwinti" /><Digit digit="6" string="seksti" /><Digit digit="7" string="septi" /><Digit digit="8" string="okti" /><Digit digit="9" string="noni" /></Scale0Prefixes>
                </NumberScale>
              </Language>
              <Language baseOn="{parent}">
                <Culture>{child}</Culture>
                <NumberScale><Suffixes><Suffix>on</Suffix></Suffixes></NumberScale>
              </Language>
              <Language baseOn="{parent}">
                <Culture>{empty}</Culture>
                <NumberScale suffixSeparator="" />
              </Language>
            </Numbers>
            """;
        var converters = NumberToStringConverter.ReadConfiguration(document);

        Assert.AreEqual("one milyoŋ", converters[parent].Convert(1_000_000));
        Assert.AreEqual("two bilyoŋ", converters[parent].Convert(2_000_000_000));
        Assert.AreEqual("milinilyoŋ", converters[parent].Scale.BuildDynamicName(1000, "yoŋ"));
        Assert.AreEqual("one milon", converters[child].Convert(1_000_000));
        Assert.AreEqual("militrilon", converters[child].Scale.BuildDynamicName(1003, "on"));
        Assert.AreEqual("one miyoŋ", converters[empty].Convert(1_000_000));
        Assert.AreEqual("militriyoŋ", converters[empty].Scale.BuildDynamicName(1003, "yoŋ"));
    }
}
