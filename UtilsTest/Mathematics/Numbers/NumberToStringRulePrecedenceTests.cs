using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
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
}
