using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// C3A-01 (compatibility entry C-09): <c>(-t) / u -&gt; -(t / u)</c> is declined for a signed integral type, like its mirror
/// <c>t / (-u) -&gt; -(t / u)</c> (C-06). For <c>t = MinValue</c> the negation wraps (<c>-MinValue = MinValue</c>), so the source
/// divides <c>MinValue</c> while the rewrite negates the quotient of <c>MinValue</c>: the two differ for every divisor except
/// <c>0</c> (both throw <see cref="DivideByZeroException"/>), <c>1</c> (both give <c>MinValue</c>) and <c>-1</c> (both throw
/// <see cref="OverflowException"/>). For <see cref="short"/> this holds for the interpreter, which wraps every operation; the
/// compiled delegate keeps the intermediate negation in 32 bits, so there the source and the rewrite agree. Every scenario
/// evaluates the source and the simplified tree, compiled and interpreted, and compares their outcomes over a grid of values,
/// under the relevant policies, for the exact and a derived simplifier.
/// </summary>
[TestClass]
[TestCategory("C3A-01")]
public class ExpressionSimplifierDivisionWithNegateTests
{
    /// <summary>A derived simplifier overriding nothing.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>The policies the rule runs under: the default, each axis relaxed alone, and every axis relaxed.</summary>
    private static readonly ExpressionSimplifierOptions[] Policies =
    [
        ExpressionSimplifierOptions.Default,
        new() { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations },
        new() { AggressiveSimplification = true },
        new() { ExceptionPreservation = ExpressionExceptionPreservation.Runtime | ExpressionExceptionPreservation.UserCode },
        new() { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations, AggressiveSimplification = true, ExceptionPreservation = ExpressionExceptionPreservation.None },
    ];

    /// <summary>The signed integral types an ordinary <see cref="ExpressionType.Divide"/> and <see cref="ExpressionType.Negate"/> exist for.</summary>
    private static readonly Type[] SignedIntegralTypes = [typeof(short), typeof(int), typeof(long)];

    /// <summary>The types the rewrite stays exact for.</summary>
    private static readonly Type[] ExactTypes = [typeof(float), typeof(double), typeof(decimal)];

    /// <summary>Builds the simplifiers under test for <paramref name="options"/>.</summary>
    /// <param name="options">The policy.</param>
    /// <returns>The exact and the derived simplifier.</returns>
    private static ExpressionSimplifier[] Simplifiers(ExpressionSimplifierOptions options)
        => [new ExpressionSimplifier { Options = options }, new DerivedSimplifier { Options = options }];

    /// <summary>Gets the dividends and divisors of <paramref name="type"/>: extremes, signs, zero, one, minus one and two.</summary>
    /// <param name="type">A signed integral type.</param>
    /// <returns>The values, boxed in <paramref name="type"/>.</returns>
    private static object[] SignedValues(Type type)
    {
        long min = Convert.ToInt64(type.GetField(nameof(int.MinValue))!.GetValue(null));
        long max = Convert.ToInt64(type.GetField(nameof(int.MaxValue))!.GetValue(null));
        long[] values = [min, min + 1, -7, -2, -1, 0, 1, 2, 3, 7, max - 1, max];
        return [.. values.Select(value => Convert.ChangeType(value, type))];
    }

    /// <summary>Gets the dividends and divisors of an exact type, including zero, the extremes and, for IEEE types, the specials.</summary>
    /// <param name="type"><see cref="float"/>, <see cref="double"/> or <see cref="decimal"/>.</param>
    /// <returns>The values, boxed in <paramref name="type"/>.</returns>
    private static object[] ExactValues(Type type) => type == typeof(decimal)
        ? [decimal.MinValue, -7.5m, -2m, -1m, -0.5m, 0m, 0.5m, 1m, 2m, 7.5m, decimal.MaxValue]
        : type == typeof(float)
            ? [float.MinValue, -7.5f, -2f, -1f, -0f, 0f, 1f, 2f, 7.5f, float.MaxValue, float.NaN, float.PositiveInfinity, float.NegativeInfinity]
            : [double.MinValue, -7.5, -2.0, -1.0, -0.0, 0.0, 1.0, 2.0, 7.5, double.MaxValue, double.NaN, double.PositiveInfinity, double.NegativeInfinity];

