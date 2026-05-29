using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

[TestClass]
public class ParserLookaheadSharedPrefixDetectorTests
{
    [TestMethod]
    public void Detect_ReturnsEmpty_WhenNoExpectedTokenNames()
    {
        var detector = new ParserLookaheadSharedPrefixDetector();
        var probes = new[]
        {
            new ParserLookaheadProbeResult(ParserLookaheadProbeKind.Unknown, null, null, null),
            new ParserLookaheadProbeResult(ParserLookaheadProbeKind.ImmediateReject, "ID", "x", null)
        };

        var result = detector.Detect(probes);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void Detect_ReturnsEmpty_WhenNoSharedTokens()
    {
        var detector = new ParserLookaheadSharedPrefixDetector();
        var probes = new[]
        {
            ProbeWithExpected(["ID"]),
            ProbeWithExpected(["NUMBER"])
        };

        var result = detector.Detect(probes);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void Detect_GroupsAlternativesBySharedToken()
    {
        var detector = new ParserLookaheadSharedPrefixDetector();
        var probes = new[]
        {
            ProbeWithExpected(["ID"]),
            ProbeWithExpected(["ID"]),
            ProbeWithExpected(["NUMBER"])
        };

        var result = detector.Detect(probes);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("ID", result[0].TokenName);
        CollectionAssert.AreEqual(new[] { 0, 1 }, result[0].AlternativeIndexes.ToArray());
    }

    [TestMethod]
    public void Detect_PreservesStableTokenOrder()
    {
        var detector = new ParserLookaheadSharedPrefixDetector();
        var probes = new[]
        {
            ProbeWithExpected(["B", "A"]),
            ProbeWithExpected(["A"]),
            ProbeWithExpected(["B"])
        };

        var result = detector.Detect(probes);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("B", result[0].TokenName);
        Assert.AreEqual("A", result[1].TokenName);
    }

    [TestMethod]
    public void Detect_DeduplicatesAlternativeIndexPerToken()
    {
        var detector = new ParserLookaheadSharedPrefixDetector();
        var probes = new[]
        {
            ProbeWithExpected(["ID", "ID"]),
            ProbeWithExpected(["ID"])
        };

        var result = detector.Detect(probes);

        Assert.AreEqual(1, result.Count);
        CollectionAssert.AreEqual(new[] { 0, 1 }, result[0].AlternativeIndexes.ToArray());
    }

    [TestMethod]
    public void Detect_IgnoresSingleAlternativeToken()
    {
        var detector = new ParserLookaheadSharedPrefixDetector();
        var probes = new[]
        {
            ProbeWithExpected(["ID"]),
            ProbeWithExpected(["NUMBER"])
        };

        var result = detector.Detect(probes);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void Detect_FromProbeResults_GroupsSharedFirstToken()
    {
        var idRule = new Rule("ID", 0, false, new Alternation([]), Kind: RuleKind.Lexer);
        var exprRule = new Rule("expr", 1, false, new Alternation([]), Kind: RuleKind.Parser);
        var alternatives = new[]
        {
            new Alternative(0, Associativity.Left, new Sequence([new RuleRef("ID"), new LiteralMatch("+"), new RuleRef("expr")]), null),
            new Alternative(1, Associativity.Left, new Sequence([new RuleRef("ID"), new LiteralMatch("-"), new RuleRef("expr")]), null)
        };

        Rule? Resolve(string name) => name switch
        {
            "ID" => idRule,
            "expr" => exprRule,
            _ => null
        };

        var probe = new ParserLookaheadProbe();
        var token = new Token(new SourceSpan(0, 1), "ID", "DEFAULT_MODE", "DEFAULT_CHANNEL", "x");
        var probeResults = alternatives.Select(alternative => probe.Probe(alternative, token, Resolve, false)).ToArray();

        var result = new ParserLookaheadSharedPrefixDetector().Detect(probeResults);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("ID", result[0].TokenName);
        CollectionAssert.AreEqual(new[] { 0, 1 }, result[0].AlternativeIndexes.ToArray());
    }

    private static ParserLookaheadProbeResult ProbeWithExpected(IEnumerable<string> expectedTokenNames)
    {
        return new ParserLookaheadProbeResult(
            ParserLookaheadProbeKind.Unknown,
            null,
            null,
            expectedTokenNames.ToArray());
    }
}
