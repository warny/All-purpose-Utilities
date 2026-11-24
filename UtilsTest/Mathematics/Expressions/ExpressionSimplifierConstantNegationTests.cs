using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// S5A-05 PR B: the sign model of <c>ExpressionSimplifier.TryFoldNegation</c> folds an ordinary negation of a numeric constant
/// exactly, wherever the negation appears (<c>docs/expressions/SimplifierFlatten.md</c> §19).
/// </summary>
[TestClass]
public class ExpressionSimplifierConstantNegationTests
{
    /// <summary>A derived simplifier without any member.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>A value type with a user-defined negation.</summary>
    private readonly struct Signed
    {
        /// <summary>Initializes a value.</summary>
        /// <param name="value">The value.</param>
        public Signed(int value) => Value = value;

        /// <summary>Gets the value.</summary>
        public int Value { get; }

        /// <summary>A negation that is not the ordinary one: it also doubles.</summary>
        /// <param name="operand">The operand.</param>
        /// <returns><c>-2 * operand</c>.</returns>
        public static Signed operator -(Signed operand) => new(-2 * operand.Value);
    }

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

    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");

    /// <summary>The policies the results must not depend on.</summary>
    private static readonly ExpressionSimplifierOptions[] Policies =
    [
        ExpressionSimplifierOptions.Default,
        new() { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations },
        new() { AggressiveSimplification = true },
        new() { ExceptionPreservation = ExpressionExceptionPreservation.None },
    ];

    /// <summary>Simplifies with a new built-in simplifier.</summary>
    private static Expression S(Expression expression, ExpressionSimplifierOptions? options = null)
        => new ExpressionSimplifier { Options = options ?? ExpressionSimplifierOptions.Default }.Simplify(expression);

    /// <summary>Formats with the engine's safe formatter.</summary>
    private static string Text(Expression expression) => ExpressionTraceFormatter.Format(expression);

    /// <summary>
    /// Asserts that <c>-c</c> folds to a constant of the same type whose bits are those the compiled node computes, under every
    /// policy and for a derived simplifier.
    /// </summary>
    /// <typeparam name="T">The numeric type.</typeparam>
    /// <param name="values">The constants.</param>
    /// <param name="bits">The exact representation of a value.</param>
    private static void AssertFolded<T>(T[] values, Func<T, string> bits)
    {
        foreach (T value in values)
        {
            Expression source = Expression.Negate(Expression.Constant(value, typeof(T)));
            T expected = Expression.Lambda<Func<T>>(source).Compile()();
            foreach (ExpressionSimplifier simplifier in Policies.Select(p => new ExpressionSimplifier { Options = p }).Append(new DerivedSimplifier()))
            {
                Expression result = simplifier.Simplify(source);
                Assert.IsInstanceOfType<ConstantExpression>(result, $"{typeof(T).Name} {value}: {Text(result)}");
                var constant = (ConstantExpression)result;
                Assert.AreEqual(typeof(T), constant.Type);
                Assert.AreEqual(bits(expected), bits((T)constant.Value!), $"{typeof(T).Name} {value}");
            }

            // Nested negations of constants fold too.
            Assert.AreEqual(bits(value), bits((T)((ConstantExpression)S(Expression.Negate(source))).Value!), $"-(-{value})");
        }
    }

