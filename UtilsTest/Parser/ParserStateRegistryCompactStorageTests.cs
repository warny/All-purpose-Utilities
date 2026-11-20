using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Pins the identity semantics of the compact <see cref="ParserStateRegistry"/> storage: visited states grouped by
/// (rule, position, precedence) with an alternative bit mask plus an overflow set, continuations as one flat set of
/// (invocation, continuation) pairs, and completed results stored inline with an overflow list. Every case checks that
/// the storage keeps the exact identities of the historical sets and lists.
/// </summary>
[TestClass]
public class ParserStateRegistryCompactStorageTests
{
    /// <summary>A visited state entered several times is new only the first time.</summary>
    [TestMethod]
    public void VisitedState_SameKeyRepeated_IsNewOnlyOnce()
    {
        var registry = new ParserStateRegistry();
        var key = new ParserStateKey("expr", 3, 2, 2, 0);

        Assert.IsTrue(registry.TryEnterState(key));
        Assert.IsFalse(registry.TryEnterState(key));
        Assert.IsFalse(registry.TryEnterState(key));
        Assert.AreEqual(1L, registry.VisitedStateCount);
    }

    /// <summary>Rule names compare by value, as in the historical set: an equal string from another instance is the same state.</summary>
    [TestMethod]
    public void VisitedState_EqualRuleNameFromAnotherInstance_IsTheSameState()
    {
        var registry = new ParserStateRegistry();
        string otherInstance = new("expr".ToCharArray());

        Assert.IsTrue(registry.TryEnterState(new ParserStateKey("expr", 3, 1, 1, 0)));
        Assert.IsFalse(registry.TryEnterState(new ParserStateKey(otherInstance, 3, 1, 1, 0)));
    }

    /// <summary>Keys differing in a single component are distinct states, whether they share a group or not.</summary>
    [TestMethod]
    public void VisitedState_KeysDifferingInOneComponent_AreDistinct()
    {
        var registry = new ParserStateRegistry();
        ParserStateKey[] keys =
        [
            new("expr", 3, 1, 1, 0),
            new("term", 3, 1, 1, 0),  // another rule
            new("expr", 4, 1, 1, 0),  // another position
            new("expr", 3, 2, 2, 0),  // another alternative of the same group
            new("expr", 3, 1, 1, 5),  // another precedence
            new("expr", 3, 1, 0, 0),  // another element index: overflow set, same group key and bit as the first key
            new("expr", 3, 0, 1, 0),  // alternative and element indexes swapped
        ];

        foreach (var key in keys)
        {
            Assert.IsTrue(registry.TryEnterState(key), $"{key} is a new state");
        }

        foreach (var key in keys)
        {
            Assert.IsFalse(registry.TryEnterState(key), $"{key} was already entered");
        }

        Assert.AreEqual(keys.Length, registry.VisitedStateCount);
    }

    /// <summary>
    /// Alternative indexes outside the 64-bit mask (negative, 64 and above) are stored exactly in the overflow set and never
    /// alias an in-range bit of the same group.
    /// </summary>
    [TestMethod]
    public void VisitedState_IndexesOutsideTheMask_DoNotAliasMaskBits()
    {
        var registry = new ParserStateRegistry();
        int[] indexes = [0, 1, 63, 64, 65, 127, 128, -1, int.MinValue, int.MaxValue];

        foreach (int index in indexes)
        {
            Assert.IsTrue(registry.TryEnterState(new ParserStateKey("expr", 0, index, index, 0)), $"index {index} is new");
        }

        foreach (int index in indexes)
        {
            Assert.IsFalse(registry.TryEnterState(new ParserStateKey("expr", 0, index, index, 0)), $"index {index} was entered");
        }

        Assert.AreEqual(indexes.Length, registry.VisitedStateCount);
        Assert.AreEqual(1, registry.VisitedStateGroupCount, "in-range indexes of one rule, position and precedence share one group");
    }

    /// <summary>The same continuation registered twice for one invocation is recorded once.</summary>
    [TestMethod]
    public void Continuation_SamePairRepeated_IsRecordedOnce()
    {
        var registry = new ParserStateRegistry();
        var invocation = new RuleInvocationKey("expr", 2, 0);
        var continuation = new ContinuationKey("start", 0, 1, 2, 0);

        Assert.IsTrue(registry.AddContinuation(invocation, continuation));
        Assert.IsFalse(registry.AddContinuation(invocation, continuation));
        Assert.AreEqual(1, registry.ContinuationCount);
        CollectionAssert.AreEqual(new[] { continuation }, registry.GetContinuations(invocation).ToArray());
    }

