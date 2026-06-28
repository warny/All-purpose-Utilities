namespace Utils.Parser.Runtime;

/// <summary>
/// Conservative lexer predicate evaluator that never evaluates lexer target-language code.
/// </summary>
public sealed class DefaultLexerPredicateEvaluator : ILexerPredicateEvaluator
{
    /// <summary>Gets the singleton no-op lexer predicate evaluator.</summary>
    public static DefaultLexerPredicateEvaluator Instance { get; } = new();

    /// <summary>Initializes the no-op lexer predicate evaluator.</summary>
    private DefaultLexerPredicateEvaluator()
    {
    }

    /// <summary>Returns <see cref="LexerPredicateEvaluationOutcome.NotEvaluated"/> for every lexer predicate.</summary>
    /// <param name="context">Ignored lexer predicate context.</param>
    /// <returns>The no-op evaluation outcome.</returns>
    public LexerPredicateEvaluationOutcome Evaluate(LexerPredicateEvaluationContext context)
    {
        return LexerPredicateEvaluationOutcome.NotEvaluated;
    }
}
