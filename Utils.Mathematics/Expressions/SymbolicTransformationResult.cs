using System;
using System.Linq.Expressions;

namespace Utils.Mathematics.Expressions;

/// <summary>
/// Categorizes the outcome of a non-throwing symbolic transformation
/// (see <see cref="MathExpressionExtensions.TryDerivate"/> and
/// <see cref="MathExpressionExtensions.TryIntegrate"/>).
/// </summary>
public enum SymbolicTransformationStatus
{
    /// <summary>The transformation produced a valid expression (see <see cref="SymbolicTransformationResult.IsExact"/>).</summary>
    Success,

    /// <summary>
    /// No symbolic rule applied to the expression (e.g. an unknown method with numeric fallback disabled,
    /// an unsupported conversion, or an unknown expression node type).
    /// </summary>
    UnsupportedExpression,

    /// <summary>
    /// A required scalar math operation (e.g. <c>Log</c>, <c>Sin</c>, <c>Sqrt</c>) is not available for the
    /// scalar type <c>T</c> (for example <see cref="decimal"/> has no trigonometric functions).
    /// </summary>
    UnsupportedScalarOperation,

    /// <summary>
    /// The API was used incorrectly: the differentiation/integration variable is null, ambiguous
    /// (two distinct parameters share one name), or foreign (not declared in the lambda).
    /// </summary>
    InvalidInput,

    /// <summary>An unexpected error occurred while constructing the result expression tree.</summary>
    ConstructionFailure
}

/// <summary>
/// A structured, non-throwing result of a symbolic transformation (differentiation or integration).
/// Lets callers distinguish "not differentiable/integrable", "approximately transformed", "invalid
/// input", and "internal construction failure" without relying on exception types or null conventions
/// (see TODO-2026-07-11-pass3.md item #42).
/// </summary>
/// <param name="Status">The outcome category.</param>
/// <param name="Expression">
/// The produced lambda expression when <see cref="Status"/> is
/// <see cref="SymbolicTransformationStatus.Success"/>; otherwise <see langword="null"/>.
/// </param>
/// <param name="IsExact">
/// <see langword="true"/> when the successful result is a fully symbolic (exact) transformation;
/// <see langword="false"/> when a numeric finite-difference fallback was used for at least one
/// sub-expression. Always <see langword="false"/> for non-success results.
/// </param>
/// <param name="UnsupportedNode">
/// The offending expression node when it can be identified (e.g. the unsupported conversion or the
/// unknown method call); otherwise <see langword="null"/>.
/// </param>
/// <param name="Diagnostic">A human-readable explanation, or <see langword="null"/> on success.</param>
/// <param name="InnerException">The originating exception, when the result was produced from a caught exception.</param>
public sealed record SymbolicTransformationResult(
    SymbolicTransformationStatus Status,
    LambdaExpression? Expression,
    bool IsExact,
    Expression? UnsupportedNode,
    string? Diagnostic,
    Exception? InnerException)
{
    /// <summary>
    /// Gets whether the transformation produced a valid expression.
    /// </summary>
    public bool Success => Status == SymbolicTransformationStatus.Success;

    /// <summary>
    /// Creates a successful result carrying the produced expression and its exactness.
    /// </summary>
    /// <param name="expression">The produced lambda expression.</param>
    /// <param name="isExact">Whether the result is exact (see <see cref="IsExact"/>).</param>
    /// <returns>A success result.</returns>
    internal static SymbolicTransformationResult SuccessResult(LambdaExpression expression, bool isExact) =>
        new(SymbolicTransformationStatus.Success, expression, isExact, null, null, null);

    /// <summary>
    /// Creates a failure result of the given category.
    /// </summary>
    /// <param name="status">The failure category (must not be <see cref="SymbolicTransformationStatus.Success"/>).</param>
    /// <param name="diagnostic">A human-readable explanation.</param>
    /// <param name="unsupportedNode">The offending node, when known.</param>
    /// <param name="innerException">The originating exception, when any.</param>
    /// <returns>A failure result.</returns>
    internal static SymbolicTransformationResult Failure(
        SymbolicTransformationStatus status,
        string diagnostic,
        Expression? unsupportedNode = null,
        Exception? innerException = null) =>
        new(status, null, false, unsupportedNode, diagnostic, innerException);
}

/// <summary>
/// Helpers for tagging a transformer exception with the offending expression node, so a non-throwing
/// caller (<see cref="MathExpressionExtensions.TryDerivate"/>/<see cref="MathExpressionExtensions.TryIntegrate"/>)
/// can surface it in <see cref="SymbolicTransformationResult.UnsupportedNode"/>.
/// </summary>
internal static class SymbolicTransformationException
{
    /// <summary>
    /// Attaches <paramref name="node"/> to <paramref name="exception"/>'s <see cref="Exception.Data"/> and
    /// returns the exception, for use as <c>throw exception.Tag(node)</c> at a throw site that knows the
    /// offending node.
    /// </summary>
    /// <typeparam name="TException">The exception type.</typeparam>
    /// <param name="exception">The exception being thrown.</param>
    /// <param name="node">The offending expression node.</param>
    /// <returns>The same exception, with the node attached.</returns>
    public static TException Tag<TException>(this TException exception, Expression node) where TException : Exception
    {
        exception.Data[MathExpressionExtensions.UnsupportedNodeKey] = node;
        return exception;
    }
}