    /// <summary>The two evaluation modes: compiled (<see langword="false"/>) and interpreted (<see langword="true"/>).</summary>
    private static readonly bool[] EvaluationModes = [false, true];

    /// <summary>Compiles <paramref name="body"/> behind a lambda taking boxed arguments.</summary>
    /// <param name="body">The tree.</param>
    /// <param name="t">The dividend parameter.</param>
    /// <param name="u">The divisor parameter.</param>
    /// <param name="interpreted">Whether the delegate is interpreted rather than compiled.</param>
    /// <returns>The evaluator.</returns>
    private static Func<object, object, object> CompileBoxed(Expression body, ParameterExpression t, ParameterExpression u, bool interpreted)
    {
        ParameterExpression boxedT = Expression.Parameter(typeof(object), "bt");
        ParameterExpression boxedU = Expression.Parameter(typeof(object), "bu");
        Expression typed = Expression.Invoke(
            Expression.Lambda(body, t, u),
            Expression.Convert(boxedT, t.Type),
            Expression.Convert(boxedU, u.Type));
        return Expression.Lambda<Func<object, object, object>>(Expression.Convert(typed, typeof(object)), boxedT, boxedU).Compile(interpreted);
    }

    /// <summary>Evaluates <paramref name="evaluator"/> and renders its value or the type of its exception.</summary>
    /// <param name="evaluator">The evaluator.</param>
    /// <param name="t">The dividend.</param>
    /// <param name="u">The divisor.</param>
    /// <returns>The rendered outcome.</returns>
    private static string Outcome(Func<object, object, object> evaluator, object t, object u)
    {
        try
        {
            object value = evaluator(t, u);
            // Distinguishes -0.0 from 0.0; every NaN is one outcome, its sign and payload being outside the IEEE contract (S3).
            return value switch
            {
                double d => double.IsNaN(d) ? "NaN" : BitConverter.DoubleToInt64Bits(d).ToString(),
                float f => float.IsNaN(f) ? "NaN" : BitConverter.SingleToInt32Bits(f).ToString(),
                _ => value.ToString()!,
            };
        }
        catch (Exception exception)
        {
            return "throws " + exception.GetType().Name;
        }
    }

    /// <summary>
    /// Asserts that <paramref name="simplified"/> has the outcome of <paramref name="source"/> for every pair of values, compiled
    /// and interpreted.
    /// </summary>
    /// <param name="source">The source tree.</param>
    /// <param name="simplified">The simplified tree.</param>
    /// <param name="t">The dividend parameter.</param>
    /// <param name="u">The divisor parameter.</param>
    /// <param name="values">The values of both parameters.</param>
    /// <param name="label">The scenario label.</param>
    private static void AssertSameOutcomes(Expression source, Expression simplified, ParameterExpression t, ParameterExpression u, object[] values, string label)
    {
        foreach (bool interpreted in EvaluationModes)
        {
            Func<object, object, object> expected = CompileBoxed(source, t, u, interpreted);
            Func<object, object, object> actual = CompileBoxed(simplified, t, u, interpreted);
            foreach (object left in values)
            {
                foreach (object right in values)
                {
                    Assert.AreEqual(Outcome(expected, left, right), Outcome(actual, left, right), $"{label} (interpreted: {interpreted}): t = {left}, u = {right}");
                }
            }
        }
    }

