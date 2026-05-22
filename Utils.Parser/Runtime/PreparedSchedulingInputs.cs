namespace Utils.Parser.Runtime;

/// <summary>
/// Immutable aggregate carrying all preparation outputs required by scheduler orchestration.
/// </summary>
/// <param name="StructuralDescriptors">Precomputed structural descriptors for ordered alternatives.</param>
/// <param name="LookaheadProbes">Precomputed shallow look-ahead probes for ordered alternatives.</param>
/// <param name="SharedPrefixCandidates">Precomputed shared-prefix candidates derived from look-ahead probes.</param>
/// <param name="ContinuationDescriptors">Precomputed continuation metadata descriptors.</param>
internal sealed record PreparedSchedulingInputs(
    IReadOnlyList<AlternativeStructuralDescriptor> StructuralDescriptors,
    IReadOnlyList<ParserLookaheadProbeResult> LookaheadProbes,
    IReadOnlyList<ParserLookaheadSharedPrefixCandidate> SharedPrefixCandidates,
    IReadOnlyList<ParserContinuationDescriptor> ContinuationDescriptors);
