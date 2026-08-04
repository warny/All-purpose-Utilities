using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers;

/// <summary>Verifies deterministic precedence and construction-time ambiguity diagnostics.</summary>
[TestClass]
public sealed class NumberToStringRulePrecedenceTests
{
    /// <summary>Verifies that ordinal priority, rather than source order, resolves equal specificity.</summary>
    [TestMethod]
    public void OrdinalPriorityWinsRegardlessOfDeclarationOrder()
    {
        foreach (bool reverse in new[] { false, true })
        {
            var low = Ordinal("female", "low", 0);
            var high = Ordinal("female", "high", 100);
            NumberToStringConverter converter = Create(ordinalVariants: reverse ? [high, low] : [low, high]);
            Assert.AreEqual("high", converter.ConvertOrdinal(1, "gender=female"));
        }
    }

    /// <summary>Verifies that compatible equal-ranked ordinal rules are rejected.</summary>
    [TestMethod]
    public void EqualRankOrdinalIntersectionIsRejected()
    {
        var exception = Assert.ThrowsException<NumberToStringConfigurationException>(() => Create(ordinalVariants:
        [
            Ordinal("female", "a", 0),
            new NumberToStringConverter.OrdinalVariantRule(
                new Dictionary<string, string> { ["number"] = "plural" },
                new Dictionary<long, string> { [1] = "b" }, new Dictionary<string, string>(), null, null)
        ]));
        Assert.AreEqual("UNTS002", exception.ErrorCode);
        StringAssert.Contains(exception.Message, "specificity 1 and priority 0");
    }

    /// <summary>Verifies that a higher-priority cumulative rule is applied later.</summary>
    [TestMethod]
    public void CumulativePriorityIsAppliedLaterRegardlessOfDeclarationOrder()
    {
        var first = Variant("female", "one", "first", 0);
        var later = Variant("female", "first", "priority-first", 100);
        foreach (bool reverse in new[] { false, true })
        {
            NumberToStringConverter converter = Create(variantRules: reverse ? [later, first] : [first, later]);
            Assert.AreEqual("priority-first", converter.Convert(1, "gender=female"));
        }
    }

    /// <summary>Verifies trigger-form priority selection and the unconditional fallback.</summary>
    [TestMethod]
    public void TriggerPriorityWinsAndDefaultRemainsFallback()
    {
        var low = new NumberToStringConverter.TriggerReplacementForm(
            new Dictionary<string, string> { ["gender"] = "female" }, "low", -1);
        var high = new NumberToStringConverter.TriggerReplacementForm(
            new Dictionary<string, string> { ["gender"] = "female" }, "high", int.MaxValue);
        NumberToStringConverter converter = Create(triggers:
        [
            new NumberToStringConverter.TriggerRule(NumberToStringConverter.TriggerAt.End, null,
            [
                new NumberToStringConverter.TriggerReplace("one", false, [high, low], "fallback")
            ])
        ]);
        Assert.AreEqual("high", converter.Convert(1, "gender=female"));
        Assert.AreEqual("fallback", converter.Convert(1, "gender=male"));
    }

    /// <summary>Verifies that canonical and local dimension names have identical runtime semantics.</summary>
    [TestMethod]
    public void CumulativeRuleAndQueryAliasesAreCanonicalizedAtRuntime()
    {
        foreach (string ruleName in new[] { "gender", "GeNrE" })
        foreach (string queryName in new[] { "gender", "GENRE" })
        {
            var rule = new NumberToStringConverter.VariantRule(
                new Dictionary<string, string> { [ruleName] = "FeMaLe" },
                [new NumberToStringConverter.ReplacementRule("one", "canonical-match", ReplacementScope.Anywhere)]);
            Assert.AreEqual("canonical-match", Create(variantRules: [rule]).Convert(1, $"{queryName}=female"));
        }
    }

    /// <summary>Verifies that XML-style conditional forms never synthesize an order-based fallback.</summary>
    [TestMethod]
    public void TriggerWithoutDefaultSkipsUnmatchedQueryRegardlessOfFormOrder()
    {
        var female = new NumberToStringConverter.TriggerReplacementForm(
            new Dictionary<string, string> { ["gender"] = "female" }, "female", 100);
        var plural = new NumberToStringConverter.TriggerReplacementForm(
            new Dictionary<string, string> { ["number"] = "plural" }, "plural", -100);
        foreach (IReadOnlyList<NumberToStringConverter.TriggerReplacementForm> forms in
            new[] { new[] { female, plural }, new[] { plural, female } })
        {
            NumberToStringConverter converter = Create(triggers:
            [
                new NumberToStringConverter.TriggerRule(NumberToStringConverter.TriggerAt.End, null,
                [new NumberToStringConverter.TriggerReplace("one", false, forms, null)])
            ]);
            Assert.AreEqual("one", converter.Convert(1, "gender=male", "number=singular"));
        }
    }

