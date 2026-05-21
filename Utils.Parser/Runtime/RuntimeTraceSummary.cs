namespace Utils.Parser.Runtime;

/// <summary>
/// Represents a deterministic, read-only summary of a runtime observation sequence.
/// </summary>
/// <param name="TotalObservations">Total number of observations in the analyzed sequence.</param>
/// <param name="EventDistribution">Count per observation kind.</param>
/// <param name="StatusDistribution">Count per observation status.</param>
/// <param name="RuleDistribution">Count per observed rule name.</param>
/// <param name="AlternativeDistribution">Count per observed alternative index.</param>
public sealed record RuntimeTraceSummary(
    int TotalObservations,
    IReadOnlyDictionary<ParserRuntimeObservationKind, int> EventDistribution,
    IReadOnlyDictionary<ParserRuntimeObservationStatus, int> StatusDistribution,
    IReadOnlyDictionary<string, int> RuleDistribution,
    IReadOnlyDictionary<int, int> AlternativeDistribution);
