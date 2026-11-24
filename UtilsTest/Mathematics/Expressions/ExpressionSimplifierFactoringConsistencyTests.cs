using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Mission C3c, consistency of the level and whole-chain factoring (<c>docs/expressions/SimplifierFlatten.md</c> §21): a chain the
/// raw admission refused is factored at its boundary when its level by level result is a factorable chain (M1), the global
/// factoring compares coefficients by magnitude (M3), and the coefficient of a binary factoring made of two single terms is
/// canonical (M2). Each result is the one the second call reached on <c>master</c> <c>523fc708</c>, and a fixed point.
/// </summary>
[TestClass]
[TestCategory("S5-Audit")]
public class ExpressionSimplifierFactoringConsistencyTests
{
    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");
    private static readonly ParameterExpression Y = Expression.Parameter(typeof(double), "y");
    private static readonly ParameterExpression Z = Expression.Parameter(typeof(double), "z");
    private static readonly ParameterExpression A = Expression.Parameter(typeof(double), "a");
    private static readonly ParameterExpression B = Expression.Parameter(typeof(double), "b");
    private static readonly ParameterExpression C = Expression.Parameter(typeof(double), "c");
    private static readonly ParameterExpression D = Expression.Parameter(typeof(double), "d");
    private static readonly ParameterExpression[] Variables = [X, Y, Z, A, B, C, D];
    private static readonly MethodInfo Sin = typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!;

    /// <summary>A derived simplifier without any member: it keeps the historical treatment.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>Counts its calls, so that an evaluation count can be compared.</summary>
    private static class Probe
    {
        /// <summary>The number of calls of <see cref="F"/>, on the current thread.</summary>
        [ThreadStatic]
        public static int Calls;

        /// <summary>An observable call: counts itself and returns 3.</summary>
        /// <returns>3.</returns>
        public static double F()
        {
            Calls++;
            return 3;
        }
    }

    /// <summary>Simplifies with a new built-in simplifier.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The simplified expression.</returns>
    private static Expression S(Expression expression) => new ExpressionSimplifier().Simplify(expression);

    /// <summary>Formats with the engine's safe formatter.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The text.</returns>
    private static string Text(Expression expression) => ExpressionTraceFormatter.Format(expression);

    /// <summary>A constant of <see cref="double"/>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The constant.</returns>
    private static ConstantExpression K(double value) => Expression.Constant(value);