    /// <summary>The constant matrix: minimum values, signed zeros, infinities, NaN payloads, subnormals, decimal scale.</summary>
    [TestMethod]
    public void ConstantNegation_IsExact()
    {
        AssertFolded<int>([0, 1, -2, int.MaxValue, int.MinValue], v => v.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AssertFolded<long>([0, 1, -2, long.MaxValue, long.MinValue], v => v.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AssertFolded<short>([0, 1, -2, short.MaxValue, short.MinValue], v => v.ToString(System.Globalization.CultureInfo.InvariantCulture));
        double payload = BitConverter.Int64BitsToDouble(unchecked((long)0xFFF8_0000_0000_1234UL));
        AssertFolded<double>([0.0, -0.0, 2.5, -2.5, double.Epsilon, -double.Epsilon, double.MaxValue, double.PositiveInfinity, double.NegativeInfinity, double.NaN, payload],
            v => BitConverter.DoubleToInt64Bits(v).ToString("X16", System.Globalization.CultureInfo.InvariantCulture));
        AssertFolded<float>([0f, -0f, 2.5f, float.Epsilon, float.NegativeInfinity, float.NaN],
            v => BitConverter.SingleToInt32Bits(v).ToString("X8", System.Globalization.CultureInfo.InvariantCulture));
        AssertFolded<decimal>([0m, 0.00m, decimal.Negate(0.00m), -0.10m, 2.50m, decimal.MaxValue, decimal.MinValue, 1.0000000000000000000000000001m],
            v => string.Join(",", decimal.GetBits(v)));
    }

    /// <summary>Lifted, checked and user negations of constants keep their node; a checked negation still throws.</summary>
    [TestMethod]
    public void NonOrdinaryConstantNegations_AreKept()
    {
        Expression[] sources =
        [
            Expression.Negate(Expression.Constant(5, typeof(int?))),
            Expression.NegateChecked(Expression.Constant(int.MinValue)),
            Expression.NegateChecked(Expression.Constant(2.5)),
            Expression.Negate(Expression.Constant(new Signed(3))),
        ];
        foreach (Expression source in sources)
        {
            Expression result = S(source);
            Assert.IsNotInstanceOfType<ConstantExpression>(result, Text(source));
        }

        Func<int> overflow = Expression.Lambda<Func<int>>(S(Expression.NegateChecked(Expression.Constant(int.MinValue)))).Compile();
        Assert.ThrowsExactly<OverflowException>(() => overflow());
        Assert.AreEqual(-6, Expression.Lambda<Func<Signed>>(S(Expression.Negate(Expression.Constant(new Signed(3))))).Compile()().Value);
    }

    /// <summary>
    /// The negation of a product keeps its node (deferred to PR C, <c>docs/expressions/SimplifierFlatten.md</c> §19.4): absorbing
    /// it into a negative coefficient made the canonical sums depend on the physical shape of their terms.
    /// </summary>
    [TestMethod]
    public void NegatedProduct_KeepsItsNegation()
    {
        Assert.AreEqual("-(-2 * x)", Text(S(Expression.Negate(Expression.Multiply(Expression.Constant(-2.0), X)))));
        Assert.AreEqual("-(2 * x)", Text(S(Expression.Negate(Expression.Multiply(Expression.Constant(2.0), X)))));
        Assert.AreEqual("x + (2 * y)", Text(S(Expression.Subtract(X, Expression.Negate(Expression.Multiply(Expression.Constant(2.0), Expression.Parameter(typeof(double), "y")))))));
    }

    /// <summary>A folded constant operand of an observable product keeps the single evaluation of the call, under every policy.</summary>
    [TestMethod]
    public void FoldedConstant_NextToAnObservableCall_KeepsItsEvaluation()
    {
        Expression call = Expression.Call(typeof(Probe).GetMethod(nameof(Probe.F))!);
        var source = Expression.Lambda<Func<double>>(Expression.Multiply(Expression.Negate(Expression.Constant(2.0)), call));
        foreach (ExpressionSimplifierOptions options in Policies)
        {
            var simplified = (Expression<Func<double>>)S(source, options);
            Assert.IsFalse(Text(simplified.Body).Contains("-(2)", StringComparison.Ordinal), Text(simplified.Body));
            Probe.Calls = 0;
            Assert.AreEqual(-6.0, simplified.Compile()());
            Assert.AreEqual(1, Probe.Calls);
        }
    }
    /// <summary>
    /// The factorization families of the S5A-05 audit (§19.5): the signed families A–D are recognized in one call through the
    /// negations; the quotient family E is not factored (deferred quotient atoms). Each result is a structural fixed point.
    /// </summary>
    [TestMethod]
    public void FactorizationFamilies_AreRecognizedThroughNegations()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a"), b = Expression.Parameter(typeof(double), "b");
        ParameterExpression y = Expression.Parameter(typeof(double), "y"), z = Expression.Parameter(typeof(double), "z");
        (Expression Source, string Expected)[] cases =
        [
            (Expression.Add(Expression.Negate(Expression.Multiply(Expression.Constant(2.0), X)), Expression.Multiply(Expression.Constant(2.0), X)), "0"),
            (Expression.Add(Expression.Negate(Expression.Multiply(a, X)), Expression.Multiply(a, X)), "0"),
            (Expression.Add(Expression.Negate(Expression.Multiply(a, X)), Expression.Multiply(a, y)), "a * ((0 - x) + y)"),
            (Expression.Subtract(Expression.Negate(Expression.Multiply(a, X)), Expression.Multiply(a, y)), "-(a * (x + y))"),
            (Expression.Add(Expression.Multiply(a, b), Expression.Multiply(a, b)), "2 * (a * b)"),
            (Expression.Subtract(Expression.Multiply(a, X), Expression.Add(Expression.Multiply(a, y), Expression.Multiply(a, z))), "a * ((0 - y) + ((0 - z) + x))"),
            (Expression.Add(Expression.Negate(Expression.Multiply(a, Expression.Subtract(X, y))), Expression.Multiply(a, X)), "y * a"),
            (Expression.Add(Expression.Negate(Expression.Divide(a, b)), Expression.Divide(a, b)), "0"),
            (Expression.Subtract(Expression.Divide(Expression.Multiply(a, X), b), Expression.Divide(Expression.Multiply(a, y), b)), "(0 - ((a * y) / b)) + ((a * x) / b)"),
        ];
        foreach ((Expression source, string expected) in cases)
        {
            Expression once = S(source);
            Assert.AreEqual(expected, Text(once), Text(source));
            Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(once, S(once)), Text(once));
        }
    }

