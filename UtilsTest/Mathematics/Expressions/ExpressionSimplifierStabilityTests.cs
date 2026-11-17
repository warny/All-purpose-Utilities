using System;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// The stabilization of the simplifier (mission C3d, <c>docs/expressions/SimplifierFlatten.md</c> §14): the rules and the
/// canonicalizations finish the nodes they rebuild, so one public call returns the tree a second call returned. Each case is the
/// reduced representative of a family of the stability campaign.
/// </summary>
/// <remarks>
/// Every case checks that the first result is a structural fixed point (raw structural equality, never a hash), that it
/// evaluates like the source on samples and, when given, its exact form.
/// </remarks>
[TestClass]
public class ExpressionSimplifierStabilityTests
{
    /// <summary>A derived simplifier without any member: it keeps the historical rules.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
    }

    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");
    private static readonly ParameterExpression Y = Expression.Parameter(typeof(double), "y");
    private static readonly ParameterExpression Z = Expression.Parameter(typeof(double), "z");

    /// <summary>The policies the results must not depend on (every operand is inert arithmetic).</summary>
    private static readonly ExpressionSimplifierOptions[] Policies =
    [
        ExpressionSimplifierOptions.Default,
        new() { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations },
        new() { AggressiveSimplification = true },
        new() { ExceptionPreservation = ExpressionExceptionPreservation.None },
    ];

    private static ConstantExpression K(double value) => Expression.Constant(value);

    /// <summary>Simplifies with a fresh built-in simplifier of <paramref name="options"/>.</summary>
    private static Expression S(Expression e, ExpressionSimplifierOptions? options = null)
        => new ExpressionSimplifier { Options = options ?? ExpressionSimplifierOptions.Default }.Simplify(e);

    /// <summary>
    /// Asserts that one call reaches a fixed point under every policy, keeps the values of <paramref name="source"/> and,
    /// when <paramref name="expected"/> is given, prints as it.
    /// </summary>
    private static Expression AssertStable(Expression source, string? expected = null)
    {
        Expression first = S(source);
        foreach (ExpressionSimplifierOptions options in Policies)
        {
            Expression once = S(source, options);
            Expression twice = S(once, options);
            Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(once, twice), $"Not a fixed point: {once} -> {twice}");
        }

        if (expected is not null) Assert.AreEqual(expected, first.ToString());
        AssertSameValues(source, first);
        return first;
    }

    /// <summary>Compiles both trees over x, y and z and compares them on samples (within the reassociation latitude).</summary>
    private static void AssertSameValues(Expression source, Expression result)
    {
        Func<double, double, double, double> f = Expression.Lambda<Func<double, double, double, double>>(source, X, Y, Z).Compile();
        Func<double, double, double, double> g = Expression.Lambda<Func<double, double, double, double>>(result, X, Y, Z).Compile();
        double[] samples = [0, 1, -1, 2.5, -3.75, 7, 0.125];
        foreach (double x in samples)
        {
            foreach (double y in samples)
            {
                double z = x - y + 0.5;
                double a = f(x, y, z), b = g(x, y, z);
                if (double.IsNaN(a) && double.IsNaN(b)) continue;
                Assert.AreEqual(a, b, 1e-9 * Math.Max(1, Math.Abs(a)), $"{source} -> {result} at ({x}, {y}, {z})");
            }
        }
    }

    /// <summary>
    /// Cycle family F1: <c>(1 - y) - (x / z)</c> and <c>1 + ((0 - y) + (0 - (x / z)))</c> rewrote each other at every call
    /// (the canonical form through <c>SubstractionWithAddition</c>, which returned <c>(1 - y) - t</c> as built).
    /// </summary>
    [TestMethod]
    public void ReassociationCycle_ReachesTheCanonicalFormInOneCall()
    {
        Expression t = Expression.Divide(X, Z);
        Expression canonical = AssertStable(Expression.Subtract(Expression.Subtract(K(1), Y), t), "(1 + ((0 - y) + (0 - (x / z))))");
        AssertStable(Expression.Add(K(1), Expression.Add(Expression.Subtract(K(0), Y), Expression.Subtract(K(0), t))), canonical.ToString());
    }

    /// <summary>Family F1: the constant pair a reassociation built (<c>(2 - 0.5) - x</c>) is folded in the same call.</summary>
    [TestMethod]
    public void Reassociation_FoldsTheConstantPairItBuilds()
    {
        Expression folded = AssertStable(Expression.Subtract(K(2), Expression.Subtract(X, K(0.5))));
        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(Expression.Add(K(2.5), Expression.Subtract(K(0), X)), folded), folded.ToString());
        AssertStable(Expression.Subtract(X, Expression.Subtract(Y, Expression.Subtract(K(0), K(0.5)))));
        AssertStable(Expression.Add(Expression.Negate(Expression.Add(Y, K(4))), Z));
    }

    /// <summary>Family F2: a sum whose terms are all subtracted is a negation (<c>(1 - x) - 1</c> gave <c>0 - x</c>, then <c>-x</c>).</summary>
    [TestMethod]
    public void AllSubtractedSum_IsANegation()
    {
        AssertStable(Expression.Subtract(Expression.Subtract(K(1), X), K(1)), "-x");
        AssertStable(Expression.Subtract(Expression.Subtract(Expression.Subtract(K(1), X), K(1)), Y), "-(x + y)");
    }

    /// <summary>
    /// Family F3: the division a nested-division rule builds is finished (<c>x / (1 / x)</c> gave <c>(x ** 2) / 1</c>). A
    /// non-finite constant keeps the division as built: its <c>NaN</c> fold belongs to the policy (S5 P18 C2b, deferred family).
    /// </summary>
    [TestMethod]
    public void NestedDivision_IsFinished()
    {
        AssertStable(Expression.Divide(X, Expression.Divide(K(1), X)), "(x ** 2)");
        AssertStable(Expression.Divide(K(-1), Expression.Divide(X, Y)), "-(y / x)");
        AssertStable(Expression.Divide(K(-2), Expression.Divide(K(-1), X)));
    }

    /// <summary>Family F4: the constants and negations of a product are combined in one call.</summary>
    [TestMethod]
    public void Product_CombinesConstantsAndNegations()
    {
        AssertStable(Expression.Multiply(Expression.Multiply(X, K(-2)), K(-2)), "(4 * x)");
        AssertStable(Expression.Multiply(Expression.Multiply(X, K(2)), K(0.5)), "x");
        AssertStable(Expression.Multiply(K(-2), Expression.Multiply(X, K(0.5))), "-x");
        AssertStable(Expression.Multiply(Expression.Multiply(X, K(-1)), K(-2)), "-(-2 * x)");
        AssertStable(Expression.Multiply(K(0.5), Expression.Add(X, X)), "x");
    }

    /// <summary>Family F5: the coefficient of a factored sum is canonical (<c>y * (x + (1 + (0 - x)))</c> took a second call).</summary>
    [TestMethod]
    public void FactoredCoefficient_IsCanonical()
    {
        AssertStable(Expression.Add(Expression.Multiply(X, Y), Expression.Multiply(Expression.Subtract(K(1), X), Y)), "y");
        AssertStable(Expression.Add(Expression.Multiply(X, K(-2)), Expression.Multiply(Expression.Subtract(K(1), X), K(-2))), "-2");
        AssertStable(Expression.Add(Y, Expression.Multiply(Expression.Subtract(Y, Z), Y)));
        AssertStable(Expression.Add(Expression.Multiply(Expression.Subtract(K(0), X), Y), Y));
    }

    /// <summary>Family F6: an unsigned subtracted constant is folded to its modular value, as the next call folded it.</summary>
    [TestMethod]
    public void UnsignedSubtractedConstant_IsFoldedOnce()
    {
        ParameterExpression p = Expression.Parameter(typeof(ulong), "p");
        Expression source = Expression.Subtract(p, Expression.Constant(9ul));
        Expression once = S(source);
        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(once, S(once)), once.ToString());
        Func<ulong, ulong> f = Expression.Lambda<Func<ulong, ulong>>(source, p).Compile();
        Func<ulong, ulong> g = Expression.Lambda<Func<ulong, ulong>>(once, p).Compile();
        foreach (ulong value in new ulong[] { 0, 1, 9, 10, ulong.MaxValue }) Assert.AreEqual(f(value), g(value));
    }

    /// <summary>A decimal product is never reassociated: <c>(x * 1e20m) * 1e-20m</c> may overflow where the folded form does not.</summary>
    [TestMethod]
    public void DecimalProduct_KeepsItsConstants()
    {
        ParameterExpression m = Expression.Parameter(typeof(decimal), "m");
        Expression source = Expression.Multiply(Expression.Multiply(m, Expression.Constant(1e20m)), Expression.Constant(1e-20m));
        Expression once = S(source);
        Func<decimal, decimal> g = Expression.Lambda<Func<decimal, decimal>>(once, m).Compile();
        Assert.ThrowsExactly<OverflowException>(() => g(decimal.MaxValue / 2), once.ToString());
    }

    /// <summary>A derived simplifier keeps the historical rules: the reassociation still returns its nodes as built.</summary>
    [TestMethod]
    public void DerivedSimplifier_KeepsTheHistoricalRules()
    {
        Expression source = Expression.Subtract(K(2), Expression.Subtract(X, K(0.5)));
        Expression derived = new DerivedSimplifier().Simplify(source);
        Assert.IsFalse(ExpressionComparer.StructuralEqualsRaw(derived, S(source)), derived.ToString());
        AssertSameValues(source, derived);
    }

    /// <summary>The finishing re-entries are counted: a budget below the work refuses cleanly, the same input then succeeds.</summary>
    [TestMethod]
    public void Finishing_ConsumesTheTransformationBudget()
    {
        Expression source = Expression.Subtract(Expression.Subtract(K(1), Y), Expression.Divide(X, Z));
        Assert.ThrowsExactly<ExpressionResourceLimitException>(
            () => new ExpressionSimplifier(ExpressionResourceLimits.Unlimited with { MaxTransformVisits = 3 }).Simplify(source));
        AssertStable(source);
    }
}
