using System.Collections.Immutable;
namespace Utils.Parser.Runtime;
/// <summary>Describes immutable shallow continuation metadata captured during parser exploration.</summary>
internal readonly record struct ParserContinuationDescriptor
{
    /// <summary>Initializes a descriptor and captures its optional expected-token snapshot.</summary>
    /// <param name="key">Stable continuation identity.</param>
    /// <param name="category">Descriptive continuation category.</param>
    /// <param name="expectedTokenNames">Optional expected-token names; <see langword="null"/> when unavailable.</param>
    /// <param name="isSharedPrefixCandidate">Whether the continuation originated from a shared-prefix observation.</param>
    public ParserContinuationDescriptor(ParserContinuationKey key, ParserContinuationCategory category, IReadOnlyList<string>? expectedTokenNames, bool isSharedPrefixCandidate) { Key = key; Category = category; ExpectedTokenNames = expectedTokenNames?.ToImmutableArray(); IsSharedPrefixCandidate = isSharedPrefixCandidate; }
    public ParserContinuationKey Key { get; }
    public ParserContinuationCategory Category { get; }
    public IReadOnlyList<string>? ExpectedTokenNames { get; }
    public bool IsSharedPrefixCandidate { get; }
}