    /// <summary>
    /// The historical rewrite is wrong: <c>-(t / u)</c> differs from <c>(-t) / u</c> at <c>t = MinValue</c> for every divisor but
    /// <c>0</c>, <c>1</c> and <c>-1</c>, for <see cref="int"/> and <see cref="long"/> in both evaluation modes and for
    /// <see cref="short"/> interpreted; everywhere else the two agree (compiled <see cref="short"/> agrees everywhere, its
    /// negation of <c>MinValue</c> staying in 32 bits). Pins the exact divergence domain the guard is for.
    /// </summary>
    [TestMethod]
    public void HistoricalRewrite_DivergesExactlyAtMinValueOutsideZeroAndUnitDivisors()
    {
        foreach (Type type in SignedIntegralTypes)
        {
            ParameterExpression t = Expression.Parameter(type, "t");
            ParameterExpression u = Expression.Parameter(type, "u");
            object minValue = type.GetField(nameof(int.MinValue))!.GetValue(null)!;
            foreach (bool interpreted in EvaluationModes)
            {
                Func<object, object, object> source = CompileBoxed(Expression.Divide(Expression.Negate(t), u), t, u, interpreted);
                Func<object, object, object> rewritten = CompileBoxed(Expression.Negate(Expression.Divide(t, u)), t, u, interpreted);
                bool wraps = interpreted || type != typeof(short);
                foreach (object left in SignedValues(type))
                {
                    foreach (object right in SignedValues(type))
                    {
                        long divisor = Convert.ToInt64(right);
                        bool diverges = wraps && left.Equals(minValue) && divisor is not (0 or 1 or -1);
                        Assert.AreEqual(diverges, Outcome(source, left, right) != Outcome(rewritten, left, right), $"{type.Name} (interpreted: {interpreted}): t = {left}, u = {right}");
                    }
                }
            }
        }

        // The example of the audit, in unchecked context.
        ParameterExpression ti = Expression.Parameter(typeof(int), "t");
        ParameterExpression ui = Expression.Parameter(typeof(int), "u");
        foreach (bool interpreted in EvaluationModes)
        {
            Assert.AreEqual("-1073741824", Outcome(CompileBoxed(Expression.Divide(Expression.Negate(ti), ui), ti, ui, interpreted), int.MinValue, 2));
            Assert.AreEqual("1073741824", Outcome(CompileBoxed(Expression.Negate(Expression.Divide(ti, ui)), ti, ui, interpreted), int.MinValue, 2));
        }
    }

    /// <summary>
    /// <c>(-t) / u</c> on a signed integral type is no longer rewritten as <c>-(t / u)</c>: the simplified tree is still a division
    /// of the negation, and it evaluates like the source (value or exception) for every pair of values, under every policy and for
    /// the exact and the derived simplifier. Before C3A-01, <c>t = MinValue, u = 2</c> gave <c>1073741824</c> instead of
    /// <c>-1073741824</c>.
    /// </summary>
    [TestMethod]
    public void NegatedDividend_SignedIntegral_IsKeptAndEvaluatesLikeTheSource()
    {
        foreach (Type type in SignedIntegralTypes)
        {
            ParameterExpression t = Expression.Parameter(type, "t");
            ParameterExpression u = Expression.Parameter(type, "u");
            Expression source = Expression.Divide(Expression.Negate(t), u);
            foreach (ExpressionSimplifierOptions policy in Policies)
            {
                foreach (ExpressionSimplifier simplifier in Simplifiers(policy))
                {
                    string label = $"{type.Name} {simplifier.GetType().Name} under {policy}";
                    Expression simplified = simplifier.Simplify(source);
                    BinaryExpression division = (BinaryExpression)simplified;
                    Assert.AreEqual(ExpressionType.Divide, division.NodeType, label);
                    Assert.AreEqual(ExpressionType.Negate, division.Left.NodeType, label);
                    Assert.AreSame(t, ((UnaryExpression)division.Left).Operand, label);
                    Assert.AreSame(u, division.Right, label);
                    AssertSameOutcomes(source, simplified, t, u, SignedValues(type), label);
                }
            }
        }
    }

