namespace Utils.Parser.Runtime;

/// <summary>
/// Defines policy-driven semantic predicate handling for <see cref="ParserEngine"/>.
/// </summary>
public interface ISemanticPredicateEvaluator
{
    /// <summary>
    /// Evaluates the semantic predicate described by <paramref name="context"/>.
    /// </summary>
    /// <param name="context">Predicate metadata and parser location.</param>
    /// <returns>Evaluation outcome controlling branch acceptance.</returns>
    SemanticPredicateEvaluationResult Evaluate(SemanticPredicateEvaluationContext context);
}
