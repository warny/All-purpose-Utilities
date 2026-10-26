using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Regression coverage for roadmap stage S5 P14 (first-pass factoring coefficient normalization), see
/// <c>Utils/TODO-2026-09-12-expression-simplifier-roadmap.md</c>. <c>AdditionOfEqualsElements</c> and
/// <c>SubstractionOfEqualsElements</c> returned <c>(a op b) * x</c> without simplifying the coefficient, so a sum
/// such as <c>2*x + 3*x + 4*x + 5*x</c> (the benchmark H family) gave <c>(2 + (3 + (4 + 5))) * x</c> after one
/// public call. A pair of constant coefficients is now folded with the typed constant folding of S5 P12, and a
/// folded <c>0</c>/<c>1</c>/<c>-1</c> coefficient goes through the existing zero/one product identity.
/// </summary>
/// <remarks>
/// Every assertion is made after ONE public <see cref="ExpressionSimplifier.Simplify(Expression)"/> call; a second
/// call is only used to check idempotence. Reference values come from compiling the source tree.
/// </remarks>
[TestClass]
public class ExpressionSimplifierFactoringCoefficientTests
{
    // ------------------------------------------------------------------------------------------
    // Shared helpers
    // ------------------------------------------------------------------------------------------

    /// <summary>Inputs the symbolic cases are evaluated on.</summary>
    private static readonly double[] Samples = [1.5, -2.0, 0.0, -0.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity];

    /// <summary>Counts the calls of <see cref="CountingIdentity"/>.</summary>
    [ThreadStatic]
    private static int _countingIdentityCalls;

    /// <summary>A method with an observable side effect: it counts its calls.</summary>
    /// <param name="value">The value returned unchanged.</param>
    /// <returns><paramref name="value"/>.</returns>
    public static double CountingIdentity(double value)
    {
        _countingIdentityCalls++;
        return value;
    }

    /// <summary>A user-defined addition, so a node built with it is never ordinary arithmetic.</summary>
    /// <param name="a">The left operand.</param>
    /// <param name="b">The right operand.</param>
    /// <returns>Always <c>42</c>.</returns>
    public static double CustomAdd(double a, double b) => 42.0;

    /// <summary>A user-defined multiplication, so a term built with it is never factored.</summary>
    /// <param name="a">The left operand.</param>
    /// <param name="b">The right operand.</param>
    /// <returns>Always <c>7</c>.</returns>
    public static double CustomMultiply(double a, double b) => 7.0;

    /// <summary>Gets a public static method of this class by name.</summary>
    /// <param name="name">The method name.</param>
    /// <returns>The method.</returns>
    private static MethodInfo Method(string name) => typeof(ExpressionSimplifierFactoringCoefficientTests).GetMethod(name)!;

    /// <summary>Simplifies <paramref name="expression"/> once with a fresh built-in simplifier.</summary>
    /// <param name="expression">The source expression.</param>
    /// <returns>The simplified expression.</returns>
    private static Expression Simplify(Expression expression) => new ExpressionSimplifier().Simplify(expression);

    /// <summary>Counts the nodes of an arithmetic tree (binary, unary and lambda nodes plus leaves).</summary>
    /// <param name="expression">The tree.</param>
    /// <returns>The node count.</returns>
    private static int CountNodes(Expression expression) => expression switch
    {
        BinaryExpression binary => 1 + CountNodes(binary.Left) + CountNodes(binary.Right),
        UnaryExpression unary => 1 + CountNodes(unary.Operand),
        LambdaExpression lambda => 1 + CountNodes(lambda.Body),
        MethodCallExpression call => 1 + call.Arguments.Sum(CountNodes),
        _ => 1,
    };

    /// <summary>Formats a result so that every <c>NaN</c> compares equal and signed zeros differ.</summary>
    /// <param name="value">The value.</param>
    /// <returns>A comparison key.</returns>
    private static string Key(double value) => double.IsNaN(value) ? "NaN" : BitConverter.DoubleToInt64Bits(value).ToString("X16");

    /// <summary>Builds <c>c * x</c> with a <see cref="double"/> constant coefficient.</summary>
    /// <param name="coefficient">The coefficient.</param>
    /// <param name="x">The factor.</param>
    /// <returns>The term.</returns>
    private static Expression Term(double coefficient, Expression x) => Expression.Multiply(Expression.Constant(coefficient), x);

