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
        Assert.AreSame(NullParserRuleLifecycleExecutor.Instance, ParserRuntimeFeaturePolicy.Default.RuleLifecycleExecutor);
        Assert.AreSame(NullParserRuleInvocationFrameManager.Instance, ParserRuntimeFeaturePolicy.Default.RuleInvocationFrameManager);
    }

    /// <summary>
    /// Verifies that the null lifecycle executor singleton is valid and accepts calls without throwing.
    /// </summary>
    [TestMethod]
    public void NullParserRuleLifecycleExecutor_Execute_DoesNotThrow()
    {
        var executor = NullParserRuleLifecycleExecutor.Instance;
        var context = new ParserRuleLifecycleContext("start", 0);

        executor.Execute(ParserRuleLifecyclePhase.Init, "start", context);
        executor.Execute(ParserRuleLifecyclePhase.After, "start", context);
    }

    /// <summary>
    /// Verifies that the null lifecycle executor rejects a null rule name.
    /// </summary>
    [TestMethod]
    public void NullParserRuleLifecycleExecutor_Execute_RejectsNullRuleName()
    {
        var executor = NullParserRuleLifecycleExecutor.Instance;
        var context = new ParserRuleLifecycleContext("start", 0);

        Assert.ThrowsException<ArgumentNullException>(() => executor.Execute(ParserRuleLifecyclePhase.Init, null!, context));
    }

    /// <summary>
    /// Verifies that the null lifecycle executor rejects a null context.
    /// </summary>
    [TestMethod]
    public void NullParserRuleLifecycleExecutor_Execute_RejectsNullContext()
    {
        var executor = NullParserRuleLifecycleExecutor.Instance;

        Assert.ThrowsException<ArgumentNullException>(() => executor.Execute(ParserRuleLifecyclePhase.Init, "start", null!));
    }

    /// <summary>
    /// Verifies that the parser engine accepts a custom rule lifecycle executor through the runtime feature policy.
    /// </summary>
    [TestMethod]
    public void ParserEngine_RejectsNullRuleLifecycleExecutor()
    {
        var definition = CreateMinimalDefinition();
        var invalidPolicy = ParserRuntimeFeaturePolicy.Default with { RuleLifecycleExecutor = null! };

        Assert.ThrowsException<ArgumentNullException>(() => new ParserEngine(definition, invalidPolicy));
    }


    /// <summary>
    /// Verifies that the default rule invocation-frame manager creates inert frames and retains no current state.
    /// </summary>
    [TestMethod]
    public void NullParserRuleInvocationFrameManager_EnterExit_DoesNotRetainCurrentFrame()
    {
        var manager = NullParserRuleInvocationFrameManager.Instance;

        var frame = manager.Enter("start", 0);
        manager.Exit(frame, succeeded: true);

        Assert.AreEqual("start", frame.RuleName);
        Assert.AreEqual(0, frame.InputPosition);
        Assert.AreEqual(0, frame.Parameters.Count);
        Assert.AreEqual(0, frame.Locals.Count);
        Assert.AreEqual(0, frame.Returns.Count);
        Assert.IsNull(manager.Current);
    }

    /// <summary>
    /// Verifies that parser construction rejects a policy whose invocation-frame manager was explicitly nulled.
    /// </summary>
    [TestMethod]
    public void ParserEngine_RejectsNullRuleInvocationFrameManager()
    {
        var definition = CreateMinimalDefinition();
        var invalidPolicy = ParserRuntimeFeaturePolicy.Default with { RuleInvocationFrameManager = null! };

        Assert.ThrowsException<ArgumentNullException>(() => new ParserEngine(definition, invalidPolicy));
    }

    /// <summary>
    /// Verifies that parser rule invocation frames provide deterministic passive value accessors.
    /// </summary>
    [TestMethod]
    public void ParserRuleInvocationFrame_StoresPassiveMetadataValues()
    {
        var frame = new ParserRuleInvocationFrame("start", 2, new Dictionary<string, object?> { ["value"] = 42 });

        frame.SetLocal("counter", 3);
        frame.SetReturnValue("result", "ok");

        Assert.AreEqual(42, frame.GetParameter("value"));
        Assert.IsTrue(frame.TryGetParameter("value", out var parameter));
        Assert.AreEqual(42, parameter);
        Assert.IsTrue(frame.TryGetLocal("counter", out var local));
        Assert.AreEqual(3, local);
        Assert.IsTrue(frame.TryGetReturnValue("result", out var returnValue));
        Assert.AreEqual("ok", returnValue);
        Assert.IsFalse(frame.TryGetParameter("missing", out _));
    }

    /// <summary>
    /// Verifies that a custom invocation-frame manager can passively observe rule entry and exit.
    /// </summary>
    [TestMethod]
    public void ParserEngine_CustomRuleInvocationFrameManager_ObservesRuleEnterAndExit()
    {
        var definition = CreateMinimalDefinition();
        var manager = new RecordingRuleInvocationFrameManager();
        var parser = new ParserEngine(definition, ParserRuntimeFeaturePolicy.Default with { RuleInvocationFrameManager = manager });

        var result = parser.Parse([]);

        Assert.IsInstanceOfType<ParserNode>(result);
        Assert.IsNull(manager.Current);
        CollectionAssert.AreEqual(
            new[] { "enter:start:0", "exit:start:0:True" },
            manager.Events.ToArray());
    }

    /// <summary>
    /// Verifies that lifecycle contexts receive the passive invocation frame for the active rule.
    /// </summary>
    [TestMethod]
    public void ParserEngine_LifecycleContext_ExposesInvocationFrame()
    {
        var definition = CreateMinimalDefinition();
        var lifecycleExecutor = new RecordingRuleLifecycleExecutor();
        var parser = new ParserEngine(definition, ParserRuntimeFeaturePolicy.Default with { RuleLifecycleExecutor = lifecycleExecutor });

        parser.Parse([]);

        Assert.AreEqual(2, lifecycleExecutor.Contexts.Count);
        Assert.IsNotNull(lifecycleExecutor.Contexts[0].InvocationFrame);
        Assert.AreSame(lifecycleExecutor.Contexts[0].InvocationFrame, lifecycleExecutor.Contexts[1].InvocationFrame);
        Assert.AreEqual("start", lifecycleExecutor.Contexts[0].InvocationFrame!.RuleName);
    }

    /// <summary>
    /// Verifies that parser rule invocation frames can carry a passive descriptor without changing value stores.
    /// </summary>
    [TestMethod]
    public void ParserRuleInvocationFrame_CanCarryPassiveDescriptor()
    {
        var descriptor = new ParserRuleInvocationDescriptor
        {
            RuleName = "start",
            RawParameters = "int value",
            Parameters = [new ParserRuleParameterDescriptor { Name = "value", RawDeclaration = "int value" }]
        };

        var frame = new ParserRuleInvocationFrame("start", 2, new Dictionary<string, object?>(), descriptor);

        Assert.AreSame(descriptor, frame.Descriptor);
        Assert.AreEqual(0, frame.Parameters.Count);
        Assert.AreEqual(0, frame.Locals.Count);
        Assert.AreEqual(0, frame.Returns.Count);
    }

    /// <summary>
    /// Verifies that the null invocation-frame manager preserves descriptors without retaining state.
    /// </summary>
    [TestMethod]
    public void NullParserRuleInvocationFrameManager_Enter_PreservesDescriptor()
    {
        var descriptor = new ParserRuleInvocationDescriptor { RuleName = "start" };

        var frame = NullParserRuleInvocationFrameManager.Instance.Enter("start", 0, descriptor);

        Assert.AreSame(descriptor, frame.Descriptor);
        Assert.IsNull(NullParserRuleInvocationFrameManager.Instance.Current);
    }

    /// <summary>
    /// Verifies that a custom invocation-frame manager can observe passive rule descriptors on rule entry.
    /// </summary>
    [TestMethod]
    public void ParserEngine_CustomRuleInvocationFrameManager_ObservesRuleDescriptors()
    {
        var diagnostics = new DiagnosticBag();
        var definition = Antlr4GrammarConverter.Parse("""
            grammar P;
            start[int value] returns [int result]
              throws ParserException
              locals [int scratch]
              options { memoize=true; }
              : A ;
              catch [System.Exception ex] { }
              finally { }
            A : 'a' ;
            """, diagnostics);
        var manager = new RecordingRuleInvocationFrameManager();
        var parser = new ParserEngine(definition, ParserRuntimeFeaturePolicy.Default with { RuleInvocationFrameManager = manager });
        var tokens = new CompiledGrammar(definition).Tokenize("a");

        var result = parser.Parse(tokens);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        var descriptor = manager.EnteredDescriptors.Single(descriptor => descriptor?.RuleName == "start");
        Assert.IsNotNull(descriptor);
        StringAssert.Contains(descriptor!.RawParameters!, "int");
        StringAssert.Contains(descriptor.RawParameters!, "value");
        StringAssert.Contains(descriptor.RawReturnType!, "int");
        StringAssert.Contains(descriptor.RawReturnType!, "result");
        Assert.AreEqual(1, descriptor.Parameters.Count);
        Assert.AreEqual(1, descriptor.Returns.Count);
        Assert.AreEqual("true", descriptor.Options["memoize"]);
        StringAssert.Contains(descriptor.RawLocals!, "int scratch");
        Assert.AreEqual(1, descriptor.Locals.Count);
        Assert.AreEqual("scratch", descriptor.Locals[0].Name);
        StringAssert.Contains(descriptor.Locals[0].RawDeclaration, "int scratch");
        Assert.AreEqual(3, descriptor.Exceptions.Count);
        Assert.IsTrue(descriptor.Exceptions.Any(exception => exception.Kind == "throws" && exception.RawDeclaration == "ParserException"));
        Assert.IsTrue(descriptor.Exceptions.Any(exception => exception.Kind == "catch" && exception.RawDeclaration.Contains("System.Exception ex", StringComparison.Ordinal)));
        Assert.IsTrue(descriptor.Exceptions.Any(exception => exception.Kind == "finally"));
        Assert.IsTrue(diagnostics.Any(diagnostic => diagnostic.Code == ParserDiagnostics.RuleReturnsIgnored.Code));
        Assert.IsTrue(diagnostics.Any(diagnostic => diagnostic.Code == ParserDiagnostics.RuleLocalsIgnored.Code));
        Assert.IsTrue(diagnostics.Any(diagnostic => diagnostic.Code == ParserDiagnostics.RuleExceptionMetadataIgnored.Code));
        Assert.IsTrue(diagnostics.Any(diagnostic => diagnostic.Code == ParserDiagnostics.ParserRuleOptionsIgnored.Code));
    }

    /// <summary>
    /// Verifies that local descriptor splitting distinguishes generic delimiters from operators and ignores non-code text.
    /// </summary>
    [TestMethod]
    public void ParserRuleInvocationDescriptor_LocalSplitting_IgnoresOperatorsLiteralsAndComments()
    {
        var rule = new Rule(
            "start",
            0,
            false,
            new Alternation([]),
            Locals:
            [
                new RuleLocal("bool less = a < b, bool greater = x > (y), Dictionary<string, int> values, string text = \"a,b\", int /* ignored , < > */ count")
            ]);

        var descriptor = ParserRuleInvocationDescriptor.FromRule(rule);

        CollectionAssert.AreEqual(
            new[] { "less", "greater", "values", "text", "count" },
            descriptor.Locals.Select(local => local.Name).ToArray());
    }

    /// <summary>
    /// Verifies that descriptor metadata does not bind parameters, allocate locals, or propagate returns.
    /// </summary>
    [TestMethod]
    public void ParserEngine_RuleDescriptors_RemainNonExecutable()
    {
        var definition = Antlr4GrammarConverter.Parse("""
            grammar P;
            start[int value] returns [int result]
              locals [int scratch]
              : A ;
            A : 'a' ;
            """);
        var manager = new RecordingRuleInvocationFrameManager();
        var parser = new ParserEngine(definition, ParserRuntimeFeaturePolicy.Default with { RuleInvocationFrameManager = manager });
        var tokens = new CompiledGrammar(definition).Tokenize("a");

        var result = parser.Parse(tokens);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        var frame = manager.EnteredFrames.Single(frame => frame.RuleName == "start");
        Assert.IsNotNull(frame.Descriptor);
        Assert.AreEqual(0, frame.Parameters.Count, "Grammar parameters must not be bound as runtime values.");
        Assert.AreEqual(0, frame.Locals.Count, "Grammar locals must not be allocated as runtime values.");
        Assert.AreEqual(0, frame.Returns.Count, "Grammar returns must not be propagated as runtime values.");
    }

    /// <summary>
    /// Verifies that exception metadata is exposed passively and does not change parse results or execute handlers.
    /// </summary>
    [TestMethod]
    public void ParserEngine_ExceptionMetadata_RemainsNonExecutable()
    {
        var definition = Antlr4GrammarConverter.Parse("""
            grammar P;
            start throws ParserException : A ;
              catch [System.Exception ex] { throw new System.InvalidOperationException(); }
              finally { throw new System.InvalidOperationException(); }
            A : 'a' ;
            """);
        var manager = new RecordingRuleInvocationFrameManager();
        var parser = new ParserEngine(definition, ParserRuntimeFeaturePolicy.Default with { RuleInvocationFrameManager = manager });
        var tokens = new CompiledGrammar(definition).Tokenize("b");

        var result = parser.Parse(tokens);

        Assert.IsInstanceOfType<ErrorNode>(result);
        var descriptor = manager.EnteredDescriptors.Single(descriptor => descriptor?.RuleName == "start");
        Assert.AreEqual(3, descriptor!.Exceptions.Count);
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
        Assert.AreSame(NullParserRuleInvocationFrameManager.Instance, GetRuleInvocationFrameManager(evaluatorOnly));

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
        Assert.AreSame(NullParserRuleInvocationFrameManager.Instance, GetRuleInvocationFrameManager(parser));
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
        var diagnostics = new DiagnosticBag();

        var result = parser.Parse([new Token(new SourceSpan(0, 1), "A", "DEFAULT_MODE", "DEFAULT_CHANNEL", "a")], diagnostics: diagnostics);

        Assert.IsInstanceOfType<ParserNode>(result);
        Assert.AreEqual(1UL, stateManager.GetCurrentStateKey().Value);
        Assert.AreEqual(
            0,
            diagnostics.Count(diagnostic => diagnostic.Code == ParserDiagnostics.ParseMemoHit.Code),
            "The failed target invocation cached at state 0 must not be reused after the action changes the state key to 1.");
        Assert.IsTrue(
            diagnostics.Count(diagnostic => diagnostic.Code == ParserDiagnostics.ParseMemoMiss.Code) >= 2,
            "The same target rule and input position must be looked up separately for state 0 and state 1.");
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
    /// Gets the parser rule invocation-frame manager instance currently held by the parser engine.
    /// </summary>
    /// <param name="parser">Parser engine to inspect.</param>
    /// <returns>Configured parser rule invocation-frame manager.</returns>
    private static IParserRuleInvocationFrameManager GetRuleInvocationFrameManager(ParserEngine parser)
    {
        var field = typeof(ParserEngine).GetField("_ruleInvocationFrameManager", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        return (IParserRuleInvocationFrameManager)field.GetValue(parser)!;
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
    /// Test invocation-frame manager that records enter and exit calls while maintaining a current-frame stack.
    /// </summary>
    private sealed class RecordingRuleInvocationFrameManager : IParserRuleInvocationFrameManager
    {
        /// <summary>Current invocation-frame stack.</summary>
        private readonly Stack<ParserRuleInvocationFrame> _frames = new();

        /// <summary>Gets recorded enter and exit event descriptions.</summary>
        public List<string> Events { get; } = [];

        /// <summary>Gets frames returned for rule entry.</summary>
        public List<ParserRuleInvocationFrame> EnteredFrames { get; } = [];

        /// <summary>Gets descriptors observed on rule entry.</summary>
        public List<ParserRuleInvocationDescriptor?> EnteredDescriptors { get; } = [];

        /// <summary>Gets the current frame when one is active.</summary>
        public ParserRuleInvocationFrame? Current => _frames.Count == 0 ? null : _frames.Peek();

        /// <summary>Records parser rule entry and returns a passive invocation frame.</summary>
        /// <param name="ruleName">Name of the rule being entered.</param>
        /// <param name="inputPosition">Input position at rule entry.</param>
        /// <param name="descriptor">Passive descriptor observed for the rule invocation.</param>
        /// <returns>The frame associated with the entered rule.</returns>
        public ParserRuleInvocationFrame Enter(string ruleName, int inputPosition, ParserRuleInvocationDescriptor? descriptor = null)
        {
            var frame = new ParserRuleInvocationFrame(ruleName, inputPosition, new Dictionary<string, object?>(), descriptor);
            _frames.Push(frame);
            EnteredFrames.Add(frame);
            EnteredDescriptors.Add(descriptor);
            Events.Add($"enter:{ruleName}:{inputPosition}");
            return frame;
        }

        /// <summary>Records parser rule exit and removes the matching active frame.</summary>
        /// <param name="frame">Frame leaving the rule invocation.</param>
        /// <param name="succeeded">Whether the rule invocation succeeded.</param>
        public void Exit(ParserRuleInvocationFrame frame, bool succeeded)
        {
            Assert.AreSame(frame, _frames.Pop());
            Events.Add($"exit:{frame.RuleName}:{frame.InputPosition}:{succeeded}");
        }
    }

    /// <summary>
    /// Test lifecycle executor that records contexts passed by the parser engine.
    /// </summary>
    private sealed class RecordingRuleLifecycleExecutor : IParserRuleLifecycleExecutor
    {
        /// <summary>Gets lifecycle contexts observed by the executor.</summary>
        public List<ParserRuleLifecycleContext> Contexts { get; } = [];

        /// <summary>Records a lifecycle context without executing embedded code.</summary>
        /// <param name="phase">Lifecycle phase being executed.</param>
        /// <param name="ruleName">Rule name being executed.</param>
        /// <param name="context">Lifecycle context to record.</param>
        public void Execute(ParserRuleLifecyclePhase phase, string ruleName, ParserRuleLifecycleContext context)
        {
            Contexts.Add(context);
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
