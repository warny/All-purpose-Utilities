using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// The additive chains of quotients, <c>1 - x1/x0 - x2/x0 - … - xn/x0</c> (mission "trigonometric rule and division chains",
/// part B, <c>docs/expressions/SimplifierFlatten.md</c> §15): their cost, their results, their values, their effects and their
/// budgets.
/// </summary>
[TestClass]
public class ExpressionSimplifierDivisionChainTests
{
    /// <summary>Counts the transformation visits.</summary>
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

    /// <summary>Builds <c>1 - x1/x0 - … - xn/x0</c>, left-associated, over <see cref="double"/> parameters.</summary>
    /// <param name="n">The number of quotients.</param>
    /// <returns>The chain.</returns>
    internal static Expression SubChain(int n)
    {
        ParameterExpression x0 = Expression.Parameter(typeof(double), "x0");
        Expression chain = Expression.Constant(1.0);
        for (int i = 1; i <= n; i++) chain = Expression.Subtract(chain, Expression.Divide(Expression.Parameter(typeof(double), "x" + i), x0));
        return chain;
    }

    /// <summary>Simplifies <paramref name="source"/> without limits and returns its visits and comparer simplifications.</summary>
    /// <param name="source">The tree.</param>
    /// <returns>The transformation visits and the simplifications requested from the comparer.</returns>
    private static (long Visits, long ComparerSimplifications) Cost(Expression source)
    {
        var counter = new VisitCounter();
        var statistics = new SimplificationMetadataStatistics();
        ExpressionSimplifier.MetadataStatistics = statistics;
        try
        {
            using (ExpressionTransformTracing.Begin(counter))
            {
                new ExpressionSimplifier(ExpressionResourceLimits.Unlimited).Simplify(source);
            }
        }
        finally
        {
            ExpressionSimplifier.MetadataStatistics = null;
        }

        return (counter.Visits, statistics.ComparerSimplifications);
    }

    /// <summary>A derived simplifier without any member: no flatten rule, the historical level by level treatment.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>A derived simplifier that changes the result of a division rule: its own rules must keep running.</summary>
    private sealed class DividingSimplifier : ExpressionSimplifier
    {
        /// <summary>Gets the number of divisions the rule saw.</summary>
        public int Divisions { get; private set; }

        /// <summary>Counts the divisions and lets the other rules run.</summary>
        /// <param name="e">The division.</param>
        /// <param name="left">The dividend.</param>
        /// <param name="right">The divisor.</param>
        /// <returns><see langword="null"/>.</returns>
        [ExpressionSignature(ExpressionType.Divide)]
        protected Expression? CountDivision(BinaryExpression e, Expression left, Expression right)
        {
            Divisions++;
            return null;
        }
    }

    /// <summary>The policies the results must not depend on (every operand is inert arithmetic).</summary>
    private static readonly ExpressionSimplifierOptions[] Policies =
    [
        ExpressionSimplifierOptions.Default,
        new() { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations },
        new() { AggressiveSimplification = true },
        new() { ExceptionPreservation = ExpressionExceptionPreservation.None },
    ];

    /// <summary>Builds <c>c - x1/x0 ± …</c> over <paramref name="type"/>: signs, association and denominators as given.</summary>
    /// <param name="type">The parameter type.</param>
    /// <param name="n">The number of quotients.</param>
    /// <param name="shape">The shape: <c>left</c>, <c>right</c>, <c>balanced</c>, <c>mixed</c> or <c>distinct</c>.</param>
    /// <param name="parameters">Receives the parameters, <c>x0</c> first.</param>
    /// <returns>The chain.</returns>
    private static Expression Chain(Type type, int n, string shape, out ParameterExpression[] parameters)
    {
        parameters = [.. Enumerable.Range(0, 2 * n + 1).Select(i => Expression.Parameter(type, "p" + i))];
        ParameterExpression[] p = parameters;
        Expression[] quotients = [.. Enumerable.Range(1, n).Select(i => (Expression)Expression.Divide(p[i], shape == "distinct" ? p[n + i] : p[0]))];
        Expression one = Expression.Constant(Convert.ChangeType(1, type), type);
        return shape switch
        {
            "right" => Expression.Subtract(one, Enumerable.Reverse(quotients).Aggregate((right, left) => (Expression)Expression.Add(left, right))),
            "balanced" => Expression.Subtract(one, Balanced(quotients)),
            "mixed" => quotients.Select((q, i) => (q, i)).Aggregate((Expression)one, (acc, t) => t.i % 2 == 0 ? Expression.Subtract(acc, t.q) : Expression.Add(acc, t.q)),
            _ => quotients.Aggregate((Expression)one, (acc, q) => Expression.Subtract(acc, q)),
        };
    }

