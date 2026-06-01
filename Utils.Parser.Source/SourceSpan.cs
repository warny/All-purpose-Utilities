namespace Utils.Parser.Source;

/// <summary>
/// Represents a technical range in a source text for lexer, parser, and runtime operations.
/// </summary>
/// <param name="Position">Zero-based absolute offset from the start of the source text.</param>
/// <param name="Length">Length of the range in the runtime text units used by the source representation.</param>
/// <param name="Line">1-based human-readable line where the range starts, when display coordinates are available.</param>
/// <param name="Column">1-based human-readable column where the range starts, when display coordinates are available.</param>
/// <param name="FilePath">Optional source file path used for display or diagnostics formatting.</param>
/// <remarks>
/// This type is used by runtime tokens, parse nodes, and related runtime analysis surfaces to
/// preserve absolute text offsets. Unlike <see cref="SourceCodeRange" />, it keeps the absolute
/// source position and allows the file path to be omitted when the source text is anonymous or
/// in-memory.
/// </remarks>
public sealed record SourceSpan(
    int Position,
    int Length,
    int Line = 1,
    int Column = 1,
    string? FilePath = null);