    /// <summary>
    /// The same holds behind a constant divisor (<c>2</c>, <c>-2</c>, <c>MinValue</c>, where the source diverged) and for a dividend
    /// that is not a parameter; the identities of the divisor <c>1</c> (<c>(-t) / 1 -&gt; -t</c>) are not affected, the divisor
    /// <c>-1</c> keeps its exception, and the constant divisor <c>0</c> still throws <see cref="DivideByZeroException"/> while
    /// simplifying, as it did before (<c>DivideWithZeroOrOne</c>, S5A-04).
    /// </summary>
    [TestMethod]
    public void NegatedDividend_SignedIntegral_ConstantDivisorsEvaluateLikeTheSource()
    {
        foreach (Type type in SignedIntegralTypes)
        {
            ParameterExpression t = Expression.Parameter(type, "t");
            ParameterExpression u = Expression.Parameter(type, "u");
            object minValue = type.GetField(nameof(int.MinValue))!.GetValue(null)!;
            object[] divisors = [Convert.ChangeType(2, type), Convert.ChangeType(-2, type), Convert.ChangeType(3, type), minValue,
                Convert.ChangeType(1, type), Convert.ChangeType(-1, type), Convert.ChangeType(0, type)];
            foreach (object divisor in divisors)
            {
                // u is unused by these sources; it keeps the evaluator signature.
                Expression[] sources =
                [
                    Expression.Divide(Expression.Negate(t), Expression.Constant(divisor, type)),
                    Expression.Divide(Expression.Negate(Expression.Add(t, u)), Expression.Constant(divisor, type)),
                ];
                foreach (Expression source in sources)
                {
                    foreach (ExpressionSimplifierOptions policy in Policies)
                    {
                        foreach (ExpressionSimplifier simplifier in Simplifiers(policy))
                        {
                            string label = $"{type.Name} {source} {simplifier.GetType().Name} under {policy}";
                            if (Convert.ToInt64(divisor) == 0)
                            {
                                // The rule throws from its reflective invocation.
                                Exception thrown = Assert.Throws<Exception>(() => simplifier.Simplify(source), label);
                                while (thrown is TargetInvocationException { InnerException: not null } wrapper) thrown = wrapper.InnerException;
                                Assert.IsInstanceOfType<DivideByZeroException>(thrown, label);
                                continue;
                            }
                            AssertSameOutcomes(source, simplifier.Simplify(source), t, u, SignedValues(type), label);
                        }
                    }
                }
            }

            // (-t) / 1 is still the negation alone.
            Expression byOne = new ExpressionSimplifier().Simplify(Expression.Divide(Expression.Negate(t), Expression.Constant(Convert.ChangeType(1, type), type)));
            Assert.AreEqual(ExpressionType.Negate, byOne.NodeType, type.Name);
        }
    }

    /// <summary>
    /// The two overloads now agree: <c>t / (-u)</c> (guarded since C-06) and <c>(-t) / u</c> are both kept for a signed integral
    /// type and both rewritten as <c>-(t / u)</c> for <see cref="float"/>, <see cref="double"/> and <see cref="decimal"/>.
    /// </summary>
    [TestMethod]
    public void BothOverloads_AgreeOnEveryType()
    {
        foreach (Type type in SignedIntegralTypes.Concat(ExactTypes))
        {
            bool rewritten = ExactTypes.Contains(type);
            ParameterExpression t = Expression.Parameter(type, "t");
            ParameterExpression u = Expression.Parameter(type, "u");
            foreach (Expression source in new Expression[] { Expression.Divide(Expression.Negate(t), u), Expression.Divide(t, Expression.Negate(u)) })
            {
                foreach (ExpressionSimplifierOptions policy in Policies)
                {
                    Expression simplified = new ExpressionSimplifier { Options = policy }.Simplify(source);
                    Assert.AreEqual(rewritten ? ExpressionType.Negate : ExpressionType.Divide, simplified.NodeType, $"{type.Name} {source} under {policy}");
                }
            }
        }
    }

