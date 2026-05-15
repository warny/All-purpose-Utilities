using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Diagnostics;
using Utils.Parser.Model;
using Utils.Parser.Resolution;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Locks branch outcome model invariants without changing runtime behavior.
/// </summary>
[TestClass]
public class ParserBranchOutcomeModelTests
{
    [TestMethod]
    public void LocalSuccess_DoesNotImplyGlobalSuccess_WhenTrailingTokensRemain()
    {
        var startRule = new Rule("start", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new LiteralMatch("a"))
        ]));
        var parser = new ParserEngine(RuleResolver.Resolve(new ParserDefinition(
            Name: "Outcome",
            Type: GrammarType.Parser,
            Options: null,
            Actions: [],
            Imports: [],
            Modes: [new LexerMode("DEFAULT_MODE", [])],
            DeclaredTokens: new HashSet<string>(StringComparer.Ordinal),
            DeclaredChannels: new HashSet<string>(StringComparer.Ordinal) { "DEFAULT_CHANNEL", "HIDDEN" },
            ExtensionBindings: [],
            ParserRules: [startRule],
            RootRule: startRule)));

        var diagnostics = new DiagnosticBag();
        var result = parser.Parse([new Token(new SourceSpan(0, 1), "A", "DEFAULT_MODE", "DEFAULT_CHANNEL", "a"), new Token(new SourceSpan(1, 1), "B", "DEFAULT_MODE", "DEFAULT_CHANNEL", "b")], diagnostics: diagnostics);

        Assert.IsInstanceOfType<ErrorNode>(result);
        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.TrailingTokensAfterParse.Code));
    }

    [TestMethod]
    public void LocalFailure_DoesNotImplyGlobalFailure_WhenAnotherAlternativeSucceeds()
    {
        var startRule = new Rule("start", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new Sequence([new LiteralMatch("a"), new LiteralMatch("b")]), "Long"),
            new Alternative(1, Associativity.Left, new LiteralMatch("a"), "Short")
        ]));
        var parser = new ParserEngine(RuleResolver.Resolve(new ParserDefinition(
            Name: "Outcome",
            Type: GrammarType.Parser,
            Options: null,
            Actions: [],
            Imports: [],
            Modes: [new LexerMode("DEFAULT_MODE", [])],
            DeclaredTokens: new HashSet<string>(StringComparer.Ordinal),
            DeclaredChannels: new HashSet<string>(StringComparer.Ordinal) { "DEFAULT_CHANNEL", "HIDDEN" },
            ExtensionBindings: [],
            ParserRules: [startRule],
            RootRule: startRule)));

        var result = parser.Parse([new Token(new SourceSpan(0, 1), "A", "DEFAULT_MODE", "DEFAULT_CHANNEL", "a")]);

        Assert.IsInstanceOfType<ParserNode>(result);
    }

    [TestMethod]
    public void Pruning_DoesNotImplySyntaxInvalidity_WhenParseGloballySucceeds()
    {
        var root = new Rule("root", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new LiteralMatch("a"), "One"),
            new Alternative(1, Associativity.Left, new LiteralMatch("a"), "Two")
        ]));
        var parser = new ParserEngine(RuleResolver.Resolve(new ParserDefinition(
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
        var diagnostics = new DiagnosticBag();

        var result = parser.Parse([new Token(new SourceSpan(0, 1), "A", "DEFAULT_MODE", "DEFAULT_CHANNEL", "a")], diagnostics: diagnostics);

        Assert.IsInstanceOfType<ParserNode>(result);
                Assert.IsFalse(diagnostics.Any(d => d.Code == ParserDiagnostics.ParseFailure.Code));
    }

    [TestMethod]
    public void LocalDiagnostics_DoNotAutomaticallyBecomeGlobalDiagnostics()
    {
        var startRule = new Rule("start", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new Sequence([new LiteralMatch("a"), new LiteralMatch("b")]), "Long"),
            new Alternative(1, Associativity.Left, new LiteralMatch("a"), "Short")
        ]));
        var parser = new ParserEngine(RuleResolver.Resolve(new ParserDefinition(
            Name: "Outcome",
            Type: GrammarType.Parser,
            Options: null,
            Actions: [],
            Imports: [],
            Modes: [new LexerMode("DEFAULT_MODE", [])],
            DeclaredTokens: new HashSet<string>(StringComparer.Ordinal),
            DeclaredChannels: new HashSet<string>(StringComparer.Ordinal) { "DEFAULT_CHANNEL", "HIDDEN" },
            ExtensionBindings: [],
            ParserRules: [startRule],
            RootRule: startRule)));
        var diagnostics = new DiagnosticBag();

        var result = parser.Parse([new Token(new SourceSpan(0, 1), "A", "DEFAULT_MODE", "DEFAULT_CHANNEL", "a")], diagnostics: diagnostics);

        Assert.IsInstanceOfType<ParserNode>(result);
        Assert.IsFalse(diagnostics.Any(d => d.Code == ParserDiagnostics.ParseFailure.Code));
        Assert.IsFalse(diagnostics.Any(d => d.Code == ParserDiagnostics.TrailingTokensAfterParse.Code));
    }

    [TestMethod]
    public void CompletedBranch_DoesNotImplyFinalSelection_AlternativeSelectionRemainsDeterministic()
    {
        var root = new Rule("root", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new Sequence([new LiteralMatch("a"), new LiteralMatch("b")]), "Long"),
            new Alternative(1, Associativity.Left, new LiteralMatch("a"), "Short")
        ]));
        var parser = new ParserEngine(RuleResolver.Resolve(new ParserDefinition(
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

        var result = parser.Parse([new Token(new SourceSpan(0, 1), "A", "DEFAULT_MODE", "DEFAULT_CHANNEL", "a"), new Token(new SourceSpan(1, 1), "B", "DEFAULT_MODE", "DEFAULT_CHANNEL", "b")]);

        Assert.IsInstanceOfType<ParserNode>(result);
    }
}