    /// <summary>A factorization that would merge two evaluations of an observable call is refused: both calls stay.</summary>
    [TestMethod]
    public void Factorization_OverAnObservableCall_KeepsBothEvaluations()
    {
        Expression F() => Expression.Call(typeof(Probe).GetMethod(nameof(Probe.F))!);
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        Expression[] sources =
        [
            Expression.Subtract(Expression.Multiply(F(), X), Expression.Multiply(F(), y)),
            Expression.Add(Expression.Negate(Expression.Multiply(F(), X)), Expression.Multiply(F(), X)),
            Expression.Add(Expression.Multiply(F(), X), Expression.Multiply(F(), X)),
        ];
        foreach (Expression body in sources)
        {
            var source = Expression.Lambda<Func<double, double, double>>(body, X, y);
            foreach (ExpressionSimplifierOptions options in Policies)
            {
                var simplified = (Expression<Func<double, double, double>>)S(source, options);
                Probe.Calls = 0;
                double expected = source.Compile()(2, 5);
                Probe.Calls = 0;
                Assert.AreEqual(expected, simplified.Compile()(2, 5), Text(simplified));
                Assert.AreEqual(2, Probe.Calls, $"{Text(source)} -> {Text(simplified)} ({options})");
            }
        }
    }

    /// <summary>The comparer identifies the folded forms, and a second call keeps them.</summary>
    [TestMethod]
    public void Comparer_IdentifiesTheFoldedForms()
    {
        Assert.IsTrue(ExpressionComparer.Default.Equals(Expression.Negate(Expression.Constant(2.0)), Expression.Constant(-2.0)));
        Expression once = S(Expression.Multiply(Expression.Negate(Expression.Constant(2.5)), X));
        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(once, S(once)));
    }
}
