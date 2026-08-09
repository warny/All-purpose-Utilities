using System.Collections.Immutable;

namespace Utils.Parser.Runtime;

/// <summary>
/// Immutable aggregate carrying all preparation outputs required by scheduler orchestration.
/// </summary>
/// <param name="StructuralDescriptors">Precomputed structural descriptors for ordered alternatives.</param>
/// <param name="LookaheadProbes">Precomputed shallow look-ahead probes for ordered alternatives.</param>
/// <param name="SharedPrefixCandidates">Precomputed shared-prefix candidates derived from look-ahead probes.</param>
/// <param name="ContinuationDescriptors">Precomputed continuation metadata descriptors.</param>
internal sealed record PreparedSchedulingInputs
{
    public PreparedSchedulingInputs(IReadOnlyList<AlternativeStructuralDescriptor> structuralDescriptors, IReadOnlyList<ParserLookaheadProbeResult> lookaheadProbes, IReadOnlyList<ParserLookaheadSharedPrefixCandidate> sharedPrefixCandidates, IReadOnlyList<ParserContinuationDescriptor> continuationDescriptors)
    {
        StructuralDescriptors = structuralDescriptors.ToImmutableArray();
        LookaheadProbes = lookaheadProbes.ToImmutableArray();
        SharedPrefixCandidates = sharedPrefixCandidates.ToImmutableArray();
        ContinuationDescriptors = continuationDescriptors.ToImmutableArray();
    }

    public IReadOnlyList<AlternativeStructuralDescriptor> StructuralDescriptors { get; }
    public IReadOnlyList<ParserLookaheadProbeResult> LookaheadProbes { get; }
    public IReadOnlyList<ParserLookaheadSharedPrefixCandidate> SharedPrefixCandidates { get; }
    public IReadOnlyList<ParserContinuationDescriptor> ContinuationDescriptors { get; }
}
