using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace ExpressionResourceProbe;

/// <summary>
/// Reflection access to internal diagnostic entry points of <c>Utils</c>. The harness is a diagnostic tool outside the
/// shipped surface, so it reads these members by reflection instead of adding an <c>InternalsVisibleTo</c> to the
/// production project. Every accessor degrades to "not available" when a member does not exist in the referenced build.
/// </summary>
internal static class InternalDiagnostics
{
    /// <summary>The flags used to find internal static members.</summary>
    private const BindingFlags InternalStatic = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;

    /// <summary><c>ExpressionComparer.StructuralHashRaw(Expression)</c>: structural hash without simplification.</summary>
    public static readonly Func<Expression, int> StructuralHashRaw =
        typeof(ExpressionComparer).GetMethod("StructuralHashRaw", InternalStatic)!.CreateDelegate<Func<Expression, int>>();

    /// <summary><c>ExpressionComparer.StructuralEqualsRaw(Expression, Expression)</c>: structural equality without simplification.</summary>
    public static readonly Func<Expression, Expression, bool> StructuralEqualsRaw =
        typeof(ExpressionComparer).GetMethod("StructuralEqualsRaw", InternalStatic)!.CreateDelegate<Func<Expression, Expression, bool>>();

    /// <summary><c>ExpressionCanonicalOrder.Compare(x, y, enclosingScopes)</c>: S4 structural canonical-order comparison.</summary>
    private static readonly MethodInfo CanonicalCompareMethod =
        typeof(ExpressionComparer).Assembly.GetType("Utils.Mathematics.Expressions.ExpressionCanonicalOrder", true)!
            .GetMethod("Compare", InternalStatic)!;

    /// <summary><c>ExpressionComparer.BeginDiagnosticCapture()</c>.</summary>
    private static readonly MethodInfo? BeginCaptureMethod = typeof(ExpressionComparer).GetMethod("BeginDiagnosticCapture", InternalStatic);

    /// <summary><c>ExpressionComparer.EndDiagnosticCapture()</c>.</summary>
    private static readonly MethodInfo? EndCaptureMethod = typeof(ExpressionComparer).GetMethod("EndDiagnosticCapture", InternalStatic);

    /// <summary><c>ExpressionComparer.UserCodeInvocationCount</c>.</summary>
    private static readonly PropertyInfo? UserCodeCountProperty = typeof(ExpressionComparer).GetProperty("UserCodeInvocationCount", InternalStatic);

    /// <summary>
    /// Optional instrumentation counter, present only in an instrumented build of <c>Utils</c> (never in production): a
    /// static <see cref="long"/> field <c>EtcTransformCoreVisits</c> on <see cref="ExpressionTransformer"/>, incremented by
    /// every <c>TransformCore</c> call. See README.md for the instrumented-worktree recipe.
    /// </summary>
    private static readonly FieldInfo? TransformCoreVisitsField = typeof(ExpressionTransformer).GetField("EtcTransformCoreVisits", InternalStatic);

    /// <summary>The per-thread ambient state fields that must be reset after any public call, success or failure.</summary>
    private static readonly (Type Owner, string Field)[] AmbientStateFields =
    [
        (typeof(ExpressionSimplifier), "_lexicalScopeStack"),
        (typeof(ExpressionSimplifier), "_comparisonMemoDepth"),
        (typeof(ExpressionSimplifier), "_comparisonMemoActive"),
        (typeof(ExpressionSimplifier), "_comparisonMemo"),
        (typeof(ExpressionSimplifier), "_observedComparisonMemo"),
        (typeof(ExpressionSimplifier), "_expressionMetricsScopeDepth"),
        (typeof(ExpressionSimplifier), "_expressionMetricsCache"),
        (typeof(ExpressionComparer), "_comparisonSimplificationDepth"),
        (typeof(ExpressionComparer), "_diagnosticEvents"),
    ];

