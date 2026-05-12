namespace Utils.Parser.Runtime;

/// <summary>
/// Default evaluator that preserves legacy parser behavior by not evaluating
/// semantic predicate source code.
/// </summary>
internal sealed class DefaultSemanticPredicateEvaluator : ISemanticPredicateEvaluator
{
    /// <inheritdoc />
    public SemanticPredicateEvaluationResult Evaluate(SemanticPredicateEvaluationContext context)
    {
        return SemanticPredicateEvaluationResult.NotEvaluated;
    }
}
