using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using Utils.Parser.Model;
using Utils.Parser.Runtime;
using Utils.Parser.Resolution;
using Utils.Parser.Diagnostics;
using Utils.Parser.Bootstrap;

namespace UtilsTest.Parser;

/// <summary>
/// Verifies parser behavior with injected semantic predicate evaluators.
/// </summary>
[TestClass]
public class ParserEngineSemanticPredicateEvaluatorTests
{
    [TestMethod]
    public void Parse_WhenEvaluatorRejectsPredicate_ReturnsErrorNode()
    {
        var startRule = CreateStartRuleWithPredicate(new ValidatingPredicate("canProceed"));
        var definition = CreateDefinition(startRule);
        var parser = new ParserEngine(definition, new ConstantSemanticPredicateEvaluator(SemanticPredicateEvaluationResult.Rejected));

        var result = parser.Parse([]);

        Assert.IsInstanceOfType<ErrorNode>(result);
    }

    [TestMethod]
    public void Parse_WhenEvaluatorSatisfiesPredicate_ParsesSuccessfully()
    {
        var startRule = CreateStartRuleWithPredicate(new GatingPredicate("canProceed"));
        var definition = CreateDefinition(startRule);
        var parser = new ParserEngine(definition, new ConstantSemanticPredicateEvaluator(SemanticPredicateEvaluationResult.Satisfied));

        var result = parser.Parse([]);

        Assert.IsInstanceOfType<ParserNode>(result);
    }