    /// <summary>
    /// Asserts that one call gives <paramref name="expected"/>, that it is a structural fixed point, and that it evaluates like the
    /// source over the variables (equal, or within a relative 1e-12 for the reassociation latitude of the symbolic contract).
    /// </summary>
    /// <param name="source">The source over the variables of <see cref="Variables"/>.</param>
    /// <param name="expected">The expected text of the first result.</param>
    private static void AssertOneCall(Expression source, string expected)
    {
        Expression once = S(source);
        Assert.AreEqual(expected, Text(once), Text(source));
        Expression twice = S(once);
        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(once, twice), $"Not a fixed point: {Text(once)} -> {Text(twice)}");
        AssertSameValues(source, once);
    }

    /// <summary>Asserts that two trees over the variables evaluate alike on a few moderate samples.</summary>
    /// <param name="source">The source.</param>
    /// <param name="simplified">The simplified tree.</param>
    private static void AssertSameValues(Expression source, Expression simplified)
    {
        Delegate before = Expression.Lambda(source, Variables).Compile();
        Delegate after = Expression.Lambda(simplified, Variables).Compile();
        double[][] samples = [[0.5, 1.5, 2.5, -1.25, 3, 0.75, -2], [1.25, -3, 0.75, 2, -0.5, 4, 1.5], [-2, 0.5, 1.5, 0.25, 2.5, -1, 3]];
        foreach (double[] sample in samples)
        {
            object[] arguments = sample.Cast<object>().ToArray();
            double a = (double)before.DynamicInvoke(arguments)!, b = (double)after.DynamicInvoke(arguments)!;
            bool same = a.Equals(b) || Math.Abs(a - b) <= 1e-12 * Math.Max(Math.Abs(a), Math.Abs(b));
            Assert.IsTrue(same, $"{Text(source)} -> {Text(simplified)}: {a} against {b}");
        }
    }

    /// <summary>Builds the left-associated, right-associated and balanced sums of signed terms.</summary>
    /// <param name="terms">The signed terms, the first one added.</param>
    /// <returns>The three trees.</returns>
    private static Expression[] Associations(params (Expression Term, bool IsNegative)[] terms)
    {
        Expression left = terms[0].Term;
        for (int i = 1; i < terms.Length; i++) left = terms[i].IsNegative ? Expression.Subtract(left, terms[i].Term) : Expression.Add(left, terms[i].Term);

        // Right-associated: the signs of the tail are those of a sum of signed terms, written with negations.
        Expression Signed((Expression Term, bool IsNegative) term) => term.IsNegative ? Expression.Negate(term.Term) : term.Term;
        Expression right = Signed(terms[^1]);
        for (int i = terms.Length - 2; i >= 0; i--) right = Expression.Add(Signed(terms[i]), right);

        Expression Balanced(int start, int count)
            => count == 1 ? Signed(terms[start]) : Expression.Add(Balanced(start, count / 2), Balanced(start + (count / 2), count - (count / 2)));

        return [left, right, Balanced(0, terms.Length)];
    }

    /// <summary>
    /// The matrix of the mission: every association of the same signed terms gives one tree in one call, a fixed point, and the
    /// symbolic coefficients and repeated factors are factored as the whole-chain factoring factors them.
    /// </summary>
    [TestMethod]
    public void MissionMatrix_EveryAssociation_GivesOneFixedPoint()
    {
        (string Expected, (Expression, bool)[] Terms)[] cases =
        [
            ("2 * (x + y)", [(Expression.Multiply(K(2), X), false), (Expression.Multiply(K(2), Y), false)]),
            ("2 * ((0 - y) + x)", [(Expression.Multiply(K(2), X), false), (Expression.Multiply(K(2), Y), true)]),
            ("a * (x + y)", [(Expression.Multiply(A, X), false), (Expression.Multiply(A, Y), false)]),
            ("x * (a + b)", [(Expression.Multiply(A, X), false), (Expression.Multiply(B, X), false)]),
            ("a * (b + c)", [(Expression.Multiply(A, B), false), (Expression.Multiply(A, C), false)]),
            ("a * (b + (c + d))", [(Expression.Multiply(A, B), false), (Expression.Multiply(A, C), false), (Expression.Multiply(A, D), false)]),
            ("x * ((2 * a) + (3 * b))", [(Expression.Multiply(Expression.Multiply(K(2), A), X), false), (Expression.Multiply(Expression.Multiply(K(3), B), X), false)]),
            ("2 * ((0 - y) + x)", [(Expression.Multiply(K(-2), Y), false), (Expression.Multiply(K(2), X), false)]),
            ("2 * ((0 - b) + ((0 - c) + (a + d)))", [(Expression.Multiply(K(2), A), false), (Expression.Multiply(K(-2), B), false), (Expression.Multiply(K(2), C), true), (Expression.Multiply(K(-2), D), true)]),
        ];
        foreach ((string expected, (Expression, bool)[] terms) in cases)
        {
            foreach (Expression source in Associations(terms)) AssertOneCall(source, expected);
        }

        AssertOneCall(Expression.Subtract(X, Expression.Subtract(Y, Expression.Subtract(X, Y))), "2 * ((0 - y) + x)");
    }

    /// <summary>
    /// The physical forms of one signed term give one factored tree (§21.4): an added term with a negative coefficient, a
    /// subtracted term, a negated product and a subtracted negative coefficient. A group whose holders all share one negative
    /// value keeps the historical coefficient (<c>-2 * (x + y)</c>), and a group of subtracted terms its negation.
    /// </summary>
    [TestMethod]
    public void SignedCoefficients_GiveOneFactoredTree()
    {
        Expression twoX = Expression.Multiply(K(2), X);
        Expression[] forms =
        [
            Expression.Subtract(twoX, Expression.Multiply(K(2), Y)),
            Expression.Add(twoX, Expression.Multiply(K(-2), Y)),
            Expression.Add(Expression.Multiply(K(-2), Y), twoX),
            Expression.Add(Expression.Subtract(K(0), Expression.Multiply(K(2), Y)), twoX),
            Expression.Add(Expression.Negate(Expression.Multiply(K(2), Y)), twoX),
            Expression.Add(twoX, Expression.Subtract(K(0), Expression.Multiply(K(2), Y))),
        ];
        foreach (Expression form in forms) AssertOneCall(form, "2 * ((0 - y) + x)");

        AssertOneCall(Expression.Add(Expression.Multiply(K(-2), X), Expression.Multiply(K(-2), Y)), "-2 * (x + y)");
        AssertOneCall(Expression.Subtract(Expression.Multiply(K(-2), X), Expression.Multiply(K(2), Y)), "-(2 * (x + y))");
        AssertOneCall(Expression.Subtract(Expression.Negate(Expression.Multiply(K(2), X)), Expression.Multiply(K(2), Y)), "-(2 * (x + y))");
    }

    /// <summary>
    /// A chain the raw admission refuses (a constant pair, the negation of a constant, a product by one, a negated sum, a factor
    /// that is a sum the levels fold) is factored at its boundary when its level by level result is a factorable chain: the
    /// tree the next call built on <c>master</c> (M1, the reproducers of the stability campaign).
    /// </summary>
    [TestMethod]
    public void RefusedChains_FactorableOnceFolded_AreFactoredInOneCall()
    {
        // gen#2594: a constant pair.
        AssertOneCall(Expression.Subtract(Expression.Subtract(Expression.Subtract(K(2), K(10)), Z), Expression.Multiply(X, Z)), "-8 + (0 - (z * (1 + x)))");

        // gen#2973 reduced: the negation of a constant.
        AssertOneCall(Expression.Subtract(Expression.Add(Expression.Negate(K(1E+300)), Y), Expression.Multiply(X, Y)), "-1E+300 + (y * (1 + (0 - x)))");

        // gen#3999: a product by one.
        AssertOneCall(Expression.Subtract(Expression.Multiply(Expression.Subtract(K(1), Z), K(1)), Expression.Multiply(Z, Y)), "1 + (0 - (z * (1 + y)))");

        // gen#2991 reduced: a factor that is a constant difference.
        AssertOneCall(
            Expression.Add(Expression.Subtract(Z, Expression.Subtract(K(-1), Z)), Expression.Add(Y, Expression.Add(Expression.Multiply(K(2), Y), Expression.Multiply(Expression.Subtract(K(1), K(2)), Y)))),
            "1 + (2 * (z + y))");

        // A chain below a call is the boundary of its own chain (gen#611 reduced).
        AssertOneCall(
            Expression.Add(Expression.Call(Sin, Expression.Subtract(Expression.Subtract(Expression.Negate(K(10)), Y), Expression.Multiply(Y, X))), Z),
            "z + Math.Sin(-10 + (0 - (y * (1 + x))))");

        // An admitted call of the factorable family.
        AssertOneCall(
            Expression.Subtract(Expression.Subtract(Expression.Subtract(K(2), K(10)), Expression.Call(Sin, X)), Expression.Multiply(Z, Expression.Call(Sin, X))),
            "-8 + (0 - ((1 + z) * Math.Sin(x)))");
    }

    /// <summary>
    /// A chain that stays outside the factorable family after the levels (a quotient, a logarithm, a call that is not inert) keeps
    /// the historical result: the boundary test refuses it.
    /// </summary>
    [TestMethod]
    public void RefusedChains_OutsideTheFamily_KeepTheHistoricalResult()
    {
        Expression refused = Expression.Subtract(Expression.Subtract(Expression.Subtract(K(2), K(10)), Z), Expression.Multiply(X, Z));
        MethodInfo log = typeof(Math).GetMethod(nameof(Math.Log), [typeof(double)])!;
        Expression[] sources =
        [
            Expression.Add(refused, Expression.Divide(X, Y)),
            Expression.Add(refused, Expression.Call(log, X)),
            Expression.Add(refused, Expression.Multiply(Z, Expression.Call(typeof(Probe).GetMethod(nameof(Probe.F))!))),
        ];
        foreach (Expression source in sources)
        {
            Assert.AreEqual(Text(Levels(source)), Text(S(source)), Text(source));
            AssertSameValues(source, S(source));
        }
    }

    /// <summary>
    /// Simplifies with the boundary finishing switched off: the level by level result of a refused chain, the result of
    /// <c>master</c> <c>523fc708</c> for the chains outside the factorable family.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The simplified expression.</returns>
    private static Expression Levels(Expression expression)
    {
        ExpressionSimplifier.RefusedChainFinishingDisabled = true;
        try
        {
            return S(expression);
        }
        finally
        {
            ExpressionSimplifier.RefusedChainFinishingDisabled = false;
        }
    }

    /// <summary>
    /// The finishing runs once per refused chain, at its boundary only: never on the crossed operands nor on the nodes built by
    /// the rules of the chain; a chain below a call is the boundary of its own chain.
    /// </summary>
    [TestMethod]
    public void RefusedChain_IsFinishedOnceAtItsBoundary()
    {
        Expression chain = Expression.Subtract(Expression.Subtract(Expression.Subtract(K(2), K(10)), Z), Expression.Multiply(X, Z));
        (long tests, long factorings) = Boundaries(chain);
        Assert.AreEqual(1, tests);
        Assert.AreEqual(1, factorings);

        (tests, factorings) = Boundaries(Expression.Add(Expression.Call(Sin, chain), Expression.Subtract(Expression.Subtract(K(1), K(2)), Y)));
        // The root and the chain below the call, and the roots of the comparisons the rules of the root make (each one an operation of its own).
        Assert.IsTrue(tests >= 2, $"{tests} boundary tests");
        Assert.AreEqual(1, factorings);
    }

    /// <summary>Simplifies with the admission counters installed and returns the boundary counters.</summary>
    /// <param name="source">The source.</param>
    /// <returns>The boundary tests and factorings.</returns>
    private static (long Tests, long Factorings) Boundaries(Expression source)
    {
        FlattenAdmissionStatistics? previous = ExpressionSimplifier.AdmissionStatistics;
        var statistics = new FlattenAdmissionStatistics();
        ExpressionSimplifier.AdmissionStatistics = statistics;
        try
        {
            S(source);
        }
        finally
        {
            ExpressionSimplifier.AdmissionStatistics = previous;
        }

        return (statistics.BoundaryTests, statistics.BoundaryFactorings);
    }

    /// <summary>
    /// The coefficient of a binary factoring made of two single terms is canonical (M2, §21.5): <c>z*x + z</c> gives
    /// <c>z * (1 + x)</c>, which the next call ordered; <c>gen#1594</c> is covered by
    /// <see cref="ExpressionSimplifierS5A05FinalizationTests.FormerResidues_TakeOneCall"/>.
    /// </summary>
    [TestMethod]
    public void FactoredCoefficientOfTwoTerms_IsCanonical()
    {
        AssertOneCall(Expression.Add(Expression.Multiply(Z, Expression.Add(X, K(0))), Z), "z * (1 + x)");
        AssertOneCall(Expression.Add(Expression.Multiply(Expression.Add(K(0), Y), X), Y), "y * (1 + x)");
        AssertOneCall(Expression.Add(Expression.Multiply(Z, Expression.Divide(Y, X)), Expression.Multiply(Z, K(3))), "z * (3 + (y / x))");
    }

    /// <summary>
    /// The integral types: the factorings are exact modular identities, on every value (overflow included), and a coefficient
    /// of <see cref="int.MinValue"/>, its own negation, is factored by its value only.
    /// </summary>
    [TestMethod]
    public void IntegralTypes_FactorExactly()
    {
        foreach (Type type in new[] { typeof(int), typeof(long) })
        {
            ParameterExpression x = Expression.Parameter(type, "x"), y = Expression.Parameter(type, "y"), z = Expression.Parameter(type, "z");
            Expression Constant(long value) => Expression.Constant(Convert.ChangeType(value, type), type);
            Expression[] sources =
            [
                Expression.Add(Expression.Multiply(Constant(2), x), Expression.Multiply(Constant(-2), y)),
                Expression.Subtract(Expression.Subtract(Expression.Subtract(Constant(2), Constant(10)), z), Expression.Multiply(x, z)),
                Expression.Subtract(x, Expression.Subtract(y, Expression.Subtract(x, y))),
                Expression.Add(Expression.Multiply(Constant(type == typeof(int) ? int.MinValue : long.MinValue), x), Expression.Multiply(Constant(type == typeof(int) ? int.MinValue : long.MinValue), y)),
            ];
            foreach (Expression source in sources)
            {
                Expression once = S(source);
                Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(once, S(once)), Text(once));
                Delegate before = Expression.Lambda(source, x, y, z).Compile(), after = Expression.Lambda(once, x, y, z).Compile();
                long[] values = [0, 1, -1, 7, -13, int.MaxValue, int.MinValue, long.MaxValue, long.MinValue];
                foreach (long a in values)
                {
                    foreach (long b in values)
                    {
                        object[] arguments = [Convert.ChangeType(unchecked(type == typeof(int) ? (int)a : a), type), Convert.ChangeType(unchecked(type == typeof(int) ? (int)b : b), type), Convert.ChangeType(unchecked(type == typeof(int) ? (int)(a ^ b) : a ^ b), type)];
                        Assert.AreEqual(before.DynamicInvoke(arguments), after.DynamicInvoke(arguments), $"{Text(source)} -> {Text(once)}");
                    }
                }
            }

            Assert.AreEqual("2 * ((0 - y) + x)", Text(S(Expression.Add(Expression.Multiply(Constant(2), x), Expression.Multiply(Constant(-2), y)))), type.Name);
        }
    }

    /// <summary>
    /// The floating-point types: a factored coefficient of opposite signs is the IEEE-754 negation, so <c>2*x + (-2)*y</c> and
    /// <c>2*x - 2*y</c> are one tree for <see cref="float"/> too; a non-finite coefficient is never factored.
    /// </summary>
    [TestMethod]
    public void FloatingPointTypes_FactorByMagnitude_AndNeverANonFiniteCoefficient()
    {
        ParameterExpression x = Expression.Parameter(typeof(float), "x"), y = Expression.Parameter(typeof(float), "y");
        Expression f1 = Expression.Add(Expression.Multiply(Expression.Constant(2f), x), Expression.Multiply(Expression.Constant(-2f), y));
        Expression f2 = Expression.Subtract(Expression.Multiply(Expression.Constant(2f), x), Expression.Multiply(Expression.Constant(2f), y));
        Assert.AreEqual(Text(S(f2)), Text(S(f1)));
        Assert.AreEqual("2f * ((0f - y) + x)", Text(S(f1)));

        Expression infinite = Expression.Add(Expression.Multiply(K(double.PositiveInfinity), X), Expression.Multiply(K(double.NegativeInfinity), Y));
        Assert.AreEqual(Text(Levels(infinite)), Text(S(infinite)));
        Expression nan = Expression.Add(Expression.Multiply(K(double.NaN), X), Expression.Multiply(K(double.NaN), Y));
        Assert.AreEqual(Text(Levels(nan)), Text(S(nan)));
    }

    /// <summary>Decimal chains are never canonicalized nor factored: the change leaves them as they were.</summary>
    [TestMethod]
    public void DecimalChains_AreUnchanged()
    {
        ParameterExpression x = Expression.Parameter(typeof(decimal), "x"), y = Expression.Parameter(typeof(decimal), "y");
        Expression source = Expression.Add(Expression.Multiply(Expression.Constant(2m), x), Expression.Multiply(Expression.Constant(-2m), y));
        Assert.AreEqual(Text(Levels(source)), Text(S(source)));
        Expression overflow = Expression.Subtract(Expression.Subtract(Expression.Subtract(Expression.Constant(decimal.MaxValue), Expression.Constant(1m)), x), Expression.Multiply(y, x));
        Assert.AreEqual(Text(Levels(overflow)), Text(S(overflow)));
    }

    /// <summary>
    /// An observable call keeps its evaluations and its order under every policy: the boundary test refuses a chain holding it,
    /// and the coefficients of its terms are not compared across it.
    /// </summary>
    [TestMethod]
    public void ObservableCalls_KeepTheirEvaluations_UnderEveryPolicy()
    {
        MethodInfo f = typeof(Probe).GetMethod(nameof(Probe.F))!;
        Expression source = Expression.Subtract(
            Expression.Subtract(Expression.Subtract(K(2), K(10)), Expression.Multiply(K(2), Expression.Call(f))),
            Expression.Add(Expression.Multiply(K(-2), Y), Expression.Multiply(Expression.Call(f), Y)));
        ExpressionSimplifierOptions[] policies =
        [
            new ExpressionSimplifierOptions(),
            new ExpressionSimplifierOptions { AggressiveSimplification = true },
            new ExpressionSimplifierOptions { AggressiveSimplification = true, ExceptionPreservation = ExpressionExceptionPreservation.None },
        ];
        var lambda = Expression.Lambda<Func<double, double>>(source, Y);
        Probe.Calls = 0;
        double expected = lambda.Compile()(4);
        Assert.AreEqual(2, Probe.Calls);
        foreach (ExpressionSimplifierOptions options in policies)
        {
            var simplified = (Expression<Func<double, double>>)new ExpressionSimplifier { Options = options }.Simplify(lambda);
            Probe.Calls = 0;
            Assert.AreEqual(expected, simplified.Compile()(4));
            Assert.AreEqual(2, Probe.Calls, Text(simplified));
        }
    }

    /// <summary>
    /// A derived simplifier and the switched-off factorable family keep the historical result of a refused chain: the boundary
    /// finishing belongs to the built-in type and to the factorable family.
    /// </summary>
    [TestMethod]
    public void DerivedSimplifiers_AndTheSwitchedOffFamily_KeepTheHistoricalResult()
    {
        Expression chain = Expression.Subtract(Expression.Subtract(Expression.Subtract(K(2), K(10)), Z), Expression.Multiply(X, Z));
        Assert.AreEqual("-8 + ((0 - z) + (0 - (x * z)))", Text(Levels(chain)));
        Assert.AreNotEqual("-8 + (0 - (z * (1 + x)))", Text(new DerivedSimplifier().Simplify(chain)));

        ExpressionSimplifier.FactorableChainsDisabled = true;
        try
        {
            Assert.AreNotEqual("-8 + (0 - (z * (1 + x)))", Text(S(chain)));
        }
        finally
        {
            ExpressionSimplifier.FactorableChainsDisabled = false;
        }
    }

    /// <summary>
    /// Parameters bound by a lambda and shared subtrees give the trees of free parameters and of copies: the boundary is the
    /// lambda body, and a shared product is a term like its copy.
    /// </summary>
    [TestMethod]
    public void BoundParameters_AndSharedSubtrees_GiveTheSameTree()
    {
        Expression chain = Expression.Subtract(Expression.Subtract(Expression.Subtract(K(2), K(10)), Z), Expression.Multiply(X, Z));
        var bound = (LambdaExpression)S(Expression.Lambda(chain, X, Z));
        Assert.AreEqual("-8 + (0 - (z * (1 + x)))", Text(bound.Body));

        Expression shared = Expression.Multiply(K(2), X);
        Expression sharedSource = Expression.Subtract(shared, Expression.Subtract(Y, Expression.Subtract(shared, Y)));
        Expression copied = Expression.Subtract(Expression.Multiply(K(2), X), Expression.Subtract(Y, Expression.Subtract(Expression.Multiply(K(2), X), Y)));
        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(S(copied), S(sharedSource)));
        AssertOneCall(sharedSource, "(-2 * y) + (4 * x)");
    }

    /// <summary>
    /// The comparer identifies the physical forms of one signed sum (they are one tree), and a refused chain with the canonical
    /// form of its next call.
    /// </summary>
    [TestMethod]
    public void Comparer_IdentifiesTheUnifiedForms()
    {
        ExpressionComparer comparer = ExpressionComparer.Default;
        Expression twoX = Expression.Multiply(K(2), X);
        Assert.IsTrue(comparer.Equals(Expression.Subtract(twoX, Expression.Multiply(K(2), Y)), Expression.Add(Expression.Multiply(K(-2), Y), twoX)));
        Expression refused = Expression.Subtract(Expression.Subtract(Expression.Subtract(K(2), K(10)), Z), Expression.Multiply(X, Z));
        Assert.IsTrue(comparer.Equals(refused, S(refused)));
        Assert.AreEqual(comparer.GetHashCode(refused), comparer.GetHashCode(S(refused)));
    }
}
