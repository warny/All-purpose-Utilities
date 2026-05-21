using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Creates structural shared-prefix plans from shallow shared-token candidates and continuation metadata.
/// </summary>
internal sealed class ParserSharedPrefixPlanFactory
{
    /// <summary>
    /// Creates shared-prefix plans while preserving candidate ordering and continuation ordering by alternative index.
    /// </summary>
    /// <param name="candidates">Ordered shared-prefix candidates detected from shallow look-ahead observations.</param>
    /// <param name="continuations">Structural continuation descriptors available for grouping.</param>
    /// <returns>
    /// Shared-prefix plans with at least two matching continuations each. This metadata is informational only and
    /// does not execute shared prefixes or continuations.
    /// </returns>
    public IReadOnlyList<ParserSharedPrefixPlan> CreatePlans(
        IReadOnlyList<ParserLookaheadSharedPrefixCandidate> candidates,
        IReadOnlyList<ParserContinuationDescriptor> continuations,
        IReadOnlyDictionary<int, Alternative>? alternativesByIndex = null)
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
            var structuralTokens = BuildStructuralTokens(candidate, alternativesByIndex);
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
    /// <param name="expectedTokenNames">Shallow expected-token names on a continuation descriptor.</param>
    /// <param name="tokenName">Candidate shared token.</param>
    /// <returns><see langword="true"/> when the token name is present; otherwise <see langword="false"/>.</returns>
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

    private static IReadOnlyList<string> BuildStructuralTokens(
        ParserLookaheadSharedPrefixCandidate candidate,
        IReadOnlyDictionary<int, Alternative>? alternativesByIndex)
    {
        if (alternativesByIndex is null || alternativesByIndex.Count == 0)
        {
            return [candidate.TokenName];
        }

        var tokenSequences = new List<IReadOnlyList<string>>();
        foreach (var alternativeIndex in candidate.AlternativeIndexes)
        {
            if (!alternativesByIndex.TryGetValue(alternativeIndex, out var alternative))
            {
                return [candidate.TokenName];
            }

            tokenSequences.Add(ExtractStructuralTokens(alternative.Content));
        }

        if (tokenSequences.Count < 2)
        {
            return [candidate.TokenName];
        }

        var sharedPrefix = ComputeSharedPrefix(tokenSequences);
        return sharedPrefix.Count == 0 ? [candidate.TokenName] : sharedPrefix;
    }

    private static IReadOnlyList<string> ExtractStructuralTokens(RuleContent content)
    {
        if (content is Sequence sequence)
        {
            var tokens = new List<string>();
            for (var index = 0; index < sequence.Items.Count; index++)
            {
                if (!TryGetStructuralToken(sequence.Items[index], out var token))
                {
                    break;
                }

                tokens.Add(token);
            }

            return tokens;
        }

        return TryGetStructuralToken(content, out var singleToken) ? [singleToken] : [];
    }

    private static bool TryGetStructuralToken(RuleContent content, out string tokenName)
    {
        switch (content)
        {
            case RuleRef reference:
                tokenName = reference.RuleName;
                return true;
            case LiteralMatch literal:
                tokenName = literal.Value;
                return true;
            default:
                tokenName = string.Empty;
                return false;
        }
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

        return prefix;
    }
}
