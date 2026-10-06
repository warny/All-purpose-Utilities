using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Utils.NumberToString;
using static Utils.NumberToString.NumberToStringConverter;

namespace UtilsTest.NumberToString;

/// <summary>
/// Language-independent engine tests for ordinal-only replacements (<c>&lt;Replacement&gt;</c> inside
/// <c>&lt;Ordinals&gt;</c> and inside an ordinal <c>&lt;Variant&gt;</c>): pipeline position, scopes,
/// variant selection including the injected default variant, isolation from cardinals, precedence
/// of exceptions, compatibility with word and stem rules, validation, cloning and <c>baseOn</c>.
/// </summary>
[TestClass]
public class NumberToStringOrdinalReplacementTests
{
    /// <summary>Unit words of the synthetic language.</summary>
    private static readonly string[] Units = ["", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"];

    /// <summary>Tens words of the synthetic language.</summary>
    private static readonly string[] Tens = ["", "ten", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

    /// <summary>The gender dimension of the synthetic language; "plain" is the injected default.</summary>
    private const string Dimension = """<Variants><Dimension name="gender" values="plain,masc,fem" /></Variants>""";

    /// <summary>Creates a unique culture name.</summary>
    private static string NewCulture() => $"ORDREPL-{Guid.NewGuid():N}";

    /// <summary>
    /// Creates a synthetic three-level language whose 11 is the two-part "feminine-eleven"
    /// (so 111 is "one hundred feminine-eleven"), with the given ordinal block.
    /// </summary>
    /// <param name="culture">The culture identifier.</param>
    /// <param name="ordinals">The complete <c>&lt;Ordinals&gt;</c> element.</param>
    /// <param name="extraLanguages">Additional Language elements.</param>
    /// <returns>The configuration document.</returns>
    private static string Document(string culture, string ordinals, string extraLanguages = "")
    {
        string units = string.Concat(Units.Select((u, i) => $"""<Digit digit="{i}" string="{u}" />"""));
        string tens = string.Concat(Tens.Select((t, i) => i == 0
            ? """<Digit digit="0" string="" buildString="*" />"""
            : $"""<Digit digit="{i}" string="{t}" buildString="{t} *" />"""));
        string hundreds = string.Concat(Units.Select((u, i) => i == 0
            ? """<Digit digit="0" string="" buildString="*" />"""
            : $"""<Digit digit="{i}" string="{u} hundred" buildString="{u} hundred *" />"""));
        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
              <Language groupSize="3" separator=" " groupSeparator="" zero="zero" minus="minus *" decimalSeparator="point" maxNumber="999999">
                <Culture>{culture}</Culture>
                <Groups>
                  <Group level="1">{units}</Group>
                  <Group level="2">{tens}</Group>
                  <Group level="3">{hundreds}</Group>
                </Groups>
                <NumberScale firstLetterUpperCase="false"><StaticNames><Scale value="0" string="" /><Scale value="1" string="thousand" /></StaticNames><Suffixes><Suffix>illion</Suffix></Suffixes></NumberScale>
                <Exceptions><Number value="11" string="feminine-eleven" /></Exceptions>
                {ordinals}
                {Dimension}
              </Language>
              {extraLanguages}
            </Numbers>
            """;
    }

    /// <summary>Builds the synthetic converter.</summary>
    /// <param name="ordinals">The complete <c>&lt;Ordinals&gt;</c> element.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Build(string ordinals)
    {
        string culture = NewCulture();
        return NumberToStringConverter.ReadConfiguration(Document(culture, ordinals))[culture];
    }

    // ── Pipeline position and scopes ────────────────────────────────────────────────────────────

    /// <summary>An EndsWith replacement rewrites the end of the assembled phrase before the suffix.</summary>
    [TestMethod]
    public void OrdinalReplacement_EndsWith_RewritesPhraseEndBeforeSuffix()
    {
        var converter = Build("""<Ordinals suffix="th"><Replacement oldValue="feminine-eleven" newValue="masculine-eleven" scope="EndsWith" /></Ordinals>""");

        Assert.AreEqual("one hundred masculine-eleventh", converter.ConvertOrdinal(111));
        Assert.AreEqual("one thousand masculine-eleventh", converter.ConvertOrdinal(1011));
        Assert.AreEqual("twentyth", converter.ConvertOrdinal(20));
    }

    /// <summary>The supported cardinal scopes behave as for cardinal replacements.</summary>
    /// <param name="scope">The replacement scope.</param>
    /// <param name="oldValue">The replaced text.</param>
    /// <param name="newValue">The replacement text.</param>
    /// <param name="number">The ordinal value.</param>
    /// <param name="expected">The expected ordinal.</param>
    [TestMethod]
    [DataRow("StartsWith", "one hundred", "a hundred", 111L, "a hundred feminine-eleventh")]
    [DataRow("StartsWith", "one hundred", "a hundred", 211L, "two hundred feminine-eleventh")]
    [DataRow("Anywhere", "hundred", "HUNDRED", 211L, "two HUNDRED feminine-eleventh")]
    [DataRow("EndsWith", "feminine-eleven", "m-eleven", 11L, "m-eleventh")]
    [DataRow("LastWord", "two", "deux", 202L, "two hundred deuxth")]
    [DataRow("LastWord", "two", "deux", 220L, "two hundred twentyth")]
    [DataRow("Standalone", "one hundred", "hundred", 100L, "hundredth")]
    [DataRow("Standalone", "one hundred", "hundred", 101L, "one hundred oneth")]
    public void OrdinalReplacement_Scopes_MatchCardinalReplacementSemantics(string scope, string oldValue, string newValue, long number, string expected)
    {
        var converter = Build($"""<Ordinals suffix="th"><Replacement oldValue="{oldValue}" newValue="{newValue}" scope="{scope}" /></Ordinals>""");

        Assert.AreEqual(expected, converter.ConvertOrdinal(number));
    }

    /// <summary>Ordinal replacements never affect cardinal conversions.</summary>
    [TestMethod]
    public void OrdinalReplacement_NeverAffectsCardinals()
    {
        var converter = Build("""
            <Ordinals suffix="th">
              <Replacement oldValue="feminine-eleven" newValue="masculine-eleven" scope="EndsWith" />
              <OrdinalVariants><Variant type="gender" variant="plain"><Replacement oldValue="hundred" newValue="HUNDRED" scope="Anywhere" /></Variant></OrdinalVariants>
            </Ordinals>
            """);

        Assert.AreEqual("one hundred feminine-eleven", converter.Convert(new BigInteger(111)));
        Assert.AreEqual("one hundred feminine-eleven", converter.Convert(new BigInteger(111), "gender=plain"));
        Assert.AreEqual("one HUNDRED masculine-eleventh", converter.ConvertOrdinal(111));
    }

    // ── Variants ────────────────────────────────────────────────────────────────────────────────

    /// <summary>A variant replacement applies only to that variant.</summary>
    [TestMethod]
    public void OrdinalReplacement_Variant_AppliesOnlyToThatVariant()
    {
        var converter = Build("""
            <Ordinals suffix="th">
              <OrdinalVariants><Variant type="gender" variant="masc"><Replacement oldValue="feminine-eleven" newValue="masculine-eleven" scope="EndsWith" /></Variant></OrdinalVariants>
            </Ordinals>
            """);

        Assert.AreEqual("one hundred masculine-eleventh", converter.ConvertOrdinal(111, "gender=masc"));
        Assert.AreEqual("one hundred feminine-eleventh", converter.ConvertOrdinal(111, "gender=fem"));
        Assert.AreEqual("one hundred feminine-eleventh", converter.ConvertOrdinal(111));
    }

    /// <summary>The replacements of the injected default variant apply without any caller variant.</summary>
    [TestMethod]
    public void OrdinalReplacement_InjectedDefaultVariant_AppliesWithoutCallerVariants()
    {
        var converter = Build("""
            <Ordinals suffix="th">
              <OrdinalVariants><Variant type="gender" variant="plain"><Replacement oldValue="feminine-eleven" newValue="masculine-eleven" scope="EndsWith" /></Variant></OrdinalVariants>
            </Ordinals>
            """);

        Assert.AreEqual("one hundred masculine-eleventh", converter.ConvertOrdinal(111));
        Assert.AreEqual("one hundred masculine-eleventh", converter.ConvertOrdinal(111, "gender=plain"));
        Assert.AreEqual("one hundred feminine-eleventh", converter.ConvertOrdinal(111, "gender=fem"));
    }

    /// <summary>Base replacements run first, then the selected variant's replacements see their result.</summary>
    [TestMethod]
    public void OrdinalReplacement_BaseThenVariant_AreChained()
    {
        var converter = Build("""
            <Ordinals suffix="th">
              <Replacement oldValue="feminine-eleven" newValue="x-eleven" scope="EndsWith" />
              <OrdinalVariants><Variant type="gender" variant="masc"><Replacement oldValue="x-eleven" newValue="y-eleven" scope="EndsWith" /></Variant></OrdinalVariants>
            </Ordinals>
            """);

        Assert.AreEqual("one hundred x-eleventh", converter.ConvertOrdinal(111));
        Assert.AreEqual("one hundred y-eleventh", converter.ConvertOrdinal(111, "gender=masc"));
    }

    // ── Precedence and compatibility ────────────────────────────────────────────────────────────

    /// <summary>A whole-number exception bypasses the replacements.</summary>
    [TestMethod]
    public void OrdinalReplacement_OrdinalException_KeepsPrecedence()
    {
        var converter = Build("""<Ordinals suffix="th"><OrdinalException value="111" string="special" /><Replacement oldValue="feminine-eleven" newValue="masculine-eleven" scope="EndsWith" /></Ordinals>""");

        Assert.AreEqual("special", converter.ConvertOrdinal(111));
        Assert.AreEqual("two hundred masculine-eleventh", converter.ConvertOrdinal(211));
    }

    /// <summary>Exact word rules see the replaced text.</summary>
    [TestMethod]
    public void OrdinalReplacement_RunsBeforeExactWordRules()
    {
        var converter = Build("""<Ordinals suffix="th"><Replacement oldValue="feminine-eleven" newValue="masc" scope="EndsWith" /><Ordinal from="masc" to="MASC-TH" /></Ordinals>""");

        Assert.AreEqual("one hundred MASC-TH", converter.ConvertOrdinal(111));
    }

    /// <summary>Stem rules see the replaced text.</summary>
    [TestMethod]
    public void OrdinalReplacement_RunsBeforeStemRules()
    {
        var converter = Build("""<Ordinals suffix="th"><Replacement oldValue="feminine-eleven" newValue="masculine-eleven" scope="EndsWith" /><OrdinalStem from="eleven" to="elev" /></Ordinals>""");

        Assert.AreEqual("one hundred masculine-elevth", converter.ConvertOrdinal(111));
    }

    /// <summary>A prefix is added to the replaced text.</summary>
    [TestMethod]
    public void OrdinalReplacement_WithPrefix_IsPrefixed()
    {
        var converter = Build("""<Ordinals prefix="no. "><Replacement oldValue="feminine-eleven" newValue="masculine-eleven" scope="EndsWith" /></Ordinals>""");

        Assert.AreEqual("no. one hundred masculine-eleven", converter.ConvertOrdinal(111));
    }

    /// <summary>Without ordinal replacements the ordinal pipeline is unchanged.</summary>
    [TestMethod]
    public void OrdinalReplacement_Absent_KeepsHistoricalOrdinals()
    {
        var converter = Build("""<Ordinals suffix="th" />""");

        Assert.AreEqual("one hundred feminine-eleventh", converter.ConvertOrdinal(111));
        Assert.AreEqual(0, converter.OrdinalReplacements.Count);
    }

    /// <summary>The historical OrdinalVariantRule constructor still compiles and yields no replacements.</summary>
    [TestMethod]
    public void OrdinalVariantRule_LegacyConstructor_HasNoReplacements()
    {
        var rule = new OrdinalVariantRule(new Dictionary<string, string>(), new Dictionary<long, string>(), new Dictionary<string, string>(), "th", null, 3);

        Assert.AreEqual(0, rule.Replacements.Count);
        Assert.AreEqual(3, rule.Priority);
    }

    // ── Validation ──────────────────────────────────────────────────────────────────────────────

    /// <summary>onScale/onValue filters are rejected on ordinal replacements (base and variant).</summary>
    /// <param name="attribute">The rejected filter attribute.</param>
    [TestMethod]
    [DataRow("""onScale="1" """)]
    [DataRow("""onValue="111" """)]
    public void OrdinalReplacement_ScaleOrValueFilter_IsRejected(string attribute)
    {
        Assert.Throws<ArgumentException>(() => Build($"""<Ordinals suffix="th"><Replacement oldValue="a" newValue="b" scope="Anywhere" {attribute}/></Ordinals>"""));
        Assert.Throws<ArgumentException>(() => Build($"""
            <Ordinals suffix="th"><OrdinalVariants><Variant type="gender" variant="masc"><Replacement oldValue="a" newValue="b" scope="Anywhere" {attribute}/></Variant></OrdinalVariants></Ordinals>
            """));
    }

    /// <summary>An ordinal replacement without newValue is rejected.</summary>
    [TestMethod]
    public void OrdinalReplacement_WithoutNewValue_IsRejected()
        => Assert.Throws<Exception>(() => Build("""<Ordinals suffix="th"><Replacement oldValue="a" scope="Anywhere" /></Ordinals>"""));

    /// <summary>Programmatic ordinal replacements are honoured and filters rejected.</summary>
    [TestMethod]
    public void OrdinalReplacement_Programmatic_IsHonouredAndValidated()
    {
        var source = Build("""<Ordinals suffix="th" />""");
        var options = new NumberToStringConverterOptions(source)
        {
            OrdinalReplacements = [new ReplacementRule("feminine-eleven", "masculine-eleven", ReplacementScope.EndsWith)],
        };
        Assert.AreEqual("one hundred masculine-eleventh", new NumberToStringConverter(options).ConvertOrdinal(111));

        options.OrdinalReplacements = [new ReplacementRule("a", "b", ReplacementScope.Anywhere, 1)];
        Assert.Throws<ArgumentException>(() => new NumberToStringConverter(options));

        options.OrdinalReplacements = [null!];
        Assert.Throws<ArgumentException>(() => new NumberToStringConverter(options));
    }

    // ── Cloning and baseOn ──────────────────────────────────────────────────────────────────────

    /// <summary>Cloning a converter through its options preserves base and variant replacements.</summary>
    [TestMethod]
    public void OrdinalReplacement_OptionsClone_PreservesBaseAndVariantReplacements()
    {
        var original = Build("""
            <Ordinals suffix="th">
              <Replacement oldValue="feminine-eleven" newValue="x-eleven" scope="EndsWith" />
              <OrdinalVariants><Variant type="gender" variant="masc"><Replacement oldValue="x-eleven" newValue="y-eleven" scope="EndsWith" /></Variant></OrdinalVariants>
            </Ordinals>
            """);

        var clone = new NumberToStringConverter(new NumberToStringConverterOptions(original));

        Assert.AreEqual("one hundred x-eleventh", clone.ConvertOrdinal(111));
        Assert.AreEqual("one hundred y-eleventh", clone.ConvertOrdinal(111, "gender=masc"));
    }

    /// <summary>A child without ordinal replacements inherits the parent's; a child declaring its own replaces them.</summary>
    [TestMethod]
    public void OrdinalReplacement_BaseOn_InheritsOrReplacesTheList()
    {
        string parent = NewCulture();
        string inheriting = NewCulture();
        string replacing = NewCulture();
        string document = Document(parent,
            """<Ordinals suffix="th"><Replacement oldValue="feminine-eleven" newValue="x-eleven" scope="EndsWith" /></Ordinals>""",
            $"""
            <Language baseOn="{parent}"><Culture>{inheriting}</Culture><Ordinals suffix="TH" /></Language>
            <Language baseOn="{parent}"><Culture>{replacing}</Culture><Ordinals><Replacement oldValue="one hundred" newValue="a hundred" scope="StartsWith" /></Ordinals></Language>
            """);

        var converters = NumberToStringConverter.ReadConfiguration(document);

        Assert.AreEqual("one hundred x-elevenTH", converters[inheriting].ConvertOrdinal(111));
        Assert.AreEqual("a hundred feminine-eleventh", converters[replacing].ConvertOrdinal(111));
    }
}
