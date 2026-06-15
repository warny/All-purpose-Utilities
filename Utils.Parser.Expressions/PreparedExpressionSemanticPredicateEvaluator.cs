using Utils.Parser.Runtime;

namespace Utils.Parser.Expressions;

/// <summary>
/// Evaluates semantic predicates by executing prepared expression artifacts from an explicit registry.
/// </summary>
public sealed class PreparedExpressionSemanticPredicateEvaluator : ISemanticPredicateEvaluator
{
    private readonly PreparedExpressionEmbeddedCodeRegistry _registry;

    /// <summary>
    /// Initializes a new evaluator that consumes already-prepared semantic predicate artifacts.
    /// </summary>
    /// <param name="registry">Registry that maps runtime predicate contexts to prepared artifacts.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="registry"/> is <c>null</c>.</exception>
    public PreparedExpressionSemanticPredicateEvaluator(PreparedExpressionEmbeddedCodeRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <inheritdoc />
    public SemanticPredicateEvaluationOutcome Evaluate(SemanticPredicateEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return _registry.TryGetSemanticPredicate(context, out var artifact) && artifact is not null
            ? artifact.Evaluate(context)
            : SemanticPredicateEvaluationOutcome.NotEvaluated();
    }
}
