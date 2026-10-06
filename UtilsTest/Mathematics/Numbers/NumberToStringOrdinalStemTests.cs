using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Schema;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// Language-independent engine tests for the <c>&lt;OrdinalStem&gt;</c> primitive: ending rewrite
/// before the ordinal suffix, longest-match precedence, interaction with exceptions, word rules,
/// <c>removeTrailing</c> and variant suffixes, load-time validation, cloning and <c>baseOn</c> merge.
/// </summary>
[TestClass]
public class NumberToStringOrdinalStemTests
{
    /// <summary>Default unit words of the synthetic language, chosen for their endings.</summary>
    private static readonly string[] DefaultUnits = ["", "foobar", "foo", "foosei", "ventisei", "venti", "plain", "alfa", "beti", "gammo"];

    /// <summary>Creates a unique culture name so tests never share registry state.</summary>
    private static string NewCulture() => $"STEM-{Guid.NewGuid():N}";

    /// <summary>Creates a synthetic two-level language document carrying the given ordinal block.</summary>
    /// <param name="ordinals">The complete <c>&lt;Ordinals&gt;</c> element, or an empty string.</param>
    /// <param name="culture">The culture identifier of the synthetic language.</param>
    /// <param name="variants">An optional <c>&lt;Variants&gt;</c> element.</param>
    /// <param name="extraLanguages">Additional Language elements appended to the document.</param>
    /// <returns>The configuration document.</returns>
    private static string Document(string ordinals, string culture, string variants = "", string extraLanguages = "")
    {
        string units = string.Concat(DefaultUnits.Select((u, i) => $"""<Digit digit="{i}" string="{u}" />"""));
        string[] tensWords = ["", "ten", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];
        string tens = string.Concat(tensWords.Select((t, i) => i == 0
            ? """<Digit digit="0" string="" buildString="*" />"""
            : $"""<Digit digit="{i}" string="{t}" buildString="{t} *" />"""));
        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
              <Language groupSize="2" separator=" " groupSeparator="" zero="zero" minus="minus *" decimalSeparator="point" maxNumber="9999">
                <Culture>{culture}</Culture>
                <Groups>
                  <Group level="1">{units}</Group>
                  <Group level="2">{tens}</Group>
                </Groups>
                <NumberScale firstLetterUpperCase="false"><StaticNames><Scale value="0" string="" /><Scale value="1" string="thousand" /></StaticNames><Suffixes><Suffix>illion</Suffix></Suffixes></NumberScale>
                {ordinals}
                {variants}
              </Language>
              {extraLanguages}
            </Numbers>
            """;
    }

    /// <summary>Builds the synthetic converter carrying <paramref name="ordinals"/>.</summary>
    /// <param name="ordinals">The complete <c>&lt;Ordinals&gt;</c> element.</param>
    /// <param name="variants">An optional <c>&lt;Variants&gt;</c> element.</param>
    /// <returns>The configured converter.</returns>
    private static NumberToStringConverter Build(string ordinals, string variants = "")
    {
        string culture = NewCulture();
        return NumberToStringConverter.ReadConfiguration(Document(ordinals, culture, variants))[culture];
    }

    // ── Ending rewrite ──────────────────────────────────────────────────────────────────────────

    /// <summary>A matching rule replaces only the matched ending, then the suffix is appended.</summary>
    [TestMethod]
    public void OrdinalStem_ReplacesMatchedEndingBeforeSuffix()
    {
        var converter = Build("""<Ordinals suffix="X"><OrdinalStem from="bar" to="baz" /></Ordinals>""");

        Assert.AreEqual("foobazX", converter.ConvertOrdinal(1));
    }

    /// <summary>An empty <c>to</c> removes the matched ending.</summary>
    [TestMethod]
    public void OrdinalStem_EmptyTo_RemovesEnding()
    {
        var converter = Build("""<Ordinals suffix="X"><OrdinalStem from="o" to="" /></Ordinals>""");

        Assert.AreEqual("foX", converter.ConvertOrdinal(2));
    }

    /// <summary>An identity rule keeps the ending and still appends the suffix.</summary>
    [TestMethod]
    public void OrdinalStem_IdentityRule_KeepsEnding()
    {
        var converter = Build("""<Ordinals suffix="X"><OrdinalStem from="sei" to="sei" /></Ordinals>""");

        Assert.AreEqual("fooseiX", converter.ConvertOrdinal(3));
    }

    /// <summary>A word without a matching rule and without removeTrailing receives the bare suffix.</summary>
    [TestMethod]
    public void OrdinalStem_NoMatchingRule_AppendsSuffixToUnchangedWord()
    {
        var converter = Build("""<Ordinals suffix="X"><OrdinalStem from="bar" to="baz" /></Ordinals>""");

        Assert.AreEqual("plainX", converter.ConvertOrdinal(6));
    }

    /// <summary>The longest matching ending wins over a shorter one that also matches.</summary>
    [TestMethod]
    public void OrdinalStem_LongestMatchWins()
    {
        var converter = Build("""<Ordinals suffix="X"><OrdinalStem from="sei" to="sei" /><OrdinalStem from="i" to="" /></Ordinals>""");

        Assert.AreEqual("ventiseiX", converter.ConvertOrdinal(4));
        Assert.AreEqual("ventX", converter.ConvertOrdinal(5));
    }

    /// <summary>The declaration order of the rules does not change the result.</summary>
    [TestMethod]
    public void OrdinalStem_DeclarationOrder_IsIrrelevant()
    {
        var forward = Build("""<Ordinals suffix="X"><OrdinalStem from="sei" to="sei" /><OrdinalStem from="i" to="" /></Ordinals>""");
        var reversed = Build("""<Ordinals suffix="X"><OrdinalStem from="i" to="" /><OrdinalStem from="sei" to="sei" /></Ordinals>""");

        foreach (int number in new[] { 3, 4, 5, 8 })
            Assert.AreEqual(forward.ConvertOrdinal(number), reversed.ConvertOrdinal(number), $"number {number}");
        Assert.AreEqual("ventiseiX", reversed.ConvertOrdinal(4));
    }

    /// <summary>The converter exposes its rules sorted longest ending first.</summary>
    [TestMethod]
    public void OrdinalStem_PublicRules_AreSortedLongestFirst()
    {
        var converter = Build("""<Ordinals suffix="X"><OrdinalStem from="i" to="" /><OrdinalStem from="mila" to="mill" /><OrdinalStem from="sei" to="sei" /></Ordinals>""");

        CollectionAssert.AreEqual(new[] { "mila", "sei", "i" }, converter.OrdinalStemRules.Select(r => r.From).ToArray());
    }

    /// <summary>Only the last word of a multi-word cardinal is rewritten.</summary>
    [TestMethod]
    public void OrdinalStem_AppliesOnlyToLastWord()
    {
        var converter = Build("""<Ordinals suffix="X"><OrdinalStem from="bar" to="baz" /><OrdinalStem from="ty" to="" /></Ordinals>""");

        Assert.AreEqual("twenty foobazX", converter.ConvertOrdinal(21));
        Assert.AreEqual("twenX", converter.ConvertOrdinal(20));
    }

    /// <summary>Matching is ordinal and case-sensitive.</summary>
    [TestMethod]
    public void OrdinalStem_Matching_IsCaseSensitive()
    {
        var converter = Build("""<Ordinals suffix="X"><OrdinalStem from="BAR" to="baz" /></Ordinals>""");

        Assert.AreEqual("foobarX", converter.ConvertOrdinal(1));
    }

    // ── Interaction with existing ordinal rules ─────────────────────────────────────────────────

    /// <summary>When no stem rule matches, the historical removeTrailing applies.</summary>
    [TestMethod]
    public void OrdinalStem_NoMatch_FallsBackToRemoveTrailing()
    {
        var converter = Build("""<Ordinals suffix="X" removeTrailing="n"><OrdinalStem from="zzz" to="" /></Ordinals>""");

        Assert.AreEqual("plaiX", converter.ConvertOrdinal(6));
    }

    /// <summary>A matching stem rule replaces removeTrailing for that word: removeTrailing is not also applied.</summary>
    [TestMethod]
    public void OrdinalStem_Match_ReplacesRemoveTrailing()
    {
        var converter = Build("""<Ordinals suffix="X" removeTrailing="o"><OrdinalStem from="o" to="Q" /></Ordinals>""");

        Assert.AreEqual("foQX", converter.ConvertOrdinal(2));
        Assert.AreEqual("gammQX", converter.ConvertOrdinal(9));
    }

    /// <summary>An exact last-word rule keeps priority over a matching stem rule.</summary>
    [TestMethod]
    public void OrdinalStem_ExactWordRule_KeepsPriority()
    {
        var converter = Build("""<Ordinals suffix="X"><OrdinalStem from="o" to="" /><Ordinal from="foo" to="first" /></Ordinals>""");

        Assert.AreEqual("first", converter.ConvertOrdinal(2));
        Assert.AreEqual("gammX", converter.ConvertOrdinal(9));
    }

    /// <summary>A whole-number exception keeps priority over a matching stem rule.</summary>
    [TestMethod]
    public void OrdinalStem_OrdinalException_KeepsPriority()
    {
        var converter = Build("""<Ordinals suffix="X"><OrdinalStem from="o" to="" /><OrdinalException value="2" string="second" /></Ordinals>""");

        Assert.AreEqual("second", converter.ConvertOrdinal(2));
    }

    /// <summary>Stem rules only act on the suffixed path: a prefix-only configuration ignores them.</summary>
    [TestMethod]
    public void OrdinalStem_WithoutSuffix_IsNotApplied()
    {
        var converter = Build("""<Ordinals prefix="P"><OrdinalStem from="o" to="" /></Ordinals>""");

        Assert.AreEqual("Pfoo", converter.ConvertOrdinal(2));
    }

    /// <summary>The base stem rules are applied with the suffix of the selected ordinal variant.</summary>
    [TestMethod]
    public void OrdinalStem_UsesEffectiveVariantSuffix()
    {
        var converter = Build(
            """
            <Ordinals suffix="X">
              <OrdinalStem from="o" to="" />
              <OrdinalVariants><Variant type="gender" variant="f" suffix="Y" /></OrdinalVariants>
            </Ordinals>
            """,
            """<Variants><Dimension name="gender" values="m,f" /></Variants>""");

        Assert.AreEqual("foX", converter.ConvertOrdinal(2));
        Assert.AreEqual("foX", converter.ConvertOrdinal(2, "gender=m"));
        Assert.AreEqual("foY", converter.ConvertOrdinal(2, "gender=f"));
    }

    /// <summary>Negative ordinals keep the existing policy: the absolute value is formed, then the sign is applied.</summary>
    [TestMethod]
    public void OrdinalStem_NegativeNumber_FormsAbsoluteValueThenAppliesSign()
    {
        var converter = Build("""<Ordinals suffix="X"><OrdinalStem from="o" to="" /></Ordinals>""");

        Assert.AreEqual("minus foX", converter.ConvertOrdinal(-2));
    }

    // ── Compatibility ───────────────────────────────────────────────────────────────────────────

    /// <summary>Without any stem rule, removeTrailing and the suffix keep their exact historical behaviour.</summary>
    [TestMethod]
    public void OrdinalStem_Absent_KeepsHistoricalRemoveTrailingBehaviour()
    {
        var converter = Build("""<Ordinals suffix="X" removeTrailing="o"><Ordinal from="plain" to="simple" /></Ordinals>""");

        Assert.AreEqual(0, converter.OrdinalStemRules.Count);
        Assert.AreEqual("foX", converter.ConvertOrdinal(2));
        Assert.AreEqual("gammX", converter.ConvertOrdinal(9));
        Assert.AreEqual("foobarX", converter.ConvertOrdinal(1));
        Assert.AreEqual("simple", converter.ConvertOrdinal(6));
        Assert.AreEqual("twenty foX", converter.ConvertOrdinal(22));
        Assert.AreEqual("zerX", converter.ConvertOrdinal(0));
    }

    // ── Load-time validation ────────────────────────────────────────────────────────────────────

    /// <summary>Two rules with the same <c>from</c> are rejected at load.</summary>
    [TestMethod]
    public void OrdinalStem_DuplicateFrom_IsRejected()
        => Assert.Throws<ArgumentException>(
            () => Build("""<Ordinals suffix="X"><OrdinalStem from="o" to="" /><OrdinalStem from="o" to="u" /></Ordinals>"""));

    /// <summary>An empty <c>from</c> is rejected by the schema.</summary>
    [TestMethod]
    public void OrdinalStem_EmptyFrom_IsRejectedBySchema()
        => Assert.ThrowsExactly<XmlSchemaValidationException>(
            () => Build("""<Ordinals suffix="X"><OrdinalStem from="" to="x" /></Ordinals>"""));

    /// <summary>A missing <c>from</c> is rejected by the schema.</summary>
    [TestMethod]
    public void OrdinalStem_MissingFrom_IsRejectedBySchema()
        => Assert.ThrowsExactly<XmlSchemaValidationException>(
            () => Build("""<Ordinals suffix="X"><OrdinalStem to="x" /></Ordinals>"""));

    /// <summary>A missing <c>to</c> is rejected by the schema (an empty one is valid).</summary>
    [TestMethod]
    public void OrdinalStem_MissingTo_IsRejectedBySchema()
        => Assert.ThrowsExactly<XmlSchemaValidationException>(
            () => Build("""<Ordinals suffix="X"><OrdinalStem from="o" /></Ordinals>"""));

    /// <summary>Creates programmatic options for a one-level language whose 2 is "foo" and 4 is "ventisei".</summary>
    /// <param name="stems">The stem rules.</param>
    /// <returns>The converter options.</returns>
    private static NumberToStringConverterOptions ProgrammaticOptions(IReadOnlyList<OrdinalStemRule> stems)
    {
        var units = new DigitListType { Digits = [.. DefaultUnits.Select((u, i) => new DigitType(i, u))] };
        var tens = new DigitListType { Digits = [.. Enumerable.Range(0, 10).Select(i => new DigitType(i, i == 0 ? "" : $"t{i}", i == 0 ? "*" : $"t{i} *"))] };
        return new NumberToStringConverterOptions
        {
            Group = 2,
            Zero = "zero",
            Minus = "minus *",
            Groups = new Dictionary<int, DigitListType> { [1] = units, [2] = tens },
            Scale = new NumberScale(["", "thousand"], ["illion"]),
            OrdinalSuffix = "X",
            OrdinalStemRules = stems,
        };
    }

    /// <summary>Stem rules supplied through options are honoured like XML rules.</summary>
    [TestMethod]
    public void OrdinalStem_ProgrammaticOptions_AreHonoured()
    {
        var converter = new NumberToStringConverter(ProgrammaticOptions([new("i", ""), new("sei", "sei")]));

        Assert.AreEqual("ventiseiX", converter.ConvertOrdinal(4));
        Assert.AreEqual("ventX", converter.ConvertOrdinal(5));
    }

    /// <summary>Invalid programmatic rules are rejected by the converter.</summary>
    [TestMethod]
    [DataRow("", "x")]
    [DataRow(null, "x")]
    [DataRow("o", null)]
    public void OrdinalStem_InvalidProgrammaticRule_IsRejected(string? from, string? to)
        => Assert.Throws<ArgumentException>(
            () => new NumberToStringConverter(ProgrammaticOptions([new(from!, to!)])));

    /// <summary>A null programmatic rule entry is rejected by the converter.</summary>
    [TestMethod]
    public void OrdinalStem_NullProgrammaticRule_IsRejected()
        => Assert.Throws<ArgumentException>(
            () => new NumberToStringConverter(ProgrammaticOptions([null!])));

    /// <summary>Duplicate programmatic rules are rejected by the converter.</summary>
    [TestMethod]
    public void OrdinalStem_DuplicateProgrammaticRule_IsRejected()
        => Assert.Throws<ArgumentException>(
            () => new NumberToStringConverter(ProgrammaticOptions([new("o", ""), new("o", "u")])));

    /// <summary>Cloning a converter through its options preserves its stem rules.</summary>
    [TestMethod]
    public void OrdinalStem_OptionsClone_PreservesRules()
    {
        var original = Build("""<Ordinals suffix="X"><OrdinalStem from="sei" to="sei" /><OrdinalStem from="i" to="" /></Ordinals>""");

        var clone = new NumberToStringConverter(new NumberToStringConverterOptions(original));

        Assert.AreEqual("ventiseiX", clone.ConvertOrdinal(4));
        Assert.AreEqual("ventX", clone.ConvertOrdinal(5));
        CollectionAssert.AreEqual(original.OrdinalStemRules.ToArray(), clone.OrdinalStemRules.ToArray());
    }

    // ── baseOn ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A child's stem rules are merged with its parent's by <c>from</c>: inherited rules are kept, a
    /// child rule with the same <c>from</c> replaces the parent's, and new child rules are added.
    /// </summary>
    [TestMethod]
    public void OrdinalStem_BaseOn_MergesRulesByFrom()
    {
        string parent = NewCulture();
        string child = NewCulture();
        string document = Document(
            """<Ordinals suffix="X"><OrdinalStem from="a" to="" /><OrdinalStem from="i" to="" /></Ordinals>""",
            parent,
            extraLanguages: $"""
                <Language baseOn="{parent}"><Culture>{child}</Culture>
                  <Ordinals><OrdinalStem from="i" to="x" /><OrdinalStem from="o" to="" /></Ordinals>
                </Language>
                """);

        var converters = NumberToStringConverter.ReadConfiguration(document);

        Assert.AreEqual("alfX", converters[parent].ConvertOrdinal(7));
        Assert.AreEqual("betX", converters[parent].ConvertOrdinal(8));
        Assert.AreEqual("gammoX", converters[parent].ConvertOrdinal(9));
        Assert.AreEqual("alfX", converters[child].ConvertOrdinal(7));
        Assert.AreEqual("betxX", converters[child].ConvertOrdinal(8));
        Assert.AreEqual("gammX", converters[child].ConvertOrdinal(9));
        CollectionAssert.AreEquivalent(
            new[] { new OrdinalStemRule("a", ""), new OrdinalStemRule("i", "x"), new OrdinalStemRule("o", "") },
            converters[child].OrdinalStemRules.ToArray());
    }

    /// <summary>A child without an Ordinals element inherits its parent's stem rules unchanged.</summary>
    [TestMethod]
    public void OrdinalStem_BaseOnChildWithoutOrdinals_InheritsRules()
    {
        string parent = NewCulture();
        string child = NewCulture();
        string document = Document(
            """<Ordinals suffix="X"><OrdinalStem from="sei" to="sei" /><OrdinalStem from="i" to="" /></Ordinals>""",
            parent,
            extraLanguages: $"""<Language baseOn="{parent}"><Culture>{child}</Culture></Language>""");

        var converters = NumberToStringConverter.ReadConfiguration(document);

        Assert.AreEqual("ventiseiX", converters[child].ConvertOrdinal(4));
        Assert.AreEqual("ventX", converters[child].ConvertOrdinal(5));
    }
}
