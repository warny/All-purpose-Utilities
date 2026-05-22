using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

[TestClass]
public class ContinuationMetadataPreparationTests
{
    [TestMethod]
    public void Prepare_SharedPrefix_IgnoresActionsAndLexerCommands()
    {
        var rule = new Rule("expr", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new Sequence([new EmbeddedAction("{a}"), new LexerCommand("skip"), new RuleRef("ID")]), "A"),
            new Alternative(1, Associativity.Left, new Sequence([new RuleRef("ID"), new RuleRef("NUMBER")]), "B")
        ]));
        var ordered = rule.Content.Alternatives.OrderBy(static a => a.Priority).ToArray();
        var probes = new[]
        {
            new ParserLookaheadProbeResult(ParserLookaheadProbeKind.RequiresParse, "ID", "id", ["ID"]),
            new ParserLookaheadProbeResult(ParserLookaheadProbeKind.RequiresParse, "ID", "id", ["ID"])
        };
        var candidates = new[] { new ParserLookaheadSharedPrefixCandidate("ID", [0, 1]) };

        var preparation = new ContinuationMetadataPreparation();
        var descriptors = preparation.Prepare(rule, ordered, probes, candidates);

        Assert.AreEqual(2, descriptors.Count);
        Assert.AreEqual(1, descriptors[0].Key.SequencePosition);
        Assert.AreEqual(1, descriptors[1].Key.SequencePosition);
        Assert.IsTrue(descriptors.All(static d => d.Category == ParserContinuationCategory.SharedPrefixCandidate));
    }
}
