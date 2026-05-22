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
        var rule = new Rule("start", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new LiteralMatch("a"))
        ]));
        var parser = CreateParser(rule, "OutcomeLocalSuccess");
        var diagnostics = new DiagnosticBag();

        var result = parser.Parse([CreateToken(0, "A", "a"), CreateToken(1, "B", "b")], diagnostics: diagnostics);

        Assert.IsInstanceOfType<ErrorNode>(result);
        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.TrailingTokensAfterParse.Code));
    }

    [TestMethod]
    public void LocalFailure_DoesNotImplyGlobalFailure_WhenAnotherAlternativeSucceeds()
    {
        var rule = new Rule("start", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new Sequence([new LiteralMatch("a"), new LiteralMatch("b")]), "Long"),
            new Alternative(1, Associativity.Left, new LiteralMatch("a"), "Short")
        ]));
        var parser = CreateParser(rule, "OutcomeLocalFailure");

        var result = parser.Parse([CreateToken(0, "A", "a")]);

        Assert.IsInstanceOfType<ParserNode>(result);
    }

    [TestMethod]
    public void Pruning_DoesNotImplySyntaxInvalidity_WhenParseGloballySucceeds()
    {
        var rule = new Rule("root", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new LiteralMatch("a"), "Same"),
            new Alternative(1, Associativity.Left, new LiteralMatch("a"), "Same")
        ]));

        var scheduler = new AlternativeScheduler();
        var schedulerDiagnostics = new DiagnosticBag();
        var state = new ActiveParseState
        {
            Rule = rule,
            Alternative = rule.Content.Alternatives[0],
            OriginInputPosition = 0,
            CurrentInputPosition = 1,
            AlternativeIndex = 0,
            Cursor = new RuleContentCursor { Index = 0, Kind = ScheduledAlternativeCursorKinds.AlternativeRoot },
            PartialNode = new ParserNode(new SourceSpan(0, 1), "DEFAULT_MODE", rule, []),
            EndPosition = 1,
            Status = ActiveParseStateStatus.Completed,
            ParentStateKey = null,
            Depth = 0,
            Continuation = null
        };

        _ = scheduler.Run(rule, rule.Content.Alternatives, 0, 0, schedulerDiagnostics, (_, index) =>
            new ScheduledAlternativeExecutionResult(
                state with { Alternative = rule.Content.Alternatives[index], AlternativeIndex = index },
                new ParserLookaheadProbeResult(ParserLookaheadProbeKind.RequiresParse, "A", "a", ["A"])),
            precomputedDescriptors: null,
            precomputedContinuationMetadata: Enumerable.Range(0, rule.Content.Alternatives.Count).Select(i => new ParserContinuationDescriptor(new ParserContinuationKey(rule.Name, i, 0), ParserContinuationCategory.Sequential, null, false)).ToArray(),
            precomputedLookaheadProbes: Enumerable.Range(0, rule.Content.Alternatives.Count).Select(static _ => new ParserLookaheadProbeResult(ParserLookaheadProbeKind.Unknown, null, null)).ToArray(),
            precomputedSharedPrefixCandidates: []);

        Assert.IsTrue(schedulerDiagnostics.Any(d => d.Code == ParserDiagnostics.AmbiguousAlternativesPruned.Code));

        var parser = CreateParser(rule, "OutcomePruning");
        var parserDiagnostics = new DiagnosticBag();
        var result = parser.Parse([CreateToken(0, "A", "a")], diagnostics: parserDiagnostics);

        Assert.IsInstanceOfType<ParserNode>(result);
        Assert.IsFalse(parserDiagnostics.Any(d => d.Code == ParserDiagnostics.ParseFailure.Code));
        Assert.IsFalse(parserDiagnostics.Any(d => d.Code == ParserDiagnostics.TrailingTokensAfterParse.Code));
    }

    [TestMethod]
    public void LocalDiagnostics_DoNotAutomaticallyBecomeGlobalDiagnostics()
    {
        var rule = new Rule("start", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new Sequence([
                new LiteralMatch("a"),
                new LiteralMatch("b"),
                new LiteralMatch("c")
            ]), "Long"),
            new Alternative(1, Associativity.Left, new Sequence([new LiteralMatch("a"), new LiteralMatch("b")]), "Short")
        ]));
        var parser = CreateParser(rule, "OutcomeDiagnostics");
        var diagnostics = new DiagnosticBag();

        var result = parser.Parse([CreateToken(0, "A", "a"), CreateToken(1, "B", "b")], diagnostics: diagnostics);

        Assert.IsInstanceOfType<ParserNode>(result);
        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.BacktrackingUsed.Code));
        Assert.IsFalse(diagnostics.Any(d => d.Code == ParserDiagnostics.ParseFailure.Code));
        Assert.IsFalse(diagnostics.Any(d => d.Code == ParserDiagnostics.TrailingTokensAfterParse.Code));
    }

    [TestMethod]
    public void CompletedBranch_DoesNotImplyFinalSelection_AlternativeSelectionRemainsDeterministic()
    {
        var rule = new Rule("root", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new Sequence([new LiteralMatch("a"), new LiteralMatch("b")]), "Long"),
            new Alternative(1, Associativity.Left, new LiteralMatch("a"), "Short")
        ]));
        var parser = CreateParser(rule, "OutcomeSelection");

        var result = parser.Parse([CreateToken(0, "A", "a"), CreateToken(1, "B", "b")]);

        Assert.IsInstanceOfType<ParserNode>(result);
        Assert.IsTrue(ContainsLexerTokenText(result, "b"));
    }

    private static ParserEngine CreateParser(Rule rootRule, string grammarName)
    {
        var definition = new ParserDefinition(
            Name: grammarName,
            Type: GrammarType.Parser,
            Options: null,
            Actions: [],
            Imports: [],
            Modes: [new LexerMode("DEFAULT_MODE", [])],
            DeclaredTokens: new HashSet<string>(StringComparer.Ordinal),
            DeclaredChannels: new HashSet<string>(StringComparer.Ordinal) { "DEFAULT_CHANNEL", "HIDDEN" },
            ExtensionBindings: [],
            ParserRules: [rootRule],
            RootRule: rootRule);

        return new ParserEngine(RuleResolver.Resolve(definition));
    }

    private static Token CreateToken(int position, string ruleName, string text)
        => new(new SourceSpan(position, 1), ruleName, "DEFAULT_MODE", "DEFAULT_CHANNEL", text);

    private static bool ContainsLexerTokenText(ParseNode root, string tokenText)
    {
        return new ParseTreeNavigator(root)
            .Descendants()
            .Prepend(new ParseTreeNavigator(root))
            .Any(node => node.IsLexer && node.Node is LexerNode lexer && string.Equals(lexer.Token.Text, tokenText, StringComparison.Ordinal));
    }
}