    /// <summary>Different continuations of one invocation are all kept, each exactly once (the order is not part of the contract).</summary>
    [TestMethod]
    public void Continuation_DifferentContinuationsOfOneInvocation_AreAllKept()
    {
        var registry = new ParserStateRegistry();
        var invocation = new RuleInvocationKey("expr", 2, 0);
        ContinuationKey[] continuations =
        [
            new("start", 0, 1, 2, 0),
            new("start", 1, 1, 2, 0),
            new("start", 0, 2, 2, 0),
            new("other", 0, 1, 2, 0),
            new("start", 0, 1, 3, 0),
            new("start", 0, 1, 2, 4),
        ];

        foreach (var continuation in continuations)
        {
            Assert.IsTrue(registry.AddContinuation(invocation, continuation));
        }

        CollectionAssert.AreEquivalent(continuations, registry.GetContinuations(invocation).ToArray());
        Assert.AreEqual(1, registry.ContinuationInvocationCount);
    }

    /// <summary>
    /// One continuation recorded for invocations that share their position but differ in rule, precedence or semantic
    /// execution-state key is a distinct entry for each invocation, and each invocation sees only its own continuations.
    /// </summary>
    [TestMethod]
    public void Continuation_SameContinuationForDistinctInvocations_IsIsolatedPerInvocation()
    {
        var registry = new ParserStateRegistry();
        var continuation = new ContinuationKey("start", 0, 1, 2, 0);
        RuleInvocationKey[] invocations =
        [
            new("expr", 2, 0, new ParserExecutionStateKey(0)),
            new("term", 2, 0, new ParserExecutionStateKey(0)),
            new("expr", 2, 1, new ParserExecutionStateKey(0)),
            new("expr", 2, 0, new ParserExecutionStateKey(7)),
        ];

        foreach (var invocation in invocations)
        {
            Assert.IsTrue(registry.AddContinuation(invocation, continuation));
        }

        var extra = new ContinuationKey("start", 1, 3, 2, 0);
        Assert.IsTrue(registry.AddContinuation(invocations[3], extra));

        CollectionAssert.AreEqual(new[] { continuation }, registry.GetContinuations(invocations[0]).ToArray());
        CollectionAssert.AreEquivalent(new[] { continuation, extra }, registry.GetContinuations(invocations[3]).ToArray());
        Assert.AreEqual(0, registry.GetContinuations(new RuleInvocationKey("expr", 3, 0)).Count);
        Assert.AreEqual(5, registry.ContinuationCount);
        Assert.AreEqual(4, registry.ContinuationInvocationCount);
    }

    /// <summary>
    /// Many invocations registered in a heavily interleaved order, with duplicates and enough pairs to resize the flat set
    /// several times, each return exactly their own distinct continuations: the same set as one set per invocation, with
    /// no duplicate and no continuation of another invocation.
    /// </summary>
    [TestMethod]
    public void Continuation_InterleavedInvocationsWithDuplicates_ReturnExactlyTheirOwnSet()
    {
        var registry = new ParserStateRegistry();
        var expected = new Dictionary<RuleInvocationKey, HashSet<ContinuationKey>>();
        var random = new Random(716);
        string[] rules = ["expr", "term", "factor"];

        for (int step = 0; step < 20_000; step++)
        {
            var invocation = new RuleInvocationKey(rules[random.Next(rules.Length)], random.Next(40), random.Next(3), new ParserExecutionStateKey((ulong)random.Next(2)));
            var continuation = new ContinuationKey(rules[random.Next(rules.Length)], random.Next(4), random.Next(4), invocation.OriginPosition + random.Next(2), random.Next(3));
            if (!expected.TryGetValue(invocation, out var set))
            {
                set = [];
                expected[invocation] = set;
            }

            Assert.AreEqual(set.Add(continuation), registry.AddContinuation(invocation, continuation), $"step {step}");
        }

        Assert.AreEqual(expected.Values.Sum(static set => set.Count), registry.ContinuationCount);
        Assert.AreEqual(expected.Count, registry.ContinuationInvocationCount);
        Assert.IsTrue(registry.ContinuationCount > 10 * expected.Count, "invocations carry many continuations");
        foreach (var (invocation, set) in expected)
        {
            var actual = registry.GetContinuations(invocation);
            Assert.AreEqual(set.Count, actual.Count, $"{invocation} returns each continuation once");
            CollectionAssert.AreEquivalent(set.ToArray(), actual.ToArray(), $"{invocation}");
        }
    }

    /// <summary>Results beyond the inline first result keep their insertion order and duplicate detection.</summary>
    [TestMethod]
    public void CompletedResults_BeyondTheFirst_KeepInsertionOrderAndDeduplication()
    {
        var registry = new ParserStateRegistry();
        var invocation = new RuleInvocationKey("expr", 0, 0);
        ParserRuleResult[] results =
        [
            new(null, 0, true),
            new(CreateNode(), 2, false, "snapshot-a"),
            new(CreateNode(), 3, false, "snapshot-b"),
            new(null, 1, true),
        ];

        foreach (var result in results)
        {
            Assert.IsTrue(registry.AddCompletedResult(invocation, result));
        }

        foreach (var result in results)
        {
            Assert.IsFalse(registry.AddCompletedResult(invocation, result), "an identical result is a duplicate wherever it is stored");
        }

        // Duplicate identity is (end position, failure, node reference): another snapshot does not make a result new.
        Assert.IsFalse(registry.AddCompletedResult(invocation, results[2] with { ExecutionStateSnapshot = "other" }));
        CollectionAssert.AreEqual(results, registry.GetCompletedResults(invocation).ToArray());
        Assert.AreEqual(4L, registry.CompletedResultCount);
        Assert.AreEqual(1, registry.CompletedInvocationCount);
    }

