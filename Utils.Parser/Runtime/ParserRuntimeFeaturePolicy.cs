namespace Utils.Parser.Runtime;

/// <summary>
/// Immutable runtime feature policy used by parser components to centralize optional
/// runtime strategies such as semantic predicate evaluation and parser action execution.
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
        ParserActionExecutor = new DefaultParserActionExecutor()
    };

    /// <summary>
    /// Gets the semantic predicate evaluation strategy.
    /// </summary>
    public required ISemanticPredicateEvaluator SemanticPredicateEvaluator { get; init; }

    /// <summary>
    /// Gets the parser action execution strategy.
    /// </summary>
    public required IParserActionExecutor ParserActionExecutor { get; init; }
}
