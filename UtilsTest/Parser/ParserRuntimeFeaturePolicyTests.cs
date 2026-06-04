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
        Assert.AreSame(NullParserExecutionStateManager.Instance, ParserRuntimeFeaturePolicy.Default.ExecutionStateManager);
    }

    /// <summary>
    /// Verifies that runtime feature policy configuration maps runtime strategies exactly as expected.
    /// </summary>
    [TestMethod]
    public void ParserEngine_RuntimeFeaturePolicy_ProducesEquivalentPolicies()
    {
        var definition = CreateMinimalDefinition();
        var evaluator = new ConstantSemanticPredicateEvaluator(SemanticPredicateEvaluationOutcome.Satisfied);
        var executor = new ConstantParserActionExecutor(ParserActionExecutionOutcome.Executed);

        var evaluatorOnly = new ParserEngine(definition, ParserRuntimeFeaturePolicy.Default with { SemanticPredicateEvaluator = evaluator });
        var executorOnly = new ParserEngine(definition, ParserRuntimeFeaturePolicy.Default with { ParserActionExecutor = executor });
        var combined = new ParserEngine(definition, ParserRuntimeFeaturePolicy.Default with { SemanticPredicateEvaluator = evaluator, ParserActionExecutor = executor });

        Assert.AreSame(evaluator, GetSemanticPredicateEvaluator(evaluatorOnly));
        Assert.IsInstanceOfType<DefaultParserActionExecutor>(GetParserActionExecutor(evaluatorOnly));
        Assert.AreSame(NullParserExecutionStateManager.Instance, GetExecutionStateManager(evaluatorOnly));

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
        var executor = new ConstantParserActionExecutor(ParserActionExecutionOutcome.Executed);
        var policy = ParserRuntimeFeaturePolicy.Default with
        {
            SemanticPredicateEvaluator = evaluator,
            ParserActionExecutor = executor
        };

        var parser = new ParserEngine(definition, policy);

        Assert.AreSame(evaluator, GetSemanticPredicateEvaluator(parser));
        Assert.AreSame(executor, GetParserActionExecutor(parser));
        Assert.AreSame(NullParserExecutionStateManager.Instance, GetExecutionStateManager(parser));
    }

    /// <summary>
    /// Verifies that the default policy exposes the singleton no-op execution-state manager.
    /// </summary>
    [TestMethod]
    public void DefaultPolicy_UsesNullExecutionStateManager()
    {
        var manager = ParserRuntimeFeaturePolicy.Default.ExecutionStateManager;

        Assert.IsNotNull(manager);
        Assert.AreSame(NullParserExecutionStateManager.Instance, manager);
        var snapshot = manager.Capture();
        Assert.IsNotNull(snapshot);
        manager.Restore(snapshot);
        Assert.AreEqual(ParserExecutionStateKey.Stateless, manager.GetCurrentStateKey());
    }

    /// <summary>
    /// Verifies that parser completed-result memoization distinguishes the same invocation by semantic execution-state key.
    /// </summary>
    [TestMethod]
    public void ParserEngine_CompletedResultCache_DistinguishesExecutionStateKeys()
    {
        var stateManager = new MutableParserExecutionStateManager();
        const string grammar = """
            grammar P;
            start : target | { bump } target ;
            target : { stateIsOne }? A ;
            A : 'a' ;
            """;
        var definition = Antlr4GrammarConverter.Parse(grammar);
        var policy = ParserRuntimeFeaturePolicy.Default with
        {
            ExecutionStateManager = stateManager,
            SemanticPredicateEvaluator = new StateKeyPredicateEvaluator(stateManager),
            ParserActionExecutor = new StateBumpingActionExecutor(stateManager)
        };
        var parser = new ParserEngine(definition, policy);

        var result = parser.Parse([new Token(new SourceSpan(0, 1), "A", "DEFAULT_MODE", "DEFAULT_CHANNEL", "a")]);

        Assert.IsInstanceOfType<ParserNode>(result);
        Assert.AreEqual(1UL, stateManager.GetCurrentStateKey().Value);
    }

    /// <summary>
    /// Verifies that stateless invocation keys retain the old rule-position-precedence equality behavior.
    /// </summary>
    [TestMethod]
    public void RuleInvocationKey_DefaultConstructor_UsesStatelessExecutionStateKey()
    {
        var oldShape = new RuleInvocationKey("rule", 3, 0);
        var explicitStateless = new RuleInvocationKey("rule", 3, 0, ParserExecutionStateKey.Stateless);
        var stateful = new RuleInvocationKey("rule", 3, 0, new ParserExecutionStateKey(1));

        Assert.AreEqual(explicitStateless, oldShape);
        Assert.AreNotEqual(oldShape, stateful);
    }

    /// <summary>
    /// Verifies that parser construction rejects a policy whose execution-state manager was explicitly nulled.
    /// </summary>
    [TestMethod]
    public void ParserEngine_RejectsNullExecutionStateManager()
    {
        var definition = CreateMinimalDefinition();
        var invalidPolicy = ParserRuntimeFeaturePolicy.Default with { ExecutionStateManager = null! };

        Assert.ThrowsException<ArgumentNullException>(() => new ParserEngine(definition, invalidPolicy));
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
    /// Gets the parser execution-state manager instance currently held by the parser engine.
    /// </summary>
    /// <param name="parser">Parser engine to inspect.</param>
    /// <returns>Configured parser execution-state manager.</returns>
    private static IParserExecutionStateManager GetExecutionStateManager(ParserEngine parser)
    {
        var field = typeof(ParserEngine).GetField("_executionStateManager", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        return (IParserExecutionStateManager)field.GetValue(parser)!;
    }

    /// <summary>
    /// Mutable test execution-state manager exposing a manually controlled state key.
    /// </summary>
    private sealed class MutableParserExecutionStateManager : IParserExecutionStateManager
    {
        /// <summary>Current mutable state value.</summary>
        private ulong _value;

        /// <summary>Captures the current numeric state value.</summary>
        /// <returns>The current state value boxed as an opaque snapshot.</returns>
        public object Capture()
        {
            return _value;
        }

        /// <summary>Restores the numeric state value from a snapshot.</summary>
        /// <param name="snapshot">Snapshot produced by <see cref="Capture"/>.</param>
        public void Restore(object snapshot)
        {
            _value = (ulong)snapshot;
        }

        /// <summary>Gets the current parser execution-state key.</summary>
        /// <returns>The current state key.</returns>
        public ParserExecutionStateKey GetCurrentStateKey()
        {
            return new ParserExecutionStateKey(_value);
        }

        /// <summary>Advances the mutable semantic state value.</summary>
        public void Bump()
        {
            _value++;
        }
    }

    /// <summary>
    /// Predicate evaluator that accepts only after the test state key changes.
    /// </summary>
    /// <param name="stateManager">State manager read by the evaluator.</param>
    private sealed class StateKeyPredicateEvaluator(MutableParserExecutionStateManager stateManager) : ISemanticPredicateEvaluator
    {
        /// <summary>Evaluates the predicate from the current state key.</summary>
        /// <param name="context">Predicate evaluation context.</param>
        /// <returns>Satisfied when the state key is one; otherwise rejected.</returns>
        public SemanticPredicateEvaluationOutcome Evaluate(SemanticPredicateEvaluationContext context)
        {
            return stateManager.GetCurrentStateKey().Value == 1
                ? SemanticPredicateEvaluationOutcome.Satisfied
                : SemanticPredicateEvaluationOutcome.Rejected;
        }
    }

    /// <summary>
    /// Action executor that mutates the test state key.
    /// </summary>
    /// <param name="stateManager">State manager mutated by the executor.</param>
    private sealed class StateBumpingActionExecutor(MutableParserExecutionStateManager stateManager) : IParserActionExecutor
    {
        /// <summary>Executes a parser action by bumping semantic state.</summary>
        /// <param name="context">Action execution context.</param>
        /// <returns>An executed action outcome.</returns>
        public ParserActionExecutionOutcome Execute(ParserActionExecutionContext context)
        {
            stateManager.Bump();
            return ParserActionExecutionOutcome.Executed;
        }
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
        private readonly ParserActionExecutionOutcome _result;

        /// <summary>
        /// Initializes the executor with the configured action execution result.
        /// </summary>
        /// <param name="result">Result returned for all action executions.</param>
        public ConstantParserActionExecutor(ParserActionExecutionOutcome result)
        {
            _result = result;
        }

        /// <summary>
        /// Returns the configured parser action execution result.
        /// </summary>
        /// <param name="context">Parser action execution context.</param>
        /// <returns>The configured result.</returns>
        public ParserActionExecutionOutcome Execute(ParserActionExecutionContext context)
        {
            return _result;
        }
    }
}