    /// <summary>Formats the current thread's ambient simplifier/comparer state (<c>name=value</c> pairs; collections by count).</summary>
    /// <returns>The formatted state; a field missing from the referenced build is reported as <c>missing</c>.</returns>
    public static string DescribeAmbientState()
    {
        var parts = new List<string>();
        foreach ((Type owner, string name) in AmbientStateFields)
        {
            FieldInfo? field = owner.GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
            object? value = field?.GetValue(null);
            string text = field is null ? "missing" : value switch
            {
                null => "null",
                ICollection collection => "count" + collection.Count,
                _ => value.ToString() ?? "?",
            };
            parts.Add(name.TrimStart('_') + "=" + text);
        }

        return string.Join(',', parts);
    }

    /// <summary>Gets whether the referenced build exposes the optional <c>TransformCore</c> visit counter.</summary>
    public static bool HasTransformCoreCounter => TransformCoreVisitsField is not null;

    /// <summary>Compares two expressions with the canonical-order key under an empty enclosing scope.</summary>
    /// <param name="x">The first expression.</param>
    /// <param name="y">The second expression.</param>
    /// <returns>The canonical comparison result.</returns>
    public static int CanonicalCompare(Expression x, Expression y)
    {
        try
        {
            return (int)CanonicalCompareMethod.Invoke(null, [x, y, Array.Empty<ParameterExpression[]>()])!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    /// <summary>Gets the optional <c>TransformCore</c> visit counter, or -1 when the build is not instrumented.</summary>
    /// <returns>The counter value.</returns>
    public static long TransformCoreVisits() => TransformCoreVisitsField is null ? -1 : (long)TransformCoreVisitsField.GetValue(null)!;

    /// <summary>Gets the per-thread count of comparer calls into non-safe constant user code, or -1 when unavailable.</summary>
    /// <returns>The counter value.</returns>
    public static long UserCodeInvocations() => UserCodeCountProperty is null ? -1 : (long)UserCodeCountProperty.GetValue(null)!;

    /// <summary>Starts capturing comparer re-simplification events on the current thread.</summary>
    /// <returns><see langword="true"/> when capture is available.</returns>
    public static bool BeginComparerCapture()
    {
        if (BeginCaptureMethod is null) return false;
        BeginCaptureMethod.Invoke(null, null);
        return true;
    }

    /// <summary>Ends the capture and returns the number of comparer re-simplifications and their maximum nesting depth.</summary>
    /// <returns>The event count and maximum comparison depth, or (-1, -1) when unavailable.</returns>
    public static (int Events, int MaxDepth) EndComparerCapture()
    {
        if (EndCaptureMethod?.Invoke(null, null) is not IEnumerable events) return (-1, -1);
        int count = 0;
        int maxDepth = 0;
        foreach (object item in events)
        {
            count++;
            object? depth = item.GetType().GetProperty("ComparisonDepth")?.GetValue(item);
            if (depth is int value && value > maxDepth) maxDepth = value;
        }

        return (count, maxDepth);
    }
}

/// <summary>A generic recursive transformer used to exercise the public <see cref="ExpressionTransformer.Transform"/> entry point.</summary>
internal sealed class IdentityRecursiveTransformer : ExpressionTransformer
{
    /// <inheritdoc/>
    public override Expression Transform(Expression expression) => TransformCore(expression);

    /// <inheritdoc/>
    protected override Expression PrepareExpression(Expression e) => TransformCore(e);

    /// <inheritdoc/>
    protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => CopyExpression(e, parameters);

    /// <summary>Replaces <paramref name="parameter"/> by <paramref name="replacement"/> through the protected substitution entry point.</summary>
    /// <param name="expression">The tree.</param>
    /// <param name="parameter">The parameter to replace.</param>
    /// <param name="replacement">The replacement.</param>
    /// <returns>The substituted tree.</returns>
    public Expression Substitute(Expression expression, ParameterExpression parameter, Expression replacement)
        => ReplaceArguments(expression, [parameter], [replacement]);
}
