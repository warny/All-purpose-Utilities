namespace Utils.Parser.Runtime;

/// <summary>
/// Holds informational metadata produced during alternative scheduling.
/// </summary>
internal sealed class AlternativeSchedulingMetadata
{
    /// <summary>
    /// Gets structural shared-prefix plans computed from shallow look-ahead observations.
    /// </summary>
    public IReadOnlyList<ParserSharedPrefixPlan> SharedPrefixPlans { get; init; } = [];
}
