using System;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Additive sign audit (<c>docs/expressions/SimplifierFlatten.md</c> §20): the permanent reproducers of the physical placement
/// of the sign of an additive term. They pin the behavior of <c>master</c> <c>1c7cec21</c>, which this audit keeps.
/// </summary>
/// <remarks>
/// <para>
/// The logical analysis is already sign-consistent: the merge of identical monomials combines signed coefficients whatever
/// their physical form (<c>(x - (-2 * y)) - 2 * y</c> gives <c>x</c>). The reconstruction is not: one sum still has two canonical
/// forms depending on where its sign sits (<c>x + (-2 * y)</c> and <c>x + (0 - (2 * y))</c>). Every normalization of the
/// reconstruction that was measured makes some first results unstable: it equalizes coefficients that only the factoring of whole
/// chains then factors, at the next call. A change of these tests must therefore come with a change of the level factoring.
/// </para>
/// </remarks>
[TestClass]
[TestCategory("S5-Audit")]
public class ExpressionSimplifierAdditiveSignTests
{
    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");
    private static readonly ParameterExpression Y = Expression.Parameter(typeof(double), "y");
    private static readonly ParameterExpression A = Expression.Parameter(typeof(double), "a");
    private static readonly ParameterExpression B = Expression.Parameter(typeof(double), "b");

    /// <summary>Counts its calls.</summary>
    private static class Probe
    {
        /// <summary>The number of calls of <see cref="F"/>, on the current thread.</summary>
        [ThreadStatic]
        public static int Calls;

        /// <summary>An observable call returning 3.</summary>
        /// <returns>3.</returns>
        public static double F()
        {
            Calls++;
            return 3;
        }
    }

    /// <summary>The policies the results are checked under.</summary>
    private static readonly ExpressionSimplifierOptions[] Policies =
    [
        ExpressionSimplifierOptions.Default,
        new() { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations },
        new() { AggressiveSimplification = true, ExceptionPreservation = ExpressionExceptionPreservation.None },
    ];

    private static ConstantExpression C(double value) => Expression.Constant(value);

    private static Expression Product(double coefficient, Expression factor) => Expression.Multiply(C(coefficient), factor);

    /// <summary>Simplifies with a new built-in simplifier.</summary>
    private static Expression S(Expression expression, ExpressionSimplifierOptions? options = null)
        => new ExpressionSimplifier { Options = options ?? ExpressionSimplifierOptions.Default }.Simplify(expression);

    /// <summary>Formats with the engine's safe formatter.</summary>
    private static string Text(Expression expression) => ExpressionTraceFormatter.Format(expression);

