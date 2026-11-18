using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace ExpressionSimplifierAudit;

/// <summary>
/// Cost profile of one family at one size (mission "trigonometric rule and division chains", part B): the measurement of
/// <see cref="Measure"/> split by mechanism. Every counter is read in its own instrumented run, never in a timed one:
/// <list type="bullet">
/// <item>the minimal transformation and structural visit budgets (bisection of the public limits);</item>
/// <item>transformation visits, rule candidates, rejections, applications and canonicalizations, split between the main
/// transformation (comparison depth 0) and the simplifications the comparer runs (depth 1 and more);</item>
/// <item>the factor equality probes, the comparer simplifications, their memo hits, the additive canonicalizations and the
/// canonical keys (internal <c>SimplificationMetadataStatistics</c>);</item>
/// <item>the Flatten admission counters (internal <c>FlattenAdmissionStatistics</c>);</item>
/// <item>the time and allocations of untraced runs, and the size and depth of the result.</item>
/// </list>
/// </summary>
internal static class Profile
{
    /// <summary>The internal <c>ExpressionSimplifier.ComparisonMemoDepth</c>: the comparer-simplification depth of the thread.</summary>
    private static readonly Func<int>? ComparisonDepth = typeof(ExpressionSimplifier)
        .GetProperty("ComparisonMemoDepth", BindingFlags.NonPublic | BindingFlags.Static)?.GetMethod?.CreateDelegate<Func<int>>();

    /// <summary>The internal <c>ExpressionSimplifier.MetadataStatistics</c>.</summary>
    private static readonly PropertyInfo? MetadataProperty = typeof(ExpressionSimplifier).GetProperty("MetadataStatistics", BindingFlags.NonPublic | BindingFlags.Static);

    /// <summary>The internal <c>ExpressionSimplifier.AdmissionStatistics</c>.</summary>
    private static readonly PropertyInfo? AdmissionProperty = typeof(ExpressionSimplifier).GetProperty("AdmissionStatistics", BindingFlags.NonPublic | BindingFlags.Static);

    /// <summary>Profiles family <paramref name="family"/> at size <paramref name="n"/>.</summary>
    /// <param name="family">The family.</param>
    /// <param name="n">The size.</param>
    /// <param name="maxSeconds">The time budget of the timed runs.</param>
    /// <returns>The profile record.</returns>
    public static object Run(string family, int n, double maxSeconds)
    {
        Expression tree = Families.Build(family, n);
        ExpressionResourceLimits limits = ExpressionResourceLimits.Unlimited;

        var first = Stopwatch.StartNew();
        (Expression? result, Exception? error) = Simplification.Run(tree, limits);
        first.Stop();
        if (error is not null) return new { family, n, outcome = AuditText.Describe(error), firstMs = first.Elapsed.TotalMilliseconds };

        // Untraced timing.
        var warm = Stopwatch.StartNew();
        for (int runs = 0; first.Elapsed.TotalSeconds < 1 && (runs < 3 || warm.Elapsed.TotalSeconds < 1); runs++) Simplification.Run(tree, limits);
        var samples = new List<double>();
        long bytes = 0;
        var budget = Stopwatch.StartNew();
        do
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            var watch = Stopwatch.StartNew();
            Simplification.Run(tree, limits);
            watch.Stop();
            bytes += GC.GetAllocatedBytesForCurrentThread() - before;
            samples.Add(watch.Elapsed.TotalMilliseconds);
        }
        while (samples.Count < 15 && budget.Elapsed.TotalSeconds + samples[^1] / 1000 < maxSeconds);
        samples.Sort();

        // Minimal budgets.
        long transformVisits = MinimalBudget(tree, value => limits with { MaxTransformVisits = value });
        long structuralVisits = MinimalBudget(tree, value => limits with { MaxStructuralVisits = value });

        // Mechanisms.
        var tracer = new MechanismTracer();
        Simplification.Run(tree, limits, tracer, ExpressionTraceDetail.Detailed);
        Dictionary<string, long> metadata = Collect(MetadataProperty, () => Simplification.Run(tree, limits));
        Dictionary<string, long> admission = Collect(AdmissionProperty, () => Simplification.Run(tree, limits));
        (int comparerEvents, int comparerMaxDepth, SortedDictionary<int, int> histogram) = EngineInternals.Capture(() => Simplification.Run(tree, limits));

