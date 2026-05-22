using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Diagnostics;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

[TestClass]
public class ArchitectureInvariantTests
{
    [TestMethod]
    public void Scheduler_DoesNotProduceContinuationMetadata()
    {
        var scheduler = new AlternativeScheduler();
        var rule = CreateRule();
        var alternatives = rule.Content.Alternatives;
        var probes = CreateProbes(alternatives.Count);
        var prepared = new ContinuationMetadataPreparation().Prepare(rule, alternatives, probes, []);

        _ = scheduler.Run(
            rule,
            alternatives,
            0,
            0,
            null,
            (alternative, index) => new ScheduledAlternativeExecutionResult(CreateCompletedState(rule, alternative, index), probes[index]),
            precomputedDescriptors: null,
            precomputedContinuationMetadata: prepared,
            precomputedLookaheadProbes: probes,
            precomputedSharedPrefixCandidates: []);

        var preparedSnapshot = prepared.Select(static descriptor => descriptor with { }).ToArray();
        CollectionAssert.AreEqual(preparedSnapshot, prepared.ToArray());
    }

    [TestMethod]
    public void Scheduler_DoesNotMutatePreparedMetadata()
    {
        var scheduler = new AlternativeScheduler();
        var rule = CreateRule();
        var alternatives = rule.Content.Alternatives;
        var probes = CreateProbes(alternatives.Count);
        var prepared = new ContinuationMetadataPreparation().Prepare(rule, alternatives, probes, []);
        var before = prepared.ToArray();

        _ = scheduler.Run(
            rule,
            alternatives,
            0,
            0,
            null,
            (alternative, index) => new ScheduledAlternativeExecutionResult(CreateCompletedState(rule, alternative, index), probes[index]),
            precomputedDescriptors: null,
            precomputedContinuationMetadata: prepared,
            precomputedLookaheadProbes: probes,
            precomputedSharedPrefixCandidates: []);

        CollectionAssert.AreEqual(before, prepared.ToArray());
    }

    [TestMethod]
    public void Scheduler_DoesNotOwnPreparation()
    {
        var scheduler = new AlternativeScheduler();
        var rule = CreateRule();
        var alternatives = rule.Content.Alternatives;
        var probes = CreateProbes(alternatives.Count);

        _ = Assert.ThrowsException<ArgumentException>(() => scheduler.Run(
            rule,
            alternatives,
            0,
            0,
            null,
            (alternative, index) => new ScheduledAlternativeExecutionResult(CreateCompletedState(rule, alternative, index), probes[index]),
            precomputedDescriptors: null,
            precomputedContinuationMetadata: [],
            precomputedLookaheadProbes: probes,
            precomputedSharedPrefixCandidates: []));
    }

    [TestMethod]
    public void Preparation_IsDeterministic()
    {
        var rule = new Rule("expr", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new Sequence([new RuleRef("ID"), new LiteralMatch("+"), new RuleRef("expr")])),
            new Alternative(1, Associativity.Left, new Sequence([new RuleRef("ID"), new LiteralMatch("-"), new RuleRef("expr")]))
        ]));
        var alternatives = rule.Content.Alternatives;
        var probes = CreateProbes(alternatives.Count);
        var candidates = new ParserLookaheadSharedPrefixDetector().Detect(probes);
        var preparation = new ContinuationMetadataPreparation();

        var first = preparation.Prepare(rule, alternatives, probes, candidates).ToArray();
        var second = preparation.Prepare(rule, alternatives, probes, candidates).ToArray();
        var third = preparation.Prepare(rule, alternatives, probes, candidates).ToArray();

        AssertContinuationSetsEqual(first, second);
        AssertContinuationSetsEqual(second, third);
    }

    [TestMethod]
    public void ContinuationMetadata_DoesNotChangeParseResult()
    {
        var grammar = ExpGrammar.Build();
        var compiled = new CompiledGrammar(grammar);

        var withMetadata = compiled.Parse("1+2");
        var withoutMetadata = compiled.Parse("1+2");

        Assert.AreEqual(withMetadata.ToString(), withoutMetadata.ToString());
        Assert.AreEqual(withMetadata is ErrorNode, withoutMetadata is ErrorNode);
    }

    [TestMethod]
    public void Observation_DoesNotChangeResult()
    {
        var definition = ExpGrammar.Build();
        var tokens = new[]
        {
            new Token(new SourceSpan(0, 1), "INT", "DEFAULT_MODE", "DEFAULT_CHANNEL", "1")
        };

        var defaultParser = new ParserEngine(definition);
        var observedParser = new ParserEngine(definition, ParserRuntimeFeaturePolicy.Default with { RuntimeObserver = new NoopObserver() });

        var defaultResult = defaultParser.Parse(tokens);
        var observedResult = observedParser.Parse(tokens);

        Assert.AreEqual(defaultResult.ToString(), observedResult.ToString());
        Assert.AreEqual(defaultResult is ErrorNode, observedResult is ErrorNode);
    }

    private static Rule CreateRule()
    {
        return new Rule("expr", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new RuleRef("ID")),
            new Alternative(1, Associativity.Left, new RuleRef("NUMBER"))
        ]));
    }

    private static ParserLookaheadProbeResult[] CreateProbes(int count)
    {
        return Enumerable.Range(0, count)
            .Select(static _ => new ParserLookaheadProbeResult(ParserLookaheadProbeKind.RequiresParse, "ID", "id", ["ID"]))
            .ToArray();
    }

    private static ActiveParseState CreateCompletedState(Rule rule, Alternative alternative, int index)
    {
        return new ActiveParseState
        {
            Rule = rule,
            Alternative = alternative,
            OriginInputPosition = 0,
            CurrentInputPosition = 1,
            AlternativeIndex = index,
            Cursor = new RuleContentCursor { Index = 0, Kind = ScheduledAlternativeCursorKinds.AlternativeRoot },
            PartialNode = new ParserNode(new SourceSpan(0, 1), "DEFAULT_MODE", rule, []),
            EndPosition = 1,
            Status = ActiveParseStateStatus.Completed,
            ParentStateKey = null,
            Depth = 0,
            Continuation = null
        };
    }


    private static void AssertContinuationSetsEqual(IReadOnlyList<ParserContinuationDescriptor> expected, IReadOnlyList<ParserContinuationDescriptor> actual)
    {
        Assert.AreEqual(expected.Count, actual.Count);
        for (var index = 0; index < expected.Count; index++)
        {
            Assert.AreEqual(expected[index].Key, actual[index].Key);
            Assert.AreEqual(expected[index].Category, actual[index].Category);
            CollectionAssert.AreEqual(expected[index].ExpectedTokenNames.ToArray(), actual[index].ExpectedTokenNames.ToArray());
            Assert.AreEqual(expected[index].IsSharedPrefixCandidate, actual[index].IsSharedPrefixCandidate);
        }
    }

    private sealed class NoopObserver : IParserRuntimeObserver
    {
        public void OnAlternativeStarted(AlternativeRuntimeObservation observation) { }
        public void OnAlternativeCompleted(AlternativeRuntimeObservation observation) { }
        public void OnAlternativeFailed(AlternativeRuntimeObservation observation) { }
        public void OnAlternativePruned(AlternativeRuntimeObservation observation) { }
        public void OnAlternativeSelected(AlternativeRuntimeObservation observation) { }
    }
}
