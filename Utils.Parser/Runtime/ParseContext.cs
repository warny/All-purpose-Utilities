namespace Utils.Parser.Runtime;

/// <summary>
/// A mutable cursor over a flat token list, used by <see cref="ParserEngine"/> while
/// building a parse tree.
/// Supports position save/restore so that the parser can speculatively try alternatives
/// and backtrack on failure without copying the token list.
/// It also owns the private resource budget of the parse operation it belongs to: restoring a
/// position never refunds consumed budget.
/// </summary>
internal sealed class ParseContext
{
    /// <summary>The flat default-channel token list being parsed.</summary>
    private readonly IReadOnlyList<Token> tokens;

    /// <summary>Zero-based index of the next token to be consumed.</summary>
    private int _position;

    /// <summary>Initializes a cursor whose parse runs under <see cref="ParserResourceLimits.Default"/>.</summary>
    /// <param name="tokens">The tokens to parse.</param>
    public ParseContext(IReadOnlyList<Token> tokens)
        : this(tokens, ParserResourceLimits.Default)
    {
    }

    /// <summary>Initializes a cursor and the resource budget of its parse operation.</summary>
    /// <param name="tokens">The tokens to parse.</param>
    /// <param name="limits">The limits of the parse operation.</param>
    public ParseContext(IReadOnlyList<Token> tokens, ParserResourceLimits limits)
    {
        this.tokens = tokens;
        Budget = new ParserResourceBudget(limits, ParserResourcePhase.Parser, CurrentCharacterOffset);
    }

    /// <summary>Gets the private resource budget of the parse operation.</summary>
    public ParserResourceBudget Budget { get; }

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

    /// <summary>
    /// Returns <c>true</c> when the current position is strictly greater than
    /// <paramref name="savedPosition"/>.
    /// </summary>
    /// <param name="savedPosition">Position snapshot captured before an operation.</param>
    public bool HasStrictProgress(int savedPosition) => _position > savedPosition;

    /// <summary>
    /// Gets the input character offset of the current token, or the end of the last token at end of input, for
    /// resource-refusal reporting.
    /// </summary>
    /// <returns>The offset, or <see langword="null"/> when the token list is empty.</returns>
    private int? CurrentCharacterOffset()
    {
        if (Peek() is { } current)
        {
            return current.Span.Position;
        }

        return Peek(-1) is { } last ? last.Span.Position + last.Span.Length : null;
    }
}