        return new
        {
            family, n, outcome = "ok",
            medianMs = samples[samples.Count / 2], minMs = samples[0], runs = samples.Count,
            bytesPerOp = bytes / samples.Count,
            transformVisits, structuralVisits,
            main = tracer.Main, comparer = tracer.Nested,
            applied = tracer.Applied.OrderByDescending(p => p.Value).ToDictionary(p => p.Key, p => p.Value),
            metadata, admission,
            comparerEvents, comparerMaxDepth, comparerDepthHistogram = histogram,
            inputNodes = AuditText.Count(tree), outputNodes = AuditText.Count(result), outputDepth = Depth(result),
            result = AuditText.Format(result),
        };
    }

    /// <summary>The smallest value of one visit budget under which the simplification of <paramref name="tree"/> succeeds.</summary>
    /// <param name="tree">The tree.</param>
    /// <param name="limitsOf">The limits with the budget set to a value.</param>
    /// <returns>The minimal budget, or -1 above 2^40.</returns>
    private static long MinimalBudget(Expression tree, Func<long, ExpressionResourceLimits> limitsOf)
    {
        bool Passes(long value) => Simplification.Run(tree, limitsOf(value)).Error is null;
        long high = 64;
        while (!Passes(high))
        {
            if (high > 1L << 40) return -1;
            high *= 2;
        }

        long low = high == 64 ? 0 : high / 2;
        while (low + 1 < high)
        {
            long middle = low + (high - low) / 2;
            if (Passes(middle)) high = middle; else low = middle;
        }

        return high;
    }

    /// <summary>Runs <paramref name="action"/> with a fresh instance of an internal statistics type installed and returns its counters.</summary>
    /// <param name="property">The internal static property that installs the statistics.</param>
    /// <param name="action">The action.</param>
    /// <returns>The non-zero counters by name (empty when the property is unavailable).</returns>
    private static Dictionary<string, long> Collect(PropertyInfo? property, Action action)
    {
        var counters = new Dictionary<string, long>();
        if (property is null)
        {
            action();
            return counters;
        }

        object statistics = Activator.CreateInstance(property.PropertyType, nonPublic: true)!;
        property.SetValue(null, statistics);
        try
        {
            action();
        }
        finally
        {
            property.SetValue(null, null);
        }

        foreach (FieldInfo field in property.PropertyType.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (field.FieldType == typeof(long) && (long)field.GetValue(statistics)! is long value and not 0) counters[field.Name] = value;
        }

        return counters;
    }

    /// <summary>The depth of a tree.</summary>
    /// <param name="e">The tree.</param>
    /// <returns>Its depth (1 for a leaf).</returns>
    private static int Depth(Expression? e)
    {
        if (e is null) return 0;
        int max = 0;
        var stack = new Stack<(Expression Node, int Depth)>();
        stack.Push((e, 1));
        while (stack.Count > 0)
        {
            (Expression node, int depth) = stack.Pop();
            max = Math.Max(max, depth);
            switch (node)
            {
                case BinaryExpression b: stack.Push((b.Left, depth + 1)); stack.Push((b.Right, depth + 1)); break;
                case UnaryExpression u: stack.Push((u.Operand, depth + 1)); break;
                case MethodCallExpression c: foreach (Expression a in c.Arguments) stack.Push((a, depth + 1)); break;
            }
        }

        return max;
    }

    /// <summary>The counters of one side (main transformation or comparer simplifications).</summary>
    internal sealed class Side
    {
        /// <summary>Gets the transformation visits.</summary>
        public long Visits { get; set; }

        /// <summary>Gets the signature rules considered.</summary>
        public long RuleCandidates { get; set; }

        /// <summary>Gets the rules that ran and declined.</summary>
        public long RulesDeclined { get; set; }

        /// <summary>Gets the rule applications.</summary>
        public long RulesApplied { get; set; }

        /// <summary>Gets the finalizations that changed the node (canonicalizations).</summary>
        public long Canonicalizations { get; set; }

        /// <summary>Gets the chains extracted for the Flatten rules.</summary>
        public long ChainsFlattened { get; set; }
    }

    /// <summary>A detailed tracer that splits the counters by comparison depth.</summary>
    private sealed class MechanismTracer : IExpressionTransformTracer
    {
        /// <summary>Gets the counters of the main transformation.</summary>
        public Side Main { get; } = new();

        /// <summary>Gets the counters of the comparer simplifications.</summary>
        public Side Nested { get; } = new();

        /// <summary>Gets the applications per rule, prefixed by <c>main:</c> or <c>cmp:</c>.</summary>
        public Dictionary<string, long> Applied { get; } = new();

        /// <inheritdoc/>
        public void OnEvent(in ExpressionTransformEvent e)
        {
            bool nested = ComparisonDepth is not null && ComparisonDepth() > 0;
            Side side = nested ? Nested : Main;
            switch (e.Kind)
            {
                case ExpressionTransformEventKind.TransformEnter: side.Visits++; break;
                case ExpressionTransformEventKind.RuleCandidate: side.RuleCandidates++; break;
                case ExpressionTransformEventKind.RuleRejected when e.Rejection == ExpressionRuleRejection.Declined: side.RulesDeclined++; break;
                case ExpressionTransformEventKind.ChainFlattened: side.ChainsFlattened++; break;
                case ExpressionTransformEventKind.Finalize when e.Outcome == ExpressionTransformOutcome.Finalized: side.Canonicalizations++; break;
                case ExpressionTransformEventKind.RuleApplied when e.Rule is not null:
                    side.RulesApplied++;
                    string key = (nested ? "cmp:" : "main:") + e.Rule.DisplayName;
                    Applied[key] = Applied.TryGetValue(key, out long count) ? count + 1 : 1;
                    break;
            }
        }
    }
}
