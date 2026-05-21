using System.Text;

namespace Utils.Parser.Runtime;

/// <summary>
/// Formats runtime observations as stable text lines for deterministic tooling traces.
/// </summary>
public static class RuntimeObservationTextWriter
{
    /// <summary>
    /// Converts observations into a deterministic text trace.
    /// </summary>
    /// <param name="observations">Observations to render in their current sequence.</param>
    /// <returns>Newline-delimited trace text using invariant formatting.</returns>
    public static string Write(IEnumerable<AlternativeRuntimeObservation> observations)
    {
        var builder = new StringBuilder();

        foreach (var observation in observations)
        {
            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(observation.Kind);
            builder.Append(" status=");
            builder.Append(observation.Status);
            builder.Append(" rule=");
            builder.Append(observation.RuleName);
            builder.Append(" alt=");
            builder.Append(observation.AlternativeIndex);
            builder.Append(" priority=");
            builder.Append(observation.Priority);
            builder.Append(" origin=");
            builder.Append(observation.OriginInputPosition);
            builder.Append(" current=");
            builder.Append(observation.CurrentInputPosition);
        }

        return builder.ToString();
    }
}
