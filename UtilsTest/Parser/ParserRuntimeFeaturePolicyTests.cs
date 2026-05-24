using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Bootstrap;
using Utils.Parser.Diagnostics;
using Utils.Parser.Model;
using Utils.Parser.Resolution;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Verifies runtime feature policy defaults and policy propagation in parser runtime entry points.
/// </summary>
[TestClass]
public class ParserRuntimeFeaturePolicyTests
{
    /// <summary>
    /// Verifies that the default runtime feature policy preserves the existing conservative strategies.
    /// </summary>
    [TestMethod]
    public void ParserRuntimeFeaturePolicy_DefaultPreservesCurrentBehavior()
    {
        Assert.IsInstanceOfType<DefaultSemanticPredicateEvaluator>(ParserRuntimeFeaturePolicy.Default.SemanticPredicateEvaluator);
        Assert.IsInstanceOfType<DefaultParserActionExecutor>(ParserRuntimeFeaturePolicy.Default.ParserActionExecutor);
    }

    /// <summary>
    /// Verifies that existing constructor overloads map runtime strategies exactly as before.
    /// </summary>
    [TestMethod]
    public void ParserEngine_ExistingOverloads_ProduceEquivalentPolicies()
    {
        var definition = CreateMinimalDefinition();
        var evaluator = new ConstantSemanticPredicateEvaluator(SemanticPredicateEvaluationOutcome.Satisfied);
        var executor = new ConstantParserActionExecutor(ParserActionExecutionResult.Executed);

        var evaluatorOnly = new ParserEngine(definition, evaluator);
        var executorOnly = new ParserEngine(definition, executor);
        var combined = new ParserEngine(definition, evaluator, executor);

        Assert.AreSame(evaluator, GetSemanticPredicateEvaluator(evaluatorOnly));
        Assert.IsInstanceOfType<DefaultParserActionExecutor>(GetParserActionExecutor(evaluatorOnly));

        Assert.IsInstanceOfType<DefaultSemanticPredicateEvaluator>(GetSemanticPredicateEvaluator(executorOnly));
        Assert.AreSame(executor, GetParserActionExecutor(executorOnly));

        Assert.AreSame(evaluator, GetSemanticPredicateEvaluator(combined));
        Assert.AreSame(executor, GetParserActionExecutor(combined));
    }

    /// <summary>
    /// Verifies that explicitly configured runtime strategy instances are propagated to the parser engine.
    /// </summary>
    [TestMethod]
    public void ParserRuntimeFeaturePolicy_CustomStrategies_ArePropagated()
    {
        var definition = CreateMinimalDefinition();
        var evaluator = new ConstantSemanticPredicateEvaluator(SemanticPredicateEvaluationOutcome.Satisfied);
        var executor = new ConstantParserActionExecutor(ParserActionExecutionResult.Executed);
        var policy = ParserRuntimeFeaturePolicy.Default with
        {
            SemanticPredicateEvaluator = evaluator,
            ParserActionExecutor = executor
        };

        var parser = new ParserEngine(definition, policy);

        Assert.AreSame(evaluator, GetSemanticPredicateEvaluator(parser));
        Assert.AreSame(executor, GetParserActionExecutor(parser));
    }

    /// <summary>
    /// Verifies that using the explicit default runtime policy does not change emitted diagnostics.
    /// </summary>
    [TestMethod]
    public void RuntimePolicy_DoesNotAlterDefaultDiagnostics()
    {
        const string grammar = """
            grammar P;
            start : {canProceed}? A {notify();} ;
            A : 'a' ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """;

        var definition = Antlr4GrammarConverter.Parse(grammar);
        var tokens = new CompiledGrammar(definition).Tokenize("a");

        var diagnosticsWithDefaultConstructor = new DiagnosticBag();
        var diagnosticsWithDefaultPolicy = new DiagnosticBag();

        var parserWithDefaultConstructor = new ParserEngine(definition);
        var parserWithDefaultPolicy = new ParserEngine(definition, ParserRuntimeFeaturePolicy.Default);

        parserWithDefaultConstructor.Parse(tokens, diagnostics: diagnosticsWithDefaultConstructor);
        parserWithDefaultPolicy.Parse(tokens, diagnostics: diagnosticsWithDefaultPolicy);

        CollectionAssert.AreEqual(
            diagnosticsWithDefaultConstructor.Select(static diagnostic => diagnostic.Code).ToArray(),
            diagnosticsWithDefaultPolicy.Select(static diagnostic => diagnostic.Code).ToArray());
    }

    /// <summary>
    /// Gets the semantic predicate evaluator instance currently held by the parser engine.
    /// </summary>
    /// <param name="parser">Parser engine to inspect.</param>
    /// <returns>Configured semantic predicate evaluator.</returns>
    private static ISemanticPredicateEvaluator GetSemanticPredicateEvaluator(ParserEngine parser)
    {
        var field = typeof(ParserEngine).GetField("_semanticPredicateEvaluator", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        return (ISemanticPredicateEvaluator)field.GetValue(parser)!;
    }

    /// <summary>
    /// Gets the parser action executor instance currently held by the parser engine.
    /// </summary>
    /// <param name="parser">Parser engine to inspect.</param>
    /// <returns>Configured parser action executor.</returns>
    private static IParserActionExecutor GetParserActionExecutor(ParserEngine parser)
    {
        var field = typeof(ParserEngine).GetField("_parserActionExecutor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        return (IParserActionExecutor)field.GetValue(parser)!;
    }

    /// <summary>
    /// Creates a minimal resolved parser definition suitable for constructor wiring tests.
    /// </summary>
    /// <returns>A resolved parser definition.</returns>
    private static ParserDefinition CreateMinimalDefinition()
    {
        var startRule = new Rule("start", 0, false, new Alternation([new Alternative(0, Associativity.Left, new Sequence([]))]));
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
    /// Semantic predicate evaluator used by tests to return a deterministic result.
    /// </summary>
    private sealed class ConstantSemanticPredicateEvaluator : ISemanticPredicateEvaluator
    {
        private readonly SemanticPredicateEvaluationOutcome _result;

        /// <summary>
        /// Initializes the evaluator with the configured evaluation result.
        /// </summary>
        /// <param name="result">Result returned for all predicate evaluations.</param>
        public ConstantSemanticPredicateEvaluator(SemanticPredicateEvaluationOutcome result)
        {
            _result = result;
        }

        /// <summary>
        /// Returns the configured semantic predicate evaluation result.
        /// </summary>
        /// <param name="context">Semantic predicate evaluation context.</param>
        /// <returns>The configured result.</returns>
        public SemanticPredicateEvaluationOutcome Evaluate(SemanticPredicateEvaluationContext context)
        {
            return _result;
        }
    }

    /// <summary>
    /// Parser action executor used by tests to return a deterministic result.
    /// </summary>
    private sealed class ConstantParserActionExecutor : IParserActionExecutor
    {
        private readonly ParserActionExecutionResult _result;

        /// <summary>
        /// Initializes the executor with the configured action execution result.
        /// </summary>
        /// <param name="result">Result returned for all action executions.</param>
        public ConstantParserActionExecutor(ParserActionExecutionResult result)
        {
            _result = result;
        }

        /// <summary>
        /// Returns the configured parser action execution result.
        /// </summary>
        /// <param name="context">Parser action execution context.</param>
        /// <returns>The configured result.</returns>
        public ParserActionExecutionResult Execute(ParserActionExecutionContext context)
        {
            return _result;
        }
    }
}
