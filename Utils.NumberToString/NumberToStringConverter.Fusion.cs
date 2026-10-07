using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Utils.NumberToString
{
    public partial class NumberToStringConverter
    {
        /// <summary>
        /// Highest group level supporting <c>&lt;Fusion&gt;</c>: its remainder domain
        /// [1, 10^(level-1) - 1] is compiled into a dense lookup table, so the level is bounded.
        /// </summary>
        internal const int MaxFusionGroupLevel = 6;

        /// <summary>
        /// The effective, already merged transformation applied to one fused junction.
        /// </summary>
        /// <param name="Left">Replacement form of the left constituent, or <see langword="null"/>.</param>
        /// <param name="Right">Replacement form of the right constituent, or <see langword="null"/>.</param>
        /// <param name="RemoveLeft">Suffix removed from the left constituent, or <see langword="null"/>.</param>
        /// <param name="RemoveRight">Prefix removed from the right constituent, or <see langword="null"/>.</param>
        private sealed record FusionPlan(string? Left, string? Right, string? RemoveLeft, string? RemoveRight);

        /// <summary>Composes two constituents according to a fusion plan: overrides first, then removals.</summary>
        /// <param name="plan">The effective plan.</param>
        /// <param name="left">The digit text (left constituent).</param>
        /// <param name="right">The lower sub-group text (right constituent).</param>
        /// <returns>The fused text.</returns>
        private static string ComposeFusion(FusionPlan plan, string left, string right)
        {
            left = plan.Left ?? left;
            right = plan.Right ?? right;
            // Load-time validation guarantees both edges are present; stay non-destructive anyway.
            if (plan.RemoveLeft != null && left.EndsWith(plan.RemoveLeft, StringComparison.Ordinal))
                left = left[..^plan.RemoveLeft.Length];
            if (plan.RemoveRight != null && right.StartsWith(plan.RemoveRight, StringComparison.Ordinal))
                right = right[plan.RemoveRight.Length..];
            return left + right;
        }

        /// <summary>A validated source rule with its precomputed range and specificity.</summary>
        /// <param name="Source">The configured rule.</param>
        /// <param name="Range">The parsed range of lower sub-group values.</param>
        /// <param name="Specificity">The number of values covered (fewer = more specific).</param>
        private sealed record FusionRuleCandidate(FusionType Source, Utils.Range.IntRange<long> Range, long Specificity);

        /// <summary>
        /// Looks up the compiled fusion plan for a junction, if any. The plans of a table are indexed by
        /// group level, then digit, then numeric value of the lower sub-group; a <see langword="null"/>
        /// entry at any depth means "no fusion: use buildString".
        /// </summary>
        /// <param name="table">The table whose plans are searched.</param>
        /// <param name="groupNumber">The group level being composed.</param>
        /// <param name="digit">The digit of the current position.</param>
        /// <param name="remainder">The numeric value of the lower sub-group.</param>
        /// <param name="plan">The plan when one applies.</param>
        /// <returns><see langword="true"/> when a fusion applies to this junction.</returns>
        private static bool TryGetFusionPlan(GroupTable table, int groupNumber, long digit, long remainder, out FusionPlan plan)
        {
            plan = null!;
            var byLevel = table.FusionPlans;
            if (byLevel is null || groupNumber >= byLevel.Length) return false;
            var byDigit = byLevel[groupNumber];
            if (byDigit is null || digit >= byDigit.Length) return false;
            var byRemainder = byDigit[digit];
            if (byRemainder is null || remainder >= byRemainder.Length) return false;
            var found = byRemainder[remainder];
            if (found is null) return false;
            plan = found;
            return true;
        }

        /// <summary>
        /// Validates every <see cref="DigitType.Fusions"/> rule of <paramref name="table"/> and compiles
        /// them into its <see cref="GroupTable.FusionPlans"/>, level by level from the lowest so that edge
        /// validation can render the actual lower constituents (including already compiled lower-level
        /// fusions of the same table).
        /// </summary>
        /// <param name="table">The table whose digits are compiled.</param>
        /// <param name="label">The table name used in diagnostics (e.g. <c>Groups</c>).</param>
        /// <param name="paramName">The parameter name used in diagnostics.</param>
        /// <exception cref="ArgumentException">Thrown when a rule is invalid or ambiguous.</exception>
        private void CompileFusions(GroupTable table, string label, string paramName)
        {
            var groups = table.Digits;
            if (!groups.Values.Any(g => g.Values.Any(d => d.Fusions is { Count: > 0 })))
                return;

            int maxLevel = groups.Keys.Max();
            var plans = new FusionPlan?[]?[]?[maxLevel + 1];
            table.FusionPlans = plans;

            foreach (int level in groups.Keys.OrderBy(k => k))
            {
                foreach (var digit in groups[level].Values.OrderBy(d => d.Digit))
                {
                    if (digit.Fusions is not { Count: > 0 }) continue;
                    string where = $"{label}[{level}].Digits[{digit.Digit}] Fusion";
                    if (level == 1)
                        throw new ArgumentException($"{where}: a level-1 digit has no lower sub-group to fuse with.", paramName);
                    if (level > MaxFusionGroupLevel)
                        throw new ArgumentException($"{where}: Fusion is supported up to group level {MaxFusionGroupLevel}.", paramName);

                    long domainMax = _decimalPowersOfTen[level - 1] - 1;
                    var candidates = BuildFusionCandidates(digit.Fusions, domainMax, where, paramName);
                    var byRemainder = new FusionPlan?[domainMax + 1];
                    for (long value = 1; value <= domainMax; value++)
                    {
                        var plan = ResolveFusionPlan(candidates, value);
                        if (plan is null) continue;

                        long number = digit.Digit * _decimalPowersOfTen[level - 1] + value;
                        // A whole-number exception short-circuits the composition: the rule is inert there.
                        if (Exceptions.ContainsKey(number)) continue;
                        if (level == 3 && _intraGroupConnector != null && digit.Digit > 0 && value < _intraGroupConnectorThreshold)
                            throw new ArgumentException(
                                $"{where}: value {value} is also joined by the intra-group connector '{_intraGroupConnector}'; a junction cannot be both fused and connected.",
                                paramName);

                        string right = ConvertGroup(table, level - 1, value);
                        // An empty lower constituent never reaches the composition step: the rule is inert.
                        if (string.IsNullOrEmpty(right)) continue;
                        ValidateFusionEdges(plan, digit.StringValue ?? string.Empty, right, value, where, paramName);
                        byRemainder[value] = plan;
                    }

                    var byDigit = plans[level] ??= new FusionPlan?[10][];
                    byDigit[digit.Digit] = byRemainder;
                }
            }
        }

        /// <summary>Validates the attributes and ranges of one digit's rules.</summary>
        /// <param name="fusions">The configured rules.</param>
        /// <param name="domainMax">The largest value of the lower sub-group.</param>
        /// <param name="where">Diagnostic location prefix.</param>
        /// <param name="paramName">The parameter name used in diagnostics.</param>
        /// <returns>The validated candidates.</returns>
        private static List<FusionRuleCandidate> BuildFusionCandidates(
            IReadOnlyList<FusionType> fusions, long domainMax, string where, string paramName)
        {
            var domain = ParseRangeExpression($"1..{domainMax}");
            var candidates = new List<FusionRuleCandidate>(fusions.Count);
            var canonicalRanges = new HashSet<string>(StringComparer.Ordinal);
            foreach (var fusion in fusions)
            {
                if (fusion is null)
                    throw new ArgumentException($"{where}: entry must not be null.", paramName);
                if (string.IsNullOrWhiteSpace(fusion.For))
                    throw new ArgumentException($"{where}: the 'for' range is required.", paramName);
                RequireNonEmpty(fusion.RemoveLeft, "removeLeft");
                RequireNonEmpty(fusion.RemoveRight, "removeRight");
                RequireNonEmpty(fusion.Left, "left");
                RequireNonEmpty(fusion.Right, "right");

                var range = ParseRangeExpression(fusion.For);
                // Subset test through the intersection: open bounds make it differ from the range.
                string canonical = range.ToString(null, CultureInfo.InvariantCulture)!;
                if ((range & domain).ToString(null, CultureInfo.InvariantCulture) != canonical)
                    throw new ArgumentException(
                        $"{where} for=\"{fusion.For}\": every value must lie within the lower sub-group domain [1, {domainMax}].", paramName);
                // Bounded by the domain check above, so the enumeration is finite.
                long specificity = range.LongCount();
                if (specificity == 0)
                    throw new ArgumentException($"{where} for=\"{fusion.For}\": the range covers no value.", paramName);
                if (!canonicalRanges.Add(canonical))
                    throw new ArgumentException($"{where} for=\"{fusion.For}\": another rule of this digit has the same range.", paramName);
                candidates.Add(new FusionRuleCandidate(fusion, range, specificity));
            }
            RejectCrossingOverrides(candidates, where, paramName);
            // Widest range first: more specific rules are folded last and therefore win.
            candidates.Sort((a, b) => b.Specificity.CompareTo(a.Specificity));
            return candidates;

            void RequireNonEmpty(string? value, string attribute)
            {
                if (value is { Length: 0 })
                    throw new ArgumentException($"{where}: '{attribute}' must not be empty when present.", paramName);
            }
        }

        /// <summary>
        /// Rejects two overlapping rules that assign different values to the same property unless
        /// one range is a subset of the other: an override is only meaningful from a general range
        /// to a range it contains, never between crossing ranges such as <c>1..5</c> and <c>4..6</c>.
        /// Two distinct overlapping ranges of equal size always cross, so equally specific
        /// conflicts are rejected here too.
        /// </summary>
        /// <param name="candidates">The validated candidates of one digit (distinct, bounded ranges).</param>
        /// <param name="where">Diagnostic location prefix.</param>
        /// <param name="paramName">The parameter name used in diagnostics.</param>
        /// <exception cref="ArgumentException">Thrown when two crossing rules disagree on a property.</exception>
        private static void RejectCrossingOverrides(List<FusionRuleCandidate> candidates, string where, string paramName)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                for (int j = i + 1; j < candidates.Count; j++)
                {
                    var a = candidates[i];
                    var b = candidates[j];
                    var shared = a.Range & b.Range;
                    // Bounded by the domain check, so the enumeration is finite.
                    long sharedCount = shared.LongCount();
                    if (sharedCount == 0 || sharedCount == a.Specificity || sharedCount == b.Specificity) continue;

                    string? property = ConflictingProperty(a.Source, b.Source);
                    if (property is null) continue;
                    throw new ArgumentException(
                        $"{where}: rules for=\"{a.Source.For}\" and for=\"{b.Source.For}\" overlap without one range containing the other, "
                        + $"and assign different values to '{property}'; only a range nested in another may override it.",
                        paramName);
                }
            }

            static string? ConflictingProperty(FusionType a, FusionType b)
                => Differs(a.Left, b.Left) ? "left"
                : Differs(a.Right, b.Right) ? "right"
                : Differs(a.RemoveLeft, b.RemoveLeft) ? "removeLeft"
                : Differs(a.RemoveRight, b.RemoveRight) ? "removeRight"
                : null;

            static bool Differs(string? a, string? b) => a is not null && b is not null && a != b;
        }

        /// <summary>
        /// Folds every candidate matching <paramref name="value"/> from the least to the most
        /// specific. <see cref="RejectCrossingOverrides"/> guarantees that only nested rules
        /// override each other, so the order among equally specific rules is irrelevant.
        /// </summary>
        /// <param name="candidates">Candidates sorted from the widest to the narrowest range.</param>
        /// <param name="value">The lower sub-group value.</param>
        /// <returns>The effective plan, or <see langword="null"/> when no rule matches.</returns>
        private static FusionPlan? ResolveFusionPlan(List<FusionRuleCandidate> candidates, long value)
        {
            FusionPlan? plan = null;
            foreach (var candidate in candidates)
            {
                if (!candidate.Range.Contains(value)) continue;
                var source = candidate.Source;
                plan = plan is null
                    ? new FusionPlan(source.Left, source.Right, source.RemoveLeft, source.RemoveRight)
                    : new FusionPlan(source.Left ?? plan.Left, source.Right ?? plan.Right, source.RemoveLeft ?? plan.RemoveLeft, source.RemoveRight ?? plan.RemoveRight);
            }
            return plan;
        }

        /// <summary>Checks that the removals of a plan can be applied to the actual constituents.</summary>
        /// <param name="plan">The effective plan.</param>
        /// <param name="left">The digit text before the override.</param>
        /// <param name="right">The lower sub-group text before the override.</param>
        /// <param name="value">The lower sub-group value.</param>
        /// <param name="where">Diagnostic location prefix.</param>
        /// <param name="paramName">The parameter name used in diagnostics.</param>
        private static void ValidateFusionEdges(FusionPlan plan, string left, string right, long value, string where, string paramName)
        {
            string effectiveLeft = plan.Left ?? left;
            if (plan.RemoveLeft != null && !effectiveLeft.EndsWith(plan.RemoveLeft, StringComparison.Ordinal))
                throw new ArgumentException(
                    $"{where}: removeLeft=\"{plan.RemoveLeft}\" is not a suffix of the left constituent '{effectiveLeft}' (value {value}).", paramName);
            string effectiveRight = plan.Right ?? right;
            if (plan.RemoveRight != null && !effectiveRight.StartsWith(plan.RemoveRight, StringComparison.Ordinal))
                throw new ArgumentException(
                    $"{where}: removeRight=\"{plan.RemoveRight}\" is not a prefix of the right constituent '{effectiveRight}' (value {value}).", paramName);
        }
    }
}
