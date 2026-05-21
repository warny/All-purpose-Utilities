namespace Utils.Parser.Runtime;

/// <summary>
/// Creates structural shared-prefix plans from shallow shared-token candidates and continuation metadata.
/// This factory aggregates pre-computed structural descriptors and does not inspect grammar model objects.
/// </summary>
internal sealed class ParserSharedPrefixPlanFactory
{
    /// <summary>
    /// Creates shared-prefix plans while preserving candidate ordering and continuation ordering by alternative index.
    /// </summary>
    /// <param name="candidates">Ordered shared-prefix candidates detected from shallow look-ahead observations.</param>
    /// <param name="continuations">Structural continuation descriptors available for grouping.</param>
    /// <param name="structuralDescriptors">
    /// Optional pre-computed per-alternative structural descriptors produced by
    /// <see cref="AlternativeStructuralPrefixExtractor"/>. When absent the segment falls back to the
    /// shallow shared token name as a single-element structural prefix.
    /// </param>
    /// <returns>
    /// Shared-prefix plans with at least two matching continuations each. This metadata is informational only and
    /// does not execute shared prefixes or continuations.
    /// </returns>
    public IReadOnlyList<ParserSharedPrefixPlan> CreatePlans(
        IReadOnlyList<ParserLookaheadSharedPrefixCandidate> candidates,
        IReadOnlyList<ParserContinuationDescriptor> continuations,
        IReadOnlyList<AlternativeStructuralDescriptor>? structuralDescriptors = null)
    {
        if (candidates.Count == 0 || continuations.Count == 0)
        {
            return [];
        }

        var plans = new List<ParserSharedPrefixPlan>();
        foreach (var candidate in candidates)
        {
            var matchingContinuations = MatchContinuations(candidate, continuations);
            if (matchingContinuations.Count < 2)
            {
                continue;
            }

            var alternativeIndexes = matchingContinuations.Select(static continuation => continuation.Key.AlternativeIndex).ToArray();
            var boundary = BuildBoundary(matchingContinuations);
            var structuralTokens = BuildStructuralTokens(candidate, structuralDescriptors);
            var segment = new ParserSharedPrefixSegment(candidate.TokenName, structuralTokens, boundary);
            plans.Add(new ParserSharedPrefixPlan(candidate.TokenName, alternativeIndexes, matchingContinuations, segment));
        }

        return plans;
    }

    /// <summary>
    /// Selects continuations that structurally match a shared-prefix candidate.
    /// </summary>
    /// <param name="candidate">Shared-prefix candidate token and alternatives.</param>
    /// <param name="continuations">Ordered continuation descriptors.</param>
    /// <returns>Matching continuation descriptors ordered by alternative index.</returns>
    private static List<ParserContinuationDescriptor> MatchContinuations(
        ParserLookaheadSharedPrefixCandidate candidate,
        IReadOnlyList<ParserContinuationDescriptor> continuations)
    {
        var candidateIndexes = candidate.AlternativeIndexes.ToHashSet();
        var matchingByAlternative = new Dictionary<int, ParserContinuationDescriptor>();

        foreach (var continuation in continuations)
        {
            if (!candidateIndexes.Contains(continuation.Key.AlternativeIndex))
            {
                continue;
            }

            if (!ContainsExpectedToken(continuation.ExpectedTokenNames, candidate.TokenName))
            {
                continue;
            }

            if (!matchingByAlternative.ContainsKey(continuation.Key.AlternativeIndex))
            {
                matchingByAlternative[continuation.Key.AlternativeIndex] = continuation;
            }
        }

        var orderedMatching = matchingByAlternative
            .OrderBy(static pair => pair.Key)
            .Select(static pair => pair.Value)
            .ToList();

        return orderedMatching;
    }

    /// <summary>
    /// Determines whether a continuation descriptor contains the candidate token in its shallow expected-token list.
    /// </summary>
    private static bool ContainsExpectedToken(IReadOnlyList<string>? expectedTokenNames, string tokenName)
    {
        if (expectedTokenNames is null || expectedTokenNames.Count == 0)
        {
            return false;
        }

        for (var index = 0; index < expectedTokenNames.Count; index++)
        {
            if (string.Equals(expectedTokenNames[index], tokenName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Builds a conservative shared-prefix boundary from matching continuations.
    /// Falls back to sequence position zero when continuations disagree.
    /// Expected-token names remain <see langword="null"/> to avoid suggesting
    /// runtime FOLLOW-token or continuation-restart semantics.
    /// </summary>
    private static ParserSharedPrefixBoundary BuildBoundary(
        IReadOnlyList<ParserContinuationDescriptor> continuations)
    {
        if (continuations.Count == 0)
        {
            return new ParserSharedPrefixBoundary(0, null);
        }

        var expectedPosition = continuations[0].Key.SequencePosition;
        for (var index = 1; index < continuations.Count; index++)
        {
            if (continuations[index].Key.SequencePosition != expectedPosition)
            {
                return new ParserSharedPrefixBoundary(0, null);
            }
        }

        return new ParserSharedPrefixBoundary(expectedPosition, null);
    }

    /// <summary>
    /// Aggregates pre-computed structural token sequences from descriptors to determine the shared prefix.
    /// Falls back to the candidate's shallow token name when descriptors are absent or incomplete.
    /// </summary>
    private static IReadOnlyList<string> BuildStructuralTokens(
        ParserLookaheadSharedPrefixCandidate candidate,
        IReadOnlyList<AlternativeStructuralDescriptor>? structuralDescriptors)
    {
        if (structuralDescriptors is null || structuralDescriptors.Count == 0)
        {
            return [candidate.TokenName];
        }

        var tokensByIndex = new Dictionary<int, IReadOnlyList<string>>(structuralDescriptors.Count);
        foreach (var descriptor in structuralDescriptors)
        {
            tokensByIndex[descriptor.AlternativeIndex] = descriptor.StructuralTokens;
        }

        var tokenSequences = new List<IReadOnlyList<string>>();
        foreach (var alternativeIndex in candidate.AlternativeIndexes)
        {
            if (!tokensByIndex.TryGetValue(alternativeIndex, out var tokens))
            {
                return [candidate.TokenName];
            }

            tokenSequences.Add(tokens);
        }

        if (tokenSequences.Count < 2)
        {
            return [candidate.TokenName];
        }

        var sharedPrefix = ComputeSharedPrefix(tokenSequences);
        return sharedPrefix.Count == 0 ? [candidate.TokenName] : sharedPrefix;
    }

    private static IReadOnlyList<string> ComputeSharedPrefix(IReadOnlyList<IReadOnlyList<string>> sequences)
    {
        var minimumLength = sequences.Min(static sequence => sequence.Count);
        var prefix = new List<string>(minimumLength);

        for (var index = 0; index < minimumLength; index++)
        {
            var expected = sequences[0][index];
            var allMatch = true;
            for (var sequenceIndex = 1; sequenceIndex < sequences.Count; sequenceIndex++)
            {
                if (!string.Equals(sequences[sequenceIndex][index], expected, StringComparison.Ordinal))
                {
                    allMatch = false;
                    break;
                }
            }

            if (!allMatch)
            {
                break;
            }

            prefix.Add(expected);
        }

        return prefix.AsReadOnly();
    }
}
