namespace Utils.Parser.Runtime;

/// <summary>
/// Describes shallow continuation metadata captured during parser exploration.
/// This descriptor is descriptive-only runtime metadata: it is non-authoritative and non-executable.
/// It does not grant parse acceptance, branch selection, replay, continuation execution,
/// resumable parsing, frame restoration, rollback safety, or semantic-equivalence guarantees.
/// </summary>
/// <param name="Key">Stable continuation identity.</param>
/// <param name="Category">Descriptive continuation category.</param>
/// <param name="ExpectedTokenNames">Optional shallow expected token names at this continuation point.</param>
/// <param name="IsSharedPrefixCandidate">Indicates whether this continuation originated from a shared-prefix observation.</param>
internal readonly record struct ParserContinuationDescriptor(
    ParserContinuationKey Key,
    ParserContinuationCategory Category,
    IReadOnlyList<string>? ExpectedTokenNames,
    bool IsSharedPrefixCandidate);
