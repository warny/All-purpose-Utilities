using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using Utils.Objects;
using Utils.Reflection;

namespace Utils.Mathematics.Expressions;

/// <summary>
/// Provides helper methods to derive lambda expressions using <see cref="ExpressionDerivation{T}"/>.
/// </summary>
public static class MathExpressionExtensions
{
    private static readonly MethodInfo DerivateGenericMethod = typeof(MathExpressionExtensions)
        .GetMethods()
        .Single(m => m.Name == nameof(Derivate) && m.IsGenericMethodDefinition);

    private static readonly MethodInfo GradientGenericMethod = typeof(MathExpressionExtensions)
        .GetMethods()
        .Single(m => m.Name == nameof(Gradient) && m.IsGenericMethodDefinition);

    private static readonly MethodInfo IntegrateGenericMethod = typeof(MathExpressionExtensions)
        .GetMethods()
        .Single(m => m.Name == nameof(Integrate) && m.IsGenericMethodDefinition);

    /// <summary>
    /// Infers the scalar type shared by the parameters named in <paramref name="paramNames"/>, for the
    /// non-generic convenience overloads below, which dispatch to the matching <c>&lt;T&gt;</c> overload
    /// through reflection instead of hardcoding <see cref="double"/> (see TODO-pass4 item #46). A lambda
    /// over <see cref="float"/> or <see cref="decimal"/> parameters is thus handled with that same type's
    /// constants and method resolution, rather than silently mixing in <see cref="double"/> ones.
    /// </summary>
    /// <param name="e">Lambda expression whose parameters are inspected.</param>
    /// <param name="paramNames">Names of the parameters the caller intends to differentiate/integrate against.</param>
    /// <returns>
    /// The single <see cref="IFloatingPoint{TSelf}"/>-conforming CLR type shared by the matching
    /// parameters, or <see cref="double"/> when no parameter matches (letting the downstream call surface
    /// its own "parameter not found" diagnostic instead of failing here for an unrelated reason).
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the matching parameters do not share a single type, or when that type does not
    /// implement <see cref="IFloatingPoint{TSelf}"/>.
    /// </exception>
    private static Type ResolveScalarType(LambdaExpression e, IReadOnlyList<string> paramNames)
    {
        Type[] types = paramNames
            .SelectMany(name => e.Parameters.Where(p => p.Name == name))
            .Select(p => p.Type)
            .Distinct()
            .ToArray();

        if (types.Length > 1)
        {
            throw new NotSupportedException(
                "Cannot infer a single scalar type for the non-generic derivative/integral overloads: " +
                $"the targeted parameters have different types ({string.Join(", ", types.Select(t => t.Name))}). " +
                "Use the explicit generic overload (e.g. Derivate<T>) to select the scalar type.");
        }

        Type scalarType = types.Length == 1 ? types[0] : typeof(double);
        if (!scalarType.IsDefinedBy(typeof(IFloatingPoint<>)))
        {
            throw new NotSupportedException(
                $"The parameter type '{scalarType}' does not implement IFloatingPoint<T>, so it cannot be " +
                "used with the non-generic derivative/integral convenience overloads. Use the explicit " +
                "generic overload (e.g. Derivate<T>) with a supported scalar type.");
        }
        return scalarType;
    }

    /// <summary>
    /// Computes the derivative of a single-parameter lambda expression with respect to its declared parameter.
    /// The scalar type is inferred from that parameter's CLR type instead of assuming <see cref="double"/>.
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
    /// The scalar type is inferred from that parameter's CLR type instead of assuming <see cref="double"/>.
    /// </summary>
    /// <param name="e">Lambda expression to differentiate.</param>
    /// <param name="paramName">Name of the parameter used as the differentiation variable.</param>
    /// <returns>A lambda expression representing the derivative.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when <paramref name="paramName"/>'s CLR type does not implement <see cref="IFloatingPoint{TSelf}"/>.
    /// </exception>
    public static LambdaExpression Derivate(this LambdaExpression e, string paramName)
    {
        e.Arg().MustNotBeNull();
        Type scalarType = ResolveScalarType(e, [paramName]);
        return (LambdaExpression)DerivateGenericMethod.MakeGenericMethod(scalarType).Invoke(null, [e, paramName])!;
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
    /// Computes the gradient of a multi-parameter lambda expression as an array of partial-derivative expressions.
    /// Each entry i is the partial derivative with respect to the i-th parameter. All parameters must share
    /// the same CLR type, which is inferred instead of assuming <see cref="double"/>.
    /// </summary>
    /// <param name="e">Lambda expression to differentiate.</param>
    /// <returns>Array of lambda expressions representing each partial derivative; shares the same parameter list as <paramref name="e"/>.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when <paramref name="e"/>'s parameters do not share a single CLR type, or that type does not
    /// implement <see cref="IFloatingPoint{TSelf}"/>.
    /// </exception>
    public static LambdaExpression[] Gradient(this LambdaExpression e)
    {
        e.Arg().MustNotBeNull();
        string[] paramNames = e.Parameters.Select(p => p.Name!).ToArray();
        Type scalarType = ResolveScalarType(e, paramNames);
        return (LambdaExpression[])GradientGenericMethod.MakeGenericMethod(scalarType).Invoke(null, [e, paramNames])!;
    }

    /// <summary>
    /// Computes the partial derivatives of a lambda expression with respect to the specified parameter names.
    /// </summary>
    /// <typeparam name="T">Floating-point type used by derivative rules.</typeparam>
    /// <param name="e">Lambda expression to differentiate.</param>
    /// <param name="paramNames">Names of the parameters to differentiate with respect to.</param>
    /// <returns>Array of lambda expressions representing each partial derivative; shares the same parameter list as <paramref name="e"/>.</returns>
    public static LambdaExpression[] Gradient<T>(this LambdaExpression e, params IEnumerable<string> paramNames)
        where T : IFloatingPoint<T>
    {
        e.Arg().MustNotBeNull();
        return paramNames
            .Select(name =>
            {
                ExpressionDerivation<T> derivation = new ExpressionDerivation<T>(name);
                var derived = (LambdaExpression)derivation.Derivate(e);
                return Expression.Lambda(derived.Body.Simplify(), e.Parameters);
            })
            .ToArray();
    }

    /// <summary>
    /// Computes the integral of a single-parameter lambda expression with respect to its declared parameter.
    /// The scalar type is inferred from that parameter's CLR type instead of assuming <see cref="double"/>.
    /// </summary>
    /// <param name="e">Lambda expression to integrate.</param>
    /// <returns>A lambda expression representing the integral.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the declared parameter's CLR type does not implement <see cref="IFloatingPoint{TSelf}"/>.
    /// </exception>
    public static LambdaExpression Integrate(this LambdaExpression e)
    {
        e.Arg().MustNotBeNull();
        e.Parameters.ArgMustBeOfSize(1);
        string paramName = e.Parameters[0].Name;
        Type scalarType = ResolveScalarType(e, [paramName]);
        return (LambdaExpression)IntegrateGenericMethod.MakeGenericMethod(scalarType).Invoke(null, [e, paramName])!;
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
