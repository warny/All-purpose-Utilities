namespace Utils.Parser.Source;

/// <summary>
/// Represents a span in a source text with an absolute offset and length.
/// </summary>
/// <param name="Position">Zero-based absolute offset from the start of the source text.</param>
/// <param name="Length">Number of characters covered by this span.</param>
/// <param name="Line">1-based line where the span starts, when display coordinates are available.</param>
/// <param name="Column">1-based column where the span starts, when display coordinates are available.</param>
/// <param name="FilePath">Optional source file path used for display or diagnostics formatting.</param>
/// <remarks>
/// This type is used by runtime tokens and parse nodes to preserve text offsets. Unlike
/// <see cref="SourceCodeRange"/>, it keeps the absolute source position and allows the file path
/// to be omitted when the source text is anonymous or in-memory.
/// </remarks>
public sealed record SourceSpan(
    int Position,
    int Length,
    int Line = 1,
    int Column = 1,
    string? FilePath = null);
