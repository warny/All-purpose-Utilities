using Microsoft.VisualStudio.TestTools.UnitTesting;
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

        var result = scheduler.Run(context, alternatives, rule, 0, null, static (_, _) => true, (alternative, index) => index == 0 || index == 1 ? CreateNode(rule, 2) : null, _ => { }, (_, _, _) => { }, () => { });

        Assert.AreEqual(1, result.CompletedStates.Count);
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
