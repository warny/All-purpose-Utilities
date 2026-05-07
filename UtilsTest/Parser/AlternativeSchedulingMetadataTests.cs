using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

[TestClass]
public class AlternativeSchedulingMetadataTests
{
    [TestMethod]
    public void Scheduler_ProducesEmptyMetadata_WhenNoSharedPrefixes()
    {
        var result = RunWithExpectedTokens([ ["ID"], ["NUMBER"] ]);
        Assert.AreEqual(0, result.Metadata.SharedPrefixPlans.Count);
    }

    [TestMethod]
    public void Scheduler_ProducesSharedPrefixPlans_ForSharedFirstTokens()
    {
        var result = RunWithExpectedTokens([ ["ID"], ["ID"] ]);
        Assert.AreEqual(1, result.Metadata.SharedPrefixPlans.Count);
        var plan = result.Metadata.SharedPrefixPlans[0];
        Assert.AreEqual("ID", plan.SharedTokenName);
        CollectionAssert.AreEqual(new[] { 0, 1 }, plan.AlternativeIndexes.ToArray());
    }

    [TestMethod]
    public void Scheduler_Metadata_DoesNotChangeAlternativeOrdering()
    {
        var result = RunWithExpectedTokens([ ["ID"], ["ID"] ]);
        CollectionAssert.AreEqual(new[] { 0, 1 }, result.CompletedStates.Select(s => s.AlternativeIndex).ToArray());
    }

    [TestMethod]
    public void Scheduler_Metadata_UsesExpectedTokenNames()
    {
        var result = RunWithExpectedTokens([ ["ID"], ["NUMBER", "ID"] ]);
        Assert.AreEqual("ID", result.Metadata.SharedPrefixPlans[0].SharedTokenName);
    }

    [TestMethod]
    public void Scheduler_Metadata_CreatesContinuationDescriptors()
    {
        var result = RunWithExpectedTokens([ ["ID"], ["ID"] ]);
        var continuations = result.Metadata.SharedPrefixPlans[0].Continuations;
        Assert.AreEqual(2, continuations.Count);
        Assert.IsTrue(continuations.All(c => c.Key.SequencePosition == 0));
    }

    private static AlternativeSchedulingResult RunWithExpectedTokens(IReadOnlyList<IReadOnlyList<string>> expected)
    {
        var scheduler = new AlternativeScheduler();
        var alternatives = new[]
        {
            new Alternative(0, Associativity.Left, new RuleRef("ID"), "a"),
            new Alternative(1, Associativity.Left, new RuleRef("ID"), "b")
        };
        var rule = new Rule("expr", 0, false, new Alternation(alternatives));
        return scheduler.Run(rule, alternatives, 0, 0, null,
            parseAlternative: (alternative, index) =>
                (CreateState(rule, alternative, index), new ParserLookaheadProbeResult(ParserLookaheadProbeKind.RequiresParse, null, null, expected[index])));
    }

    private static ActiveParseState CreateState(Rule rule, Alternative alternative, int index)
    {
        return new ActiveParseState
        {
            Rule = rule,
            Alternative = alternative,
            OriginInputPosition = 0,
            CurrentInputPosition = 1,
            AlternativeIndex = index,
            Cursor = new RuleContentCursor { Index = 0, Kind = "alternative-root" },
            PartialNode = new ParserNode(new SourceSpan(0, 1), "DEFAULT_MODE", rule, []),
            EndPosition = 1,
            Status = ActiveParseStateStatus.Completed,
            ParentStateKey = null,
            Depth = 0,
            Continuation = null
        };
    }
}
