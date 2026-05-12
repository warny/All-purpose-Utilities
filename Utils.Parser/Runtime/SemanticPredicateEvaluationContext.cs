using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Carries immutable information required to evaluate a semantic predicate.
/// </summary>
public sealed record SemanticPredicateEvaluationContext(
    /// <summary>
    /// Parser rule currently being evaluated.
    /// </summary>
    Rule Rule,
    /// <summary>
    /// Predicate element from grammar content.
    /// </summary>
    RuleContent Predicate,
    /// <summary>
    /// Raw predicate source code.
    /// </summary>
    string PredicateCode,
    /// <summary>
    /// Current token index in the parse context.
    /// </summary>
    int InputPosition,
    /// <summary>
    /// Alternative index where the predicate appears, or <c>-1</c> when unknown.
    /// </summary>
    int AlternativeIndex,
    /// <summary>
    /// Element index inside the alternative, or <c>-1</c> when unknown.
    /// </summary>
    int ElementIndex);
