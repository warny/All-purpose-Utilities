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
        IReadOnlyList<ParserContinuationDescriptor> continuations)
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
            plans.Add(new ParserSharedPrefixPlan(candidate.TokenName, alternativeIndexes, matchingContinuations));
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
}
