namespace Utils.Parser.Runtime;

/// <summary>
/// Represents the status of a semantic predicate evaluation.
/// </summary>
public enum SemanticPredicateEvaluationStatus
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
    /// The predicate was not evaluated by runtime policy.
    /// </summary>
    NotEvaluated
}
