using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;
using UtilsTest.Mathematics.Expressions.Coverage;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// AUD-03 (S5 P17): termination of the factoring rules. <c>SubstractionOfEqualsElements</c> factored a difference whose operand
/// is the constant 1 by that 1 (<c>y - 1 -&gt; (y - 1) * 1</c>), and <c>MultiplicationOfEqualsElements</c> moved that factor out
/// of the enclosing product and transformed the product again, which rebuilt <c>y - 1</c> and its factor without end:
/// <c>c * (y - 1) -&gt; 1 * (c * (y - 1)) -&gt; ...</c>. The subtraction now declines on an operand equal to 1, as the addition
/// always did.
/// </summary>
[TestClass]
public class ExpressionSimplifierRuleTerminationTests
{
    /// <summary>The parameter <c>x</c>.</summary>
    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");

    /// <summary>The parameter <c>y</c>.</summary>
    private static readonly ParameterExpression Y = Expression.Parameter(typeof(double), "y");

    /// <summary>The evaluation points (no zero, so that the S3 domain of the symbolic identities is respected).</summary>
    private static readonly (double X, double Y)[] Points = [(3, 7), (-2, 0.5), (1.5, -4), (0.25, 11)];

    /// <summary>Builds a <see cref="double"/> constant.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The constant.</returns>
    private static ConstantExpression C(double value) => Expression.Constant(value);

    /// <summary>Simplifies a body over <c>(x, y)</c>.</summary>
    /// <param name="body">The body.</param>
    /// <returns>The simplified lambda.</returns>
    private static Expression<Func<double, double, double>> Simplify(Expression body)
        => (Expression<Func<double, double, double>>)new ExpressionSimplifier().Simplify(Expression.Lambda<Func<double, double, double>>(body, X, Y));

    /// <summary>Checks that the simplified lambda computes the same values as the original body.</summary>
    /// <param name="body">The original body.</param>
    /// <param name="simplified">The simplified lambda.</param>
    private static void AssertSameValues(Expression body, Expression<Func<double, double, double>> simplified)
    {
        Func<double, double, double> original = Expression.Lambda<Func<double, double, double>>(body, X, Y).Compile();
        Func<double, double, double> result = simplified.Compile();
        foreach ((double x, double y) in Points)
        {
            double expected = original(x, y), actual = result(x, y);
            Assert.AreEqual(expected, actual, 1e-9 * Math.Max(1, Math.Abs(expected)), $"{ExpressionTraceFormatter.Format(body)} at ({x}, {y})");
        }
    }

    /// <summary>
    /// The four audit reproducers (and the differences that started the loop) terminate, with their values, in the canonical
    /// form of the other differences: <c>y - 1</c> is now simplified like <c>y - 2</c> (<c>(0 - 2) + y</c>). The double division
    /// keeps the <c>/ 1</c> that <c>DivisionOfDivision</c> leaves on a unit numerator (a separate, pre-existing limitation).
    /// </summary>
    [TestMethod]
    public void Aud03Reproducers_Terminate()
    {
        (Expression Body, string Expected)[] cases =
        [
            (Expression.Multiply(C(10), Expression.Subtract(Y, C(1))), "10 * ((0 - 1) + y)"),
            (Expression.Multiply(C(2), Expression.Subtract(Y, C(1))), "2 * ((0 - 1) + y)"),
            (Expression.Multiply(C(10), Expression.Subtract(C(1), Y)), "10 * (1 + (0 - y))"),
            (Expression.Divide(Expression.Subtract(C(1), Y), Expression.Divide(C(1), Y)), "(y * (1 + (0 - y))) / 1"),
            (Expression.Subtract(Y, C(1)), "(0 - 1) + y"),
            (Expression.Subtract(C(1), Y), "1 + (0 - y)"),
            (Expression.Multiply(C(-1), Expression.Subtract(Y, C(1))), "-((0 - 1) + y)"),
            (Expression.Subtract(Y, C(2)), "(0 - 2) + y"),
        ];

        foreach ((Expression body, string expected) in cases)
        {
            Expression<Func<double, double, double>> simplified = Simplify(body);
            Assert.AreEqual(expected, ExpressionTraceFormatter.Format(simplified.Body), ExpressionTraceFormatter.Format(body));
            AssertSameValues(body, simplified);
        }
    }

