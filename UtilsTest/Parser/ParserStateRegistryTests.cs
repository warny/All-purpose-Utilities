using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Tests parser state key semantics and registry behavior used by ParserEngine.
/// </summary>
[TestClass]
public class ParserStateRegistryTests
{
    /// <summary>
    /// Ensures parser state keys compare by value for duplicate detection.
    /// </summary>
    [TestMethod]
    public void ParserStateKey_Equality_IsValueBased()
    {
        var left = new ParserStateKey("expr", 5, 1, 2, 0);
        var right = new ParserStateKey("expr", 5, 1, 2, 0);

        Assert.AreEqual(left, right);
        Assert.AreEqual(left.GetHashCode(), right.GetHashCode());
    }

    /// <summary>
    /// Ensures duplicate parser states are rejected by the registry.
    /// </summary>
    [TestMethod]
    public void Registry_DuplicateState_IsRejected()
    {
        var registry = new ParserStateRegistry();
        var state = new ParserStateKey("expr", 2, 0, 0, 0);

        Assert.IsTrue(registry.TryEnterState(state));
        Assert.IsFalse(registry.TryEnterState(state));
    }

    /// <summary>
    /// Ensures clearing removes visited states, continuations, and completed results.
    /// </summary>
    [TestMethod]
    public void Clear_RemovesVisitedStatesContinuationsAndCompletedResults()
    {
        var registry = new ParserStateRegistry();
        var parserState = new ParserStateKey("expr", 2, 0, 0, 0);
        var invocation = new RuleInvocationKey("expr", 0, 0);
        var continuation = new ContinuationKey("root", 0, 1, 2, 0);

        registry.TryEnterState(parserState);
        registry.AddContinuation(invocation, continuation);
        registry.AddCompletedResult(invocation, new ParserRuleResult(null, 0, true));

        registry.Clear();

        Assert.IsTrue(registry.TryEnterState(parserState));
        Assert.AreEqual(0, registry.GetContinuations(invocation).Count);
        Assert.AreEqual(0, registry.GetCompletedResults(invocation).Count);
        Assert.IsFalse(registry.TryGetReusableResult(invocation, out _));
    }

    /// <summary>
    /// Ensures continuation metadata does not create reusable completed results.
    /// </summary>
    [TestMethod]
    public void Registry_ContinuationMetadata_DoesNotCreateReusableResult()
    {
        var registry = new ParserStateRegistry();
        var invocation = new RuleInvocationKey("expr", 0, 0);

        registry.AddContinuation(invocation, new ContinuationKey("root", 0, 1, 2, 0));

        Assert.IsFalse(registry.TryGetReusableResult(invocation, out _));
        Assert.AreEqual(0, registry.GetCompletedResults(invocation).Count);
    }

    /// <summary>
    /// Ensures completed results do not create continuation metadata.
    /// </summary>
    [TestMethod]
    public void Registry_CompletedResult_DoesNotCreateContinuationMetadata()
    {
        var registry = new ParserStateRegistry();
        var invocation = new RuleInvocationKey("expr", 0, 0);

        registry.AddCompletedResult(invocation, new ParserRuleResult(null, 0, true));

        Assert.AreEqual(0, registry.GetContinuations(invocation).Count);
    }

    /// <summary>
    /// Ensures continuations are grouped by invocation key.
    /// </summary>
    [TestMethod]
    public void Registry_ContinuationMetadata_IsGroupedByInvocationKey()
    {
        var registry = new ParserStateRegistry();
        var invocationOne = new RuleInvocationKey("expr", 0, 0);
        var invocationTwo = new RuleInvocationKey("expr", 1, 0);
        var continuationOne = new ContinuationKey("root", 0, 1, 2, 0);
        var continuationTwo = new ContinuationKey("root", 1, 0, 3, 0);

        registry.AddContinuation(invocationOne, continuationOne);
        registry.AddContinuation(invocationTwo, continuationTwo);

        CollectionAssert.Contains(registry.GetContinuations(invocationOne).ToArray(), continuationOne);
        CollectionAssert.DoesNotContain(registry.GetContinuations(invocationOne).ToArray(), continuationTwo);
        CollectionAssert.Contains(registry.GetContinuations(invocationTwo).ToArray(), continuationTwo);
    }

    /// <summary>
    /// Ensures completed results are grouped by invocation key.
    /// </summary>
    [TestMethod]
    public void Registry_CompletedResults_AreGroupedByInvocationKey()
    {
        var registry = new ParserStateRegistry();
        var invocationOne = new RuleInvocationKey("expr", 0, 0);
        var invocationTwo = new RuleInvocationKey("expr", 1, 0);
        var nodeOne = new ErrorNode(new SourceSpan(0, 0), "DEFAULT_MODE", "one");
        var nodeTwo = new ErrorNode(new SourceSpan(1, 0), "DEFAULT_MODE", "two");
        var resultOne = new ParserRuleResult(nodeOne, 1, false);
        var resultTwo = new ParserRuleResult(nodeTwo, 2, false);

        registry.AddCompletedResult(invocationOne, resultOne);
        registry.AddCompletedResult(invocationTwo, resultTwo);

        CollectionAssert.Contains(registry.GetCompletedResults(invocationOne).ToArray(), resultOne);
        CollectionAssert.DoesNotContain(registry.GetCompletedResults(invocationOne).ToArray(), resultTwo);
        CollectionAssert.Contains(registry.GetCompletedResults(invocationTwo).ToArray(), resultTwo);
    }

