namespace Utils.Parser.Runtime;

/// <summary>
/// Describes a passive scheduler observation for a single alternative.
/// This payload is immutable and descriptive-only.
/// </summary>
/// <param name="RuleName">Rule currently being scheduled.</param>
/// <param name="AlternativeIndex">Deterministic alternative index in the ordered schedule.</param>
/// <param name="Priority">Alternative priority value.</param>
/// <param name="OriginInputPosition">Input position where scheduling started.</param>
/// <param name="CurrentInputPosition">Observed current input position for the event.</param>
/// <param name="Status">Observed scheduler status label.</param>
public sealed record AlternativeRuntimeObservation(
    string RuleName,
    int AlternativeIndex,
    int Priority,
    int OriginInputPosition,
    int CurrentInputPosition,
    string Status)
{
    /// <summary>
    /// Gets a normalized status parsed from <see cref="Status"/>.
    /// Unknown values are mapped to <see cref="ActiveParseStateStatus.Active"/>.
    /// </summary>
    public ParserRuntimeObservationStatus NormalizedStatus => Enum.TryParse<ParserRuntimeObservationStatus>(Status, ignoreCase: true, out var status)
        ? status
        : ParserRuntimeObservationStatus.Unknown;

    /// <summary>
    /// Gets a normalized observation kind inferred from <see cref="NormalizedStatus"/>.
    /// This value is descriptive and not an execution contract.
    /// </summary>
    public ParserRuntimeObservationKind Kind => NormalizedStatus switch
    {
        ParserRuntimeObservationStatus.Active => ParserRuntimeObservationKind.AlternativeStarted,
        ParserRuntimeObservationStatus.Completed => ParserRuntimeObservationKind.AlternativeCompleted,
        ParserRuntimeObservationStatus.Failed => ParserRuntimeObservationKind.AlternativeFailed,
        ParserRuntimeObservationStatus.Pruned => ParserRuntimeObservationKind.AlternativePruned,
        _ => ParserRuntimeObservationKind.Unknown
    };
}
