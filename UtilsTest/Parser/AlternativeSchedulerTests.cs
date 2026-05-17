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
            parseAlternative: (alternative, index) => new ScheduledAlternativeExecutionResult(
                CreateState(rule, alternative, context.Position, 5 + index, index),
                new ParserLookaheadProbeResult(ParserLookaheadProbeKind.RequiresParse, "ID", "id", ["ID"])));

        var withoutMetadata = scheduler.Run(
            rule,
            alternatives,
            context.Position,
            minimumPrecedence: 0,
            diagnostics: null,
            parseAlternative: (alternative, index) => new ScheduledAlternativeExecutionResult(
                CreateState(rule, alternative, context.Position, 5 + index, index),
                new ParserLookaheadProbeResult(ParserLookaheadProbeKind.Unknown, null, null)));

        Assert.IsNotNull(withMetadata.SelectedState);
        Assert.IsNotNull(withoutMetadata.SelectedState);
        Assert.AreEqual(withMetadata.SelectedState.CurrentInputPosition, withoutMetadata.SelectedState.CurrentInputPosition);
        Assert.AreEqual(withMetadata.SelectedState.AlternativeIndex, withoutMetadata.SelectedState.AlternativeIndex);
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
}

