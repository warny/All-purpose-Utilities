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
/// S5A-10: an ordinary negation of an ordinary negation is its operand, in one call. The finalization of a <c>Negate</c> node and
/// every rule that builds a negation share one sign model (<c>ExpressionSimplifier.BuildNegation</c>), the one the additive
/// canonicalization already applied to the terms of a sum (<c>docs/expressions/SimplifierFlatten.md</c>, "Double negations").
/// </summary>
/// <remarks>
/// Each case checks the exact form, that the first result is a structural fixed point under every policy (raw structural
/// equality, never a hash), that the type is kept and that the compiled source and result agree bit for bit on the special values
/// of the type. Custom, lifted and checked negations, conversions and opaque nodes are never removed.
/// </remarks>
[TestClass]
public class ExpressionSimplifierDoubleNegationTests
{
    /// <summary>A derived simplifier without any member.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>A value type with a user-defined negation that records its calls.</summary>
    private readonly struct Signed
    {
        /// <summary>The number of calls of the user negation, on the current thread.</summary>
        [ThreadStatic]
        public static int Negations;

        /// <summary>Initializes a value.</summary>
        /// <param name="value">The value.</param>
        public Signed(int value) => Value = value;

        /// <summary>Gets the value.</summary>
        public int Value { get; }

        /// <summary>The user negation: doubles too, so that a removed pair is visible in the value (it is no involution).</summary>
        /// <param name="operand">The operand.</param>
        /// <returns><c>-2 * operand</c>.</returns>
        public static Signed operator -(Signed operand)
        {
            Negations++;
            return new Signed(-2 * operand.Value);
        }
    }

    /// <summary>The state read and written by the effectful calls of the tests (per thread: MSTest may run tests in parallel).</summary>
    private static class Probe
    {
        /// <summary>The ordered log of the calls.</summary>
        [ThreadStatic]
        public static List<string>? Log;

        /// <summary>A mutable value read by <see cref="Read"/>.</summary>
        [ThreadStatic]
        public static double State;

        /// <summary>Logs its call and returns 3.</summary>
        /// <returns>3.</returns>
        public static double Count()
        {
            (Log ??= []).Add("count");
            return 3;
        }

        /// <summary>Logs its call and throws.</summary>
        /// <returns>Never returns.</returns>
        public static double Throw()
        {
            (Log ??= []).Add("throw");
            throw new InvalidOperationException("probe");
        }

        /// <summary>Logs its call and returns the mutable state.</summary>
        /// <returns><see cref="State"/>.</returns>
        public static double Read()
        {
            (Log ??= []).Add("read");
            return State;
        }

        /// <summary>Logs its call, increments the state and returns it.</summary>
        /// <returns>The new <see cref="State"/>.</returns>
        public static double Write()
        {
            (Log ??= []).Add("write");
            return ++State;
        }

        /// <summary>Logs its call and returns a new boxed value (an allocation).</summary>
        /// <returns>A new box of 2.</returns>
        public static object Allocate()
        {
            (Log ??= []).Add("allocate");
            return 2.0;
        }

        /// <summary>A method declared deterministic that logs its call.</summary>
        /// <param name="value">The argument.</param>
        /// <returns><paramref name="value"/> plus one.</returns>
        [Deterministic]
        public static double Declared(double value)
        {
            (Log ??= []).Add("declared");
            return value + 1;
        }
    }

    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");
    private static readonly ParameterExpression Y = Expression.Parameter(typeof(double), "y");

    /// <summary>The policies of S5 P18 Architecture C the results are checked under.</summary>
    private static readonly ExpressionSimplifierOptions[] Policies =
    [
        ExpressionSimplifierOptions.Default,
        new() { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations },
        new() { AggressiveSimplification = true },
        new() { ExceptionPreservation = ExpressionExceptionPreservation.Arithmetic | ExpressionExceptionPreservation.Runtime },
        new() { ExceptionPreservation = ExpressionExceptionPreservation.None },
        new() { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations, AggressiveSimplification = true, ExceptionPreservation = ExpressionExceptionPreservation.None },
    ];

