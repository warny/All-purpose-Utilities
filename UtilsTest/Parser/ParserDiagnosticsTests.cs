using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using Utils.Parser.Bootstrap;
using Utils.Parser.Diagnostics;
using Utils.Parser.Generators.Internal;
using Utils.Parser.Model;
using Utils.Parser.Resolution;
using Utils.Parser.Runtime;

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
            ParserRules: [parserRule],
            RootRule: parserRule));

        var parser = new ParserEngine(definition);
        parser.Parse([], diagnostics: diagnostics);

        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.SemanticPredicateNotEnforced.Code));
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
        var tokens = lexer.Tokenize(new StringCharStream("1 2"), diagnostics).ToList();
        var parser = new ParserEngine(definition);
        var node = parser.Parse(tokens, diagnostics: diagnostics);

        Assert.IsInstanceOfType<ErrorNode>(node);
        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.TrailingTokensAfterParse.Code));
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
        var tokens = lexer.Tokenize(new StringCharStream("1"), diagnostics).ToList();
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
        var tokens = lexer.Tokenize(new StringCharStream("1 ?"), diagnostics: null).ToList();
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
