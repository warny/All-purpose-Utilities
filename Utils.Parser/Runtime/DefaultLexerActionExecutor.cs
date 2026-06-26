namespace Utils.Parser.Runtime;

/// <summary>
/// Conservative lexer action executor that never executes lexer inline actions.
/// </summary>
public sealed class DefaultLexerActionExecutor : ILexerActionExecutor
{
    /// <summary>Gets the singleton no-op lexer action executor.</summary>
    public static DefaultLexerActionExecutor Instance { get; } = new();

    /// <summary>Initializes the no-op lexer action executor.</summary>
    private DefaultLexerActionExecutor()
    {
    }

    /// <summary>Returns <see cref="LexerActionExecutionOutcome.NotExecuted"/> for every lexer action.</summary>
    /// <param name="context">Ignored lexer action context.</param>
    /// <returns>The no-op execution outcome.</returns>
    public LexerActionExecutionOutcome Execute(LexerActionExecutionContext context)
    {
        return LexerActionExecutionOutcome.NotExecuted;
    }
}
