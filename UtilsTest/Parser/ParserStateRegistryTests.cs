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
    /// Ensures successful completed results are reusable for the same invocation key.
    /// </summary>
    [TestMethod]
    public void Registry_Memoization_ReusableSuccess_IsReturned()
    {
        var registry = new ParserStateRegistry();
        var invocation = new RuleInvocationKey("expr", 0, 0);
        var node = new ErrorNode(new SourceSpan(0, 0), "DEFAULT_MODE", "test");

        registry.AddCompletedResult(invocation, new ParserRuleResult(node, 4, false));

        Assert.IsTrue(registry.TryGetReusableSuccess(invocation, out var reusable));
        Assert.AreEqual(4, reusable.EndPosition);
        Assert.AreSame(node, reusable.Node);
    }

    /// <summary>
    /// Ensures failure-only invocations are not considered reusable successes.
    /// </summary>
    [TestMethod]
    public void Registry_Memoization_FailureOnly_IsNotReusable()
    {
        var registry = new ParserStateRegistry();
        var invocation = new RuleInvocationKey("expr", 0, 0);

        registry.AddCompletedResult(invocation, new ParserRuleResult(null, 0, true));

        Assert.IsFalse(registry.TryGetReusableSuccess(invocation, out _));
    }
}
