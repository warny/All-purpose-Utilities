using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

[TestClass]
public class ParserSharedPrefixMetadataScenarioTests
{
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
        Assert.AreEqual(1, plan.Segment.Boundary.SequencePosition);
        CollectionAssert.AreEqual(new[] { 1, 1 }, plan.Continuations.Select(static c => c.Key.SequencePosition).ToArray());

        var validation = new ParserSharedPrefixPlanValidator().Validate(plan);
        Assert.IsTrue(validation.IsValid);
        Assert.AreEqual(0, validation.Issues.Count);

        var formatted = new ParserSharedPrefixPlanFormatter().FormatPlans(plans);
        Assert.AreEqual(1, formatted.Count);
        Assert.AreEqual("shared segment: ID\nboundary: position 1\ncontinuations:\n  alt 0 -> position 1\n  alt 1 -> position 1", formatted[0]);
    }

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
        Assert.AreEqual(1, plan.Segment.Boundary.SequencePosition);
        CollectionAssert.AreEqual(new[] { 1, 1 }, plan.Continuations.Select(static c => c.Key.SequencePosition).ToArray());

        var validation = new ParserSharedPrefixPlanValidator().Validate(plan);
        Assert.IsTrue(validation.IsValid);
        Assert.AreEqual(0, validation.Issues.Count);

        var formatted = new ParserSharedPrefixPlanFormatter().FormatPlans(plans);
        Assert.IsFalse(formatted[0].Contains("fallback", StringComparison.Ordinal));
    }

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
        Assert.AreEqual("shared segment: ID\nboundary: position 1\ncontinuations:\n  alt 0 -> position 1\n  alt 1 -> position 1", formatted[0]);
    }

    [TestMethod]
    public void ValidatorAndFormatter_FallbackDivergence_EmitsInfoAndFallbackMarker()
    {
        var plan = Plan("ID", 0, [Continuation(0, 1), Continuation(1, 2)]);

        var validation = new ParserSharedPrefixPlanValidator().Validate(plan);

        Assert.IsTrue(validation.IsValid);
        Assert.IsTrue(validation.Issues.Any(static i => i.Severity == ParserSharedPrefixPlanValidationSeverity.Info));
        Assert.IsFalse(validation.Issues.Any(static i => i.Message.Contains("non-fallback", StringComparison.Ordinal)));

        var formatted = new ParserSharedPrefixPlanFormatter().FormatPlans([plan]);
        Assert.AreEqual("shared segment: ID\nboundary: position 0 (fallback)\ncontinuations:\n  alt 0 -> position 1\n  alt 1 -> position 2", formatted[0]);
    }

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

        return planFactory.CreatePlans(candidates, continuations);
    }

    private static Rule? ResolveRule(string name)
    {
        if (name is "ID" or "NUMBER")
        {
            return new Rule(name, 0, false, new Alternation([])) { Kind = RuleKind.Lexer };
        }

        return new Rule(name, 0, false, new Alternation([])) { Kind = RuleKind.Parser };
    }

    private static ParserSharedPrefixPlan Plan(string tokenName, int boundaryPosition, IReadOnlyList<ParserContinuationDescriptor> continuations)
    {
        var alternativeIndexes = continuations.Select(static continuation => continuation.Key.AlternativeIndex).ToArray();
        return new ParserSharedPrefixPlan(
            tokenName,
            alternativeIndexes,
            continuations,
            new ParserSharedPrefixSegment(tokenName, new ParserSharedPrefixBoundary(boundaryPosition, null)));
    }

    private static ParserContinuationDescriptor Continuation(int alternativeIndex, int sequencePosition)
    {
        return new ParserContinuationDescriptor(
            new ParserContinuationKey("expr", alternativeIndex, sequencePosition),
            ["ID"],
            true);
    }

    private static Alternative Alternative(int index, RuleContent content)
    {
        return new Alternative(index, Associativity.Left, content, null);
    }

    private static Sequence Sequence(params RuleContent[] items)
    {
        return new Sequence(items);
    }

    private static Token Token(string ruleName, string text)
    {
        return new Token(new SourceSpan(0, text.Length), ruleName, "DEFAULT_MODE", "DEFAULT_CHANNEL", text);
    }
}
