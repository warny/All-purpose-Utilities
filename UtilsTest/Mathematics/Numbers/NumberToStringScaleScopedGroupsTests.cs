using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// Language-independent engine tests for the two NTS-20 primitives: <c>&lt;Groups onScale&gt;</c> (the
/// digit tables used to render the multiplier of the covered scales) and <c>multiplierPosition</c> (the
/// order of the multiplier and the scale noun). Covers default behaviour, table selection, ranges,
/// validation, the interaction with replacements, variant rules, fusions and triggers, ordinals,
/// programmatic configuration, cloning and <c>baseOn</c>.
/// </summary>
[TestClass]
public class NumberToStringScaleScopedGroupsTests
{
    /// <summary>Unit words of the synthetic standalone cardinal.</summary>
    private static readonly string[] Units = ["", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"];

    /// <summary>Tens words of the synthetic standalone cardinal.</summary>
    private static readonly string[] Tens = ["", "ten", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

    /// <summary>Unit words of the first scoped multiplier table.</summary>
    private static readonly string[] AlphaUnits = ["", "alpha", "beta", "gamma", "delta", "epsilon", "zeta", "eta", "theta", "iota"];

    /// <summary>Unit words of the second scoped multiplier table.</summary>
    private static readonly string[] UpperUnits = ["", "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE"];

    /// <summary>Creates a unique culture name.</summary>
    private static string NewCulture() => $"SCOPED-{Guid.NewGuid():N}";

    /// <summary>Builds the three levels of a group table whose units are <paramref name="units"/>.</summary>
    /// <param name="units">The unit words (index = digit).</param>
    /// <param name="tensSuffix">A suffix appended to every tens word, to tell the tables apart.</param>
    /// <returns>The level 1..3 digit tables.</returns>
    private static Dictionary<int, DigitListType> Table(string[] units, string tensSuffix = "")
        => new()
        {
            [1] = new DigitListType { Digits = [.. units.Select((u, i) => new DigitType(i, u))] },
            [2] = new DigitListType
            {
                Digits = [.. Tens.Select((t, i) => i == 0
                    ? new DigitType(0, "", "*")
                    : new DigitType(i, t + tensSuffix, t + tensSuffix + " *"))],
            },
            [3] = new DigitListType
            {
                Digits = [.. units.Select((u, i) => i == 0
                    ? new DigitType(0, "", "*")
                    : new DigitType(i, u + " hundred", u + " hundred *"))],
            },
        };

    /// <summary>Creates programmatic options for the synthetic language (scales thousand to trillion).</summary>
    /// <param name="scoped">The scale-scoped group sets.</param>
    /// <param name="position">The multiplier position.</param>
    /// <returns>The options.</returns>
    private static NumberToStringConverterOptions Options(IReadOnlyList<ScaleScopedGroups>? scoped = null, ScaleMultiplierPosition position = ScaleMultiplierPosition.BeforeScale)
        => new()
        {
            Group = 3,
            Zero = "zero",
            Minus = "minus *",
            Groups = Table(Units),
            ScaleScopedGroups = scoped ?? [],
            ScaleMultiplierPosition = position,
            Scale = new NumberScale(["", "thousand", "million", "billion", "trillion"], []),
            MaxNumber = 999_999_999_999_999,
            OrdinalSuffix = "th",
            OrdinalExceptions = new Dictionary<long, string> { [1] = "first" },
        };

    /// <summary>Converts <paramref name="value"/> with <paramref name="converter"/>.</summary>
    private static string C(NumberToStringConverter converter, long value) => converter.Convert(new BigInteger(value));

    /// <summary>Values exercising every group, connector and scale of the synthetic language.</summary>
    private static readonly long[] SweepValues =
        [0, 1, 2, 9, 10, 11, 21, 99, 100, 101, 121, 999, 1000, 1001, 2000, 21000, 99999, 100000, 234567, 1000000, 2000000, 21021021, 999999999, 1000000000, 3000000000000];

    /// <summary>Without scoped groups the conversion is unchanged, and an empty scoped list is identical to none.</summary>
    [TestMethod]
    public void NoScopedGroups_EmptyListIsIdenticalToHistoricalBehaviour()
    {
        var historical = new NumberToStringConverter(Options());
        var empty = new NumberToStringConverter(Options([]));

        Assert.AreEqual("two thousand", C(historical, 2000));
        Assert.AreEqual("twenty one thousand twenty one", C(historical, 21021));
        foreach (long value in SweepValues)
            Assert.AreEqual(C(historical, value), C(empty, value), $"value {value}");
        Assert.AreEqual(0, empty.ScaleScopedGroups.Count);
        Assert.AreEqual(ScaleMultiplierPosition.BeforeScale, empty.ScaleMultiplierPosition);
    }

    /// <summary>A scoped set renders the multiplier of its scale only; the standalone cardinal keeps the default table.</summary>
    [TestMethod]
    public void ScopedGroups_RenderOnlyTheMultiplierOfTheCoveredScale()
    {
        var converter = new NumberToStringConverter(Options([new ScaleScopedGroups("1", Table(AlphaUnits, "'"))]));

        Assert.AreEqual("one", C(converter, 1));
        Assert.AreEqual("two", C(converter, 2));
        Assert.AreEqual("alpha thousand", C(converter, 1000));
        Assert.AreEqual("beta thousand", C(converter, 2000));
        // The scoped table serves the whole multiplier (tens, hundreds), not only its units.
        Assert.AreEqual("twenty' alpha thousand", C(converter, 21000));
        Assert.AreEqual("beta hundred thirty' delta thousand two", C(converter, 234002));
        // Scale 2 is not covered: back to the default table.
        Assert.AreEqual("two million beta thousand", C(converter, 2002000));
    }

    /// <summary>Each scale index selects the scoped set whose range contains it, otherwise the default table.</summary>
    [TestMethod]
    public void ScopedGroups_RangesSelectTheTablePerScale()
    {
        var converter = new NumberToStringConverter(Options([
            new ScaleScopedGroups("1", Table(AlphaUnits)),
            new ScaleScopedGroups("2..4", Table(UpperUnits)),
        ]));

        Assert.AreEqual("two", C(converter, 2));
        Assert.AreEqual("beta thousand", C(converter, 2000));
        Assert.AreEqual("TWO million", C(converter, 2_000_000));
        Assert.AreEqual("TWO billion", C(converter, 2_000_000_000));
        Assert.AreEqual("TWO trillion beta thousand two", C(converter, 2_000_000_002_002));
    }

    /// <summary>An open range covers every scale from its lower bound, including those beyond the lookup table.</summary>
    [TestMethod]
    public void ScopedGroups_OpenRangeCoversEveryHigherScale()
    {
        var options = Options([new ScaleScopedGroups("2..", Table(UpperUnits))]);
        options.Scale = new NumberScale(["", "thousand", "million", .. Enumerable.Range(3, 70).Select(i => $"scale{i}")], []);
        options.MaxNumber = null;
        var converter = new NumberToStringConverter(options);

        Assert.AreEqual("two thousand", C(converter, 2000));
        Assert.AreEqual("TWO million", C(converter, 2_000_000));
        // Scale index 70 lies beyond the precompiled lookup and is resolved through the ranges.
        string huge = converter.Convert(BigInteger.Pow(1000, 70) * 2);
        Assert.IsTrue(huge.StartsWith("TWO ", StringComparison.Ordinal), huge);
    }

    /// <summary>Invalid scoped configurations are rejected at construction with a precise diagnostic.</summary>
    [TestMethod]
    public void ScopedGroups_InvalidConfigurationsAreRejected()
    {
        AssertRejected([new ScaleScopedGroups("1..3", Table(AlphaUnits)), new ScaleScopedGroups("3..5", Table(UpperUnits))], "overlaps");
        AssertRejected([new ScaleScopedGroups("1", Table(AlphaUnits)), new ScaleScopedGroups("1", Table(UpperUnits))], "same range");
        AssertRejected([new ScaleScopedGroups("0..1", Table(AlphaUnits))], "at least 1");
        // An open lower bound covers zero and the negative indices.
        AssertRejected([new ScaleScopedGroups("..2", Table(AlphaUnits))], "at least 1");
        AssertRejected([new ScaleScopedGroups("", Table(AlphaUnits))], "range is required");
        // The shared range syntax reads unparsable bounds as an empty range.
        AssertRejected([new ScaleScopedGroups("x..y", Table(AlphaUnits))], "covers no scale index");
        AssertRejected([null!], "must not be null");
        AssertRejected([new ScaleScopedGroups("1", null!)], "Groups");
        var twoLevels = Table(AlphaUnits);
        twoLevels.Remove(3);
        AssertRejected([new ScaleScopedGroups("1", twoLevels)], "same group levels");
        var duplicateDigit = Table(AlphaUnits);
        duplicateDigit[1].Digits.Add(new DigitType(2, "again"));
        AssertRejected([new ScaleScopedGroups("1", duplicateDigit)], "duplicate digit value 2");

        static void AssertRejected(IReadOnlyList<ScaleScopedGroups> scoped, string expectedFragment)
        {
            var exception = Assert.Throws<ArgumentException>(() => new NumberToStringConverter(Options(scoped)));
            StringAssert.Contains(exception.Message, expectedFragment);
            StringAssert.Contains(exception.Message, "ScaleScopedGroups");
        }
    }

    /// <summary>Replacements with onScale apply after the scoped table, with onValue on the numeric multiplier.</summary>
    [TestMethod]
    public void ScopedGroups_ReplacementsRunAfterTheScopedTable()
    {
        var options = Options([new ScaleScopedGroups("1", Table(AlphaUnits))]);
        options.Replacements = [
            new NumberToStringConverter.ReplacementRule("alpha thousand", "thousand", ReplacementScope.Standalone, NumberToStringConverter.ParseRangeExpression("1"), NumberToStringConverter.ParseRangeExpression("1")),
            new NumberToStringConverter.ReplacementRule("beta", "BETA", ReplacementScope.Anywhere, NumberToStringConverter.ParseRangeExpression("1"), NumberToStringConverter.ParseRangeExpression("2")),
        ];
        var converter = new NumberToStringConverter(options);

        Assert.AreEqual("thousand", C(converter, 1000));
        Assert.AreEqual("BETA thousand", C(converter, 2000));
        // onValue is the numeric multiplier (12), not the rendered text: the "beta" rule (onValue 2) does not fire.
        Assert.AreEqual("ten beta thousand", C(converter, 12000));
    }

    /// <summary>Scale-specific variant rules see the text rendered by the scoped table.</summary>
    [TestMethod]
    public void ScopedGroups_ScaleVariantRulesSeeTheScopedText()
    {
        var options = Options([new ScaleScopedGroups("1", Table(AlphaUnits))]);
        options.VariantDimensions = [new NumberToStringConverter.VariantDimension("gender", ["plain", "fem"])];
        options.VariantRules = [new NumberToStringConverter.VariantRule(
            new Dictionary<string, string> { ["gender"] = "fem" },
            [new NumberToStringConverter.ReplacementRule("beta", "betta", ReplacementScope.Anywhere, NumberToStringConverter.ParseRangeExpression("1"), null)])];
        var converter = new NumberToStringConverter(options);

        Assert.AreEqual("beta thousand two", converter.Convert(new BigInteger(2002), "gender=plain"));
        Assert.AreEqual("betta thousand two", converter.Convert(new BigInteger(2002), "gender=fem"));
    }

    /// <summary>A fusion declared only in a scoped set never affects the standalone cardinal.</summary>
    [TestMethod]
    public void ScopedGroups_OwnTheirFusions()
    {
        var scopedTable = Table(AlphaUnits);
        scopedTable[2].Digits[2].Fusions = [new FusionType { For = "1", Right = "ALPHA" }];
        var converter = new NumberToStringConverter(Options([new ScaleScopedGroups("1", scopedTable)]));

        Assert.AreEqual("twenty one", C(converter, 21));
        Assert.AreEqual("twentyALPHA thousand twenty one", C(converter, 21021));
    }

    /// <summary>An invalid fusion inside a scoped set is reported with the scoped location.</summary>
    [TestMethod]
    public void ScopedGroups_InvalidFusionIsReportedWithItsScope()
    {
        var scopedTable = Table(AlphaUnits);
        scopedTable[2].Digits[2].Fusions = [new FusionType { For = "1", RemoveLeft = "zzz" }];
        var exception = Assert.Throws<ArgumentException>(() => new NumberToStringConverter(Options([new ScaleScopedGroups("1", scopedTable)])));
        StringAssert.Contains(exception.Message, "ScaleScopedGroups[onScale=1]");
        StringAssert.Contains(exception.Message, "removeLeft");
    }

    /// <summary>Group triggers see the text rendered by the scoped table.</summary>
    [TestMethod]
    public void ScopedGroups_GroupTriggersSeeTheScopedText()
    {
        var options = Options([new ScaleScopedGroups("1", Table(AlphaUnits))]);
        options.Triggers = [new NumberToStringConverter.TriggerRule(
            NumberToStringConverter.TriggerAt.Group, [1],
            [new NumberToStringConverter.TriggerReplace("beta", false, [], "B")])];
        var converter = new NumberToStringConverter(options);

        Assert.AreEqual("B thousand two", C(converter, 2002));
    }

    /// <summary>With the multiplier after the scale noun the scale comes first; the default stays multiplier first.</summary>
    [TestMethod]
    public void MultiplierAfterScale_PutsTheScaleNounFirst()
    {
        var converter = new NumberToStringConverter(Options(position: ScaleMultiplierPosition.AfterScale));

        Assert.AreEqual("two", C(converter, 2));
        Assert.AreEqual("thousand one", C(converter, 1000));
        Assert.AreEqual("thousand two", C(converter, 2000));
        Assert.AreEqual("thousand twenty one", C(converter, 21000));
        Assert.AreEqual("thousand two hundred thirty four", C(converter, 234000));
        Assert.AreEqual("million two thousand twenty one five", C(converter, 2_021_005));
        Assert.AreEqual(ScaleMultiplierPosition.AfterScale, converter.ScaleMultiplierPosition);
    }

    /// <summary>The two primitives are independent: the scoped table shapes the multiplier, the position orders it.</summary>
    [TestMethod]
    public void ScopedGroupsAndMultiplierAfterScale_AreIndependent()
    {
        var options = Options([new ScaleScopedGroups("1", Table(AlphaUnits))], ScaleMultiplierPosition.AfterScale);
        options.Scale = new NumberScale(["", "grand"], []);
        options.MaxNumber = 999_999;
        var converter = new NumberToStringConverter(options);

        Assert.AreEqual("two", C(converter, 2));
        Assert.AreEqual("grand beta", C(converter, 2000));
        Assert.AreEqual("grand alpha", C(converter, 1000));
        Assert.AreEqual("grand twenty alpha", C(converter, 21000));
        Assert.AreEqual("grand beta hundred thirty delta two", C(converter, 234002));
    }

    /// <summary>The scale connector sits between the scale noun and the multiplier when the multiplier follows.</summary>
    [TestMethod]
    public void MultiplierAfterScale_KeepsTheScaleConnectorBetweenNounAndMultiplier()
    {
        var options = Options(position: ScaleMultiplierPosition.AfterScale);
        options.ScaleConnector = "of";
        options.ScaleConnectorThreshold = 20;
        var converter = new NumberToStringConverter(options);

        Assert.AreEqual("thousand two", C(converter, 2000));
        Assert.AreEqual("thousand of twenty", C(converter, 20000));
    }

    /// <summary>The ordinal suffix attaches to the multiplier, which ends the text when it follows the scale noun.</summary>
    [TestMethod]
    public void MultiplierAfterScale_OrdinalSuffixGoesOnTheMultiplier()
    {
        var converter = new NumberToStringConverter(Options(position: ScaleMultiplierPosition.AfterScale));

        Assert.AreEqual("first", converter.ConvertOrdinal(1));
        Assert.AreEqual("thousand oneth", converter.ConvertOrdinal(1000));
        Assert.AreEqual("thousand twoth", converter.ConvertOrdinal(2000));
        Assert.AreEqual("thousand two fiveth", converter.ConvertOrdinal(2005));
    }

    /// <summary>OrdinalScale assembles multiplier + noun itself, so it is rejected where that assembly would differ from the cardinal.</summary>
    [TestMethod]
    public void OrdinalScale_IsRejectedWithMultiplierAfterScaleOrOnAScopedScale()
    {
        var afterScale = Options(position: ScaleMultiplierPosition.AfterScale);
        afterScale.OrdinalScaleRules = [new OrdinalScaleRule("2", " ")];
        var exception = Assert.Throws<ArgumentException>(() => new NumberToStringConverter(afterScale));
        StringAssert.Contains(exception.Message, "AfterScale");

        var scoped = Options([new ScaleScopedGroups("2..3", Table(UpperUnits))]);
        scoped.OrdinalScaleRules = [new OrdinalScaleRule("3..4", " ")];
        exception = Assert.Throws<ArgumentException>(() => new NumberToStringConverter(scoped));
        StringAssert.Contains(exception.Message, "scale 3");

        var disjoint = Options([new ScaleScopedGroups("1", Table(AlphaUnits))]);
        disjoint.OrdinalScaleRules = [new OrdinalScaleRule("2", " ")];
        Assert.AreEqual("two millionth", new NumberToStringConverter(disjoint).ConvertOrdinal(2_000_000));
    }

    /// <summary>A clone keeps the scoped sets and the position; changing the clone leaves the original untouched.</summary>
    [TestMethod]
    public void Clone_PreservesScopedGroupsAndPosition()
    {
        var original = new NumberToStringConverter(Options([new ScaleScopedGroups("1", Table(AlphaUnits))], ScaleMultiplierPosition.AfterScale));
        var cloneOptions = new NumberToStringConverterOptions(original);
        var clone = new NumberToStringConverter(cloneOptions);

        Assert.AreEqual("thousand beta", C(clone, 2000));
        Assert.AreEqual(ScaleMultiplierPosition.AfterScale, cloneOptions.ScaleMultiplierPosition);
        Assert.AreEqual(1, cloneOptions.ScaleScopedGroups.Count);

        cloneOptions.ScaleScopedGroups[0].Groups[1].Digits[2].StringValue = "changed";
        cloneOptions.ScaleMultiplierPosition = ScaleMultiplierPosition.BeforeScale;
        Assert.AreEqual("changed thousand", C(new NumberToStringConverter(cloneOptions), 2000));
        Assert.AreEqual("thousand beta", C(original, 2000));
        Assert.AreEqual("beta", original.ScaleScopedGroups[0].Groups[1].Digits.Single(d => d.Digit == 2).StringValue);
    }

    /// <summary>Builds the synthetic XML document.</summary>
    /// <param name="culture">The culture identifier.</param>
    /// <param name="groups">The Groups elements (default and scoped).</param>
    /// <param name="languageAttributes">Extra Language attributes.</param>
    /// <param name="extraLanguages">Additional Language elements.</param>
    /// <returns>The configuration document.</returns>
    private static string Document(string culture, string groups, string languageAttributes = "", string extraLanguages = "") => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
          <Language groupSize="3" separator=" " groupSeparator="" zero="zero" minus="minus *" decimalSeparator="point" maxNumber="999999999999" {languageAttributes}>
            <Culture>{culture}</Culture>
            {groups}
            <NumberScale firstLetterUpperCase="false"><StaticNames><Scale value="0" string="" /><Scale value="1" string="thousand" /><Scale value="2" string="million" /><Scale value="3" string="billion" /></StaticNames></NumberScale>
          </Language>
          {extraLanguages}
        </Numbers>
        """;

    /// <summary>Serializes a group table as a Groups element.</summary>
    /// <param name="units">The unit words.</param>
    /// <param name="onScale">The onScale attribute, or <see langword="null"/> for the default table.</param>
    /// <returns>The Groups element.</returns>
    private static string GroupsXml(string[] units, string? onScale = null)
    {
        string level1 = string.Concat(units.Select((u, i) => $"""<Digit digit="{i}" string="{u}" />"""));
        string level2 = string.Concat(Tens.Select((t, i) => i == 0
            ? """<Digit digit="0" string="" buildString="*" />"""
            : $"""<Digit digit="{i}" string="{t}" buildString="{t} *" />"""));
        string level3 = string.Concat(units.Select((u, i) => i == 0
            ? """<Digit digit="0" string="" buildString="*" />"""
            : $"""<Digit digit="{i}" string="{u} hundred" buildString="{u} hundred *" />"""));
        string scope = onScale is null ? "" : $""" onScale="{onScale}" """;
        return $"""<Groups{scope}><Group level="1">{level1}</Group><Group level="2">{level2}</Group><Group level="3">{level3}</Group></Groups>""";
    }

    /// <summary>Reads a document and returns the converter of <paramref name="culture"/>.</summary>
    private static NumberToStringConverter Read(string document, string culture)
        => NumberToStringConverter.ReadConfiguration(document)[culture];

    /// <summary>XML: several Groups elements, onScale and multiplierPosition are schema-valid and honoured.</summary>
    [TestMethod]
    public void Xml_ScopedGroupsAndMultiplierPosition()
    {
        string culture = NewCulture();
        var converter = Read(Document(culture,
            GroupsXml(Units) + GroupsXml(AlphaUnits, "1") + GroupsXml(UpperUnits, "2.."),
            """multiplierPosition="afterScale" """), culture);

        Assert.AreEqual("two", C(converter, 2));
        Assert.AreEqual("thousand beta", C(converter, 2000));
        Assert.AreEqual("million TWO thousand beta two", C(converter, 2_002_002));
        Assert.AreEqual(2, converter.ScaleScopedGroups.Count);
        Assert.AreEqual(ScaleMultiplierPosition.AfterScale, converter.ScaleMultiplierPosition);
    }

    /// <summary>
    /// The public XML model keeps the historical <c>GroupsListType? Groups</c> member (default tables) while
    /// every <c>&lt;Groups&gt;</c> element is read into <c>GroupsElements</c>; setting <c>Groups</c> replaces
    /// the default element and keeps the scoped ones.
    /// </summary>
    [TestMethod]
    public void XmlModel_KeepsHistoricalGroupsMember()
    {
        var property = typeof(LanguageXmlModel).GetProperty(nameof(LanguageXmlModel.Groups))!;
        Assert.AreEqual(typeof(GroupsListType), property.PropertyType);
        Assert.IsTrue(property.CanRead && property.CanWrite);

        var scoped = new GroupsListType { OnScale = "1" };
        var first = new GroupsListType();
        var model = new LanguageXmlModel { GroupsElements = [scoped, first] };
        Assert.AreSame(first, model.Groups);

        var replacement = new GroupsListType();
        model.Groups = replacement;
        Assert.AreSame(replacement, model.Groups);
        CollectionAssert.AreEqual(new[] { replacement, scoped }, model.GroupsElements);

        model.Groups = null;
        Assert.IsNull(model.Groups);
        CollectionAssert.AreEqual(new[] { scoped }, model.GroupsElements);
        Assert.IsNull(new LanguageXmlModel().Groups);
    }

    /// <summary>XML: the historical single Groups without the new attributes is unchanged.</summary>
    [TestMethod]
    public void Xml_HistoricalGroupsUnchanged()
    {
        string culture = NewCulture();
        var converter = Read(Document(culture, GroupsXml(Units), """multiplierPosition="beforeScale" """), culture);

        Assert.AreEqual("two thousand two", C(converter, 2002));
        Assert.AreEqual(0, converter.ScaleScopedGroups.Count);
    }

    /// <summary>XML: duplicate default tables, a scoped table without default, and an unknown position are rejected.</summary>
    [TestMethod]
    public void Xml_InvalidDocumentsAreRejected()
    {
        string culture = NewCulture();
        var exception = Assert.Throws<InvalidOperationException>(() => Read(Document(culture, GroupsXml(Units) + GroupsXml(AlphaUnits)), culture));
        StringAssert.Contains(exception.Message, "more than one <Groups> without onScale");

        culture = NewCulture();
        exception = Assert.Throws<InvalidOperationException>(() => Read(Document(culture, GroupsXml(AlphaUnits, "1")), culture));
        StringAssert.Contains(exception.Message, "Groups");

        // Rejected by the schema enumeration.
        culture = NewCulture();
        Assert.Throws<Exception>(() => Read(Document(culture, GroupsXml(Units), """multiplierPosition="sideways" """), culture));

        culture = NewCulture();
        var overlap = Assert.Throws<ArgumentException>(() => Read(Document(culture, GroupsXml(Units) + GroupsXml(AlphaUnits, "1..3") + GroupsXml(UpperUnits, "2")), culture));
        StringAssert.Contains(overlap.Message, "overlaps");
    }

    /// <summary>
    /// baseOn: a child redeclaring a scoped range replaces it, a new range is added, an inherited range is
    /// kept, the position and the default table are inherited unless redeclared, and a crossing range is rejected.
    /// </summary>
    [TestMethod]
    public void Xml_BaseOnMergesScopedGroupsByRange()
    {
        string parent = NewCulture();
        string child = NewCulture();
        string parentDocument = Document(parent,
            GroupsXml(Units) + GroupsXml(AlphaUnits, "1") + GroupsXml(AlphaUnits, "3"),
            """multiplierPosition="afterScale" """,
            $"""
            <Language baseOn="{parent}">
              <Culture>{child}</Culture>
              {GroupsXml(UpperUnits, "1")}
              {GroupsXml(UpperUnits, "2")}
            </Language>
            """);
        var converters = NumberToStringConverter.ReadConfiguration(parentDocument);

        Assert.AreEqual("thousand beta", C(converters[parent], 2000));
        Assert.AreEqual("million two", C(converters[parent], 2_000_000));
        var derived = converters[child];
        Assert.AreEqual("two", C(derived, 2));
        Assert.AreEqual("thousand TWO", C(derived, 2000));
        Assert.AreEqual("million TWO", C(derived, 2_000_000));
        Assert.AreEqual("billion beta", C(derived, 2_000_000_000));

        string crossingParent = NewCulture();
        string crossingChild = NewCulture();
        var exception = Assert.Throws<InvalidOperationException>(() => NumberToStringConverter.ReadConfiguration(Document(crossingParent,
            GroupsXml(Units) + GroupsXml(AlphaUnits, "1..2"), "",
            $"""<Language baseOn="{crossingParent}"><Culture>{crossingChild}</Culture>{GroupsXml(UpperUnits, "2..3")}</Language>""")));
        StringAssert.Contains(exception.Message, "overlaps");
    }
}
