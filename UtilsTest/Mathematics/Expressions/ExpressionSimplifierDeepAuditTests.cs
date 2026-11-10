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
/// S5 deep audit (2026-11-04, roadmap <c>Utils/TODO-2026-11-04-s5-simplifier-deep-audit.md</c>): characterization of the anomalies
/// the audit confirmed on <c>master</c> <c>af2dc928</c>. Every test pins the behavior observed on that reference, so it passes
/// today and documents the defect; the correcting stage (P18 and following) must change the assertion of its anomaly in the same
/// commit as the fix, exactly as <c>AUD03_RuleLoop_IsAControlledRefusal</c> became <c>AUD03_RuleLoop_Terminates</c>. The positive
/// controls and invariant checks must keep passing. The heavy campaigns live in <c>tests/ExpressionSimplifierAudit</c>.
/// </summary>
[TestClass]
[TestCategory("S5-Audit")]
public class ExpressionSimplifierDeepAuditTests
{
    /// <summary>The parameter <c>x</c>.</summary>
    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");

    /// <summary>The parameter <c>y</c>.</summary>
    private static readonly ParameterExpression Y = Expression.Parameter(typeof(double), "y");

    /// <summary>The parameter <c>z</c>.</summary>
    private static readonly ParameterExpression Z = Expression.Parameter(typeof(double), "z");

    /// <summary>The <see cref="double"/> methods used by the tests.</summary>
    private static readonly MethodInfo Sin = typeof(double).GetMethod(nameof(double.Sin), [typeof(double)])!, Cos = typeof(double).GetMethod(nameof(double.Cos), [typeof(double)])!;

    /// <summary>Builds a <see cref="double"/> constant.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The constant.</returns>
    private static ConstantExpression C(double value) => Expression.Constant(value);

    /// <summary>Simplifies with a new simplifier.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The simplified expression.</returns>
    private static Expression Simplify(Expression expression) => new ExpressionSimplifier().Simplify(expression);

    /// <summary>Formats an expression with the engine's safe formatter.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The text.</returns>
    private static string Text(Expression expression) => ExpressionTraceFormatter.Format(expression);

