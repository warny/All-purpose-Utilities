using System;

namespace Utils.Expressions;

/// <summary>The resource dimension whose limit an expression-tree operation exceeded.</summary>
public enum ExpressionResourceLimitKind
{
    /// <summary>The input is deeper than <see cref="ExpressionResourceLimits.MaxDepth"/>.</summary>
    Depth,

    /// <summary>The input has more logical nodes than <see cref="ExpressionResourceLimits.MaxNodes"/>.</summary>
    Nodes,

    /// <summary>The operation performed more transformation visits than <see cref="ExpressionResourceLimits.MaxTransformVisits"/>.</summary>
    Work,

    /// <summary>The operation performed more structural visits than <see cref="ExpressionResourceLimits.MaxStructuralVisits"/>.</summary>
    StructuralWork,

    /// <summary>The remaining execution stack was insufficient to continue the recursion safely.</summary>
    Stack,
}

/// <summary>
/// Thrown when an expression-tree operation exceeds one of its <see cref="ExpressionResourceLimits"/>, or when the
/// remaining execution stack is insufficient to continue it safely.
/// </summary>
/// <remarks>
/// This is a refusal, never a result: an equality or hash operation that throws it has not decided anything. A
/// <see cref="ExpressionResourceLimitKind.Stack"/> failure keeps the original
/// <see cref="InsufficientExecutionStackException"/> as <see cref="Exception.InnerException"/>. The engine leaves no
/// ambient state behind when it throws this exception, so the same thread can run further operations.
/// </remarks>
public sealed class ExpressionResourceLimitException : InvalidOperationException
{
    /// <summary>Initializes a new instance.</summary>
    /// <param name="kind">The exceeded dimension.</param>
    /// <param name="limit">The limit in force (0 for <see cref="ExpressionResourceLimitKind.Stack"/>).</param>
    /// <param name="observed">The observed value, or a lower bound of it when the measurement stopped early (0 for <see cref="ExpressionResourceLimitKind.Stack"/>).</param>
    public ExpressionResourceLimitException(ExpressionResourceLimitKind kind, long limit, long observed)
        : this(kind, limit, observed, null)
    {
    }

    /// <summary>Initializes a new instance with an inner exception.</summary>
    /// <param name="kind">The exceeded dimension.</param>
    /// <param name="limit">The limit in force (0 for <see cref="ExpressionResourceLimitKind.Stack"/>).</param>
    /// <param name="observed">The observed value, or a lower bound of it when the measurement stopped early (0 for <see cref="ExpressionResourceLimitKind.Stack"/>).</param>
    /// <param name="innerException">The exception that caused this one, such as an <see cref="InsufficientExecutionStackException"/>.</param>
    public ExpressionResourceLimitException(ExpressionResourceLimitKind kind, long limit, long observed, Exception? innerException)
        : base(BuildMessage(kind, limit, observed), innerException)
    {
        Kind = kind;
        Limit = limit;
        Observed = observed;
    }

    /// <summary>Gets the exceeded dimension.</summary>
    public ExpressionResourceLimitKind Kind { get; }

    /// <summary>Gets the limit in force (0 for <see cref="ExpressionResourceLimitKind.Stack"/>).</summary>
    public long Limit { get; }

    /// <summary>
    /// Gets the observed value, or a lower bound of it when the measurement stopped as soon as the limit was crossed
    /// (0 for <see cref="ExpressionResourceLimitKind.Stack"/>).
    /// </summary>
    public long Observed { get; }

    /// <summary>Builds the exception message.</summary>
    /// <param name="kind">The exceeded dimension.</param>
    /// <param name="limit">The limit in force.</param>
    /// <param name="observed">The observed value.</param>
    /// <returns>The message.</returns>
    private static string BuildMessage(ExpressionResourceLimitKind kind, long limit, long observed) => kind switch
    {
        ExpressionResourceLimitKind.Depth => $"The expression tree is deeper than the configured limit ({observed} > {limit} levels).",
        ExpressionResourceLimitKind.Nodes => $"The expression tree has more logical nodes than the configured limit ({observed} > {limit}).",
        ExpressionResourceLimitKind.Work => $"The expression operation exceeded its transformation budget ({observed} > {limit} visits).",
        ExpressionResourceLimitKind.StructuralWork => $"The expression operation exceeded its structural budget ({observed} > {limit} visits).",
        ExpressionResourceLimitKind.Stack => "The expression operation stopped because the remaining execution stack was insufficient.",
        _ => "The expression operation exceeded a resource limit.",
    };
}
