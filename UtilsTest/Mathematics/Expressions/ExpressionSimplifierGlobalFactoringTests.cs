using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// The global factoring of the additive chains (mission C3c, <c>docs/expressions/SimplifierFlatten.md</c> §13): the terms of a
/// canonicalized sum holding the same factors merge (phase A), and the terms of a whole factorable chain are grouped by their
/// common factors (phase B), from the analysis of the terms instead of a succession of binary factorings.
/// </summary>
/// <remarks>
/// Every new form is checked on its values (the source and the result are compiled and evaluated on samples) and on its second
/// pass, which must give the same tree.
/// </remarks>
[TestClass]
public class ExpressionSimplifierGlobalFactoringTests
{
    /// <summary>A derived simplifier without any member: it keeps the historical canonicalization and the level by level treatment.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
    }

    private static readonly ParameterExpression A = Expression.Parameter(typeof(double), "a");
    private static readonly ParameterExpression B = Expression.Parameter(typeof(double), "b");
    private static readonly ParameterExpression C = Expression.Parameter(typeof(double), "c");
    private static readonly ParameterExpression D = Expression.Parameter(typeof(double), "d");
    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");
    private static readonly ParameterExpression Y = Expression.Parameter(typeof(double), "y");

    /// <summary>The parameters every sample binds, in order.</summary>
    private static readonly ParameterExpression[] Parameters = [A, B, C, D, X, Y];

    /// <summary>Free <see cref="double"/> parameters for long chains.</summary>
    private static readonly ParameterExpression[] P = [.. Enumerable.Range(0, 600).Select(static i => Expression.Parameter(typeof(double), "p" + i))];

    private static readonly MethodInfo Sin = typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!;
    private static readonly MethodInfo Cos = typeof(Math).GetMethod(nameof(Math.Cos), [typeof(double)])!;
    private static readonly MethodInfo Log = typeof(Math).GetMethod(nameof(Math.Log), [typeof(double)])!;

    /// <summary>The number of calls of <see cref="Unknown"/> on this thread.</summary>
    [ThreadStatic]
    private static int s_calls;

    /// <summary>A function the effect analysis does not know: its calls are observable.</summary>
    /// <returns>The number of calls so far.</returns>
    public static double Unknown() => ++s_calls;

    private static readonly MethodInfo UnknownMethod = typeof(ExpressionSimplifierGlobalFactoringTests).GetMethod(nameof(Unknown))!;

    /// <summary>The policies the results must not depend on (every operand of a factorable chain is inert).</summary>
    private static readonly ExpressionSimplifierOptions[] Policies =
    [
        ExpressionSimplifierOptions.Default,
        new() { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations },
        new() { AggressiveSimplification = true },
        new() { ExceptionPreservation = ExpressionExceptionPreservation.None },
        new() { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations, AggressiveSimplification = true, ExceptionPreservation = ExpressionExceptionPreservation.None },
    ];

    private static ConstantExpression K(double value) => Expression.Constant(value);

    private static Expression Mul(params Expression[] factors) => factors.Aggregate(Expression.Multiply);

    private static Expression Pow(Expression @base, double exponent) => Expression.Power(@base, K(exponent));

    private static Expression Call(MethodInfo method, Expression argument) => Expression.Call(method, argument);

    private static Expression LeftSum(IEnumerable<Expression> terms) => terms.Aggregate(Expression.Add);

    private static Expression RightSum(IEnumerable<Expression> terms) => terms.Reverse().Aggregate((acc, term) => Expression.Add(term, acc));

    /// <summary>The balanced sum of <paramref name="terms"/>.</summary>
    /// <param name="terms">The terms.</param>
    /// <returns>The sum.</returns>
    private static Expression BalancedSum(IReadOnlyList<Expression> terms)
        => terms.Count == 1 ? terms[0] : Expression.Add(BalancedSum([.. terms.Take(terms.Count / 2)]), BalancedSum([.. terms.Skip(terms.Count / 2)]));

    /// <summary>The text of a tree, culture-invariant.</summary>
    /// <param name="expression">The tree.</param>
    /// <returns>The text.</returns>
    private static string Text(Expression expression)
    {
        CultureInfo culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            return expression.ToString();
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    /// <summary>Simplifies <paramref name="tree"/> with admission counters installed.</summary>
    /// <param name="tree">The tree.</param>
    /// <param name="options">The policy.</param>
    /// <returns>The result and the counters.</returns>
    private static (Expression Result, FlattenAdmissionStatistics Statistics) Measure(Expression tree, ExpressionSimplifierOptions? options = null)
    {
        var statistics = new FlattenAdmissionStatistics();
        ExpressionSimplifier.AdmissionStatistics = statistics;
        try
        {
            return (new ExpressionSimplifier { Options = options ?? ExpressionSimplifierOptions.Default }.Simplify(tree), statistics);
        }
        finally
        {
            ExpressionSimplifier.AdmissionStatistics = null;
        }
    }

    /// <summary>Evaluates a <see cref="double"/> tree over <see cref="Parameters"/>.</summary>
    /// <param name="tree">The tree.</param>
    /// <param name="values">The values of the parameters.</param>
    /// <returns>The value.</returns>
    private static double Evaluate(Expression tree, double[] values)
        => (double)Expression.Lambda(tree, Parameters).Compile().DynamicInvoke([.. values.Cast<object>()])!;

    /// <summary>The samples of <see cref="AssertSameValues"/>.</summary>
    private static readonly double[][] Samples =
    [
        [1.5, -2, 3, 0.25, 0.75, -1.25],
        [-3, 0.5, -1, 2, -0.5, 4],
        [0, 1, 0, -2, 1.25, 0],
    ];

    /// <summary>Asserts that <paramref name="source"/> and <paramref name="result"/> evaluate alike on <see cref="Samples"/> (relative tolerance).</summary>
    /// <param name="source">The source.</param>
    /// <param name="result">The simplified tree.</param>
    private static void AssertSameValues(Expression source, Expression result)
    {
        foreach (double[] sample in Samples)
        {
            double expected = Evaluate(source, sample), actual = Evaluate(result, sample);
            if (double.IsFinite(expected)) Assert.AreEqual(expected, actual, 1e-9 * Math.Max(1, Math.Abs(expected)), Text(source) + " -> " + Text(result));
            else Assert.AreEqual(expected, actual, Text(source) + " -> " + Text(result));
        }
    }

    /// <summary>Asserts the simplified form, its values, and that a second pass gives the same tree.</summary>
    /// <param name="source">The source.</param>
    /// <param name="expected">The expected text.</param>
    private static void AssertForm(Expression source, string expected)
    {
        Expression once = new ExpressionSimplifier().Simplify(source);
        Assert.AreEqual(expected, Text(once), Text(source));
        AssertSameValues(source, once);
        Expression twice = new ExpressionSimplifier().Simplify(once);
        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(once, twice), "second pass of " + Text(source) + ": " + Text(twice));
    }

    /// <summary>Identical monomials merge, coefficients summed once (§2.1).</summary>
    [TestMethod]
    public void IdenticalTerms_Merge()
    {
        AssertForm(Expression.Add(X, X), "(2 * x)");
        AssertForm(LeftSum([X, X, X]), "(3 * x)");
        AssertForm(LeftSum([Mul(K(2), X), Mul(K(3), X)]), "(5 * x)");
        AssertForm(LeftSum([Mul(K(2), X), Mul(K(3), X), Mul(K(4), X)]), "(9 * x)");
        AssertForm(Expression.Add(Pow(X, 2), Mul(K(2), Pow(X, 2))), "(3 * (x ** 2))");
        AssertForm(Expression.Add(Pow(X, 3), Mul(K(3), Pow(X, 3))), "(4 * (x ** 3))");
        AssertForm(Expression.Add(Mul(K(3), Call(Cos, X)), Mul(K(5), Call(Cos, X))), "(8 * Cos(x))");
        AssertForm(Expression.Add(Call(Sin, X), Call(Sin, X)), "(2 * Sin(x))");
        AssertForm(Expression.Add(Mul(K(2), X, Y), Mul(K(3), X, Y)), "(5 * (x * y))");
        AssertForm(Expression.Add(Mul(K(2), A, B), Mul(K(3), B, A)), "(5 * (a * b))");
    }

    /// <summary>Terms cancel when their coefficients sum to zero (§2.4).</summary>
    [TestMethod]
    public void OppositeTerms_Cancel()
    {
        AssertForm(Expression.Subtract(X, X), "0");
        AssertForm(Expression.Subtract(Mul(K(3), X), Mul(K(3), X)), "0");
        AssertForm(Expression.Subtract(Mul(A, X), Mul(A, X)), "0");
        AssertForm(Expression.Subtract(LeftSum([Mul(K(2), X), Mul(K(3), X)]), Mul(K(5), X)), "0");
        AssertForm(Expression.Subtract(Expression.Add(X, X), X), "x");
        AssertForm(Expression.Add(Expression.Subtract(Expression.Add(X, Y), Y), Y), "(x + y)");
        AssertForm(Expression.Subtract(X, Mul(K(2), X)), "-x");
    }

    /// <summary>Terms sharing a factor are grouped, the residues summed into one coefficient (§2.2, §6).</summary>
    [TestMethod]
    public void PartialCommonFactor_IsFactoredOut()
    {
        AssertForm(Expression.Add(Mul(A, X), Mul(K(3), X)), "(x * (3 + a))");
        AssertForm(Expression.Add(Mul(A, X), Mul(B, X)), "(x * (a + b))");
        AssertForm(Expression.Add(Mul(A, B), Mul(A, C)), "(a * (b + c))");
        AssertForm(Expression.Add(Mul(K(2), A, X), Mul(K(3), B, X)), "(x * ((2 * a) + (3 * b)))");
        AssertForm(LeftSum([Mul(K(2), X), Mul(A, X), Mul(K(3), X)]), "(x * (5 + a))");
        AssertForm(LeftSum([Mul(A, X), Mul(B, X), Mul(C, X)]), "(x * (a + (b + c)))");
        AssertForm(Expression.Add(Mul(A, B), A), "(a * (1 + b))");
        AssertForm(Expression.Add(Mul(K(2), A), Mul(K(2), B)), "(2 * (a + b))");
    }

    /// <summary>Several common factors and their multiplicity (§7).</summary>
    [TestMethod]
    public void SeveralCommonFactors_AreFactoredOut()
    {
        AssertForm(Expression.Add(Mul(A, B, C), Mul(A, B, D)), "(a * (b * (c + d)))");
        AssertForm(Expression.Add(Mul(K(2), A, B), Mul(K(3), B, A)), "(5 * (a * b))");
        AssertForm(LeftSum([Mul(A, X), Mul(A, Y), Mul(B, X), Mul(B, Y)]), "((x + y) * (a + b))");

        // A factor repeated in one term is removed once: x*x*y + x*y*y = x*y*(x + y) for an integral type (no power node).
        ParameterExpression i = Expression.Parameter(typeof(int), "i"), j = Expression.Parameter(typeof(int), "j");
        Expression source = Expression.Add(Mul(i, i, j), Mul(i, j, j));
        Expression result = new ExpressionSimplifier().Simplify(source);
        Assert.AreEqual("(i * (j * (i + j)))", Text(result));
        Func<int, int, int> before = Expression.Lambda<Func<int, int, int>>(source, i, j).Compile();
        Func<int, int, int> after = Expression.Lambda<Func<int, int, int>>(result, i, j).Compile();
        foreach ((int x, int y) in new[] { (3, -4), (int.MaxValue, 7), (-12345, 678) }) Assert.AreEqual(before(x, y), after(x, y));
    }

    /// <summary>A power stays one indivisible factor: <c>x²</c> and <c>x</c> are not merged nor factored (§7).</summary>
    [TestMethod]
    public void Powers_AreIndivisible()
    {
        AssertForm(Expression.Add(Pow(X, 2), Mul(X, Y)), "((x * y) + (x ** 2))");
        AssertForm(Expression.Add(Pow(X, 2), X), "(x + (x ** 2))");
    }

    /// <summary>The mixed chain of the mission (§2.3): no variable is chosen by its name, the factor <c>x</c> is the shared one.</summary>
    [TestMethod]
    public void MixedChain_IsFactoredGlobally()
    {
        Expression source = LeftSum([Pow(X, 2), Mul(K(2), X), Mul(A, X), X, K(15), A, Mul(K(3), Call(Cos, X)), Mul(K(5), Call(Sin, X))]);
        (Expression result, FlattenAdmissionStatistics statistics) = Measure(source);
        Assert.AreEqual("(15 + (a + ((3 * Cos(x)) + ((5 * Sin(x)) + ((x * (3 + a)) + (x ** 2))))))", Text(result));
        AssertSameValues(source, result);
        Assert.AreEqual(1, statistics.Factorable, "the whole chain is admitted at its root");
        Assert.AreEqual(1, statistics.FactorableChains);
        Assert.IsTrue(statistics.MonomialGroups >= 1 && statistics.CommonFactorings >= 1);

        // The same terms in another order and association give the same tree (bound parameters order by their scope).
        Expression[] terms = [Pow(X, 2), Mul(K(2), X), Mul(A, X), X, K(15), A, Mul(K(3), Call(Cos, X)), Mul(K(5), Call(Sin, X))];
        string reference = Text(new ExpressionSimplifier().Simplify(Expression.Lambda(LeftSum(terms), A, X)));
        Assert.AreEqual(reference, Text(new ExpressionSimplifier().Simplify(Expression.Lambda(RightSum(Enumerable.Reverse(terms)), A, X))));
        Assert.AreEqual(reference, Text(new ExpressionSimplifier().Simplify(Expression.Lambda(BalancedSum([.. terms.Skip(3).Concat(terms.Take(3))]), A, X))));
    }

    /// <summary>The associations of one chain give one tree: left, right, balanced (§12).</summary>
    [TestMethod]
    public void Associations_GiveOneTree()
    {
        Expression[] terms = [Mul(K(2), X), Mul(A, X), Mul(K(3), X), Mul(A, B), Mul(B, X), K(4)];
        string left = Text(new ExpressionSimplifier().Simplify(Expression.Lambda(LeftSum(terms), A, B, X)));
        Assert.AreEqual(left, Text(new ExpressionSimplifier().Simplify(Expression.Lambda(RightSum(terms), A, B, X))));
        Assert.AreEqual(left, Text(new ExpressionSimplifier().Simplify(Expression.Lambda(BalancedSum(terms), A, B, X))));
    }

    /// <summary>
    /// The factor chosen when several are shared does not depend on the order of the terms (§6, choice): the factor held by
    /// the most terms first, then the canonical order.
    /// </summary>
    [TestMethod]
    public void FactorChoice_IsDeterministic()
    {
        Expression[] terms = [Mul(A, B), Mul(A, C), Mul(B, C)];
        var forms = new HashSet<string>();
        foreach (int[] order in new[] { new[] { 0, 1, 2 }, [0, 2, 1], [1, 0, 2], [1, 2, 0], [2, 0, 1], [2, 1, 0] })
        {
            Expression source = Expression.Lambda(LeftSum(order.Select(index => terms[index])), A, B, C);
            forms.Add(Text(new ExpressionSimplifier().Simplify(source)));
        }

        Assert.AreEqual(1, forms.Count, string.Join(" | ", forms));

        // The factor shared by three terms wins over the one shared by two.
        AssertForm(LeftSum([Mul(A, B), Mul(B, C), Mul(B, D), Mul(A, D)]), "((a * d) + (b * (a + (c + d))))");
    }

    /// <summary>The coefficients keep the numeric semantics of the binary factoring (§8).</summary>
    [TestMethod]
    public void Coefficients_KeepTheNumericSemantics()
    {
        // Floating-point sums in source order, as the binary factoring folds them.
        AssertForm(LeftSum([Mul(K(0.1), X), Mul(K(0.2), X), Mul(K(0.3), X)]), "(" + (0.1 + 0.2 + 0.3).ToString("R", CultureInfo.InvariantCulture) + " * x)");

        // An overflowing sum gives the infinite coefficient the binary factoring folds.
        Expression overflow = Expression.Add(Mul(K(double.MaxValue), X), Mul(K(double.MaxValue), X));
        Assert.AreEqual("(Infinity * x)", Text(new ExpressionSimplifier().Simplify(overflow)));

        // NaN folds the chain, as before.
        Assert.AreEqual("NaN", Text(new ExpressionSimplifier().Simplify(Expression.Add(Mul(K(double.NaN), X), Mul(K(2), X)))));

        // Signed zero: two constant operands are folded exactly by the levels, -0 + -0 = -0.
        Expression zeros = Expression.Add(Expression.Constant(-0f), Expression.Constant(-0f));
        Assert.IsTrue(float.IsNegative((float)((ConstantExpression)new ExpressionSimplifier().Simplify(zeros)).Value!));

        // Integral types wrap, as the source does; an unsigned difference wraps too.
        ParameterExpression i = Expression.Parameter(typeof(int), "i");
        Expression wrapped = Expression.Add(Mul(Expression.Constant(int.MaxValue), i), Mul(Expression.Constant(int.MaxValue), i));
        Assert.AreEqual("(-2 * i)", Text(new ExpressionSimplifier().Simplify(wrapped)));
        ParameterExpression u = Expression.Parameter(typeof(uint), "u");
        Expression unsigned = Expression.Subtract(Mul(Expression.Constant(3u), u), Mul(Expression.Constant(4u), u));
        Assert.AreEqual("(4294967295 * u)", Text(new ExpressionSimplifier().Simplify(unsigned)));
        Assert.AreEqual(Expression.Lambda<Func<uint, uint>>(unsigned, u).Compile()(7u), Expression.Lambda<Func<uint, uint>>(new ExpressionSimplifier().Simplify(unsigned), u).Compile()(7u));

        // A lone subtracted constant folds as SubstractionOfConstants does, in every type.
        Assert.AreEqual("-1", Text(new ExpressionSimplifier().Simplify(Expression.Subtract(Expression.Subtract(X, X), K(1)))));
    }

    /// <summary>Decimal and checked chains are not canonicalized: their operators may throw (ETC R1b), nothing is merged.</summary>
    [TestMethod]
    public void ThrowingOperators_AreNotMerged()
    {
        ParameterExpression m = Expression.Parameter(typeof(decimal), "m");
        Expression decimals = Expression.Add(Mul(Expression.Constant(2m), m), Mul(Expression.Constant(3m), m));
        Assert.AreEqual(Text(decimals), Text(new ExpressionSimplifier().Simplify(decimals)));

        ParameterExpression i = Expression.Parameter(typeof(int), "i");
        Expression checkedSum = Expression.AddChecked(Expression.AddChecked(i, i), i);
        Expression result = new ExpressionSimplifier().Simplify(checkedSum);
        Assert.AreEqual(ExpressionType.AddChecked, result.NodeType);
        Assert.ThrowsExactly<OverflowException>(() => Expression.Lambda<Func<int, int>>(result, i).Compile()(int.MaxValue));
    }

    /// <summary>
    /// Observable terms are never merged nor moved (§13): two calls of an unknown method stay two calls, evaluated in their
    /// order, and an inert term next to them is not merged across them.
    /// </summary>
    [TestMethod]
    public void ObservableTerms_AreNeverMerged()
    {
        Expression call = Expression.Call(UnknownMethod);
        Expression twice = Expression.Add(call, call);
        Expression result = new ExpressionSimplifier().Simplify(twice);
        s_calls = 0;
        Assert.AreEqual(3.0, Expression.Lambda<Func<double>>(result).Compile()(), "1 + 2: both calls are evaluated");
        Assert.AreEqual(2, s_calls);

        Expression scaled = Expression.Add(Mul(K(2), call), Mul(K(3), call));
        s_calls = 0;
        Assert.AreEqual(2.0 * 1 + 3.0 * 2, Expression.Lambda<Func<double>>(new ExpressionSimplifier().Simplify(scaled)).Compile()());
        Assert.AreEqual(2, s_calls);

        (_, FlattenAdmissionStatistics statistics) = Measure(LeftSum([X, call, X]));
        Assert.AreEqual(0, statistics.Factorable, "a chain holding an observable call is not factorable");
    }

    /// <summary>The additive signature rules the global treatment cannot reproduce keep the chain level by level (§9).</summary>
    [TestMethod]
    public void ChainsWhereARuleMayApply_AreNotFactorable()
    {
        Expression[] refused =
        [
            Expression.Add(Expression.Add(Call(Log, X), Call(Log, Y)), X),
            Expression.Add(Mul(Call(Sin, X), Call(Sin, X)), Mul(Call(Cos, X), Call(Cos, X))),
            Expression.Add(Pow(Call(Sin, X), 2), Pow(Call(Cos, X), 2)),
            Expression.Add(Expression.Add(A, K(3)), Mul(Expression.Add(A, K(3)), X)),
            Expression.Add(Expression.Divide(X, Y), Mul(K(2), Expression.Divide(X, Y))),
            Expression.Add(Expression.Add(K(2), K(3)), X),
            Expression.Add(Mul(K(double.PositiveInfinity), X), X),
        ];
        foreach (Expression source in refused)
        {
            (Expression result, FlattenAdmissionStatistics statistics) = Measure(source);
            Assert.AreEqual(0, statistics.Factorable, Text(source));
            AssertSameValues(source, result);
        }

        MethodInfo doubleSin = typeof(double).GetMethod(nameof(double.Sin), [typeof(double)])!, doubleCos = typeof(double).GetMethod(nameof(double.Cos), [typeof(double)])!;
        Assert.AreEqual("1", Text(new ExpressionSimplifier().Simplify(Expression.Add(Pow(Call(doubleSin, X), 2), Pow(Call(doubleCos, X), 2)))), "sin² + cos² = 1 still applies");
        Assert.AreEqual("((1 + x) * (3 + a))", Text(new ExpressionSimplifier().Simplify(Expression.Add(Expression.Add(A, K(3)), Mul(Expression.Add(A, K(3)), X)))), "a sub-sum equal to a factor is still factored by the binary rule");
    }

    /// <summary>Every policy gives the same result on a factorable chain: its operands are inert.</summary>
    [TestMethod]
    public void Policies_GiveTheSameResult()
    {
        Expression source = LeftSum([Pow(X, 2), Mul(K(2), X), Mul(A, X), X, K(15), A, Mul(K(3), Call(Cos, X))]);
        string expected = Text(new ExpressionSimplifier().Simplify(source));
        foreach (ExpressionSimplifierOptions options in Policies)
        {
            (Expression result, FlattenAdmissionStatistics statistics) = Measure(source, options);
            Assert.AreEqual(expected, Text(result), options.ToString());
            Assert.AreEqual(1, statistics.Factorable, options.ToString());
        }
    }

    /// <summary>A derived simplifier keeps the historical canonicalization and the level by level treatment.</summary>
    [TestMethod]
    public void DerivedSimplifiers_KeepTheHistoricalTreatment()
    {
        var statistics = new FlattenAdmissionStatistics();
        ExpressionSimplifier.AdmissionStatistics = statistics;
        Expression result;
        try
        {
            result = new DerivedSimplifier().Simplify(Expression.Lambda(Expression.Add(X, Expression.Add(X, Y)), X, Y));
        }
        finally
        {
            ExpressionSimplifier.AdmissionStatistics = null;
        }

        Assert.AreEqual("(x, y) => ((x + x) + y)", Text(result));
        Assert.AreEqual(0, statistics.Factorable);
        Assert.AreEqual(0, statistics.MonomialGroups);
        Assert.AreEqual("((a + b) * x)", Text(new DerivedSimplifier().Simplify(Expression.Add(Mul(A, X), Mul(B, X)))), "the binary factoring keeps its historical product");
    }

    /// <summary>Random factorable chains: the values are kept and the result is a fixed point of a second pass (§10).</summary>
    [TestMethod]
    public void RandomChains_KeepTheirValues_AndAreFixedPoints()
    {
        var random = new Random(20261117);
        Expression[] factors = [A, B, C, X, Y, Pow(X, 2), Call(Sin, Y), K(2), K(3), K(-1.5)];
        for (int round = 0; round < 300; round++)
        {
            int count = random.Next(2, 9);
            var terms = new List<Expression>();
            for (int index = 0; index < count; index++)
            {
                int size = random.Next(1, 4);
                Expression term = factors[random.Next(factors.Length)];
                for (int k = 1; k < size; k++)
                {
                    Expression factor = factors[random.Next(factors.Length)];
                    if (factor is MethodCallExpression && term.ToString().Contains("Sin")) continue;
                    term = Expression.Multiply(term, factor);
                }

                terms.Add(random.Next(3) == 0 ? Expression.Negate(term) : term);
            }

            Expression source = random.Next(2) == 0 ? LeftSum(terms) : BalancedSum(terms);
            if (random.Next(3) == 0) source = Expression.Subtract(source, terms[random.Next(terms.Count)]);
            Expression once = new ExpressionSimplifier().Simplify(source);
            AssertSameValues(source, once);
            Expression twice = new ExpressionSimplifier().Simplify(once);
            AssertSameValues(source, twice);
        }
    }

    /// <summary>
    /// The second pass of every result of the mission's examples is the result itself (§10): the factored products are
    /// canonical, so neither the binary factoring nor the multiplicative canonicalization rewrites them.
    /// </summary>
    [TestMethod]
    public void FactoredForms_AreFixedPoints()
    {
        Expression[] sources =
        [
            Expression.Add(Mul(A, X), Mul(K(3), X)),
            Expression.Add(Mul(Expression.Add(A, K(3)), X), Mul(K(2), X)),
            Expression.Add(Mul(K(2), A, X), Mul(K(3), B, X)),
            LeftSum([Mul(A, B), Mul(A, C), Mul(B, C)]),
            LeftSum([Pow(X, 2), Mul(K(2), X), Mul(A, X), X, K(15), A, Mul(K(3), Call(Cos, X)), Mul(K(5), Call(Sin, X))]),
            LeftSum([X, Y, X, Y]),
            LeftSum([Mul(K(2), X), Mul(K(2), Y), K(2)]),
        ];
        foreach (Expression source in sources)
        {
            Expression once = new ExpressionSimplifier().Simplify(source);
            Expression twice = new ExpressionSimplifier().Simplify(once);
            Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(once, twice), Text(source) + ": " + Text(once) + " then " + Text(twice));
            AssertSameValues(source, once);
        }
    }

    /// <summary>
    /// A long repeated chain is simplified at once (§11): one transform visit per node, where the spine chains of C3b
    /// finalized every level, and the result holds one term per distinct monomial.
    /// </summary>
    [TestMethod]
    public void RepeatedChains_AreLinear()
    {
        static long Visits(Expression tree)
        {
            long low = 0, high = 1;
            while (!Fits(tree, high)) { low = high; high *= 2; }
            while (high - low > 1)
            {
                long middle = (low + high) / 2;
                if (Fits(tree, middle)) high = middle;
                else low = middle;
            }

            return high;
        }

        static bool Fits(Expression tree, long visits)
        {
            try
            {
                new ExpressionSimplifier(ExpressionResourceLimits.Unlimited with { MaxTransformVisits = visits }).Simplify(tree);
                return true;
            }
            catch (ExpressionResourceLimitException)
            {
                return false;
            }
        }

        Expression Tail(int n) => Expression.Add(LeftSum(P.Take(n)), P[0]);
        long small = Visits(Tail(64)), large = Visits(Tail(512));
        Assert.IsTrue(large <= 9 * small, $"64 terms: {small} visits, 512 terms: {large}");
        Assert.IsTrue(large <= 2 * 513, $"about one visit per term: {large}");

        Expression repeated = LeftSum(Enumerable.Range(0, 300).Select(i => (Expression)P[i % 3]));
        Assert.AreEqual("(100 * (p0 + (p1 + p2)))", Text(new ExpressionSimplifier().Simplify(repeated)));
    }

    /// <summary>
    /// The admitted factorable root belongs to its operation, as the spine root of C3b: an exception after the admission leaves
    /// nothing behind, a nested public operation neither sees nor consumes it, and outside an operation nothing is admitted.
    /// </summary>
    [TestMethod]
    public void AdmittedFactorableRoot_BelongsToItsOperation()
    {
        Expression tree = Expression.Add(Mul(A, X), Mul(B, X));
        Expression other = Expression.Add(Mul(A, Y), Mul(B, Y));
        var simplifier = new ExpressionSimplifier();

        Assert.AreEqual(ExpressionRuleRejection.ChainNotAdmitted, simplifier.FlattenAdmission(tree));

        Assert.ThrowsExactly<InvalidOperationException>(() => ExpressionResourceBudget.Run<(ExpressionSimplifier simplifier, Expression tree), int>(ExpressionResourceLimits.Unlimited, simplifier, null, null, (simplifier, tree), static state =>
        {
            Assert.AreEqual(ExpressionRuleRejection.None, state.simplifier.FlattenAdmission(state.tree));
            Assert.AreSame(state.tree, ExpressionResourceBudget.ActiveSession!.AdmittedFactorableRoot);
            throw new InvalidOperationException("after the admission");
        }));
        Assert.IsNull(LeftOverRoot(simplifier));

        for (long visits = 1; visits < 5; visits++)
        {
            var limited = new ExpressionSimplifier(ExpressionResourceLimits.Default with { MaxTransformVisits = visits });
            Assert.ThrowsExactly<ExpressionResourceLimitException>(() => limited.Simplify(tree));
            Assert.IsNull(LeftOverRoot(limited), "after a refusal with " + visits + " visits");
        }

        ExpressionResourceBudget.Run(ExpressionResourceLimits.Unlimited, simplifier, null, null, (simplifier, tree, other), static state =>
        {
            Assert.AreEqual(ExpressionRuleRejection.None, state.simplifier.FlattenAdmission(state.tree));
            Expression? seen = ExpressionResourceBudget.Run(ExpressionResourceLimits.Unlimited, state.simplifier, null, null, state.other, static inner =>
                ExpressionResourceBudget.ActiveSession!.AdmittedFactorableRoot);
            Assert.IsNull(seen, "a nested operation does not see its caller's root");
            Assert.AreEqual("(y * (a + b))", Text(new ExpressionSimplifier().Simplify(state.other)), "a nested simplification uses its own root");
            Assert.AreSame(state.tree, ExpressionResourceBudget.ActiveSession!.AdmittedFactorableRoot, "the caller's root is given back");
            return 0;
        });
        Assert.IsNull(LeftOverRoot(simplifier));
        Assert.AreEqual("(x * (a + b))", Text(simplifier.Simplify(tree)));
    }

    /// <summary>The factorable root a new operation of <paramref name="simplifier"/> finds in its session when it starts.</summary>
    /// <param name="simplifier">The simplifier.</param>
    /// <returns>The root left in the session, or <see langword="null"/>.</returns>
    private static Expression? LeftOverRoot(ExpressionSimplifier simplifier)
        => ExpressionResourceBudget.Run(ExpressionResourceLimits.Unlimited, simplifier, null, null, 0, static _ => ExpressionResourceBudget.ActiveSession!.AdmittedFactorableRoot);

    /// <summary>
    /// A refused chain whose cause lies deep is walked by the factorable test only where the linear test walks it (the nodes
    /// above the cause are recorded as known refusals for both), and the sub-chain below it is admitted on its own.
    /// </summary>
    [TestMethod]
    public void DeepRefusal_IsWalkedOnce_AndTheSubChainIsAdmitted()
    {
        // p0 + ... + p19 + p0 (factorable: p0 repeats), then a division, then p20 + ... + p39 + p3 above it.
        Expression lower = Expression.Add(LeftSum(P.Take(20)), P[0]);
        Expression chain = LeftSum(P.Skip(20).Take(20).Cast<Expression>().Append(P[3]).Prepend(Expression.Add(lower, Expression.Divide(P[0], P[1]))));
        (Expression result, FlattenAdmissionStatistics statistics) = Measure(chain);
        // The comparisons of the levels simplify their operands in nested operations, which walk them again (as before C3c): the
        // factorable test never walks more than the linear one.
        Assert.IsTrue(statistics.FactorableWalks <= statistics.Walks, $"{statistics.FactorableWalks} factorable walks, {statistics.Walks} linear walks");
        Assert.AreEqual(1, statistics.Factorable, "the sub-chain below the division is admitted");
        Assert.IsTrue(Text(result).Contains("(2 * p0)") && Text(result).Contains("(2 * p3)"), Text(result));
    }
}
