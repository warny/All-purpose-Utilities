namespace Utils.Parser.Runtime;

/// <summary>
/// Represents the outcome of a semantic predicate evaluation.
/// </summary>
public enum SemanticPredicateEvaluationResult
{
    /// <summary>
    /// The predicate passed and the parser may continue.
    /// </summary>
    Satisfied,

    /// <summary>
    /// The predicate failed and the current parse branch must be rejected.
    /// </summary>
    Rejected,

    /// <summary>
    /// The predicate was intentionally not evaluated by policy.
    /// </summary>
    NotEvaluated
}
