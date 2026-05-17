namespace Utils.Parser.Runtime;

/// <summary>
/// Holds informational metadata produced during alternative scheduling.
/// This container is observable and testable, but non-authoritative:
/// metadata here cannot override parse acceptance, parse-tree shape, or diagnostics authority.
/// </summary>
internal sealed class AlternativeSchedulingMetadata
{
    /// <summary>
    /// Gets structural shared-prefix plans computed from shallow look-ahead observations.
    /// </summary>
    public IReadOnlyList<ParserSharedPrefixPlan> SharedPrefixPlans { get; init; } = [];
}
