using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

[TestClass]
public class SchedulingPreparationTests
{
    [TestMethod]
    public void Prepare_ReturnsDeterministicInputs()
    {
        var (rule, orderedAlternatives) = BuildRuleAndOrderedAlternatives();
        var preparation = new SchedulingPreparation();

        var first = Prepare(preparation, rule, orderedAlternatives);
        var second = Prepare(preparation, rule, orderedAlternatives);

        AssertStructuralDescriptorsEqual(first.StructuralDescriptors, second.StructuralDescriptors);
        CollectionAssert.AreEqual(first.LookaheadProbes.ToArray(), second.LookaheadProbes.ToArray());
        CollectionAssert.AreEqual(first.SharedPrefixCandidates.ToArray(), second.SharedPrefixCandidates.ToArray());
        CollectionAssert.AreEqual(first.ContinuationDescriptors.ToArray(), second.ContinuationDescriptors.ToArray());
    }

    [TestMethod]
    public void Prepare_DoesNotModifyGrammar()
    {
        var (rule, orderedAlternatives) = BuildRuleAndOrderedAlternatives();
        var snapshot = rule.Content.Alternatives.Select(static alternative => alternative.Content.ToString()).ToArray();

        var preparation = new SchedulingPreparation();
        _ = Prepare(preparation, rule, orderedAlternatives);

        var after = rule.Content.Alternatives.Select(static alternative => alternative.Content.ToString()).ToArray();
        CollectionAssert.AreEqual(snapshot, after);
    }

    [TestMethod]
    public void Prepare_DoesNotExecuteScheduler()
    {
        var members = typeof(SchedulingPreparation).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Select(static field => field.FieldType)
            .ToArray();

        Assert.IsFalse(members.Any(static type => type == typeof(AlternativeScheduler)));
    }

    [TestMethod]
    public void Prepare_DoesNotRequireExecution()
    {
        var (rule, orderedAlternatives) = BuildRuleAndOrderedAlternatives();
        var preparation = new SchedulingPreparation();

        var prepared = Prepare(preparation, rule, orderedAlternatives);

        Assert.AreEqual(2, prepared.StructuralDescriptors.Count);
        Assert.AreEqual(2, prepared.LookaheadProbes.Count);
    }

    [TestMethod]
    public void Prepare_ProducesEquivalentInputsToCurrentFlow()
    {
        var (rule, orderedAlternatives) = BuildRuleAndOrderedAlternatives();
        var parseContext = new ParseContext([new Token(new SourceSpan(0, 2), "ID", "DEFAULT_MODE", "DEFAULT_CHANNEL", "id")]);
        var lookaheadCache = new ParserLookaheadCache();
        var lookaheadProbe = new ParserLookaheadProbe();
        var sharedPrefixDetector = new ParserLookaheadSharedPrefixDetector();
        var continuationPreparation = new ContinuationMetadataPreparation();
        var structuralExtractor = new AlternativeStructuralPrefixExtractor();
        var preparation = new SchedulingPreparation();

        var prepared = preparation.Prepare(
            rule,
            orderedAlternatives,
            new SchedulingPreparationContext(parseContext, parseContext.Position, 0, ScheduledAlternativeCursorKinds.RuleRoot, -1),
            static _ => true,
            lookaheadCache,
            lookaheadProbe,
            static _ => null,
            false);

        var structuralDescriptors = structuralExtractor.ExtractAll(orderedAlternatives);
        var precomputedLookaheadProbes = orderedAlternatives
            .Select(alternative => lookaheadProbe.Probe(alternative, parseContext.Peek(), static _ => null, false))
            .ToArray();
        var sharedPrefixCandidates = sharedPrefixDetector.Detect(precomputedLookaheadProbes);
        var continuationDescriptors = continuationPreparation.Prepare(rule, orderedAlternatives, precomputedLookaheadProbes, sharedPrefixCandidates);

        AssertStructuralDescriptorsEqual(structuralDescriptors, prepared.StructuralDescriptors);
        CollectionAssert.AreEqual(precomputedLookaheadProbes, prepared.LookaheadProbes.ToArray());
        CollectionAssert.AreEqual(sharedPrefixCandidates.ToArray(), prepared.SharedPrefixCandidates.ToArray());
        CollectionAssert.AreEqual(continuationDescriptors.ToArray(), prepared.ContinuationDescriptors.ToArray());
    }

    private static PreparedSchedulingInputs Prepare(
        SchedulingPreparation preparation,
        Rule rule,
        IReadOnlyList<Alternative> orderedAlternatives)
    {
        var parseContext = new ParseContext([new Token(new SourceSpan(0, 2), "ID", "DEFAULT_MODE", "DEFAULT_CHANNEL", "id")]);
        return preparation.Prepare(
            rule,
            orderedAlternatives,
            new SchedulingPreparationContext(parseContext, parseContext.Position, 0, ScheduledAlternativeCursorKinds.RuleRoot, -1),
            static _ => true,
            new ParserLookaheadCache(),
            new ParserLookaheadProbe(),
            static _ => null,
            false);
    }


    private static void AssertStructuralDescriptorsEqual(
        IReadOnlyList<AlternativeStructuralDescriptor> expected,
        IReadOnlyList<AlternativeStructuralDescriptor> actual)
    {
        Assert.AreEqual(expected.Count, actual.Count);
        for (var index = 0; index < expected.Count; index++)
        {
            Assert.AreEqual(expected[index].AlternativeIndex, actual[index].AlternativeIndex);
            CollectionAssert.AreEqual(expected[index].StructuralTokens.ToArray(), actual[index].StructuralTokens.ToArray());
        }
    }
    private static (Rule Rule, IReadOnlyList<Alternative> OrderedAlternatives) BuildRuleAndOrderedAlternatives()
    {
        var rule = new Rule("expr", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new Sequence([new RuleRef("ID"), new RuleRef("NUMBER")]), "A"),
            new Alternative(1, Associativity.Left, new Sequence([new RuleRef("ID"), new RuleRef("STRING")]), "B")
        ]));
        return (rule, rule.Content.Alternatives.OrderBy(static a => a.Priority).ToArray());
    }
}
