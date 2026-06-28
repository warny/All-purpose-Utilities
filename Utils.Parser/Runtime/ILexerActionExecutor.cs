using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Executes lexer inline actions when an explicit runtime policy opts in to them.
/// </summary>
public interface ILexerActionExecutor
{
    /// <summary>
    /// Executes a lexer inline action for an accepted token match.
    /// </summary>
    /// <param name="context">Context describing the accepted lexer action.</param>
    /// <returns>The action execution outcome.</returns>
    LexerActionExecutionOutcome Execute(LexerActionExecutionContext context);
}

/// <summary>
/// Describes one accepted lexer inline action execution request.
/// </summary>
/// <param name="Rule">Lexer rule that owns the action.</param>
/// <param name="ActionCode">Raw action code without ANTLR braces.</param>
/// <param name="AlternativeIndex">Best-effort deterministic alternative index, or <c>-1</c> when not represented.</param>
/// <param name="ElementIndex">Best-effort deterministic element index, or <c>-1</c> when not represented.</param>
/// <param name="Text">Accepted lexer text for the current rule match before <c>more</c> accumulation is appended.</param>
/// <param name="TokenType">Current token type before lexer commands are applied.</param>
/// <param name="Channel">Current token channel before lexer commands are applied.</param>
/// <param name="Mode">Lexer mode that accepted the current rule match.</param>
public sealed record LexerActionExecutionContext(Rule Rule, string ActionCode, int AlternativeIndex, int ElementIndex, string Text, string TokenType, string Channel, string Mode);

/// <summary>
/// Result of attempting to execute a lexer inline action.
/// </summary>
public enum LexerActionExecutionOutcome
{
    /// <summary>The action was not recognized by the executor.</summary>
    NotExecuted = 0,

    /// <summary>The action was executed.</summary>
    Executed = 1
}