    /// <summary>Simplifies with a fresh built-in simplifier of <paramref name="options"/>.</summary>
    /// <param name="expression">The expression.</param>
    /// <param name="options">The policy, the default when <see langword="null"/>.</param>
    /// <returns>The simplified expression.</returns>
    private static Expression S(Expression expression, ExpressionSimplifierOptions? options = null)
        => new ExpressionSimplifier { Options = options ?? ExpressionSimplifierOptions.Default }.Simplify(expression);

    /// <summary>Formats an expression with the engine's safe formatter.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The text.</returns>
    private static string Text(Expression expression) => ExpressionTraceFormatter.Format(expression);

    /// <summary>Builds <paramref name="levels"/> ordinary negations of <paramref name="operand"/>.</summary>
    /// <param name="operand">The innermost operand.</param>
    /// <param name="levels">The number of negations.</param>
    /// <returns>The tower.</returns>
    private static Expression Negations(Expression operand, int levels)
    {
        for (int i = 0; i < levels; i++) operand = Expression.Negate(operand);
        return operand;
    }

    /// <summary>Asserts that <paramref name="source"/> simplifies to <paramref name="expected"/> in one call under every policy.</summary>
    /// <param name="source">The source tree.</param>
    /// <param name="expected">The expected text of the result.</param>
    private static void AssertSimplified(Expression source, string expected)
    {
        foreach (ExpressionSimplifierOptions options in Policies)
        {
            Expression once = S(source, options);
            Assert.AreEqual(expected, Text(once), Text(source) + " under " + options);
            Assert.AreEqual(source.Type, once.Type);
            Expression twice = S(once, options);
            Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(once, twice), $"Not a fixed point: {Text(once)} -> {Text(twice)}");
        }
    }

    /// <summary>The cases of the brief, in one call and under every policy (their operands are inert).</summary>
    [TestMethod]
    public void RequiredCases_AreSimplifiedInOneCall()
    {
        AssertSimplified(Negations(X, 2), "x");
        AssertSimplified(Negations(X, 3), "-x");
        AssertSimplified(Negations(X, 4), "x");
        AssertSimplified(Expression.Add(X, Negations(Y, 2)), "x + y");
        AssertSimplified(Expression.Subtract(X, Negations(Y, 2)), "(0 - y) + x");
        AssertSimplified(Negations(Expression.Add(X, Y), 2), "x + y");
        AssertSimplified(Negations(Expression.Subtract(X, Y), 2), "(0 - y) + x");
        AssertSimplified(Expression.Add(X, Expression.Subtract(Expression.Constant(0.0), Expression.Subtract(Expression.Constant(0.0), Y))), "x + y");
        AssertSimplified(Expression.Subtract(Expression.Subtract(Expression.Constant(0.0), X), Expression.Subtract(Expression.Constant(0.0), Y)), "(0 - x) + y");
    }

    /// <summary>The double negations a rule builds (<c>x * -1</c>, <c>(-a) * b</c>, <c>(-a) / b</c>) are removed by the same call.</summary>
    [TestMethod]
    public void RuleResults_HoldNoDoubleNegation()
    {
        ConstantExpression minusOne = Expression.Constant(-1.0);
        AssertSimplified(Expression.Multiply(Expression.Negate(X), Expression.Negate(Y)), "x * y");
        AssertSimplified(Expression.Divide(Expression.Negate(X), Expression.Negate(Y)), "x / y");
        AssertSimplified(Expression.Multiply(Expression.Multiply(X, minusOne), minusOne), "x");
        AssertSimplified(Expression.Multiply(Expression.Subtract(Expression.Constant(0.0), X), minusOne), "x");
        AssertSimplified(Expression.Divide(Expression.Negate(X), minusOne), "x");
        AssertSimplified(Negations(Expression.Divide(X, Y), 2), "x / y");
        AssertSimplified(Negations(Expression.Multiply(X, Expression.Constant(2.0)), 2), "2 * x");
    }

    /// <summary>Associations, constants, calls, divisions and non-finite constants around double negations.</summary>
    [TestMethod]
    public void Variants_AreSimplifiedInOneCall()
    {
        MethodInfo sin = typeof(double).GetMethod(nameof(double.Sin), [typeof(double)])!;
        AssertSimplified(Expression.Add(Negations(X, 2), Y), "x + y");
        AssertSimplified(Expression.Add(Expression.Add(Negations(X, 2), Negations(Y, 2)), Expression.Constant(1.0)), "1 + (x + y)");
        AssertSimplified(Expression.Add(Negations(X, 2), Expression.Add(Negations(Y, 2), Expression.Constant(1.0))), "1 + (x + y)");
        AssertSimplified(Negations(Expression.Call(sin, X), 2), "Double.Sin(x)");
        AssertSimplified(Expression.Call(sin, Negations(X, 2)), "Double.Sin(x)");
        AssertSimplified(Negations(Expression.Constant(2.0), 2), "2");
        AssertSimplified(Expression.Add(Negations(X, 2), Expression.Constant(double.NaN)), "NaN");
        AssertSimplified(Expression.Multiply(Negations(X, 2), Expression.Constant(double.PositiveInfinity)), "Infinity * x");
    }

    /// <summary>The cost of a tower of negations is linear: one visit per node, nothing simplified twice.</summary>
    [TestMethod]
    public void NegationTower_CostsOneVisitPerNode()
    {
        foreach (int levels in new[] { 2, 8, 32, 128 })
        {
            Expression source = Negations(X, levels);
            var collector = new ExpressionTraceCollector(100_000);
            Expression result;
            using (ExpressionTransformTracing.Begin(collector)) result = S(source);
            Assert.AreEqual(levels % 2 == 0 ? "x" : "-x", Text(result));
            int visits = collector.GetEvents().Count(e => e.Kind == ExpressionTransformEventKind.TransformEnter);
            Assert.AreEqual(levels + 1, visits, "levels " + levels);
        }
    }

    /// <summary>A shared double negation is simplified like two copies, and the result does not grow.</summary>
    [TestMethod]
    public void SharedDoubleNegation_IsSimplified()
    {
        Expression shared = Negations(X, 2);
        AssertSimplified(Expression.Add(shared, shared), "2 * x");
        AssertSimplified(Expression.Multiply(shared, shared), "x ** 2");
        Expression dag = X;
        for (int i = 0; i < 12; i++)
        {
            Expression level = Negations(dag, 2);
            dag = Expression.Subtract(level, level);
        }

        AssertSimplified(dag, "0");
    }

    /// <summary>A bound parameter keeps its identity and its lambda.</summary>
    [TestMethod]
    public void BoundParameter_KeepsItsIdentity()
    {
        var lambda = Expression.Lambda<Func<double, double, double>>(Expression.Add(Negations(X, 2), Negations(Y, 4)), X, Y);
        var result = (LambdaExpression)S(lambda);
        Assert.AreEqual("(x, y) => x + y", Text(result));
        var sum = (BinaryExpression)result.Body;
        Assert.AreSame(X, sum.Left);
        Assert.AreSame(Y, sum.Right);
        Assert.AreSame(lambda.Parameters[0], result.Parameters[0]);
    }

    /// <summary>Exact for every type with an ordinary negation, minimum values, signed zeros, infinities and NaN payloads included.</summary>
    [TestMethod]
    public void OrdinaryNegation_IsExactOnSpecialValues()
    {
        AssertExact<int>([0, 1, -1, int.MaxValue, int.MinValue], (a, b) => a == b);
        AssertExact<long>([0, 1, -1, long.MaxValue, long.MinValue], (a, b) => a == b);
        AssertExact<short>([0, 1, short.MinValue, short.MaxValue], (a, b) => a == b);
        double payload = BitConverter.Int64BitsToDouble(unchecked((long)0xFFF8_0000_0000_1234UL));
        AssertExact<double>([0.0, -0.0, 1.5, double.Epsilon, -double.Epsilon, double.MaxValue, double.PositiveInfinity, double.NegativeInfinity, double.NaN, payload],
            (a, b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b));
        AssertExact<float>([0f, -0f, 1.5f, float.Epsilon, float.NegativeInfinity, float.NaN],
            (a, b) => BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b));
        AssertExact<decimal>([0m, 0.00m, -0.10m, decimal.MaxValue, decimal.MinValue, 1.23456789012345678901234567m],
            (a, b) => decimal.GetBits(a).SequenceEqual(decimal.GetBits(b)));
    }

    /// <summary>Asserts that <c>-(-p)</c> simplifies to <c>p</c> and that both evaluate identically on <paramref name="values"/>.</summary>
    /// <typeparam name="T">The numeric type.</typeparam>
    /// <param name="values">The special values of the type.</param>
    /// <param name="same">The exact (bitwise) equality of the type.</param>
    private static void AssertExact<T>(T[] values, Func<T, T, bool> same)
    {
        ParameterExpression p = Expression.Parameter(typeof(T), "p");
        var source = Expression.Lambda<Func<T, T>>(Negations(p, 2), p);
        var simplified = (Expression<Func<T, T>>)S(source);
        Assert.AreSame(p, simplified.Body, typeof(T).Name);
        Func<T, T> before = source.Compile(), after = simplified.Compile();
        foreach (T value in values) Assert.IsTrue(same(before(value), after(value)), typeof(T).Name + " " + value);
    }

    /// <summary>Checked, lifted and custom negations, conversions between them and unsigned types keep every operator.</summary>
    [TestMethod]
    public void NonOrdinaryNegations_AreKept()
    {
        ParameterExpression i = Expression.Parameter(typeof(int), "i");
        ParameterExpression n = Expression.Parameter(typeof(int?), "n");
        ParameterExpression s = Expression.Parameter(typeof(Signed), "s");

        AssertKept(Expression.NegateChecked(Expression.Negate(i)));
        AssertKept(Expression.Negate(Expression.NegateChecked(i)));
        AssertKept(Expression.NegateChecked(Expression.NegateChecked(i)));
        AssertKept(Expression.Negate(Expression.Negate(n)));
        AssertKept(Expression.Negate(Expression.Negate(s)));
        AssertKept(Expression.Negate(Expression.Convert(Expression.Negate(i), typeof(long))));

        // The custom operator runs twice in the source and in the result: -(-(s)) is 4 * s with this operator.
        var lambda = Expression.Lambda<Func<Signed, Signed>>(Expression.Negate(Expression.Negate(s)), s);
        var simplified = (Expression<Func<Signed, Signed>>)S(lambda);
        Signed.Negations = 0;
        Assert.AreEqual(20, simplified.Compile()(new Signed(5)).Value);
        Assert.AreEqual(2, Signed.Negations);

        // checked(-(-int.MinValue)) still throws.
        var checkedLambda = Expression.Lambda<Func<int, int>>(Expression.NegateChecked(Expression.NegateChecked(i)), i);
        Func<int, int> compiled = ((Expression<Func<int, int>>)S(checkedLambda)).Compile();
        Assert.ThrowsExactly<OverflowException>(() => compiled(int.MinValue));
    }

    /// <summary>Asserts that <paramref name="source"/> is kept, node for node.</summary>
    /// <param name="source">The source tree.</param>
    private static void AssertKept(Expression source)
    {
        foreach (ExpressionSimplifierOptions options in Policies)
        {
            Expression result = S(source, options);
            Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(source, result), $"{Text(source)} became {Text(result)}");
        }
    }

    /// <summary>
    /// <c>-(-F())</c> evaluates <c>F</c> exactly once, in the same order, with the same value and exception, under every policy:
    /// the pair of ordinary negations evaluates nothing, so removing it drops, repeats, merges or moves no evaluation.
    /// </summary>
    [TestMethod]
    public void EffectfulOperand_IsEvaluatedOnceInOrder()
    {
        MethodInfo Method(string name) => typeof(Probe).GetMethod(name)!;
        Expression unbox = Expression.Unbox(Expression.Call(Method(nameof(Probe.Allocate))), typeof(double));
        Expression[] operands =
        [
            Expression.Call(Method(nameof(Probe.Count))),
            Expression.Call(Method(nameof(Probe.Throw))),
            Expression.Call(Method(nameof(Probe.Read))),
            Expression.Call(Method(nameof(Probe.Write))),
            unbox,
            Expression.Call(Method(nameof(Probe.Declared)), X),
        ];

        foreach (Expression operand in operands)
        {
            // Inside a sum after a write, so that a moved read or a repeated write would change the value.
            Expression body = Expression.Add(Expression.Call(Method(nameof(Probe.Write))), Negations(operand, 2));
            var source = Expression.Lambda<Func<double, double>>(body, X);
            foreach (ExpressionSimplifierOptions options in Policies)
            {
                var simplified = (Expression<Func<double, double>>)S(source, options);
                Assert.IsFalse(Text(simplified).Contains("-(-", StringComparison.Ordinal), Text(simplified));
                (string Log, string Outcome) expected = Run(source.Compile()), actual = Run(simplified.Compile());
                Assert.AreEqual(expected, actual, $"{Text(source)} -> {Text(simplified)} ({options})");
            }

            // Alone, the double negation of an effectful call is removed too, and the call keeps its single evaluation.
            var alone = Expression.Lambda<Func<double, double>>(Negations(operand, 2), X);
            Expression result = ((LambdaExpression)S(alone)).Body;
            Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(operand, result), Text(alone) + " -> " + Text(result));
        }
    }

    /// <summary>Runs <paramref name="function"/> on fresh state and returns the call log and the value or exception.</summary>
    /// <param name="function">The compiled function.</param>
    /// <returns>The log and the outcome.</returns>
    private static (string Log, string Outcome) Run(Func<double, double> function)
    {
        Probe.Log = [];
        Probe.State = 10;
        string outcome;
        try
        {
            outcome = function(4).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (InvalidOperationException exception)
        {
            outcome = exception.GetType().Name + ": " + exception.Message;
        }

        return (string.Join(",", Probe.Log), outcome);
    }

    /// <summary>The comparer identifies a double negation with its operand, and its invariant holds on the cases of the brief.</summary>
    [TestMethod]
    public void Comparer_IdentifiesDoubleNegation()
    {
        Assert.IsTrue(ExpressionComparer.Default.Equals(Negations(X, 2), X));
        Assert.IsTrue(ExpressionComparer.Default.Equals(Negations(X, 3), Expression.Negate(X)));
        Assert.IsTrue(ExpressionComparer.Default.Equals(Expression.Multiply(Expression.Negate(X), Expression.Negate(Y)), Expression.Multiply(X, Y)));
        Assert.AreEqual(ExpressionComparer.Default.GetHashCode(X), ExpressionComparer.Default.GetHashCode(Negations(X, 2)));

        Expression[] cases =
        [
            X, Expression.Negate(X), Negations(X, 2), Negations(X, 3), Negations(X, 4),
            Expression.Add(X, Negations(Y, 2)), Expression.Subtract(X, Negations(Y, 2)), Expression.Add(X, Y), Expression.Subtract(X, Y),
            Negations(Expression.Add(X, Y), 2), Negations(Expression.Subtract(X, Y), 2),
            Expression.Multiply(Expression.Negate(X), Expression.Negate(Y)), Expression.Multiply(X, Y),
            Expression.Multiply(Expression.Multiply(X, Expression.Constant(-1.0)), Expression.Constant(-1.0)),
        ];
        foreach (Expression a in cases)
        {
            foreach (Expression b in cases)
            {
                Assert.AreEqual(ExpressionComparer.Default.Equals(a, b), ExpressionComparer.Default.Equals(S(a), S(b)), $"{Text(a)} / {Text(b)}");
            }
        }
    }

    /// <summary>A derived simplifier gets the finalization too, like the additive canonicalization it shares a sign model with.</summary>
    [TestMethod]
    public void DerivedSimplifier_RemovesDoubleNegation()
    {
        Assert.AreEqual("x", Text(new DerivedSimplifier().Simplify(Negations(X, 2))));
        Assert.AreEqual("x * y", Text(new DerivedSimplifier().Simplify(Expression.Multiply(Expression.Negate(X), Expression.Negate(Y)))));
    }

    /// <summary>The removal is a finalization, reported by the trace as a canonicalization of the outer <c>Negate</c> node.</summary>
    [TestMethod]
    public void Trace_ReportsTheFinalization()
    {
        var collector = new ExpressionTraceCollector(1_000);
        using (ExpressionTransformTracing.Begin(collector)) S(Negations(X, 2));
        ExpressionTransformEvent[] events = collector.GetEvents();
        Assert.IsTrue(events.Any(e => e.Kind == ExpressionTransformEventKind.Finalize && e.Outcome == ExpressionTransformOutcome.Finalized));
        Assert.IsFalse(events.Any(e => e.Kind == ExpressionTransformEventKind.RuleApplied));
    }
}
