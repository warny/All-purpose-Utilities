namespace Utils.Parser.Runtime;

/// <summary>
/// Extends lexer behavior with runtime token injection hooks.
/// </summary>
public interface ILexerExtension
{
    /// <summary>
    /// Attempts to read one or more tokens directly from the current input position.
    /// </summary>
    /// <param name="context">Current lexer extension context.</param>
    /// <returns>Generated tokens or an empty list.</returns>
    IReadOnlyList<Token> TryReadTokens(LexerExtensionContext context);

    /// <summary>
    /// Produces optional extra tokens after a token has been emitted by the lexer.
    /// </summary>
    /// <param name="token">The token just emitted.</param>
    /// <param name="context">Current lexer extension context.</param>
    /// <returns>Generated tokens or an empty list.</returns>
    IReadOnlyList<Token> OnAfterToken(Token token, LexerExtensionContext context);

    /// <summary>
    /// Produces optional trailing tokens after reaching end of input.
    /// </summary>
    /// <param name="context">Current lexer extension context.</param>
    /// <returns>Generated tokens or an empty list.</returns>
    IReadOnlyList<Token> OnEndOfInput(LexerExtensionContext context);
}
