namespace Utils.Parser.Runtime;

/// <summary>
/// Formats shared-prefix planning metadata into deterministic dry-run strings for tests and debugging.
/// </summary>
internal sealed class ParserSharedPrefixPlanFormatter
{
    /// <summary>
    /// Formats a list of shared-prefix plans while preserving plan order.
    /// </summary>
    /// <param name="plans">Shared-prefix plans to format.</param>
    /// <returns>A deterministic list of readable dry-run lines.</returns>
    public IReadOnlyList<string> FormatPlans(IReadOnlyList<ParserSharedPrefixPlan> plans)
    {
        if (plans.Count == 0)
        {
            return [];
        }

        var lines = new string[plans.Count];
        for (var index = 0; index < plans.Count; index++)
        {
            lines[index] = FormatPlan(plans[index]);
        }

        return lines;
    }

    /// <summary>
    /// Formats one shared-prefix plan into a single deterministic dry-run line.
    /// </summary>
    /// <param name="plan">Shared-prefix plan metadata.</param>
    /// <returns>A readable dry-run line with shared token, optional boundary, and continuation positions.</returns>
    private static string FormatPlan(ParserSharedPrefixPlan plan)
    {
        var parts = new List<string>
        {
            $"shared token: {plan.SharedTokenName}"
        };

        if (ShouldRenderBoundary(plan))
        {
            parts.Add($"boundary: position {plan.Segment.Boundary.SequencePosition}");
        }

        for (var index = 0; index < plan.Continuations.Count; index++)
        {
            var continuation = plan.Continuations[index];
            parts.Add($"alt {continuation.Key.AlternativeIndex} -> after position {continuation.Key.SequencePosition}");
        }

        return string.Join(" | ", parts);
    }

    /// <summary>
    /// Determines whether the shared-prefix boundary should be rendered explicitly.
    /// </summary>
    /// <param name="plan">Plan to inspect.</param>
    /// <returns>
    /// <see langword="true"/> when at least one continuation position differs from the boundary position;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    private static bool ShouldRenderBoundary(ParserSharedPrefixPlan plan)
    {
        for (var index = 0; index < plan.Continuations.Count; index++)
        {
            if (plan.Continuations[index].Key.SequencePosition != plan.Segment.Boundary.SequencePosition)
            {
                return true;
            }
        }

        return false;
    }
}
