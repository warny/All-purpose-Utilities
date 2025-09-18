using System.Linq.Expressions;
using Utils.Objects;

namespace Utils.Mathematics.Expressions;

/// <summary>
/// Provides helper methods to derive lambda expressions using <see cref="ExpressionDerivation"/>.
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
        return e.Derivate(e.Parameters[0].Name);
    }

    /// <summary>
    /// Computes the derivative of a lambda expression with respect to the specified parameter name.
    /// </summary>
    /// <param name="e">Lambda expression to differentiate.</param>
    /// <param name="paramName">Name of the parameter used as the differentiation variable.</param>
    /// <returns>A lambda expression representing the derivative.</returns>
    public static LambdaExpression Derivate(this LambdaExpression e, string paramName)
    {
        e.Arg().MustNotBeNull();

        ExpressionDerivation derivation = new ExpressionDerivation(paramName);
        var expression = e.Body.Simplify();
        expression = derivation.Derivate((LambdaExpression)expression);
        expression = expression.Simplify();
        return Expression.Lambda(expression, e.Parameters);
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
