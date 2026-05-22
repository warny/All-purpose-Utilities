using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using Utils.Parser.Bootstrap;
using Utils.Parser.Diagnostics;
using Utils.Parser.Generators.Internal;
using Utils.Parser.Model;
using Utils.Parser.Resolution;
using Utils.Parser.Runtime;
using System.IO;
using System;
using System.Collections.Generic;

namespace UtilsTest.Parser;

/// <summary>
/// Tests for shared parser diagnostics used by runtime and source generator pipelines.
/// </summary>
[TestClass]
public class ParserDiagnosticsTests
{
    [TestMethod]
    public void DiagnosticCode_MapsToSeverity()
    {
        Assert.AreEqual(DiagnosticSeverity.Error, ParserDiagnosticSeverityMapper.FromCode("UP0001"));
        Assert.AreEqual(DiagnosticSeverity.Warning, ParserDiagnosticSeverityMapper.FromCode("UP1001"));
        Assert.AreEqual(DiagnosticSeverity.Warning, ParserDiagnosticSeverityMapper.FromCode("UP5001"));
        Assert.AreEqual(DiagnosticSeverity.Info, ParserDiagnosticSeverityMapper.FromCode("UP8001"));
        Assert.AreEqual(DiagnosticSeverity.Debug, ParserDiagnosticSeverityMapper.FromCode("UP9001"));
        Assert.AreEqual(DiagnosticSeverity.Warning, ParserDiagnosticSeverityMapper.FromCode("PARSER001"));
    }

    [TestMethod]
    public void DescriptorTable_LookupByCode_Works()
    {
        var found = ParserDiagnostics.TryGet("UP1002", out var descriptor);
        Assert.IsTrue(found);
        Assert.IsNotNull(descriptor);
        Assert.AreEqual(ParserDiagnostics.TokensBlockIgnored.Code, descriptor.Code);
    }

