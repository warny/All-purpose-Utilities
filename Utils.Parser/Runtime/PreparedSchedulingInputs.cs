using System.Collections.Immutable;

namespace Utils.Parser.Runtime;

/// <summary>
/// Immutable aggregate carrying all preparation outputs required by scheduler orchestration.
/// </summary>
internal sealed record PreparedSchedulingInputs
{
    /// <summary>Initializes immutable scheduling inputs.</summary>
    /// <param name="structuralDescriptors">Precomputed structural descriptors for ordered alternatives.</param>
    /// <param name="lookaheadProbes">Precomputed shallow look-ahead probes for ordered alternatives.</param>
    /// <param name="sharedPrefixCandidates">Precomputed shared-prefix candidates derived from look-ahead probes.</param>
    /// <param name="continuationDescriptors">Precomputed continuation metadata descriptors.</param>
    public PreparedSchedulingInputs(IReadOnlyList<AlternativeStructuralDescriptor> structuralDescriptors, IReadOnlyList<ParserLookaheadProbeResult> lookaheadProbes, IReadOnlyList<ParserLookaheadSharedPrefixCandidate> sharedPrefixCandidates, IReadOnlyList<ParserContinuationDescriptor> continuationDescriptors)
    {
        StructuralDescriptors = structuralDescriptors.ToImmutableArray();
        LookaheadProbes = lookaheadProbes.ToImmutableArray();
        SharedPrefixCandidates = sharedPrefixCandidates.ToImmutableArray();
        ContinuationDescriptors = continuationDescriptors.ToImmutableArray();
    }

    /// <summary>Gets the immutable structural-descriptor snapshot.</summary>
    public IReadOnlyList<AlternativeStructuralDescriptor> StructuralDescriptors { get; }

    /// <summary>Gets the immutable look-ahead-probe snapshot.</summary>
    public IReadOnlyList<ParserLookaheadProbeResult> LookaheadProbes { get; }

    /// <summary>Gets the immutable shared-prefix-candidate snapshot.</summary>
    public IReadOnlyList<ParserLookaheadSharedPrefixCandidate> SharedPrefixCandidates { get; }

    /// <summary>Gets the immutable continuation-descriptor snapshot.</summary>
    public IReadOnlyList<ParserContinuationDescriptor> ContinuationDescriptors { get; }
}
