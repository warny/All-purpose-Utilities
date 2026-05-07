using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

[TestClass]
public class ParserLookaheadProbeTests
{
    [TestMethod]
    public void Probe_LiteralMatch_MatchingToken_ReturnsRequiresParse()
    {
        var result = new ParserLookaheadProbe().Probe(CreateAlternative(new LiteralMatch("if")), TokenWithText("if"), static _ => null, false);
        Assert.AreEqual(ParserLookaheadProbeKind.RequiresParse, result.Kind);
    }

    [TestMethod]
    public void Probe_LiteralMatch_NonMatchingToken_ReturnsImmediateReject()
    {
        var result = new ParserLookaheadProbe().Probe(CreateAlternative(new LiteralMatch("if")), TokenWithText("else"), static _ => null, false);
        Assert.AreEqual(ParserLookaheadProbeKind.ImmediateReject, result.Kind);
    }

    [TestMethod]
    public void Probe_LiteralMatch_NullToken_ReturnsImmediateReject()
    {
        var result = new ParserLookaheadProbe().Probe(CreateAlternative(new LiteralMatch("if")), null, static _ => null, false);
        Assert.AreEqual(ParserLookaheadProbeKind.ImmediateReject, result.Kind);
    }

    [TestMethod]
    public void Probe_LiteralMatch_RespectsCaseInsensitive()
    {
        var result = new ParserLookaheadProbe().Probe(CreateAlternative(new LiteralMatch("if")), TokenWithText("IF"), static _ => null, true);
        Assert.AreEqual(ParserLookaheadProbeKind.RequiresParse, result.Kind);
    }

    [TestMethod]
    public void Probe_LexerRuleRef_MatchingTokenRule_ReturnsRequiresParse()
    {
        Rule? Resolve(string name) => name == "ID" ? new Rule("ID", 0, false, new Alternation([])) { Kind = RuleKind.Lexer } : null;
        var result = new ParserLookaheadProbe().Probe(CreateAlternative(new RuleRef("ID")), TokenWithRule("ID"), Resolve, false);
        Assert.AreEqual(ParserLookaheadProbeKind.RequiresParse, result.Kind);
    }

    [TestMethod]
    public void Probe_LexerRuleRef_NonMatchingTokenRule_ReturnsImmediateReject()
    {
        Rule? Resolve(string name) => name == "ID" ? new Rule("ID", 0, false, new Alternation([])) { Kind = RuleKind.Lexer } : null;
        var result = new ParserLookaheadProbe().Probe(CreateAlternative(new RuleRef("ID")), TokenWithRule("NUMBER"), Resolve, false);
        Assert.AreEqual(ParserLookaheadProbeKind.ImmediateReject, result.Kind);
    }

    [TestMethod]
    public void Probe_ParserRuleRef_ReturnsUnknown()
    {
        Rule? Resolve(string name) => name == "expr" ? new Rule("expr", 0, false, new Alternation([])) { Kind = RuleKind.Parser } : null;
        var result = new ParserLookaheadProbe().Probe(CreateAlternative(new RuleRef("expr")), TokenWithRule("ID"), Resolve, false);
        Assert.AreEqual(ParserLookaheadProbeKind.Unknown, result.Kind);
    }

    [TestMethod]
    public void Probe_ParserRuleRef_NullToken_ReturnsUnknown()
    {
        // Parser rules may accept empty input, so a null token must not produce ImmediateReject.
        Rule? Resolve(string name) => name == "opt" ? new Rule("opt", 0, false, new Alternation([])) { Kind = RuleKind.Parser } : null;
        var result = new ParserLookaheadProbe().Probe(CreateAlternative(new RuleRef("opt")), null, Resolve, false);
        Assert.AreEqual(ParserLookaheadProbeKind.Unknown, result.Kind);
    }

    [TestMethod]
    public void Probe_LexerRuleRef_NullToken_ReturnsImmediateReject()
    {
        // A lexer rule always requires a token; EOF is a definitive reject.
        Rule? Resolve(string name) => name == "ID" ? new Rule("ID", 0, false, new Alternation([])) { Kind = RuleKind.Lexer } : null;
        var result = new ParserLookaheadProbe().Probe(CreateAlternative(new RuleRef("ID")), null, Resolve, false);
        Assert.AreEqual(ParserLookaheadProbeKind.ImmediateReject, result.Kind);
    }

    [TestMethod]
    public void Probe_Sequence_UsesFirstMeaningfulItem()
    {
        var sequence = new Sequence([
            new EmbeddedAction("x", ActionContext.Alternative, ActionPosition.Inline, []),
            new LexerCommand(LexerCommandType.Skip, null),
            new LiteralMatch("go")
        ]);
        var result = new ParserLookaheadProbe().Probe(CreateAlternative(sequence), TokenWithText("stop"), static _ => null, false);
        Assert.AreEqual(ParserLookaheadProbeKind.ImmediateReject, result.Kind);
    }

    [TestMethod]
    public void Probe_NestedAlternation_ReturnsUnknown()
    {
        var nested = new Alternation([new Alternative(0, Associativity.Left, new LiteralMatch("x"), null)]);
        var result = new ParserLookaheadProbe().Probe(CreateAlternative(nested), TokenWithText("x"), static _ => null, false);
        Assert.AreEqual(ParserLookaheadProbeKind.Unknown, result.Kind);
    }

    private static Alternative CreateAlternative(RuleContent content) => new(0, Associativity.Left, content, null);

    private static Token TokenWithText(string text) => new(new SourceSpan(0, text.Length), "ID", "DEFAULT_MODE", "DEFAULT_CHANNEL", text);

    private static Token TokenWithRule(string ruleName) => new(new SourceSpan(0, 1), ruleName, "DEFAULT_MODE", "DEFAULT_CHANNEL", "t");
}