    [TestMethod]
    public void Generator_UsesSharedDescriptors_ForIgnoredConstructs()
    {
        var diagnostics = new DiagnosticBag();
        var tokens = new G4Tokenizer("grammar G; tokens { A }; rule : 'a' ;").Tokenize();

        var parser = new G4Parser(tokens, diagnostics);
        parser.Parse();

        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.TokensBlockIgnored.Code));
    }

    [TestMethod]
    public void RuntimeEngine_InlineActionAndPredicate_EmitDiagnostics()
    {
        var diagnostics = new DiagnosticBag();
        var parserRule = new Rule(
            "start",
            0,
            false,
            new Alternation([
                new Alternative(
                    0,
                    Associativity.Left,
                    new Sequence([
                        new EmbeddedAction("var x = 1;", ActionContext.Alternative, ActionPosition.Inline, []),
                        new ValidatingPredicate("true")
                    ]))
            ]));

        var definition = RuleResolver.Resolve(new ParserDefinition(
            Name: "G",
            Type: GrammarType.Parser,
            Options: null,
            Actions: [],
            Imports: [],
            Modes: [new LexerMode("DEFAULT_MODE", [])],
            DeclaredTokens: new HashSet<string>(StringComparer.Ordinal),
            DeclaredChannels: new HashSet<string>(StringComparer.Ordinal) { "DEFAULT_CHANNEL", "HIDDEN" },
            ExtensionBindings: [],
            ParserRules: [parserRule],
            RootRule: parserRule));

        var parser = new ParserEngine(definition);
        parser.Parse([], diagnostics: diagnostics);

        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.SemanticPredicateNotEnforced.Code));
        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.InlineActionStoredNotExecuted.Code));
    }

    [TestMethod]
    public void Resolver_UnknownRule_EmitsBlockingDiagnostic()
    {
        var diagnostics = new DiagnosticBag();
        var parserRule = new Rule(
            "start",
            0,
            false,
            new Alternation([
                new Alternative(0, Associativity.Left, new RuleRef("MissingRule"))
            ]));

        var definition = new ParserDefinition(
            Name: "G",
            Type: GrammarType.Parser,
            Options: null,
            Actions: [],
            Imports: [],
            Modes: [new LexerMode("DEFAULT_MODE", [])],
            DeclaredTokens: new HashSet<string>(StringComparer.Ordinal),
            DeclaredChannels: new HashSet<string>(StringComparer.Ordinal) { "DEFAULT_CHANNEL", "HIDDEN" },
            ExtensionBindings: [],
            ParserRules: [parserRule],
            RootRule: parserRule);

        Assert.ThrowsException<GrammarValidationException>(() => RuleResolver.Resolve(definition, diagnostics));
        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.UnknownRuleReference.Code));
    }

    [TestMethod]
    public void ParserEngine_TrailingTokens_EmitsWarningDiagnostic()
    {
        var diagnostics = new DiagnosticBag();
        var definition = Antlr4GrammarConverter.Parse("""
            grammar Exp;
            eval   : Number ;
            Number : ('0'..'9')+ ;
            WS     : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """, diagnostics);

        var lexer = new LexerEngine(definition);
        var tokens = lexer.Tokenize(new StringReader("1 2"), diagnostics: diagnostics).ToList();
        var parser = new ParserEngine(definition);
        var node = parser.Parse(tokens, diagnostics: diagnostics);

        Assert.IsInstanceOfType<ErrorNode>(node);
        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.TrailingTokensAfterParse.Code));
    }

    [TestMethod]
    /// <summary>
    /// Verifies that backtracking diagnostics remain orchestration-oriented and do not
    /// escalate to parse-failure diagnostics when the final parse succeeds.
    /// </summary>
    public void ParserEngine_BacktrackingDiagnostic_RemainsOrchestrationOnly_OnSuccessfulParse()
    {
        var diagnostics = new DiagnosticBag();
        var rule = new Rule(
            "start",
            0,
            false,
            new Alternation([
                new Alternative(0, Associativity.Left, new Sequence([new LiteralMatch("a"), new LiteralMatch("b")])),
                new Alternative(1, Associativity.Left, new LiteralMatch("a"))
            ]));
        var definition = RuleResolver.Resolve(new ParserDefinition(
            Name: "DiagBacktracking",
            Type: GrammarType.Parser,
            Options: null,
            Actions: [],
            Imports: [],
            Modes: [new LexerMode("DEFAULT_MODE", [])],
            DeclaredTokens: new HashSet<string>(StringComparer.Ordinal),
            DeclaredChannels: new HashSet<string>(StringComparer.Ordinal) { "DEFAULT_CHANNEL", "HIDDEN" },
            ExtensionBindings: [],
            ParserRules: [rule],
            RootRule: rule));

        var parser = new ParserEngine(definition);
        var result = parser.Parse([new Token(new SourceSpan(0, 1), "A", "DEFAULT_MODE", "DEFAULT_CHANNEL", "a")], diagnostics: diagnostics);

        Assert.IsInstanceOfType<ParserNode>(result);
        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.BacktrackingUsed.Code));
        Assert.IsFalse(diagnostics.Any(d => d.Code == ParserDiagnostics.ParseFailure.Code));
    }

    [TestMethod]
    /// <summary>
    /// Verifies that branch-equivalence orchestration remains non-authoritative for
    /// parser failure semantics on a successful parse.
    /// </summary>
    public void ParserEngine_BranchEquivalenceObservations_DoNotCreateParseFailure_OnSuccessfulParse()
    {
        var scheduler = new AlternativeScheduler();
        var diagnostics = new DiagnosticBag();
        var rule = new Rule(
            "start",
            0,
            false,
            new Alternation([
                new Alternative(0, Associativity.Left, new LiteralMatch("a"), "X"),
                new Alternative(1, Associativity.Left, new LiteralMatch("a"), "X"),
                new Alternative(2, Associativity.Left, new LiteralMatch("a"), "Y")
            ]));
        var result = scheduler.Run(
            rule,
            rule.Content.Alternatives,
            originInputPosition: 0,
            minimumPrecedence: 0,
            diagnostics,
            precomputedDescriptors: null,
            precomputedContinuationMetadata: Enumerable.Range(0, rule.Content.Alternatives.Count).Select(i => new ParserContinuationDescriptor(new ParserContinuationKey(rule.Name, i, 0), ParserContinuationCategory.Sequential, null, false)).ToArray(),
            precomputedLookaheadProbes: Enumerable.Range(0, rule.Content.Alternatives.Count).Select(static _ => new ParserLookaheadProbeResult(ParserLookaheadProbeKind.Unknown, null, null)).ToArray(),
            precomputedSharedPrefixCandidates: [],
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
        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.AmbiguousAlternativesPruned.Code));
        Assert.IsFalse(diagnostics.Any(d => d.Code == ParserDiagnostics.ParseFailure.Code));
    }



    [TestMethod]
    /// <summary>
    /// Verifies that semantic-predicate compatibility diagnostics remain deterministic
    /// and observable independently from final parse success.
    /// </summary>
    public void SemanticPredicateCompatibilityDiagnostic_IsDeterministic_AndIndependentFromParseSuccess()
    {
        var diagnostics = new DiagnosticBag();
        var definition = Antlr4GrammarConverter.Parse("""
            grammar PredicateDiag;
            start : {canProceed}? 'a' ;
            """, diagnostics);

        var parser = new ParserEngine(definition);
        var node = parser.Parse([new Token(new SourceSpan(0, 1), "a", "DEFAULT_MODE", "DEFAULT_CHANNEL", "a")], diagnostics: diagnostics);

        Assert.IsInstanceOfType<ParserNode>(node);
        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.SemanticPredicateNotEnforced.Code));
    }

    [TestMethod]
    /// <summary>
    /// Verifies that unsupported-feature compatibility diagnostics remain observable
    /// independently of final parse success.
    /// </summary>
    public void UnsupportedFeatureDiagnostic_SurvivesIndependently_FromRuntimeParseOutcome()
    {
        var diagnostics = new DiagnosticBag();
        var definition = Antlr4GrammarConverter.Parse("""
            grammar G;
            options { language = Cpp; }
            start : 'a' ;
            """, diagnostics);

        var parser = new ParserEngine(definition);
        var node = parser.Parse([new Token(new SourceSpan(0, 1), "a", "DEFAULT_MODE", "DEFAULT_CHANNEL", "a")], diagnostics: diagnostics);

        Assert.IsInstanceOfType<ParserNode>(node);
        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.UnsupportedAntlrLanguageOptionIgnored.Code));
    }

    [TestMethod]
    public void ValidCase_EmitsNoErrorDiagnostics()
    {
        var diagnostics = new DiagnosticBag();
        var definition = Antlr4GrammarConverter.Parse("""
            grammar Exp;
            eval   : Number ;
            Number : ('0'..'9')+ ;
            WS     : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """, diagnostics);

        var lexer = new LexerEngine(definition);
        var tokens = lexer.Tokenize(new StringReader("1"), diagnostics: diagnostics).ToList();
        var parser = new ParserEngine(definition);
        var node = parser.Parse(tokens, diagnostics: diagnostics);

        Assert.IsFalse(node is ErrorNode);
        Assert.IsFalse(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error));
    }

    [TestMethod]
    public void RuntimeEngines_WithNullDiagnostics_DoNotThrow()
    {
        var definition = Antlr4GrammarConverter.Parse("""
            grammar Exp;
            eval   : Number ;
            Number : ('0'..'9')+ ;
            WS     : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """);

        var lexer = new LexerEngine(definition);
        var tokens = lexer.Tokenize(new StringReader("1 ?"), diagnostics: null).ToList();
        var parser = new ParserEngine(definition);
        var node = parser.Parse(tokens, diagnostics: null);

        Assert.IsNotNull(node);
        Assert.IsTrue(tokens.Any(t => t.RuleName == "ERROR"));
    }

    [TestMethod]
    public void RuleResolver_WithNullDiagnostics_ThrowsExpectedExceptionOnly()
    {
        var parserRule = new Rule(
            "start",
            0,
            false,
            new Alternation([
                new Alternative(0, Associativity.Left, new RuleRef("MissingRule"))
            ]));

        var definition = new ParserDefinition(
            Name: "G",
            Type: GrammarType.Parser,
            Options: null,
            Actions: [],
            Imports: [],
            Modes: [new LexerMode("DEFAULT_MODE", [])],
            DeclaredTokens: new HashSet<string>(StringComparer.Ordinal),
            DeclaredChannels: new HashSet<string>(StringComparer.Ordinal) { "DEFAULT_CHANNEL", "HIDDEN" },
            ExtensionBindings: [],
            ParserRules: [parserRule],
            RootRule: parserRule);

        Assert.ThrowsException<GrammarValidationException>(() => RuleResolver.Resolve(definition, diagnostics: null));
    }

    [TestMethod]
    public void GeneratorParser_WithNullDiagnostics_DoesNotThrow()
    {
        var tokens = new G4Tokenizer("grammar G; tokens { A }; rule : 'a' ;").Tokenize();
        var parser = new G4Parser(tokens, diagnostics: null);
        var grammar = parser.Parse();

        Assert.IsNotNull(grammar);
        Assert.AreEqual("G", grammar.Name);
    }
}