    [TestMethod]
    public void Parse_WhenEvaluatorReturnsNotEvaluated_EmitsLegacyDiagnostic()
    {
        var startRule = CreateStartRuleWithPredicate(new ValidatingPredicate("canProceed"));
        var definition = CreateDefinition(startRule);
        var parser = new ParserEngine(definition, new ConstantSemanticPredicateEvaluator(SemanticPredicateEvaluationResult.NotEvaluated));
        var diagnostics = new DiagnosticBag();

        parser.Parse([], diagnostics: diagnostics);

        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.SemanticPredicateNotEnforced.Code));
    }

    [TestMethod]
    public void Parser_DefaultSemanticPredicatePolicy_EmitsUP1006()
    {
        var diagnostics = new DiagnosticBag();
        var definition = Antlr4GrammarConverter.Parse(
            """
            grammar P;
            start
                : {allow()}? A
                ;

            A : 'a';
            """,
            diagnostics: diagnostics);
        var grammar = new CompiledGrammar(definition);
        var result = grammar.Parse("a");
        var semanticPredicateDiagnostics = diagnostics
            .Where(d => d.Code == ParserDiagnostics.SemanticPredicateNotEnforced.Code)
            .ToList();

        Assert.IsInstanceOfType<ParserNode>(result);
        Assert.AreEqual(1, semanticPredicateDiagnostics.Count);
    }

    [TestMethod]
    public void Parser_CustomPredicateEvaluator_CanRejectBranch()
    {
        var definition = Antlr4GrammarConverter.Parse(
            """
            grammar P;
            start
                : {allow()}? A
                ;

            A : 'a';
            """,
            diagnostics: null);

        var parser = new ParserEngine(definition, new ConstantSemanticPredicateEvaluator(SemanticPredicateEvaluationResult.Rejected));
        var lexer = new LexerEngine(definition);
        var result = parser.Parse(lexer.Tokenize(new StringReader("a")));

        Assert.IsInstanceOfType<ErrorNode>(result);
    }

    [TestMethod]
    public void Parser_Precpred_DoesNotEmitUP1006()
    {
        var diagnostics = new DiagnosticBag();
        var startRule = CreateStartRuleWithPredicate(new PrecedencePredicate(2));
        var definition = CreateDefinition(startRule);
        var parser = new ParserEngine(definition);
        parser.Parse([], diagnostics: diagnostics);

        Assert.IsFalse(diagnostics.Any(d => d.Code == ParserDiagnostics.SemanticPredicateNotEnforced.Code));
    }

    [TestMethod]
    public void Parse_WithSecondAlternativePredicate_ProvidesCorrectAlternativeAndElementIndices()
    {
        var tokenRuleA = new Rule("A", 0, true, new Alternation([new Alternative(0, Associativity.Left, new LiteralMatch("a"))]));
        var tokenRuleB = new Rule("B", 1, true, new Alternation([new Alternative(0, Associativity.Left, new LiteralMatch("b"))]));
        var startRule = new Rule(
            "start",
            0,
            false,
            new Alternation([
                new Alternative(0, Associativity.Left, new RuleRef("B")),
                new Alternative(1, Associativity.Left, new Sequence([
                    new ValidatingPredicate("canProceed"),
                    new RuleRef("A")
                ]))
            ]));

        var definition = RuleResolver.Resolve(new ParserDefinition(
            Name: "G",
            Type: GrammarType.Combined,
            Options: null,
            Actions: [],
            Imports: [],
            Modes: [new LexerMode("DEFAULT_MODE", [tokenRuleA, tokenRuleB])],
            DeclaredTokens: new HashSet<string>(StringComparer.Ordinal),
            DeclaredChannels: new HashSet<string>(StringComparer.Ordinal) { "DEFAULT_CHANNEL", "HIDDEN" },
            ExtensionBindings: [],
            ParserRules: [startRule],
            RootRule: startRule));

        var observer = new ObservingSemanticPredicateEvaluator(SemanticPredicateEvaluationResult.Satisfied);
        var tokens = new List<Token>
        {
            new(new SourceSpan(0, 1, 1, 1), "A", "DEFAULT_MODE", "DEFAULT_CHANNEL", "a")
        };
        var parser = new ParserEngine(definition, observer);

        var result = parser.Parse(tokens);

        Assert.IsInstanceOfType<ParserNode>(result);
        Assert.AreEqual(1, observer.LastContext?.AlternativeIndex);
        Assert.AreEqual(0, observer.LastContext?.ElementIndex);
    }

    [TestMethod]
    public void CompileAndParse_FromAntlrText_UsesInjectedEvaluator()
    {
        var definition = Antlr4GrammarConverter.Parse(
            """
            grammar P;
            start : {canProceed}? A ;
            A : 'a' ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """,
            diagnostics: null);

        var grammar = new CompiledGrammar(
            definition,
            new ConstantSemanticPredicateEvaluator(SemanticPredicateEvaluationResult.Rejected));

        var result = grammar.Parse("a");

        Assert.IsInstanceOfType<ErrorNode>(result);
    }

    private static Rule CreateStartRuleWithPredicate(RuleContent predicate)
    {
        return new Rule(
            "start",
            0,
            false,
            new Alternation([
                new Alternative(
                    0,
                    Associativity.Left,
                    new Sequence([
                        predicate
                    ]))
            ]));
    }

    private static ParserDefinition CreateDefinition(Rule startRule)
    {
        return RuleResolver.Resolve(new ParserDefinition(
            Name: "G",
            Type: GrammarType.Parser,
            Options: null,
            Actions: [],
            Imports: [],
            Modes: [new LexerMode("DEFAULT_MODE", [])],
            DeclaredTokens: new HashSet<string>(StringComparer.Ordinal),
            DeclaredChannels: new HashSet<string>(StringComparer.Ordinal) { "DEFAULT_CHANNEL", "HIDDEN" },
            ExtensionBindings: [],
            ParserRules: [startRule],
            RootRule: startRule));
    }

    /// <summary>
    /// Test evaluator returning a deterministic preconfigured result.
    /// </summary>
    private sealed class ConstantSemanticPredicateEvaluator : ISemanticPredicateEvaluator
    {
        private readonly SemanticPredicateEvaluationResult _result;

        /// <summary>
        /// Initializes the evaluator.
        /// </summary>
        /// <param name="result">Result returned by <see cref="Evaluate"/>.</param>
        public ConstantSemanticPredicateEvaluator(SemanticPredicateEvaluationResult result)
        {
            _result = result;
        }

        /// <inheritdoc />
        public SemanticPredicateEvaluationResult Evaluate(SemanticPredicateEvaluationContext context)
        {
            return _result;
        }
    }

    /// <summary>
    /// Test evaluator that captures the latest context for assertions.
    /// </summary>
    private sealed class ObservingSemanticPredicateEvaluator : ISemanticPredicateEvaluator
    {
        private readonly SemanticPredicateEvaluationResult _result;

        /// <summary>
        /// Initializes the evaluator.
        /// </summary>
        /// <param name="result">Result returned by <see cref="Evaluate"/>.</param>
        public ObservingSemanticPredicateEvaluator(SemanticPredicateEvaluationResult result)
        {
            _result = result;
        }

        /// <summary>
        /// Gets the last context passed to <see cref="Evaluate"/>.
        /// </summary>
        public SemanticPredicateEvaluationContext? LastContext { get; private set; }

        /// <inheritdoc />
        public SemanticPredicateEvaluationResult Evaluate(SemanticPredicateEvaluationContext context)
        {
            LastContext = context;
            return _result;
        }
    }
}
