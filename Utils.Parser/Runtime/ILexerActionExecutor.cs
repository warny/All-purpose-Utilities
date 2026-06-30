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
    /// <param name="result">Mutable result receiving supported lexer action token mutations.</param>
    /// <returns>The action execution outcome.</returns>
    LexerActionExecutionOutcome Execute(LexerActionExecutionContext context, LexerActionExecutionResult result);
}

/// <summary>
/// Describes one accepted lexer inline action execution request.
/// </summary>
/// <param name="Rule">Lexer rule that owns the action.</param>
/// <param name="ActionCode">Raw action code without ANTLR braces.</param>
/// <param name="AlternativeIndex">Best-effort deterministic alternative index, or <c>-1</c> when not represented.</param>
/// <param name="ElementIndex">Best-effort deterministic element index, or <c>-1</c> when not represented.</param>
/// <param name="Text">Accepted token or chunk text available to the accepted lexer action context.</param>
/// <param name="TokenType">Token type available to the accepted lexer action context before lexer commands are applied.</param>
/// <param name="Channel">Token channel available to the accepted lexer action context before lexer commands are applied.</param>
/// <param name="Mode">Lexer mode available to the accepted lexer action context.</param>
/// <param name="Line">One-based source line at the start of the accepted token or chunk.</param>
/// <param name="Column">One-based source column at the start of the accepted token or chunk.</param>
public sealed record LexerActionExecutionContext(Rule Rule, string ActionCode, int AlternativeIndex, int ElementIndex, string Text, string TokenType, string Channel, string Mode, int Line, int Column);

/// <summary>
/// Carries the bounded token mutations produced by a generated lexer inline action.
/// </summary>
public sealed class LexerActionExecutionResult
{
    /// <summary>Gets or sets the replacement token type requested by the action.</summary>
    public string? TokenType { get; set; }

    /// <summary>Gets or sets the replacement token channel requested by the action.</summary>
    public string? Channel { get; set; }
}

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
