namespace Utils.Parser.Runtime;

/// <summary>
/// Formats shared-prefix planning metadata into deterministic dry-run strings for tests and debugging.
/// </summary>
internal sealed class ParserSharedPrefixPlanFormatter
{
    /// <summary>Analyzer used to compute eligibility and blockers for each formatted plan.</summary>
    private readonly ParserSharedPrefixExecutionEligibilityAnalyzer eligibilityAnalyzer = new();

    /// <summary>
    /// Formats a list of shared-prefix plans while preserving plan and continuation order.
    /// </summary>
    /// <param name="plans">Shared-prefix plans to format.</param>
    /// <returns>A deterministic list of multi-line readable dry-run blocks.</returns>
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
    /// Formats one shared-prefix plan into a deterministic multi-line dry-run block.
    /// </summary>
    /// <param name="plan">Shared-prefix plan metadata.</param>
    /// <returns>
    /// A readable block with explicit shared segment, boundary, eligibility status,
    /// optional blockers, and continuations.
    /// </returns>
    private string FormatPlan(ParserSharedPrefixPlan plan)
    {
        var isFallbackBoundary = IsFallbackBoundary(plan);
        var eligibility = this.eligibilityAnalyzer.Analyze(plan);
        var lines = new List<string>
        {
            $"shared segment: {plan.Segment.SharedTokenName}",
            isFallbackBoundary
                ? $"boundary: position {plan.Segment.Boundary.SequencePosition} (fallback)"
                : $"boundary: position {plan.Segment.Boundary.SequencePosition}",
            $"eligibility: {eligibility.Eligibility}",
        };

        if (eligibility.Blockers.Count > 0)
        {
            lines.Add("blockers:");
            for (var index = 0; index < eligibility.Blockers.Count; index++)
            {
                var blocker = eligibility.Blockers[index];
                lines.Add($"  {blocker.Code}: {blocker.Message}");
            }
        }

        lines.Add(
            "continuations:"
        );

        for (var index = 0; index < plan.Continuations.Count; index++)
        {
            var continuation = plan.Continuations[index];
            lines.Add($"  alt {continuation.Key.AlternativeIndex} -> position {continuation.Key.SequencePosition}");
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Determines whether the boundary corresponds to a fallback state.
    /// </summary>
    /// <param name="plan">Plan to inspect.</param>
    /// <returns>
    /// <see langword="true"/> when at least one continuation position differs from the boundary position;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    private static bool IsFallbackBoundary(ParserSharedPrefixPlan plan)
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
