using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Tests for the ActiveParseState infrastructure mapping.
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
        var alternative = new Alternative(2, Associativity.Left, new LiteralMatch("A"), "altLabel");
        var rule = new Rule(
            "sampleRule",
            0,
            false,
            new Alternation([alternative]));

        var branch = new ParseBranch
        {
            Rule = rule,
            Alternative = alternative,
            InputPosition = 4,
            Cursor = new RuleContentCursor { Index = 1, Kind = "alternative-root" },
            PartialNode = new ParserNode(new SourceSpan(2, 3), "DEFAULT_MODE", rule, []),
            EndPosition = 7,
            IsComplete = true
        };

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
}