    /// <summary>Builds <c>x * c</c> (factor first) with a <see cref="double"/> constant coefficient.</summary>
    /// <param name="x">The factor.</param>
    /// <param name="coefficient">The coefficient.</param>
    /// <returns>The term.</returns>
    private static Expression ReversedTerm(Expression x, double coefficient) => Expression.Multiply(x, Expression.Constant(coefficient));

    /// <summary>Builds the H benchmark family: <c>2*x + 3*x + ... + (n+1)*x</c>.</summary>
    /// <param name="x">The factor.</param>
    /// <param name="n">The number of terms.</param>
    /// <param name="leftAssociated">Whether to chain the sum to the left (the benchmark shape) or to the right.</param>
    /// <returns>The sum.</returns>
    private static Expression HFamily(ParameterExpression x, int n, bool leftAssociated)
    {
        Expression[] terms = Enumerable.Range(0, n).Select(i => Term(i + 2, x)).ToArray();
        if (leftAssociated) return terms.Aggregate(Expression.Add);
        Expression sum = terms[^1];
        for (int i = n - 2; i >= 0; i--) sum = Expression.Add(terms[i], sum);
        return sum;
    }

    /// <summary>
    /// Asserts that <paramref name="simplified"/> evaluates like <paramref name="source"/> on every sample, bit for
    /// bit (every <c>NaN</c> equal), for a single <see cref="double"/> parameter.
    /// </summary>
    /// <param name="x">The parameter.</param>
    /// <param name="source">The source expression.</param>
    /// <param name="simplified">The simplified expression.</param>
    /// <param name="samples">The inputs.</param>
    private static void AssertSameValues(ParameterExpression x, Expression source, Expression simplified, params double[] samples)
    {
        Func<double, double> expected = Expression.Lambda<Func<double, double>>(source, x).Compile();
        Func<double, double> actual = Expression.Lambda<Func<double, double>>(simplified, x).Compile();
        foreach (double sample in samples)
        {
            Assert.AreEqual(Key(expected(sample)), Key(actual(sample)), $"{source} => {simplified} differs at x = {sample}.");
        }
    }

    /// <summary>Asserts that <paramref name="expression"/> is <c>constant * factor</c> and returns the constant.</summary>
    /// <param name="expression">The simplified expression.</param>
    /// <param name="factor">The expected factor.</param>
    /// <returns>The coefficient constant.</returns>
    private static ConstantExpression AssertConstantTimes(Expression expression, Expression factor)
    {
        var product = expression as BinaryExpression ?? throw new AssertFailedException($"Expected a product, got {expression}.");
        Assert.AreEqual(ExpressionType.Multiply, product.NodeType, expression.ToString());
        Assert.AreSame(factor, product.Right, expression.ToString());
        return product.Left as ConstantExpression ?? throw new AssertFailedException($"The coefficient of {expression} is not a constant.");
    }

