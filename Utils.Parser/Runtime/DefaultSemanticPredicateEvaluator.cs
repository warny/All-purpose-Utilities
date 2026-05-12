namespace Utils.Parser.Runtime;

/// <summary>
/// Default evaluator that preserves conservative legacy behavior by not evaluating
/// semantic predicate source code and returning <see cref="SemanticPredicateEvaluationResult.NotEvaluated"/>.
/// </summary>
internal sealed class DefaultSemanticPredicateEvaluator : ISemanticPredicateEvaluator
{
    /// <inheritdoc />
    public SemanticPredicateEvaluationResult Evaluate(SemanticPredicateEvaluationContext context)
    {
        return SemanticPredicateEvaluationResult.NotEvaluated;
    }
}
