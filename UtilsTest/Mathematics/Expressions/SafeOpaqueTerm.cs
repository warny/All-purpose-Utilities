using System;
using System.Linq.Expressions;
using System.Reflection;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Builds the algebraically safe carrier of an opaque constant used by the S5 user-code and memo scenarios since ETC R1b.
/// </summary>
/// <remarks>
/// <para>
/// Before ETC R1b those scenarios embedded an opaque constant (a value whose <see cref="object.Equals(object?)"/> is user
/// code) in a numeric term through a test-local user method, <c>IdentityFromObject(constant)</c>. Since R1b (compatibility
/// entry C-06) a user method is not proven free of effects, so the simplifier no longer merges, cancels or reorders such a
/// term, and the comparisons the scenarios observe would never run. The carrier built here keeps the constant, declared as
/// <see cref="object"/>, in a term the simplifier may rewrite: <c>Math.Truncate((constant as double?) ?? 1.0)</c>. Every
/// node of it is inert (an allow-listed total function, a type test conversion, a coalesce without conversion), it always
/// evaluates to <c>1.0</c> like the former method, and the structural equality, hashing and canonical ordering reach the
/// constant exactly once per comparison, as they reached the former call's single argument.
/// </para>
/// <para>
/// The behavior of the former user-method carrier under the new contract (each call kept once, in order) is asserted by
/// <c>ExpressionTreeR1bEffectTests.UserMethodCarrierChains_KeepEveryCallInOrder</c>.
/// </para>
/// </remarks>
internal static class SafeOpaqueTerm
{
    /// <summary>The allow-listed <see cref="Math.Truncate(double)"/> function.</summary>
    private static readonly MethodInfo Truncate = typeof(Math).GetMethod(nameof(Math.Truncate), [typeof(double)])!;

    /// <summary>Builds <c>Math.Truncate((value as double?) ?? 1.0)</c>, with <paramref name="value"/> declared as <see cref="object"/>.</summary>
    /// <param name="value">The opaque constant value.</param>
    /// <returns>The carrier term, which evaluates to <c>1.0</c>.</returns>
    public static Expression Carrier(object? value)
        => Expression.Call(
            Truncate,
            Expression.Coalesce(Expression.TypeAs(Expression.Constant(value, typeof(object)), typeof(double?)), Expression.Constant(1.0)));
}
