namespace Utils.Parser.Runtime;

/// <summary>
/// Runtime options for <see cref="LexerEngine"/>.
/// </summary>
public sealed class LexerEngineOptions
{
    /// <summary>Registered lexer extensions.</summary>
    public IReadOnlyList<ILexerExtension> Extensions { get; init; } = [];
}
