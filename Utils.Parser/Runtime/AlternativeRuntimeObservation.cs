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
    string Status);
