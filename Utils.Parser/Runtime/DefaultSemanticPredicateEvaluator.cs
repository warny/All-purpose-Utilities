namespace Utils.Parser.Runtime;

/// <summary>
/// Default evaluator that preserves conservative legacy behavior by not evaluating
/// semantic predicate source code and returning <see cref="SemanticPredicateEvaluationStatus.NotEvaluated"/>.
/// </summary>
internal sealed class DefaultSemanticPredicateEvaluator : ISemanticPredicateEvaluator
{
    /// <inheritdoc />
    public SemanticPredicateEvaluationOutcome Evaluate(SemanticPredicateEvaluationContext context)
    {
        return SemanticPredicateEvaluationOutcome.NotEvaluated();
    }
}
