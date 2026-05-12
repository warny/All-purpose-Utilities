namespace Utils.Parser.Runtime;

/// <summary>
/// Defines policy-driven semantic predicate handling for <see cref="ParserEngine"/>.
/// Implementations can influence branch acceptance and therefore parse outcomes.
/// </summary>
public interface ISemanticPredicateEvaluator
{
    /// <summary>
    /// Evaluates the semantic predicate described by <paramref name="context"/>.
    /// </summary>
    /// <param name="context">Predicate metadata and parser location.</param>
    /// <returns>Evaluation outcome controlling branch acceptance.
    /// Implementations are expected to remain deterministic for identical invocation context when memoization is enabled.</returns>
    SemanticPredicateEvaluationResult Evaluate(SemanticPredicateEvaluationContext context);
}
