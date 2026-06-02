using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Prepares continuation descriptors before scheduler orchestration.
/// This component is descriptive-only and never transports active runtime state.
/// </summary>
internal sealed class ContinuationMetadataPreparation
{
    /// <summary>Factory used to create typed continuation descriptors.</summary>
    private readonly ParserContinuationFactory _continuationFactory = new();
    /// <summary>Extractor used to compute structural sequence positions for shared-prefix candidates.</summary>
    private readonly ContinuationStructuralPositionExtractor _positionExtractor = new();

    /// <summary>
    /// Produces continuation descriptors for ordered alternatives and look-ahead probes.
    /// The output is deterministic descriptive metadata and does not authorize execution behavior.
    /// </summary>
    public IReadOnlyList<ParserContinuationDescriptor> Prepare(
        Rule rule,
        IReadOnlyList<Alternative> orderedAlternatives,
        IReadOnlyList<ParserLookaheadProbeResult> probes,
        IReadOnlyList<ParserLookaheadSharedPrefixCandidate> candidates)
    {
        var candidateIndexes = candidates
            .SelectMany(static candidate => candidate.AlternativeIndexes)
            .ToHashSet();

        var continuations = new List<ParserContinuationDescriptor>(orderedAlternatives.Count);
        for (var index = 0; index < orderedAlternatives.Count; index++)
        {
            var expectedTokenNames = probes[index].ExpectedTokenNames;
            var sharedTokenName = ResolveSharedTokenName(candidates, index);
            var sequencePosition = sharedTokenName is null
                ? 0
                : _positionExtractor.ExtractSharedPrefixSequencePosition(orderedAlternatives[index], sharedTokenName);

            continuations.Add(_continuationFactory.Create(new ParserContinuationPreparationInput(
                rule.Name,
                index,
                sequencePosition,
                expectedTokenNames,
                candidateIndexes.Contains(index))));
        }

        return continuations;
    }

    /// <summary>Returns the shared token name for the specified alternative index when it participates in a shared-prefix candidate; otherwise <see langword="null"/>.</summary>
    private static string? ResolveSharedTokenName(
        IReadOnlyList<ParserLookaheadSharedPrefixCandidate> candidates,
        int alternativeIndex)
    {
        for (var index = 0; index < candidates.Count; index++)
        {
            if (candidates[index].AlternativeIndexes.Contains(alternativeIndex))
            {
                return candidates[index].TokenName;
            }
        }

        return null;
    }
}
