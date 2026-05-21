using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

[TestClass]
/// <summary>
/// Validates grammar-like shared-prefix metadata scenarios across the dry-run pipeline components.
/// </summary>
public class ParserSharedPrefixMetadataScenarioTests
{
    /// <summary>
    /// Verifies shared-prefix metadata for operator alternatives that begin with the same identifier token.
    /// </summary>
    [TestMethod]
    public void Pipeline_SimpleOperatorAlternatives_ProducesStablePlanMetadata()
    {
        var alternatives = new[]
        {
            Alternative(0, Sequence(new RuleRef("ID"), new LiteralMatch("+"), new RuleRef("expr"))),
            Alternative(1, Sequence(new RuleRef("ID"), new LiteralMatch("-"), new RuleRef("expr")))
        };

        var plans = CreatePlansFromAlternatives(alternatives, Token("ID", "x"));

        Assert.AreEqual(1, plans.Count);
        var plan = plans[0];
        Assert.AreEqual("ID", plan.Segment.SharedTokenName);
        CollectionAssert.AreEqual(new[] { "ID" }, plan.Segment.StructuralTokens.ToArray());
        Assert.AreEqual(1, plan.Segment.Boundary.SequencePosition);
        CollectionAssert.AreEqual(new[] { 1, 1 }, plan.Continuations.Select(static c => c.Key.SequencePosition).ToArray());

        var validation = new ParserSharedPrefixPlanValidator().Validate(plan);
        Assert.IsTrue(validation.IsValid);
        Assert.AreEqual(0, validation.Issues.Count);

        var formatted = new ParserSharedPrefixPlanFormatter().FormatPlans(plans);
        Assert.AreEqual(1, formatted.Count);
        Assert.AreEqual("shared segment: ID\nboundary: position 1\neligibility: Eligible\ncontinuations:\n  alt 0 -> position 1\n  alt 1 -> position 1", formatted[0]);
    }

