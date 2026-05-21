namespace Utils.Parser.Runtime;

/// <summary>
/// Represents a shallow shared-prefix segment and its conservative structural boundary.
/// This metadata is preparatory only and is not executable.
/// </summary>
/// <param name="SharedTokenName">Shared shallow token identifier.</param>
/// <param name="StructuralTokens">Ordered structural token sequence shared by all participating alternatives.</param>
/// <param name="Boundary">Conservative boundary used for future continuation resumption planning.</param>
internal readonly record struct ParserSharedPrefixSegment(
    string SharedTokenName,
    IReadOnlyList<string> StructuralTokens,
    ParserSharedPrefixBoundary Boundary);
