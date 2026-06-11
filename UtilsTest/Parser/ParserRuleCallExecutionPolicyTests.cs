using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Bootstrap;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Verifies explicit parser rule-call execution policy ordering, metadata, failure handling,
/// rollback behavior, and memoization-safe current call-site annotation.
/// </summary>
[TestClass]
public class ParserRuleCallExecutionPolicyTests
{
    /// <summary>
    /// Verifies that the conservative default policy remains a no-op and preserves parsing behavior.
    /// </summary>
    [TestMethod]
    public void DefaultPolicy_ParsesRuleReferencesWithoutExecutingCustomBehavior()
    {
        const string grammar = """
            grammar P;
            start : child[ignored] ;
            child : A ;
            A : 'a' ;
            """;

        var compiled = Antlr4GrammarConverter.Compile(grammar);
        var result = compiled.Parse("a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreSame(NullParserRuleCallExecutionPolicy.Instance, ParserRuntimeFeaturePolicy.Default.RuleCallExecutionPolicy);
    }

    /// <summary>
    /// Verifies that the null policy accepts valid contexts and rejects null callback inputs.
    /// </summary>
    [TestMethod]
    public void NullPolicy_CallbacksAreNoOpAndRejectNull()
    {
        var context = new ParserRuleCallExecutionContext
        {
            CallerFrame = null,
            RuleName = "child",
        };

        NullParserRuleCallExecutionPolicy.Instance.BeforeRuleCall(context);
        NullParserRuleCallExecutionPolicy.Instance.AfterRuleCall(context);

        Assert.ThrowsException<ArgumentNullException>(() => NullParserRuleCallExecutionPolicy.Instance.BeforeRuleCall(null!));
        Assert.ThrowsException<ArgumentNullException>(() => NullParserRuleCallExecutionPolicy.Instance.AfterRuleCall(null!));
    }

    /// <summary>
    /// Verifies that parser construction rejects an explicitly null rule-call execution policy.
    /// </summary>
    [TestMethod]
    public void ParserEngine_RejectsNullRuleCallExecutionPolicy()
    {
        var definition = Antlr4GrammarConverter.Parse("""
            grammar P;
            start : A ;
            A : 'a' ;
            """);
        var policy = ParserRuntimeFeaturePolicy.Default with { RuleCallExecutionPolicy = null! };

        Assert.ThrowsException<ArgumentNullException>(() => new ParserEngine(definition, policy));
    }

    /// <summary>
    /// Verifies that before-call notification precedes child invocation and after-call notification follows it.
    /// </summary>
    [TestMethod]
    public void PolicyCallbacks_SurroundChildRuleInvocation()
    {
        const string grammar = """
            grammar P;
            start : child ;
            child @init { } @after { } : A ;
            A : 'a' ;
            """;
        var events = new List<string>();
        var callPolicy = new RecordingRuleCallPolicy(events);
        var lifecycleExecutor = new RecordingLifecycleExecutor(events);

        var result = Compile(grammar, callPolicy, lifecycleExecutor: lifecycleExecutor).Parse("a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        CollectionAssert.AreEqual(
            new[] { "before:child", "init:child", "after-lifecycle:child", "after:child" },
            events.Where(static item => item.EndsWith(":child", StringComparison.Ordinal)).ToArray());
    }

    /// <summary>
    /// Verifies that successful callbacks expose current raw arguments, label metadata, split metadata,
    /// the target descriptor, caller frame, and the annotated completed call result.
    /// </summary>
    [TestMethod]
    public void SuccessfulRuleCall_ContextContainsCurrentCallSiteMetadataAndResult()
    {
        const string grammar = """
            grammar P;
            start : item=child[first: 1, second: call(2, 3)] ;
            child : A ;
            A : 'a' ;
            """;
        var callPolicy = new RecordingRuleCallPolicy();

        var result = Compile(grammar, callPolicy).Parse("a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        var before = callPolicy.Events.Single(item => item.Phase == "before" && item.Context.RuleName == "child").Context;
        var after = callPolicy.Events.Single(item => item.Phase == "after" && item.Context.RuleName == "child").Context;
        Assert.AreEqual("first: 1, second: call(2, 3)", before.RawArguments);
        Assert.AreEqual("item", before.LabelName);
        Assert.AreEqual(ParserRuleReferenceLabelKind.Assignment, before.LabelKind);
        CollectionAssert.AreEqual(new[] { "first: 1", "second: call(2, 3)" }, before.PositionalRawArguments!.ToArray());
        Assert.AreEqual("1", before.NamedRawArguments!["first"]);
        Assert.AreEqual("call(2, 3)", before.NamedRawArguments["second"]);
        Assert.AreEqual("child", before.TargetRuleDescriptor!.RuleName);
        Assert.AreEqual("start", before.CallerFrame!.RuleName);
        Assert.IsTrue(after.Succeeded);
        Assert.IsNotNull(after.CompletedCallResult);
        Assert.AreEqual("child", after.CompletedCallResult.RuleName);
        Assert.AreEqual(before.RawArguments, after.CompletedCallResult.RawArguments);
        Assert.AreEqual(before.LabelName, after.CompletedCallResult.LabelName);
        Assert.AreEqual(before.LabelKind, after.CompletedCallResult.LabelKind);
    }

    /// <summary>
    /// Verifies that a direct-left-recursive right-hand self-reference receives policy callbacks and call-site annotations.
    /// </summary>
    [TestMethod]
    public void LeftRecursiveRightHandRuleCall_UsesPolicyHooksAndCurrentCallSiteMetadata()
    {
        const string grammar = """
            grammar P;
            start : expr ;
            expr : atom | expr PLUS right=expr[minimum: 2] ;
            atom : INT ;
            PLUS : '+' ;
            INT : '1' | '2' ;
            """;
        var callPolicy = new RecordingRuleCallPolicy();

        var result = Compile(grammar, callPolicy).Parse("1+2");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        var recursiveBefore = callPolicy.Events.Single(item =>
            item.Phase == "before" &&
            item.Context.RuleName == "expr" &&
            item.Context.LabelName == "right");
        var recursiveAfter = callPolicy.Events.Single(item =>
            item.Phase == "after" &&
            item.Context.RuleName == "expr" &&
            item.Context.LabelName == "right");
        Assert.AreSame(recursiveBefore.Context, recursiveAfter.Context);
        Assert.AreEqual("minimum: 2", recursiveAfter.Context.RawArguments);
        Assert.AreEqual(ParserRuleReferenceLabelKind.Assignment, recursiveAfter.Context.LabelKind);
        Assert.IsTrue(recursiveAfter.Context.Succeeded);
        Assert.IsNotNull(recursiveAfter.Context.CompletedCallResult);
        Assert.AreEqual("minimum: 2", recursiveAfter.Context.CompletedCallResult.RawArguments);
        Assert.AreEqual("right", recursiveAfter.Context.CompletedCallResult.LabelName);
        Assert.AreEqual(ParserRuleReferenceLabelKind.Assignment, recursiveAfter.Context.CompletedCallResult.LabelKind);
    }

    /// <summary>
    /// Verifies that a failed parser rule call still receives both callbacks without a completed call result.
    /// </summary>
    [TestMethod]
    public void FailedRuleCall_AfterContextReportsFailureWithoutResult()
    {
        const string grammar = """
            grammar P;
            start : child? A ;
            child : B ;
            A : 'a' ;
            B : 'b' ;
            """;
        var callPolicy = new RecordingRuleCallPolicy();

        var result = Compile(grammar, callPolicy).Parse("a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        var childEvents = callPolicy.Events.Where(item => item.Context.RuleName == "child").ToArray();
        CollectionAssert.AreEqual(new[] { "before", "after" }, childEvents.Select(static item => item.Phase).ToArray());
        Assert.IsFalse(childEvents[1].Context.Succeeded);
        Assert.IsNull(childEvents[1].Context.CompletedCallResult);
    }

    /// <summary>
    /// Verifies that policy metadata for the successful alternative replaces metadata observed in a failed alternative.
    /// </summary>
    [TestMethod]
    public void Backtracking_UsesSuccessfulAlternativeCurrentCallSiteMetadata()
    {
        const string grammar = """
            grammar P;
            start : a=child[1] B | b=child[2] ;
            child : A ;
            A : 'a' ;
            B : 'b' ;
            """;
        var callPolicy = new RecordingRuleCallPolicy();
        ParserRuleCallResult? lastCallResult = null;
        var frameManager = new StackParserRuleInvocationFrameManager(result => lastCallResult = result);

        var result = Compile(grammar, callPolicy, frameManager).Parse("a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        var successfulChildCalls = callPolicy.Events
            .Where(item => item.Phase == "after" && item.Context.RuleName == "child" && item.Context.Succeeded)
            .ToArray();
        Assert.AreEqual(2, successfulChildCalls.Length);
        var current = successfulChildCalls[^1].Context;
        Assert.AreEqual("2", current.RawArguments);
        Assert.AreEqual("b", current.LabelName);
        Assert.AreEqual("2", current.CompletedCallResult!.RawArguments);
        Assert.AreEqual("b", current.CompletedCallResult.LabelName);
        Assert.AreEqual("2", lastCallResult!.RawArguments);
        Assert.AreEqual("b", lastCallResult.LabelName);
    }

    /// <summary>
    /// Verifies that a memoized child call is re-annotated with the second call site's raw arguments and label.
    /// </summary>
    [TestMethod]
    public void MemoizedRuleCall_UsesCurrentCallSiteMetadata()
    {
        const string grammar = """
            grammar P;
            start : x=child[1] B | y=child[2] ;
            child : A ;
            A : 'a' ;
            B : 'b' ;
            """;
        var callPolicy = new RecordingRuleCallPolicy();

        var result = Compile(grammar, callPolicy).Parse("a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        var childAfterCalls = callPolicy.Events
            .Where(item => item.Phase == "after" && item.Context.RuleName == "child")
            .ToArray();
        Assert.AreEqual(2, childAfterCalls.Length);
        Assert.AreEqual("1", childAfterCalls[0].Context.CompletedCallResult!.RawArguments);
        Assert.AreEqual("x", childAfterCalls[0].Context.CompletedCallResult.LabelName);
        Assert.AreEqual("2", childAfterCalls[1].Context.CompletedCallResult!.RawArguments);
        Assert.AreEqual("y", childAfterCalls[1].Context.CompletedCallResult.LabelName);
    }

    /// <summary>
    /// Verifies exact positional binding, declaration order, null presence, and overwrite behavior.
    /// </summary>
    [TestMethod]
    public void PositionalLiteralPolicy_ValidCall_SeedsDeclaredParameters()
    {
        const string grammar = """
            grammar P;
            start : child[42, "hello", true, null] ;
            child[int value, string text, bool enabled, object missing] : A ;
            A : 'a' ;
            """;
        var frameManager = new StackParserRuleInvocationFrameManager();
        var observed = new Dictionary<string, object?>();
        var lifecycle = new ParameterRecordingLifecycleExecutor(observed);

        var result = Compile(
            grammar,
            new PositionalLiteralRuleCallExecutionPolicy(),
            frameManager,
            lifecycle).Parse("a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(42, observed["value"]);
        Assert.AreEqual("hello", observed["text"]);
        Assert.AreEqual(true, observed["enabled"]);
        Assert.IsTrue(observed.ContainsKey("missing"));
        Assert.IsNull(observed["missing"]);
    }

    /// <summary>
    /// Verifies call-site policy values overwrite same-parameter seeds without clearing unrelated pending seeds.
    /// </summary>
    [TestMethod]
    public void PositionalLiteralPolicy_OverwritesMatchingSeedAndPreservesUnrelatedSeed()
    {
        const string grammar = """
            grammar P;
            start : child[42] ;
            child[int value] : A ;
            A : 'a' ;
            """;
        var observed = new Dictionary<string, object?>();
        var lifecycle = new DirectParameterRecordingLifecycleExecutor(observed, ["value", "unrelated"]);
        var policy = new PreseedingRuleCallPolicy(new PositionalLiteralRuleCallExecutionPolicy());

        var result = Compile(grammar, policy, lifecycleExecutor: lifecycle).Parse("a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(42, observed["value"]);
        Assert.AreEqual("keep", observed["unrelated"]);
    }

    /// <summary>
    /// Verifies ignored invalid calls apply no partial seeds while strict mode reports rule and argument metadata.
    /// </summary>
    [TestMethod]
    public void PositionalLiteralPolicy_InvalidCall_IsAtomicAndConfigurable()
    {
        const string grammar = """
            grammar P;
            start : child[1, foo()] ;
            child[int first, int second] : A ;
            A : 'a' ;
            """;
        var observed = new Dictionary<string, object?>();
        var ignoredResult = Compile(
            grammar,
            new PositionalLiteralRuleCallExecutionPolicy(),
            lifecycleExecutor: new ParameterRecordingLifecycleExecutor(observed)).Parse("a");

        Assert.IsNotInstanceOfType(ignoredResult, typeof(ErrorNode));
        Assert.AreEqual(0, observed.Count, "Validation must finish before any seed is written.");

        var exception = Assert.ThrowsException<ParserRuleCallBindingException>(() => Compile(
            grammar,
            new PositionalLiteralRuleCallExecutionPolicy(ParserRuleCallBindingFailureBehavior.Throw)).Parse("a"));
        Assert.AreEqual("child", exception.RuleName);
        Assert.AreEqual("1, foo()", exception.RawArguments);
        Assert.AreEqual(1, exception.ArgumentIndex);
    }

    /// <summary>
    /// Verifies exact arity and descriptor-name validation reject a complete call without writing seeds.
    /// </summary>
    [TestMethod]
    public void PositionalLiteralPolicy_InvalidArityOrDescriptorNames_WritesNoSeeds()
    {
        var policy = new PositionalLiteralRuleCallExecutionPolicy(ParserRuleCallBindingFailureBehavior.Throw);
        var arityContext = CreatePolicyContext(["1"], ["first", "second"]);
        Assert.ThrowsException<ParserRuleCallBindingException>(() => policy.BeforeRuleCall(arityContext));

        var duplicateContext = CreatePolicyContext(["1", "2"], ["value", "value"]);
        Assert.ThrowsException<ParserRuleCallBindingException>(() => policy.BeforeRuleCall(duplicateContext));

        var missingContext = CreatePolicyContext(["1"], [" "]);
        Assert.ThrowsException<ParserRuleCallBindingException>(() => policy.BeforeRuleCall(missingContext));
    }

    /// <summary>
    /// Verifies a call without an argument clause remains metadata-only and post-call handling is a no-op.
    /// </summary>
    [TestMethod]
    public void PositionalLiteralPolicy_NoArgumentsAndAfterCall_AreNoOp()
    {
        var policy = new PositionalLiteralRuleCallExecutionPolicy(ParserRuleCallBindingFailureBehavior.Throw);
        var context = new ParserRuleCallExecutionContext
        {
            CallerFrame = null,
            RuleName = "child",
            PositionalRawArguments = null,
        };

        policy.BeforeRuleCall(context);
        policy.AfterRuleCall(context);
    }

    /// <summary>
    /// Creates direct policy metadata for descriptor-validation tests.
    /// </summary>
    /// <param name="arguments">Positional raw arguments.</param>
    /// <param name="parameterNames">Target descriptor parameter names.</param>
    /// <returns>A rule-call execution context without a managed seed writer.</returns>
    private static ParserRuleCallExecutionContext CreatePolicyContext(
        IReadOnlyList<string> arguments,
        IReadOnlyList<string> parameterNames)
    {
        return new ParserRuleCallExecutionContext
        {
            CallerFrame = null,
            RuleName = "child",
            RawArguments = string.Join(", ", arguments),
            PositionalRawArguments = arguments,
            TargetRuleDescriptor = new ParserRuleInvocationDescriptor
            {
                RuleName = "child",
                Parameters = parameterNames.Select(static name => new ParserRuleParameterDescriptor
                {
                    Name = name,
                    RawDeclaration = name,
                }).ToArray(),
            },
        };
    }

    /// <summary>
    /// Seeds existing values before delegating to the concrete positional literal policy.
    /// </summary>
    private sealed class PreseedingRuleCallPolicy(IParserRuleCallExecutionPolicy inner) : IParserRuleCallExecutionPolicy
    {
        /// <summary>
        /// Seeds matching and unrelated parameters before the concrete call-site policy runs.
        /// </summary>
        /// <param name="context">Current call context.</param>
        public void BeforeRuleCall(ParserRuleCallExecutionContext context)
        {
            Assert.IsTrue(context.TrySetParameterSeed("value", 999));
            Assert.IsTrue(context.TrySetParameterSeed("unrelated", "keep"));
            inner.BeforeRuleCall(context);
        }

        /// <summary>
        /// Delegates post-call notification without changing the result.
        /// </summary>
        /// <param name="context">Completed call context.</param>
        public void AfterRuleCall(ParserRuleCallExecutionContext context)
        {
            inner.AfterRuleCall(context);
        }
    }

    /// <summary>
    /// Records explicitly requested parameter names from the child frame.
    /// </summary>
    private sealed class DirectParameterRecordingLifecycleExecutor(
        Dictionary<string, object?> observed,
        IReadOnlyList<string> parameterNames) : IParserRuleLifecycleExecutor
    {
        /// <summary>
        /// Reads requested values during child initialization.
        /// </summary>
        /// <param name="phase">Current lifecycle phase.</param>
        /// <param name="ruleName">Current rule name.</param>
        /// <param name="context">Current lifecycle context.</param>
        public void Execute(ParserRuleLifecyclePhase phase, string ruleName, ParserRuleLifecycleContext context)
        {
            if (phase != ParserRuleLifecyclePhase.Init || ruleName != "child" || context.InvocationFrame is null)
            {
                return;
            }

            foreach (string parameterName in parameterNames)
            {
                if (context.InvocationFrame.TryGetParameter(parameterName, out object? value))
                {
                    observed[parameterName] = value;
                }
            }
        }
    }

    /// <summary>
    /// Records all parameters visible during the child initialization phase.
    /// </summary>
    private sealed class ParameterRecordingLifecycleExecutor(Dictionary<string, object?> observed) : IParserRuleLifecycleExecutor
    {
        /// <summary>
        /// Records declared child parameters that are present in the invocation frame.
        /// </summary>
        /// <param name="phase">Current lifecycle phase.</param>
        /// <param name="ruleName">Current rule name.</param>
        /// <param name="context">Current lifecycle context.</param>
        public void Execute(ParserRuleLifecyclePhase phase, string ruleName, ParserRuleLifecycleContext context)
        {
            if (phase != ParserRuleLifecyclePhase.Init || ruleName != "child" || context.InvocationFrame?.Descriptor is null)
            {
                return;
            }

            foreach (ParserRuleParameterDescriptor parameter in context.InvocationFrame.Descriptor.Parameters)
            {
                if (context.InvocationFrame.TryGetParameter(parameter.Name, out object? value))
                {
                    observed[parameter.Name] = value;
                }
            }
        }
    }

    /// <summary>
    /// Compiles a grammar with an explicit call policy and rollback-aware stack manager.
    /// </summary>
    /// <param name="grammar">ANTLR grammar source.</param>
    /// <param name="callPolicy">Rule-call policy under test.</param>
    /// <param name="frameManager">Optional stack manager supplied by the caller.</param>
    /// <param name="lifecycleExecutor">Optional lifecycle executor supplied by the caller.</param>
    /// <returns>A compiled grammar using the supplied policies.</returns>
    private static CompiledGrammar Compile(
        string grammar,
        IParserRuleCallExecutionPolicy callPolicy,
        StackParserRuleInvocationFrameManager? frameManager = null,
        IParserRuleLifecycleExecutor? lifecycleExecutor = null)
    {
        var policy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = callPolicy,
            RuleInvocationFrameManager = frameManager ?? new StackParserRuleInvocationFrameManager(),
            RuleLifecycleExecutor = lifecycleExecutor ?? NullParserRuleLifecycleExecutor.Instance,
        };
        return new CompiledGrammar(Antlr4GrammarConverter.Parse(grammar), policy);
    }

    /// <summary>
    /// Records rule-call callback phases and contexts for deterministic assertions.
    /// </summary>
    private sealed class RecordingRuleCallPolicy : IParserRuleCallExecutionPolicy
    {
        private readonly List<string>? _orderedEvents;

        /// <summary>
        /// Initializes a recording policy.
        /// </summary>
        /// <param name="orderedEvents">Optional shared ordering sink.</param>
        public RecordingRuleCallPolicy(List<string>? orderedEvents = null)
        {
            _orderedEvents = orderedEvents;
        }

        /// <summary>
        /// Gets callback events in invocation order.
        /// </summary>
        public List<(string Phase, ParserRuleCallExecutionContext Context)> Events { get; } = [];

        /// <summary>
        /// Records a before-call callback.
        /// </summary>
        /// <param name="context">Current call context.</param>
        public void BeforeRuleCall(ParserRuleCallExecutionContext context)
        {
            Events.Add(("before", context));
            _orderedEvents?.Add($"before:{context.RuleName}");
        }

        /// <summary>
        /// Records an after-call callback.
        /// </summary>
        /// <param name="context">Completed call context.</param>
        public void AfterRuleCall(ParserRuleCallExecutionContext context)
        {
            Events.Add(("after", context));
            _orderedEvents?.Add($"after:{context.RuleName}");
        }
    }

    /// <summary>
    /// Records lifecycle callbacks in a shared ordering sink.
    /// </summary>
    private sealed class RecordingLifecycleExecutor : IParserRuleLifecycleExecutor
    {
        private readonly List<string> _events;

        /// <summary>
        /// Initializes the lifecycle recorder.
        /// </summary>
        /// <param name="events">Shared ordering sink.</param>
        public RecordingLifecycleExecutor(List<string> events)
        {
            _events = events;
        }

        /// <summary>
        /// Records the lifecycle phase and rule name.
        /// </summary>
        /// <param name="phase">Lifecycle phase.</param>
        /// <param name="ruleName">Current rule name.</param>
        /// <param name="context">Current lifecycle context.</param>
        public void Execute(ParserRuleLifecyclePhase phase, string ruleName, ParserRuleLifecycleContext context)
        {
            _events.Add(phase == ParserRuleLifecyclePhase.Init
                ? $"init:{ruleName}"
                : $"after-lifecycle:{ruleName}");
        }
    }
}
