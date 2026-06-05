using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using Utils.Parser.Diagnostics;
using Utils.Parser.Metadata;

namespace UtilsTest.Parser;

/// <summary>
/// Tests for the parser capability descriptor model.
/// </summary>
[TestClass]
public class ParserFeatureCapabilitiesTests
{
    [TestMethod]
    public void ParserFeatureCapabilities_AllFeaturesHaveUniqueEntries()
    {
        var all = ParserFeatureCapabilities.All;

        Assert.AreEqual(all.Count, all.Select(static capability => capability.Feature).Distinct().Count());
        Assert.AreEqual(Enum.GetValues<ParserFeature>().Length, all.Count);
    }

    [TestMethod]
    public void ParserFeatureCapabilities_KnownFeaturesHaveExpectedLevels()
    {
        Assert.AreEqual(ParserFeatureSupportLevel.RuntimeOptional, ParserFeatureCapabilities.Get(ParserFeature.SemanticPredicates).SupportLevel);
        Assert.AreEqual(ParserFeatureSupportLevel.RuntimeOptional, ParserFeatureCapabilities.Get(ParserFeature.InlineActions).SupportLevel);
        Assert.AreEqual(ParserFeatureSupportLevel.RuntimeOptional, ParserFeatureCapabilities.Get(ParserFeature.RuleActions).SupportLevel);
        Assert.AreEqual(ParserFeatureSupportLevel.MetadataOnly, ParserFeatureCapabilities.Get(ParserFeature.RuleParameters).SupportLevel);
        Assert.AreEqual(ParserFeatureSupportLevel.Unsupported, ParserFeatureCapabilities.Get(ParserFeature.SharedPrefixExecution).SupportLevel);
        Assert.AreEqual(ParserFeatureSupportLevel.Supported, ParserFeatureCapabilities.Get(ParserFeature.AssocRight).SupportLevel);
    }

    [TestMethod]
    public void ParserFeatureCapabilities_RuleActions_AreRuntimeOptional()
    {
        var capability = ParserFeatureCapabilities.Get(ParserFeature.RuleActions);

        Assert.AreEqual(ParserFeatureSupportLevel.RuntimeOptional, capability.SupportLevel);
        StringAssert.Contains(capability.Summary, "generated C# opt-in path");
        StringAssert.Contains(capability.Limitation, "Default parsing does not execute them");
        Assert.IsFalse(capability.Limitation!.Contains("No runtime execution hook", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ParserFeatureCapabilities_RelatedDiagnostics_AreAligned()
    {
        var semanticPredicates = ParserFeatureCapabilities.Get(ParserFeature.SemanticPredicates);
        var inlineActions = ParserFeatureCapabilities.Get(ParserFeature.InlineActions);

        Assert.AreEqual(ParserDiagnostics.SemanticPredicateNotEnforced.Code, semanticPredicates.RelatedDiagnosticCode);
        Assert.AreEqual(ParserDiagnostics.InlineActionStoredNotExecuted.Code, inlineActions.RelatedDiagnosticCode);

        foreach (var capability in ParserFeatureCapabilities.All.Where(static item => !string.IsNullOrWhiteSpace(item.RelatedDiagnosticCode)))
        {
            Assert.IsTrue(ParserDiagnostics.TryGet(capability.RelatedDiagnosticCode!, out _), $"Unknown diagnostic code: {capability.RelatedDiagnosticCode}");
        }
    }

    [TestMethod]
    public void ParserFeatureCapabilities_GetThrowsClearlyForUnknown()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => ParserFeatureCapabilities.Get((ParserFeature)999));
    }
}
