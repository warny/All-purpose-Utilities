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
            context,
            alternatives,
            rule,
            minimumPrecedence: 3,
            diagnostics: null,
            checkPrecedence: static (_, _) => true,
            tryParseAlternative: (_, index) => index == 0 || index == 1 ? CreateNode(rule, 2) : null,
            registerVisitedState: _ => { },
            onRepeatedState: (_, _, _) => { },
            onBacktracking: () => { });

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
            context,
            rule.Content.Alternatives,
            rule,
            minimumPrecedence: 1,
            diagnostics,
            checkPrecedence: static (_, _) => true,
            tryParseAlternative: (_, _) => CreateNode(rule, 4),
            registerVisitedState: _ => { },
            onRepeatedState: (_, _, _) => { },
            onBacktracking: () => { });

        Assert.AreEqual(2, result.CompletedStates.Count);
        Assert.AreEqual(1, result.PrunedStates.Count);
        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.AmbiguousAlternativesPruned.Code));
    }

    [TestMethod]
    public void Run_UsesMinimumPrecedenceAndBacktrackingCallback()
    {
        var scheduler = new AlternativeScheduler();
        var (context, rule, alternatives) = CreateAlternatives();
        int callbackCount = 0;
        int precedenceSeen = -1;

        var result = scheduler.Run(
            context,
            alternatives,
            rule,
            minimumPrecedence: 7,
            diagnostics: null,
            checkPrecedence: (_, precedence) =>
            {
                precedenceSeen = precedence;
                return true;
            },
            tryParseAlternative: (_, index) => index == 0 ? null : CreateNode(rule, 5),
            registerVisitedState: _ => { },
            onRepeatedState: (_, _, _) => { },
            onBacktracking: () => callbackCount++);

        Assert.AreEqual(7, precedenceSeen);
        Assert.AreEqual(1, callbackCount);
        Assert.IsNotNull(result.SelectedState);
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
}
