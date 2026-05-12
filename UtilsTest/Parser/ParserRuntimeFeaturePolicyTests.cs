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
    [TestMethod]
    public void ParserRuntimeFeaturePolicy_DefaultPreservesCurrentBehavior()
    {
        Assert.IsInstanceOfType<DefaultSemanticPredicateEvaluator>(ParserRuntimeFeaturePolicy.Default.SemanticPredicateEvaluator);
        Assert.IsInstanceOfType<DefaultParserActionExecutor>(ParserRuntimeFeaturePolicy.Default.ParserActionExecutor);
    }

    [TestMethod]
    public void ParserEngine_ExistingOverloads_ProduceEquivalentPolicies()
    {
        var definition = CreateMinimalDefinition();
        var evaluator = new ConstantSemanticPredicateEvaluator(SemanticPredicateEvaluationResult.Satisfied);
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

    [TestMethod]
    public void ParserRuntimeFeaturePolicy_CustomStrategies_ArePropagated()
    {
        var definition = CreateMinimalDefinition();
        var evaluator = new ConstantSemanticPredicateEvaluator(SemanticPredicateEvaluationResult.Satisfied);
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

    private static ISemanticPredicateEvaluator GetSemanticPredicateEvaluator(ParserEngine parser)
    {
        var field = typeof(ParserEngine).GetField("_semanticPredicateEvaluator", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        return (ISemanticPredicateEvaluator)field.GetValue(parser)!;
    }

    private static IParserActionExecutor GetParserActionExecutor(ParserEngine parser)
    {
        var field = typeof(ParserEngine).GetField("_parserActionExecutor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        return (IParserActionExecutor)field.GetValue(parser)!;
    }

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

    private sealed class ConstantSemanticPredicateEvaluator : ISemanticPredicateEvaluator
    {
        private readonly SemanticPredicateEvaluationResult _result;

        public ConstantSemanticPredicateEvaluator(SemanticPredicateEvaluationResult result)
        {
            _result = result;
        }

        public SemanticPredicateEvaluationResult Evaluate(SemanticPredicateEvaluationContext context)
        {
            return _result;
        }
    }

    private sealed class ConstantParserActionExecutor : IParserActionExecutor
    {
        private readonly ParserActionExecutionResult _result;

        public ConstantParserActionExecutor(ParserActionExecutionResult result)
        {
            _result = result;
        }

        public ParserActionExecutionResult Execute(ParserActionExecutionContext context)
        {
            return _result;
        }
    }
}
