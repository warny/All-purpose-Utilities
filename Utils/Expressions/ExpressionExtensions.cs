using System.Linq.Expressions;

namespace Utils.Mathematics.Expressions;

/// <summary>
/// Provides extension methods for simplifying LINQ expression trees,
/// as well as custom exceptions for extension-related errors.
/// </summary>
public static class ExpressionExtensions
{
    /// <summary>
    /// A shared <see cref="ExpressionSimplifier"/> instance used to simplify expression trees.
    /// </summary>
    private static readonly ExpressionSimplifier _simplifier = new ExpressionSimplifier();

    /// <summary>
    /// Simplifies the specified <see cref="Expression"/> using the internal simplifier.
    /// </summary>
    /// <param name="e">The <see cref="Expression"/> to simplify.</param>
    /// <returns>A new <see cref="Expression"/> representing the simplified form of <paramref name="e"/>.</returns>
    public static Expression Simplify(this Expression e)
    {
        return _simplifier.Simplify(e);
    }
}

/// <summary>
/// Represents a custom exception related to expression extension operations.
/// </summary>
public class ExpressionExtensionsException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionExtensionsException"/> class
    /// with the specified error message.
    /// </summary>
    /// <param name="msg">A brief message that describes the error.</param>
    public ExpressionExtensionsException(string msg)
        : base(msg, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionExtensionsException"/> class
    /// with the specified error message and an inner exception.
    /// </summary>
    /// <param name="msg">A brief message that describes the error.</param>
    /// <param name="innerException">The exception that caused this error.</param>
    public ExpressionExtensionsException(string msg, Exception innerException)
        : base(msg, innerException)
    {
    }
}
