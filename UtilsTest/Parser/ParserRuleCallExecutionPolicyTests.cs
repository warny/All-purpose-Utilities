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
