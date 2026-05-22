using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Diagnostics;
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
        var preparation = CreatePreparation();

        var first = Prepare(preparation, rule, orderedAlternatives);
        var second = Prepare(preparation, rule, orderedAlternatives);

        AssertStructuralDescriptorsEqual(first.StructuralDescriptors, second.StructuralDescriptors);
        CollectionAssert.AreEqual(first.LookaheadProbes.ToArray(), second.LookaheadProbes.ToArray());
        AssertSharedPrefixCandidatesEqual(first.SharedPrefixCandidates, second.SharedPrefixCandidates);
        AssertContinuationDescriptorsEqual(first.ContinuationDescriptors, second.ContinuationDescriptors);
    }

    [TestMethod]
    public void Prepare_DoesNotModifyGrammar()
    {
        var (rule, orderedAlternatives) = BuildRuleAndOrderedAlternatives();
        var snapshot = rule.Content.Alternatives.Select(static alternative => alternative.Content.ToString()).ToArray();

        var preparation = CreatePreparation();
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
        var preparation = CreatePreparation();

        var prepared = Prepare(preparation, rule, orderedAlternatives);

        Assert.AreEqual(2, prepared.StructuralDescriptors.Count);
        Assert.AreEqual(2, prepared.LookaheadProbes.Count);
    }

    [TestMethod]
    public void ParserEngine_PreparationRefactor_IsBehaviorEquivalent()
    {
        var rootRule = new Rule("root", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new Sequence([new LiteralMatch("id"), new LiteralMatch("42")]), "A"),
            new Alternative(1, Associativity.Left, new Sequence([new LiteralMatch("id"), new LiteralMatch("txt")]), "B")
        ]));
        var definition = Utils.Parser.Resolution.RuleResolver.Resolve(new ParserDefinition(
            Name: "G",
            Type: GrammarType.Parser,
            Options: null,
            Actions: [],
            Imports: [],
            Modes: [new LexerMode("DEFAULT_MODE", [])],
            DeclaredTokens: new HashSet<string>(StringComparer.Ordinal),
            DeclaredChannels: new HashSet<string>(StringComparer.Ordinal) { "DEFAULT_CHANNEL", "HIDDEN" },
            ExtensionBindings: [],
            ParserRules: [rootRule],
            RootRule: rootRule));
        var tokens = new[]
        {
            new Token(new SourceSpan(0, 2), "ID", "DEFAULT_MODE", "DEFAULT_CHANNEL", "id"),
            new Token(new SourceSpan(3, 2), "NUMBER", "DEFAULT_MODE", "DEFAULT_CHANNEL", "42")
        };

        var diagnosticsFirst = new DiagnosticBag();
        var diagnosticsSecond = new DiagnosticBag();
        var parser = new ParserEngine(definition);

        var first = parser.Parse(tokens, diagnostics: diagnosticsFirst);
        var second = parser.Parse(tokens, diagnostics: diagnosticsSecond);

        Assert.AreEqual(first.ToString(), second.ToString());
        Assert.AreEqual(first.Span, second.Span);
        Assert.AreEqual(diagnosticsFirst.ToString(), diagnosticsSecond.ToString());
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
            new SchedulingPreparationContext(parseContext, parseContext.Position, 0, ScheduledAlternativeCursorKinds.RuleRoot, -1, false));
    }

    private static SchedulingPreparation CreatePreparation()
    {
        return new SchedulingPreparation(new ParserLookaheadProbe(), new ParserLookaheadCache(), static _ => null);
    }


    private static void AssertSharedPrefixCandidatesEqual(
        IReadOnlyList<ParserLookaheadSharedPrefixCandidate> expected,
        IReadOnlyList<ParserLookaheadSharedPrefixCandidate> actual)
    {
        Assert.AreEqual(expected.Count, actual.Count);
        for (var index = 0; index < expected.Count; index++)
        {
            Assert.AreEqual(expected[index].TokenName, actual[index].TokenName);
            CollectionAssert.AreEqual(expected[index].AlternativeIndexes.ToArray(), actual[index].AlternativeIndexes.ToArray());
        }
    }

    private static void AssertContinuationDescriptorsEqual(
        IReadOnlyList<ParserContinuationDescriptor> expected,
        IReadOnlyList<ParserContinuationDescriptor> actual)
    {
        Assert.AreEqual(expected.Count, actual.Count);
        for (var index = 0; index < expected.Count; index++)
        {
            Assert.AreEqual(expected[index].Key, actual[index].Key);
            Assert.AreEqual(expected[index].Category, actual[index].Category);
            Assert.AreEqual(expected[index].IsSharedPrefixCandidate, actual[index].IsSharedPrefixCandidate);
            CollectionAssert.AreEqual(expected[index].ExpectedTokenNames.ToArray(), actual[index].ExpectedTokenNames.ToArray());
        }
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
            new Alternative(0, Associativity.Left, new Sequence([new LiteralMatch("id"), new LiteralMatch("42")]), "A"),
            new Alternative(1, Associativity.Left, new Sequence([new LiteralMatch("id"), new LiteralMatch("txt")]), "B")
        ]));
        return (rule, rule.Content.Alternatives.OrderBy(static a => a.Priority).ToArray());
    }
}