    /// <summary>Verifies that XML parsing has the same explicit-fallback contract as programmatic construction.</summary>
    [TestMethod]
    public void XmlTriggerWithoutToDoesNotUseFirstFormAsFallback()
    {
        const string culture = "precedence-xml-trigger";
        foreach (bool reverse in new[] { false, true })
        {
            string forms = reverse
                ? "<Variant type=\"number\" variant=\"plural\" value=\"plural\" priority=\"-10\" /><Variant type=\"gender\" variant=\"female\" value=\"female\" priority=\"10\" />"
                : "<Variant type=\"gender\" variant=\"female\" value=\"female\" priority=\"10\" /><Variant type=\"number\" variant=\"plural\" value=\"plural\" priority=\"-10\" />";
            NumberToStringConverter.RegisterConfigurations([CreateTriggerXml(culture, forms)], DuplicateCulturePolicy.Replace);
            NumberToStringConverter converter = NumberToStringConverter.GetConverter(culture);
            Assert.AreEqual("one", converter.Convert(1, "gender=male", "number=singular"));
            Assert.AreEqual("female", converter.Convert(1, "gender=female", "number=singular"));
        }
    }

    /// <summary>Verifies the cumulative-rule ambiguity diagnostic.</summary>
    [TestMethod]
    public void EqualRankCumulativeIntersectionReportsUnts001()
    {
        var exception = Assert.ThrowsException<NumberToStringConfigurationException>(() => Create(variantRules:
        [
            Variant("female", "one", "a", 0),
            new NumberToStringConverter.VariantRule(
                new Dictionary<string, string> { ["number"] = "plural" },
                [new NumberToStringConverter.ReplacementRule("one", "b", ReplacementScope.Anywhere)])
        ]));
        Assert.AreEqual("UNTS001", exception.ErrorCode);
    }

    /// <summary>Verifies the trigger-form ambiguity diagnostic.</summary>
    [TestMethod]
    public void EqualRankTriggerIntersectionReportsUnts003()
    {
        var forms = new[]
        {
            new NumberToStringConverter.TriggerReplacementForm(new Dictionary<string, string> { ["gender"] = "female" }, "a"),
            new NumberToStringConverter.TriggerReplacementForm(new Dictionary<string, string> { ["number"] = "plural" }, "b")
        };
        var exception = Assert.ThrowsException<NumberToStringConfigurationException>(() => Create(triggers:
        [
            new NumberToStringConverter.TriggerRule(NumberToStringConverter.TriggerAt.End, null,
            [new NumberToStringConverter.TriggerReplace("one", false, forms, null)])
        ]));
        Assert.AreEqual("UNTS003", exception.ErrorCode);
    }

    /// <summary>Verifies that canonical and alias keys cannot duplicate one logical dimension.</summary>
    [TestMethod]
    public void CanonicalAndAliasConstraintDuplicateReportsUnts004()
    {
        var rule = new NumberToStringConverter.VariantRule(
            new Dictionary<string, string> { ["gender"] = "female", ["genre"] = "female" },
            [new NumberToStringConverter.ReplacementRule("one", "a", ReplacementScope.Anywhere)]);
        var exception = Assert.ThrowsException<NumberToStringConfigurationException>(() => Create(variantRules: [rule]));
        Assert.AreEqual("UNTS004", exception.ErrorCode);
    }

    /// <summary>Verifies that disjoint global value filters are not reported as concurrent.</summary>
    [TestMethod]
    public void EqualRankGlobalRulesWithDisjointOnValueRangesAreAccepted()
    {
        var constraints = new Dictionary<string, string> { ["gender"] = "female" };
        var one = new NumberToStringConverter.VariantRule(constraints,
        [
            new NumberToStringConverter.ReplacementRule("one", "first", ReplacementScope.Anywhere,
                null, NumberToStringConverter.ParseRangeExpression("1"))
        ]);
        var two = new NumberToStringConverter.VariantRule(constraints,
        [
            new NumberToStringConverter.ReplacementRule("two", "second", ReplacementScope.Anywhere,
                null, NumberToStringConverter.ParseRangeExpression("2"))
        ]);
        NumberToStringConverter converter = Create(variantRules: [one, two]);
        Assert.AreEqual("first", converter.Convert(1, "gender=female"));
        Assert.AreEqual("second", converter.Convert(2, "gender=female"));
    }

    /// <summary>Verifies that XML replacement forms retain priority during synthetic aggregation.</summary>
    [TestMethod]
    public void XmlReplacementFormsPreservePriority()
    {
        const string culture = "precedence-xml-replacement-priority";
        const string configuration = """
            <Replacements>
                <Replacement oldValue="one" scope="Anywhere">
                    <Variant type="gender" variant="female" value="first" priority="10" />
                </Replacement>
                <Replacement oldValue="first" scope="Anywhere">
                    <Variant type="number" variant="plural" value="final" priority="20" />
                </Replacement>
            </Replacements>
            """;
        NumberToStringConverter.RegisterConfigurations(
            [CreatePriorityXml(culture, configuration)], DuplicateCulturePolicy.Replace);
        NumberToStringConverter converter = NumberToStringConverter.GetConverter(culture);
        Assert.AreEqual("final", converter.Convert(1, "gender=female", "number=plural"));
        CollectionAssert.AreEquivalent(new[] { 10, 20 }, converter.VariantRules.Select(rule => rule.Priority).ToArray());
    }

