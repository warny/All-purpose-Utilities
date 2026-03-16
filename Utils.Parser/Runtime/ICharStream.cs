namespace Utils.Parser.Runtime;

/// <summary>
/// Abstraction over a forward-readable character stream used by <see cref="LexerEngine"/>.
/// Supports position save/restore without allocation so that multiple lexer rules can
/// speculatively scan the same input in parallel.
/// </summary>
public interface ICharStream
{
    /// <summary>Current zero-based position in the stream.</summary>
    int Position { get; }

    /// <summary><c>true</c> when the end of the stream has been reached.</summary>
    bool IsEnd { get; }

    /// <summary>
    /// Peeks at the character at <c>Position + offset</c> without consuming it.
    /// Returns <c>'\0'</c> when the offset is past the end of the stream.
    /// </summary>
    /// <param name="offset">Zero-based offset from the current position (default 0).</param>
    char Peek(int offset = 0);

    /// <summary>
    /// Advances the position by <paramref name="count"/> characters.
    /// </summary>
    /// <param name="count">Number of characters to consume (default 1).</param>
    void Consume(int count = 1);

    /// <summary>
    /// Saves the current position and returns an opaque token that can be passed to
    /// <see cref="RestorePosition"/> to rewind the stream without allocation.
    /// </summary>
    /// <returns>An integer that encodes the saved position.</returns>
    int SavePosition();

    /// <summary>
    /// Restores the stream position to the value previously captured by <see cref="SavePosition"/>.
    /// </summary>
    /// <param name="savedPosition">Token returned by a prior call to <see cref="SavePosition"/>.</param>
    void RestorePosition(int savedPosition);
}
