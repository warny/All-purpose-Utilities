using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Diagnostics;
using Utils.Parser.Model;
using Utils.Parser.Runtime;
using static UtilsTest.Parser.TestInfrastructure.ParserEngineTestHelpers;

namespace UtilsTest.Parser;

/// <summary>
/// Tests that lock alternative selection behavior and ambiguous-branch handling invariants.
/// </summary>
[TestClass]
public class ParserEngineAlternativeSelectionTests
{
    /// <summary>
    /// Verifies that when both alternatives can start, the branch that consumes more input is selected.
    /// </summary>
    [TestMethod]
    public void Parser_Alternatives_LongerSuccessfulBranchIsSelected()
    {
        var root = new Rule("root", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new Sequence([new LiteralMatch("a"), new LiteralMatch("b")]), "Long"),
            new Alternative(1, Associativity.Left, new LiteralMatch("a"), "Short")
        ]));

        var parser = CreateParser(root, "AltLongest");
        var diagnostics = new DiagnosticBag();
        var tree = parser.Parse([
            Token(0, 1, "A", "a"),
            Token(1, 1, "B", "b")
        ], diagnostics: diagnostics);

        Assert.IsNotInstanceOfType<ErrorNode>(tree);
        Assert.IsFalse(diagnostics.Any(d => d.Code == ParserDiagnostics.TrailingTokensAfterParse.Code));
        Assert.IsTrue(CollectTokenTexts(tree).Contains("b"));
    }

    [TestMethod]
    public void Parser_Alternatives_SimpleAndFailedThenSuccess_Preserved()
    {
        var root = new Rule("root", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new Sequence([new LiteralMatch("a"), new LiteralMatch("b")]), "Long"),
            new Alternative(1, Associativity.Left, new LiteralMatch("a"), "Short")
        ]));
        var parser = new ParserEngine(Utils.Parser.Resolution.RuleResolver.Resolve(new ParserDefinition(
            Name: "Alt",
            Type: GrammarType.Parser,
            Options: null,
            Actions: [],
            Imports: [],
            Modes: [new LexerMode("DEFAULT_MODE", [])],
            DeclaredTokens: new HashSet<string>(StringComparer.Ordinal),
            DeclaredChannels: new HashSet<string>(StringComparer.Ordinal) { "DEFAULT_CHANNEL", "HIDDEN" },
            ExtensionBindings: [],
            ParserRules: [root],
            RootRule: root)));
        var tokens = new List<Token>
        {
            Token(0, 1, "A", "a"),
            Token(1, 1, "B", "b")
        };

        var tree = parser.Parse(tokens);
        Assert.IsNotInstanceOfType<ErrorNode>(tree);
    }

    [TestMethod]
    public void Parser_Alternatives_AmbiguousAndLabeledBranches_Preserved()
    {
        var root = new Rule("root", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new LiteralMatch("a"), "One"),
            new Alternative(1, Associativity.Left, new LiteralMatch("a"), "Two")
        ]));
        var parser = new ParserEngine(Utils.Parser.Resolution.RuleResolver.Resolve(new ParserDefinition(
            Name: "Amb",
            Type: GrammarType.Parser,
            Options: null,
            Actions: [],
            Imports: [],
            Modes: [new LexerMode("DEFAULT_MODE", [])],
            DeclaredTokens: new HashSet<string>(StringComparer.Ordinal),
            DeclaredChannels: new HashSet<string>(StringComparer.Ordinal) { "DEFAULT_CHANNEL", "HIDDEN" },
            ExtensionBindings: [],
            ParserRules: [root],
            RootRule: root)));
        var diagnostics = new Utils.Parser.Diagnostics.DiagnosticBag();
        var tokens = new List<Token> { Token(0, 1, "A", "a") };

        var tree = parser.Parse(tokens, diagnostics: diagnostics);
        Assert.IsNotInstanceOfType<ErrorNode>(tree);
    }

    [TestMethod]
    public void Parser_Alternatives_PrecedenceSensitiveSelection_Preserved()
    {
        var root = new Rule("root", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new Sequence([new LiteralMatch("1"), new LiteralMatch("+")]), "Plus"),
            new Alternative(1, Associativity.Left, new LiteralMatch("1"), "Single")
        ]));
        var parser = new ParserEngine(Utils.Parser.Resolution.RuleResolver.Resolve(new ParserDefinition(
            Name: "Prec",
            Type: GrammarType.Parser,
            Options: null,
            Actions: [],
            Imports: [],
            Modes: [new LexerMode("DEFAULT_MODE", [])],
            DeclaredTokens: new HashSet<string>(StringComparer.Ordinal),
            DeclaredChannels: new HashSet<string>(StringComparer.Ordinal) { "DEFAULT_CHANNEL", "HIDDEN" },
            ExtensionBindings: [],
            ParserRules: [root],
            RootRule: root)));
        var tokens = new List<Token>
        {
            Token(0, 1, "NUM", "1"),
            Token(1, 1, "PLUS", "+")
        };

        var tree = parser.Parse(tokens);
        Assert.IsNotInstanceOfType<ErrorNode>(tree);
    }

    /// <summary>
    /// Verifies orchestration pruning for equivalent branches without escalating to parse-failure diagnostics.
    /// </summary>
    [TestMethod]
    public void Parser_Alternatives_EquivalentBranchesCanBePrunedWithoutParseFailure()
    {
        var scheduler = new AlternativeScheduler();
        var diagnostics = new DiagnosticBag();
        var rule = new Rule("root", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new LiteralMatch("a"), "Same"),
            new Alternative(1, Associativity.Left, new LiteralMatch("a"), "Same"),
            new Alternative(2, Associativity.Left, new LiteralMatch("a"), "Different")
        ]));

        var result = scheduler.Run(
            rule,
            rule.Content.Alternatives,
            originInputPosition: 0,
            minimumPrecedence: 0,
            diagnostics,
            precomputedDescriptors: null,
            precomputedContinuationMetadata: [],
            parseAlternative: (alternative, index) => new ScheduledAlternativeExecutionResult(
                new ActiveParseState
                {
                    Rule = rule,
                    Alternative = alternative,
                    OriginInputPosition = 0,
                    CurrentInputPosition = 1,
                    AlternativeIndex = index,
                    Cursor = new RuleContentCursor { Index = 0, Kind = ScheduledAlternativeCursorKinds.AlternativeRoot },
                    PartialNode = new ParserNode(new SourceSpan(0, 1), "DEFAULT_MODE", rule, []),
                    EndPosition = 1,
                    Status = ActiveParseStateStatus.Completed,
                    ParentStateKey = null,
                    Depth = 0,
                    Continuation = null
                },
                new ParserLookaheadProbeResult(ParserLookaheadProbeKind.RequiresParse, "A", "a", ["A"])));

        Assert.IsNotNull(result.SelectedState);
        Assert.IsTrue(result.PrunedStates.Count > 0);
        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.AmbiguousAlternativesPruned.Code));
        Assert.IsFalse(diagnostics.Any(d => d.Code == ParserDiagnostics.ParseFailure.Code));
    }

    /// <summary>
    /// Verifies that backtracking diagnostics remain scoped and do not become parse-failure diagnostics on success.
    /// </summary>
    [TestMethod]
    public void Parser_Alternatives_BacktrackingDiagnosticDoesNotBecomeParseFailureDiagnostic()
    {
        var root = new Rule("root", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new Sequence([new LiteralMatch("a"), new LiteralMatch("c")]), "FirstFails"),
            new Alternative(1, Associativity.Left, new LiteralMatch("a"), "SecondSucceeds")
        ]));
        var parser = CreateParser(root, "AltBacktracking");
        var diagnostics = new DiagnosticBag();

        var tree = parser.Parse([Token(0, 1, "A", "a")], diagnostics: diagnostics);

        Assert.IsNotInstanceOfType<ErrorNode>(tree);
        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.BacktrackingUsed.Code));
        Assert.IsFalse(diagnostics.Any(d => d.Code == ParserDiagnostics.ParseFailure.Code));
    }

    /// <summary>
    /// Collects lexer token texts from the parse tree in traversal order.
    /// </summary>
    private static List<string> CollectTokenTexts(ParseNode node)
    {
        var texts = new List<string>();
        CollectTokenTextsCore(node, texts);
        return texts;
    }

    /// <summary>
    /// Recursively traverses parse nodes and appends lexer token texts to <paramref name="texts"/>.
    /// </summary>
    private static void CollectTokenTextsCore(ParseNode node, List<string> texts)
    {
        switch (node)
        {
            case LexerNode lexerNode:
                texts.Add(lexerNode.Token.Text);
                return;
            case ParserNode parserNode:
                foreach (var child in parserNode.Children)
                {
                    CollectTokenTextsCore(child, texts);
                }

                return;
            default:
                return;
        }
    }

    /// <summary>
    /// Creates a parser engine for tests using a single root parser rule and default channels/mode.
    /// </summary>
    private static ParserEngine CreateParser(Rule root, string grammarName)
    {
        return new ParserEngine(Utils.Parser.Resolution.RuleResolver.Resolve(new ParserDefinition(
            Name: grammarName,
            Type: GrammarType.Parser,
            Options: null,
            Actions: [],
            Imports: [],
            Modes: [new LexerMode("DEFAULT_MODE", [])],
            DeclaredTokens: new HashSet<string>(StringComparer.Ordinal),
            DeclaredChannels: new HashSet<string>(StringComparer.Ordinal) { "DEFAULT_CHANNEL", "HIDDEN" },
            ExtensionBindings: [],
            ParserRules: [root],
            RootRule: root)));
    }
}
