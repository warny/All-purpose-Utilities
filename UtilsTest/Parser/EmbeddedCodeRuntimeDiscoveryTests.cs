using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.EmbeddedCode;
using Utils.Parser.Model;

namespace UtilsTest.Parser;

/// <summary>
/// Validates shared embedded-code runtime discovery metadata used by parser execution paths.
/// </summary>
[TestClass]
public class EmbeddedCodeRuntimeDiscoveryTests
{
    /// <summary>
    /// Verifies that validating predicates receive parser runtime rule, alternative, and element indexes.
    /// </summary>
    [TestMethod]
    public void Discover_WhenValidatingPredicateExists_ReturnsExecutableRuntimeEntry()
    {
        var predicate = new ValidatingPredicate("inputPosition == 0");
        var rule = CreateParserRule("start", new Sequence([predicate]));

        var entry = DiscoverSingle(CreateDefinition(rule));

        AssertExecutable(entry, EmbeddedCodeKind.SemanticPredicate, "start", "inputPosition == 0", 0, 0);
    }

    /// <summary>
    /// Verifies that inline parser actions receive parser runtime rule, alternative, and element indexes.
    /// </summary>
    [TestMethod]
    public void Discover_WhenInlineParserActionExists_ReturnsExecutableRuntimeEntry()
    {
        var action = new EmbeddedAction("record();", ActionContext.Alternative, ActionPosition.Inline, []);
        var rule = CreateParserRule("start", new Sequence([new LiteralMatch("a"), action]));

        var entry = DiscoverSingle(CreateDefinition(rule));

        AssertExecutable(entry, EmbeddedCodeKind.ParserInlineAction, "start", "record();", 0, 1);
    }

    /// <summary>
    /// Verifies that single-item alternatives keep an unavailable element index, matching runtime dispatch.
    /// </summary>
    [TestMethod]
    public void Discover_WhenAlternativeHasSingleItem_UsesUnavailableElementIndex()
    {
        var action = new EmbeddedAction("single();", ActionContext.Alternative, ActionPosition.Inline, []);
        var rule = CreateParserRule("start", new Alternation([new Alternative(0, Associativity.Left, action)]));

        var entry = DiscoverSingle(CreateDefinition(rule));

        AssertExecutable(entry, EmbeddedCodeKind.ParserInlineAction, "start", "single();", 0, null);
    }

    /// <summary>
    /// Verifies that sequence items use zero-based runtime element indexes.
    /// </summary>
    [TestMethod]
    public void Discover_WhenActionIsInSequence_UsesSequenceElementIndex()
    {
        var action = new EmbeddedAction("sequence();", ActionContext.Alternative, ActionPosition.Inline, []);
        var rule = CreateParserRule("start", new Sequence([new LiteralMatch("a"), action, new LiteralMatch("b")]));

        var entry = DiscoverSingle(CreateDefinition(rule));

        AssertExecutable(entry, EmbeddedCodeKind.ParserInlineAction, "start", "sequence();", 0, 1);
    }

    /// <summary>
    /// Verifies that quantified content uses the runtime inner element index strategy.
    /// </summary>
    [TestMethod]
    public void Discover_WhenPredicateIsInsideQuantifier_UsesRuntimeInnerIndex()
    {
        var predicate = new ValidatingPredicate("insideQuantifier");
        var rule = CreateParserRule("start", new Sequence([new LiteralMatch("a"), new Quantifier(predicate, 0, null)]));

        var entry = DiscoverSingle(CreateDefinition(rule));

        AssertExecutable(entry, EmbeddedCodeKind.SemanticPredicate, "start", "insideQuantifier", 0, 0);
    }

    /// <summary>
    /// Verifies that negation probes use the runtime inner element index strategy.
    /// </summary>
    [TestMethod]
    public void Discover_WhenPredicateIsInsideNegation_UsesRuntimeProbeIndex()
    {
        var predicate = new ValidatingPredicate("insideNegation");
        var rule = CreateParserRule("start", new Sequence([new LiteralMatch("a"), new Negation(predicate)]));

        var entry = DiscoverSingle(CreateDefinition(rule));

        AssertExecutable(entry, EmbeddedCodeKind.SemanticPredicate, "start", "insideNegation", 0, 0);
    }

