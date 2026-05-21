using System.Text.Json;

namespace Utils.Parser.Runtime;

/// <summary>
/// Serializes runtime observations to deterministic JSON arrays for tooling experiments.
/// </summary>
public static class RuntimeObservationJsonWriter
{
    /// <summary>
    /// Serializes observations in sequence order using stable property naming.
    /// </summary>
    /// <param name="observations">Observations to serialize.</param>
    /// <returns>JSON representation that remains descriptive and non-authoritative.</returns>
    public static string Write(IEnumerable<AlternativeRuntimeObservation> observations)
    {
        var payload = observations.Select(static observation => new RuntimeObservationJsonRecord(
            observation.Kind.ToString(),
            observation.Status.ToString(),
            observation.RuleName,
            observation.AlternativeIndex,
            observation.CurrentInputPosition,
            observation.OriginInputPosition,
            observation.Priority));

        return JsonSerializer.Serialize(payload);
    }

    /// <summary>
    /// Represents serialized observation values with explicit stable JSON field names.
    /// </summary>
    /// <param name="Kind">Observation kind.</param>
    /// <param name="Status">Observation status.</param>
    /// <param name="Rule">Rule name.</param>
    /// <param name="Alternative">Alternative index.</param>
    /// <param name="CurrentInputPosition">Observed current input position.</param>
    /// <param name="OriginInputPosition">Observed origin input position.</param>
    /// <param name="Priority">Runtime priority.</param>
    private sealed record RuntimeObservationJsonRecord(
        string Kind,
        string Status,
        string Rule,
        int Alternative,
        int CurrentInputPosition,
        int OriginInputPosition,
        int Priority);
}