    /// <summary>
    /// Verifies shared-prefix metadata for a call-or-identifier shape without requiring fallback boundaries.
    /// </summary>
    [TestMethod]
    public void Pipeline_CallOrIdentifier_ProducesSharedPrefixMetadataWithoutFallback()
    {
        var detector = new ParserLookaheadSharedPrefixDetector();
        var continuations = new[]
        {
            Continuation(0, 1),
            Continuation(1, 1)
        };
        var probes = new[]
        {
            new ParserLookaheadProbeResult(ParserLookaheadProbeKind.RequiresParse, "ID", "x", ["ID"]),
            new ParserLookaheadProbeResult(ParserLookaheadProbeKind.RequiresParse, "ID", "x", ["ID"])
        };

        var candidates = detector.Detect(probes);
        var plans = new ParserSharedPrefixPlanFactory().CreatePlans(candidates, continuations);

        Assert.AreEqual(1, plans.Count);
        var plan = plans[0];
        Assert.AreEqual("ID", plan.Segment.SharedTokenName);
        CollectionAssert.AreEqual(new[] { "ID" }, plan.Segment.StructuralTokens.ToArray());
        Assert.AreEqual(1, plan.Segment.Boundary.SequencePosition);
        CollectionAssert.AreEqual(new[] { 1, 1 }, plan.Continuations.Select(static c => c.Key.SequencePosition).ToArray());

        var validation = new ParserSharedPrefixPlanValidator().Validate(plan);
        Assert.IsTrue(validation.IsValid);
        Assert.AreEqual(0, validation.Issues.Count);

        var formatted = new ParserSharedPrefixPlanFormatter().FormatPlans(plans);
        Assert.IsFalse(formatted[0].Contains("fallback", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies that alternatives with different first tokens do not produce shared-prefix plans.
    /// </summary>
    [TestMethod]
    public void Pipeline_NoSharedPrefix_ProducesNoPlan()
    {
        var alternatives = new[]
        {
            Alternative(0, new RuleRef("ID")),
            Alternative(1, new RuleRef("NUMBER"))
        };

        var plans = CreatePlansFromAlternatives(alternatives, Token("ID", "x"));

        Assert.AreEqual(0, plans.Count);
    }

    [TestMethod]
    public void Pipeline_SharedIdDotPrefix_ProducesTwoTokenStructuralPrefix()
    {
        var alternatives = new[]
        {
            Alternative(0, Sequence(new RuleRef("ID"), new LiteralMatch("."), new RuleRef("A"))),
            Alternative(1, Sequence(new RuleRef("ID"), new LiteralMatch("."), new RuleRef("B")))
        };

        var plans = CreatePlansFromAlternatives(alternatives, Token("ID", "x"));

        Assert.AreEqual(1, plans.Count);
        CollectionAssert.AreEqual(new[] { "ID", "." }, plans[0].Segment.StructuralTokens.ToArray());
    }

    [TestMethod]
    public void Pipeline_NestedParenthesizedRuleRefs_ProducesSingleTokenStructuralPrefix()
    {
        var alternatives = new[]
        {
            Alternative(0, Sequence(new RuleRef("ID"), new Quantifier(new RuleRef("A"), 1, 1))),
            Alternative(1, Sequence(new RuleRef("ID"), new Quantifier(new RuleRef("B"), 1, 1)))
        };

        var plans = CreatePlansFromAlternatives(alternatives, Token("ID", "x"));

        Assert.AreEqual(1, plans.Count);
        CollectionAssert.AreEqual(new[] { "ID" }, plans[0].Segment.StructuralTokens.ToArray());
    }

    /// <summary>
    /// Verifies normalization of continuation positions when actions or lexer commands prefix the shared token.
    /// </summary>
    [TestMethod]
    public void Pipeline_ActionPrefixedAlternatives_NormalizeContinuationPositionsAfterId()
    {
        var alternatives = new[]
        {
            Alternative(0, Sequence(new EmbeddedAction("Act();", ActionContext.Alternative, ActionPosition.Inline, []), new RuleRef("ID"), new LiteralMatch("+"), new RuleRef("expr"))),
            Alternative(1, Sequence(new LexerCommand(LexerCommandType.Skip, null), new RuleRef("ID"), new LiteralMatch("-"), new RuleRef("expr")))
        };

        var plans = CreatePlansFromAlternatives(alternatives, Token("ID", "x"));

        Assert.AreEqual(1, plans.Count);
        var plan = plans[0];
        Assert.AreEqual("ID", plan.Segment.SharedTokenName);
        Assert.AreEqual(1, plan.Segment.Boundary.SequencePosition);
        CollectionAssert.AreEqual(new[] { 1, 1 }, plan.Continuations.Select(static c => c.Key.SequencePosition).ToArray());

        var validation = new ParserSharedPrefixPlanValidator().Validate(plan);
        Assert.IsTrue(validation.IsValid);

        var formatted = new ParserSharedPrefixPlanFormatter().FormatPlans(plans);
        Assert.AreEqual("shared segment: ID\nboundary: position 1\neligibility: Eligible\ncontinuations:\n  alt 0 -> position 1\n  alt 1 -> position 1", formatted[0]);
    }

    /// <summary>
    /// Verifies fallback divergence handling emits informational validation and fallback formatting markers.
    /// </summary>
    [TestMethod]
    public void ValidatorAndFormatter_FallbackDivergence_EmitsInfoAndFallbackMarker()
    {
        var plan = Plan("ID", 0, [Continuation(0, 1), Continuation(1, 2)]);

        var validation = new ParserSharedPrefixPlanValidator().Validate(plan);

        Assert.IsTrue(validation.IsValid);
        Assert.IsTrue(validation.Issues.Any(static i => i.Severity == ParserSharedPrefixPlanValidationSeverity.Info));
        Assert.IsFalse(validation.Issues.Any(static i => i.Message.Contains("non-fallback", StringComparison.Ordinal)));

        var formatted = new ParserSharedPrefixPlanFormatter().FormatPlans([plan]);
        Assert.AreEqual("shared segment: ID\nboundary: position 0 (fallback)\neligibility: RequiresFallback\nblockers:\n  SP002: Continuation positions diverge.\n  SP001: Fallback boundary prevents safe execution.\ncontinuations:\n  alt 0 -> position 1\n  alt 1 -> position 2", formatted[0]);
    }

    /// <summary>
    /// Verifies non-fallback divergence emits both informational and warning validation issues.
    /// </summary>
    [TestMethod]
    public void Validator_NonFallbackDivergence_EmitsInfoAndWarning()
    {
        var plan = Plan("ID", 1, [Continuation(0, 2), Continuation(1, 3)]);

        var validation = new ParserSharedPrefixPlanValidator().Validate(plan);

        Assert.IsTrue(validation.IsValid);
        Assert.IsTrue(validation.Issues.Any(static i => i.Severity == ParserSharedPrefixPlanValidationSeverity.Info));
        Assert.IsTrue(validation.Issues.Any(static i => i.Severity == ParserSharedPrefixPlanValidationSeverity.Warning));
        Assert.IsTrue(validation.Issues.Any(static i => i.Message.Contains("non-fallback", StringComparison.Ordinal)));
    }

    // ─── AlternativeStructuralPrefixExtractor tests ──────────────────────────

    [TestMethod]
    public void Extractor_SequenceAlternatives_ProducesCorrectDescriptors()
    {
        var extractor = new AlternativeStructuralPrefixExtractor();
        var alternatives = new[]
        {
            Alternative(0, Sequence(new RuleRef("ID"), new LiteralMatch("."), new RuleRef("A"))),
            Alternative(1, Sequence(new RuleRef("ID"), new LiteralMatch("."), new RuleRef("B")))
        };

        var descriptors = extractor.ExtractAll(alternatives);

        Assert.AreEqual(2, descriptors.Count);
        Assert.AreEqual(0, descriptors[0].AlternativeIndex);
        Assert.AreEqual(1, descriptors[1].AlternativeIndex);
        CollectionAssert.AreEqual(new[] { "ID", ".", "A" }, descriptors[0].StructuralTokens.ToArray());
        CollectionAssert.AreEqual(new[] { "ID", ".", "B" }, descriptors[1].StructuralTokens.ToArray());
    }

    [TestMethod]
    public void Extractor_QuantifierStopsExtraction_Conservatively()
    {
        var extractor = new AlternativeStructuralPrefixExtractor();
        var alternatives = new[]
        {
            Alternative(0, Sequence(new RuleRef("ID"), new Quantifier(new RuleRef("X"), 1, 1)))
        };

        var descriptors = extractor.ExtractAll(alternatives);

        CollectionAssert.AreEqual(new[] { "ID" }, descriptors[0].StructuralTokens.ToArray());
    }

    [TestMethod]
    public void Extractor_SingleRuleRef_ProducesSingleTokenDescriptor()
    {
        var extractor = new AlternativeStructuralPrefixExtractor();
        var alternatives = new[]
        {
            Alternative(0, new RuleRef("ID"))
        };

        var descriptors = extractor.ExtractAll(alternatives);

        CollectionAssert.AreEqual(new[] { "ID" }, descriptors[0].StructuralTokens.ToArray());
    }

    [TestMethod]
    public void Extractor_StructuralTokens_IsReadOnly_CannotBeMutated()
    {
        var extractor = new AlternativeStructuralPrefixExtractor();
        var alternatives = new[]
        {
            Alternative(0, Sequence(new RuleRef("ID"), new LiteralMatch("+")))
        };

        var tokens = extractor.ExtractAll(alternatives)[0].StructuralTokens;

        Assert.IsTrue(((ICollection<string>)tokens).IsReadOnly,
            "StructuralTokens must be wrapped in a read-only collection");
    }

    [TestMethod]
    public void Factory_WithNoDescriptors_FallsBackToSharedTokenName()
    {
        var candidates = new[]
        {
            new ParserLookaheadSharedPrefixCandidate("ID", [0, 1])
        };
        var continuations = new[]
        {
            Continuation(0, 1),
            Continuation(1, 1)
        };

        var plans = new ParserSharedPrefixPlanFactory().CreatePlans(candidates, continuations);

        Assert.AreEqual(1, plans.Count);
        CollectionAssert.AreEqual(new[] { "ID" }, plans[0].Segment.StructuralTokens.ToArray());
    }

    // ─── Regression: scheduler integration ───────────────────────────────────

    [TestMethod]
    public void Regression_SharedIdDotPrefix_StillProducesTwoTokenPrefix()
    {
        var alternatives = new[]
        {
            Alternative(0, Sequence(new RuleRef("ID"), new LiteralMatch("."), new RuleRef("A"))),
            Alternative(1, Sequence(new RuleRef("ID"), new LiteralMatch("."), new RuleRef("B")))
        };

        var plans = CreatePlansFromAlternatives(alternatives, Token("ID", "x"));

        Assert.AreEqual(1, plans.Count);
        CollectionAssert.AreEqual(new[] { "ID", "." }, plans[0].Segment.StructuralTokens.ToArray());
    }

    [TestMethod]
    public void Regression_QuantifierAlternatives_StillProducesSingleTokenPrefix()
    {
        var alternatives = new[]
        {
            Alternative(0, Sequence(new RuleRef("ID"), new Quantifier(new RuleRef("A"), 1, 1))),
            Alternative(1, Sequence(new RuleRef("ID"), new Quantifier(new RuleRef("B"), 1, 1)))
        };

        var plans = CreatePlansFromAlternatives(alternatives, Token("ID", "x"));

        Assert.AreEqual(1, plans.Count);
        CollectionAssert.AreEqual(new[] { "ID" }, plans[0].Segment.StructuralTokens.ToArray());
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds shared-prefix plans by probing alternatives and flowing metadata through detector, continuation, and plan factories.
    /// </summary>
    private static IReadOnlyList<ParserSharedPrefixPlan> CreatePlansFromAlternatives(IReadOnlyList<Alternative> alternatives, Token token)
    {
        var lookaheadProbe = new ParserLookaheadProbe();
        var detector = new ParserLookaheadSharedPrefixDetector();
        var continuationFactory = new ParserContinuationFactory();
        var planFactory = new ParserSharedPrefixPlanFactory();
        var rule = new Rule("expr", 0, false, new Alternation(alternatives));

        var probes = alternatives
            .Select(alternative => lookaheadProbe.Probe(alternative, token, ResolveRule, false))
            .ToArray();
        var candidates = detector.Detect(probes);
        var continuations = candidates
            .SelectMany(candidate => candidate.AlternativeIndexes.Select(alternativeIndex =>
            {
                var alternative = alternatives[alternativeIndex];
                var rawPosition = continuationFactory.ComputeSharedPrefixSequencePosition(alternative, candidate.TokenName);
                return continuationFactory.Create(rule, alternative, alternativeIndex, rawPosition, [candidate.TokenName], true);
            }))
            .ToArray();

        var descriptors = new AlternativeStructuralPrefixExtractor().ExtractAll(alternatives);
        return planFactory.CreatePlans(candidates, continuations, descriptors);
    }

    /// <summary>
    /// Resolves token-like names as lexer rules and all other names as parser rules for probe metadata tests.
    /// </summary>
    private static Rule? ResolveRule(string name)
    {
        if (name is "ID" or "NUMBER")
        {
            return new Rule(name, 0, false, new Alternation([])) { Kind = RuleKind.Lexer };
        }

        return new Rule(name, 0, false, new Alternation([])) { Kind = RuleKind.Parser };
    }

    /// <summary>
    /// Creates a shared-prefix plan with explicit boundary metadata for validator and formatter scenarios.
    /// </summary>
    private static ParserSharedPrefixPlan Plan(string tokenName, int boundaryPosition, IReadOnlyList<ParserContinuationDescriptor> continuations)
    {
        var alternativeIndexes = continuations.Select(static continuation => continuation.Key.AlternativeIndex).ToArray();
        return new ParserSharedPrefixPlan(
            tokenName,
            alternativeIndexes,
            continuations,
            new ParserSharedPrefixSegment(tokenName, [tokenName], new ParserSharedPrefixBoundary(boundaryPosition, null)));
    }

    /// <summary>
    /// Creates a continuation descriptor with deterministic expected-token metadata for shared-prefix tests.
    /// </summary>
    private static ParserContinuationDescriptor Continuation(int alternativeIndex, int sequencePosition)
    {
        return new ParserContinuationDescriptor(
            new ParserContinuationKey("expr", alternativeIndex, sequencePosition),
            sequencePosition,
            ParserContinuationCategory.SharedPrefixCandidate,
            ["ID"],
            true);
    }

    /// <summary>
    /// Creates a parser alternative with left associativity for compact scenario setup.
    /// </summary>
    private static Alternative Alternative(int index, RuleContent content)
    {
        return new Alternative(index, Associativity.Left, content, null);
    }

    /// <summary>
    /// Creates a sequence content node from ordered rule-content items.
    /// </summary>
    private static Sequence Sequence(params RuleContent[] items)
    {
        return new Sequence(items);
    }

    /// <summary>
    /// Creates a token instance used as lookahead input for probe-driven metadata scenarios.
    /// </summary>
    private static Token Token(string ruleName, string text)
    {
        return new Token(new SourceSpan(0, text.Length), ruleName, "DEFAULT_MODE", "DEFAULT_CHANNEL", text);
    }
}