    // ------------------------------------------------------------------------------------------
    // Small equal-factor expressions
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Small sums of equal factors fold their coefficient in one call, whatever the operand order and association;
    /// before P14 each kept an unevaluated coefficient (<c>(2 + 3) * x</c>, <c>((2 + 3) + 4) * x</c>,
    /// <c>(2 + 1) * x</c>, ...).
    /// </summary>
    [TestMethod]
    public void Simplify_SmallEqualFactorSums_FoldCoefficientInOnePass()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        (Expression Source, double Coefficient)[] cases =
        [
            (Expression.Add(Term(2, x), Term(3, x)), 5),
            (Expression.Add(Expression.Add(Term(2, x), Term(3, x)), Term(4, x)), 9),
            (Expression.Add(Term(2, x), Expression.Add(Term(3, x), Term(4, x))), 9),
            (Expression.Add(Expression.Add(Expression.Add(Term(2, x), Term(3, x)), Term(4, x)), Term(5, x)), 14),
            (Expression.Add(ReversedTerm(x, 2), Term(3, x)), 5),
            (Expression.Add(Term(3, x), ReversedTerm(x, 2)), 5),
            (Expression.Add(ReversedTerm(x, 2), ReversedTerm(x, 3)), 5),
            (Expression.Add(Term(2, x), x), 3),
            (Expression.Add(x, Term(2, x)), 3),
            (Expression.Add(Term(2, x), Expression.Add(x, Term(3, x))), 6),
        ];
        foreach ((Expression source, double coefficient) in cases)
        {
            Expression simplified = Simplify(source);
            Assert.AreEqual(coefficient, (double)AssertConstantTimes(simplified, x).Value!, $"{source} => {simplified}");
            Assert.AreEqual(3, CountNodes(simplified), $"{source} => {simplified}");
            AssertSameValues(x, source, simplified, Samples);
            Assert.AreEqual(simplified.ToString(), Simplify(simplified).ToString(), $"Simplifying {source} again must not change it.");
        }
    }

    /// <summary>
    /// The H benchmark family folds to <c>S * x</c> in one call for n = 4, 8, 12 and 16, chained to the left (the
    /// benchmark shape) or to the right; before P14 it gave <c>((((2 + 3) + 4) + 5) ...) * x</c> with n - 1
    /// unevaluated additions (9, 17, 25 and 33 nodes instead of 3).
    /// </summary>
    /// <param name="n">The number of terms.</param>
    /// <param name="leftAssociated">The association of the source sum.</param>
    [TestMethod]
    [DataRow(4, true)]
    [DataRow(4, false)]
    [DataRow(8, true)]
    [DataRow(8, false)]
    [DataRow(12, true)]
    [DataRow(12, false)]
    [DataRow(16, true)]
    [DataRow(16, false)]
    public void Simplify_HFamily_FoldsCoefficientInOnePass(int n, bool leftAssociated)
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression source = HFamily(x, n, leftAssociated);
        Expression simplified = Simplify(source);
        double expected = Enumerable.Range(2, n).Sum();
        ConstantExpression coefficient = AssertConstantTimes(simplified, x);
        Assert.AreEqual(typeof(double), coefficient.Type);
        Assert.AreEqual(expected, (double)coefficient.Value!);
        Assert.AreEqual(3, CountNodes(simplified));
        AssertSameValues(x, source, simplified, Samples);
        Assert.AreEqual(simplified.ToString(), Simplify(simplified).ToString());
    }

    /// <summary>
    /// Differences and mixed signs fold too, and a folded coefficient of <c>1</c>, <c>-1</c> or <c>0</c> goes through
    /// the existing zero/one product identity: <c>2x - 3x</c> is <c>-x</c>, <c>2x + 3x - 4x</c> is <c>x</c>,
    /// <c>2x - x</c> is <c>x</c>, <c>2x + (-2)x</c> is <c>0</c> (the symbolic <c>x * 0 -&gt; 0</c> of the
    /// "Symbolic-equivalence contract", as a second call gave before).
    /// </summary>
    [TestMethod]
    public void Simplify_MixedSignCoefficients_FoldAndApplyZeroOneIdentity()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Assert.AreEqual("-x", Simplify(Expression.Subtract(Term(2, x), Term(3, x))).ToString());
        Assert.AreSame(x, Simplify(Expression.Subtract(Expression.Add(Term(2, x), Term(3, x)), Term(4, x))));
        Assert.AreSame(x, Simplify(Expression.Add(Term(2, x), Expression.Subtract(Term(3, x), Term(4, x)))));
        Assert.AreEqual(3.0, (double)AssertConstantTimes(Simplify(Expression.Add(Expression.Subtract(Term(2, x), Term(3, x)), Term(4, x))), x).Value!);
        Assert.AreSame(x, Simplify(Expression.Subtract(Term(2, x), Term(1, x))));
        Assert.AreEqual("-x", Simplify(Expression.Subtract(Term(1, x), Term(2, x))).ToString());
        Expression zero = Simplify(Expression.Add(Term(2, x), Term(-2, x)));
        Assert.AreEqual(0.0, (double)((ConstantExpression)zero).Value!);
        Assert.AreEqual("-x", Simplify(Expression.Add(Term(2, x), Term(-3, x))).ToString());
        Assert.AreEqual(-3.0, (double)AssertConstantTimes(Simplify(Expression.Add(Term(2, x), Term(-5, x))), x).Value!);
    }

    // ------------------------------------------------------------------------------------------
    // Numeric typing, overflow and exceptional values
    // ------------------------------------------------------------------------------------------

    /// <summary>Simplifies <c>a*x op b*x</c> on <typeparamref name="T"/> and returns the folded coefficient.</summary>
    /// <typeparam name="T">The operand type.</typeparam>
    /// <param name="a">The left coefficient.</param>
    /// <param name="b">The right coefficient.</param>
    /// <param name="subtract">Whether the terms are subtracted.</param>
    /// <returns>The coefficient constant of the result.</returns>
    private static ConstantExpression FoldedCoefficient<T>(T a, T b, bool subtract)
    {
        ParameterExpression x = Expression.Parameter(typeof(T), "x");
        Expression left = Expression.Multiply(Expression.Constant(a, typeof(T)), x);
        Expression right = Expression.Multiply(Expression.Constant(b, typeof(T)), x);
        Expression simplified = Simplify(subtract ? Expression.Subtract(left, right) : Expression.Add(left, right));
        ConstantExpression coefficient = AssertConstantTimes(simplified, x);
        Assert.AreEqual(typeof(T), coefficient.Type, simplified.ToString());
        Assert.AreEqual(typeof(T), simplified.Type);
        return coefficient;
    }

    /// <summary>
    /// The folded coefficient keeps the operand type and the unchecked semantics of the ordinary operation:
    /// <see cref="short"/>/<see cref="ushort"/> wrap in their own type (not <see cref="int"/>), <see cref="int"/>,
    /// <see cref="long"/>, <see cref="uint"/> wrap, <see cref="decimal"/> is exact, and <see cref="float"/> rounds
    /// in single precision.
    /// </summary>
    [TestMethod]
    public void Simplify_FoldedCoefficient_KeepsTypeAndOrdinarySemantics()
    {
        Assert.AreEqual((short)7, FoldedCoefficient<short>(3, 4, false).Value);
        Assert.AreEqual(short.MinValue, FoldedCoefficient<short>(short.MaxValue, 1, false).Value);
        Assert.AreEqual(ushort.MaxValue, FoldedCoefficient<ushort>(3, 4, true).Value);
        Assert.AreEqual(int.MinValue, FoldedCoefficient(int.MaxValue, 1, false).Value);
        Assert.AreEqual(int.MaxValue, FoldedCoefficient(int.MinValue, 1, true).Value);
        Assert.AreEqual(unchecked(long.MaxValue + 2), FoldedCoefficient(long.MaxValue, 2L, false).Value);
        Assert.AreEqual(uint.MaxValue, FoldedCoefficient(3u, 4u, true).Value);
        Assert.AreEqual(3.75m, FoldedCoefficient(1.5m, 2.25m, false).Value);
        Assert.AreEqual(0.1f + 0.2f, FoldedCoefficient(0.1f, 0.2f, false).Value);
        Assert.AreEqual(0.1 + 0.2, FoldedCoefficient(0.1, 0.2, false).Value);
        Assert.AreEqual(1e-323, FoldedCoefficient(double.Epsilon, double.Epsilon, false).Value, "Subnormal coefficients fold exactly.");
        Assert.AreEqual(double.PositiveInfinity, FoldedCoefficient(double.MaxValue, double.MaxValue, false).Value);
    }

    /// <summary>
    /// A wrapped <see cref="ulong"/> coefficient of <c>1</c> goes through the identity: <c>MaxValue*x + 2*x</c> is
    /// <c>x</c> (unchecked <c>MaxValue + 2 = 1</c>, as at runtime).
    /// </summary>
    [TestMethod]
    public void Simplify_WrappedCoefficientOfOne_ReturnsFactor()
    {
        ParameterExpression x = Expression.Parameter(typeof(ulong), "x");
        Expression source = Expression.Add(Expression.Multiply(Expression.Constant(ulong.MaxValue), x), Expression.Multiply(Expression.Constant(2ul), x));
        Assert.AreSame(x, Simplify(source));
        Assert.AreEqual(3ul, Expression.Lambda<Func<ulong, ulong>>(source, x).Compile()(3ul));
    }

    /// <summary>
    /// A decimal coefficient pair whose sum overflows is not folded and does not throw while simplifying: the source
    /// terms still evaluate for <c>x = 0</c>. The unevaluated coefficient is kept exactly as before P14.
    /// </summary>
    [TestMethod]
    public void Simplify_DecimalCoefficientOverflow_KeepsUnfoldedCoefficient()
    {
        ParameterExpression x = Expression.Parameter(typeof(decimal), "x");
        Expression source = Expression.Add(Expression.Multiply(Expression.Constant(decimal.MaxValue), x), Expression.Multiply(Expression.Constant(decimal.MaxValue), x));
        Assert.AreEqual(0m, Expression.Lambda<Func<decimal, decimal>>(source, x).Compile()(0m), "Precondition: the source evaluates for x = 0.");
        var product = (BinaryExpression)Simplify(source);
        var coefficient = (BinaryExpression)product.Left;
        Assert.AreEqual(ExpressionType.Add, coefficient.NodeType);
        Assert.AreSame(x, product.Right);

        Expression difference = Expression.Subtract(Expression.Multiply(Expression.Constant(decimal.MinValue), x), Expression.Multiply(Expression.Constant(decimal.MaxValue), x));
        Assert.AreEqual(ExpressionType.Subtract, ((BinaryExpression)((BinaryExpression)Simplify(difference)).Left).NodeType);
    }

    /// <summary>
    /// <c>NaN</c> and infinite coefficients fold with IEEE-754 semantics and evaluate like the source:
    /// <c>NaN*x + 2*x</c> and <c>inf*x + (-inf)*x</c> are <c>NaN</c>.
    /// </summary>
    [TestMethod]
    public void Simplify_NonFiniteCoefficients_FoldWithIeeeSemantics()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression nanSum = Expression.Add(Term(double.NaN, x), Term(2, x));
        Assert.IsTrue(double.IsNaN((double)((ConstantExpression)Simplify(nanSum)).Value!));
        Expression infinities = Expression.Add(Term(double.PositiveInfinity, x), Term(double.NegativeInfinity, x));
        Expression simplified = Simplify(infinities);
        Assert.IsTrue(double.IsNaN((double)((ConstantExpression)simplified).Value!));
        AssertSameValues(x, infinities, simplified, Samples);
    }

    /// <summary>
    /// Nested factoring with constant coefficients folds in the first pass, with free and bound lambda parameters:
    /// <c>(2*x + 3*x) + 5*z</c> gives <c>(x + z) * 5</c>; before P14 the inner factoring left <c>(2 + 3)</c>, which the
    /// outer probe simplified on a copy to recognize <c>5</c>, and the result kept <c>(x + z) * (2 + 3)</c>. The
    /// unfolded-coefficient comparison path itself is covered with an opaque coefficient by
    /// <c>ExpressionSimplifierComparerBatchingTests.EndToEnd_NestedAdditionOfEqualsElements_ProducesSecondPassFactoredShape</c>.
    /// </summary>
    [TestMethod]
    public void Simplify_NestedFactoringWithConstantCoefficients_FoldsInFirstPass()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression z = Expression.Parameter(typeof(double), "z");
        Expression body = Expression.Add(Expression.Add(Term(2, x), Term(3, x)), Term(5, z));

        void AssertShape(Expression result)
        {
            var product = (BinaryExpression)result;
            Assert.AreEqual(ExpressionType.Multiply, product.NodeType, result.ToString());
            var variables = (BinaryExpression)product.Left;
            Assert.AreSame(x, variables.Left);
            Assert.AreSame(z, variables.Right);
            Assert.AreEqual(5.0, (double)((ConstantExpression)product.Right).Value!);
        }

        AssertShape(Simplify(body));
        var lambda = (LambdaExpression)Simplify(Expression.Lambda(body, x, z));
        AssertShape(lambda.Body);
        Assert.AreSame(x, lambda.Parameters[0]);
        Assert.AreSame(z, lambda.Parameters[1]);
    }

    // ------------------------------------------------------------------------------------------
    // Opaque coefficients, calls, custom operators, scope and derived simplifiers
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Non-constant coefficients keep the existing factoring and are not simplified again (controls):
    /// <c>a*x + b*x -&gt; (a + b)*x</c>, <c>a*x + 3*x -&gt; (a + 3)*x</c>, <c>(a + 2)*x + 3*x -&gt; ((2 + a) + 3)*x</c>.
    /// </summary>
    [TestMethod]
    public void Simplify_OpaqueCoefficients_KeepExistingFactoring()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        ParameterExpression b = Expression.Parameter(typeof(double), "b");
        Assert.AreEqual("((a + b) * x)", Simplify(Expression.Add(Expression.Multiply(a, x), Expression.Multiply(b, x))).ToString());
        Assert.AreEqual("((a + 3) * x)", Simplify(Expression.Add(Expression.Multiply(a, x), Term(3, x))).ToString());
        Assert.AreEqual("(((2 + a) + 3) * x)", Simplify(Expression.Add(Expression.Multiply(Expression.Add(a, Expression.Constant(2.0)), x), Term(3, x))).ToString());
    }

    /// <summary>
    /// An unevaluated opaque coefficient still reaches the enclosing factoring and is recognized by its probe
    /// (control): <c>(a*x + 2*x) + (a + 2)*b</c> gives <c>(x + b) * (a + 2)</c>.
    /// </summary>
    [TestMethod]
    public void Simplify_UnfoldedOpaqueCoefficient_StillFactorsInEnclosingSum()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        ParameterExpression b = Expression.Parameter(typeof(double), "b");
        Expression source = Expression.Add(
            Expression.Add(Expression.Multiply(a, x), Term(2, x)),
            Expression.Multiply(Expression.Add(a, Expression.Constant(2.0)), b));
        Assert.AreEqual("((x + b) * (a + 2))", Simplify(source).ToString());
    }

    /// <summary>
    /// A call factor folds its coefficient without running user code while simplifying, and the simplified tree
    /// calls it once per evaluation instead of once per term.
    /// </summary>
    [TestMethod]
    public void Simplify_CallFactor_FoldsCoefficientWithoutRunningUserCode()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression call = Expression.Call(Method(nameof(CountingIdentity)), x);
        _countingIdentityCalls = 0;
        Expression simplified = Simplify(Expression.Add(Expression.Add(Term(2, call), Term(3, call)), Term(4, call)));
        Assert.AreEqual(0, _countingIdentityCalls, "Simplifying must not call user code.");
        Assert.AreEqual(9.0, (double)((ConstantExpression)((BinaryExpression)simplified).Left).Value!);
        Assert.AreEqual(18.0, Expression.Lambda<Func<double, double>>(simplified, x).Compile()(2.0));
        Assert.AreEqual(1, _countingIdentityCalls);
    }

    /// <summary>Custom additions and custom multiplications of terms are neither factored nor folded (controls).</summary>
    [TestMethod]
    public void Simplify_CustomOperators_AreUntouched()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        var customSum = (BinaryExpression)Simplify(Expression.Add(Term(2, x), Term(3, x), Method(nameof(CustomAdd))));
        Assert.AreEqual(Method(nameof(CustomAdd)), customSum.Method);
        Assert.AreEqual(42.0, Expression.Lambda<Func<double, double>>(customSum, x).Compile()(1.0));

        Expression customTerms = Simplify(Expression.Add(
            Expression.Multiply(Expression.Constant(2.0), x, Method(nameof(CustomMultiply))),
            Expression.Multiply(Expression.Constant(3.0), x, Method(nameof(CustomMultiply)))));
        Assert.AreEqual(14.0, Expression.Lambda<Func<double, double>>(customTerms, x).Compile()(1.0));
        Assert.AreEqual(7, CountNodes(customTerms));
    }

    /// <summary>
    /// Lambda bodies fold their coefficients in one call, and two distinct parameters that share a name are not
    /// merged (S4 scope).
    /// </summary>
    [TestMethod]
    public void Simplify_LambdaBodies_FoldAndKeepParameterIdentity()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        var lambda = (LambdaExpression)Simplify(Expression.Lambda(HFamily(x, 4, false), x));
        Assert.AreEqual(14.0, (double)AssertConstantTimes(lambda.Body, x).Value!);

        ParameterExpression other = Expression.Parameter(typeof(double), "x");
        var twoParameters = (LambdaExpression)Simplify(Expression.Lambda(Expression.Add(Term(2, x), Term(3, other)), x, other));
        Assert.AreEqual(7, CountNodes(twoParameters.Body), "Distinct parameters named alike must not be factored together.");
    }

    /// <summary>A derived simplifier with no overrides, used to check that subclasses get the same folding.</summary>
    private sealed class MinimalDerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>A derived simplifier folds factored coefficients exactly like the built-in one.</summary>
    [TestMethod]
    public void Simplify_DerivedSimplifier_FoldsCoefficients()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression simplified = new MinimalDerivedSimplifier().Simplify(HFamily(x, 8, true));
        Assert.AreEqual(44.0, (double)AssertConstantTimes(simplified, x).Value!);
    }
}
