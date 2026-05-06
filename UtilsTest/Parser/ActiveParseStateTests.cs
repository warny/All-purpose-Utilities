using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Tests for the ActiveParseState infrastructure mapping and identity semantics.
/// </summary>
[TestClass]
public class ActiveParseStateTests
{
    /// <summary>
    /// Ensures conversion from legacy branch representation to active parse state and back preserves data.
    /// </summary>
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

    /// <summary>
    /// Ensures state keys built from equivalent active states are equal and produce the same hash code.
    /// </summary>
    [TestMethod]
    public void ActiveParseState_ToStateKey_EquivalentStatesAreEqual()
    {
        var left = ActiveParseState.FromBranch(CreateBranch(priority: 1, cursorIndex: 0));
        var right = ActiveParseState.FromBranch(CreateBranch(priority: 1, cursorIndex: 0));

        var leftKey = left.ToStateKey(minimumPrecedence: 3);
        var rightKey = right.ToStateKey(minimumPrecedence: 3);

        Assert.AreEqual(leftKey, rightKey);
        Assert.AreEqual(leftKey.GetHashCode(), rightKey.GetHashCode());
    }

    /// <summary>
    /// Ensures state key identity changes when alternative priority, cursor index, or precedence differs.
    /// </summary>
    [TestMethod]
    public void ActiveParseState_ToStateKey_DifferentDimensionsProduceDifferentKeys()
    {
        var baseline = ActiveParseState.FromBranch(CreateBranch(priority: 1, cursorIndex: 0)).ToStateKey(minimumPrecedence: 2);
        var differentAlternative = ActiveParseState.FromBranch(CreateBranch(priority: 2, cursorIndex: 0)).ToStateKey(minimumPrecedence: 2);
        var differentCursor = ActiveParseState.FromBranch(CreateBranch(priority: 1, cursorIndex: 1)).ToStateKey(minimumPrecedence: 2);
        var differentPrecedence = ActiveParseState.FromBranch(CreateBranch(priority: 1, cursorIndex: 0)).ToStateKey(minimumPrecedence: 4);

        Assert.AreNotEqual(baseline, differentAlternative);
        Assert.AreNotEqual(baseline, differentCursor);
        Assert.AreNotEqual(baseline, differentPrecedence);
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
