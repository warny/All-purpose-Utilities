namespace Utils.Parser.Runtime;

/// <summary>
/// Detects shared shallow first-token candidates from look-ahead probe observations.
/// </summary>
internal sealed class ParserLookaheadSharedPrefixDetector
{
    /// <summary>
    /// Groups alternatives by shared expected first-token names while preserving stable token ordering.
    /// </summary>
    /// <param name="probes">Ordered probe observations by alternative index.</param>
    /// <returns>Shared token candidates where at least two alternatives reference the same token.</returns>
    public IReadOnlyList<ParserLookaheadSharedPrefixCandidate> Detect(IReadOnlyList<ParserLookaheadProbeResult> probes)
    {
        var alternativesByToken = new Dictionary<string, SortedSet<int>>(StringComparer.Ordinal);
        var tokenOrder = new List<string>();

        for (var alternativeIndex = 0; alternativeIndex < probes.Count; alternativeIndex++)
        {
            var expectedTokenNames = probes[alternativeIndex].ExpectedTokenNames;
            if (expectedTokenNames is null || expectedTokenNames.Count == 0)
            {
                continue;
            }

            foreach (var tokenName in expectedTokenNames)
            {
                if (!alternativesByToken.TryGetValue(tokenName, out var alternativeIndexes))
                {
                    alternativeIndexes = [];
                    alternativesByToken[tokenName] = alternativeIndexes;
                    tokenOrder.Add(tokenName);
                }

                alternativeIndexes.Add(alternativeIndex);
            }
        }

        var candidates = new List<ParserLookaheadSharedPrefixCandidate>();
        foreach (var tokenName in tokenOrder)
        {
            var alternativeIndexes = alternativesByToken[tokenName];
            if (alternativeIndexes.Count < 2)
            {
                continue;
            }

            candidates.Add(new ParserLookaheadSharedPrefixCandidate(tokenName, alternativeIndexes.ToArray()));
        }

        return candidates;
    }
}