    /// <summary>
    /// Verifies that direct-left-recursive base alternatives are re-indexed after recursive alternatives are split out.
    /// </summary>
    [TestMethod]
    public void Discover_WhenLeftRecursiveBaseFollowsRecursiveAlternative_UsesResolvedBaseIndex()
    {
        var predicate = new ValidatingPredicate("base");
        var recursiveAlternative = new Alternative(0, Associativity.Left, new Sequence([new RuleRef("expr"), new LiteralMatch("+"), new RuleRef("expr")]));
        var baseAlternative = new Alternative(1, Associativity.Left, new Sequence([predicate, new LiteralMatch("id")]));
        var rule = CreateParserRule("expr", new Alternation([recursiveAlternative, baseAlternative]));
        var definition = CreateDefinition(rule, leftRecursiveRules: new Dictionary<string, LeftRecursiveRuleInfo>
        {
            [rule.Name] = new LeftRecursiveRuleInfo
            {
                Rule = rule,
                BaseAlternatives = [baseAlternative],
                RecursiveAlternatives = [recursiveAlternative]
            }
        });

        var entry = DiscoverSingle(definition);

        AssertExecutable(entry, EmbeddedCodeKind.SemanticPredicate, "expr", "base", 0, 0);
    }

    /// <summary>
    /// Verifies that direct-left-recursive tail indexing uses the effective tail after self-reference removal.
    /// </summary>
    [TestMethod]
    public void Discover_WhenActionIsInLeftRecursiveTail_UsesTailElementIndex()
    {
        var action = new EmbeddedAction("tail();", ActionContext.Alternative, ActionPosition.Inline, []);
        var baseAlternative = new Alternative(0, Associativity.Left, new Sequence([new LiteralMatch("id")]));
        var recursiveAlternative = new Alternative(1, Associativity.Left, new Sequence([new RuleRef("expr"), new LiteralMatch("+"), action, new RuleRef("expr")]));
        var rule = CreateParserRule("expr", new Alternation([baseAlternative, recursiveAlternative]));
        var definition = CreateDefinition(rule, leftRecursiveRules: new Dictionary<string, LeftRecursiveRuleInfo>
        {
            [rule.Name] = new LeftRecursiveRuleInfo
            {
                Rule = rule,
                BaseAlternatives = [baseAlternative],
                RecursiveAlternatives = [recursiveAlternative]
            }
        });

        var entry = DiscoverSingle(definition);

        AssertExecutable(entry, EmbeddedCodeKind.ParserInlineAction, "expr", "tail();", 0, 1);
    }

    /// <summary>
    /// Verifies that duplicate source text in different alternatives remains represented as distinct runtime entries.
    /// </summary>
    [TestMethod]
    public void Discover_WhenSourceTextIsDuplicated_ReturnsDistinctRuntimeEntries()
    {
        var rule = CreateParserRule(
            "start",
            new Alternation([
                new Alternative(0, Associativity.Left, new Sequence([new ValidatingPredicate("same")])),
                new Alternative(1, Associativity.Left, new Sequence([new ValidatingPredicate("same")]))
            ]));

        var entries = EmbeddedCodeRuntimeDiscovery.Discover(CreateDefinition(rule)).ExecutableEntries;

        Assert.AreEqual(2, entries.Count);
        Assert.AreEqual(0, entries[0].AlternativeIndex);
        Assert.AreEqual(1, entries[1].AlternativeIndex);
        Assert.AreEqual("same", entries[0].Source.SourceText);
        Assert.AreEqual("same", entries[1].Source.SourceText);
    }

