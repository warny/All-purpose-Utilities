using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Evaluates lexer semantic predicates when an explicit runtime policy opts in to them.
/// </summary>
public interface ILexerPredicateEvaluator
{
    /// <summary>
    /// Evaluates a lexer predicate while the lexer is matching a candidate token path.
    /// </summary>
    /// <param name="context">Context describing the lexer predicate location.</param>
    /// <returns>The predicate evaluation outcome.</returns>
    LexerPredicateEvaluationOutcome Evaluate(LexerPredicateEvaluationContext context);
}

/// <summary>
/// Describes one lexer predicate evaluation request.
/// </summary>
/// <param name="Rule">Lexer rule that owns the predicate.</param>
/// <param name="PredicateCode">Raw predicate code without ANTLR braces and trailing question mark.</param>
/// <param name="AlternativeIndex">Best-effort deterministic alternative index, or <c>-1</c> when not represented.</param>
/// <param name="ElementIndex">Best-effort deterministic element index, or <c>-1</c> when not represented.</param>
public sealed record LexerPredicateEvaluationContext(Rule Rule, string PredicateCode, int AlternativeIndex, int ElementIndex);

/// <summary>
/// Result of attempting to evaluate a lexer predicate.
/// </summary>
public enum LexerPredicateEvaluationOutcome
{
    /// <summary>The predicate was not recognized by the evaluator.</summary>
    NotEvaluated = 0,

    /// <summary>The predicate evaluated to false and rejects the current lexer matching path.</summary>
    False = 1,

    /// <summary>The predicate evaluated to true and allows the current lexer matching path to continue.</summary>
    True = 2
}