    /// <summary>A balanced sum.</summary>
    /// <param name="terms">The terms.</param>
    /// <returns>The sum.</returns>
    private static Expression Balanced(IReadOnlyList<Expression> terms)
        => terms.Count == 1 ? terms[0] : Expression.Add(Balanced([.. terms.Take(terms.Count / 2)]), Balanced([.. terms.Skip(terms.Count / 2)]));

    /// <summary>Evaluates <paramref name="tree"/> over <paramref name="parameters"/> with one value per parameter, or the exception type.</summary>
    /// <param name="tree">The tree.</param>
    /// <param name="parameters">The parameters.</param>
    /// <param name="values">The values.</param>
    /// <returns>The value, or the name of the exception thrown.</returns>
    private static object Evaluate(Expression tree, ParameterExpression[] parameters, object[] values)
    {
        Delegate compiled = Expression.Lambda(tree, parameters).Compile();
        try
        {
            return compiled.DynamicInvoke(values)!;
        }
        catch (System.Reflection.TargetInvocationException exception)
        {
            return exception.InnerException!.GetType().Name;
        }
    }

    /// <summary>Whether two evaluations agree within the floating-point latitude of the symbolic contract (reassociation).</summary>
    /// <param name="expected">The source value.</param>
    /// <param name="actual">The simplified value.</param>
    /// <returns><see langword="true"/> when they agree.</returns>
    private static bool Agree(object expected, object actual)
    {
        // The S3 latitude: a reassociated floating-point sum may round differently, and at extreme magnitudes or with
        // non-finite terms it may overflow, cancel or produce NaN at another point than the source.
        static bool Close(double a, double b)
            => a.Equals(b)
                || !double.IsFinite(a) || !double.IsFinite(b) || Math.Abs(a) > 1e300 || Math.Abs(b) > 1e300
                || Math.Abs(a - b) <= 1e-9 * Math.Max(1, Math.Max(Math.Abs(a), Math.Abs(b)));
        return (expected, actual) switch
        {
            (double a, double b) => Close(a, b),
            (float a, float b) => Close(a, b) || Math.Abs(a) > 1e37 || Math.Abs(b) > 1e37 || Math.Abs(a - b) <= 1e-4 * Math.Max(1, Math.Abs(a)),
            _ => Equals(expected, actual),
        };
    }

