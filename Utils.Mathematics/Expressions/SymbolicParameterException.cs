using System;

namespace Utils.Mathematics.Expressions;

/// <summary>
/// Thrown when the symbolic transformation target parameter cannot be resolved from a
/// <see cref="System.Linq.Expressions.LambdaExpression"/>: the named parameter was not found,
/// is present more than once (ambiguous), or the explicit parameter instance is not declared
/// in the given lambda. This is a caller-error: <see cref="InvalidInput"/> classification in
/// <see cref="SymbolicTransformationResult"/>.
/// </summary>
/// <remarks>
/// Using a dedicated subclass (rather than <see cref="InvalidOperationException"/> directly)
/// allows <see cref="MathExpressionExtensions.TryDerivate{T}"/> and
/// <see cref="MathExpressionExtensions.TryIntegrate{T}"/> to distinguish a genuine caller-error
/// (wrong parameter name) from an internal failure that happens to produce an
/// <see cref="InvalidOperationException"/> for a different reason.
/// </remarks>
public sealed class SymbolicParameterException : InvalidOperationException
{
    /// <summary>Initializes a new instance with a descriptive message.</summary>
    public SymbolicParameterException(string message) : base(message) { }

    /// <summary>Initializes a new instance with a message and an inner exception.</summary>
    public SymbolicParameterException(string message, Exception innerException)
        : base(message, innerException) { }
}
