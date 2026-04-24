using System.Linq.Expressions;
using System.Numerics;
using Utils.Objects;

namespace Utils.Mathematics.Expressions;

/// <summary>
/// Provides helper methods to derive lambda expressions using <see cref="ExpressionDerivation{T}"/>.
/// </summary>
public static class MathExpressionExtensions
{
    /// <summary>
    /// Computes the derivative of a single-parameter lambda expression with respect to its declared parameter.
    /// </summary>
    /// <param name="e">Lambda expression to differentiate.</param>
    /// <returns>A lambda expression representing the derivative.</returns>
    public static LambdaExpression Derivate(this LambdaExpression e)
    {
        e.Arg().MustNotBeNull();
        e.Parameters.ArgMustBeOfSize(1);
        return e.Derivate<double>(e.Parameters[0].Name);
    }

    /// <summary>
    /// Computes the derivative of a lambda expression with respect to the specified parameter name.
    /// </summary>
    /// <param name="e">Lambda expression to differentiate.</param>
    /// <param name="paramName">Name of the parameter used as the differentiation variable.</param>
    /// <returns>A lambda expression representing the derivative.</returns>
    public static LambdaExpression Derivate(this LambdaExpression e, string paramName)
    {
        return e.Derivate<double>(paramName);
    }

    /// <summary>
    /// Computes the derivative of a lambda expression with respect to the specified parameter name.
    /// </summary>
    /// <typeparam name="T">Floating-point type used by derivative rules.</typeparam>
    /// <param name="e">Lambda expression to differentiate.</param>
    /// <param name="paramName">Name of the parameter used as the differentiation variable.</param>
    /// <returns>A lambda expression representing the derivative.</returns>
    public static LambdaExpression Derivate<T>(this LambdaExpression e, string paramName) where T : IFloatingPoint<T>
    {
        e.Arg().MustNotBeNull();

        ExpressionDerivation<T> derivation = new ExpressionDerivation<T>(paramName);
        var expression = (LambdaExpression)derivation.Derivate(e);
        return Expression.Lambda(expression.Body.Simplify(), e.Parameters);
    }

    /// <summary>
    /// Computes the integral of a single-parameter lambda expression with respect to its declared parameter.
    /// </summary>
    /// <param name="e">Lambda expression to integrate.</param>
    /// <returns>A lambda expression representing the integral.</returns>
    public static LambdaExpression Integrate(this LambdaExpression e)
    {
        e.Arg().MustNotBeNull();
        e.Parameters.ArgMustBeOfSize(1);
        return e.Integrate<double>(e.Parameters[0].Name);
    }

    /// <summary>
    /// Computes the integral of a lambda expression with respect to the specified parameter name.
    /// </summary>
    /// <typeparam name="T">Floating-point type used by integration rules.</typeparam>
    /// <param name="e">Lambda expression to integrate.</param>
    /// <param name="paramName">Name of the parameter used as the integration variable.</param>
    /// <returns>A lambda expression representing the integral.</returns>
    public static LambdaExpression Integrate<T>(this LambdaExpression e, string paramName) where T : IFloatingPoint<T>
    {
        e.Arg().MustNotBeNull();

        ExpressionIntegration<T> integration = new ExpressionIntegration<T>(paramName);
        var expression = (LambdaExpression)integration.Integrate(e);
        return Expression.Lambda(expression.Body.Simplify(), e.Parameters);
    }

}

/// <summary>
/// Represents validation errors that occur while building derivative expressions.
/// </summary>
public class ExpressionExtensionsException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionExtensionsException"/> class with the provided message.
    /// </summary>
    /// <param name="msg">Explanation of the validation failure.</param>
    public ExpressionExtensionsException(string msg) : base(msg, null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionExtensionsException"/> class with the provided message and inner exception.
    /// </summary>
    /// <param name="msg">Explanation of the validation failure.</param>
    /// <param name="innerException">Inner exception that caused the validation error.</param>
    public ExpressionExtensionsException(string msg, Exception innerException) : base(msg, innerException) { }
}
