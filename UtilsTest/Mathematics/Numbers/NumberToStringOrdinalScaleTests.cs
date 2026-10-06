using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// Language-independent engine tests for <c>&lt;OrdinalScale&gt;</c>: the ordinal of a round scale
/// value (multiplier × scale unit, every lower group zero) is formed from the multiplier's cardinal and
/// the singular scale noun, the multiplier one being dropped, then run through the ordinal replacements,
/// word rules, stems and suffix. Covers selection of the highest scale, variants, precedence,
/// validation, programmatic configuration, cloning and <c>baseOn</c>.
/// </summary>
[TestClass]
public class NumberToStringOrdinalScaleTests
{
    /// <summary>Unit words of the synthetic language.</summary>
    private static readonly string[] Units = ["", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"];

    /// <summary>Tens words of the synthetic language.</summary>
    private static readonly string[] Tens = ["", "ten", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

    /// <summary>Creates a unique culture name.</summary>
    private static string NewCulture() => $"ORDSCALE-{Guid.NewGuid():N}";

    /// <summary>
    /// Creates a synthetic three-level language naming the scales thousand, million and billion only (up to 999 999 999 999),
    /// with a plain/fem gender dimension and the given ordinal block.
    /// </summary>
    /// <param name="culture">The culture identifier.</param>
    /// <param name="ordinals">The complete <c>&lt;Ordinals&gt;</c> element.</param>
    /// <param name="extraLanguages">Additional Language elements.</param>
    /// <returns>The configuration document.</returns>
    internal static string Document(string culture, string ordinals, string extraLanguages = "")
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
              <Language groupSize="3" separator=" " groupSeparator="" zero="zero" minus="minus *" decimalSeparator="point" maxNumber="999999999999">
                <Culture>{culture}</Culture>
                <Groups>
                  <Group level="1">{units}</Group>
                  <Group level="2">{tens}</Group>
                  <Group level="3">{hundreds}</Group>
                </Groups>
                <NumberScale firstLetterUpperCase="false"><StaticNames><Scale value="0" string="" /><Scale value="1" string="thousand" /><Scale value="2" string="million" /><Scale value="3" string="billion" /></StaticNames></NumberScale>
                {ordinals}
                <Variants><Dimension name="gender" values="plain,fem" /></Variants>
              </Language>
              {extraLanguages}
            </Numbers>
            """;
    }

    /// <summary>Builds the synthetic converter.</summary>
    /// <param name="ordinals">The complete <c>&lt;Ordinals&gt;</c> element.</param>
    /// <returns>The converter.</returns>
    internal static NumberToStringConverter Build(string ordinals)
    {
        string culture = NewCulture();
        return NumberToStringConverter.ReadConfiguration(Document(culture, ordinals))[culture];
    }

    /// <summary>The ordinal block with the given OrdinalScale elements, a "th" suffix and a feminine "the" suffix.</summary>
    /// <param name="scaleRules">The OrdinalScale elements.</param>
    /// <returns>The ordinal block.</returns>
    private static string Ordinals(string scaleRules) => $"""
        <Ordinals suffix="th">
          <OrdinalException value="1" string="first" />
          {scaleRules}
          <OrdinalVariants><Variant type="gender" variant="fem" suffix="the"><OrdinalException value="1" string="firsta" /></Variant></OrdinalVariants>
        </Ordinals>
        """;

    // ── Formation ───────────────────────────────────────────────────────────────────────────────

    /// <summary>Without OrdinalScale the ordinal of a round scale value keeps the historical cardinal-based form.</summary>
    [TestMethod]
    public void OrdinalScale_Absent_KeepsHistoricalOrdinals()
    {
        var converter = Build(Ordinals(""));

        Assert.AreEqual("one millionth", converter.ConvertOrdinal(1_000_000));
        Assert.AreEqual("two millionth", converter.ConvertOrdinal(2_000_000));
        Assert.AreEqual(0, converter.OrdinalScaleRules.Count);
    }

    /// <summary>A multiplier of one is dropped; a larger multiplier is its cardinal joined by the separator.</summary>
    /// <param name="separator">The configured multiplier separator.</param>
    /// <param name="number">The ordinal value.</param>
    /// <param name="expected">The expected ordinal.</param>
    [TestMethod]
    [DataRow("", 1_000_000L, "millionth")]
    [DataRow("", 2_000_000L, "twomillionth")]
    [DataRow("", 10_000_000L, "tenmillionth")]
    [DataRow("", 21_000_000L, "twenty onemillionth")]
    [DataRow("", 999_000_000L, "nine hundred ninety ninemillionth")]
    [DataRow("-", 1_000_000L, "millionth")]
    [DataRow("-", 2_000_000L, "two-millionth")]
    [DataRow("-", 10_000_000L, "ten-millionth")]
    [DataRow(" ", 2_000_000L, "two millionth")]
    public void OrdinalScale_FormsMultiplierAndScaleNoun(string separator, long number, string expected)
    {
        var converter = Build(Ordinals($"""<OrdinalScale scales="2..3" multiplierSeparator="{separator}" />"""));

        Assert.AreEqual(expected, converter.ConvertOrdinal(number));
    }

    /// <summary>The scale noun is the singular form whatever the multiplier, and the cardinal keeps its plural.</summary>
    [TestMethod]
    public void OrdinalScale_UsesTheSingularScaleNoun()
    {
        string culture = NewCulture();
        string document = Document(culture, Ordinals("""<OrdinalScale scales="2" multiplierSeparator="-" />"""))
            .Replace("""<Scale value="2" string="million" />""", """<Scale value="2" string="millio(n|ns)" />""");
        var converter = NumberToStringConverter.ReadConfiguration(document)[culture];

        Assert.AreEqual("two millions", converter.Convert(new BigInteger(2_000_000)));
        Assert.AreEqual("two-millionth", converter.ConvertOrdinal(2_000_000));
        Assert.AreEqual("millionth", converter.ConvertOrdinal(1_000_000));
    }

    /// <summary>The highest scale of a round value is selected, never a lower scale with a multiplier of a thousand or more.</summary>
    [TestMethod]
    public void OrdinalScale_SelectsTheHighestScale()
    {
        var converter = Build(Ordinals("""<OrdinalScale scales="1..3" multiplierSeparator="" />"""));

        Assert.AreEqual("thousandth", converter.ConvertOrdinal(1_000));
        Assert.AreEqual("twothousandth", converter.ConvertOrdinal(2_000));
        Assert.AreEqual("millionth", converter.ConvertOrdinal(1_000_000));
        Assert.AreEqual("billionth", converter.ConvertOrdinal(1_000_000_000));
        Assert.AreEqual("five hundredbillionth", converter.ConvertOrdinal(500_000_000_000));
    }

    /// <summary>A round value whose highest scale is outside the rule's scales keeps the historical form.</summary>
    [TestMethod]
    public void OrdinalScale_ScaleOutsideTheRule_KeepsHistoricalForm()
    {
        var converter = Build(Ordinals("""<OrdinalScale scales="2" multiplierSeparator="" />"""));

        Assert.AreEqual("twomillionth", converter.ConvertOrdinal(2_000_000));
        Assert.AreEqual("one billionth", converter.ConvertOrdinal(1_000_000_000));
        Assert.AreEqual("two thousandth", converter.ConvertOrdinal(2_000));
    }

    /// <summary>A value with any non-zero lower group is not round and keeps the historical form.</summary>
    /// <param name="number">The non-round value.</param>
    /// <param name="expected">The historical ordinal.</param>
    [TestMethod]
    [DataRow(2_000_001L, "two million oneth")]
    [DataRow(2_001_000L, "two million one thousandth")]
    [DataRow(1_000_000_001L, "one billion oneth")]
    public void OrdinalScale_NonRoundValue_KeepsHistoricalForm(long number, string expected)
    {
        var converter = Build(Ordinals("""<OrdinalScale scales="2..3" multiplierSeparator="" />"""));

        Assert.AreEqual(expected, converter.ConvertOrdinal(number));
    }

    // ── Variants and precedence ─────────────────────────────────────────────────────────────────

    /// <summary>The variant's suffix applies to the scale ordinal; no gender-specific rule is needed.</summary>
    [TestMethod]
    public void OrdinalScale_UsesTheVariantSuffix()
    {
        var converter = Build(Ordinals("""<OrdinalScale scales="2..3" multiplierSeparator="" />"""));

        Assert.AreEqual("millionthe", converter.ConvertOrdinal(1_000_000, "gender=fem"));
        Assert.AreEqual("twomillionthe", converter.ConvertOrdinal(2_000_000, "gender=fem"));
        Assert.AreEqual("millionth", converter.ConvertOrdinal(1_000_000, "gender=plain"));
    }

    /// <summary>The multiplier is rendered with the caller's variants (here through a feminine cardinal variant rule on scale 0 only, which must not leak into the noun).</summary>
    [TestMethod]
    public void OrdinalScale_RendersTheMultiplierWithTheVariantQuery()
    {
        string culture = NewCulture();
        string document = Document(culture, Ordinals("""<OrdinalScale scales="2" multiplierSeparator="-" />"""))
            .Replace("""<Variants><Dimension name="gender" values="plain,fem" /></Variants>""",
                """<Variants><Dimension name="gender" values="plain,fem" /><Variant type="gender" variant="fem"><Replacement oldValue="two" newValue="twa" scope="LastWord" onScale="0" /></Variant></Variants>""");
        var converter = NumberToStringConverter.ReadConfiguration(document)[culture];

        Assert.AreEqual("twa-millionthe", converter.ConvertOrdinal(2_000_000, "gender=fem"));
        Assert.AreEqual("two-millionth", converter.ConvertOrdinal(2_000_000));
    }

    /// <summary>A whole-number exception keeps precedence over the scale ordinal.</summary>
    [TestMethod]
    public void OrdinalScale_OrdinalException_KeepsPrecedence()
    {
        var converter = Build(Ordinals("""<OrdinalScale scales="2..3" multiplierSeparator="" /><OrdinalException value="2000000" string="special" />"""));

        Assert.AreEqual("special", converter.ConvertOrdinal(2_000_000));
        Assert.AreEqual("threemillionth", converter.ConvertOrdinal(3_000_000));
    }

    /// <summary>Ordinal replacements, word rules and stems see the scale ordinal's text.</summary>
    [TestMethod]
    public void OrdinalScale_RunsThroughReplacementsWordRulesAndStems()
    {
        var converter = Build(Ordinals("""
            <OrdinalScale scales="2..3" multiplierSeparator="" />
            <Replacement oldValue="twomillion" newValue="bimillion" scope="Anywhere" />
            <Ordinal from="billion" to="BILLIONTH" />
            <OrdinalStem from="ion" to="ione" />
            """));

        Assert.AreEqual("bimillioneth", converter.ConvertOrdinal(2_000_000));
        Assert.AreEqual("BILLIONTH", converter.ConvertOrdinal(1_000_000_000));
        Assert.AreEqual("threemillioneth", converter.ConvertOrdinal(3_000_000));
    }

    /// <summary>The configured prefix and the sign policy apply once to the scale ordinal.</summary>
    [TestMethod]
    public void OrdinalScale_NegativeValue_UsesTheSignPolicy()
    {
        var converter = Build(Ordinals("""<OrdinalScale scales="2" multiplierSeparator="" />"""));

        Assert.AreEqual("minus twomillionth", converter.ConvertOrdinal(-2_000_000));
    }

    /// <summary>Cardinals are never affected.</summary>
    [TestMethod]
    public void OrdinalScale_NeverAffectsCardinals()
    {
        var converter = Build(Ordinals("""<OrdinalScale scales="1..3" multiplierSeparator="" />"""));

        Assert.AreEqual("one million", converter.Convert(new BigInteger(1_000_000)));
        Assert.AreEqual("two thousand", converter.Convert(new BigInteger(2_000)));
    }

    /// <summary>
    /// Values beyond the highest configured scale (up to long.MaxValue) are not handled by the rule and
    /// behave exactly as without it: the round-value test never overflows.
    /// </summary>
    /// <param name="number">A value whose highest group is above the synthetic scales.</param>
    [TestMethod]
    [DataRow(1_000_000_000_000L)]
    [DataRow(9_000_000_000_000_000_000L)]
    [DataRow(long.MaxValue)]
    public void OrdinalScale_ValuesAboveTheConfiguredScales_BehaveAsWithoutTheRule(long number)
    {
        var withRule = Build(Ordinals("""<OrdinalScale scales="1..3" multiplierSeparator="" />"""));
        var withoutRule = Build(Ordinals(""));

        Assert.AreEqual(Outcome(() => withoutRule.ConvertOrdinal(number)), Outcome(() => withRule.ConvertOrdinal(number)));

        static string Outcome(Func<string> conversion)
        {
            try { return conversion(); }
            catch (Exception exception) { return "EXC:" + exception.GetType().Name; }
        }
    }

    // ── Validation ──────────────────────────────────────────────────────────────────────────────

    /// <summary>Invalid scale ranges are rejected at load.</summary>
    /// <param name="scales">The rejected scales expression.</param>
    [TestMethod]
    [DataRow("0")]
    [DataRow("0..2")]
    [DataRow("..2")]
    [DataRow("4")]
    [DataRow("2..4")]
    [DataRow("7")]
    [DataRow("x")]
    public void OrdinalScale_InvalidScales_AreRejected(string scales)
        => Assert.Throws<Exception>(() => Build(Ordinals($"""<OrdinalScale scales="{scales}" multiplierSeparator="" />""")));

    /// <summary>Two rules covering the same scale are rejected, whatever their declaration order.</summary>
    [TestMethod]
    public void OrdinalScale_OverlappingRules_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => Build(Ordinals("""<OrdinalScale scales="1..2" multiplierSeparator="" /><OrdinalScale scales="2..3" multiplierSeparator="-" />""")));
        Assert.Throws<ArgumentException>(() => Build(Ordinals("""<OrdinalScale scales="2" multiplierSeparator="" /><OrdinalScale scales="2" multiplierSeparator="" />""")));
    }

    /// <summary>Disjoint rules may use different separators.</summary>
    [TestMethod]
    public void OrdinalScale_DisjointRules_ApplyTheirOwnSeparator()
    {
        var converter = Build(Ordinals("""<OrdinalScale scales="1" multiplierSeparator="-" /><OrdinalScale scales="2..3" multiplierSeparator="" />"""));

        Assert.AreEqual("two-thousandth", converter.ConvertOrdinal(2_000));
        Assert.AreEqual("twomillionth", converter.ConvertOrdinal(2_000_000));
        Assert.AreEqual(2, converter.OrdinalScaleRules.Count);
    }

    /// <summary>Programmatic rules are honoured and validated (null rule, null scales, null separator).</summary>
    [TestMethod]
    public void OrdinalScale_Programmatic_IsHonouredAndValidated()
    {
        var source = Build(Ordinals(""));
        var options = new NumberToStringConverterOptions(source) { OrdinalScaleRules = [new OrdinalScaleRule("2", "-")] };
        Assert.AreEqual("two-millionth", new NumberToStringConverter(options).ConvertOrdinal(2_000_000));

        options.OrdinalScaleRules = [null!];
        Assert.Throws<ArgumentException>(() => new NumberToStringConverter(options));
        options.OrdinalScaleRules = [new OrdinalScaleRule(null!, "")];
        Assert.Throws<ArgumentException>(() => new NumberToStringConverter(options));
        options.OrdinalScaleRules = [new OrdinalScaleRule("2", null!)];
        Assert.Throws<ArgumentException>(() => new NumberToStringConverter(options));
    }

    /// <summary>The converter snapshots the rules: later changes to the source list are not observed.</summary>
    [TestMethod]
    public void OrdinalScale_Programmatic_IsSnapshotted()
    {
        var source = Build(Ordinals(""));
        var rules = new System.Collections.Generic.List<OrdinalScaleRule> { new("2", "-") };
        var converter = new NumberToStringConverter(new NumberToStringConverterOptions(source) { OrdinalScaleRules = rules });
        rules[0] = new OrdinalScaleRule("2", "+");
        rules.Add(new OrdinalScaleRule("3", "+"));

        Assert.AreEqual("two-millionth", converter.ConvertOrdinal(2_000_000));
        Assert.AreEqual(1, converter.OrdinalScaleRules.Count);
    }

    // ── Cloning and baseOn ──────────────────────────────────────────────────────────────────────

    /// <summary>Cloning a converter through its options preserves its scale rules.</summary>
    [TestMethod]
    public void OrdinalScale_OptionsClone_PreservesTheRules()
    {
        var original = Build(Ordinals("""<OrdinalScale scales="2..3" multiplierSeparator="-" />"""));

        var clone = new NumberToStringConverter(new NumberToStringConverterOptions(original));

        Assert.AreEqual("two-millionth", clone.ConvertOrdinal(2_000_000));
        Assert.AreEqual("billionth", clone.ConvertOrdinal(1_000_000_000));
        CollectionAssert.AreEqual(original.OrdinalScaleRules.ToArray(), clone.OrdinalScaleRules.ToArray());
    }

    /// <summary>A child without scale rules inherits the parent's; a child declaring its own replaces them.</summary>
    [TestMethod]
    public void OrdinalScale_BaseOn_InheritsOrReplacesTheList()
    {
        string parent = NewCulture();
        string inheriting = NewCulture();
        string replacing = NewCulture();
        string document = Document(parent, Ordinals("""<OrdinalScale scales="2..3" multiplierSeparator="" />"""),
            $"""
            <Language baseOn="{parent}"><Culture>{inheriting}</Culture><Ordinals suffix="TH" /></Language>
            <Language baseOn="{parent}"><Culture>{replacing}</Culture><Ordinals><OrdinalScale scales="2" multiplierSeparator="-" /></Ordinals></Language>
            """);

        var converters = NumberToStringConverter.ReadConfiguration(document);

        Assert.AreEqual("twomillionTH", converters[inheriting].ConvertOrdinal(2_000_000));
        Assert.AreEqual("two-millionth", converters[replacing].ConvertOrdinal(2_000_000));
        Assert.AreEqual("one billionth", converters[replacing].ConvertOrdinal(1_000_000_000));
    }
}
