namespace Utils.Parser.Runtime;

/// <summary>
/// Read-only forward lookahead abstraction over lexer input.
/// </summary>
public interface TextReaderLookahead
{
    /// <summary>
    /// Peeks the character at the specified lookahead offset.
    /// Returns <c>-1</c> when end of input is reached.
    /// </summary>
    /// <param name="offset">Zero-based lookahead offset.</param>
    int Peek(int offset);

    /// <summary>Current zero-based absolute position in the stream.</summary>
    int Position { get; }

    /// <summary>Current 1-based line number.</summary>
    int Line { get; }

    /// <summary>Current 1-based column number.</summary>
    int Column { get; }

    /// <summary>Indicates whether the input is at end of stream.</summary>
    bool IsEnd { get; }
}