    /// <summary>Returns the innermost exception thrown by <paramref name="action"/>, or <see langword="null"/>.</summary>
    /// <param name="action">The action.</param>
    /// <returns>The innermost exception, or <see langword="null"/>.</returns>
    private static Exception? Innermost(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception exception)
        {
            while (exception is TargetInvocationException { InnerException: { } inner }) exception = inner;
            return exception;
        }
    }

    /// <summary>Counts the transformation visits (treatments entered) of one simplification.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The number of visits.</returns>
    private static long Visits(Expression expression)
    {
        var counter = new VisitCounter();
        using (ExpressionTransformTracing.Begin(counter))
        {
            new ExpressionSimplifier(ExpressionResourceLimits.Unlimited).Simplify(expression);
        }

        return counter.Visits;
    }

    /// <summary>Builds <c>acc = wrap(acc, x_i)</c> from <c>x0</c> over <paramref name="n"/> fresh parameters.</summary>
    /// <param name="n">The number of parameters.</param>
    /// <param name="wrap">The wrapping shape.</param>
    /// <returns>The nested tree.</returns>
    private static Expression Nest(int n, Func<Expression, Expression, Expression> wrap)
    {
        ParameterExpression[] xs = Enumerable.Range(0, n).Select(i => Expression.Parameter(typeof(double), "x" + i)).ToArray();
        Expression acc = xs[0];
        for (int i = 1; i < n; i++) acc = wrap(acc, xs[i]);
        return acc;
    }

    /// <summary>
    /// S5A-01: <c>AdditionOfCos2andSin2Number</c> dereferences the exponents of any two <see cref="double"/> powers as constants, so
    /// the sum of two powers with a symbolic exponent throws <see cref="NullReferenceException"/>, from <c>Simplify</c> and from
    /// the comparer, which simplifies first. The identity itself still works (positive control).
    /// </summary>
    /// <param name="shape">The shape.</param>
    [TestMethod]
    [DataRow("x^y + z^2")]
    [DataRow("x^2 + z^y")]
    [DataRow("sin(x)^y + cos(x)^2")]
    [DataRow("x^y + x^z")]
    public void S5A01_SumOfPowersWithSymbolicExponent_ThrowsNullReference(string shape)
    {
        Expression source = shape switch
        {
            "x^y + z^2" => Expression.Add(Expression.Power(X, Y), Expression.Power(Z, C(2))),
            "x^2 + z^y" => Expression.Add(Expression.Power(X, C(2)), Expression.Power(Z, Y)),
            "sin(x)^y + cos(x)^2" => Expression.Add(Expression.Power(Expression.Call(Sin, X), Y), Expression.Power(Expression.Call(Cos, X), C(2))),
            _ => Expression.Add(Expression.Power(X, Y), Expression.Power(X, Z)),
        };

        Assert.IsInstanceOfType<NullReferenceException>(Innermost(() => Simplify(source)));
        Assert.IsInstanceOfType<NullReferenceException>(Innermost(() => ExpressionComparer.Default.GetHashCode(source)));
        Assert.AreEqual("1", Text(Simplify(Expression.Add(Expression.Power(Expression.Call(Sin, X), C(2)), Expression.Power(Expression.Call(Cos, X), C(2))))));
    }

    /// <summary>
    /// S5A-02: the <c>*ConversionMath</c> rules rebuild a <see cref="double"/> call by looking the method up with an all-double
    /// signature of the same arity, so the <see cref="double.Round(double, int)"/>, <see cref="double.Round(double, MidpointRounding)"/>
    /// and <see cref="double.Round(double, int, MidpointRounding)"/> overloads throw <see cref="InvalidOperationException"/>. The
    /// one-argument overload and <see cref="Math.Round(double, int)"/> (not matched) are unaffected.
    /// </summary>
    [TestMethod]
    public void S5A02_DoubleRoundWithANonDoubleArgument_Throws()
    {
        Expression digits = Expression.Call(typeof(double).GetMethod(nameof(double.Round), [typeof(double), typeof(int)])!, X, Expression.Constant(2));
        Expression mode = Expression.Call(typeof(double).GetMethod(nameof(double.Round), [typeof(double), typeof(MidpointRounding)])!, X, Expression.Constant(MidpointRounding.AwayFromZero));
        Expression both = Expression.Call(typeof(double).GetMethod(nameof(double.Round), [typeof(double), typeof(int), typeof(MidpointRounding)])!, X, Expression.Constant(2), Expression.Constant(MidpointRounding.ToEven));

        foreach (Expression source in new[] { digits, mode, both })
        {
            Assert.IsInstanceOfType<InvalidOperationException>(Innermost(() => Simplify(source)), Text(source));
        }

        Assert.AreEqual("Double.Round(x)", Text(Simplify(Expression.Call(typeof(double).GetMethod(nameof(double.Round), [typeof(double)])!, X))));
        Assert.AreEqual("Math.Round(x, 2)", Text(Simplify(Expression.Call(typeof(Math).GetMethod(nameof(Math.Round), [typeof(double), typeof(int)])!, X, Expression.Constant(2)))));
    }

    /// <summary>
    /// S5A-03, fixed by P18b: <c>IsOrdinaryFieldDivision</c> accepted <see cref="decimal"/> (<c>Types.FloatingPointNumber</c> lists it),
    /// so <c>DivisionOfDivision</c> reassociated decimal divisions by a non-zero constant. A decimal quotient can underflow to zero
    /// or overflow, so the rewrite removed a <see cref="DivideByZeroException"/> the source raises (<c>1 / (b / 3) -&gt; 3 / b</c>),
    /// added one it does not raise (<c>(a / 0.5) / b -&gt; a / (0.5 * b)</c>) or added an <see cref="OverflowException"/>
    /// (<c>a / (b / 3) -&gt; (3 * a) / b</c>); ETC R1b (C-06) promises that such exceptions are kept. Field reassociation is now
    /// limited to <see cref="double"/> and <see cref="float"/>: the decimal sources are kept and raise the source's outcomes, and the
    /// floating-point rewrites are unchanged (control).
    /// </summary>
    [TestMethod]
    public void S5A03_DecimalNestedDivision_KeepsTheExceptions()
    {
        ParameterExpression a = Expression.Parameter(typeof(decimal), "a"), b = Expression.Parameter(typeof(decimal), "b");
        decimal tiny = 0.0000000000000000000000000001m;

        // 1 / (b / 3): with b = 1e-28 the source divides by b / 3 == 0.
        Expression removed = Expression.Divide(Expression.Constant(1m), Expression.Divide(b, Expression.Constant(3m)));
        AssertDecimalOutcomes(removed, a, b, "1 / (b / 3)", (0m, tiny), "DivideByZeroException", "DivideByZeroException");

        // (a / 0.5) / b: the source's value is kept (the old rewrite underflowed its divisor to zero).
        Expression addedDivide = Expression.Divide(Expression.Divide(a, Expression.Constant(0.5m)), b);
        AssertDecimalOutcomes(addedDivide, a, b, "(a / 0.5) / b", (1m, tiny), "20000000000000000000000000000", "20000000000000000000000000000");

        // a / (b / 3): the source's value is kept (the old rewrite overflowed its numerator).
        Expression addedOverflow = Expression.Divide(a, Expression.Divide(b, Expression.Constant(3m)));
        AssertDecimalOutcomes(addedOverflow, a, b, "a / (b / 3)", (30000000000000000000000000000m, 3m), "30000000000000000000000000000", "30000000000000000000000000000");

        // The zero identity declines decimal for the same exception (control).
        Assert.AreEqual("0m / b", Text(Simplify(Expression.Divide(Expression.Constant(0m), b))));

        // Binary floating-point field reassociation is unchanged (control).
        Assert.AreEqual("3 / y", Text(Simplify(Expression.Divide(C(1), Expression.Divide(Y, C(3))))));
        Assert.AreEqual("(x * 3) / y", Text(Simplify(Expression.Divide(X, Expression.Divide(Y, C(3))))).Replace("3 * x", "x * 3"));
        ParameterExpression f = Expression.Parameter(typeof(float), "f"), g = Expression.Parameter(typeof(float), "g");
        Assert.AreEqual("3f / g", Text(Simplify(Expression.Divide(Expression.Constant(1f), Expression.Divide(g, Expression.Constant(3f))))));
        Assert.AreNotEqual("f / (g / 3f)", Text(Simplify(Expression.Divide(f, Expression.Divide(g, Expression.Constant(3f))))));
    }

    /// <summary>
    /// S5A-13, fixed by P18b: <c>ExpressionEffectAnalysis.IsSafeDivisor</c> treated a decimal division by any non-zero constant as
    /// unable to throw, but a divisor of magnitude below 1 can overflow (<c>decimal.MaxValue / 0.5m</c>), so
    /// <c>(a / 0.5m) - (a / 0.5m)</c> became <c>0</c>, removing the <see cref="OverflowException"/> of the source. Such a division is
    /// now a throwing evaluation for the rule gate: the difference is kept and still overflows. (The behavior analysis classifies
    /// the <c>decimal.op_Division</c> method the node carries, which is partial whatever the divisor, so it was never affected.) A divisor of magnitude 1 or more cannot overflow, so <c>a / 2m</c> and <c>a / -1m</c> stay inert and
    /// their difference still cancels (control).
    /// </summary>
    [TestMethod]
    public void S5A13_DecimalDivisionByASmallConstant_IsNotInert()
    {
        ParameterExpression a = Expression.Parameter(typeof(decimal), "a"), b = Expression.Parameter(typeof(decimal), "b");
        Expression body = Expression.Subtract(Expression.Divide(a, Expression.Constant(0.5m)), Expression.Divide(a, Expression.Constant(0.5m)));
        AssertDecimalOutcomes(body, a, b, "(a / 0.5) - (a / 0.5)", (decimal.MaxValue, 0m), "OverflowException", "OverflowException");

        Expression negative = Expression.Subtract(Expression.Divide(a, Expression.Constant(-0.5m)), Expression.Divide(a, Expression.Constant(-0.5m)));
        AssertDecimalOutcomes(negative, a, b, "(a / -0.5) - (a / -0.5)", (decimal.MaxValue, 0m), "OverflowException", "OverflowException");

        foreach (decimal divisor in new[] { 2m, -1m, 1m, -7.5m })
        {
            Expression control = Expression.Subtract(Expression.Divide(a, Expression.Constant(divisor)), Expression.Divide(a, Expression.Constant(divisor)));
            AssertDecimalOutcomes(control, a, b, "0", (decimal.MaxValue, 0m), "0", "0");
        }

        // The predicate and the rule gate's classification of the division operator.
        foreach ((decimal divisor, bool safe) in new[] { (0m, false), (0.5m, false), (-0.5m, false), (0.9999999999999999999999999999m, false), (1m, true), (-1m, true), (2m, true), (decimal.MinValue, true) })
        {
            string label = divisor.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Assert.AreEqual(safe, ExpressionEffectAnalysis.IsSafeDivisor(divisor), label);
            ExpressionEffects effects = ExpressionEffectAnalysis.Analyze(Expression.Divide(a, Expression.Constant(divisor)));
            Assert.AreEqual(safe ? ExpressionEffects.ReadsMutableState : ExpressionEffects.ReadsMutableState | ExpressionEffects.MayThrow, effects, label);

            // The behavior analysis classifies the decimal operator method itself (partial whatever the divisor): unaffected.
            Assert.IsTrue(ExpressionBehaviorAnalysis.Analyze(Expression.Divide(a, Expression.Constant(divisor))).MayThrow, label);
        }
    }

    /// <summary>Checks the simplified form of a decimal body and the outcomes of the source and the simplified form at one point.</summary>
    /// <param name="body">The source body over <paramref name="a"/> and <paramref name="b"/>.</param>
    /// <param name="a">The first parameter.</param>
    /// <param name="b">The second parameter.</param>
    /// <param name="expectedForm">The expected simplified body.</param>
    /// <param name="point">The evaluation point.</param>
    /// <param name="expectedSource">The source outcome (value or exception type name).</param>
    /// <param name="expectedSimplified">The simplified outcome.</param>
    private static void AssertDecimalOutcomes(Expression body, ParameterExpression a, ParameterExpression b, string expectedForm, (decimal A, decimal B) point, string expectedSource, string expectedSimplified)
    {
        var source = Expression.Lambda<Func<decimal, decimal, decimal>>(body, a, b);
        var simplified = (Expression<Func<decimal, decimal, decimal>>)Simplify(source);
        Assert.AreEqual(expectedForm, Text(simplified.Body).Replace("m", ""));
        Assert.AreEqual(expectedSource, Outcome(source.Compile(), point));
        Assert.AreEqual(expectedSimplified, Outcome(simplified.Compile(), point));
    }

    /// <summary>Evaluates a decimal function and returns its value or its exception type name.</summary>
    /// <param name="function">The function.</param>
    /// <param name="point">The arguments.</param>
    /// <returns>The outcome.</returns>
    private static string Outcome(Func<decimal, decimal, decimal> function, (decimal A, decimal B) point)
    {
        try
        {
            return function(point.A, point.B).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception exception)
        {
            return exception.GetType().Name;
        }
    }

    /// <summary>
    /// S5A-04: constant folding and the zero-divisor check throw while simplifying, even when the node sits in a conditional branch
    /// that the source never evaluates for some inputs: the exception moves from evaluation time (conditional) to simplification
    /// time (unconditional), and the comparer, which simplifies first, throws too. The documented intent ("throws as evaluating
    /// the node would") holds for an unconditional node only.
    /// </summary>
    [TestMethod]
    public void S5A04_ExceptionOfAnUnevaluatedBranch_IsRaisedBySimplification()
    {
        ParameterExpression i = Expression.Parameter(typeof(int), "i");
        ParameterExpression m = Expression.Parameter(typeof(decimal), "m");
        (Expression Body, ParameterExpression Parameter, object Negative, Type Exception)[] cases =
        [
            (Expression.Condition(Expression.GreaterThan(i, Expression.Constant(0)), Expression.Divide(Expression.Constant(1), Expression.Constant(0)), Expression.Constant(2)), i, -1, typeof(DivideByZeroException)),
            (Expression.Condition(Expression.GreaterThan(i, Expression.Constant(0)), Expression.Divide(i, Expression.Constant(0)), Expression.Constant(2)), i, -1, typeof(DivideByZeroException)),
            (Expression.Condition(Expression.GreaterThan(i, Expression.Constant(0)), Expression.Divide(Expression.Constant(int.MinValue), Expression.Constant(-1)), Expression.Constant(2)), i, -1, typeof(OverflowException)),
            (Expression.Condition(Expression.GreaterThan(m, Expression.Constant(0m)), Expression.Multiply(Expression.Constant(decimal.MaxValue), Expression.Constant(2m)), Expression.Constant(0m)), m, -1m, typeof(OverflowException)),
        ];

        foreach ((Expression body, ParameterExpression parameter, object negative, Type exception) in cases)
        {
            LambdaExpression lambda = Expression.Lambda(body, parameter);
            Assert.IsNotNull(lambda.Compile().DynamicInvoke(negative), "the source evaluates without exception on the other branch");
            Assert.AreEqual(exception, Innermost(() => Simplify(lambda))?.GetType(), Text(body));
            Assert.AreEqual(exception, Innermost(() => ExpressionComparer.Default.Equals(lambda, Expression.Lambda(body, parameter)))?.GetType(), Text(body));
        }
    }

    /// <summary>
    /// S5A-05: rule priority and unfinalized rule results. <c>SubstractionWithAddition</c> (plan index 16) runs before
    /// <c>SubstractionOfEqualsElements</c> (20) and returns its reassociation without simplifying it, so <c>(x + y) - (x + y)</c> is
    /// not cancelled by one pass; the commutation of <c>Multiplication(e, t, c)</c> (22) runs before <c>MultiplicationWithNegate</c>
    /// (26) and the coefficient folding of the next pass. The second pass completes each of them, and the comparer, which
    /// simplifies once, does not identify the source with its second-pass result.
    /// </summary>
    [TestMethod]
    public void S5A05_RuleResultsLeftForTheNextPass()
    {
        (Expression Source, string First, string Second)[] cases =
        [
            (Expression.Subtract(Expression.Add(X, Y), Expression.Add(X, Y)), "((x + y) - x) - y", "0"),
            (Expression.Subtract(Expression.Subtract(C(3), Z), Expression.Subtract(C(3), Z)), "((3 + (0 - z)) - 3) - (0 - z)", "0"),
            (Expression.Multiply(Expression.Negate(X), C(2)), "2 * (-x)", "-(2 * x)"),
            (Expression.Multiply(Expression.Multiply(C(2), X), C(3)), "3 * (2 * x)", "6 * x"),
        ];

        foreach ((Expression source, string first, string second) in cases)
        {
            Expression once = Simplify(source);
            Assert.AreEqual(first, Text(once));
            Assert.AreEqual(second, Text(Simplify(once)));
        }

        Assert.IsFalse(ExpressionComparer.Default.Equals(cases[0].Source, C(0)));
        Assert.AreEqual("0", Text(Simplify(Expression.Subtract(X, X))), "control: a direct difference cancels");
    }

    /// <summary>
    /// S5A-06: repeated public passes oscillate, with bound parameters too (so not only because free parameters of one type tie in
    /// the S4 order): <c>(x - y) - z</c> alternates with <c>x + ((0 - y) + (0 - z))</c> (period 2) and <c>((1 - x) - y) - z</c> has
    /// period 3 (roadmap P10: the subtraction and negation rules return shapes only a later pass recanonicalizes). Consequence: the
    /// comparer does not identify a tree with its own simplified form.
    /// </summary>
    [TestMethod]
    public void S5A06_RepeatedPasses_Oscillate()
    {
        Expression twoCycle = Expression.Lambda<Func<double, double, double, double>>(Expression.Subtract(Expression.Subtract(X, Y), Z), X, Y, Z);
        List<string> forms = Passes(twoCycle, 4);
        CollectionAssert.AreEqual(new[] { "(x, y, z) => x + ((0 - y) + (0 - z))", "(x, y, z) => (x - y) - z", "(x, y, z) => x + ((0 - y) + (0 - z))", "(x, y, z) => (x - y) - z" }, forms);

        Expression threeCycle = Expression.Lambda<Func<double, double, double, double>>(Expression.Subtract(Expression.Subtract(Expression.Subtract(C(1), X), Y), Z), X, Y, Z);
        List<string> period3 = Passes(threeCycle, 4);
        Assert.AreNotEqual(period3[0], period3[1]);
        Assert.AreNotEqual(period3[0], period3[2]);
        Assert.AreEqual(period3[0], period3[3]);

        Assert.IsFalse(ExpressionComparer.Default.Equals(twoCycle, Simplify(twoCycle)), "Equals(e, Simplify(e)) is false on an oscillating form");
    }

    /// <summary>Applies <paramref name="count"/> public passes and returns the text of each result.</summary>
    /// <param name="expression">The source.</param>
    /// <param name="count">The number of passes.</param>
    /// <returns>The forms.</returns>
    private static List<string> Passes(Expression expression, int count)
    {
        var forms = new List<string>();
        for (int pass = 0; pass < count; pass++)
        {
            expression = Simplify(expression);
            forms.Add(Text(expression));
        }

        return forms;
    }

    /// <summary>
    /// S5A-07: <c>AdditionWithZero(e, t, 0)</c> and <c>PowerByZeroOrOne</c> (exponent 1) return <c>TransformCore(t)</c> on an
    /// operand that is already prepared, whereas <c>AdditionWithZero(e, 0, t)</c> returns <c>t</c>. The extra pass multiplies the
    /// work of nested shapes and, the simplifier not being idempotent, even changes the result: <c>t + 0</c> and <c>0 + t</c>
    /// simplify differently. The work ratio was more than three at twelve terms; since S5 P19d the comparison memo serves the
    /// repeated comparer simplifications of the extra pass (830 visits against 557), so only the excess itself is pinned.
    /// </summary>
    [TestMethod]
    public void S5A07_ZeroAndOneIdentities_RetransformAPreparedOperand()
    {
        long plusZero = Visits(Nest(12, (acc, v) => Expression.Add(Expression.Multiply(acc, v), C(0))));
        long zeroPlus = Visits(Nest(12, (acc, v) => Expression.Add(C(0), Expression.Multiply(acc, v))));
        long powerOne = Visits(Nest(12, (acc, v) => Expression.Power(Expression.Multiply(acc, v), C(1))));
        Assert.IsTrue(plusZero > zeroPlus * 5 / 4, $"t + 0: {plusZero} visits, 0 + t: {zeroPlus}");
        Assert.IsTrue(powerOne > zeroPlus * 5 / 4, $"t ^ 1: {powerOne} visits, 0 + t: {zeroPlus}");

        Expression difference = Expression.Subtract(Expression.Subtract(X, Y), Z);
        string right = Text(Simplify(Expression.Lambda<Func<double, double, double, double>>(Expression.Add(difference, C(0)), X, Y, Z)));
        string left = Text(Simplify(Expression.Lambda<Func<double, double, double, double>>(Expression.Add(C(0), difference), X, Y, Z)));
        Assert.AreEqual("(x, y, z) => (x - y) - z", right);
        Assert.AreEqual("(x, y, z) => x + ((0 - y) + (0 - z))", left);
    }

    /// <summary>
    /// S5A-08: nested negations of differences re-enter the engine on prepared operands at every level
    /// (<c>NegateWithSubstraction</c>, <c>SubstractionWithNegate</c>, <c>AdditionWithNegate</c>, comparer probes), and the work grows
    /// faster than any small polynomial: the default visit budget refuses the 70-node tree of n = 24. Measured by the audit campaign (isolated
    /// processes); the bounds below only pin the order of magnitude.
    /// </summary>
    [TestMethod]
    public void S5A08_NestedNegatedDifferences_GrowSuperPolynomially()
    {
        static Expression NegatedDifferences(int n) => Nest(n, (acc, v) => Expression.Negate(Expression.Subtract(acc, v)));
        long v8 = Visits(NegatedDifferences(8)), v16 = Visits(NegatedDifferences(16));
        Assert.IsTrue(v16 > 25 * v8, $"n = 8: {v8} visits, n = 16: {v16}");

        var limited = new ExpressionSimplifier(new ExpressionResourceLimits { MaxTransformVisits = 100_000 });
        var refusal = Innermost(() => limited.Simplify(NegatedDifferences(20))) as ExpressionResourceLimitException;
        Assert.IsNotNull(refusal, "a 58-node tree exceeds 100,000 visits");
        Assert.AreEqual(ExpressionResourceLimitKind.Work, refusal.Kind);
    }

    /// <summary>
    /// S5A-12: a left-associated difference of free parameters of one type, <c>x0 - x1 - ... - x(n-1)</c>, grows exponentially,
    /// whereas the same tree with its parameters bound by a lambda grows polynomially: free parameters tie in the S4 order. The
    /// default visit budget (1,000,000) refuses the free chain from sixteen terms (31 nodes); the test uses a smaller budget.
    /// </summary>
    [TestMethod]
    public void S5A12_DifferenceOfFreeParameters_GrowsExponentially()
    {
        ParameterExpression[] xs = Enumerable.Range(0, 12).Select(i => Expression.Parameter(typeof(double), "x" + i)).ToArray();
        Expression chain = xs.Aggregate<Expression>(Expression.Subtract);
        long free = Visits(chain), bound = Visits(Expression.Lambda(chain, xs));
        Assert.IsTrue(free > 5 * bound, $"free: {free} visits, bound: {bound}");

        ParameterExpression[] ys = Enumerable.Range(0, 14).Select(i => Expression.Parameter(typeof(double), "y" + i)).ToArray();
        var limited = new ExpressionSimplifier(new ExpressionResourceLimits { MaxTransformVisits = 100_000 });
        Expression freeChain = ys.Aggregate<Expression>(Expression.Subtract);
        var refusal = Innermost(() => limited.Simplify(freeChain)) as ExpressionResourceLimitException;
        Assert.IsNotNull(refusal, "x0 - ... - x13 (27 nodes) exceeds 100,000 visits");
        Assert.IsNotNull(limited.Simplify(Expression.Lambda(freeChain, ys)), "the bound chain stays within the same budget");
        Assert.AreEqual(ExpressionResourceLimitKind.Work, refusal.Kind);
    }

    /// <summary>
    /// S5A-14 (fixed by P19d): <c>AdditionOfEqualsElements</c> probes a constant other than 1 against the call below it, whose
    /// comparer simplification runs the same probe on every lower level. On <c>af8ce3f8</c> the work doubled with each level
    /// (589,817 visits at sixteen levels); the comparison memo now activates on calls and serves every later comparison of the
    /// operation (461 visits at sixteen, 1,685 at thirty-two). Nothing simplifies, so the result is the source.
    /// </summary>
    [TestMethod]
    public void S5A14_ConstantPlusCallNesting_GrowsPolynomially()
    {
        MethodInfo abs = typeof(Math).GetMethod(nameof(Math.Abs), [typeof(double)])!;
        static Expression ConstAbs(MethodInfo abs, int n)
        {
            Expression e = Expression.Call(Sin, X);
            for (int i = 0; i < n; i++) e = Expression.Add(C(2), Expression.Call(abs, e));
            return e;
        }

        long v16 = Visits(ConstAbs(abs, 16)), v32 = Visits(ConstAbs(abs, 32));
        Assert.IsTrue(v16 < 2_000, $"n = 16: {v16} visits");
        Assert.IsTrue(v32 < 5 * v16, $"n = 16: {v16} visits, n = 32: {v32}");
        Assert.AreEqual("2 + Math.Abs(2 + Math.Abs(Double.Sin(x)))", Text(Simplify(ConstAbs(abs, 2))));
        Assert.IsNotNull(Simplify(ConstAbs(abs, 64)), "the default limits accept sixty-four levels");
    }

    /// <summary>
    /// S5A-15 (fixed by P19d): at every level of a Horner chain the power rule compares <c>1 + y * (…)</c> with <c>y</c>, and the
    /// comparer simplification of the left base redoes every lower level. On <c>af8ce3f8</c> the work grew about as n^3.8 and the
    /// default structural budget refused sixty-four levels; the comparison memo now serves every later comparison of the
    /// operation (564 visits at sixteen levels, 2,148 at thirty-two, 8,388 at sixty-four).
    /// </summary>
    [TestMethod]
    public void S5A15_HornerChain_GrowsPolynomially()
    {
        static Expression Horner(int n)
        {
            Expression e = Expression.Multiply(X, Y);
            for (int i = 1; i < n; i++) e = Expression.Multiply(Expression.Add(e, C(1)), Y);
            return Expression.Add(e, C(1));
        }

        long v16 = Visits(Horner(16)), v32 = Visits(Horner(32));
        Assert.IsTrue(v16 < 2_000, $"n = 16: {v16} visits");
        Assert.IsTrue(v32 < 5 * v16, $"n = 16: {v16} visits, n = 32: {v32}");
        Assert.IsNotNull(Simplify(Horner(64)), "the default limits accept sixty-four levels");
    }

    /// <summary>
    /// S5A-09: one pass leaves simplifications that the next pass performs. The additive canonicalization groups equal terms
    /// without combining them (<c>x + (x + y)</c> gives <c>(x + x) + y</c>), and the power combination only sees direct operands
    /// (<c>x * (x * y)</c> is kept), so the comparer does not identify <c>x * (x * y)</c> with <c>y * x^2</c>.
    /// </summary>
    [TestMethod]
    public void S5A09_EqualTermsGroupedButNotCombined()
    {
        var sum = Expression.Lambda<Func<double, double, double>>(Expression.Add(X, Expression.Add(X, Y)), X, Y);
        Expression once = Simplify(sum);
        Assert.AreEqual("(x, y) => (x + x) + y", Text(once));
        Assert.AreNotEqual(Text(once), Text(Simplify(once)), "the second pass combines the grouped terms");

        var product = Expression.Lambda<Func<double, double, double>>(Expression.Multiply(X, Expression.Multiply(X, Y)), X, Y);
        var power = Expression.Lambda<Func<double, double, double>>(Expression.Multiply(Y, Expression.Power(X, C(2))), X, Y);
        Assert.AreEqual("(x, y) => x * (x * y)", Text(Simplify(product)));
        Assert.IsFalse(ExpressionComparer.Default.Equals(product, power));
    }

    /// <summary>S5A-10: no rule removes a double ordinary negation, even after several passes.</summary>
    [TestMethod]
    public void S5A10_DoubleNegation_IsNeverSimplified()
    {
        Expression source = Expression.Negate(Expression.Negate(X));
        Assert.AreEqual("-(-x)", Text(Simplify(Simplify(Simplify(source)))));
    }

    /// <summary>
    /// Documented limitation (S4): free parameters of one type tie in the canonical order, so a sum of three of them keeps an
    /// order that depends on the source and the comparer does not identify <c>(x + y) + z</c> with <c>(z + y) + x</c> (two terms are
    /// still identified), while the same terms bound by a lambda are.
    /// </summary>
    [TestMethod]
    public void Limitation_FreeParametersOfOneType_AreNotCommuted()
    {
        Assert.IsTrue(ExpressionComparer.Default.Equals(Expression.Add(X, Y), Expression.Add(Y, X)));
        Expression xyz = Expression.Add(Expression.Add(X, Y), Z), zyx = Expression.Add(Expression.Add(Z, Y), X);
        Assert.IsFalse(ExpressionComparer.Default.Equals(xyz, zyx));
        Assert.IsTrue(ExpressionComparer.Default.Equals(Expression.Lambda(xyz, X, Y, Z), Expression.Lambda(zyx, X, Y, Z)));
    }

    /// <summary>
    /// Invariants that held on every audited input (about 1.4 million comparer checks in the campaign): reflexivity on a rebuilt
    /// copy, symmetry, transitivity and hash consistency of the public comparer, on a deterministic sample of the forms the audit
    /// found delicate (oscillating forms, rule results, factored products, constants of several types).
    /// </summary>
    [TestMethod]
    public void ComparerInvariants_HoldOnDelicateForms()
    {
        Expression difference = Expression.Subtract(Expression.Subtract(X, Y), Z);
        Expression[] pool =
        [
            difference,
            Simplify(difference),
            Simplify(Simplify(difference)),
            Expression.Subtract(Expression.Add(X, Y), Expression.Add(X, Y)),
            C(0),
            Expression.Add(Expression.Multiply(C(2), X), Expression.Multiply(C(3), X)),
            Expression.Multiply(C(5), X),
            Expression.Multiply(X, Expression.Multiply(X, Y)),
            Expression.Constant(1),
            C(1),
            Expression.Lambda(difference, X, Y, Z),
            Expression.Lambda(Simplify(difference), X, Y, Z),
        ];

        ExpressionComparer comparer = ExpressionComparer.Default;
        foreach (Expression a in pool)
        {
            foreach (Expression b in pool)
            {
                bool equal = comparer.Equals(a, b);
                Assert.AreEqual(equal, comparer.Equals(b, a), $"symmetry {Text(a)} / {Text(b)}");
                if (equal) Assert.AreEqual(comparer.GetHashCode(a), comparer.GetHashCode(b), $"hash {Text(a)} / {Text(b)}");
                foreach (Expression c in pool)
                {
                    if (equal && comparer.Equals(b, c)) Assert.IsTrue(comparer.Equals(a, c), $"transitivity {Text(a)} / {Text(b)} / {Text(c)}");
                }
            }
        }

        Assert.IsTrue(comparer.Equals(Expression.Constant(1), C(1)), "documented exact cross-type numeric model");
    }

    /// <summary>
    /// Observable effects (ETC R1b, C-06) held on every audited input (8,000 seeded trees with counting calls, constant calls,
    /// getters with effects and throwing calls, 5,262 of them rewritten): the evaluation log, the value and the exception of the
    /// simplified tree are those of the source. This deterministic sample keeps the shapes that rewrite most (factoring,
    /// cancellation, zero product, canonical reordering).
    /// </summary>
    [TestMethod]
    public void ObservableEffects_ArePreservedByEveryRewrite()
    {
        Expression next = Expression.Call(typeof(EffectLog).GetMethod(nameof(EffectLog.Next))!);
        Expression spy = Expression.Property(null, typeof(EffectLog).GetProperty(nameof(EffectLog.Spy))!);
        Expression fail = Expression.Call(typeof(EffectLog).GetMethod(nameof(EffectLog.Fail))!);
        Expression[] bodies =
        [
            Expression.Add(Expression.Multiply(C(2), next), Expression.Multiply(C(-2), next)),
            Expression.Subtract(next, next),
            Expression.Multiply(C(0), Expression.Add(C(4), next)),
            Expression.Add(Expression.Add(spy, X), Expression.Add(next, Y)),
            Expression.Subtract(Expression.Subtract(next, spy), next),
            Expression.Multiply(Expression.Multiply(next, C(2)), Expression.Multiply(spy, C(3))),
            Expression.Add(Expression.Multiply(next, X), Expression.Multiply(spy, X)),
            Expression.Multiply(C(0), fail),
            Expression.Subtract(Expression.Add(fail, next), next),
            Expression.Divide(Expression.Divide(next, spy), Expression.Divide(next, C(2))),
        ];

        foreach (Expression body in bodies)
        {
            var source = Expression.Lambda<Func<double, double, double>>(body, X, Y);
            var simplified = (Expression<Func<double, double, double>>)Simplify(source);
            Assert.AreEqual(Run(source.Compile()), Run(simplified.Compile()), Text(body) + " -> " + Text(simplified));
        }
    }

    /// <summary>Evaluates a function at (3, 7) and returns its value or exception, followed by its effect log.</summary>
    /// <param name="function">The function.</param>
    /// <returns>The outcome and the log.</returns>
    private static string Run(Func<double, double, double> function)
    {
        EffectLog.Reset();
        string outcome;
        try
        {
            outcome = function(3, 7).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception exception)
        {
            outcome = exception.GetType().Name;
        }

        return outcome + " | " + string.Join(",", EffectLog.Log);
    }

    /// <summary>Counts the treatments entered during a traced operation.</summary>
    private sealed class VisitCounter : IExpressionTransformTracer
    {
        /// <summary>Gets the number of treatments entered.</summary>
        public long Visits { get; private set; }

        /// <inheritdoc/>
        public void OnEvent(in ExpressionTransformEvent traceEvent)
        {
            if (traceEvent.Kind == ExpressionTransformEventKind.TransformEnter) Visits++;
        }
    }
}

/// <summary>Effectful members for <see cref="ExpressionSimplifierDeepAuditTests.ObservableEffects_ArePreservedByEveryRewrite"/>: every evaluation is logged.</summary>
public static class EffectLog
{
    /// <summary>The log of the current evaluation.</summary>
    public static readonly List<string> Log = new();

    /// <summary>The counter returned by <see cref="Next"/>.</summary>
    private static int s_counter;

    /// <summary>Resets the log and the counter.</summary>
    public static void Reset()
    {
        Log.Clear();
        s_counter = 0;
    }

    /// <summary>Returns 1, 2, 3, ... and logs the call.</summary>
    /// <returns>The next value.</returns>
    public static double Next()
    {
        Log.Add("next");
        return ++s_counter;
    }

    /// <summary>Gets 3 and logs the read.</summary>
    public static double Spy
    {
        get
        {
            Log.Add("spy");
            return 3.0;
        }
    }

    /// <summary>Logs the call and throws.</summary>
    /// <returns>Never returns.</returns>
    public static double Fail()
    {
        Log.Add("fail");
        throw new InvalidOperationException("Fail");
    }
}
