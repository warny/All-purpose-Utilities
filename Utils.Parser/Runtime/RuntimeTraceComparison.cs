using System.Collections.Immutable;

namespace Utils.Parser.Runtime;

/// <summary>
/// Represents a deterministic descriptive comparison between two observation sequences.
/// </summary>
/// <param name="AreSummariesEquivalent">Indicates whether the two deterministic summaries are equivalent.</param>
/// <param name="AreTextExportsIdentical">Informational flag indicating whether deterministic text exports are byte-identical.</param>
/// <param name="AreJsonExportsIdentical">Informational flag indicating whether deterministic JSON exports are byte-identical.</param>
/// <param name="FirstTotalObservations">Observation count in the first sequence.</param>
/// <param name="SecondTotalObservations">Observation count in the second sequence.</param>
/// <param name="EventCountDelta">Per-event-kind count deltas computed as first minus second.</param>
public sealed record RuntimeTraceComparison
{
    /// <summary>Initializes a comparison by capturing an immutable delta snapshot.</summary>
    public RuntimeTraceComparison(bool areSummariesEquivalent, bool areTextExportsIdentical, bool areJsonExportsIdentical, int firstTotalObservations, int secondTotalObservations, IReadOnlyDictionary<ParserRuntimeObservationKind, int> eventCountDelta)
    { AreSummariesEquivalent = areSummariesEquivalent; AreTextExportsIdentical = areTextExportsIdentical; AreJsonExportsIdentical = areJsonExportsIdentical; FirstTotalObservations = firstTotalObservations; SecondTotalObservations = secondTotalObservations; EventCountDelta = eventCountDelta.ToImmutableDictionary(); }
    public bool AreSummariesEquivalent { get; }
    public bool AreTextExportsIdentical { get; }
    public bool AreJsonExportsIdentical { get; }
    public int FirstTotalObservations { get; }
    public int SecondTotalObservations { get; }
    public IReadOnlyDictionary<ParserRuntimeObservationKind, int> EventCountDelta { get; }
}