    /// <summary>
    /// Idempotence, examined: a second simplification folds the <c>0 - c</c> of the canonical negative group, as it always did
    /// for <c>y - 2</c> (S5 P10, the representation of a negative group), so a single pass is not idempotent on these forms;
    /// the second pass is a fixed point.
    /// </summary>
    [TestMethod]
    public void Aud03Reproducers_ReachAFixedPointAtTheSecondPass()
    {
        (Expression Body, string Second)[] cases =
        [
            (Expression.Multiply(C(10), Expression.Subtract(Y, C(1))), "10 * (-1 + y)"),
            (Expression.Multiply(C(10), Expression.Subtract(C(1), Y)), "10 * (1 + (0 - y))"),
            (Expression.Divide(Expression.Subtract(C(1), Y), Expression.Divide(C(1), Y)), "y * (1 + (0 - y))"),
            (Expression.Subtract(Y, C(2)), "-2 + y"),
        ];

        var simplifier = new ExpressionSimplifier();
        foreach ((Expression body, string second) in cases)
        {
            Expression once = simplifier.Simplify(body);
            Expression twice = simplifier.Simplify(once);
            Expression thrice = simplifier.Simplify(twice);
            Assert.AreEqual(second, ExpressionTraceFormatter.Format(twice), ExpressionTraceFormatter.Format(body));
            Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(twice, thrice), ExpressionTraceFormatter.Format(body));
            Assert.AreEqual(twice.ToString(), thrice.ToString());
        }
    }

    /// <summary>The bodies of the neighbouring shapes: coefficients, constants, operand orders, sides, nesting and divisions.</summary>
    /// <returns>The bodies.</returns>
    private static IEnumerable<Expression> NeighbourShapes()
    {
        double[] coefficients = [-3, -1, 0.5, 1, 2, 10];
        double[] constants = [-1, 0.5, 1, 2];
        foreach (double c in coefficients)
        {
            foreach (double k in constants)
            {
                yield return Expression.Multiply(C(c), Expression.Subtract(Y, C(k)));
                yield return Expression.Multiply(C(c), Expression.Subtract(C(k), Y));
                yield return Expression.Multiply(C(c), Expression.Add(Y, C(k)));
                yield return Expression.Multiply(Expression.Subtract(Y, C(k)), C(c));
                yield return Expression.Divide(Expression.Subtract(Y, C(k)), C(c));
                yield return Expression.Divide(Expression.Subtract(C(k), Y), Expression.Divide(C(c), Y));
            }
        }

        // Several parameters, expanded and nested factorizations, mixed factorization and division.
        yield return Expression.Multiply(X, Expression.Subtract(Y, C(1)));
        yield return Expression.Multiply(Expression.Subtract(X, C(1)), Expression.Subtract(Y, C(1)));
        yield return Expression.Subtract(Expression.Multiply(C(10), Y), C(10));
        yield return Expression.Multiply(C(10), Expression.Subtract(Expression.Multiply(C(2), Y), C(2)));
        yield return Expression.Multiply(C(10), Expression.Multiply(C(10), Expression.Subtract(Y, C(1))));
        yield return Expression.Multiply(C(10), Expression.Subtract(Expression.Subtract(Y, C(1)), C(1)));
        yield return Expression.Multiply(C(10), Expression.Subtract(Expression.Multiply(Y, X), X));
        yield return Expression.Multiply(Expression.Divide(Expression.Subtract(C(1), Y), Expression.Divide(C(1), Y)), C(3));
        yield return Expression.Divide(Expression.Divide(Expression.Subtract(Y, C(1)), C(2)), Expression.Divide(C(1), X));
        yield return Expression.Multiply(Expression.Subtract(Y, C(1)), Expression.Subtract(C(1), Y));
    }

    /// <summary>
    /// The neighbouring shapes terminate, keep their values, and reach a fixed point within four passes, every pass
    /// terminating too. One pass is not idempotent in general (S5 P10): a factoring found on the second pass
    /// (<c>10 * y - 10 -&gt; (y - 1) * 10</c> is a rule result, simplified only by the next pass) needs a third one.
    /// </summary>
    [TestMethod]
    public void NeighbourShapes_TerminateKeepTheirValuesAndStabilize()
    {
        var simplifier = new ExpressionSimplifier();
        int count = 0;
        foreach (Expression body in NeighbourShapes())
        {
            Expression<Func<double, double, double>> simplified = Simplify(body);
            AssertSameValues(body, simplified);

            Expression current = simplified;
            int passes = 1;
            for (Expression next = simplifier.Simplify(current); next.ToString() != current.ToString(); next = simplifier.Simplify(current))
            {
                AssertSameValues(body, (Expression<Func<double, double, double>>)next);
                current = next;
                Assert.IsTrue(++passes <= 4, ExpressionTraceFormatter.Format(body));
            }

            count++;
        }

        Assert.AreEqual(154, count);
    }

    /// <summary>
    /// The stack overflow left open by S5 P14 and P16 on <c>a * x + (1 - a) * x</c> had the same cause (the coefficient
    /// <c>1 - a</c> was factored by 1, then moved around by the multiplication rule): it terminates too. The factored
    /// coefficient is a rule result, simplified by the next pass only (S5 P14).
    /// </summary>
    [TestMethod]
    public void P14OpenStackOverflow_HadTheSameCause()
    {
        Expression one = C(1);
        (Expression Body, string Expected)[] cases =
        [
            (Expression.Add(Expression.Multiply(X, Y), Expression.Multiply(Expression.Subtract(one, X), Y)), "(x + (1 + (0 - x))) * y"),
            (Expression.Add(Expression.Multiply(Y, Expression.Subtract(one, X)), Expression.Multiply(X, Y)), "(1 + (x - x)) * y"),
            (Expression.Subtract(Expression.Multiply(Expression.Subtract(X, one), Y), Expression.Multiply(X, Y)), "(-1 + (x + (0 - x))) * y"),
        ];

        foreach ((Expression body, string expected) in cases)
        {
            Expression<Func<double, double, double>> simplified = Simplify(body);
            Assert.AreEqual(expected, ExpressionTraceFormatter.Format(simplified.Body), ExpressionTraceFormatter.Format(body));
            AssertSameValues(body, simplified);
            AssertSameValues(body, (Expression<Func<double, double, double>>)new ExpressionSimplifier().Simplify(simplified));
        }
    }

    /// <summary>
    /// The trace of a former reproducer shows the cause gone: no rule applies to <c>y - 1</c>, no form is transformed again
    /// inside its own treatment, and no rule sequence repeats.
    /// </summary>
    [TestMethod]
    public void Aud03Trace_ShowsNoFactoringByOneAndNoCycle()
    {
        Expression difference = Expression.Subtract(Y, C(1));
        var collector = new ExpressionTraceCollector();
        using (ExpressionTransformTracing.Begin(collector))
        {
            Simplify(Expression.Multiply(C(10), difference));
        }

        ExpressionTransformEvent[] events = collector.GetEvents();
        Assert.IsFalse(events.Any(e => e.Kind == ExpressionTransformEventKind.RuleApplied && e.Rule!.Name == "SubstractionOfEqualsElements"));
        Assert.IsFalse(events.Any(e => e.Kind == ExpressionTransformEventKind.RuleApplied && e.Rule!.Name == "MultiplicationOfEqualsElements"));
        ExpressionTraceCycleReport report = ExpressionTraceCycleAnalyzer.Analyze(events);
        Assert.IsFalse(report.HasRecurrence);
        Assert.AreEqual(0, report.Periods.Count);
        Assert.IsTrue(report.MaximumDepth < 8, report.MaximumDepth.ToString());
    }

    /// <summary>
    /// Operands with observable effects (a traced call, a traced getter) keep their evaluations, count and order: the effect
    /// gate (ETC R1b) still keeps every rule away from them, and the shapes terminate.
    /// </summary>
    [TestMethod]
    public void ObservableOperands_KeepTheirEffects()
    {
        var cell = new AuditCell();
        Expression call = Expression.Call(typeof(R4DynamicRuntime).GetMethod(nameof(R4DynamicRuntime.Trace))!, Expression.Constant("f"), Y);
        Expression getter = Expression.Property(Expression.Constant(cell), nameof(AuditCell.V));
        Expression[] bodies =
        [
            Expression.Multiply(C(10), Expression.Subtract(call, C(1))),
            Expression.Multiply(call, Expression.Subtract(Y, C(1))),
            Expression.Multiply(C(10), Expression.Subtract(C(1), getter)),
            Expression.Divide(Expression.Subtract(C(1), call), Expression.Divide(C(1), Y)),
            Expression.Add(Expression.Multiply(C(2), Expression.Subtract(getter, C(1))), Expression.Multiply(C(2), Expression.Subtract(call, C(1)))),
        ];

        foreach (Expression body in bodies)
        {
            Expression<Func<double, double, double>> simplified = Simplify(body);
            Func<double, double, double> original = Expression.Lambda<Func<double, double, double>>(body, X, Y).Compile();
            Func<double, double, double> result = simplified.Compile();
            foreach ((double x, double y) in Points)
            {
                cell.V = y;
                R4DynamicRuntime.Reset();
                double expected = original(x, y);
                List<string> expectedEffects = [.. R4DynamicRuntime.Events];
                cell.V = y;
                R4DynamicRuntime.Reset();
                double actual = result(x, y);
                Assert.AreEqual(expected, actual, 1e-9 * Math.Max(1, Math.Abs(expected)), ExpressionTraceFormatter.Format(body));
                CollectionAssert.AreEqual(expectedEffects, R4DynamicRuntime.Events, ExpressionTraceFormatter.Format(body));
            }
        }
    }

    /// <summary>
    /// A seeded campaign of arithmetic trees over two parameters and the constants 1, -1, 0, 0.5, 2, 3 and 10 (depth up to four,
    /// with explicit <c>c * (a - b)</c> nodes) terminates without any resource refusal and keeps every finite value. Before the
    /// fix, 69 of the first 3,000 seeds were refused with <see cref="ExpressionResourceLimitKind.Stack"/>; the first ones are
    /// replayed explicitly below.
    /// </summary>
    [TestMethod]
    public void GeneratedArithmetic_HasNoRuleLoop()
    {
        // Counterexamples recorded on master (aa9d9e39): Stack refusals.
        int[] recorded = [51, 75, 335, 479, 627, 639, 650, 687, 723, 853, 890, 1026, 1163, 1217, 1219, 1305, 1315, 1519, 1579, 1639];
        foreach (int seed in recorded.Concat(Enumerable.Range(0, 1_500)))
        {
            Expression body = ArithmeticGenerator.Build(seed, X, Y);
            Expression<Func<double, double, double>> simplified;
            try
            {
                simplified = Simplify(body);
            }
            catch (ExpressionResourceLimitException exception)
            {
                Assert.Fail($"seed {seed}: {exception.Kind} refusal of {ExpressionTraceFormatter.Format(body)}");
                return;
            }

            Func<double, double, double> original = Expression.Lambda<Func<double, double, double>>(body, X, Y).Compile();
            Func<double, double, double> result = simplified.Compile();
            foreach ((double x, double y) in Points)
            {
                double expected = original(x, y), actual = result(x, y);

                // Outside finite values the S3 contract does not promise IEEE-identical results (NaN, infinities, signed zeros).
                if (!double.IsFinite(expected) || !double.IsFinite(actual)) continue;
                Assert.AreEqual(expected, actual, 1e-9 * Math.Max(1, Math.Abs(expected)), $"seed {seed}: {ExpressionTraceFormatter.Format(body)}");
            }
        }
    }

    /// <summary>The seeded generator of <see cref="GeneratedArithmetic_HasNoRuleLoop"/>.</summary>
    private static class ArithmeticGenerator
    {
        /// <summary>The constants of the leaves.</summary>
        private static readonly double[] Constants = [1, -1, 2, 10, 0.5, 3, 0];

        /// <summary>Builds the tree of a seed (depth <c>1 + seed % 4</c>).</summary>
        /// <param name="seed">The seed.</param>
        /// <param name="x">The first parameter.</param>
        /// <param name="y">The second parameter.</param>
        /// <returns>The tree.</returns>
        public static Expression Build(int seed, ParameterExpression x, ParameterExpression y)
            => Build(new Random(seed), 1 + seed % 4, x, y);

        /// <summary>Builds a random subtree.</summary>
        /// <param name="random">The random source.</param>
        /// <param name="depth">The remaining depth.</param>
        /// <param name="x">The first parameter.</param>
        /// <param name="y">The second parameter.</param>
        /// <returns>The subtree.</returns>
        private static Expression Build(Random random, int depth, ParameterExpression x, ParameterExpression y)
        {
            if (depth <= 0 || random.Next(4) == 0)
            {
                int leaf = random.Next(3);
                return leaf == 0 ? x : leaf == 1 ? y : Expression.Constant(Constants[random.Next(Constants.Length)]);
            }

            Expression left = Build(random, depth - 1, x, y), right = Build(random, depth - 1, x, y);
            int node = random.Next(9);
            return node switch
            {
                0 or 1 => Expression.Add(left, right),
                2 or 3 => Expression.Subtract(left, right),
                4 or 5 => Expression.Multiply(left, right),
                6 => Expression.Divide(left, right),
                7 => Expression.Negate(left),
                _ => Expression.Multiply(Expression.Constant(Constants[random.Next(Constants.Length)]), Expression.Subtract(left, right)),
            };
        }
    }
}
