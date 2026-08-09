using System.Collections.Immutable;

namespace Utils.Parser.Runtime;

/// <summary>
/// Represents a deterministic, read-only summary of a runtime observation sequence.
/// </summary>
/// <param name="TotalObservations">Total number of observations in the analyzed sequence.</param>
/// <param name="EventDistribution">Count per observation kind.</param>
/// <param name="StatusDistribution">Count per observation status.</param>
/// <param name="RuleDistribution">Count per observed rule name.</param>
/// <param name="AlternativeDistribution">Count per observed alternative index.</param>
public sealed record RuntimeTraceSummary
{
    /// <summary>Initializes a summary by capturing immutable distribution snapshots.</summary>
    public RuntimeTraceSummary(int totalObservations, IReadOnlyDictionary<ParserRuntimeObservationKind, int> eventDistribution, IReadOnlyDictionary<ParserRuntimeObservationStatus, int> statusDistribution, IReadOnlyDictionary<string, int> ruleDistribution, IReadOnlyDictionary<int, int> alternativeDistribution)
    { TotalObservations = totalObservations; EventDistribution = eventDistribution.ToImmutableDictionary(); StatusDistribution = statusDistribution.ToImmutableDictionary(); RuleDistribution = ruleDistribution.ToImmutableDictionary(StringComparer.Ordinal); AlternativeDistribution = alternativeDistribution.ToImmutableDictionary(); }
    public int TotalObservations { get; }
    public IReadOnlyDictionary<ParserRuntimeObservationKind, int> EventDistribution { get; }
    public IReadOnlyDictionary<ParserRuntimeObservationStatus, int> StatusDistribution { get; }
    public IReadOnlyDictionary<string, int> RuleDistribution { get; }
    public IReadOnlyDictionary<int, int> AlternativeDistribution { get; }
}
