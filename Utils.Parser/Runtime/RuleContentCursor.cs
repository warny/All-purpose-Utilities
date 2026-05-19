namespace Utils.Parser.Runtime;

/// <summary>
/// Tracks the current position within the content of a grammar alternative during parsing.
/// Carries the element index and a diagnostic label identifying the kind of content at that position.
/// </summary>
internal sealed record RuleContentCursor
{
    /// <summary>The zero-based index of the current element within the alternative's content sequence.</summary>
    public required int Index { get; init; }

    /// <summary>A label describing the kind of content at this cursor position, used for diagnostics and tracing.</summary>
    public required string Kind { get; init; }
}
