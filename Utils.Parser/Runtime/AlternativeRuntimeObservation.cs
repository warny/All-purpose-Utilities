namespace Utils.Parser.Runtime;

/// <summary>
/// Describes a passive scheduler observation for a single alternative.
/// This payload is immutable and descriptive-only.
/// </summary>
/// <param name="Kind">Observed scheduler event kind.</param>
/// <param name="RuleName">Rule currently being scheduled.</param>
/// <param name="AlternativeIndex">Deterministic alternative index in the ordered schedule.</param>
/// <param name="Priority">Alternative priority value.</param>
/// <param name="OriginInputPosition">Input position where scheduling started.</param>
/// <param name="CurrentInputPosition">Observed current input position for the event.</param>
/// <param name="Status">Observed scheduler status.</param>
public sealed record AlternativeRuntimeObservation(
    ParserRuntimeObservationKind Kind,
    string RuleName,
    int AlternativeIndex,
    int Priority,
    int OriginInputPosition,
    int CurrentInputPosition,
    ParserRuntimeObservationStatus Status)
{
    /// <summary>
    /// Initializes an observation from legacy status text while preserving explicit event kind.
    /// </summary>
    public AlternativeRuntimeObservation(
        string ruleName,
        int alternativeIndex,
        int priority,
        int originInputPosition,
        int currentInputPosition,
        string status)
        : this(
            InferKindFromLegacyStatus(status),
            ruleName,
            alternativeIndex,
            priority,
            originInputPosition,
            currentInputPosition,
            ParseLegacyStatus(status))
    {
    }

    private static ParserRuntimeObservationStatus ParseLegacyStatus(string status)
    {
        return Enum.TryParse<ParserRuntimeObservationStatus>(status, ignoreCase: true, out var normalized)
            ? normalized
            : ParserRuntimeObservationStatus.Unknown;
    }

    private static ParserRuntimeObservationKind InferKindFromLegacyStatus(string status)
    {
        return ParseLegacyStatus(status) switch
        {
            ParserRuntimeObservationStatus.Active => ParserRuntimeObservationKind.AlternativeStarted,
            ParserRuntimeObservationStatus.Completed => ParserRuntimeObservationKind.AlternativeCompleted,
            ParserRuntimeObservationStatus.Failed => ParserRuntimeObservationKind.AlternativeFailed,
            ParserRuntimeObservationStatus.Pruned => ParserRuntimeObservationKind.AlternativePruned,
            _ => ParserRuntimeObservationKind.Unknown
        };
    }
}
