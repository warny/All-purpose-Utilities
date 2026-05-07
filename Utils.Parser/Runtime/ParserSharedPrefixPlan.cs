namespace Utils.Parser.Runtime;

/// <summary>
/// Represents structural shared-prefix planning metadata that associates a shallow shared token
/// with the participating alternatives and their continuation descriptors.
/// </summary>
/// <param name="SharedTokenName">Shallow shared token identifier observed during look-ahead.</param>
/// <param name="AlternativeIndexes">Stable alternative indexes participating in this plan.</param>
/// <param name="Continuations">Continuation descriptors associated with participating alternatives.</param>
/// <param name="Segment">Explicit shared-prefix segment and conservative boundary metadata.</param>
internal readonly record struct ParserSharedPrefixPlan(
    string SharedTokenName,
    IReadOnlyList<int> AlternativeIndexes,
    IReadOnlyList<ParserContinuationDescriptor> Continuations,
    ParserSharedPrefixSegment Segment);