    /// <summary>
    /// Verifies that unsupported embedded-code scopes expose explicit common reasons.
    /// </summary>
    [TestMethod]
    public void Discover_WhenUnsupportedConstructsExist_ReturnsExplicitReasons()
    {
        var init = new EmbeddedAction("init", ActionContext.Rule, ActionPosition.Before, []);
        var after = new EmbeddedAction("after", ActionContext.Rule, ActionPosition.After, []);
        var nonInline = new EmbeddedAction("before", ActionContext.Alternative, ActionPosition.Before, []);
        var parserRule = CreateParserRule("start", new Sequence([nonInline]), init, after);
        var lexerAction = new EmbeddedAction("lexer", ActionContext.Alternative, ActionPosition.Inline, []);
        var lexerPredicate = new ValidatingPredicate("lexerPredicate");
        var lexerRule = CreateLexerRule("A", new Sequence([lexerAction, lexerPredicate]));
        var grammarAction = new GrammarAction("members", "int value;");

        var unsupported = EmbeddedCodeRuntimeDiscovery.Discover(CreateDefinition(parserRule, [grammarAction], [lexerRule])).UnsupportedEntries;

        CollectionAssert.AreEquivalent(
            new object[]
            {
                EmbeddedCodeUnsupportedReason.GrammarAction,
                EmbeddedCodeUnsupportedReason.RuleInitAction,
                EmbeddedCodeUnsupportedReason.UnsupportedActionPosition,
                EmbeddedCodeUnsupportedReason.RuleAfterAction,
                EmbeddedCodeUnsupportedReason.LexerAction,
                EmbeddedCodeUnsupportedReason.LexerPredicate
            },
            unsupported.Select(static entry => (object)entry.UnsupportedReason).ToArray());
    }


    /// <summary>
    /// Verifies that skipped embedded-code entries are non-executable and never expose runtime dispatch keys.
    /// </summary>
    [TestMethod]
    public void Discover_WhenUnsupportedConstructsExist_MarksEntriesAsNonExecutableWithoutRuntimeKeys()
    {
        var init = new EmbeddedAction("init", ActionContext.Rule, ActionPosition.Before, []);
        var after = new EmbeddedAction("after", ActionContext.Rule, ActionPosition.After, []);
        var nonInline = new EmbeddedAction("before", ActionContext.Alternative, ActionPosition.Before, []);
        var parserRule = CreateParserRule("start", new Sequence([nonInline]), init, after);
        var lexerAction = new EmbeddedAction("lexer", ActionContext.Alternative, ActionPosition.Inline, []);
        var lexerPredicate = new ValidatingPredicate("lexerPredicate");
        var lexerRule = CreateLexerRule("A", new Sequence([lexerAction, lexerPredicate]));
        var grammarAction = new GrammarAction("members", "int value;");

        var unsupported = EmbeddedCodeRuntimeDiscovery.Discover(CreateDefinition(parserRule, [grammarAction], [lexerRule])).UnsupportedEntries;

        Assert.AreEqual(6, unsupported.Count);
        foreach (var entry in unsupported)
        {
            Assert.IsFalse(entry.IsRuntimeExecutable);
            Assert.IsNull(entry.RuntimeKey);
            Assert.AreNotEqual(EmbeddedCodeUnsupportedReason.None, entry.UnsupportedReason);
        }
    }

    /// <summary>
    /// Verifies that parser actions inside negation probes use shared runtime probe indexes.
    /// </summary>
    [TestMethod]
    public void Discover_WhenActionIsInsideNegation_UsesRuntimeProbeIndex()
    {
        var action = new EmbeddedAction("insideNegationAction", ActionContext.Alternative, ActionPosition.Inline, []);
        var rule = CreateParserRule("start", new Sequence([new LiteralMatch("a"), new Negation(action)]));

        var entry = DiscoverSingle(CreateDefinition(rule));

        AssertExecutable(entry, EmbeddedCodeKind.ParserInlineAction, "start", "insideNegationAction", 0, 0);
    }

    /// <summary>
    /// Returns the single discovered executable entry for a test definition.
    /// </summary>
    /// <param name="definition">Parser definition to inspect.</param>
    /// <returns>The only executable discovery entry.</returns>
    private static EmbeddedCodeRuntimeEntry DiscoverSingle(ParserDefinition definition) =>
        EmbeddedCodeRuntimeDiscovery.Discover(definition).ExecutableEntries.Single();

