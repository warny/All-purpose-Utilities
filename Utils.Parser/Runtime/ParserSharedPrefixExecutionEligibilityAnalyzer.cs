namespace Utils.Parser.Runtime;

/// <summary>
/// Represents conservative execution eligibility classifications for shared-prefix plan metadata.
/// </summary>
internal enum ParserSharedPrefixExecutionEligibility
{
    /// <summary>
    /// Indicates that the metadata shape is conservatively safe for future shared-prefix execution experiments.
    /// </summary>
    Eligible,

    /// <summary>
    /// Indicates that fallback-only execution should be required if execution is ever introduced.
    /// </summary>
    RequiresFallback,

    /// <summary>
    /// Indicates that the metadata shape is currently unsupported for execution.
    /// </summary>
    NotEligible,

    /// <summary>
    /// Indicates contradictory or corrupted metadata that should be treated as unsafe.
    /// </summary>
    Unsafe
}

/// <summary>
/// Represents one deterministic blocker emitted by shared-prefix execution eligibility analysis.
/// </summary>
/// <param name="Code">Stable blocker code.</param>
/// <param name="Message">Deterministic blocker message.</param>
internal readonly record struct ParserSharedPrefixExecutionBlocker(
    string Code,
    string Message);

/// <summary>
/// Represents the immutable result of analyzing one shared-prefix plan for execution eligibility.
/// </summary>
/// <param name="Eligibility">Conservative execution eligibility classification.</param>
/// <param name="Blockers">Deterministic blockers explaining why the plan is not fully eligible.</param>
internal readonly record struct ParserSharedPrefixExecutionEligibilityResult(
    ParserSharedPrefixExecutionEligibility Eligibility,
    IReadOnlyList<ParserSharedPrefixExecutionBlocker> Blockers);

/// <summary>
/// Analyzes shared-prefix plan metadata conservatively without executing parser logic.
/// </summary>
internal sealed class ParserSharedPrefixExecutionEligibilityAnalyzer
{
    private readonly ParserSharedPrefixPlanValidator validator = new();

    /// <summary>
    /// Analyzes one shared-prefix plan and classifies conservative execution eligibility.
    /// </summary>
    /// <param name="plan">Shared-prefix plan metadata to classify.</param>
    /// <returns>Immutable classification and deterministic blockers.</returns>
    public ParserSharedPrefixExecutionEligibilityResult Analyze(ParserSharedPrefixPlan plan)
    {
        var blockers = new List<ParserSharedPrefixExecutionBlocker>();
        var validation = this.validator.Validate(plan);

        var boundaryPosition = plan.Segment.Boundary.SequencePosition;
        if (boundaryPosition < 0)
        {
            blockers.Add(new ParserSharedPrefixExecutionBlocker("SP004", "Negative shared-prefix position detected."));
        }

        if (string.IsNullOrWhiteSpace(plan.Segment.SharedTokenName))
        {
            blockers.Add(new ParserSharedPrefixExecutionBlocker("SP005", "Shared segment token is empty."));
        }

        if (plan.Continuations.Count == 0)
        {
            blockers.Add(new ParserSharedPrefixExecutionBlocker("SP006", "Shared-prefix plan has no continuations."));
        }

        var seenAlternativeIndexes = new HashSet<int>();
        var hasDuplicateAlternativeIndex = false;
        var hasNegativeContinuationPosition = false;
        var hasDivergentContinuationPosition = false;

        for (var index = 0; index < plan.Continuations.Count; index++)
        {
            var continuation = plan.Continuations[index];
            if (!seenAlternativeIndexes.Add(continuation.Key.AlternativeIndex))
            {
                hasDuplicateAlternativeIndex = true;
            }

            if (continuation.Key.SequencePosition < 0)
            {
                hasNegativeContinuationPosition = true;
            }

            if (continuation.Key.SequencePosition != boundaryPosition)
            {
                hasDivergentContinuationPosition = true;
            }
        }

        if (hasDuplicateAlternativeIndex)
        {
            blockers.Add(new ParserSharedPrefixExecutionBlocker("SP003", "Duplicate alternative indexes detected."));
        }

        if (hasNegativeContinuationPosition)
        {
            blockers.Add(new ParserSharedPrefixExecutionBlocker("SP008", "Negative continuation sequence position detected."));
        }

        if (hasDivergentContinuationPosition)
        {
            blockers.Add(new ParserSharedPrefixExecutionBlocker("SP002", "Continuation positions diverge."));
        }

        if (boundaryPosition == 0)
        {
            blockers.Add(new ParserSharedPrefixExecutionBlocker("SP001", "Fallback boundary prevents safe execution."));
        }

        if (!validation.IsValid)
        {
            blockers.Add(new ParserSharedPrefixExecutionBlocker("SP007", "Plan validation failed."));
        }

        var hasNonFallbackDivergenceWarning = validation.Issues.Any(static issue =>
            issue.Severity == ParserSharedPrefixPlanValidationSeverity.Warning
            && issue.Message.Contains("non-fallback", StringComparison.Ordinal));

        if (hasNonFallbackDivergenceWarning)
        {
            return new ParserSharedPrefixExecutionEligibilityResult(ParserSharedPrefixExecutionEligibility.Unsafe, blockers);
        }

        if (boundaryPosition < 0 || hasNegativeContinuationPosition)
        {
            return new ParserSharedPrefixExecutionEligibilityResult(ParserSharedPrefixExecutionEligibility.Unsafe, blockers);
        }

        if (hasDivergentContinuationPosition && boundaryPosition == 0)
        {
            return new ParserSharedPrefixExecutionEligibilityResult(ParserSharedPrefixExecutionEligibility.RequiresFallback, blockers);
        }

        if (validation.IsValid && blockers.Count == 0)
        {
            return new ParserSharedPrefixExecutionEligibilityResult(ParserSharedPrefixExecutionEligibility.Eligible, blockers);
        }

        return new ParserSharedPrefixExecutionEligibilityResult(ParserSharedPrefixExecutionEligibility.NotEligible, blockers);
    }
}
