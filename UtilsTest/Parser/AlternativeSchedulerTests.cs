using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Diagnostics;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

[TestClass]
public class AlternativeSchedulerTests
{
    [TestMethod]
    public void Run_DeduplicatesExactStateIdentity()
    {
        var scheduler = new AlternativeScheduler();
        var (context, rule, alternatives) = CreateAlternatives();

        var result = scheduler.Run(
            rule,
            alternatives,
            originInputPosition: context.Position,
            minimumPrecedence: 3,
            diagnostics: null,
            precomputedDescriptors: null,
            precomputedContinuationMetadata: DefaultContinuations(rule, alternatives.Count),
            precomputedLookaheadProbes: UnknownProbes(alternatives.Count),
            parseAlternative: (_, index) => index == 0 || index == 1
                ? new ScheduledAlternativeExecutionResult(CreateState(rule, alternatives[index], context.Position, 2, index), new ParserLookaheadProbeResult(ParserLookaheadProbeKind.RequiresParse, "ID", "id", ["ID"]))
                : new ScheduledAlternativeExecutionResult(null, new ParserLookaheadProbeResult(ParserLookaheadProbeKind.Unknown, null, null)));

        Assert.AreEqual(1, result.CompletedStates.Count);
    }

    [TestMethod]
    public void Run_PrunesByBranchEquivalenceAndKeepsDistinctLabels()
    {
        var scheduler = new AlternativeScheduler();
        var rule = new Rule("r", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new LiteralMatch("a"), "X"),
            new Alternative(1, Associativity.Left, new LiteralMatch("a"), "X"),
            new Alternative(2, Associativity.Left, new LiteralMatch("a"), "Y")
        ]));
        var context = new ParseContext([]);
        var diagnostics = new DiagnosticBag();

        var result = scheduler.Run(
            rule,
            rule.Content.Alternatives,
            originInputPosition: context.Position,
            minimumPrecedence: 1,
            diagnostics,
            precomputedDescriptors: null,
            precomputedContinuationMetadata: DefaultContinuations(rule, rule.Content.Alternatives.Count),
            precomputedLookaheadProbes: UnknownProbes(rule.Content.Alternatives.Count),
            parseAlternative: (alternative, index) => new ScheduledAlternativeExecutionResult(CreateState(rule, alternative, context.Position, 4, index), new ParserLookaheadProbeResult(ParserLookaheadProbeKind.RequiresParse, "ID", "id", ["ID"])));

        Assert.AreEqual(2, result.CompletedStates.Count);
        Assert.AreEqual(1, result.PrunedStates.Count);
        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.AmbiguousAlternativesPruned.Code));
    }

    [TestMethod]
    public void Run_ReturnsNoSelectedState_WhenAlternativesIsEmpty()
    {
        var scheduler = new AlternativeScheduler();
        var rule = new Rule("r", 0, false, new Alternation([]));
        var context = new ParseContext([]);

        var result = scheduler.Run(
            rule,
            [],
            originInputPosition: 0,
            minimumPrecedence: 0,
            diagnostics: null,
            precomputedDescriptors: null,
            precomputedContinuationMetadata: [],
            precomputedLookaheadProbes: [],
            parseAlternative: (_, _) => new ScheduledAlternativeExecutionResult(null, new ParserLookaheadProbeResult(ParserLookaheadProbeKind.Unknown, null, null)));

        Assert.IsNull(result.SelectedState);
        Assert.AreEqual(0, result.CompletedStates.Count);
        Assert.AreEqual(0, result.FailedStates.Count);
    }

    [TestMethod]
    public void Run_AllAlternativesFail_ReturnsNullSelectedStateAndAllFailed()
    {
        var scheduler = new AlternativeScheduler();
        var (context, rule, alternatives) = CreateAlternatives();

        var result = scheduler.Run(
            rule,
            alternatives,
            originInputPosition: context.Position,
            minimumPrecedence: 0,
            diagnostics: null,
            precomputedDescriptors: null,
            precomputedContinuationMetadata: DefaultContinuations(rule, alternatives.Count),
            precomputedLookaheadProbes: UnknownProbes(alternatives.Count),
            parseAlternative: (_, _) => new ScheduledAlternativeExecutionResult(null, new ParserLookaheadProbeResult(ParserLookaheadProbeKind.Unknown, null, null)));

        Assert.IsNull(result.SelectedState);
        Assert.AreEqual(0, result.CompletedStates.Count);
        Assert.AreEqual(alternatives.Count, result.FailedStates.Count);
    }

    [TestMethod]
    public void Run_UsesMinimumPrecedenceInIdentity()
    {
        var scheduler = new AlternativeScheduler();
        var (context, rule, alternatives) = CreateAlternatives();
        var result = scheduler.Run(
            rule,
            alternatives,
            originInputPosition: context.Position,
            minimumPrecedence: 7,
            diagnostics: null,
            precomputedDescriptors: null,
            precomputedContinuationMetadata: DefaultContinuations(rule, alternatives.Count),
            precomputedLookaheadProbes: UnknownProbes(alternatives.Count),
            parseAlternative: (alternative, index) => index == 0
                ? new ScheduledAlternativeExecutionResult(null, new ParserLookaheadProbeResult(ParserLookaheadProbeKind.Unknown, null, null))
                : new ScheduledAlternativeExecutionResult(CreateState(rule, alternative, context.Position, 5, index), new ParserLookaheadProbeResult(ParserLookaheadProbeKind.RequiresParse, "ID", "id", ["ID"])));

        Assert.IsNotNull(result.SelectedState);
        Assert.IsTrue(result.CompletedStates.All(s => s.ToStateKey(7).MinimumPrecedence == 7));
    }

    [TestMethod]
    public void Run_BranchEquivalence_IgnoresPriorityAndContinuationMetadata()
    {
        var scheduler = new AlternativeScheduler();
        var rule = new Rule("r", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new LiteralMatch("a"), "X"),
            new Alternative(9, Associativity.Left, new LiteralMatch("a"), "X")
        ]));
        var context = new ParseContext([]);

        var result = scheduler.Run(
            rule,
            rule.Content.Alternatives,
            originInputPosition: context.Position,
            minimumPrecedence: 2,
            diagnostics: null,
            precomputedDescriptors: null,
            precomputedContinuationMetadata: DefaultContinuations(rule, rule.Content.Alternatives.Count),
            precomputedLookaheadProbes: UnknownProbes(rule.Content.Alternatives.Count),
            parseAlternative: (alternative, index) =>
            {
                var state = CreateState(rule, alternative, context.Position, 4, index)
                    .WithContinuation(new ContinuationKey(rule.Name, index, 0, 4 + index, 2));
                return new ScheduledAlternativeExecutionResult(state, new ParserLookaheadProbeResult(ParserLookaheadProbeKind.RequiresParse, "ID", "id", ["ID"]));
            });

        Assert.AreEqual(1, result.CompletedStates.Count, "Equivalent pruning key should keep a single branch.");
        Assert.AreEqual(1, result.PrunedStates.Count);
    }


    [TestMethod]
    public void Run_MetadataProbeDifferences_DoNotChangeSelectedState()
    {
        var scheduler = new AlternativeScheduler();
        var (context, rule, alternatives) = CreateAlternatives();

        var withMetadata = scheduler.Run(
            rule,
            alternatives,
            context.Position,
            minimumPrecedence: 0,
            diagnostics: null,
            precomputedDescriptors: null,
            precomputedContinuationMetadata: DefaultContinuations(rule, alternatives.Count),
            precomputedLookaheadProbes: SharedPrefixProbes(alternatives.Count, "ID"),
            parseAlternative: (alternative, index) => new ScheduledAlternativeExecutionResult(
                CreateState(rule, alternative, context.Position, 5 + index, index),
                new ParserLookaheadProbeResult(ParserLookaheadProbeKind.RequiresParse, "ID", "id", ["ID"])));

        var withoutMetadata = scheduler.Run(
            rule,
            alternatives,
            context.Position,
            minimumPrecedence: 0,
            diagnostics: null,
            precomputedDescriptors: null,
            precomputedContinuationMetadata: DefaultContinuations(rule, alternatives.Count),
            precomputedLookaheadProbes: UnknownProbes(alternatives.Count),
            parseAlternative: (alternative, index) => new ScheduledAlternativeExecutionResult(
                CreateState(rule, alternative, context.Position, 5 + index, index),
                new ParserLookaheadProbeResult(ParserLookaheadProbeKind.Unknown, null, null)));

        Assert.IsNotNull(withMetadata.SelectedState);
        Assert.IsNotNull(withoutMetadata.SelectedState);
        Assert.AreEqual(withMetadata.SelectedState.CurrentInputPosition, withoutMetadata.SelectedState.CurrentInputPosition);
        Assert.AreEqual(withMetadata.SelectedState.AlternativeIndex, withoutMetadata.SelectedState.AlternativeIndex);
    }

    [TestMethod]
    public void Run_ContinuationMetadataCategories_AreDeterministicAndDescriptive()
    {
        var scheduler = new AlternativeScheduler();
        var rule = new Rule("r", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new LiteralMatch("id"), "A"),
            new Alternative(0, Associativity.Left, new LiteralMatch("number"), "B")
        ]));
        var context = new ParseContext([]);

        var withSharedPrefix = scheduler.Run(
            rule,
            rule.Content.Alternatives,
            originInputPosition: context.Position,
            minimumPrecedence: 0,
            diagnostics: null,
            precomputedDescriptors: null,
            precomputedContinuationMetadata: SharedPrefixContinuations(rule, rule.Content.Alternatives.Count),
            precomputedLookaheadProbes: SharedPrefixProbes(rule.Content.Alternatives.Count, "ID"),
            parseAlternative: (alternative, index) => new ScheduledAlternativeExecutionResult(
                CreateState(rule, alternative, context.Position, 2 + index, index),
                new ParserLookaheadProbeResult(ParserLookaheadProbeKind.RequiresParse, "ID", "id", ["ID"])));

        var continuationCategories = withSharedPrefix.Metadata.SharedPrefixPlans
            .SelectMany(static p => p.Continuations)
            .Select(static c => c.Category)
            .Distinct()
            .ToArray();

        Assert.AreEqual(1, continuationCategories.Length);
        Assert.AreEqual(ParserContinuationCategory.SharedPrefixCandidate, continuationCategories[0]);
        Assert.IsTrue(withSharedPrefix.Metadata.SharedPrefixPlans.SelectMany(static p => p.Continuations).All(static c => c.Key.SequencePosition >= 0));
    }

    [TestMethod]
    public void Run_DeterministicSelection_UsesLengthThenPriorityThenAlternativeIndex()
    {
        var scheduler = new AlternativeScheduler();
        var rule = new Rule("r", 0, false, new Alternation([
            new Alternative(4, Associativity.Left, new LiteralMatch("a"), "A"),
            new Alternative(1, Associativity.Left, new LiteralMatch("a"), "A"),
            new Alternative(1, Associativity.Left, new LiteralMatch("a"), "A")
        ]));
        var context = new ParseContext([]);

        var result = scheduler.Run(
            rule,
            rule.Content.Alternatives,
            originInputPosition: context.Position,
            minimumPrecedence: 0,
            diagnostics: null,
            precomputedDescriptors: null,
            precomputedContinuationMetadata: [],
            precomputedLookaheadProbes: [],
            parseAlternative: (alternative, index) => index switch
            {
                0 => new ScheduledAlternativeExecutionResult(CreateState(rule, alternative, context.Position, 10, index), new ParserLookaheadProbeResult(ParserLookaheadProbeKind.Unknown, null, null)),
                1 => new ScheduledAlternativeExecutionResult(CreateState(rule, alternative, context.Position, 12, index), new ParserLookaheadProbeResult(ParserLookaheadProbeKind.Unknown, null, null)),
                _ => new ScheduledAlternativeExecutionResult(CreateState(rule, alternative, context.Position, 12, index), new ParserLookaheadProbeResult(ParserLookaheadProbeKind.Unknown, null, null))
            });

        Assert.IsNotNull(result.SelectedState);
        Assert.AreEqual(12, result.SelectedState.CurrentInputPosition);
        Assert.AreEqual(1, result.SelectedState.Alternative.Priority);
        Assert.AreEqual(1, result.SelectedState.AlternativeIndex);
    }

    [TestMethod]
    public void Run_PrunedStatesAssertions_AreMembershipBased_NotOrderBased()
    {
        var scheduler = new AlternativeScheduler();
        var rule = new Rule("r", 0, false, new Alternation([
            new Alternative(1, Associativity.Left, new LiteralMatch("a"), "X"),
            new Alternative(2, Associativity.Left, new LiteralMatch("a"), "X"),
            new Alternative(3, Associativity.Left, new LiteralMatch("a"), "Y")
        ]));
        var context = new ParseContext([]);

        var result = scheduler.Run(
            rule,
            rule.Content.Alternatives,
            originInputPosition: context.Position,
            minimumPrecedence: 0,
            diagnostics: null,
            precomputedDescriptors: null,
            precomputedContinuationMetadata: [],
            precomputedLookaheadProbes: [],
            parseAlternative: (alternative, index) => new ScheduledAlternativeExecutionResult(
                CreateState(rule, alternative, context.Position, 8, index),
                new ParserLookaheadProbeResult(ParserLookaheadProbeKind.RequiresParse, "ID", "id", ["ID"])));

        Assert.AreEqual(1, result.PrunedStates.Count);
        Assert.IsTrue(result.PrunedStates.All(static state => state.Status == ActiveParseStateStatus.Pruned));
        Assert.IsTrue(result.CompletedStates.Any(static state => state.Alternative.Label == "X"));
        Assert.IsTrue(result.CompletedStates.Any(static state => state.Alternative.Label == "Y"));
    }

    [TestMethod]
    public void Run_WithObserver_ProducesDeterministicEventOrdering()
    {
        var observer = new RecordingRuntimeObserver();
        var scheduler = new AlternativeScheduler(observer);
        var (context, rule, alternatives) = CreateAlternatives();

        _ = scheduler.Run(
            rule,
            alternatives,
            originInputPosition: context.Position,
            minimumPrecedence: 0,
            diagnostics: null,
            precomputedDescriptors: null,
            precomputedContinuationMetadata: [],
            precomputedLookaheadProbes: [],
            parseAlternative: (alternative, index) => new ScheduledAlternativeExecutionResult(
                CreateState(rule, alternative, context.Position, 5 + index, index),
                new ParserLookaheadProbeResult(ParserLookaheadProbeKind.RequiresParse, "ID", "id", ["ID"])));

        CollectionAssert.AreEqual(
            new string[]
            {
                "started:0", "completed:0",
                "started:1", "completed:1",
                "started:2", "completed:2",
                "selected:2"
            },
            observer.Events);
    }

    [TestMethod]
    public void Run_WithObserver_DoesNotChangeSchedulerSelection()
    {
        var withObserverScheduler = new AlternativeScheduler(new RecordingRuntimeObserver());
        var withoutObserverScheduler = new AlternativeScheduler();
        var (context, rule, alternatives) = CreateAlternatives();

        var withObserver = withObserverScheduler.Run(
            rule,
            alternatives,
            context.Position,
            minimumPrecedence: 0,
            diagnostics: null,
            precomputedDescriptors: null,
            precomputedContinuationMetadata: [],
            precomputedLookaheadProbes: [],
            parseAlternative: (alternative, index) => new ScheduledAlternativeExecutionResult(
                CreateState(rule, alternative, context.Position, 7 + index, index),
                new ParserLookaheadProbeResult(ParserLookaheadProbeKind.Unknown, null, null)));

        var withoutObserver = withoutObserverScheduler.Run(
            rule,
            alternatives,
            context.Position,
            minimumPrecedence: 0,
            diagnostics: null,
            precomputedDescriptors: null,
            precomputedContinuationMetadata: [],
            precomputedLookaheadProbes: [],
            parseAlternative: (alternative, index) => new ScheduledAlternativeExecutionResult(
                CreateState(rule, alternative, context.Position, 7 + index, index),
                new ParserLookaheadProbeResult(ParserLookaheadProbeKind.Unknown, null, null)));

        Assert.IsNotNull(withObserver.SelectedState);
        Assert.IsNotNull(withoutObserver.SelectedState);
        Assert.AreEqual(withoutObserver.SelectedState.AlternativeIndex, withObserver.SelectedState.AlternativeIndex);
        Assert.AreEqual(withoutObserver.SelectedState.CurrentInputPosition, withObserver.SelectedState.CurrentInputPosition);
    }



    [TestMethod]
    public void Run_WithThrowingObserver_DoesNotChangeSchedulerSelection()
    {
        var withThrowingObserverScheduler = new AlternativeScheduler(new ThrowingRuntimeObserver());
        var withoutObserverScheduler = new AlternativeScheduler();
        var (context, rule, alternatives) = CreateAlternatives();

        var withThrowingObserver = withThrowingObserverScheduler.Run(
            rule,
            alternatives,
            context.Position,
            minimumPrecedence: 0,
            diagnostics: null,
            precomputedDescriptors: null,
            precomputedContinuationMetadata: [],
            precomputedLookaheadProbes: [],
            parseAlternative: (alternative, index) => new ScheduledAlternativeExecutionResult(
                CreateState(rule, alternative, context.Position, 7 + index, index),
                new ParserLookaheadProbeResult(ParserLookaheadProbeKind.Unknown, null, null)));

        var withoutObserver = withoutObserverScheduler.Run(
            rule,
            alternatives,
            context.Position,
            minimumPrecedence: 0,
            diagnostics: null,
            precomputedDescriptors: null,
            precomputedContinuationMetadata: [],
            precomputedLookaheadProbes: [],
            parseAlternative: (alternative, index) => new ScheduledAlternativeExecutionResult(
                CreateState(rule, alternative, context.Position, 7 + index, index),
                new ParserLookaheadProbeResult(ParserLookaheadProbeKind.Unknown, null, null)));

        Assert.IsNotNull(withThrowingObserver.SelectedState);
        Assert.IsNotNull(withoutObserver.SelectedState);
        Assert.AreEqual(withoutObserver.SelectedState.AlternativeIndex, withThrowingObserver.SelectedState.AlternativeIndex);
        Assert.AreEqual(withoutObserver.SelectedState.CurrentInputPosition, withThrowingObserver.SelectedState.CurrentInputPosition);
    }

    [TestMethod]
    public void Run_WithThrowingObserver_ContinuesToNotifyDeterministically()
    {
        var observer = new CountingThrowingRuntimeObserver();
        var scheduler = new AlternativeScheduler(observer);
        var (context, rule, alternatives) = CreateAlternatives();

        var result = scheduler.Run(
            rule,
            alternatives,
            context.Position,
            minimumPrecedence: 0,
            diagnostics: null,
            precomputedDescriptors: null,
            precomputedContinuationMetadata: [],
            precomputedLookaheadProbes: [],
            parseAlternative: (alternative, index) => new ScheduledAlternativeExecutionResult(
                CreateState(rule, alternative, context.Position, 7 + index, index),
                new ParserLookaheadProbeResult(ParserLookaheadProbeKind.Unknown, null, null)));

        Assert.IsNotNull(result.SelectedState);
        Assert.AreEqual(alternatives.Count, observer.StartedCount);
        Assert.AreEqual(alternatives.Count, observer.CompletedCount);
        Assert.AreEqual(1, observer.SelectedCount);
    }

    [TestMethod]
    public void AlternativeRuntimeObservation_NormalizesStatusAndKind()
    {
        var selectedCompleted = new AlternativeRuntimeObservation(
            ParserRuntimeObservationKind.AlternativeSelected,
            "r",
            0,
            1,
            0,
            2,
            ParserRuntimeObservationStatus.Completed);

        var legacyCompleted = new AlternativeRuntimeObservation("r", 0, 1, 0, 2, "Completed");
        var legacyUnknown = new AlternativeRuntimeObservation("r", 0, 1, 0, 2, "not-a-status");

        Assert.AreEqual(ParserRuntimeObservationKind.AlternativeSelected, selectedCompleted.Kind);
        Assert.AreEqual(ParserRuntimeObservationStatus.Completed, selectedCompleted.Status);
        Assert.AreEqual(ParserRuntimeObservationKind.AlternativeCompleted, legacyCompleted.Kind);
        Assert.AreEqual(ParserRuntimeObservationStatus.Completed, legacyCompleted.Status);
        Assert.AreEqual(ParserRuntimeObservationKind.Unknown, legacyUnknown.Kind);
        Assert.AreEqual(ParserRuntimeObservationStatus.Unknown, legacyUnknown.Status);
    }

    private static (ParseContext Context, Rule Rule, IReadOnlyList<Alternative> Alternatives) CreateAlternatives()
    {
        var a = new Alternative(2, Associativity.Left, new LiteralMatch("a"), "A");
        var b = new Alternative(1, Associativity.Left, new LiteralMatch("a"), "A");
        var c = new Alternative(3, Associativity.Left, new LiteralMatch("a"), "C");
        var alternatives = new List<Alternative> { a, b, c };
        var rule = new Rule("r", 0, false, new Alternation([.. alternatives]));
        var context = new ParseContext([]);
        return (context, rule, alternatives);
    }

    private static ParseNode CreateNode(Rule rule, int position)
    {
        return new ParserNode(new SourceSpan(0, position), "DEFAULT_MODE", rule, []);
    }

    private static IReadOnlyList<ParserLookaheadProbeResult> UnknownProbes(int count)
    {
        return Enumerable.Range(0, count)
            .Select(static _ => new ParserLookaheadProbeResult(ParserLookaheadProbeKind.Unknown, null, null))
            .ToArray();
    }

    private static IReadOnlyList<ParserLookaheadProbeResult> SharedPrefixProbes(int count, string tokenName)
    {
        return Enumerable.Range(0, count)
            .Select(_ => new ParserLookaheadProbeResult(ParserLookaheadProbeKind.RequiresParse, tokenName, tokenName.ToLowerInvariant(), [tokenName]))
            .ToArray();
    }

    private static IReadOnlyList<ParserContinuationDescriptor> DefaultContinuations(Rule rule, int count)
    {
        return Enumerable.Range(0, count)
            .Select(index => new ParserContinuationDescriptor(
                new ParserContinuationKey(rule.Name, index, 0),
                ParserContinuationCategory.Sequential,
                null,
                false))
            .ToArray();
    }

    private static IReadOnlyList<ParserContinuationDescriptor> SharedPrefixContinuations(Rule rule, int count)
    {
        return Enumerable.Range(0, count)
            .Select(index => new ParserContinuationDescriptor(
                new ParserContinuationKey(rule.Name, index, 1),
                ParserContinuationCategory.SharedPrefixCandidate,
                ["ID"],
                true))
            .ToArray();
    }

    private static ActiveParseState CreateState(Rule rule, Alternative alternative, int origin, int current, int alternativeIndex)
    {
        return new ActiveParseState
        {
            Rule = rule,
            Alternative = alternative,
            OriginInputPosition = origin,
            CurrentInputPosition = current,
            AlternativeIndex = alternativeIndex,
            Cursor = new RuleContentCursor { Index = 0, Kind = "alternative-root" },
            PartialNode = CreateNode(rule, current),
            EndPosition = current,
            Status = ActiveParseStateStatus.Completed,
            ParentStateKey = null,
            Depth = 0,
            Continuation = null
        };
    }



    private sealed class ThrowingRuntimeObserver : IParserRuntimeObserver
    {
        public void OnAlternativeStarted(AlternativeRuntimeObservation observation) => throw new InvalidOperationException("observer exception");

        public void OnAlternativeCompleted(AlternativeRuntimeObservation observation) => throw new InvalidOperationException("observer exception");

        public void OnAlternativeFailed(AlternativeRuntimeObservation observation) => throw new InvalidOperationException("observer exception");

        public void OnAlternativePruned(AlternativeRuntimeObservation observation) => throw new InvalidOperationException("observer exception");

        public void OnAlternativeSelected(AlternativeRuntimeObservation observation) => throw new InvalidOperationException("observer exception");
    }

    private sealed class CountingThrowingRuntimeObserver : IParserRuntimeObserver
    {
        public int StartedCount { get; private set; }

        public int CompletedCount { get; private set; }

        public int FailedCount { get; private set; }

        public int PrunedCount { get; private set; }

        public int SelectedCount { get; private set; }

        public void OnAlternativeStarted(AlternativeRuntimeObservation observation)
        {
            StartedCount++;
            throw new InvalidOperationException("observer exception");
        }

        public void OnAlternativeCompleted(AlternativeRuntimeObservation observation)
        {
            CompletedCount++;
            throw new InvalidOperationException("observer exception");
        }

        public void OnAlternativeFailed(AlternativeRuntimeObservation observation)
        {
            FailedCount++;
            throw new InvalidOperationException("observer exception");
        }

        public void OnAlternativePruned(AlternativeRuntimeObservation observation)
        {
            PrunedCount++;
            throw new InvalidOperationException("observer exception");
        }

        public void OnAlternativeSelected(AlternativeRuntimeObservation observation)
        {
            SelectedCount++;
            throw new InvalidOperationException("observer exception");
        }
    }

    private sealed class RecordingRuntimeObserver : IParserRuntimeObserver
    {
        public List<string> Events { get; } = [];

        public void OnAlternativeStarted(AlternativeRuntimeObservation observation) => Events.Add($"started:{observation.AlternativeIndex}");

        public void OnAlternativeCompleted(AlternativeRuntimeObservation observation) => Events.Add($"completed:{observation.AlternativeIndex}");

        public void OnAlternativeFailed(AlternativeRuntimeObservation observation) => Events.Add($"failed:{observation.AlternativeIndex}");

        public void OnAlternativePruned(AlternativeRuntimeObservation observation) => Events.Add($"pruned:{observation.AlternativeIndex}");

        public void OnAlternativeSelected(AlternativeRuntimeObservation observation) => Events.Add($"selected:{observation.AlternativeIndex}");
    }
}
