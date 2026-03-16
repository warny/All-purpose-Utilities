namespace Utils.Parser.Runtime;

/// <summary>
/// A mutable cursor over a flat token list, used by <see cref="ParserEngine"/> while
/// building a parse tree.
/// Supports position save/restore so that the parser can speculatively try alternatives
/// and backtrack on failure without copying the token list.
/// </summary>
internal sealed class ParseContext(IReadOnlyList<Token> tokens)
{
    private int _position;

    /// <summary>Current zero-based index into the token list.</summary>
    public int Position => _position;

    /// <summary><c>true</c> when all tokens have been consumed.</summary>
    public bool IsEnd => _position >= tokens.Count;

    /// <summary>
    /// Returns the token at <c>Position + offset</c> without consuming it,
    /// or <c>null</c> when the index is out of range.
    /// </summary>
    /// <param name="offset">Offset from the current position (may be negative to look back).</param>
    public Token? Peek(int offset = 0)
    {
        var index = _position + offset;
        return index >= 0 && index < tokens.Count ? tokens[index] : null;
    }

    /// <summary>
    /// Consumes and returns the current token, advancing the position by one.
    /// </summary>
    /// <returns>The token at the current position.</returns>
    public Token Consume() => tokens[_position++];

    /// <summary>Saves the current position for later restoration.</summary>
    /// <returns>An integer encoding the saved position.</returns>
    public int SavePosition() => _position;

    /// <summary>Restores the position to a value previously returned by <see cref="SavePosition"/>.</summary>
    /// <param name="saved">Value returned by a prior call to <see cref="SavePosition"/>.</param>
    public void RestorePosition(int saved) => _position = saved;
}
