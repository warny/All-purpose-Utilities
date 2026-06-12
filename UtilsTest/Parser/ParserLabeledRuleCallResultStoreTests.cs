using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Verifies immutable child-return snapshots and rollback-friendly assignment/list labeled result storage.
/// </summary>
[TestClass]
public sealed class ParserLabeledRuleCallResultStoreTests
{
    /// <summary>
    /// Verifies empty returns and ordinal present-null lookup semantics.
    /// </summary>
    [TestMethod]
    public void ParserRuleCallResult_ReturnLookup_DistinguishesAbsentAndPresentNull()
    {
        var empty = CreateResult("child");
        var presentNull = CreateResult("child", new Dictionary<string, object?> { ["value"] = null });

        Assert.AreEqual(0, empty.Returns.Count);
        Assert.IsFalse(empty.TryGetReturn("value", out _));
        Assert.IsTrue(presentNull.TryGetReturn("value", out object? value));
        Assert.IsNull(value);
        Assert.ThrowsException<ArgumentNullException>(() => presentNull.TryGetReturn(null!, out _));
    }

    /// <summary>
    /// Verifies completed return dictionaries are immutable snapshots unaffected by source mutation.
    /// </summary>
    [TestMethod]
    public void ParserRuleCallResult_FromFrame_CapturesImmutableReturnSnapshot()
    {
        var frame = new ParserRuleInvocationFrame("child", 0);
        frame.SetReturnValue("first", 1);
        frame.SetReturnValue("second", null);
        ParserRuleCallResult result = ParserRuleCallResult.FromFrame(frame);

        frame.SetReturnValue("first", 99);
        frame.SetReturnValue("third", 3);

        Assert.AreEqual(2, result.Returns.Count);
        Assert.AreEqual(1, result.Returns["first"]);
        Assert.IsTrue(result.TryGetReturn("second", out object? second));
        Assert.IsNull(second);
        Assert.IsFalse(result.Returns.ContainsKey("third"));
        Assert.ThrowsException<NotSupportedException>(() => ((IDictionary<string, object?>)result.Returns).Add("fourth", 4));
    }

    /// <summary>
    /// Verifies direct call-result initialization snapshots a mutable return dictionary.
    /// </summary>
    [TestMethod]
    public void ParserRuleCallResult_ReturnInitializer_CopiesMutableDictionary()
    {
        var source = new Dictionary<string, object?> { ["value"] = 42 };
        var result = new ParserRuleCallResult
        {
            RuleName = "child",
            InputPosition = 0,
            Depth = 1,
            Returns = source,
        };

        source["value"] = 99;
        source["other"] = 1;

        Assert.AreEqual(42, result.Returns["value"]);
        Assert.IsFalse(result.Returns.ContainsKey("other"));
    }

    /// <summary>
    /// Verifies assignment replacement, list ordering, unrelated-label preservation, and immutable prior snapshots.
    /// </summary>
    [TestMethod]
    public void Store_AssignmentAndListUpdates_AreImmutableAndOrdered()
    {
        ParserRuleCallResult first = CreateResult("first");
        ParserRuleCallResult second = CreateResult("second");
        ParserLabeledRuleCallResultStore initial = ParserLabeledRuleCallResultStore.Empty;
        ParserLabeledRuleCallResultStore assigned = initial.SetAssignment("x", first).SetAssignment("other", second);
        ParserLabeledRuleCallResultStore overwritten = assigned.SetAssignment("x", second);
        ParserLabeledRuleCallResultStore listed = overwritten.AppendList("xs", first).AppendList("xs", second);

        Assert.IsFalse(initial.TryGetAssignment("x", out _));
        Assert.AreEqual(0, initial.GetList("xs").Count);
        Assert.AreEqual(0, assigned.GetList("xs").Count);
        Assert.IsTrue(overwritten.TryGetAssignment("x", out ParserRuleCallResult assignment));
        Assert.AreSame(second, assignment);
        Assert.IsTrue(listed.TryGetAssignment("other", out ParserRuleCallResult other));
        Assert.AreSame(second, other);
        CollectionAssert.AreEqual(new[] { first, second }, listed.GetList("xs").ToArray());
        Assert.AreEqual(0, overwritten.GetList("xs").Count, "Earlier snapshots must not observe later appends.");
        Assert.ThrowsException<NotSupportedException>(() => ((IList<ParserRuleCallResult>)listed.GetList("xs")).Add(first));
    }

