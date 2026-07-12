using System.Numerics;
using System.Reflection;

namespace Utils.Mathematics.Expressions;

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
        return method ?? throw new NotSupportedException(
            $"The '{methodName}' operation is not supported for type '{typeof(T)}'.");
    }
}