    /// <summary>Verifies that XML ordinal forms retain priority through aggregation and structural merging.</summary>
    [TestMethod]
    public void XmlOrdinalFormsPreservePriorityAndStructuralMergeRank()
    {
        const string culture = "precedence-xml-ordinal-priority";
        const string configuration = """
            <Ordinals suffix="th">
                <OrdinalException value="1" string="first">
                    <Variant type="gender" variant="female" value="low" priority="10" />
                </OrdinalException>
                <Ordinal from="two" to="second">
                    <Variant type="number" variant="plural" value="high" priority="20" />
                </Ordinal>
                <OrdinalVariants>
                    <Variant type="gender" variant="female" suffix="th" priority="10" />
                </OrdinalVariants>
            </Ordinals>
            """;
        NumberToStringConverter.RegisterConfigurations(
            [CreatePriorityXml(culture, configuration)], DuplicateCulturePolicy.Replace);
        NumberToStringConverter converter = NumberToStringConverter.GetConverter(culture);
        Assert.AreEqual("high", converter.ConvertOrdinal(2, "gender=female", "number=plural"));
        CollectionAssert.AreEquivalent(new[] { 10, 20 }, converter.OrdinalVariants.Select(rule => rule.Priority).ToArray());
    }

    /// <summary>Creates an isolated English-derived converter for precedence tests.</summary>
    private static NumberToStringConverter Create(
        IReadOnlyList<NumberToStringConverter.OrdinalVariantRule>? ordinalVariants = null,
        IReadOnlyList<NumberToStringConverter.VariantRule>? variantRules = null,
        IReadOnlyList<NumberToStringConverter.TriggerRule>? triggers = null)
    {
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            LanguageIdentifier = "precedence-test",
            VariantDimensions =
            [
                new NumberToStringConverter.VariantDimension("gender", ["male", "female"], "genre"),
                new NumberToStringConverter.VariantDimension("number", ["singular", "plural"])
            ],
            OrdinalVariants = ordinalVariants ?? [],
            VariantRules = variantRules ?? [],
            Triggers = triggers ?? []
        };
        return new NumberToStringConverter(options);
    }

    /// <summary>Creates an ordinal rule constrained by gender.</summary>
    private static NumberToStringConverter.OrdinalVariantRule Ordinal(string gender, string text, int priority) =>
        new(new Dictionary<string, string> { ["gender"] = gender },
            new Dictionary<long, string> { [1] = text }, new Dictionary<string, string>(), null, null, priority);

    /// <summary>Creates a cumulative global variant rule constrained by gender.</summary>
    private static NumberToStringConverter.VariantRule Variant(string gender, string from, string to, int priority) =>
        new(new Dictionary<string, string> { ["gender"] = gender },
            [new NumberToStringConverter.ReplacementRule(from, to, ReplacementScope.Anywhere)], priority);

    /// <summary>Creates a deterministic single-digit XML configuration containing conditional trigger forms.</summary>
    private static string CreateTriggerXml(string culture, string forms) => $$"""
        <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
            <Language groupSize="3" separator=" " groupSeparator="" zero="zero" minus="minus *" decimalSeparator="point" maxNumber="9">
                <Culture>{{culture}}</Culture>
                <Groups><Group level="1">
                    <Digit digit="0" string="" /><Digit digit="1" string="one" /><Digit digit="2" string="two" />
                    <Digit digit="3" string="three" /><Digit digit="4" string="four" /><Digit digit="5" string="five" />
                    <Digit digit="6" string="six" /><Digit digit="7" string="seven" /><Digit digit="8" string="eight" />
                    <Digit digit="9" string="nine" />
                </Group></Groups>
                <Variants>
                    <Dimension name="gender" values="male,female" />
                    <Dimension name="number" values="singular,plural" />
                </Variants>
                <Trigger executeAt="end"><Replace from="one">{{forms}}</Replace></Trigger>
            </Language>
        </Numbers>
        """;

    /// <summary>Creates XML configuration for priority-preservation loader tests.</summary>
    private static string CreatePriorityXml(string culture, string configuration) => $$"""
        <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
            <Language groupSize="3" separator=" " groupSeparator="" zero="zero" minus="minus *" decimalSeparator="point" maxNumber="9">
                <Culture>{{culture}}</Culture>
                <Groups><Group level="1">
                    <Digit digit="0" string="" /><Digit digit="1" string="one" /><Digit digit="2" string="two" />
                    <Digit digit="3" string="three" /><Digit digit="4" string="four" /><Digit digit="5" string="five" />
                    <Digit digit="6" string="six" /><Digit digit="7" string="seven" /><Digit digit="8" string="eight" />
                    <Digit digit="9" string="nine" />
                </Group></Groups>
                {{configuration}}
                <Variants>
                    <Dimension name="gender" values="male,female" />
                    <Dimension name="number" values="singular,plural" />
                </Variants>
            </Language>
        </Numbers>
        """;
}