    /// <summary>
    /// Verifies manager binding uses the final label kind and reports accurately when no active frame exists.
    /// </summary>
    [TestMethod]
    public void StackManager_BindLastCompletedResult_StoresAssignmentAndListOnlyWithActiveFrame()
    {
        var manager = new StackParserRuleInvocationFrameManager();
        Assert.IsFalse(manager.TryBindLastCompletedChildCallToCurrentLabel());
        ParserRuleInvocationFrame parent = manager.Enter("start", 0);
        parent.LastCompletedChildCall = CreateResult("child", labelName: "x", labelKind: ParserRuleReferenceLabelKind.Assignment);

        Assert.IsTrue(manager.TryBindLastCompletedChildCallToCurrentLabel());
        Assert.IsTrue(parent.LabeledCallResults.TryGetAssignment("x", out _));

        parent.LastCompletedChildCall = CreateResult("child", labelName: "xs", labelKind: ParserRuleReferenceLabelKind.List);
        Assert.IsTrue(manager.TryBindLastCompletedChildCallToCurrentLabel());
        Assert.AreEqual(1, parent.LabeledCallResults.GetList("xs").Count);
    }

    /// <summary>
    /// Verifies deterministic store hashing includes assignment values, list order, list length, and present-null keys.
    /// </summary>
    [TestMethod]
    public void StoreHash_ReflectsObservableLabeledResultState()
    {
        ParserRuleCallResult one = CreateResult("child", new Dictionary<string, object?> { ["value"] = 1 });
        ParserRuleCallResult two = CreateResult("child", new Dictionary<string, object?> { ["value"] = 2 });
        ParserRuleCallResult absent = CreateResult("child");
        ParserRuleCallResult presentNull = CreateResult("child", new Dictionary<string, object?> { ["value"] = null });

        Assert.AreEqual(
            ParserLabeledRuleCallResultStore.Empty.SetAssignment("x", one).GetParserExecutionStateHash(),
            ParserLabeledRuleCallResultStore.Empty.SetAssignment("x", one).GetParserExecutionStateHash());
        Assert.AreNotEqual(
            ParserLabeledRuleCallResultStore.Empty.SetAssignment("x", one).GetParserExecutionStateHash(),
            ParserLabeledRuleCallResultStore.Empty.SetAssignment("x", two).GetParserExecutionStateHash());
        Assert.AreNotEqual(
            ParserLabeledRuleCallResultStore.Empty.AppendList("xs", one).AppendList("xs", two).GetParserExecutionStateHash(),
            ParserLabeledRuleCallResultStore.Empty.AppendList("xs", two).AppendList("xs", one).GetParserExecutionStateHash());
        Assert.AreNotEqual(
            ParserLabeledRuleCallResultStore.Empty.AppendList("xs", one).GetParserExecutionStateHash(),
            ParserLabeledRuleCallResultStore.Empty.AppendList("xs", one).AppendList("xs", two).GetParserExecutionStateHash());
        Assert.AreNotEqual(absent.GetParserExecutionStateHash(), presentNull.GetParserExecutionStateHash());
    }

    /// <summary>
    /// Verifies unsupported arbitrary return objects conservatively produce volatile state hashes.
    /// </summary>
    [TestMethod]
    public void ParserRuleCallResultHash_ArbitraryObject_IsVolatile()
    {
        ParserRuleCallResult result = CreateResult("child", new Dictionary<string, object?> { ["value"] = new object() });

        Assert.AreNotEqual(result.GetParserExecutionStateHash(), result.GetParserExecutionStateHash());
    }

    /// <summary>
    /// Creates a call result for store tests.
    /// </summary>
    /// <param name="ruleName">Producing rule name.</param>
    /// <param name="returns">Optional immutable return source.</param>
    /// <param name="labelName">Optional call-site label.</param>
    /// <param name="labelKind">Call-site label kind.</param>
    /// <returns>A test call result.</returns>
    private static ParserRuleCallResult CreateResult(
        string ruleName,
        IReadOnlyDictionary<string, object?>? returns = null,
        string? labelName = null,
        ParserRuleReferenceLabelKind labelKind = ParserRuleReferenceLabelKind.None)
    {
        var frame = new ParserRuleInvocationFrame(ruleName, 0);
        if (returns is not null)
        {
            foreach (KeyValuePair<string, object?> item in returns)
            {
                frame.SetReturnValue(item.Key, item.Value);
            }
        }

        return ParserRuleCallResult.FromFrame(frame).WithLabel(labelName, labelKind);
    }
}
