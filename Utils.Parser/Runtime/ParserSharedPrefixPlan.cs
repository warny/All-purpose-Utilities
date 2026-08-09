using System.Collections.Immutable;
namespace Utils.Parser.Runtime;
/// <summary>Represents immutable structural shared-prefix planning metadata.</summary>
internal readonly record struct ParserSharedPrefixPlan
{
    /// <summary>Initializes a plan and captures its ordered collection snapshots.</summary>
    public ParserSharedPrefixPlan(string sharedTokenName, IReadOnlyList<int> alternativeIndexes, IReadOnlyList<ParserContinuationDescriptor> continuations, ParserSharedPrefixSegment segment) { SharedTokenName = sharedTokenName; AlternativeIndexes = alternativeIndexes.ToImmutableArray(); Continuations = continuations.ToImmutableArray(); Segment = segment; }
    public string SharedTokenName { get; }
    public IReadOnlyList<int> AlternativeIndexes { get; }
    public IReadOnlyList<ParserContinuationDescriptor> Continuations { get; }
    public ParserSharedPrefixSegment Segment { get; }
}
