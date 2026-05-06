using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

[TestClass]
public class ActiveParseStateTests
{
    [TestMethod]
    public void ActiveParseState_BranchRoundTrip_PreservesStateData()
    {
        var branch = CreateBranch(priority: 2, cursorIndex: 1);

        var activeState = ActiveParseState.FromBranch(branch);
        var restoredBranch = activeState.ToBranch();

        Assert.AreEqual(branch.Rule.Name, restoredBranch.Rule.Name);
        Assert.AreEqual(branch.Alternative.Priority, restoredBranch.Alternative.Priority);
        Assert.AreEqual(branch.Alternative.Label, restoredBranch.Alternative.Label);
        Assert.AreEqual(branch.InputPosition, restoredBranch.InputPosition);
        Assert.AreEqual(branch.Cursor.Index, restoredBranch.Cursor.Index);
        Assert.AreEqual(branch.Cursor.Kind, restoredBranch.Cursor.Kind);
        Assert.AreEqual(branch.EndPosition, restoredBranch.EndPosition);
        Assert.AreEqual(branch.IsComplete, restoredBranch.IsComplete);
        Assert.AreSame(branch.PartialNode, restoredBranch.PartialNode);
    }

    [TestMethod]
    public void ActiveParseState_ToStateKey_EquivalentStatesAreEqual()
    {
        var left = CreateActiveState(priority: 1, cursorIndex: 0, alternativeIndex: 0, currentPosition: 6);
        var right = CreateActiveState(priority: 1, cursorIndex: 0, alternativeIndex: 0, currentPosition: 6);

        var leftKey = left.ToStateKey(minimumPrecedence: 3);
        var rightKey = right.ToStateKey(minimumPrecedence: 3);

        Assert.AreEqual(leftKey, rightKey);
        Assert.AreEqual(leftKey.GetHashCode(), rightKey.GetHashCode());
    }

    [TestMethod]
    public void ActiveParseState_ToStateKey_DifferentDimensionsProduceDifferentKeys()
    {
        var baseline = CreateActiveState(1, 0, 0, 6).ToStateKey(2);
        Assert.AreNotEqual(baseline, CreateActiveState(2, 0, 1, 6).ToStateKey(2));
        Assert.AreNotEqual(baseline, CreateActiveState(1, 1, 0, 6).ToStateKey(2));
        Assert.AreNotEqual(baseline, CreateActiveState(1, 0, 0, 7).ToStateKey(2));
        Assert.AreNotEqual(baseline, CreateActiveState(1, 0, 0, 6).ToStateKey(4));
        Assert.AreNotEqual(baseline, CreateActiveState(1, 0, 0, 6).WithContinuation(new ContinuationKey("r", 0, 0, 6, 2)).ToStateKey(2));
    }

    [TestMethod]
    public void ActiveParseState_LifecycleTransitions_AreExplicit()
    {
        var active = CreateActiveState(1, 0, 0, 4);
        Assert.AreEqual(ActiveParseStateStatus.Active, active.Status);

        var completed = active.Complete(8);
        Assert.AreEqual(ActiveParseStateStatus.Completed, completed.Status);
        Assert.AreEqual(8, completed.EndPosition);

        var failed = active.Fail();
        Assert.AreEqual(ActiveParseStateStatus.Failed, failed.Status);
        Assert.IsNull(failed.EndPosition);

        var pruned = active.Prune();
        Assert.AreEqual(ActiveParseStateStatus.Pruned, pruned.Status);
    }

    [TestMethod]
    public void ActiveParseState_RegistryProjection_UsesExpectedKeys()
    {
        var state = CreateActiveState(3, 2, 5, 10);

        var parserStateKey = state.ToParserStateKey(4);
        Assert.AreEqual(new ParserStateKey("sampleRule", 10, 5, 2, 4), parserStateKey);

        var invocationKey = state.ToRuleInvocationKey(4);
        Assert.AreEqual(new RuleInvocationKey("sampleRule", 4, 4), invocationKey);

        var continuationKey = state.ToContinuationKey(11, 4);
        Assert.AreEqual(new ContinuationKey("sampleRule", 5, 2, 11, 4), continuationKey);
    }