    /// <summary>
    /// Asserts the common executable runtime metadata for an entry.
    /// </summary>
    /// <param name="entry">Entry to inspect.</param>
    /// <param name="kind">Expected embedded-code kind.</param>
    /// <param name="ruleName">Expected rule name.</param>
    /// <param name="sourceText">Expected source text.</param>
    /// <param name="alternativeIndex">Expected alternative index.</param>
    /// <param name="elementIndex">Expected element index.</param>
    private static void AssertExecutable(EmbeddedCodeRuntimeEntry entry, EmbeddedCodeKind kind, string ruleName, string sourceText, int? alternativeIndex, int? elementIndex)
    {
        Assert.IsTrue(entry.IsRuntimeExecutable);
        Assert.AreEqual(EmbeddedCodeUnsupportedReason.None, entry.UnsupportedReason);
        Assert.AreEqual(kind, entry.Kind);
        Assert.AreEqual(ruleName, entry.RuleName);
        Assert.AreEqual(sourceText, entry.Source.SourceText);
        Assert.AreEqual(alternativeIndex, entry.AlternativeIndex);
        Assert.AreEqual(elementIndex, entry.ElementIndex);
        Assert.IsNotNull(entry.RuntimeKey);
        Assert.AreEqual(kind, entry.RuntimeKey.Kind);
        Assert.AreEqual(ruleName, entry.RuntimeKey.RuleName);
    }

    /// <summary>
    /// Creates a parser definition for discovery tests.
    /// </summary>
    /// <param name="parserRule">Parser rule to include.</param>
    /// <param name="actions">Optional grammar-level actions.</param>
    /// <param name="lexerRules">Optional lexer rules.</param>
    /// <param name="leftRecursiveRules">Optional left-recursive rule metadata.</param>
    /// <returns>A parser definition containing supplied metadata.</returns>
    private static ParserDefinition CreateDefinition(
        Rule parserRule,
        IReadOnlyList<GrammarAction>? actions = null,
        IReadOnlyList<Rule>? lexerRules = null,
        IReadOnlyDictionary<string, LeftRecursiveRuleInfo>? leftRecursiveRules = null) =>
        new ParserDefinition(
            "G",
            GrammarType.Combined,
            null,
            actions ?? [],
            [],
            lexerRules is null ? [] : [new LexerMode("DEFAULT_MODE", lexerRules)],
            [parserRule],
            parserRule)
        {
            LeftRecursiveRules = leftRecursiveRules ?? new Dictionary<string, LeftRecursiveRuleInfo>()
        };

    /// <summary>
    /// Creates a parser rule around supplied content.
    /// </summary>
    /// <param name="name">Rule name.</param>
    /// <param name="content">Rule content or full alternation.</param>
    /// <param name="initAction">Optional rule initialization action.</param>
    /// <param name="afterAction">Optional rule finalization action.</param>
    /// <returns>A parser rule.</returns>
    private static Rule CreateParserRule(string name, RuleContent content, EmbeddedAction? initAction = null, EmbeddedAction? afterAction = null)
    {
        var alternation = content as Alternation ?? new Alternation([new Alternative(0, Associativity.Left, content)]);
        return new Rule(name, 0, false, alternation, InitAction: initAction, AfterAction: afterAction, Kind: RuleKind.Parser);
    }

    /// <summary>
    /// Creates a lexer rule around supplied content.
    /// </summary>
    /// <param name="name">Rule name.</param>
    /// <param name="content">Rule content or full alternation.</param>
    /// <returns>A lexer rule.</returns>
    private static Rule CreateLexerRule(string name, RuleContent content)
    {
        var alternation = content as Alternation ?? new Alternation([new Alternative(0, Associativity.Left, content)]);
        return new Rule(name, 0, false, alternation, Kind: RuleKind.Lexer);
    }
}