    /// <summary>A reusable success stored after a failure is still preferred, with its own snapshot.</summary>
    [TestMethod]
    public void TryGetReusableResult_SuccessStoredAfterFailures_IsPreferredWithItsSnapshot()
    {
        var registry = new ParserStateRegistry();
        var invocation = new RuleInvocationKey("expr", 0, 0, new ParserExecutionStateKey(3));
        var success = new ParserRuleResult(CreateNode(), 4, false, "post-rule-state");

        registry.AddCompletedResult(invocation, new ParserRuleResult(null, 0, true, "failure-state"));
        registry.AddCompletedResult(invocation, new ParserRuleResult(null, 1, true));
        registry.AddCompletedResult(invocation, success);

        Assert.IsTrue(registry.TryGetReusableResult(invocation, out var reusable));
        Assert.AreEqual(success, reusable);
        Assert.AreSame(success.ExecutionStateSnapshot, reusable.ExecutionStateSnapshot);
    }

    /// <summary>
    /// A result memoized for one semantic execution-state key is never returned for another key at the same rule,
    /// position and precedence, and each key keeps its own snapshot.
    /// </summary>
    [TestMethod]
    public void TryGetReusableResult_IsIsolatedBySemanticExecutionStateKey()
    {
        var registry = new ParserStateRegistry();
        var stateOne = new RuleInvocationKey("expr", 0, 0, new ParserExecutionStateKey(1));
        var stateTwo = new RuleInvocationKey("expr", 0, 0, new ParserExecutionStateKey(2));
        var resultOne = new ParserRuleResult(CreateNode(), 2, false, "state-one");
        var resultTwo = new ParserRuleResult(null, 0, true, "state-two");

        registry.AddCompletedResult(stateOne, resultOne);
        Assert.IsFalse(registry.TryGetReusableResult(stateTwo, out _));
        registry.AddCompletedResult(stateTwo, resultTwo);

        Assert.IsTrue(registry.TryGetReusableResult(stateOne, out var reusableOne));
        Assert.IsTrue(registry.TryGetReusableResult(stateTwo, out var reusableTwo));
        Assert.AreEqual(resultOne, reusableOne);
        Assert.AreEqual(resultTwo, reusableTwo);
        Assert.AreEqual("state-one", reusableOne.ExecutionStateSnapshot);
        Assert.AreEqual("state-two", reusableTwo.ExecutionStateSnapshot);
    }

    /// <summary>
    /// A result that is neither a success with a node nor a failure (only reachable through direct registry calls) is
    /// stored but never reusable, as in the historical list.
    /// </summary>
    [TestMethod]
    public void TryGetReusableResult_NonReusableOnlyResult_IsNotReused()
    {
        var registry = new ParserStateRegistry();
        var invocation = new RuleInvocationKey("expr", 0, 0);

        Assert.IsTrue(registry.AddCompletedResult(invocation, new ParserRuleResult(null, 2, false)));
        Assert.IsFalse(registry.TryGetReusableResult(invocation, out _));
        Assert.AreEqual(1, registry.GetCompletedResults(invocation).Count);
    }

    /// <summary>Clearing empties every compact collection.</summary>
    [TestMethod]
    public void Clear_EmptiesEveryCollection()
    {
        var registry = new ParserStateRegistry();
        var invocation = new RuleInvocationKey("expr", 0, 0);
        registry.TryEnterState(new ParserStateKey("expr", 0, 1, 1, 0));
        registry.TryEnterState(new ParserStateKey("expr", 0, 1, 2, 0));
        registry.AddContinuation(invocation, new ContinuationKey("start", 0, 0, 0, 0));
        registry.AddCompletedResult(invocation, new ParserRuleResult(null, 0, true));
        registry.AddCompletedResult(invocation, new ParserRuleResult(null, 1, true));

        registry.Clear();

        Assert.AreEqual(0L, registry.VisitedStateCount);
        Assert.AreEqual(0, registry.VisitedStateGroupCount);
        Assert.AreEqual(0, registry.ContinuationCount);
        Assert.AreEqual(0, registry.CompletedInvocationCount);
        Assert.AreEqual(0L, registry.CompletedResultCount);
        Assert.IsTrue(registry.TryEnterState(new ParserStateKey("expr", 0, 1, 2, 0)));
    }

    /// <summary>Creates a distinct parse node instance.</summary>
    /// <returns>The node.</returns>
    private static ParseNode CreateNode() => new ErrorNode(new Utils.Parser.Source.SourceSpan(0, 0), "DEFAULT_MODE", "result");
}
