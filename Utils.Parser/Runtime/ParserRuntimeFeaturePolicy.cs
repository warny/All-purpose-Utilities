using Utils.Parser.Diagnostics.EmbeddedCode;

namespace Utils.Parser.Runtime;

/// <summary>
/// Immutable runtime feature policy used by parser components to centralize optional
/// runtime strategies such as semantic predicate evaluation and parser action execution.
/// The policy can affect branch acceptance and action execution, but does not change parser scheduling mechanics.
/// </summary>
public sealed record ParserRuntimeFeaturePolicy
{
    /// <summary>
    /// Gets the conservative default runtime policy.
    /// Semantic predicates are not evaluated, parser actions are not executed, and rule lifecycle hooks are not invoked.
    /// </summary>
    public static ParserRuntimeFeaturePolicy Default { get; } = new()
    {
        SemanticPredicateEvaluator = new DefaultSemanticPredicateEvaluator(),
        ParserActionExecutor = new DefaultParserActionExecutor(),
        LexerActionExecutor = DefaultLexerActionExecutor.Instance,
        ExecutionStateManager = NullParserExecutionStateManager.Instance,
        RuleLifecycleExecutor = NullParserRuleLifecycleExecutor.Instance,
        RuleInvocationFrameManager = NullParserRuleInvocationFrameManager.Instance,
        RuleCallExecutionPolicy = NullParserRuleCallExecutionPolicy.Instance,
        RuntimeObserver = null,
        EmbeddedCodeTransformer = NoOpParserEmbeddedCodeTransformer.Instance
    };

    /// <summary>
    /// Gets the semantic predicate evaluation strategy.
    /// </summary>
    public required ISemanticPredicateEvaluator SemanticPredicateEvaluator { get; init; }

    /// <summary>
    /// Gets the parser action execution strategy.
    /// </summary>
    public required IParserActionExecutor ParserActionExecutor { get; init; }

    /// <summary>
    /// Gets the lexer action execution strategy. The default no-op executor preserves conservative lexer behavior.
    /// </summary>
    public required ILexerActionExecutor LexerActionExecutor { get; init; }

    /// <summary>
    /// Gets the parser execution-state manager used to expose opaque semantic state snapshots and memoization keys.
    /// ParserEngine reads state keys for completed-result memoization and invokes capture/restore around managed parser attempt boundaries.
    /// </summary>
    public required IParserExecutionStateManager ExecutionStateManager { get; init; }

    /// <summary>
    /// Gets the parser rule lifecycle executor that handles <c>@init</c> and <c>@after</c> hooks.
    /// The default no-op executor skips lifecycle hooks and preserves conservative behavior.
    /// </summary>
    public required IParserRuleLifecycleExecutor RuleLifecycleExecutor { get; init; }

    /// <summary>
    /// Gets the parser rule invocation-frame manager used to create passive per-rule metadata frames.
    /// The default no-op manager does not execute parameters, locals, returns, or exception metadata.
    /// </summary>
    public required IParserRuleInvocationFrameManager RuleInvocationFrameManager { get; init; }

    /// <summary>
    /// Gets the explicit parser rule-call execution policy invoked around parser rule references.
    /// The default no-op policy preserves metadata-only call arguments and labels.
    /// </summary>
    public IParserRuleCallExecutionPolicy RuleCallExecutionPolicy { get; init; } = NullParserRuleCallExecutionPolicy.Instance;

    /// <summary>
    /// Gets the optional passive runtime observer for scheduling introspection.
    /// This observer is descriptive-only and does not control parser execution.
    /// </summary>
    public IParserRuntimeObserver? RuntimeObserver { get; init; }

    /// <summary>
    /// Gets the embedded-code transformer that dynamic embedded-code preparation paths apply before invoking their configured compiler.
    /// The default transformer preserves code unchanged.
    /// </summary>
    public IParserEmbeddedCodeTransformer EmbeddedCodeTransformer { get; init; } = NoOpParserEmbeddedCodeTransformer.Instance;
}

