namespace Utils.Parser.Runtime;

/// <summary>
/// Represents a structural continuation boundary for shared-prefix planning metadata.
/// This boundary is informational only and does not capture runtime parser state.
/// </summary>
/// <param name="SequencePosition">Conservative resumption position within meaningful sequence items.</param>
/// <param name="ExpectedTokenNames">
/// Optional shallow expected-token snapshot at this structural boundary.
/// This value is intentionally <see langword="null"/> for now to avoid implying FOLLOW/restart semantics.
/// </param>
internal readonly record struct ParserSharedPrefixBoundary(
    int SequencePosition,
    IReadOnlyList<string>? ExpectedTokenNames);
