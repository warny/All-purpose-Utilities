using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Xml.Schema;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// Language-independent engine tests for the <c>&lt;Fusion&gt;</c> morphological composition primitive:
/// matching, edge transformations, cumulative specificity, load-time validation, <c>baseOn</c>
/// inheritance, immutability, and strict compatibility with configurations that declare no fusion.
/// </summary>
[TestClass]
public class NumberToStringFusionTests
{
    /// <summary>Default units used by the synthetic test language.</summary>
    private static readonly string[] DefaultUnits = ["", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"];

    /// <summary>Default tens digit 2 element: the historical buildString composes with a space.</summary>
    private const string DefaultTwenty = """<Digit digit="2" string="twenty" buildString="twenty *" />""";

    /// <summary>
    /// Builds a synthetic converter whose tens digit 2 (and optionally hundreds digit 1) can carry
    /// fusion rules, and returns the converter registered for <paramref name="culture"/>.
    /// </summary>
    /// <param name="twenty">The complete level-2 digit 2 element.</param>
    /// <param name="hundred">The complete level-3 digit 1 element, or <see langword="null"/> for a two-level language.</param>
    /// <param name="units">Optional unit strings overriding <see cref="DefaultUnits"/>.</param>
    /// <param name="eighty">Optional level-2 digit 8 element.</param>
    /// <param name="languageAttributes">Additional attributes appended to the Language element.</param>
    /// <param name="culture">The culture identifier of the synthetic language.</param>
    /// <returns>The configured converter.</returns>
    private static NumberToStringConverter Build(
        string twenty = DefaultTwenty,
        string? hundred = null,
        string[]? units = null,
        string? eighty = null,
        string languageAttributes = "",
        string? culture = null)
        => NumberToStringConverter.ReadConfiguration(Document(twenty, hundred, units, eighty, languageAttributes, culture ??= NewCulture()))[culture];

    /// <summary>Creates a unique culture name so tests never share registry state.</summary>
    private static string NewCulture() => $"FUSION-{Guid.NewGuid():N}";

    /// <summary>Creates the synthetic configuration document used by <see cref="Build"/>.</summary>
    private static string Document(
        string twenty,
        string? hundred,
        string[]? units,
        string? eighty,
        string languageAttributes,
        string culture,
        string extraLanguages = "")
    {
        units ??= DefaultUnits;
        string unitDigits = string.Concat(units.Select((u, i) => $"""<Digit digit="{i}" string="{u}" />"""));
        string tens = $"""
            <Digit digit="0" string="" buildString="*" />
            <Digit digit="1" string="ten" buildString="ten *" />
            {twenty}
            <Digit digit="3" string="thirty" buildString="thirty *" />
            <Digit digit="4" string="forty" buildString="forty *" />
            <Digit digit="5" string="fifty" buildString="fifty *" />
            <Digit digit="6" string="sixty" buildString="sixty *" />
            <Digit digit="7" string="seventy" buildString="seventy *" />
            {eighty ?? """<Digit digit="8" string="eighty" buildString="eighty *" />"""}
            <Digit digit="9" string="ninety" buildString="ninety *" />
            """;
        string hundreds = hundred is null ? "" : $"""
            <Group level="3">
              <Digit digit="0" string="" buildString="*" />
              {hundred}
              <Digit digit="2" string="two hundred" buildString="two hundred *" />
              <Digit digit="3" string="three hundred" buildString="three hundred *" />
              <Digit digit="4" string="four hundred" buildString="four hundred *" />
              <Digit digit="5" string="five hundred" buildString="five hundred *" />
              <Digit digit="6" string="six hundred" buildString="six hundred *" />
              <Digit digit="7" string="seven hundred" buildString="seven hundred *" />
              <Digit digit="8" string="eight hundred" buildString="eight hundred *" />
              <Digit digit="9" string="nine hundred" buildString="nine hundred *" />
            </Group>
            """;
        int groupSize = hundred is null ? 2 : 3;
        // Two static scale levels (units and "thousand") bound the language to two groups.
        string maxNumber = new string('9', 2 * groupSize);
        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
              <Language groupSize="{groupSize}" separator=" " groupSeparator="" zero="zero" minus="minus *" decimalSeparator="point" maxNumber="{maxNumber}"{languageAttributes}>
                <Culture>{culture}</Culture>
                <Groups>
                  <Group level="1">{unitDigits}</Group>
                  <Group level="2">{tens}</Group>
                  {hundreds}
                </Groups>
                <NumberScale firstLetterUpperCase="false"><StaticNames><Scale value="0" string="" /><Scale value="1" string="thousand" /></StaticNames><Suffixes><Suffix>illion</Suffix></Suffixes></NumberScale>
              </Language>
              {extraLanguages}
            </Numbers>
            """;
    }

    /// <summary>Converts <paramref name="number"/> as a cardinal.</summary>
    private static string Cardinal(NumberToStringConverter converter, long number) => converter.Convert((BigInteger)number);

    // ── Matching and composition ────────────────────────────────────────────────────────────────

    /// <summary>A matching rule concatenates the two constituents directly, bypassing buildString.</summary>
    [TestMethod]
    public void Fusion_MatchingRule_ConcatenatesConstituentsDirectly()
    {
        var converter = Build("""<Digit digit="2" string="twenty" buildString="twenty *"><Fusion for="1" /></Digit>""");

        Assert.AreEqual("twentyone", Cardinal(converter, 21));
    }

    /// <summary>Without a matching rule the historical buildString composition is used unchanged.</summary>
    [TestMethod]
    public void Fusion_NoMatchingRule_UsesHistoricalBuildString()
    {
        var converter = Build("""<Digit digit="2" string="twenty" buildString="twenty *"><Fusion for="1" /></Digit>""");

        Assert.AreEqual("twenty two", Cardinal(converter, 22));
        Assert.AreEqual("twenty", Cardinal(converter, 20));
        Assert.AreEqual("thirty one", Cardinal(converter, 31));
    }

    /// <summary><c>removeLeft</c> strips the configured suffix of the left constituent.</summary>
    [TestMethod]
    public void Fusion_RemoveLeft_StripsSuffixOfLeftConstituent()
    {
        string[] units = [.. DefaultUnits];
        units[3] = "def";
        var converter = Build("""<Digit digit="2" string="abcX" buildString="abcX *"><Fusion for="3" removeLeft="X" /></Digit>""", units: units);

        Assert.AreEqual("abcdef", Cardinal(converter, 23));
        Assert.AreEqual("abcX", Cardinal(converter, 20));
    }

    /// <summary><c>removeRight</c> strips the configured prefix of the right constituent.</summary>
    [TestMethod]
    public void Fusion_RemoveRight_StripsPrefixOfRightConstituent()
    {
        string[] units = [.. DefaultUnits];
        units[3] = "Xdef";
        var converter = Build("""<Digit digit="2" string="abc" buildString="abc *"><Fusion for="3" removeRight="X" /></Digit>""", units: units);

        Assert.AreEqual("abcdef", Cardinal(converter, 23));
        Assert.AreEqual("Xdef", Cardinal(converter, 3));
    }

    /// <summary><c>left</c> replaces the left constituent for the fused junction only.</summary>
    [TestMethod]
    public void Fusion_LeftOverride_ReplacesLeftConstituent()
    {
        var converter = Build("""<Digit digit="2" string="abc" buildString="abc *"><Fusion for="1" left="XYZ" /></Digit>""");

        Assert.AreEqual("XYZone", Cardinal(converter, 21));
        Assert.AreEqual("abc", Cardinal(converter, 20));
        Assert.AreEqual("abc two", Cardinal(converter, 22));
    }

    /// <summary><c>right</c> replaces the right constituent for the fused junction only.</summary>
    [TestMethod]
    public void Fusion_RightOverride_ReplacesRightConstituentOnlyInsideTheCompound()
    {
        var converter = Build("""<Digit digit="2" string="twenty" buildString="twenty *"><Fusion for="3" right="tré" /></Digit>""");

        Assert.AreEqual("twentytré", Cardinal(converter, 23));
        Assert.AreEqual("three", Cardinal(converter, 3));
    }

    /// <summary>An override is applied before the matching removal on the same edge.</summary>
    [TestMethod]
    public void Fusion_OverrideThenRemoval_OnSameEdge()
    {
        var converter = Build("""<Digit digit="2" string="abc" buildString="abc *"><Fusion for="1" left="XYZ" removeLeft="Z" /></Digit>""");

        Assert.AreEqual("XYone", Cardinal(converter, 21));
    }

    // ── Cumulative rules and specificity ────────────────────────────────────────────────────────

    /// <summary>
    /// Applicable rules accumulate from the least to the most specific; a specific rule only
    /// completes the properties it declares.
    /// </summary>
    [TestMethod]
    public void Fusion_CumulativeRules_SpecificRuleCompletesGeneralRule()
    {
        var converter = Build("""
            <Digit digit="2" string="twenty" buildString="twenty *">
              <Fusion for="1..9" />
              <Fusion for="1,8" removeLeft="y" />
              <Fusion for="3" right="tree" />
            </Digit>
            """);

        Assert.AreEqual("twentone", Cardinal(converter, 21));
        Assert.AreEqual("twenteight", Cardinal(converter, 28));
        Assert.AreEqual("twentytree", Cardinal(converter, 23));
        Assert.AreEqual("twentyfive", Cardinal(converter, 25));
        Assert.AreEqual("twenty", Cardinal(converter, 20));
    }

    /// <summary>Specificity, not XML order, decides precedence.</summary>
    [TestMethod]
    public void Fusion_Precedence_IsIndependentOfXmlOrder()
    {
        var converter = Build("""
            <Digit digit="2" string="twenty" buildString="twenty *">
              <Fusion for="3" right="tree" />
              <Fusion for="1,8" removeLeft="y" />
              <Fusion for="1..9" right="X" />
            </Digit>
            """);

        Assert.AreEqual("twentytree", Cardinal(converter, 23));
        Assert.AreEqual("twentX", Cardinal(converter, 21));
        Assert.AreEqual("twentyX", Cardinal(converter, 25));
    }

    /// <summary>A more specific rule overrides a property already set by a less specific one.</summary>
    [TestMethod]
    public void Fusion_MoreSpecificRule_OverridesSameProperty()
    {
        var converter = Build("""
            <Digit digit="2" string="twenty" buildString="twenty *">
              <Fusion for="1..9" right="general" />
              <Fusion for="4" right="specific" />
            </Digit>
            """);

        Assert.AreEqual("twentyspecific", Cardinal(converter, 24));
        Assert.AreEqual("twentygeneral", Cardinal(converter, 25));
    }

    /// <summary>
    /// Crossing ranges (overlapping, neither containing the other) cannot override one another,
    /// even when one is smaller: only a nested range may override a general one.
    /// </summary>
    [TestMethod]
    [DataRow("right")]
    [DataRow("left")]
    [DataRow("removeLeft")]
    [DataRow("removeRight")]
    public void Fusion_CrossingOverlappingRangesOnSameProperty_AreRejected(string attribute)
    {
        string twenty = $"""
            <Digit digit="2" string="twenty" buildString="twenty *">
              <Fusion for="1..5" {attribute}="a" />
              <Fusion for="4..6" {attribute}="b" />
            </Digit>
            """;

        var exception = Assert.Throws<ArgumentException>(() => Build(twenty));
        StringAssert.Contains(exception.Message, "Fusion");
        StringAssert.Contains(exception.Message, attribute);
    }

    /// <summary>Crossing ranges that agree on a shared property are not an override and stay accepted.</summary>
    [TestMethod]
    public void Fusion_CrossingRangesWithIdenticalValues_AreAccepted()
    {
        var converter = Build("""
            <Digit digit="2" string="twenty" buildString="twenty *">
              <Fusion for="1..5" right="x" />
              <Fusion for="4..6" right="x" />
            </Digit>
            """);

        Assert.AreEqual("twentyx", Cardinal(converter, 21));
        Assert.AreEqual("twentyx", Cardinal(converter, 25));
        Assert.AreEqual("twentyx", Cardinal(converter, 26));
        Assert.AreEqual("twenty seven", Cardinal(converter, 27));
    }

    /// <summary>A nested non-contiguous range overrides the range that contains it.</summary>
    [TestMethod]
    public void Fusion_NestedRange_OverridesContainingRange()
    {
        var converter = Build("""
            <Digit digit="2" string="twenty" buildString="twenty *">
              <Fusion for="1..5" right="a" />
              <Fusion for="2,4" right="b" />
            </Digit>
            """);

        Assert.AreEqual("twentya", Cardinal(converter, 21));
        Assert.AreEqual("twentyb", Cardinal(converter, 22));
        Assert.AreEqual("twentyb", Cardinal(converter, 24));
        Assert.AreEqual("twenty six", Cardinal(converter, 26));
    }

    /// <summary>Two equally specific rules assigning different values to one property are rejected.</summary>
    [TestMethod]
    [DataRow("right")]
    [DataRow("left")]
    [DataRow("removeLeft")]
    [DataRow("removeRight")]
    public void Fusion_EquallySpecificConflict_IsRejectedAtLoad(string attribute)
    {
        string twenty = $"""
            <Digit digit="2" string="twenty" buildString="twenty *">
              <Fusion for="1,2" {attribute}="a" />
              <Fusion for="2,3" {attribute}="b" />
            </Digit>
            """;

        var exception = Assert.Throws<ArgumentException>(() => Build(twenty));
        StringAssert.Contains(exception.Message, "Fusion");
    }

    /// <summary>Equally specific overlapping rules that set different properties are compatible.</summary>
    [TestMethod]
    public void Fusion_EquallySpecificRulesOnDifferentProperties_AreCompatible()
    {
        var converter = Build("""
            <Digit digit="2" string="twenty" buildString="twenty *">
              <Fusion for="1,2" removeLeft="y" />
              <Fusion for="2,3" right="b" />
            </Digit>
            """);

        Assert.AreEqual("twentb", Cardinal(converter, 22));
        Assert.AreEqual("twentone", Cardinal(converter, 21));
        Assert.AreEqual("twentyb", Cardinal(converter, 23));
    }

    /// <summary>A fusion on the hundreds digit sees the complete two-digit remainder.</summary>
    [TestMethod]
    public void Fusion_HundredsDigit_MatchesTwoDigitRemainder()
    {
        var converter = Build(
            hundred: """<Digit digit="1" string="cento" buildString="cento *"><Fusion for="80..89" removeLeft="o" /></Digit>""",
            eighty: """<Digit digit="8" string="ottanta" buildString="ottanta *" />""");

        Assert.AreEqual("centottanta", Cardinal(converter, 180));
        Assert.AreEqual("centottanta one", Cardinal(converter, 181));
        Assert.AreEqual("cento eight", Cardinal(converter, 108));
        Assert.AreEqual("cento", Cardinal(converter, 100));
    }

    // ── Load-time validation ────────────────────────────────────────────────────────────────────

    /// <summary>The <c>for</c> attribute is mandatory.</summary>
    [TestMethod]
    public void Fusion_MissingFor_IsRejectedBySchema()
        => Assert.ThrowsExactly<XmlSchemaValidationException>(
            () => Build("""<Digit digit="2" string="twenty" buildString="twenty *"><Fusion right="x" /></Digit>"""));

    /// <summary>A range outside the remainder domain of the group can never match and is rejected.</summary>
    [TestMethod]
    [DataRow("0")]
    [DataRow("10")]
    [DataRow("5..")]
    [DataRow("..3")]
    public void Fusion_RangeOutsideRemainderDomain_IsRejected(string range)
        => Assert.Throws<ArgumentException>(
            () => Build($"""<Digit digit="2" string="twenty" buildString="twenty *"><Fusion for="{range}" /></Digit>"""));

    /// <summary>A malformed range is rejected.</summary>
    [TestMethod]
    public void Fusion_MalformedRange_IsRejected()
        => Assert.Throws<Exception>(
            () => Build("""<Digit digit="2" string="twenty" buildString="twenty *"><Fusion for="a..b" /></Digit>"""));

    /// <summary>Empty attribute values are not meaningful and are rejected by the schema.</summary>
    [TestMethod]
    [DataRow("removeLeft")]
    [DataRow("removeRight")]
    [DataRow("left")]
    [DataRow("right")]
    public void Fusion_EmptyAttribute_IsRejectedBySchema(string attribute)
        => Assert.ThrowsExactly<XmlSchemaValidationException>(
            () => Build($"""<Digit digit="2" string="twenty" buildString="twenty *"><Fusion for="1" {attribute}="" /></Digit>"""));

    /// <summary>Empty edge values supplied programmatically are rejected by the converter.</summary>
    [TestMethod]
    [DataRow("removeLeft")]
    [DataRow("removeRight")]
    [DataRow("left")]
    [DataRow("right")]
    public void Fusion_EmptyProgrammaticValue_IsRejected(string attribute)
    {
        var fusion = new FusionType { For = "1" };
        switch (attribute)
        {
            case "removeLeft": fusion.RemoveLeft = ""; break;
            case "removeRight": fusion.RemoveRight = ""; break;
            case "left": fusion.Left = ""; break;
            default: fusion.Right = ""; break;
        }

        Assert.Throws<ArgumentException>(() => new NumberToStringConverter(ProgrammaticOptions([fusion])));
    }

    /// <summary>A programmatic rule without a range is rejected.</summary>
    [TestMethod]
    public void Fusion_ProgrammaticMissingFor_IsRejected()
        => Assert.Throws<ArgumentException>(() => new NumberToStringConverter(ProgrammaticOptions([new FusionType { Right = "x" }])));

    /// <summary>Two rules with the same canonical range on one digit are rejected.</summary>
    [TestMethod]
    public void Fusion_DuplicateFor_IsRejected()
        => Assert.Throws<ArgumentException>(() => Build("""
            <Digit digit="2" string="twenty" buildString="twenty *">
              <Fusion for="1..2" />
              <Fusion for="1,2" right="x" />
            </Digit>
            """));

    /// <summary>A level-1 digit has no lower constituent: a fusion there is rejected.</summary>
    [TestMethod]
    public void Fusion_OnUnitsGroup_IsRejected()
    {
        string culture = NewCulture();
        string document = Document(DefaultTwenty, null, null, null, "", culture)
            .Replace("""<Digit digit="1" string="one" />""", """<Digit digit="1" string="one"><Fusion for="1" /></Digit>""", StringComparison.Ordinal);

        Assert.Throws<ArgumentException>(() => NumberToStringConverter.ReadConfiguration(document));
    }

    /// <summary><c>removeLeft</c> must name a suffix the left constituent actually has.</summary>
    [TestMethod]
    public void Fusion_RemoveLeftAbsentSuffix_IsRejectedAtLoad()
        => Assert.Throws<ArgumentException>(
            () => Build("""<Digit digit="2" string="twenty" buildString="twenty *"><Fusion for="1" removeLeft="q" /></Digit>"""));

    /// <summary><c>removeRight</c> must name a prefix every matched right constituent actually has.</summary>
    [TestMethod]
    public void Fusion_RemoveRightAbsentPrefix_IsRejectedAtLoad()
        => Assert.Throws<ArgumentException>(
            () => Build("""<Digit digit="2" string="twenty" buildString="twenty *"><Fusion for="1..2" removeRight="o" /></Digit>"""));

    /// <summary>A fusion may not compete with the intra-group connector on the same junction.</summary>
    [TestMethod]
    public void Fusion_OverlappingIntraGroupConnector_IsRejected()
        => Assert.Throws<ArgumentException>(() => Build(
            hundred: """<Digit digit="1" string="hundred" buildString="hundred *"><Fusion for="5" /></Digit>""",
            languageAttributes: """ intraGroupConnector="and" intraGroupConnectorThreshold="100" """));

    /// <summary>A fusion outside the intra-group connector range is accepted alongside the connector.</summary>
    [TestMethod]
    public void Fusion_OutsideIntraGroupConnectorRange_IsAccepted()
    {
        var converter = Build(
            hundred: """<Digit digit="1" string="hundred" buildString="hundred *"><Fusion for="50..59" /></Digit>""",
            languageAttributes: """ intraGroupConnector="and" intraGroupConnectorThreshold="20" """);

        Assert.AreEqual("hundredfifty", Cardinal(converter, 150));
        Assert.AreEqual("hundred and five", Cardinal(converter, 105));
    }

    /// <summary>Fusion is a group-composition primitive: prefix tables reject it.</summary>
    [TestMethod]
    public void Fusion_InScalePrefixTable_IsRejected()
    {
        string culture = NewCulture();
        string prefixes = string.Concat(Enumerable.Range(0, 10).Select(i => i == 1
            ? """<Digit digit="1" string="m"><Fusion for="1" /></Digit>"""
            : $"""<Digit digit="{i}" string="p{i}" />"""));
        string document = Document(DefaultTwenty, null, null, null, "", culture)
            .Replace("</Suffixes>", $"</Suffixes><UnitsPrefixes>{prefixes}</UnitsPrefixes>", StringComparison.Ordinal);

        var exception = Assert.Throws<Exception>(() => NumberToStringConverter.ReadConfiguration(document));
        StringAssert.Contains(exception.Message, "Fusion");
    }

    // ── Programmatic configuration, cloning and immutability ────────────────────────────────────

    /// <summary>Creates programmatic options equivalent to the synthetic XML language.</summary>
    private static NumberToStringConverterOptions ProgrammaticOptions(List<FusionType> fusions)
    {
        var units = new DigitListType { Digits = [.. DefaultUnits.Select((u, i) => new DigitType(i, u))] };
        var tens = new DigitListType
        {
            Digits =
            [
                new DigitType(0, "", "*"),
                new DigitType(1, "ten", "ten *"),
                new DigitType(2, "twenty", "twenty *") { Fusions = fusions },
                new DigitType(3, "thirty", "thirty *"),
                new DigitType(4, "forty", "forty *"),
                new DigitType(5, "fifty", "fifty *"),
                new DigitType(6, "sixty", "sixty *"),
                new DigitType(7, "seventy", "seventy *"),
                new DigitType(8, "eighty", "eighty *"),
                new DigitType(9, "ninety", "ninety *"),
            ],
        };
        return new NumberToStringConverterOptions
        {
            Group = 2,
            Zero = "zero",
            Minus = "minus *",
            Groups = new Dictionary<int, DigitListType> { [1] = units, [2] = tens },
            Scale = new NumberScale(["", "thousand"], ["illion"]),
        };
    }

    /// <summary>Fusion rules supplied through options are compiled like XML rules.</summary>
    [TestMethod]
    public void Fusion_ProgrammaticOptions_AreHonoured()
    {
        var converter = new NumberToStringConverter(ProgrammaticOptions([new FusionType { For = "1", RemoveLeft = "y" }]));

        Assert.AreEqual("twentone", Cardinal(converter, 21));
        Assert.AreEqual("twenty two", Cardinal(converter, 22));
    }

    /// <summary>Cloning a converter through its options preserves its fusion behaviour.</summary>
    [TestMethod]
    public void Fusion_OptionsClone_PreservesFusions()
    {
        var original = Build("""<Digit digit="2" string="twenty" buildString="twenty *"><Fusion for="1" removeLeft="y" /></Digit>""");

        var clone = new NumberToStringConverter(new NumberToStringConverterOptions(original));

        Assert.AreEqual("twentone", Cardinal(clone, 21));
        Assert.AreEqual("twenty two", Cardinal(clone, 22));
    }

    // ── baseOn inheritance ──────────────────────────────────────────────────────────────────────

    /// <summary>A child that does not redefine Groups inherits the parent's fusion rules.</summary>
    [TestMethod]
    public void Fusion_BaseOnChildWithoutGroups_InheritsParentFusions()
    {
        string parent = NewCulture();
        string child = NewCulture();
        string document = Document(
            """<Digit digit="2" string="twenty" buildString="twenty *"><Fusion for="1" removeLeft="y" /></Digit>""",
            null, null, null, "", parent,
            $"""<Language baseOn="{parent}"><Culture>{child}</Culture></Language>""");

        var converters = NumberToStringConverter.ReadConfiguration(document);

        Assert.AreEqual("twentone", Cardinal(converters[child], 21));
    }

    /// <summary>
    /// Groups are replaced as a whole by a child that redefines them, so the parent's fusion rules
    /// are not merged into the child's redefined digits (the existing Groups merge rule).
    /// </summary>
    [TestMethod]
    public void Fusion_BaseOnChildRedefiningGroups_ReplacesParentFusions()
    {
        string parent = NewCulture();
        string child = NewCulture();
        string childGroups = Document(
            """<Digit digit="2" string="twenty" buildString="twenty *"><Fusion for="2" /></Digit>""",
            null, null, null, "", "unused");
        int start = childGroups.IndexOf("<Groups>", StringComparison.Ordinal);
        int end = childGroups.IndexOf("</Groups>", StringComparison.Ordinal) + "</Groups>".Length;
        string document = Document(
            """<Digit digit="2" string="twenty" buildString="twenty *"><Fusion for="1" /></Digit>""",
            null, null, null, "", parent,
            $"""<Language baseOn="{parent}"><Culture>{child}</Culture>{childGroups[start..end]}</Language>""");

        var converters = NumberToStringConverter.ReadConfiguration(document);

        Assert.AreEqual("twentyone", Cardinal(converters[parent], 21));
        Assert.AreEqual("twenty one", Cardinal(converters[child], 21));
        Assert.AreEqual("twentytwo", Cardinal(converters[child], 22));
    }

    // ── Compatibility ───────────────────────────────────────────────────────────────────────────

    /// <summary>A self-closing digit without fusion keeps the exact historical composition.</summary>
    [TestMethod]
    public void Fusion_Absent_KeepsHistoricalComposition()
    {
        var converter = Build();

        Assert.AreEqual("twenty one", Cardinal(converter, 21));
        Assert.AreEqual("ninety nine", Cardinal(converter, 99));
        Assert.AreEqual("twenty thousand twenty one", Cardinal(converter, 2021));
    }

    /// <summary>Fused groups keep composing with scale names through the ordinary scale pipeline.</summary>
    [TestMethod]
    public void Fusion_ComposesWithScaleNamesThroughTheOrdinaryPipeline()
    {
        var converter = Build("""<Digit digit="2" string="twenty" buildString="twenty *"><Fusion for="1" /></Digit>""");

        Assert.AreEqual("twentyone thousand twentyone", Cardinal(converter, 2121));
    }
}
