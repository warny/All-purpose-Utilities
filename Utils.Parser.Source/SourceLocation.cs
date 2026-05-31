namespace Utils.Parser.Source;

/// <summary>
/// Represents a point in a source text with both display coordinates and an absolute offset.
/// </summary>
/// <param name="FilePath">Optional path of the source file containing the location.</param>
/// <param name="Line">1-based line number for human-readable display.</param>
/// <param name="Column">1-based column number for human-readable display.</param>
/// <param name="Position">Zero-based absolute offset from the start of the source text.</param>
public sealed record SourceLocation(
    string? FilePath,
    int Line,
    int Column,
    int Position);
