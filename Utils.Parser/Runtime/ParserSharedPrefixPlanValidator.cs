namespace Utils.Parser.Runtime;

/// <summary>
/// Defines severities produced by shared-prefix plan metadata validation.
/// </summary>
internal enum ParserSharedPrefixPlanValidationSeverity
{
    /// <summary>
    /// Indicates informational metadata that does not invalidate a plan.
    /// </summary>
    Info,

    /// <summary>
    /// Indicates suspicious metadata that should be reviewed.
    /// </summary>
    Warning
}

/// <summary>
/// Represents one immutable validation issue produced for shared-prefix plan metadata.
/// </summary>
/// <param name="Severity">Severity assigned to this structural metadata issue.</param>
/// <param name="Message">Deterministic issue message describing the detected condition.</param>
internal readonly record struct ParserSharedPrefixPlanValidationIssue(
    ParserSharedPrefixPlanValidationSeverity Severity,
    string Message);

/// <summary>
/// Represents the immutable result of validating one shared-prefix plan.
/// </summary>
/// <param name="IsValid">
/// <see langword="true"/> when no invalidating structural metadata was detected;
/// otherwise, <see langword="false"/>.
/// </param>
/// <param name="Issues">Ordered structural metadata issues emitted during validation.</param>
internal readonly record struct ParserSharedPrefixPlanValidationResult(
    bool IsValid,
    IReadOnlyList<ParserSharedPrefixPlanValidationIssue> Issues);

/// <summary>
/// Validates shared-prefix planning metadata conservatively without executing parser logic.
/// </summary>
internal sealed class ParserSharedPrefixPlanValidator
{
    /// <summary>
    /// Validates one shared-prefix plan and returns deterministic structural metadata diagnostics.
    /// </summary>
    /// <param name="plan">Shared-prefix plan metadata to validate.</param>
    /// <returns>An immutable validation result describing structural consistency and suspicious states.</returns>
    public ParserSharedPrefixPlanValidationResult Validate(ParserSharedPrefixPlan plan)
    {
        var issues = new List<ParserSharedPrefixPlanValidationIssue>();
        var isValid = true;

        if (plan.Continuations.Count == 0)
        {
            issues.Add(new ParserSharedPrefixPlanValidationIssue(
                ParserSharedPrefixPlanValidationSeverity.Warning,
                "Shared-prefix plan has no continuations."));
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(plan.Segment.SharedTokenName))
        {
            issues.Add(new ParserSharedPrefixPlanValidationIssue(
                ParserSharedPrefixPlanValidationSeverity.Warning,
                "Shared-prefix segment token name is empty."));
            isValid = false;
        }

        if (plan.Segment.Boundary.SequencePosition < 0)
        {
            issues.Add(new ParserSharedPrefixPlanValidationIssue(
                ParserSharedPrefixPlanValidationSeverity.Warning,
                "Shared-prefix boundary position is negative."));
            isValid = false;
        }

        var seenAlternativeIndexes = new HashSet<int>();
        var hasDivergentContinuationPosition = false;
        for (var index = 0; index < plan.Continuations.Count; index++)
        {
            var continuation = plan.Continuations[index];
            if (continuation.Key.SequencePosition < 0)
            {
                issues.Add(new ParserSharedPrefixPlanValidationIssue(
                    ParserSharedPrefixPlanValidationSeverity.Warning,
                    "Shared-prefix continuation position is negative."));
                isValid = false;
            }

            if (!seenAlternativeIndexes.Add(continuation.Key.AlternativeIndex))
            {
                issues.Add(new ParserSharedPrefixPlanValidationIssue(
                    ParserSharedPrefixPlanValidationSeverity.Warning,
                    $"Shared-prefix continuation alternative index {continuation.Key.AlternativeIndex} is duplicated."));
            }

            if (continuation.Key.SequencePosition != plan.Segment.Boundary.SequencePosition)
            {
                hasDivergentContinuationPosition = true;
            }
        }

        if (hasDivergentContinuationPosition)
        {
            issues.Add(new ParserSharedPrefixPlanValidationIssue(
                ParserSharedPrefixPlanValidationSeverity.Info,
                "Shared-prefix boundary diverges from continuation positions; fallback metadata is expected."));

            if (plan.Segment.Boundary.SequencePosition != 0)
            {
                issues.Add(new ParserSharedPrefixPlanValidationIssue(
                    ParserSharedPrefixPlanValidationSeverity.Warning,
                    "Shared-prefix boundary position appears non-fallback while continuation positions diverge."));
            }
        }

        return new ParserSharedPrefixPlanValidationResult(isValid, issues);
    }
}
