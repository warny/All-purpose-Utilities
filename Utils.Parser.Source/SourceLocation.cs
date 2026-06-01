namespace Utils.Parser.Source;

/// <summary>
/// Represents a technical point in a source text for lexer, parser, and runtime operations.
/// </summary>
/// <param name="FilePath">Optional path of the source file containing the location, or <see langword="null" /> for anonymous or in-memory source text.</param>
/// <param name="Line">1-based human-readable line number associated with the same point, when display coordinates are available.</param>
/// <param name="Column">1-based human-readable column number associated with the same point, when display coordinates are available.</param>
/// <param name="Position">Zero-based absolute offset from the start of the source text.</param>
/// <remarks>
/// <see cref="SourceLocation" /> is a runtime/source-buffer coordinate contract. It is not
/// equivalent to <see cref="SourceCodeLocation" />, which intentionally carries no absolute
/// source offset and requires a file path for diagnostic or tooling display.
/// </remarks>
public sealed record SourceLocation(
    string? FilePath,
    int Line,
    int Column,
    int Position);
