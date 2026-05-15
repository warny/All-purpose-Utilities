using Microsoft.VisualStudio.TestTools.UnitTesting;
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
}