    /// <summary>
    /// The rewrite is kept for <see cref="float"/>, <see cref="double"/> and <see cref="decimal"/>, where it is exact: the result is
    /// <c>-(t / u)</c> with the source operands, and evaluates like the source (signed zeros, infinities, <c>NaN</c>, decimal
    /// overflow and division by zero) under every policy, for the exact and the derived simplifier.
    /// </summary>
    [TestMethod]
    public void NegatedDividend_ExactTypes_IsStillRewritten()
    {
        foreach (Type type in ExactTypes)
        {
            ParameterExpression t = Expression.Parameter(type, "t");
            ParameterExpression u = Expression.Parameter(type, "u");
            Expression source = Expression.Divide(Expression.Negate(t), u);
            foreach (ExpressionSimplifierOptions policy in Policies)
            {
                foreach (ExpressionSimplifier simplifier in Simplifiers(policy))
                {
                    string label = $"{type.Name} {simplifier.GetType().Name} under {policy}";
                    Expression simplified = simplifier.Simplify(source);
                    UnaryExpression negation = (UnaryExpression)simplified;
                    Assert.AreEqual(ExpressionType.Negate, negation.NodeType, label);
                    BinaryExpression division = (BinaryExpression)negation.Operand;
                    Assert.AreEqual(ExpressionType.Divide, division.NodeType, label);
                    Assert.AreSame(t, division.Left, label);
                    Assert.AreSame(u, division.Right, label);
                    AssertSameOutcomes(source, simplified, t, u, ExactValues(type), label);
                }
            }
        }
    }

    /// <summary>
    /// The neighboring negation rules of signed integral types are unchanged: <c>(-t) * u -&gt; -(t * u)</c> (modular, exact),
    /// <c>t / 1 -&gt; t</c>, and <c>t / -1</c> is kept for its overflow.
    /// </summary>
    [TestMethod]
    public void NeighboringRules_SignedIntegral_AreUnchanged()
    {
        ParameterExpression t = Expression.Parameter(typeof(int), "t");
        ParameterExpression u = Expression.Parameter(typeof(int), "u");
        var simplifier = new ExpressionSimplifier();

        Expression product = simplifier.Simplify(Expression.Multiply(Expression.Negate(t), u));
        Assert.AreEqual(ExpressionType.Negate, product.NodeType);
        Assert.AreEqual(ExpressionType.Multiply, ((UnaryExpression)product).Operand.NodeType);
        AssertSameOutcomes(Expression.Multiply(Expression.Negate(t), u), product, t, u, SignedValues(typeof(int)), "(-t) * u");

        Assert.AreSame(t, simplifier.Simplify(Expression.Divide(t, Expression.Constant(1))));
        Assert.AreEqual(ExpressionType.Divide, simplifier.Simplify(Expression.Divide(t, Expression.Constant(-1))).NodeType);
    }

    /// <summary>
    /// <see cref="ExpressionComparer.Default"/>, which simplifies first, no longer equates <c>(-t) / u</c> with <c>-(t / u)</c> for
    /// a signed integral type (they differ at <c>t = MinValue</c>), and still does for <see cref="float"/>, <see cref="double"/>
    /// and <see cref="decimal"/>.
    /// </summary>
    [TestMethod]
    public void Comparer_EquatesTheTwoFormsOnlyWhereTheyAreEqual()
    {
        foreach (Type type in SignedIntegralTypes.Concat(ExactTypes))
        {
            ParameterExpression t = Expression.Parameter(type, "t");
            ParameterExpression u = Expression.Parameter(type, "u");
            bool equal = ExpressionComparer.Default.Equals(Expression.Divide(Expression.Negate(t), u), Expression.Negate(Expression.Divide(t, u)));
            Assert.AreEqual(ExactTypes.Contains(type), equal, type.Name);
        }
    }

    /// <summary>
    /// <see cref="sbyte"/> is listed by the guard but has no ordinary division nor negation node: the factories refuse it, so the
    /// rule cannot meet it.
    /// </summary>
    [TestMethod]
    public void SByte_HasNoOrdinaryDivisionOrNegation()
    {
        ParameterExpression s = Expression.Parameter(typeof(sbyte), "s");
        Assert.ThrowsExactly<InvalidOperationException>(() => Expression.Divide(s, s));
        Assert.ThrowsExactly<InvalidOperationException>(() => Expression.Negate(s));
    }
}
