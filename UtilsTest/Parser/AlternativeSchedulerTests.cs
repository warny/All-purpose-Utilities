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
        var scheduler = new AlternativeScheduler(new ParserStateRegistry());
        var state = CreateState(priority: 1, alternativeIndex: 0, currentPosition: 4, continuation: null);

        var result = scheduler.Run([state, state], diagnostics: null);

        Assert.AreEqual(1, result.CompletedStates.Count);
    }

    [TestMethod]
    public void Run_DoesNotMergeDifferentAlternativeIndexOrContinuationOrPosition()
    {
        var scheduler = new AlternativeScheduler(new ParserStateRegistry());
        var baseState = CreateState(1, 0, 4, null, "A");
        var differentAlternative = CreateState(1, 1, 4, null, "B");
        var differentContinuation = CreateState(1, 0, 4, new ContinuationKey("rule", 0, 0, 4, 0), "C");
        var differentPosition = CreateState(1, 0, 5, null, "D");

        var result = scheduler.Run([baseState, differentAlternative, differentContinuation, differentPosition], diagnostics: null);

        Assert.AreEqual(4, result.CompletedStates.Count);
    }

    [TestMethod]
    public void Run_SelectsBestByLongestThenPriorityDeterministically()
    {
        var scheduler = new AlternativeScheduler(new ParserStateRegistry());
        var shorter = CreateState(priority: 0, alternativeIndex: 0, currentPosition: 3, continuation: null);
        var longer = CreateState(priority: 2, alternativeIndex: 1, currentPosition: 6, continuation: null);
        var sameLengthBetterPriority = CreateState(priority: 1, alternativeIndex: 2, currentPosition: 6, continuation: null);

        var result = scheduler.Run([shorter, longer, sameLengthBetterPriority], diagnostics: null);

        Assert.IsNotNull(result.SelectedState);
        Assert.AreEqual(1, result.SelectedState.Alternative.Priority);
        Assert.AreEqual(6, result.SelectedState.CurrentInputPosition);
    }

    [TestMethod]
    public void Run_PrunesEquivalentStatesUsingBranchEquivalenceKey()
    {
        var scheduler = new AlternativeScheduler(new ParserStateRegistry());
        var diagnostics = new DiagnosticBag();
        var kept = CreateState(priority: 1, alternativeIndex: 0, currentPosition: 8, continuation: null, label: "same");
        var pruned = CreateState(priority: 2, alternativeIndex: 4, currentPosition: 8, continuation: null, label: "same");

        var result = scheduler.Run([kept, pruned], diagnostics);

        Assert.AreEqual(1, result.CompletedStates.Count);
        Assert.AreEqual(1, result.PrunedStates.Count);
        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.AmbiguousAlternativesPruned.Code));
    }

    [TestMethod]
    public void Run_DoesNotPruneDistinctSemantics()
    {
        var scheduler = new AlternativeScheduler(new ParserStateRegistry());
        var left = CreateState(priority: 1, alternativeIndex: 0, currentPosition: 8, continuation: null, label: "L");
        var right = CreateState(priority: 2, alternativeIndex: 1, currentPosition: 8, continuation: null, label: "R");

        var result = scheduler.Run([left, right], diagnostics: null);

        Assert.AreEqual(2, result.CompletedStates.Count);
        Assert.AreEqual(0, result.PrunedStates.Count);
    }

    private static ActiveParseState CreateState(int priority, int alternativeIndex, int currentPosition, ContinuationKey? continuation, string label = "A")
    {
        var alternative = new Alternative(priority, Associativity.Left, new LiteralMatch("x"), label);
        var rule = new Rule("rule", 0, false, new Alternation([alternative]));
        return new ActiveParseState
        {
            Rule = rule,
            Alternative = alternative,
            OriginInputPosition = 0,
            CurrentInputPosition = currentPosition,
            AlternativeIndex = alternativeIndex,
            Cursor = new RuleContentCursor { Index = 0, Kind = "alternative-root" },
            PartialNode = new ParserNode(new SourceSpan(0, currentPosition), "DEFAULT_MODE", rule, []),
            EndPosition = currentPosition,
            Status = ActiveParseStateStatus.Completed,
            ParentStateKey = null,
            Depth = 0,
            Continuation = continuation
        };
    }
}
