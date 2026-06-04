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
    /// Semantic predicates are not evaluated and parser actions are not executed.
    /// </summary>
    public static ParserRuntimeFeaturePolicy Default { get; } = new()
    {
        SemanticPredicateEvaluator = new DefaultSemanticPredicateEvaluator(),
        ParserActionExecutor = new DefaultParserActionExecutor(),
        ExecutionStateManager = NullParserExecutionStateManager.Instance,
        RuntimeObserver = null
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
    /// Gets the parser execution-state manager used to capture and restore opaque semantic state.
    /// ParserEngine validates this contract but does not invoke it for branch rollback yet.
    /// </summary>
    public required IParserExecutionStateManager ExecutionStateManager { get; init; }

    /// <summary>
    /// Gets the optional passive runtime observer for scheduling introspection.
    /// This observer is descriptive-only and does not control parser execution.
    /// </summary>
    public IParserRuntimeObserver? RuntimeObserver { get; init; }
}

