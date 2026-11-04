using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using Utils.Expressions;

namespace ExpressionSimplifierAudit;

/// <summary>
/// Complexity measurement of one family at one size (brief §6 and §13), meant to run alone in a fresh process: untraced Release
/// timing (warm-up, then the median of repeated runs) and allocations, then separate instrumented runs for the transformation
/// visits, rule applications and engine re-entries (counting tracer), the comparer simplifications and their depth (comparer
/// diagnostics), the user calls and the size of the result.
/// </summary>
internal static class Measure
{
    /// <summary>Measures family <paramref name="family"/> at size <paramref name="n"/>.</summary>
    /// <param name="family">The family.</param>
    /// <param name="n">The size.</param>
    /// <param name="unlimited">Whether to lift the resource limits (the default limits are reported separately).</param>
    /// <param name="maxSeconds">The time budget of the timed runs.</param>
    /// <returns>The measurement record.</returns>
    public static object Run(string family, int n, bool unlimited, double maxSeconds)
    {
        Expression tree = Families.Build(family, n);
        ExpressionResourceLimits? limits = unlimited ? ExpressionResourceLimits.Unlimited : null;

        // Untraced timing.
        var first = Stopwatch.StartNew();
        (Expression? result, Exception? error) = Simplification.Run(tree, limits);
        first.Stop();
        if (error is not null)
        {
            return new { family, n, outcome = AuditText.Describe(error), firstMs = first.Elapsed.TotalMilliseconds };
        }

        var warm = Stopwatch.StartNew();
        int warmRuns = 0;
        while (first.Elapsed.TotalSeconds < 1 && (warmRuns < 3 || warm.Elapsed.TotalSeconds < 1))
        {
            Simplification.Run(tree, limits);
            warmRuns++;
        }

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

        // Instrumented runs (never timed).
        var tracer = new CountingTracer();
        Simplification.Run(tree, limits, tracer);
        long userBefore = EngineInternals.UserCodeCount, opaqueBefore = CountingOpaque.Calls;
        (int comparerSimplifications, int comparerMaxDepth, SortedDictionary<int, int> histogram) = EngineInternals.Capture(() => Simplification.Run(tree, limits));
        long userCalls = EngineInternals.UserCodeCount - userBefore, opaqueCalls = CountingOpaque.Calls - opaqueBefore;
        (_, Exception? defaultError) = unlimited ? Simplification.Run(tree) : (null, null);

        return new
        {
            family, n, outcome = "ok",
            medianMs = samples[samples.Count / 2], minMs = samples[0], runs = samples.Count,
            bytesPerOp = bytes / samples.Count,
            visits = tracer.Visits, ruleApplications = tracer.RuleApplications, maxDepth = tracer.MaxDepth,
            comparerSimplifications, comparerMaxDepth, comparerDepthHistogram = histogram,
            userCalls, opaqueCalls,
            inputNodes = AuditText.Count(tree), outputNodes = AuditText.Count(result),
            defaultLimits = defaultError is null ? "accepted" : AuditText.Describe(defaultError),
            topRules = tracer.Applied.OrderByDescending(p => p.Value).Take(6).ToDictionary(p => p.Key, p => p.Value),
            reentries = tracer.Reentries.OrderByDescending(p => p.Value).Take(6).ToDictionary(p => p.Key, p => p.Value),
            result = AuditText.Format(result),
        };
    }
}