    /// <summary>Asserts the first result, that it is a structural fixed point, and that it evaluates like the source.</summary>
    private static void AssertForm(Expression source, string expected)
    {
        Expression once = S(source);
        Assert.AreEqual(expected, Text(once), Text(source));
        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(once, S(once)), $"Not a fixed point: {Text(once)}");
        var parameters = new[] { X, Y, A, B };
        Func<double, double, double, double, double> before = Expression.Lambda<Func<double, double, double, double, double>>(source, parameters).Compile();
        Func<double, double, double, double, double> after = Expression.Lambda<Func<double, double, double, double, double>>(once, parameters).Compile();
        foreach ((double x, double y, double a, double b) in new[] { (0.5, 1.5, 2.5, -3.0), (-1.25, 3.0, 0.75, 4.0) })
        {
            double p = before(x, y, a, b), q = after(x, y, a, b);
            Assert.IsTrue(p.Equals(q) || Math.Abs(p - q) <= 1e-12 * Math.Max(Math.Abs(p), Math.Abs(q)), $"{Text(source)}: {p} against {q}");
        }
    }

    /// <summary>The matrix of the mission on <c>master</c>: every result is a fixed point, the sign keeps its physical place.</summary>
    [TestMethod]
    public void SignMatrix_IsPinned()
    {
        AssertForm(Expression.Negate(Product(2, X)), "-(2 * x)");
        AssertForm(Expression.Negate(Product(-2, X)), "-(-2 * x)");
        AssertForm(Expression.Subtract(C(0), Product(-2, X)), "-(-2 * x)");
        AssertForm(Expression.Subtract(C(0), Product(2, X)), "-(2 * x)");
        AssertForm(Expression.Add(X, Expression.Negate(Product(2, Y))), "x + (0 - (2 * y))");
        AssertForm(Expression.Subtract(X, Expression.Negate(Product(2, Y))), "x + (2 * y)");
        AssertForm(Expression.Add(X, Product(-2, Y)), "x + (-2 * y)");
        AssertForm(Expression.Subtract(X, Product(-2, Y)), "x + (0 - (-2 * y))");
        AssertForm(Expression.Add(Expression.Negate(Expression.Multiply(A, B)), Expression.Multiply(A, B)), "0");
        AssertForm(Expression.Subtract(Expression.Negate(Expression.Multiply(A, B)), Expression.Multiply(A, B)), "-2 * (a * b)");
        AssertForm(Expression.Subtract(Expression.Multiply(A, X), Expression.Multiply(A, Y)), "a * ((0 - y) + x)");
        AssertForm(Expression.Subtract(Expression.Multiply(A, X), Expression.Add(Expression.Multiply(A, Y), Expression.Multiply(A, B))), "a * ((0 - y) + ((0 - b) + x))");
    }

    /// <summary>
    /// The logical analysis is sign-consistent: the merge of identical monomials combines signed coefficients across the physical
    /// forms (negation, subtraction, negative constant).
    /// </summary>
    [TestMethod]
    public void LogicalAnalysis_MergesAcrossPhysicalForms()
    {
        AssertForm(Expression.Subtract(Expression.Subtract(X, Product(-2, Y)), Product(2, Y)), "x");
        AssertForm(Expression.Add(Expression.Add(X, Expression.Negate(Product(2, Y))), Product(2, Y)), "x");
        AssertForm(Expression.Add(Expression.Add(X, Product(-2, Y)), Product(2, Y)), "x");
        AssertForm(Expression.Add(Expression.Negate(Product(2, X)), Product(-2, X)), "-4 * x");
        AssertForm(Expression.Add(Expression.Subtract(C(0), Product(-2, X)), Product(-2, X)), "0");
        AssertForm(Expression.Add(Expression.Subtract(X, Product(-2, Y)), Product(2, Y)), "x + (4 * y)");
    }

    /// <summary>
    /// The competing canonical forms the audit keeps: one value, two sums, which the comparer does not identify. The
    /// normalization that would unify them is the deferred change of §20.4.
    /// </summary>
    [TestMethod]
    public void CompetingForms_AreDocumented()
    {
        Expression negativeCoefficient = Expression.Add(X, Product(-2, Y));
        Expression negatedProduct = Expression.Add(X, Expression.Negate(Product(2, Y)));
        Assert.AreNotEqual(Text(S(negativeCoefficient)), Text(S(negatedProduct)));
        Assert.IsFalse(ExpressionComparer.Default.Equals(negativeCoefficient, negatedProduct));
    }

    /// <summary>Associations, repeated terms and quotients with negative coefficients: the forms of <c>master</c>.</summary>
    [TestMethod]
    public void Associations_ArePinned()
    {
        AssertForm(Expression.Add(Expression.Add(X, Product(-2, Y)), Product(-3, A)), "x + ((-3 * a) + (-2 * y))");
        AssertForm(Expression.Add(X, Expression.Add(Product(-2, Y), Product(-3, A))), "x + ((-3 * a) + (-2 * y))");
        AssertForm(Expression.Add(Expression.Add(X, Product(-2, Y)), Expression.Add(A, Product(-3, B))), "x + (a + ((-3 * b) + (-2 * y)))");
        AssertForm(Expression.Subtract(Expression.Subtract(X, Product(-2, Y)), Product(-2, Y)), "x + (4 * y)");
        AssertForm(Expression.Subtract(X, Product(-2, Expression.Divide(Y, A))), "x + (0 - (-2 * (y / a)))");
    }

    /// <summary>
    /// Powers, extreme, infinite and NaN coefficients, bound parameters and a shared negative-coefficient product: one call
    /// reaches a fixed point that keeps the type and the values (NaN stays NaN).
    /// </summary>
    [TestMethod]
    public void Variants_AreStableAndExact()
    {
        Expression shared = Product(-2, Y);
        Expression[] sources =
        [
            Expression.Subtract(X, Product(-2, Expression.Power(Y, C(2)))),
            Expression.Subtract(X, Product(-1e300, Y)),
            Expression.Subtract(X, Product(double.NegativeInfinity, Y)),
            Expression.Add(X, Product(double.NaN, Y)),
            Expression.Subtract(Expression.Add(X, shared), shared),
            Expression.Subtract(Expression.Subtract(X, shared), shared),
        ];
        foreach (Expression body in sources)
        {
            Expression once = S(body);
            Assert.AreEqual(body.Type, once.Type);
            Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(once, S(once)), $"Not a fixed point: {Text(once)}");
            Func<double, double, double> before = Expression.Lambda<Func<double, double, double>>(body, X, Y).Compile();
            Func<double, double, double> after = Expression.Lambda<Func<double, double, double>>(once, X, Y).Compile();
            double p = before(1.5, 2.5), q = after(1.5, 2.5);
            Assert.IsTrue(double.IsNaN(p) ? double.IsNaN(q) : p.Equals(q) || Math.Abs(p - q) <= 1e-12 * Math.Max(Math.Abs(p), Math.Abs(q)), $"{Text(body)} -> {Text(once)}: {p} against {q}");
        }

        // Bound parameters give the same form as free ones.
        var bound = Expression.Lambda<Func<double, double, double>>(Expression.Subtract(X, Product(-2, Y)), X, Y);
        Assert.AreEqual("(x, y) => x + (0 - (-2 * y))", Text(S(bound)));
    }

    /// <summary>An observable call with a negative coefficient keeps its single evaluation, under every policy.</summary>
    [TestMethod]
    public void ObservableTerm_WithANegativeCoefficient_KeepsItsEvaluation()
    {
        Expression call = Expression.Call(typeof(Probe).GetMethod(nameof(Probe.F))!);
        Expression[] bodies =
        [
            Expression.Subtract(X, Product(-2, call)),
            Expression.Add(X, Expression.Negate(Product(2, call))),
            Expression.Subtract(C(0), Product(-2, call)),
        ];
        foreach (Expression body in bodies)
        {
            var source = Expression.Lambda<Func<double, double>>(body, X);
            foreach (ExpressionSimplifierOptions options in Policies)
            {
                var simplified = (Expression<Func<double, double>>)S(source, options);
                Probe.Calls = 0;
                double expected = source.Compile()(2);
                Probe.Calls = 0;
                Assert.AreEqual(expected, simplified.Compile()(2), Text(simplified));
                Assert.AreEqual(1, Probe.Calls, $"{Text(source)} -> {Text(simplified)} ({options})");
            }
        }
    }
}