    /// <summary>
    /// Every shape, type and value: the result evaluates like the source (exceptions included) and like the derived simplifier's
    /// level by level result, and is a structural fixed point of a second call for the field types (but alternating signs, §15.9).
    /// </summary>
    [TestMethod]
    public void Chains_KeepTheirValues_InEveryTypeAndShape()
    {
        var samples = new Dictionary<Type, object[]>
        {
            [typeof(double)] = [3.0, -2.0, 0.0, -0.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.MaxValue, double.Epsilon, 1e-310],
            [typeof(float)] = [3f, -2f, 0f, -0f, float.NaN, float.PositiveInfinity, float.MaxValue, float.Epsilon],
            [typeof(int)] = [7, -3, 0, int.MaxValue, int.MinValue, -1],
            [typeof(long)] = [7L, -3L, 0L, long.MaxValue, long.MinValue, -1L],
            [typeof(uint)] = [7u, 3u, 0u, uint.MaxValue],
            [typeof(ulong)] = [7ul, 3ul, 0ul, ulong.MaxValue],
            [typeof(decimal)] = [7m, -3m, 0m, decimal.MaxValue, 0.0000001m],
        };

        foreach ((Type type, object[] values) in samples)
        {
            foreach (string shape in new[] { "left", "right", "balanced", "mixed", "distinct" })
            {
                Expression source = Chain(type, 5, shape, out ParameterExpression[] parameters);
                Expression result = new ExpressionSimplifier().Simplify(source);
                Expression derived = new DerivedSimplifier().Simplify(source);
                var random = new Random(type.Name.Length * 31 + shape.Length);
                for (int sample = 0; sample < 60; sample++)
                {
                    object[] assignment = [.. parameters.Select(_ => values[random.Next(values.Length)])];
                    // Either within the S3 latitude of the source, or exactly the value of the level by level result (the
                    // historical reassociation, whose rounding at extreme magnitudes is not new).
                    object expected = Evaluate(source, parameters, assignment);
                    object actual = Evaluate(result, parameters, assignment);
                    object levels = Evaluate(derived, parameters, assignment);
                    Assert.IsTrue(Agree(expected, actual) || Equals(levels, actual), $"{type.Name} {shape}: {source} -> {result} at [{string.Join(", ", assignment)}]");
                }

                // Alternating signs keep the S4 tie of free parameters: the level by level result is not a fixed point (§15.9).
                if ((type == typeof(double) || type == typeof(float)) && shape != "mixed")
                {
                    Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(result, new ExpressionSimplifier().Simplify(result)), $"{type.Name} {shape}: {result}");
                }
            }
        }
    }

    /// <summary>The result does not depend on the policy: the operands are inert under every one.</summary>
    [TestMethod]
    public void Chains_GiveOneResult_UnderEveryPolicy()
    {
        foreach (string shape in new[] { "left", "right", "balanced", "mixed", "distinct" })
        {
            Expression source = Chain(typeof(double), 12, shape, out _);
            Expression expected = new ExpressionSimplifier().Simplify(source);
            foreach (ExpressionSimplifierOptions options in Policies)
            {
                Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(expected, new ExpressionSimplifier { Options = options }.Simplify(source)), shape);
            }
        }
    }

    /// <summary>The number of calls of <see cref="Observed"/>, in order of their arguments.</summary>
    [ThreadStatic]
    private static List<double>? s_log;

    /// <summary>A user function with an effect: it logs its argument.</summary>
    /// <param name="value">The value.</param>
    /// <returns><paramref name="value"/>.</returns>
    public static double Observed(double value)
    {
        (s_log ??= []).Add(value);
        return value;
    }

    /// <summary>A user function that throws for a negative argument.</summary>
    /// <param name="value">The value.</param>
    /// <returns><paramref name="value"/>.</returns>
    public static double Checked(double value) => value < 0 ? throw new ArgumentOutOfRangeException(nameof(value)) : value;

    /// <summary>A cell whose field is read by a member access.</summary>
    public sealed class Cell
    {
        /// <summary>A mutable value.</summary>
        public double Value = 4;
    }

    /// <summary>A value with a user subtraction operator.</summary>
    public readonly record struct Money(double Amount)
    {
        /// <summary>The user subtraction.</summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>The difference.</returns>
        public static Money operator -(Money left, Money right) => new(left.Amount - right.Amount);
    }

    /// <summary>
    /// A chain holding observable evaluations (a logging call, a throwing call, a member access, an opaque constant, a custom
    /// operator) evaluates them as often and in the order the source does, and throws as the source does.
    /// </summary>
    [TestMethod]
    public void EffectfulChains_KeepTheirEvaluations()
    {
        ParameterExpression x0 = Expression.Parameter(typeof(double), "x0"), x1 = Expression.Parameter(typeof(double), "x1"), x2 = Expression.Parameter(typeof(double), "x2");
        ParameterExpression cell = Expression.Parameter(typeof(Cell), "cell");
        Expression observed1 = Expression.Call(typeof(ExpressionSimplifierDivisionChainTests).GetMethod(nameof(Observed))!, x1);
        Expression observed2 = Expression.Call(typeof(ExpressionSimplifierDivisionChainTests).GetMethod(nameof(Observed))!, x2);
        Expression throwing = Expression.Call(typeof(ExpressionSimplifierDivisionChainTests).GetMethod(nameof(Checked))!, x2);
        Expression member = Expression.Field(cell, nameof(Cell.Value));
        Expression opaque = Expression.Convert(Expression.Constant(new object(), typeof(object)), typeof(object));
        Expression[] sources =
        [
            Expression.Subtract(Expression.Subtract(Expression.Subtract(Expression.Constant(1.0), Expression.Divide(x1, x0)), observed2), Expression.Divide(x2, x0)),
            Expression.Subtract(Expression.Subtract(Expression.Subtract(Expression.Constant(1.0), observed1), Expression.Divide(x2, x0)), observed2),
            Expression.Subtract(Expression.Subtract(Expression.Constant(1.0), Expression.Divide(x1, x0)), throwing),
            Expression.Subtract(Expression.Subtract(Expression.Constant(1.0), Expression.Divide(x1, x0)), Expression.Divide(member, x0)),
            Expression.Subtract(Expression.Subtract(Expression.Constant(1.0), Expression.Divide(x1, x0)),
                Expression.Condition(Expression.ReferenceEqual(opaque, Expression.Constant(null)), x2, Expression.Divide(x2, x0))),
        ];

        foreach (Expression source in sources)
        {
            Expression result = new ExpressionSimplifier().Simplify(source);
            var f = Expression.Lambda<Func<double, double, double, Cell, double>>(source, x0, x1, x2, cell).Compile();
            var g = Expression.Lambda<Func<double, double, double, Cell, double>>(result, x0, x1, x2, cell).Compile();
            foreach ((double a, double b, double c) in new[] { (2.0, 3.0, 5.0), (0.0, 1.0, -1.0), (-4.0, 0.5, 2.0) })
            {
                s_log = [];
                string expected = Run(() => f(a, b, c, new Cell()));
                List<double> expectedLog = s_log;
                s_log = [];
                string actual = Run(() => g(a, b, c, new Cell()));
                Assert.AreEqual(expected, actual, $"{source} -> {result}");
                CollectionAssert.AreEqual(expectedLog, s_log, $"{source} -> {result}");
            }
        }

        ParameterExpression m0 = Expression.Parameter(typeof(Money), "m0"), m1 = Expression.Parameter(typeof(Money), "m1");
        Expression money = Expression.Subtract(Expression.Subtract(m0, m1), m1);
        Assert.AreEqual(ExpressionType.Subtract, new ExpressionSimplifier().Simplify(money).NodeType);
    }

    /// <summary>Runs <paramref name="action"/> and renders its value or exception type.</summary>
    /// <param name="action">The action.</param>
    /// <returns>The rendering.</returns>
    private static string Run(Func<double> action)
    {
        try
        {
            return action().ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception exception)
        {
            return exception.GetType().Name;
        }
    }

    /// <summary>
    /// The derived simplifiers keep the level by level treatment and their own rules: one without members gives the built-in
    /// result here (no rule applies at any level), one with a division rule sees every quotient once per preparation.
    /// </summary>
    [TestMethod]
    public void DerivedSimplifiers_KeepTheirRules()
    {
        Expression source = SubChain(12);
        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(new ExpressionSimplifier().Simplify(source), new DerivedSimplifier().Simplify(source)));
        var dividing = new DividingSimplifier();
        dividing.Simplify(source);
        Assert.IsTrue(dividing.Divisions >= 12, dividing.Divisions.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// The visit budget: the minimal budget succeeds, one less refuses cleanly, and the same instance then succeeds again with the
    /// same result (no session, cache or marker left behind).
    /// </summary>
    [TestMethod]
    public void Budgets_RefuseCleanly_AtTheLimit()
    {
        Expression source = SubChain(24);
        Expression expected = new ExpressionSimplifier(ExpressionResourceLimits.Unlimited).Simplify(source);
        foreach (Func<long, ExpressionResourceLimits> limitsOf in new Func<long, ExpressionResourceLimits>[]
        {
            value => ExpressionResourceLimits.Unlimited with { MaxTransformVisits = value },
            value => ExpressionResourceLimits.Unlimited with { MaxStructuralVisits = value },
        })
        {
            long low = 1, high = 1 << 20;
            while (low + 1 < high)
            {
                long middle = (low + high) / 2;
                bool passes;
                try
                {
                    new ExpressionSimplifier(limitsOf(middle)).Simplify(source);
                    passes = true;
                }
                catch (ExpressionResourceLimitException)
                {
                    passes = false;
                }

                if (passes) high = middle; else low = middle;
            }

            var simplifier = new ExpressionSimplifier(limitsOf(high - 1));
            Assert.ThrowsExactly<ExpressionResourceLimitException>(() => simplifier.Simplify(source));
            Assert.ThrowsExactly<ExpressionResourceLimitException>(() => simplifier.Simplify(source));
            var exact = new ExpressionSimplifier(limitsOf(high));
            Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(expected, exact.Simplify(source)));
            Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(expected, exact.Simplify(source)));
        }

        // The depth limit refuses the chain before any work, then the unlimited instance succeeds.
        Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpressionSimplifier(ExpressionResourceLimits.Unlimited with { MaxDepth = 10 }).Simplify(source));
    }

    /// <summary>A shared quotient (a DAG) and a DAG of many paths: the work stays bounded by the distinct nodes.</summary>
    [TestMethod]
    public void SharedQuotients_AreTreatedOnce()
    {
        ParameterExpression x0 = Expression.Parameter(typeof(double), "x0"), x1 = Expression.Parameter(typeof(double), "x1");
        Expression q = Expression.Divide(x1, x0);
        Expression shared = Expression.Subtract(Expression.Subtract(Expression.Constant(1.0), q), q);
        Expression result = new ExpressionSimplifier().Simplify(shared);
        var f = Expression.Lambda<Func<double, double, double>>(shared, x0, x1).Compile();
        var g = Expression.Lambda<Func<double, double, double>>(result, x0, x1).Compile();
        Assert.AreEqual(f(4, 2), g(4, 2), result.ToString());

        // 2^40 paths through 40 shared levels: refused or simplified within the default budgets, never walked path by path.
        Expression dag = SubChain(4);
        for (int i = 0; i < 40; i++) dag = Expression.Add(dag, dag);
        try
        {
            new ExpressionSimplifier().Simplify(dag);
        }
        catch (ExpressionResourceLimitException)
        {
        }
    }

    /// <summary>A chain of 20,000 quotients on a 256 KiB stack: the treatment is iterative.</summary>
    [TestMethod]
    public void LongChain_RunsOnASmallStack()
    {
        Expression source = SubChain(20_000);
        Exception? failure = null;
        Expression? result = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                result = new ExpressionSimplifier(ExpressionResourceLimits.Unlimited).Simplify(source);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        }, 256 * 1024);
        thread.Start();
        thread.Join();
        Assert.IsNull(failure, failure?.ToString());
        Assert.IsNotNull(result);
    }

    /// <summary>Several threads, nested simplifications from user code, and an instance reused after a refusal.</summary>
    [TestMethod]
    public void Concurrency_AndReentrancy()
    {
        Expression source = SubChain(16);
        Expression expected = new ExpressionSimplifier().Simplify(source);
        var shared = new ExpressionSimplifier();
        System.Threading.Tasks.Parallel.For(0, 32, _ => Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(expected, shared.Simplify(source))));

        // A nested public simplification from user code (a constant's Equals called by the comparer) is independent.
        s_reentrant = shared;
        s_nestedSource = source;
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        static Expression Carrier(object value) => Expression.Call(typeof(ExpressionSimplifierDivisionChainTests).GetMethod(nameof(Unbox))!, Expression.Constant(value, typeof(object)));
        Expression withReentry = Expression.Subtract(Expression.Add(source, Expression.Multiply(Carrier(new Reentrant()), x)), Expression.Multiply(Carrier(new Reentrant()), x));
        shared.Simplify(withReentry);
        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(expected, shared.Simplify(source)));

        // Reuse after a refusal.
        var limited = new ExpressionSimplifier(ExpressionResourceLimits.Unlimited with { MaxTransformVisits = 5 });
        Assert.ThrowsExactly<ExpressionResourceLimitException>(() => limited.Simplify(source));
        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(expected, shared.Simplify(source)));
    }

    /// <summary>The simplifier a <see cref="Reentrant"/> value calls.</summary>
    [ThreadStatic]
    private static ExpressionSimplifier? s_reentrant;

    /// <summary>The tree a <see cref="Reentrant"/> value simplifies.</summary>
    [ThreadStatic]
    private static Expression? s_nestedSource;

    /// <summary>A value whose equality simplifies a quotient chain, from user code.</summary>
    private sealed class Reentrant
    {
        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            if (s_reentrant is { } simplifier && s_nestedSource is { } nested) simplifier.Simplify(nested);
            return ReferenceEquals(this, obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode() => 1;
    }

    /// <summary>Turns a value into a number.</summary>
    /// <param name="value">The value.</param>
    /// <returns><c>1</c>.</returns>
    public static double Unbox(object value) => 1;

    /// <summary>
    /// The historical treatment compared, at every level, the growing canonical prefix with the new quotient, and the comparer
    /// simplified that prefix again, which rebuilt it level by level: cubic visits (6,934 for 16 quotients, about 1.4 s for 64).
    /// The work is linear: no comparison of the prefix, so no simplification of it.
    /// </summary>
    [TestMethod]
    public void SubChain_WorkIsLinear()
    {
        (long visits8, long comparer8) = Cost(SubChain(8));
        (long visits32, long comparer32) = Cost(SubChain(32));
        Assert.IsTrue(visits32 <= 5 * visits8, $"visits: {visits8} for 8 quotients, {visits32} for 32");
        Assert.IsTrue(comparer32 <= 32, $"comparer simplifications: {comparer8} for 8 quotients, {comparer32} for 32");
    }
}