    [TestMethod]
    public void ActiveParseState_ToBranchEquivalenceKey_SameKeyForDifferentAlternativeIndices()
    {
        // Two states that reach the same semantic position from different alternatives must share
        // the same equivalence key so that ambiguity pruning can collapse them.
        // Both use priority=1 so the label ("alt1") is identical; only AlternativeIndex differs.
        var stateA = CreateActiveState(priority: 1, cursorIndex: 0, alternativeIndex: 0, currentPosition: 10).Complete(10);
        var stateB = CreateActiveState(priority: 1, cursorIndex: 0, alternativeIndex: 3, currentPosition: 10).Complete(10);

        Assert.AreEqual(stateA.ToBranchEquivalenceKey(), stateB.ToBranchEquivalenceKey());
        // Scheduler identity must still distinguish them.
        Assert.AreNotEqual(stateA.ToStateKey(2), stateB.ToStateKey(2));
    }

    [TestMethod]
    public void ActiveParseState_ToBranchEquivalenceKey_DifferentKeyForDifferentEndPosition()
    {
        var stateA = CreateActiveState(priority: 1, cursorIndex: 0, alternativeIndex: 0, currentPosition: 10).Complete(10);
        var stateB = CreateActiveState(priority: 1, cursorIndex: 0, alternativeIndex: 0, currentPosition: 12).Complete(12);

        Assert.AreNotEqual(stateA.ToBranchEquivalenceKey(), stateB.ToBranchEquivalenceKey());
    }

    [TestMethod]
    public void ActiveParseState_ToBranchEquivalenceKey_IndependentOfSchedulerIdentity()
    {
        // Equivalence key must be independent of scheduler-identity fields so it does not
        // accidentally prevent pruning of semantically identical states on different paths.
        var state = CreateActiveState(priority: 1, cursorIndex: 0, alternativeIndex: 0, currentPosition: 6).Complete(6);
        var withContinuation = state.WithContinuation(new ContinuationKey("sampleRule", 0, 0, 6, 2));

        Assert.AreEqual(state.ToBranchEquivalenceKey(), withContinuation.ToBranchEquivalenceKey());
    }

    [TestMethod]
    public void ActiveParseState_RegistryIntegration_StableReuseKeys()
    {
        var state = CreateActiveState(1, 0, 0, 4).Complete(9);
        var registry = new ParserStateRegistry();
        var invocationKey = state.ToRuleInvocationKey(2);
        var parserKey = state.ToParserStateKey(2);

        Assert.IsTrue(registry.TryEnterState(parserKey));
        Assert.IsFalse(registry.TryEnterState(parserKey), "Same parser state key should deduplicate.");

        var result = new ParserRuleResult(state.PartialNode, state.EndPosition ?? state.CurrentInputPosition, IsFailure: false);
        Assert.IsTrue(registry.AddCompletedResult(invocationKey, result));
        Assert.IsTrue(registry.TryGetReusableResult(invocationKey, out var reusable));
        Assert.AreEqual(result.EndPosition, reusable.EndPosition);
        Assert.AreSame(result.Node, reusable.Node);
    }

    private static ActiveParseState CreateActiveState(int priority, int cursorIndex, int alternativeIndex, int currentPosition)
    {
        var alternative = new Alternative(priority, Associativity.Left, new LiteralMatch("A"), $"alt{priority}");
        var rule = new Rule("sampleRule", 0, false, new Alternation([alternative]));

        return new ActiveParseState
        {
            Rule = rule,
            Alternative = alternative,
            OriginInputPosition = 4,
            CurrentInputPosition = currentPosition,
            AlternativeIndex = alternativeIndex,
            Cursor = new RuleContentCursor { Index = cursorIndex, Kind = "alternative-root" },
            PartialNode = new ParserNode(new SourceSpan(2, 3), "DEFAULT_MODE", rule, []),
            EndPosition = null,
            Status = ActiveParseStateStatus.Active,
            ParentStateKey = null,
            Depth = 0,
            Continuation = null
        };
    }

    private static ParseBranch CreateBranch(int priority, int cursorIndex)
    {
        var alternative = new Alternative(priority, Associativity.Left, new LiteralMatch("A"), $"alt{priority}");
        var rule = new Rule(
            "sampleRule",
            0,
            false,
            new Alternation([alternative]));

        return new ParseBranch
        {
            Rule = rule,
            Alternative = alternative,
            InputPosition = 4,
            Cursor = new RuleContentCursor { Index = cursorIndex, Kind = "alternative-root" },
            PartialNode = new ParserNode(new SourceSpan(2, 3), "DEFAULT_MODE", rule, []),
            EndPosition = 7,
            IsComplete = true
        };
    }
}
