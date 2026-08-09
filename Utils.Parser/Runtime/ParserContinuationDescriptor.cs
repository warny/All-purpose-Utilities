using System.Collections.Immutable;
namespace Utils.Parser.Runtime;
/// <summary>Describes immutable shallow continuation metadata captured during parser exploration.</summary>
internal readonly record struct ParserContinuationDescriptor
{
    /// <summary>Initializes a descriptor and captures its optional expected-token snapshot.</summary>
    public ParserContinuationDescriptor(ParserContinuationKey key, ParserContinuationCategory category, IReadOnlyList<string>? expectedTokenNames, bool isSharedPrefixCandidate) { Key = key; Category = category; ExpectedTokenNames = expectedTokenNames?.ToImmutableArray(); IsSharedPrefixCandidate = isSharedPrefixCandidate; }
    public ParserContinuationKey Key { get; }
    public ParserContinuationCategory Category { get; }
    public IReadOnlyList<string>? ExpectedTokenNames { get; }
    public bool IsSharedPrefixCandidate { get; }
}