    /// <summary>
    /// Ensures successful completed results are reusable for the same invocation key.
    /// </summary>
    [TestMethod]
    public void Registry_Memoization_ReusableSuccess_IsReturnedFirst()
    {
        var registry = new ParserStateRegistry();
        var invocation = new RuleInvocationKey("expr", 0, 0);
        var node = new ErrorNode(new SourceSpan(0, 0), "DEFAULT_MODE", "test");

        registry.AddCompletedResult(invocation, new ParserRuleResult(node, 4, false));

        registry.AddCompletedResult(invocation, new ParserRuleResult(null, 0, true));

        Assert.IsTrue(registry.TryGetReusableResult(invocation, out var reusable));
        Assert.AreEqual(4, reusable.EndPosition);
        Assert.AreSame(node, reusable.Node);
        Assert.IsFalse(reusable.IsFailure);
    }

    /// <summary>
    /// Ensures first reusable success wins before reusable failures.
    /// </summary>
    [TestMethod]
    public void Registry_Memoization_FirstReusableSuccessWinsBeforeFailure()
    {
        var registry = new ParserStateRegistry();
        var invocation = new RuleInvocationKey("expr", 0, 0);
        var successNode = new ErrorNode(new SourceSpan(0, 0), "DEFAULT_MODE", "success");

        registry.AddCompletedResult(invocation, new ParserRuleResult(null, 0, true));
        registry.AddCompletedResult(invocation, new ParserRuleResult(successNode, 5, false));

        Assert.IsTrue(registry.TryGetReusableResult(invocation, out var reusable));
        Assert.IsFalse(reusable.IsFailure);
        Assert.AreSame(successNode, reusable.Node);
        Assert.AreEqual(5, reusable.EndPosition);
    }

    /// <summary>
    /// Ensures first reusable failure wins when no reusable success exists.
    /// </summary>
    [TestMethod]
    public void Registry_Memoization_FirstReusableFailureWinsWhenNoSuccessExists()
    {
        var registry = new ParserStateRegistry();
        var invocation = new RuleInvocationKey("expr", 0, 0);

        registry.AddCompletedResult(invocation, new ParserRuleResult(null, 1, true));
        registry.AddCompletedResult(invocation, new ParserRuleResult(null, 3, true));

        Assert.IsTrue(registry.TryGetReusableResult(invocation, out var reusable));
        Assert.IsTrue(reusable.IsFailure);
        Assert.AreEqual(1, reusable.EndPosition);
    }

    /// <summary>
    /// Ensures duplicate completed results are rejected.
    /// </summary>
    [TestMethod]
    public void Registry_Memoization_DuplicateCompletedResult_IsRejected()
    {
        var registry = new ParserStateRegistry();
        var invocation = new RuleInvocationKey("expr", 0, 0);
        var node = new ErrorNode(new SourceSpan(0, 0), "DEFAULT_MODE", "same");
        var result = new ParserRuleResult(node, 2, false);

        Assert.IsTrue(registry.AddCompletedResult(invocation, result));
        Assert.IsFalse(registry.AddCompletedResult(invocation, result));
        Assert.AreEqual(1, registry.GetCompletedResults(invocation).Count);
    }

    /// <summary>
    /// Ensures completed results with different end positions are distinct.
    /// </summary>
    [TestMethod]
    public void Registry_Memoization_DifferentEndPosition_IsDistinctCompletion()
    {
        var registry = new ParserStateRegistry();
        var invocation = new RuleInvocationKey("expr", 0, 0);
        var node = new ErrorNode(new SourceSpan(0, 0), "DEFAULT_MODE", "same-node");

        Assert.IsTrue(registry.AddCompletedResult(invocation, new ParserRuleResult(node, 2, false)));
        Assert.IsTrue(registry.AddCompletedResult(invocation, new ParserRuleResult(node, 3, false)));
        Assert.AreEqual(2, registry.GetCompletedResults(invocation).Count);
    }

    /// <summary>
    /// Ensures completed results with different parse-node references are distinct.
    /// </summary>
    [TestMethod]
    public void Registry_Memoization_DifferentNodeReference_IsDistinctCompletion()
    {
        var registry = new ParserStateRegistry();
        var invocation = new RuleInvocationKey("expr", 0, 0);
        var nodeOne = new ErrorNode(new SourceSpan(0, 0), "DEFAULT_MODE", "node-1");
        var nodeTwo = new ErrorNode(new SourceSpan(0, 0), "DEFAULT_MODE", "node-1");

        Assert.IsTrue(registry.AddCompletedResult(invocation, new ParserRuleResult(nodeOne, 2, false)));
        Assert.IsTrue(registry.AddCompletedResult(invocation, new ParserRuleResult(nodeTwo, 2, false)));
        Assert.AreEqual(2, registry.GetCompletedResults(invocation).Count);
    }

    /// <summary>
    /// Ensures failure-only invocations return reusable failures.
    /// </summary>
    [TestMethod]
    public void Registry_Memoization_FailureOnly_ReturnsReusableFailure()
    {
        var registry = new ParserStateRegistry();
        var invocation = new RuleInvocationKey("expr", 0, 0);

        registry.AddCompletedResult(invocation, new ParserRuleResult(null, 0, true));

        Assert.IsTrue(registry.TryGetReusableResult(invocation, out var reusable));
        Assert.IsTrue(reusable.IsFailure);
        Assert.IsNull(reusable.Node);
    }

    /// <summary>
    /// Ensures missing invocations are not reusable.
    /// </summary>
    [TestMethod]
    public void Registry_Memoization_MissingInvocation_ReturnsFalse()
    {
        var registry = new ParserStateRegistry();
        var invocation = new RuleInvocationKey("expr", 0, 0);

        Assert.IsFalse(registry.TryGetReusableResult(invocation, out _));
    }
}
