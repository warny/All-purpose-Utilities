using System.Collections.ObjectModel;

namespace Utils.Parser.Runtime;

/// <summary>
/// Provides deterministic, read-only analysis over runtime observations and exports.
/// </summary>
public static class RuntimeTraceAnalyzer
{
    /// <summary>
    /// Builds a descriptive summary from the provided observation sequence.
    /// </summary>
    /// <param name="observations">Observations to summarize in deterministic sequence order.</param>
    /// <returns>A deterministic summary containing counts and read-only distributions.</returns>
    public static RuntimeTraceSummary Summarize(IEnumerable<AlternativeRuntimeObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);

        var materialized = observations.ToArray();

        return new RuntimeTraceSummary(
            materialized.Length,
            CountBy(materialized, static observation => observation.Kind),
            CountBy(materialized, static observation => observation.Status),
            CountBy(materialized, static observation => observation.RuleName),
            CountBy(materialized, static observation => observation.AlternativeIndex));
    }

    /// <summary>
    /// Builds a deterministic, descriptive comparison between two observation sequences.
    /// </summary>
    /// <param name="first">First sequence to compare.</param>
    /// <param name="second">Second sequence to compare.</param>
    /// <returns>Deterministic comparison values based on passive observations, with optional export identity indicators.</returns>
    public static RuntimeTraceComparison Compare(
        IEnumerable<AlternativeRuntimeObservation> first,
        IEnumerable<AlternativeRuntimeObservation> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var firstMaterialized = first.ToArray();
        var secondMaterialized = second.ToArray();

        var firstSummary = Summarize(firstMaterialized);
        var secondSummary = Summarize(secondMaterialized);

        return new RuntimeTraceComparison(
            AreSummariesEquivalent(firstSummary, secondSummary),
            RuntimeObservationTextWriter.Write(firstMaterialized) == RuntimeObservationTextWriter.Write(secondMaterialized),
            RuntimeObservationJsonWriter.Write(firstMaterialized) == RuntimeObservationJsonWriter.Write(secondMaterialized),
            firstSummary.TotalObservations,
            secondSummary.TotalObservations,
            ComputeEventDelta(firstSummary.EventDistribution, secondSummary.EventDistribution));
    }

    private static bool AreSummariesEquivalent(RuntimeTraceSummary first, RuntimeTraceSummary second)
    {
        return first.TotalObservations == second.TotalObservations
            && AreDictionariesEquivalent(first.EventDistribution, second.EventDistribution)
            && AreDictionariesEquivalent(first.StatusDistribution, second.StatusDistribution)
            && AreDictionariesEquivalent(first.RuleDistribution, second.RuleDistribution)
            && AreDictionariesEquivalent(first.AlternativeDistribution, second.AlternativeDistribution);
    }

    private static bool AreDictionariesEquivalent<TKey>(IReadOnlyDictionary<TKey, int> first, IReadOnlyDictionary<TKey, int> second)
        where TKey : notnull
    {
        if (first.Count != second.Count)
        {
            return false;
        }

        foreach (var entry in first)
        {
            if (!second.TryGetValue(entry.Key, out var value) || value != entry.Value)
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyDictionary<T, int> CountBy<T>(
        IEnumerable<AlternativeRuntimeObservation> observations,
        Func<AlternativeRuntimeObservation, T> selector)
        where T : notnull
    {
        var counts = new Dictionary<T, int>();

        foreach (var observation in observations)
        {
            var key = selector(observation);
            if (counts.TryGetValue(key, out var current))
            {
                counts[key] = current + 1;
            }
            else
            {
                counts[key] = 1;
            }
        }

        return new ReadOnlyDictionary<T, int>(counts);
    }

    private static IReadOnlyDictionary<ParserRuntimeObservationKind, int> ComputeEventDelta(
        IReadOnlyDictionary<ParserRuntimeObservationKind, int> first,
        IReadOnlyDictionary<ParserRuntimeObservationKind, int> second)
    {
        var combinedKeys = first.Keys.Concat(second.Keys).Distinct().OrderBy(static key => (int)key);
        var delta = new Dictionary<ParserRuntimeObservationKind, int>();

        foreach (var key in combinedKeys)
        {
            first.TryGetValue(key, out var firstCount);
            second.TryGetValue(key, out var secondCount);
            delta[key] = firstCount - secondCount;
        }

        return new ReadOnlyDictionary<ParserRuntimeObservationKind, int>(delta);
    }
}
