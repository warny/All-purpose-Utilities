using System;
using System.Numerics;
using System.Reflection;

namespace Utils.Mathematics.Expressions;

/// <summary>
/// Thrown when a required scalar math operation (e.g. <c>Log</c>, <c>Sin</c>, <c>Sqrt</c>) is not
/// available for the scalar type used to build a symbolic derivative/integral. This is a specialized
/// <see cref="NotSupportedException"/> so callers can categorize "operation unavailable for scalar type
/// T" (see <see cref="SymbolicTransformationStatus.UnsupportedScalarOperation"/>) distinctly from other
/// unsupported-expression failures, while existing <c>catch (NotSupportedException)</c> sites keep working.
/// </summary>
public sealed class UnsupportedScalarOperationException : NotSupportedException
{
    /// <summary>
    /// Gets the name of the operation that was not available (e.g. <c>Log</c>).
    /// </summary>
    public string OperationName { get; }

    /// <summary>
    /// Gets the scalar type for which <see cref="OperationName"/> was unavailable.
    /// </summary>
    public Type ScalarType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnsupportedScalarOperationException"/> class.
    /// </summary>
    /// <param name="operationName">Name of the unavailable operation.</param>
    /// <param name="scalarType">Scalar type for which the operation is unavailable.</param>
    public UnsupportedScalarOperationException(string operationName, Type scalarType)
        : base($"The '{operationName}' operation is not supported for type '{scalarType}'.")
    {
        OperationName = operationName;
        ScalarType = scalarType;
    }
}

/// <summary>
/// Resolves the single-argument static math method (e.g. <c>Log</c>, <c>Sin</c>, <c>Exp</c>) that a
/// scalar type <c>T</c> exposes, for use when building symbolic derivative/integral expression trees.
/// </summary>
/// <remarks>
/// <see cref="IFloatingPoint{TSelf}"/> is intentionally broad: it does not guarantee <c>Log</c>,
/// <c>Sin</c>, <c>Exp</c>, or similar transcendental functions exist for every conforming type (e.g.
/// <see cref="decimal"/> has none of them). A raw <c>typeof(T).GetMethod(...)</c> call silently
/// returns <see langword="null"/> for an unsupported type, which then surfaces as an incidental
/// <see cref="ArgumentNullException"/> deep inside <see cref="System.Linq.Expressions.Expression.Call(MethodInfo?, System.Linq.Expressions.Expression[])"/>
/// instead of a clear diagnostic. Every call site that needs one of these methods should resolve it
/// through <see cref="Resolve{T}"/> instead of calling <c>GetMethod</c> directly, so an unsupported
/// operation fails with a message naming both the operation and the type.
/// </remarks>
internal static class MathMethodResolver
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(Type Type, string Name), MethodInfo?> cache = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(Type Type, string Name), MethodInfo?> binaryCache = new();

    /// <summary>
    /// Resolves the single-argument static method named <paramref name="methodName"/> on
    /// <typeparamref name="T"/> (matching the shape of <c>static T Method(T value)</c>, as declared by
    /// <see cref="double"/>'s own math methods). Results (including failures) are cached per
    /// <c>(T, methodName)</c> pair.
    /// </summary>
    /// <typeparam name="T">Scalar type the expression tree is being built for.</typeparam>
    /// <param name="methodName">Name of the method, e.g. <c>nameof(double.Log)</c>.</param>
    /// <returns>The resolved <see cref="MethodInfo"/>.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when <typeparamref name="T"/> does not declare a matching <paramref name="methodName"/> method.
    /// </exception>
    public static MethodInfo Resolve<T>(string methodName) where T : IFloatingPoint<T>
    {
        MethodInfo? method = cache.GetOrAdd((typeof(T), methodName), key => key.Type.GetMethod(key.Name, [key.Type]));
        return method ?? throw new UnsupportedScalarOperationException(methodName, typeof(T));
    }

    /// <summary>
    /// Resolves the two-argument static method named <paramref name="methodName"/> on
    /// <typeparamref name="T"/> (matching the shape of <c>static T Method(T x, T y)</c>, as declared by
    /// <see cref="double.Pow(double, double)"/>). Unlike <see cref="System.Linq.Expressions.Expression.Power(System.Linq.Expressions.Expression, System.Linq.Expressions.Expression)"/>,
    /// which only works when both operands are literally <see cref="double"/>, this resolves a
    /// type-appropriate method (e.g. <see cref="float.Pow(float, float)"/>) so the resulting call works
    /// for any <typeparamref name="T"/> that declares it. Results (including failures) are cached per
    /// <c>(T, methodName)</c> pair.
    /// </summary>
    /// <typeparam name="T">Scalar type the expression tree is being built for.</typeparam>
    /// <param name="methodName">Name of the method, e.g. <c>nameof(double.Pow)</c>.</param>
    /// <returns>The resolved <see cref="MethodInfo"/>.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when <typeparamref name="T"/> does not declare a matching two-argument
    /// <paramref name="methodName"/> method.
    /// </exception>
    public static MethodInfo ResolveBinary<T>(string methodName) where T : IFloatingPoint<T>
    {
        MethodInfo? method = binaryCache.GetOrAdd((typeof(T), methodName), key => key.Type.GetMethod(key.Name, [key.Type, key.Type]));
        return method ?? throw new UnsupportedScalarOperationException(methodName, typeof(T));
    }
}
