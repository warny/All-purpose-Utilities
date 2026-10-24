using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Xml.Schema;
using Utils.NumberToString;
using Utils.Numerics;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-30G: two independent generic primitives and the two production defects they close.
/// <list type="number">
/// <item><description>
/// <c>finalGroupConnector</c> (<see cref="NumberToStringConverterOptions.FinalGroupConnector"/>): a connector placed
/// between groups only, before the last non-zero group of every numeral phrase; Basque <c>eta</c> (Euskaltzaindia
/// rule 203).
/// </description></item>
/// <item><description>
/// Variants forced by the forms of an exact <c>&lt;ScaleForm&gt;</c>
/// (<see cref="NumberToStringConverterOptions.ScaleFormMultiplierForcedVariants"/>), with the NTS-30C contract of
/// <see cref="GeneratedScaleForm"/>; Spanish <c>millón</c>/<c>millones</c> force a masculine multiplier (RAE DPD).
/// </description></item>
/// </list>
/// The synthetic languages deliberately do not follow any real grammar. Sources are in
/// <c>docs/NTS-08-linguistic-sources.md</c>, NTS-30G section.
/// </summary>
[TestClass]
public class NumberToStringNts30GTests
{
    // ===== shared synthetic language =================================================================

    /// <summary>Unit words of the synthetic languages.</summary>
    private static readonly string[] Units = ["", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"];

    /// <summary>Tens words of the synthetic languages.</summary>
    private static readonly string[] Tens = ["", "ten", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

    /// <summary>Atomic 11–19 of the synthetic languages.</summary>
    private static readonly string[] Teens = ["eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen"];

    /// <summary>Creates a unique culture or registration name.</summary>
    /// <returns>The name.</returns>
    private static string NewName() => $"NTS30G-{Guid.NewGuid():N}";

    /// <summary>
    /// Creates a synthetic language: groups of three digits, atomic 11–19, static scales "thousand" and
    /// "million(s)" (and "billion" unless the scales are paired from the million), and a gender dimension a/b/c.
    /// </summary>
    /// <param name="culture">The culture identifier.</param>
    /// <param name="attributes">Extra attributes of the Language element.</param>
    /// <param name="scaleAttributes">Extra attributes of the NumberScale element.</param>
    /// <param name="scaleContent">Elements appended inside the NumberScale (ScaleForm …).</param>
    /// <param name="variants">The Variant elements of the gender dimension.</param>
    /// <param name="extra">Additional elements appended to the Language after its Variants (Trigger …).</param>
    /// <param name="extraLanguages">Additional Language elements.</param>
    /// <param name="tensFusion">An optional Fusion element placed on the tens digit 2.</param>
    /// <returns>The configuration document.</returns>
    private static string Document(string culture, string attributes = "", string scaleAttributes = "", string scaleContent = "",
        string variants = "", string extra = "", string extraLanguages = "", string tensFusion = "")
    {
        bool paired = scaleAttributes.Contains("pairedScalesFrom", StringComparison.Ordinal);
        string units = string.Concat(Units.Select((u, i) => $"""<Digit digit="{i}" string="{u}" />"""));
        string tens = string.Concat(Tens.Select((t, i) => i == 0
            ? """<Digit digit="0" string="" buildString="*" />"""
            : i == 2 && tensFusion.Length > 0
                ? $"""<Digit digit="{i}" string="{t}" buildString="{t} *">{tensFusion}</Digit>"""
                : $"""<Digit digit="{i}" string="{t}" buildString="{t} *" />"""));
        string hundreds = string.Concat(Units.Select((u, i) => i == 0
            ? """<Digit digit="0" string="" buildString="*" />"""
            : $"""<Digit digit="{i}" string="{u} hundred" buildString="{u} hundred *" />"""));
        string teens = string.Concat(Teens.Select((t, i) => $"""<Number value="{i + 11}" string="{t}" />"""));
        string billion = paired ? "" : """<Scale value="3" string="billion" />""";
        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
              <Language groupSize="3" separator=" " groupSeparator="" zero="zero" minus="minus *" decimalSeparator="point"
                        fractionSeparator="over" maxNumber="999999999999" {attributes}>
                <Culture>{culture}</Culture>
                <Groups>
                  <Group level="1">{units}</Group>
                  <Group level="2">{tens}</Group>
                  <Group level="3">{hundreds}</Group>
                </Groups>
                <NumberScale firstLetterUpperCase="false" {scaleAttributes}>
                  <StaticNames>
                    <Scale value="0" string="" /><Scale value="1" string="thousand" /><Scale value="2" string="milli(on|ons)" />{billion}
                  </StaticNames>
                  {scaleContent}
                </NumberScale>
                <Exceptions>{teens}</Exceptions>
                <Variants>
                  <Dimension name="gender" values="a,b,c" />
                  {variants}
                </Variants>
                {extra}
              </Language>
              {extraLanguages}
            </Numbers>
            """;
    }

    /// <summary>Builds a synthetic converter.</summary>
    /// <param name="attributes">Extra attributes of the Language element.</param>
    /// <param name="scaleAttributes">Extra attributes of the NumberScale element.</param>
    /// <param name="scaleContent">Elements appended inside the NumberScale.</param>
    /// <param name="variants">The Variant elements of the gender dimension.</param>
    /// <param name="extra">Additional elements appended to the Language after its Variants.</param>
    /// <param name="tensFusion">An optional Fusion element placed on the tens digit 2.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Build(string attributes = "", string scaleAttributes = "", string scaleContent = "",
        string variants = "", string extra = "", string tensFusion = "")
    {
        string culture = NewName();
        return NumberToStringConverter.ReadConfiguration(
            Document(culture, attributes, scaleAttributes, scaleContent, variants, extra, "", tensFusion))[culture];
    }

    /// <summary>Gets a built-in converter.</summary>
    /// <param name="language">The culture.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Get(string language) => NumberToStringConverter.GetConverter(language);

    // ===== generic finalGroupConnector ===============================================================

    /// <summary>Builds the synthetic converter with the final-group connector "and".</summary>
    /// <param name="attributes">Further attributes of the Language element.</param>
    /// <param name="scaleAttributes">Extra attributes of the NumberScale element.</param>
    /// <param name="tensFusion">An optional Fusion element placed on the tens digit 2.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter BuildAnd(string attributes = "", string scaleAttributes = "", string tensFusion = "")
        => Build($"""finalGroupConnector="and" {attributes}""", scaleAttributes, tensFusion: tensFusion);

    /// <summary>Without the attribute the converter has no final-group connector and composes as before.</summary>
    [TestMethod]
    public void FinalGroup_Absent_HistoricalComposition()
    {
        var converter = Build();
        Assert.IsNull(converter.FinalGroupConnector);
        Assert.AreEqual("one million one thousand one", converter.Convert(1_001_001));
        Assert.AreEqual("one hundred twenty one", converter.Convert(121));
    }

    /// <summary>The connector precedes the last non-zero group only, whatever that group holds.</summary>
    [TestMethod]
    [DataRow(1_001L, "one thousand and one")]
    [DataRow(1_121L, "one thousand and one hundred twenty one")]
    [DataRow(1_000_001L, "one million and one")]
    [DataRow(1_001_000L, "one million and one thousand")]
    [DataRow(1_001_001L, "one million one thousand and one")]
    [DataRow(2_000_200_015L, "two billion two hundred thousand and fifteen")]
    [DataRow(2_003_004_005L, "two billion three millions four thousand and five")]
    public void FinalGroup_PrecedesTheLastNonZeroGroup(long value, string expected)
        => Assert.AreEqual(expected, BuildAnd().Convert(value));

    /// <summary>A single group has no inter-group junction: the composition inside a group is never affected.</summary>
    [TestMethod]
    [DataRow(1L, "one")]
    [DataRow(21L, "twenty one")]
    [DataRow(121L, "one hundred twenty one")]
    [DataRow(15L, "fifteen")]
    [DataRow(1_000L, "one thousand")]
    [DataRow(5_000_000L, "five millions")]
    public void FinalGroup_NeverEntersAGroup(long value, string expected)
        => Assert.AreEqual(expected, BuildAnd().Convert(value));

    /// <summary>Exactly one connector per phrase: every value of the matrix has one "and" per junction before its last group.</summary>
    [TestMethod]
    public void FinalGroup_ExactlyOneConnectorPerPhrase()
    {
        var converter = BuildAnd();
        foreach (long high in new long[] { 0, 1, 21, 999 })
            foreach (long middle in new long[] { 0, 1, 115 })
                foreach (long low in new long[] { 0, 1, 120 })
                {
                    long value = high * 1_000_000 + middle * 1_000 + low;
                    if (value == 0) continue;
                    int groups = (high > 0 ? 1 : 0) + (middle > 0 ? 1 : 0) + (low > 0 ? 1 : 0);
                    int connectors = converter.Convert(value).Split(' ').Count(word => word == "and");
                    Assert.AreEqual(groups > 1 ? 1 : 0, connectors, $"value {value}");
                }
    }

    /// <summary>
    /// Precedence with the group connector: the final-group connector claims the junction before the last group, the
    /// other junctions keep the group connector below its threshold, or the group separator.
    /// </summary>
    [TestMethod]
    public void FinalGroup_TakesPrecedenceOverTheGroupConnectorAtTheLastJunction()
    {
        var converter = BuildAnd("""groupConnector="plus" groupConnectorThreshold="100" """);

        Assert.AreEqual("one million plus five thousand and one", converter.Convert(1_005_001));
        Assert.AreEqual("one million two hundred thousand and one", converter.Convert(1_200_001));
        Assert.AreEqual("one million and five", converter.Convert(1_000_005));
        Assert.AreEqual("one million and two hundred", converter.Convert(1_000_200));
    }

    /// <summary>The intra-group connector and a Fusion compose inside the groups, independently of the final-group connector.</summary>
    [TestMethod]
    public void FinalGroup_IsOrthogonalToTheIntraGroupConnectorAndFusions()
    {
        var intra = BuildAnd("""intraGroupConnector="o" intraGroupConnectorThreshold="10" """);
        Assert.AreEqual("one hundred o one", intra.Convert(101));
        Assert.AreEqual("one hundred o one thousand and one hundred o one", intra.Convert(101_101));

        var fused = BuildAnd(tensFusion: """<Fusion for="1..9" />""");
        Assert.AreEqual("twentyone", fused.Convert(21));
        Assert.AreEqual("twentyone thousand and twentytwo", fused.Convert(21_022));
    }

    /// <summary>The scale connector joins a multiplier to its scale noun: it never meets the final-group connector.</summary>
    [TestMethod]
    public void FinalGroup_IsOrthogonalToTheScaleConnector()
    {
        var converter = BuildAnd("""scaleConnector="of" scaleConnectorThreshold="20" """);
        Assert.AreEqual("twenty of thousand and one", converter.Convert(20_001));
        Assert.AreEqual("two thousand and one", converter.Convert(2_001));
    }

    /// <summary>
    /// A paired multiplier is a numeral phrase of its own: its connector precedes its own last group, and the whole
    /// number places one more before its last group (structural, no text parsing).
    /// </summary>
    [TestMethod]
    [DataRow(2_002_000_000L, "two thousand and two millions")]
    [DataRow(2_002_000_001L, "two thousand and two millions and one")]
    [DataRow(2_002_001_001L, "two thousand and two millions one thousand and one")]
    [DataRow(2_000_000_001L, "two thousand millions and one")]
    [DataRow(1_000_001L, "one million and one")]
    public void FinalGroup_PairedMultiplierIsAPhraseOfItsOwn(long value, string expected)
        => Assert.AreEqual(expected, BuildAnd(scaleAttributes: """pairedScalesFrom="2" """).Convert(value));

    /// <summary>Variants apply after the connector, on the whole number.</summary>
    [TestMethod]
    public void FinalGroup_VariantsApplyAfterTheConnector()
    {
        string culture = NewName();
        var converter = NumberToStringConverter.ReadConfiguration(Document(culture, """finalGroupConnector="and" """,
            variants: """<Variant type="gender" variant="b"><Replacement oldValue="one" newValue="oneB" scope="LastWord" /></Variant>"""))[culture];
        Assert.AreEqual("two thousand and oneB", converter.Convert(2_001, "gender=b"));
        Assert.AreEqual("two thousand and one", converter.Convert(2_001));
    }

    /// <summary>The composite surfaces reuse the cardinal and its connector.</summary>
    [TestMethod]
    public void FinalGroup_CompositeSurfacesReuseTheCardinal()
    {
        var converter = BuildAnd();
        Assert.AreEqual("minus one million and one", converter.Convert(-1_000_001));
        Assert.AreEqual("two thousand and one point five", converter.Convert(2_001.5m));
        Assert.AreEqual("two thousand and one over three", converter.ConvertFraction(2_001, 3));
        Assert.AreEqual("two thousand and two over three", converter.Convert(new Number(2_002, 3)));
        var currency = new CurrencyDefinition { UnitSingular = "euro", UnitPlural = "euros", SubunitSingular = "cent", SubunitPlural = "cents" };
        StringAssert.StartsWith(converter.ConvertCurrency(2_001m, currency), "two thousand and one ");
    }

    /// <summary><c>baseOn</c>: absent inherits, an explicit value replaces, an explicit empty value removes the connector.</summary>
    [TestMethod]
    public void FinalGroup_IsInheritedThroughBaseOn()
    {
        string parent = NewName();
        string inheriting = NewName();
        string replacing = NewName();
        string removing = NewName();
        string document = Document(parent, """finalGroupConnector="and" """, extraLanguages: $"""
            <Language baseOn="{parent}"><Culture>{inheriting}</Culture></Language>
            <Language baseOn="{parent}" finalGroupConnector="plus"><Culture>{replacing}</Culture></Language>
            <Language baseOn="{parent}" finalGroupConnector=""><Culture>{removing}</Culture></Language>
            """);
        var converters = NumberToStringConverter.ReadConfiguration(document);

        Assert.AreEqual("and", converters[inheriting].FinalGroupConnector);
        Assert.AreEqual("one thousand and one", converters[inheriting].Convert(1_001));
        Assert.AreEqual("plus", converters[replacing].FinalGroupConnector);
        Assert.AreEqual("one thousand plus one", converters[replacing].Convert(1_001));
        Assert.IsNull(converters[removing].FinalGroupConnector);
        Assert.AreEqual("one thousand one", converters[removing].Convert(1_001));
    }

    /// <summary>The programmatic options carry the connector, and cloning a converter through its options keeps it.</summary>
    [TestMethod]
    public void FinalGroup_ProgrammaticOptionsAndClone()
    {
        var original = BuildAnd();
        var options = new NumberToStringConverterOptions(original);
        Assert.AreEqual("and", options.FinalGroupConnector);
        var clone = new NumberToStringConverter(options);
        Assert.AreEqual("and", clone.FinalGroupConnector);
        Assert.AreEqual("one million one thousand and one", clone.Convert(1_001_001));

        var programmatic = new NumberToStringConverter(new NumberToStringConverterOptions(Build()) { FinalGroupConnector = "et" });
        Assert.AreEqual("et", programmatic.FinalGroupConnector);
        Assert.AreEqual("one thousand et one", programmatic.Convert(1_001));
        var blank = new NumberToStringConverter(new NumberToStringConverterOptions(original) { FinalGroupConnector = "  " });
        Assert.IsNull(blank.FinalGroupConnector);
        Assert.AreEqual("one thousand one", blank.Convert(1_001));
    }

    /// <summary>
    /// The final-component connector also claims the junction before the last group: the combination is rejected,
    /// from XML as from the programmatic options; every other connector combines.
    /// </summary>
    [TestMethod]
    public void FinalGroup_CombinedWithTheFinalComponentConnector_IsRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Build("""finalGroupConnector="and" finalComponentConnector="and" """));
        Assert.ThrowsExactly<ArgumentException>(() => new NumberToStringConverter(
            new NumberToStringConverterOptions(Build("""finalComponentConnector="and" """)) { FinalGroupConnector = "and" }));
        Assert.IsNotNull(BuildAnd("""groupConnector="plus" intraGroupConnector="o" scaleConnector="of" """));
    }

    // ===== generic exact ScaleForm forced variants ===================================================

    /// <summary>The gender rules of the forced-variant tests: b and c rewrite digits anywhere, a tries to undo them.</summary>
    private const string GenderRules = """
        <Variant type="gender" variant="b">
          <Replacement oldValue="one" newValue="oneB" scope="Anywhere" />
          <Replacement oldValue="two" newValue="twoB" scope="Anywhere" />
        </Variant>
        <Variant type="gender" variant="c">
          <Replacement oldValue="two" newValue="twoC" scope="Anywhere" />
        </Variant>
        <Variant type="gender" variant="a">
          <Replacement oldValue="five" newValue="fiveA" scope="Anywhere" />
          <Replacement oldValue="twoB" newValue="twoA" scope="Anywhere" />
          <Replacement oldValue="twoC" newValue="twoA" scope="Anywhere" />
        </Variant>
        """;

    /// <summary>Exact scale 2 whose singular and plural force gender b on their multiplier.</summary>
    private const string MillionForcesB = """<ScaleForm scale="2"><Forms><Form key="singular" value="million" forceVariants="gender=b" /><Form key="plural" value="millions" forceVariants="gender=b" /></Forms></ScaleForm>""";

    /// <summary>Exact scale 1 whose singular and plural force gender c on their multiplier.</summary>
    private const string ThousandForcesC = """<ScaleForm scale="1"><Forms><Form key="singular" value="thousand" forceVariants="gender=c" /><Form key="plural" value="thousand" forceVariants="gender=c" /></Forms></ScaleForm>""";

    /// <summary>Builds the forced-variant synthetic converter.</summary>
    /// <param name="scaleContent">The ScaleForm elements.</param>
    /// <param name="scaleAttributes">Extra attributes of the NumberScale element.</param>
    /// <param name="extraVariants">Further Variant elements.</param>
    /// <param name="extra">Additional elements appended to the Language (Trigger …).</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter BuildForced(string scaleContent = MillionForcesB, string scaleAttributes = "",
        string extraVariants = "", string extra = "")
        => Build(scaleAttributes: scaleAttributes, scaleContent: scaleContent, variants: GenderRules + extraVariants, extra: extra);

    /// <summary>The selected form's forced variants render the whole multiplier, against the caller's value.</summary>
    [TestMethod]
    public void ScaleForm_ForcedVariantsRenderTheWholeMultiplier()
    {
        var converter = BuildForced();

        Assert.AreEqual("twoB millions", converter.Convert(2_000_000));
        Assert.AreEqual("twoB millions", converter.Convert(2_000_000, "gender=a"));
        Assert.AreEqual("twoB hundred twenty twoB millions", converter.Convert(222_000_000, "gender=a"));
        Assert.AreEqual("oneB million", converter.Convert(1_000_000, "gender=c"), "the singular form forces too");
        Assert.AreEqual("twoB millions", converter.Convert(2_000_000, "gender=c"), "b wins over the caller's c locally");
    }

    /// <summary>The caller's variants keep reaching the groups the scale noun does not govern.</summary>
    [TestMethod]
    public void ScaleForm_CallerVariantsKeepReachingTheOtherGroups()
    {
        var converter = BuildForced();

        Assert.AreEqual("five millions fiveA", converter.Convert(5_000_005, "gender=a"));
        Assert.AreEqual("twoB millions twoC thousand twoC", converter.Convert(2_002_002, "gender=c"));
        Assert.AreEqual("twoB millions two thousand", converter.Convert(2_002_000, "gender=a"));
        Assert.AreEqual("fiveA thousand", converter.Convert(5_000, "gender=a"));
    }

    /// <summary>The forced variants reach the group triggers of the forced group only.</summary>
    [TestMethod]
    public void ScaleForm_ForcedVariantsReachGroupTriggers()
    {
        var converter = BuildForced(extra: """<Trigger executeAt="group(1,2)"><Replace from="four"><Variant type="gender" variant="b" value="fourT" /></Replace></Trigger>""");

        Assert.AreEqual("fourT millions", converter.Convert(4_000_000, "gender=a"));
        Assert.AreEqual("four thousand", converter.Convert(4_000, "gender=a"), "an unforced group keeps the caller's query");
        Assert.AreEqual("fourT thousand", converter.Convert(4_000, "gender=b"));
    }

    /// <summary>The forced variants reach the scale-scoped (<c>onScale</c>) variant rules of the group.</summary>
    [TestMethod]
    public void ScaleForm_ForcedVariantsReachScaleScopedVariantRules()
    {
        var converter = BuildForced(extraVariants: """<Variant type="gender" variant="b" priority="1"><Replacement oldValue="three" newValue="threeB" scope="StartsWith" onScale="2" /></Variant>""");

        Assert.AreEqual("threeB millions", converter.Convert(3_000_000, "gender=a"));
        Assert.AreEqual("three thousand", converter.Convert(3_000, "gender=b"), "the rule is scoped to scale 2");
    }

    /// <summary>The forced variants reach the group-with-scale triggers of the group.</summary>
    [TestMethod]
    public void ScaleForm_ForcedVariantsReachGroupWithScaleTriggers()
    {
        var converter = BuildForced(extra: """<Trigger executeAt="groupWithScale(2)"><Replace from="twoB millions"><Variant type="gender" variant="b" value="twoB millions!" /></Replace></Trigger>""");

        Assert.AreEqual("twoB millions!", converter.Convert(2_000_000, "gender=a"));
        Assert.AreEqual("twoB millions! fiveA", converter.Convert(2_000_005));
    }

    /// <summary>
    /// The forced group is shielded from the whole-number passes run with the caller's query: neither a global variant
    /// rule nor an end trigger of the caller's value rewrites it, while the rest of the number still follows them.
    /// </summary>
    [TestMethod]
    public void ScaleForm_ForcedGroupIsShieldedFromTheCallerPasses()
    {
        var converter = BuildForced(extra: """<Trigger executeAt="end"><Replace from="twoB"><Variant type="gender" variant="a" value="twoEnd" /></Replace></Trigger>""");

        Assert.AreEqual("twoB millions", converter.Convert(2_000_000, "gender=a"));
        Assert.AreEqual("twoB millions fiveA", converter.Convert(2_000_005, "gender=a"));
    }

    /// <summary>
    /// A language with forcing exact forms and no generated form still collects the shielded groups (NTS-30C reserved
    /// the shield list to generated forms); a language using neither allocates none.
    /// </summary>
    [TestMethod]
    public void ScaleForm_ShieldsAreAllocatedForForcingExactForms()
    {
        var method = typeof(NumberToStringConverter).GetMethod("NewShieldedGroups", BindingFlags.Instance | BindingFlags.NonPublic)!;

        var forcing = BuildForced(ThousandForcesC);
        Assert.AreEqual(0, forcing.GeneratedScaleForms.Count);
        Assert.IsNotNull(method.Invoke(forcing, []));
        Assert.AreEqual("twoC thousand", forcing.Convert(2_000, "gender=a"), "without the shield, gender=a would give twoA");

        Assert.IsNull(method.Invoke(Build(variants: GenderRules), []));
        Assert.IsNull(method.Invoke(Build(scaleContent: """<ScaleForm scale="2"><Forms><Form key="plural" value="millions" /></Forms></ScaleForm>""", variants: GenderRules), []),
            "an exact form forcing nothing allocates nothing");
        Assert.IsNull(method.Invoke(Get("EN"), []), "a built-in language using neither primitive");
    }

    /// <summary>On a paired scale the forced variants render the complete two-group multiplier.</summary>
    [TestMethod]
    public void ScaleForm_PairedMultiplierIsRenderedWholeWithTheForcedVariants()
    {
        var converter = BuildForced(scaleAttributes: """pairedScalesFrom="2" """);

        Assert.AreEqual("twoB thousand twoB millions", converter.Convert(2_002_000_000L, "gender=a"));
        Assert.AreEqual("twoB hundred thousand oneB millions fiveA", converter.Convert(200_001_000_005L, "gender=a"));
        Assert.AreEqual("twoB millions", converter.Convert(2_000_000, "gender=a"), "one-group multiplier");
    }

    /// <summary>
    /// A forced group inside a forced paired multiplier keeps its own value (c inside b) and stays shielded from the
    /// multiplier's own pass as from the caller's whole-number passes.
    /// </summary>
    [TestMethod]
    public void ScaleForm_NestedForcedGroupsStayShielded()
    {
        var converter = BuildForced(ThousandForcesC + MillionForcesB, """pairedScalesFrom="2" """);

        Assert.AreEqual("twoC thousand twoB millions", converter.Convert(2_002_000_000L, "gender=a"));
        Assert.AreEqual("twoC thousand twoB millions twoC thousand fiveA", converter.Convert(2_002_002_005L, "gender=a"));
        Assert.AreEqual("twoC thousand twoB millions", converter.Convert(2_002_000_000L, "gender=b"));
    }

    /// <summary>
    /// A forced group inside an unforced paired multiplier is propagated to the whole-number shield: the caller's passes
    /// still reach the rest of the multiplier.
    /// </summary>
    [TestMethod]
    public void ScaleForm_ForcedGroupInsideAnUnforcedPairedMultiplierStaysShielded()
    {
        var converter = BuildForced(ThousandForcesC, """pairedScalesFrom="2" """);

        Assert.AreEqual("twoC thousand two millions", converter.Convert(2_002_000_000L, "gender=a"));
        Assert.AreEqual("twoC thousand twoB millions", converter.Convert(2_002_000_000L, "gender=b"));
    }

    /// <summary>End trigger of the caller's gender a trying to rewrite the forced spellings.</summary>
    private const string EndTriggerA = """<Trigger executeAt="end"><Replace from="twoB"><Variant type="gender" variant="a" value="twoEndB" /></Replace><Replace from="twoC"><Variant type="gender" variant="a" value="twoEndC" /></Replace></Trigger>""";

    /// <summary>
    /// <c>ConvertMultiplicative</c> renders an unnamed multiplicative through the cardinal: its whole-number variant
    /// pass and end triggers, run with the caller's query, must leave the forced group untouched.
    /// </summary>
    [TestMethod]
    public void ScaleForm_Multiplicative_ForcedGroupIsShielded()
    {
        var converter = BuildForced(extra: """<Multiplicatives suffix=" times" />""" + EndTriggerA);

        Assert.AreEqual("twoB millions times", converter.ConvertMultiplicative(2_000_000, "gender=a"));
        Assert.AreEqual("twoB millions fiveA times", converter.ConvertMultiplicative(2_000_005, "gender=a"));
        Assert.AreEqual("twoB millions twoC times", converter.ConvertMultiplicative(2_000_002, "gender=c"));
        Assert.AreEqual("minus twoB millions times", converter.ConvertMultiplicative(-2_000_000, "gender=a"));
    }

    /// <summary>
    /// An <c>OrdinalScale</c> builds a round ordinal on its scale noun from the multiplier's cardinal: the variants the
    /// noun's form forces render that multiplier, as in the cardinal, and survive the caller's variant pass.
    /// </summary>
    [TestMethod]
    public void ScaleForm_OrdinalScale_ForcedVariantsRenderTheMultiplier()
    {
        var converter = BuildForced(extra: """<Ordinals suffix="th"><OrdinalScale scales="2" multiplierSeparator=" " /></Ordinals>""");

        Assert.AreEqual("twoB millionth", converter.ConvertOrdinal(2_000_000L, "gender=a"));
        Assert.AreEqual("twoB hundred twenty twoB millionth", converter.ConvertOrdinal(222_000_000L, "gender=a"));
        Assert.AreEqual("millionth", converter.ConvertOrdinal(1_000_000L, "gender=a"), "a multiplier of one is the noun alone");
        Assert.AreEqual("two thousandth", converter.ConvertOrdinal(2_000L, "gender=a"), "scale 1 is not covered: cardinal path, unforced");
    }

    /// <summary>
    /// An <c>OrdinalScale</c> builds on the singular scale noun whatever the multiplier, so the variants forced on the
    /// multiplier are those of that singular form, not those of the form the cardinal selects for the same multiplier.
    /// </summary>
    [TestMethod]
    public void ScaleForm_OrdinalScale_UsesTheForcedVariantsOfTheSingularForm()
    {
        var converter = BuildForced(
            """<ScaleForm scale="2"><Forms><Form key="singular" value="million" forceVariants="gender=c" /><Form key="plural" value="millions" /></Forms></ScaleForm>""",
            extra: """<Ordinals suffix="th"><OrdinalScale scales="2" multiplierSeparator=" " /></Ordinals>""");

        Assert.AreEqual("twoC millionth", converter.ConvertOrdinal(2_000_000L, "gender=a"), "the ordinal uses the singular form");
        Assert.AreEqual("two millions", converter.Convert(2_000_000, "gender=a"), "the cardinal uses the plural form, which forces nothing");
        Assert.AreEqual("five millionth", converter.ConvertOrdinal(5_000_000L, "gender=a"), "c rewrites two only; the caller's a pass is shielded off");
    }

    /// <summary>
    /// The ordinal end triggers run with the caller's query on the finished ordinal: a forced group the ordinal
    /// transformation left intact stays shielded, on the round-scale path as on the cardinal path.
    /// </summary>
    [TestMethod]
    public void ScaleForm_OrdinalEndTriggers_LeaveTheForcedGroupUntouched()
    {
        var roundScale = BuildForced(extra: """<Ordinals suffix="th"><OrdinalScale scales="2" multiplierSeparator=" " /></Ordinals>""" + EndTriggerA);
        Assert.AreEqual("twoB millionth", roundScale.ConvertOrdinal(2_000_000L, "gender=a"));

        var cardinal = BuildForced(ThousandForcesC, extra: """<Ordinals suffix="th" /><Trigger executeAt="end"><Replace from="twoC"><Variant type="gender" variant="c" value="twoEnd" /></Replace></Trigger>""");
        Assert.AreEqual("twoC thousand fiveAth", cardinal.ConvertOrdinal(2_005L, "gender=a"));
        Assert.AreEqual("twoC thousand twoEndth", cardinal.ConvertOrdinal(2_002L, "gender=c"),
            "the forced group keeps its own twoC, the caller's end trigger still reaches the unforced twoC");
    }

    /// <summary>A ScaleForm without forceVariants behaves exactly as before: the caller's query reaches the multiplier.</summary>
    [TestMethod]
    public void ScaleForm_WithoutForcedVariants_KeepsHistoricalOutput()
    {
        var plain = Build(variants: GenderRules);
        var unforced = Build(scaleContent: """<ScaleForm scale="2"><Forms><Form key="singular" value="million" /><Form key="plural" value="millions" /></Forms></ScaleForm>""", variants: GenderRules);
        Assert.AreEqual(0, unforced.ScaleFormMultiplierForcedVariants.Count);

        foreach (long value in new long[] { 1_000_000, 2_000_000, 2_002_002, 5_000_005, 222_000_000, 1_002_000_001 })
            foreach (string[] variants in new string[][] { [], ["gender=a"], ["gender=b"], ["gender=c"] })
                Assert.AreEqual(plain.Convert(value, variants), unforced.Convert(value, variants), $"{value} {string.Join(",", variants)}");
        Assert.AreEqual("two millions", unforced.Convert(2_000_000, "gender=a"));
        Assert.AreEqual("twoB millions", unforced.Convert(2_000_000, "gender=b"));
    }

    /// <summary>The selector chooses the form, so each form forces its own variants (or none).</summary>
    [TestMethod]
    public void ScaleForm_EachSelectedFormForcesItsOwnVariants()
    {
        var converter = BuildForced("""<ScaleForm scale="2"><Forms><Form key="singular" value="million" forceVariants="gender=c" /><Form key="plural" value="millions" /></Forms></ScaleForm>""");

        Assert.AreEqual("one million", converter.Convert(1_000_000, "gender=b"), "c forces nothing on one");
        Assert.AreEqual("two millions", converter.Convert(2_000_000, "gender=a"), "the plural forces nothing");
        Assert.AreEqual("twoB millions", converter.Convert(2_000_000, "gender=b"));
    }

    /// <summary>A forced dimension or value the language does not declare is rejected while loading, with its path.</summary>
    [TestMethod]
    public void ScaleForm_UnknownForcedVariant_IsRejected()
    {
        var dimension = Assert.ThrowsExactly<NumberToStringConfigurationException>(() =>
            BuildForced("""<ScaleForm scale="2"><Forms><Form key="plural" value="millions" forceVariants="case=b" /></Forms></ScaleForm>"""));
        Assert.AreEqual("UNTS006", dimension.ErrorCode);
        StringAssert.Contains(dimension.Message, "ScaleForms[2].plural");
        var value = Assert.ThrowsExactly<NumberToStringConfigurationException>(() =>
            BuildForced("""<ScaleForm scale="2"><Forms><Form key="singular" value="million" forceVariants="gender=z" /></Forms></ScaleForm>"""));
        Assert.AreEqual("UNTS006", value.ErrorCode);
        StringAssert.Contains(value.Message, "ScaleForms[2].singular");
    }

    /// <summary>forceVariants is a scale-form attribute only: the schema rejects it on a time-unit form.</summary>
    [TestMethod]
    public void ScaleForm_ForceVariantsOnATimeUnitForm_IsRejectedBySchema()
        => Assert.ThrowsExactly<XmlSchemaValidationException>(() => Build(extra: """
            <TimeUnits><Unit name="hour" singular="h" plural="hs"><Forms><Form key="singular" value="h" forceVariants="gender=b" /></Forms></Unit></TimeUnits>
            """));

    /// <summary>
    /// Without the schema (the built-in path), the loader itself rejects forceVariants on a time-unit form, naming it.
    /// </summary>
    [TestMethod]
    public void ScaleForm_ForceVariantsOnATimeUnitForm_IsRejectedByTheLoader()
    {
        string culture = NewName();
        string document = Document(culture, variants: GenderRules, extra: """
            <TimeUnits><Unit name="hour" singular="h" plural="hs"><Forms><Form key="singular" value="h" forceVariants="gender=b" /></Forms></Unit></TimeUnits>
            """);
        var error = Assert.ThrowsExactly<InvalidOperationException>(() =>
            NumberToStringConverter.BuildConfigurationForTesting(document, NumberToStringConverter.ConfigurationSchemaValidation.Skip));
        StringAssert.Contains(error.Message, "TimeUnits[hour].Forms[singular].ForceVariants");
    }

    /// <summary>
    /// The programmatic options carry the forced variants with the XML capabilities: synthesized forms may force, a
    /// scale listed only there is registered, the public view is canonical, and the keys are validated.
    /// </summary>
    [TestMethod]
    public void ScaleForm_ProgrammaticForcedVariants()
    {
        var plain = Build(variants: GenderRules);
        var options = new NumberToStringConverterOptions(plain)
        {
            ScaleFormMultiplierForcedVariants = new Dictionary<int, IReadOnlyDictionary<string, ForcedVariantSet>>
            {
                [2] = new Dictionary<string, ForcedVariantSet> { ["PLURAL"] = ForcedVariantSet.Parse("GENDER=b") },
            },
        };
        var converter = new NumberToStringConverter(options);

        Assert.AreEqual("twoB millions", converter.Convert(2_000_000, "gender=a"));
        Assert.AreEqual("one million", converter.Convert(1_000_000, "gender=a"));
        Assert.IsTrue(converter.ScaleForms.ContainsKey(2), "the scale is registered with its synthesized forms");
        Assert.IsFalse(converter.ScaleFormMultiplierForcedVariants[2]["plural"].IsEmpty, "keys are case-insensitive");

        Assert.ThrowsExactly<ArgumentException>(() => new NumberToStringConverter(new NumberToStringConverterOptions(plain)
        {
            ScaleFormMultiplierForcedVariants = new Dictionary<int, IReadOnlyDictionary<string, ForcedVariantSet>>
            {
                [2] = new Dictionary<string, ForcedVariantSet> { ["dual"] = ForcedVariantSet.Parse("gender=b") },
            },
        }), "a key must name a form of the scale");
        Assert.ThrowsExactly<ArgumentException>(() => new NumberToStringConverter(new NumberToStringConverterOptions(plain)
        {
            ScaleFormMultiplierForcedVariants = new Dictionary<int, IReadOnlyDictionary<string, ForcedVariantSet>> { [2] = null! },
        }));
        Assert.ThrowsExactly<ArgumentException>(() => new NumberToStringConverter(new NumberToStringConverterOptions(plain)
        {
            ScaleFormMultiplierForcedVariants = new Dictionary<int, IReadOnlyDictionary<string, ForcedVariantSet>>
            {
                [2] = new Dictionary<string, ForcedVariantSet> { ["plural"] = null! },
            },
        }));
        Assert.ThrowsExactly<ArgumentException>(() => new NumberToStringConverter(new NumberToStringConverterOptions(plain)
        {
            ScaleFormMultiplierForcedVariants = new Dictionary<int, IReadOnlyDictionary<string, ForcedVariantSet>>
            {
                [7] = new Dictionary<string, ForcedVariantSet> { ["plural"] = ForcedVariantSet.Parse("gender=b") },
            },
        }), "the scale must be nameable");
        var unknown = Assert.ThrowsExactly<NumberToStringConfigurationException>(() => new NumberToStringConverter(new NumberToStringConverterOptions(plain)
        {
            ScaleFormMultiplierForcedVariants = new Dictionary<int, IReadOnlyDictionary<string, ForcedVariantSet>>
            {
                [2] = new Dictionary<string, ForcedVariantSet> { ["plural"] = ForcedVariantSet.Parse("gender=z") },
            },
        }));
        Assert.AreEqual("UNTS006", unknown.ErrorCode);
    }

    /// <summary>XML and programmatic configurations expose the same canonical forced variants, and a clone keeps them.</summary>
    [TestMethod]
    public void ScaleForm_ForcedVariantsSurviveTheCloneAndMatchTheXml()
    {
        var original = BuildForced();
        Assert.AreEqual(1, original.ScaleFormMultiplierForcedVariants.Count);
        Assert.AreEqual(2, original.ScaleFormMultiplierForcedVariants[2].Count);

        var options = new NumberToStringConverterOptions(original);
        Assert.AreSame(original.ScaleFormMultiplierForcedVariants, options.ScaleFormMultiplierForcedVariants);
        var clone = new NumberToStringConverter(options);
        Assert.AreEqual("twoB millions fiveA", clone.Convert(2_000_005, "gender=a"));
        Assert.AreEqual("oneB million", clone.Convert(1_000_000, "gender=a"));
        CollectionAssert.AreEquivalent(
            original.ScaleFormMultiplierForcedVariants[2].Keys.ToArray(),
            clone.ScaleFormMultiplierForcedVariants[2].Keys.ToArray());
    }

    /// <summary>
    /// <c>baseOn</c> replaces a whole exact ScaleForm entry by scale index: the child's forms bring their own forced
    /// variants (here none), and an absent entry is inherited unchanged.
    /// </summary>
    [TestMethod]
    public void ScaleForm_ForcedVariantsFollowTheBaseOnEntry()
    {
        string parent = NewName();
        string inheriting = NewName();
        string replacing = NewName();
        string document = Document(parent, scaleContent: MillionForcesB, variants: GenderRules, extraLanguages: $"""
            <Language baseOn="{parent}"><Culture>{inheriting}</Culture></Language>
            <Language baseOn="{parent}"><Culture>{replacing}</Culture><NumberScale><ScaleForm scale="2"><Forms><Form key="plural" value="millions" /></Forms></ScaleForm></NumberScale></Language>
            """);
        var converters = NumberToStringConverter.ReadConfiguration(document);

        Assert.AreEqual("twoB millions", converters[inheriting].Convert(2_000_000, "gender=a"));
        Assert.AreEqual(1, converters[inheriting].ScaleFormMultiplierForcedVariants.Count);
        Assert.AreEqual("two millions", converters[replacing].Convert(2_000_000, "gender=a"));
        Assert.AreEqual(0, converters[replacing].ScaleFormMultiplierForcedVariants.Count);
    }

    // ===== EU production =============================================================================

    /// <summary>
    /// Basque declares the final-group connector and no longer the group connector (rule 203: one eta, before the
    /// last group); the eta inside a group stays in the buildStrings.
    /// </summary>
    [TestMethod]
    [DataRow("EU")]
    [DataRow("eu-ES")]
    public void Basque_DeclaresTheFinalGroupConnector(string culture)
    {
        var converter = Get(culture);
        Assert.AreEqual("eta", converter.FinalGroupConnector);
        Assert.IsNull(converter.GroupConnector);
        Assert.IsNull(converter.FinalComponentConnector);
    }

    /// <summary>The cardinals of Euskaltzaindia rule 203 §4 (table and the 206 795 843 237 model), exactly.</summary>
    [TestMethod]
    [DataRow(1_001L, "mila eta bat")]
    [DataRow(1_100L, "mila eta ehun")]
    [DataRow(1_200L, "mila eta berrehun")]
    [DataRow(1_201L, "mila berrehun eta bat")]
    [DataRow(1_107L, "mila ehun eta zazpi")]
    [DataRow(1_984L, "mila bederatziehun eta laurogeita lau")]
    [DataRow(2_025L, "bi mila eta hogeita bost")]
    [DataRow(5_040L, "bost mila eta berrogei")]
    [DataRow(13_425L, "hamahiru mila laurehun eta hogeita bost")]
    [DataRow(147_589L, "ehun eta berrogeita zazpi mila bostehun eta laurogeita bederatzi")]
    [DataRow(1_000_000L, "milioi bat")]
    [DataRow(1_200_000L, "milioi bat eta berrehun mila")]
    [DataRow(1_647_413L, "milioi bat seiehun eta berrogeita zazpi mila laurehun eta hamahiru")]
    [DataRow(2_700_000L, "bi milioi eta zazpiehun mila")]
    [DataRow(10_000_000L, "hamar milioi")]
    [DataRow(60_200_001L, "hirurogei milioi berrehun mila eta bat")]
    [DataRow(500_000_000L, "bostehun milioi")]
    [DataRow(500_321_207L, "bostehun milioi hirurehun eta hogeita bat mila berrehun eta zazpi")]
    [DataRow(1_000_000_000L, "mila milioi")]
    [DataRow(206_795_843_000L, "berrehun eta sei mila zazpiehun eta laurogeita hamabost milioi zortziehun eta berrogeita hiru mila")]
    public void Basque_Rule203(long value, string expected)
        => Assert.AreEqual(expected, Get("EU").Convert(value));

    /// <summary>
    /// The rule-203 example 206 795 843 237 ("… mila berrehun eta hogeita zazpi" in the source, a typo for 227) keeps
    /// the source's structure: no eta after milioi, one before nothing since the last group carries its own.
    /// </summary>
    [TestMethod]
    public void Basque_Rule203_ModelNumber()
        => Assert.AreEqual(
            "berrehun eta sei mila zazpiehun eta laurogeita hamabost milioi zortziehun eta berrogeita hiru mila berrehun eta hogeita hamazazpi",
            Get("EU").Convert(206_795_843_237L));

    /// <summary>
    /// Paired million multipliers: the eta inside the multiplier belongs to the multiplier's own phrase, the final eta
    /// of the whole number precedes its last group only.
    /// </summary>
    [TestMethod]
    [DataRow(1_000_000_001L, "mila milioi eta bat")]
    [DataRow(1_200_000_001L, "mila eta berrehun milioi eta bat")]
    [DataRow(2_500_000_000L, "bi mila eta bostehun milioi")]
    [DataRow(2_500_000_001L, "bi mila eta bostehun milioi eta bat")]
    [DataRow(2_500_001_000L, "bi mila eta bostehun milioi eta mila")]
    [DataRow(2_500_001_001L, "bi mila eta bostehun milioi mila eta bat")]
    [DataRow(1_001_100_100L, "mila eta bat milioi ehun mila eta ehun")]
    [DataRow(500_500_000_000L, "bostehun mila eta bostehun milioi")]
    [DataRow(999_999_000_000L, "bederatziehun eta laurogeita hemeretzi mila bederatziehun eta laurogeita hemeretzi milioi")]
    public void Basque_PairedMillions(long value, string expected)
        => Assert.AreEqual(expected, Get("EU").Convert(value));

    /// <summary>Only the junction before the last group carries eta; the earlier junctions are plain.</summary>
    [TestMethod]
    [DataRow(1_000_001L, "milioi bat eta bat")]
    [DataRow(1_000_200L, "milioi bat eta berrehun")]
    [DataRow(2_001_000L, "bi milioi eta mila")]
    [DataRow(500_000_500L, "bostehun milioi eta bostehun")]
    [DataRow(500_200_000L, "bostehun milioi eta berrehun mila")]
    [DataRow(500_500_500L, "bostehun milioi bostehun mila eta bostehun")]
    [DataRow(21_021_021L, "hogeita bat milioi hogeita bat mila eta hogeita bat")]
    [DataRow(1_100_000L, "milioi bat eta ehun mila")]
    [DataRow(1_100_001L, "milioi bat ehun mila eta bat")]
    [DataRow(1_100_100L, "milioi bat ehun mila eta ehun")]
    [DataRow(1_101_000L, "milioi bat ehun eta bat mila")]
    public void Basque_OneEtaBeforeTheLastGroup(long value, string expected)
        => Assert.AreEqual(expected, Get("EU").Convert(value));

    /// <summary>The composite surfaces reuse the corrected cardinal.</summary>
    [TestMethod]
    public void Basque_CompositeSurfaces()
    {
        var converter = Get("EU");
        Assert.AreEqual("ken hirurogei milioi berrehun mila eta bat", converter.Convert(-60_200_001));
        Assert.AreEqual("hirurogei milioi berrehun mila eta bat koma bost", converter.Convert(60_200_001.5m));
        Assert.AreEqual("hirurogei milioi berrehun mila eta bat zati hiru", converter.ConvertFraction(60_200_001, 3));
        Assert.AreEqual("bi mila eta bostehun milioi mila eta bat zati bi", converter.Convert(new Number(2_500_001_001L, 2)));
        var currency = new CurrencyDefinition { UnitSingular = "euro", UnitPlural = "euro", SubunitSingular = "zentimo", SubunitPlural = "zentimo" };
        StringAssert.StartsWith(converter.ConvertCurrency(60_200_001m, currency), "hirurogei milioi berrehun mila eta bat ");
    }

    // ===== ES production =============================================================================

    /// <summary>Spanish scale 2 forces gender=masculino on its multiplier, for both forms; no other language forces.</summary>
    [TestMethod]
    public void Spanish_MillionForcesAMasculineMultiplier()
    {
        var converter = Get("ES");
        var forced = converter.ScaleFormMultiplierForcedVariants;
        CollectionAssert.AreEquivalent(new[] { 2 }, forced.Keys.ToArray());
        CollectionAssert.AreEquivalent(new[] { "singular", "plural" }, forced[2].Keys.ToArray());
        Assert.AreEqual("millón", converter.Convert(1_000_000).Split(' ')[1]);
        Assert.AreEqual(0, converter.GeneratedScaleForms.Count);
    }

    /// <summary>Under a feminine caller the multiplier of millones stays masculine, one-group and paired multipliers alike.</summary>
    [TestMethod]
    [DataRow(200_000_000L, "doscientos millones")]
    [DataRow(500_000_000L, "quinientos millones")]
    [DataRow(900_000_000L, "novecientos millones")]
    [DataRow(2_500_000_000L, "dos mil quinientos millones")]
    [DataRow(21_500_000_000L, "veintiún mil quinientos millones")]
    [DataRow(101_500_000_000L, "ciento un mil quinientos millones")]
    [DataRow(500_500_000_000L, "quinientos mil quinientos millones")]
    [DataRow(999_999_000_000L, "novecientos noventa y nueve mil novecientos noventa y nueve millones")]
    [DataRow(1_200_000_000L, "mil doscientos millones")]
    public void Spanish_FeminineCaller_MasculineMillionMultiplier(long value, string expected)
    {
        var converter = Get("ES");
        Assert.AreEqual(expected, converter.Convert(value, "gender=femenino"));
        Assert.AreEqual(expected, converter.Convert(value, "gender=femenino", "form=attributive"));
        Assert.AreEqual(expected, converter.Convert(value), "the masculine baseline is the same");
    }

    /// <summary>The groups below the million phrase are not governed by millones: they keep the caller's feminine.</summary>
    [TestMethod]
    [DataRow(500_000_500L, "quinientos millones quinientas")]
    [DataRow(500_000_501L, "quinientos millones quinientas una")]
    [DataRow(500_500_500L, "quinientos millones quinientas mil quinientas")]
    [DataRow(200_000_001L, "doscientos millones una")]
    [DataRow(500_200_000L, "quinientos millones doscientas mil")]
    [DataRow(2_500_001_001L, "dos mil quinientos millones mil una")]
    [DataRow(206_795_843_237L, "doscientos seis mil setecientos noventa y cinco millones ochocientas cuarenta y tres mil doscientas treinta y siete")]
    [DataRow(1_200_000L, "un millón doscientas mil")]
    [DataRow(1_647_413L, "un millón seiscientas cuarenta y siete mil cuatrocientas trece")]
    public void Spanish_FeminineCaller_LowerGroupsStayFeminine(long value, string expected)
        => Assert.AreEqual(expected, Get("ES").Convert(value, "gender=femenino"));

    /// <summary>The apocope of uno before millón/millones (NTS-27, NTS-30F) is kept under the forced masculine.</summary>
    [TestMethod]
    [DataRow(1_000_000L, "un millón")]
    [DataRow(21_000_000L, "veintiún millones")]
    [DataRow(31_000_000L, "treinta y un millones")]
    [DataRow(101_000_000L, "ciento un millones")]
    [DataRow(1_001_000_000L, "mil un millones")]
    [DataRow(21_001_000_000L, "veintiún mil un millones")]
    [DataRow(1_000_000_000L, "mil millones")]
    public void Spanish_FeminineCaller_ApocopeKept(long value, string expected)
    {
        var converter = Get("ES");
        Assert.AreEqual(expected, converter.Convert(value, "gender=femenino"));
        Assert.AreEqual(expected, converter.Convert(value, "gender=femenino", "form=attributive"));
        Assert.AreEqual(expected, converter.Convert(value));
    }

    /// <summary>Standalone feminine numbers without a million are unchanged.</summary>
    [TestMethod]
    [DataRow(1L, "una")]
    [DataRow(21L, "veintiuna")]
    [DataRow(200L, "doscientas")]
    [DataRow(1_201L, "mil doscientas una")]
    [DataRow(500_000L, "quinientas mil")]
    [DataRow(999_999L, "novecientas noventa y nueve mil novecientas noventa y nueve")]
    public void Spanish_FeminineCaller_WithoutMillionUnchanged(long value, string expected)
        => Assert.AreEqual(expected, Get("ES").Convert(value, "gender=femenino"));

    /// <summary>The feminine composite surfaces reuse the corrected cardinal.</summary>
    [TestMethod]
    public void Spanish_FeminineCompositeSurfaces()
    {
        var converter = Get("ES");
        Assert.AreEqual("menos quinientos millones", converter.Convert(-500_000_000, "gender=femenino"));
        Assert.AreEqual("menos dos mil quinientos millones mil una", converter.Convert(-2_500_001_001L, "gender=femenino"));
        Assert.AreEqual("quinientos millones coma dos cinco", converter.Convert(500_000_000.25m, "gender=femenino"));
        Assert.AreEqual("quinientos millones sobre siete", converter.ConvertFraction(500_000_000, 7, "gender=femenino"));
        Assert.AreEqual("dos mil quinientos millones mil uno sobre dos", converter.Convert(new Number(2_500_001_001L, 2)));
        var currency = new CurrencyDefinition { UnitSingular = "euro", UnitPlural = "euros", SubunitSingular = "céntimo", SubunitPlural = "céntimos" };
        StringAssert.StartsWith(converter.ConvertCurrency(2_500_000_000m, currency, "gender=femenino"), "dos mil quinientos millones ");
    }

    /// <summary>The Spanish ordinals keep their outputs (the forced multiplier only concerns the cardinal ≥ 10^6).</summary>
    [TestMethod]
    [DataRow(1_200L, "gender=femenino", "milésima ducentésima")]
    [DataRow(1_000_000L, "gender=femenino", "millonésima")]
    [DataRow(2_000_000L, "gender=masculino", "dosmillonésimo")]
    [DataRow(500_000L, "gender=masculino", "quinientosmilésimo")]
    public void Spanish_Ordinals_Unchanged(long value, string variant, string expected)
        => Assert.AreEqual(expected, Get("ES").ConvertOrdinal(value, variant));

    /// <summary>The Spanish clock is unchanged.</summary>
    [TestMethod]
    public void Spanish_Clock_Unchanged()
    {
        var converter = Get("ES");
        Assert.AreEqual("la una y veinticinco", converter.ConvertClockTime(new TimeOnly(1, 25)));
        Assert.AreEqual("las doce y cinco", converter.ConvertClockTime(new TimeOnly(0, 5)));
    }

    // ===== regression ================================================================================

    /// <summary>
    /// Every built-in language other than Spanish, Catalan and Galician (whose million keeps a masculine multiplier,
    /// NTS-30G and NTS-53) and Romanian (whose multipliers agree with mii, milioane and miliarde, NTS-54) declares no
    /// forcing exact scale form, and every language other than Basque no final-group connector: their outputs cannot
    /// depend on either primitive.
    /// </summary>
    [TestMethod]
    public void BuiltInLanguages_OnlySpanishAndBasqueOptIn()
    {
        string[] forcing = ["ES", "CA", "ca-ES", "ca-ES-valencia", "GL", "gl-ES", "RO", "RO-RO"];
        var field = typeof(NumberToStringConverter).GetField("CachedConfigurations", BindingFlags.NonPublic | BindingFlags.Static)!;
        _ = Get("EN");
        var cache = (System.Collections.IDictionary)field.GetValue(null)!;
        int checkedLanguages = 0;
        foreach (System.Collections.DictionaryEntry entry in cache)
        {
            var converter = (NumberToStringConverter)entry.Value!;
            string language = (string)entry.Key;
            bool forces = forcing.Contains(language, StringComparer.OrdinalIgnoreCase);
            bool basque = language.Equals("EU", StringComparison.OrdinalIgnoreCase) || language.Equals("eu-ES", StringComparison.OrdinalIgnoreCase);
            Assert.AreEqual(forces, converter.ScaleFormMultiplierForcedVariants.Count != 0, language);
            Assert.AreEqual(basque, converter.FinalGroupConnector != null, language);
            checkedLanguages++;
        }
        Assert.IsTrue(checkedLanguages > 40);
    }

    /// <summary>Exact-ScaleForm languages without forceVariants keep their outputs (NTS-30B/30C/37 anchors).</summary>
    [TestMethod]
    [DataRow("RU", 2_000_000L, "два миллиона")]
    [DataRow("RU", 5_000_000L, "пять миллионов")]
    [DataRow("UK", 2_000L, "дві тисячі")]
    [DataRow("BG", 2_000L, "две хиляди")]
    [DataRow("CS", 2_000L, "dva tisíce")]
    [DataRow("SK", 2_000L, "dvetisíc")]
    [DataRow("FI", 2_000L, "kaksituhatta")]
    public void ExactScaleFormLanguages_Unchanged(string culture, long value, string expected)
        => Assert.AreEqual(expected, Get(culture).Convert(value));
}
