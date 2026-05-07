namespace Utils.Parser.Runtime;

/// <summary>
/// Describes shallow continuation metadata for future continuation-aware parser orchestration.
/// </summary>
/// <param name="Key">Stable continuation identity.</param>
/// <param name="ExpectedTokenNames">Optional shallow expected token names at this continuation point.</param>
/// <param name="IsSharedPrefixCandidate">Indicates whether this continuation originated from a shared-prefix observation.</param>
internal readonly record struct ParserContinuationDescriptor(
    ParserContinuationKey Key,
    IReadOnlyList<string>? ExpectedTokenNames,
    bool IsSharedPrefixCandidate);
