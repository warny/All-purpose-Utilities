using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Characterization of the result of one call of the built-in <see cref="ExpressionSimplifier"/> on grouped quotient chains
/// (<c>subdivR</c>: <c>1 - (x1/x0 - (x2/x0 - …))</c>, <c>subdivB</c>: <c>1 - ((x1/x0 + x2/x0) + …)</c>, and their mixed-sign,
/// distinct-denominator and <see cref="float"/> variants), captured before the grouped quotient family of
/// <c>docs/expressions/SimplifierFlatten.md</c> §16 existed: structure after one and two passes under every policy, values on
/// special operands, exceptions of the integral variants and order of observable evaluations. Every expectation is a literal,
/// so the class pins the historical behavior whichever path computes it.
/// </summary>
[TestClass]
public class ExpressionSimplifierGroupedQuotientReferenceTests
{
    /// <summary>The policies of the brief.</summary>
    private static readonly ExpressionSimplifierOptions[] Policies =
    [
        ExpressionSimplifierOptions.Default,
        new() { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations },
        new() { AggressiveSimplification = true },
        new() { ExceptionPreservation = ExpressionExceptionPreservation.Runtime | ExpressionExceptionPreservation.UserCode },
        new() { ExceptionPreservation = ExpressionExceptionPreservation.None },
        new() { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations, AggressiveSimplification = true, ExceptionPreservation = ExpressionExceptionPreservation.None },
    ];

    /// <summary>
    /// Renders a tree in infix form with full parentheses: parameters by name, constants with their type suffix
    /// (<c>f</c>, <c>L</c>, <c>m</c>) and round-trip digits, operators by symbol, any other node by its node type.
    /// </summary>
    /// <param name="expression">The tree.</param>
    /// <returns>The rendering.</returns>
    private static string Infix(Expression expression)
    {
        var builder = new StringBuilder();
        var pending = new Stack<object>();
        pending.Push(expression);
        while (pending.Count > 0)
        {
            switch (pending.Pop())
            {
                case string text:
                    builder.Append(text);
                    break;
                case ParameterExpression parameter:
                    builder.Append(parameter.Name);
                    break;
                case ConstantExpression { Value: double value }:
                    builder.Append(value.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case ConstantExpression { Value: float value }:
                    builder.Append(value.ToString("R", CultureInfo.InvariantCulture)).Append('f');
                    break;
                case ConstantExpression { Value: long value }:
                    builder.Append(value.ToString(CultureInfo.InvariantCulture)).Append('L');
                    break;
                case ConstantExpression constant:
                    builder.Append(Convert.ToString(constant.Value, CultureInfo.InvariantCulture));
                    break;
                case UnaryExpression { NodeType: ExpressionType.Negate } negation:
                    builder.Append("-(");
                    pending.Push(")");
                    pending.Push(negation.Operand);
                    break;
                case BinaryExpression binary:
                    string symbol = binary.NodeType switch
                    {
                        ExpressionType.Add => " + ",
                        ExpressionType.Subtract => " - ",
                        ExpressionType.Multiply => " * ",
                        ExpressionType.Divide => " / ",
                        _ => " " + binary.NodeType + " ",
                    };
                    builder.Append('(');
                    pending.Push(")");
                    pending.Push(binary.Right);
                    pending.Push(symbol);
                    pending.Push(binary.Left);
                    break;
                case Expression other:
                    builder.Append('<').Append(other.NodeType).Append('>');
                    break;
            }
        }

        return builder.ToString();
    }

    /// <summary>Builds a family of the S5 harness (<c>tests/ExpressionSimplifierAudit/Corpus.cs</c>).</summary>
    /// <param name="family">The family.</param>
    /// <param name="n">The number of quotients.</param>
    /// <returns>The tree.</returns>
    private static Expression Family(string family, int n)
    {
        Type type = family switch { "subdivRF" => typeof(float), "subdivRL" => typeof(long), _ => typeof(double) };
        ParameterExpression x0 = Expression.Parameter(type, "x0");
        Expression[] quotients = [.. Enumerable.Range(1, n).Select(i => (Expression)Expression.Divide(
            Expression.Parameter(type, "x" + i),
            family == "subdivRD" ? Expression.Parameter(type, "y" + i) : x0))];
        Expression one = Expression.Constant(Convert.ChangeType(1, type, CultureInfo.InvariantCulture), type);
        return family switch
        {
            "subdivB" => Expression.Subtract(one, Balanced(quotients, 0, n, false, 0)),
            "subdivBM" => Expression.Subtract(one, Balanced(quotients, 0, n, true, 0)),
            _ => Expression.Subtract(one, Enumerable.Reverse(quotients).Aggregate((right, left) => Expression.Subtract(left, right))),
        };
    }

    /// <summary>A balanced tree over <c>items[from..to)</c>, whose odd levels subtract their right operand when <paramref name="mixed"/>.</summary>
    /// <param name="items">The items.</param>
    /// <param name="from">The first item.</param>
    /// <param name="to">The end of the range.</param>
    /// <param name="mixed">Whether odd levels subtract.</param>
    /// <param name="level">The level of the node.</param>
    /// <returns>The tree.</returns>
    private static Expression Balanced(IReadOnlyList<Expression> items, int from, int to, bool mixed, int level)
    {
        if (to - from == 1) return items[from];
        int middle = from + (to - from) / 2;
        Expression left = Balanced(items, from, middle, mixed, level + 1);
        Expression right = Balanced(items, middle, to, mixed, level + 1);
        return mixed && level % 2 == 1 ? Expression.Subtract(left, right) : Expression.Add(left, right);
    }

    /// <summary>The pinned results: family, size, first pass, second pass.</summary>
    private static readonly (string Family, int N, string First, string Second)[] Pinned =
    [
        ("subdivR", 4,
            "(1 + ((0 - (x1 / x0)) + ((0 - (x3 / x0)) + ((x2 / x0) + (x4 / x0)))))",
            "(1 + ((0 - (x3 / x0)) + ((0 - (x1 / x0)) + ((x2 / x0) + (x4 / x0)))))"),
        ("subdivR", 7,
            "(1 + ((0 - (x1 / x0)) + ((0 - (x3 / x0)) + ((0 - (x5 / x0)) + ((0 - (x7 / x0)) + ((x2 / x0) + ((x4 / x0) + (x6 / x0))))))))",
            "(1 + ((0 - (x7 / x0)) + ((0 - (x3 / x0)) + ((0 - (x5 / x0)) + ((0 - (x1 / x0)) + ((x2 / x0) + ((x4 / x0) + (x6 / x0))))))))"),
        ("subdivB", 4,
            "(1 + ((0 - (x1 / x0)) + ((0 - (x2 / x0)) + ((0 - (x3 / x0)) + (0 - (x4 / x0))))))",
            "(1 + ((0 - (x1 / x0)) + ((0 - (x2 / x0)) + ((0 - (x3 / x0)) + (0 - (x4 / x0))))))"),
        ("subdivB", 7,
            "(1 + ((0 - (x1 / x0)) + ((0 - (x2 / x0)) + ((0 - (x3 / x0)) + ((0 - (x4 / x0)) + ((0 - (x5 / x0)) + ((0 - (x6 / x0)) + (0 - (x7 / x0)))))))))",
            "(1 + ((0 - (x1 / x0)) + ((0 - (x2 / x0)) + ((0 - (x3 / x0)) + ((0 - (x4 / x0)) + ((0 - (x5 / x0)) + ((0 - (x6 / x0)) + (0 - (x7 / x0)))))))))"),
        ("subdivBM", 8,
            "(1 + ((0 - (x1 / x0)) + ((0 - (x2 / x0)) + ((0 - (x5 / x0)) + ((0 - (x6 / x0)) + ((x3 / x0) + ((x4 / x0) + ((x7 / x0) + (x8 / x0)))))))))",
            "(1 + ((0 - (x6 / x0)) + ((0 - (x2 / x0)) + ((0 - (x5 / x0)) + ((0 - (x1 / x0)) + ((x3 / x0) + ((x4 / x0) + ((x7 / x0) + (x8 / x0)))))))))"),
        ("subdivRD", 5,
            "(1 + ((0 - (x1 / y1)) + ((0 - (x3 / y3)) + ((0 - (x5 / y5)) + ((x2 / y2) + (x4 / y4))))))",
            "(1 + ((0 - (x3 / y3)) + ((0 - (x5 / y5)) + ((0 - (x1 / y1)) + ((x2 / y2) + (x4 / y4))))))"),
        ("subdivRF", 4,
            "(1f + ((0f - (x1 / x0)) + ((0f - (x3 / x0)) + ((x2 / x0) + (x4 / x0)))))",
            "(1f + ((0f - (x3 / x0)) + ((0f - (x1 / x0)) + ((x2 / x0) + (x4 / x0)))))"),
        ("subdivRL", 4,
            "(1L + ((0L - (x1 / x0)) + ((x2 / x0) + ((0L - (x3 / x0)) + (x4 / x0)))))",
            "(1L + ((0L - (x1 / x0)) + ((x2 / x0) + ((0L - (x3 / x0)) + (x4 / x0)))))"),
    ];

    /// <summary>
    /// The structure after one and two passes, identical under every policy. With mixed signs the second pass reorders the
    /// subtracted terms: free parameters tie in the canonical order (S4, docs/expressions/SimplifierFlatten.md §14.7, §15.9).
    /// </summary>
    [TestMethod]
    public void Structure_IsPinned_UnderEveryPolicy()
    {
        foreach ((string family, int n, string first, string second) in Pinned)
        {
            Expression source = Family(family, n);
            foreach (ExpressionSimplifierOptions policy in Policies)
            {
                var simplifier = new ExpressionSimplifier { Options = policy };
                Expression result = simplifier.Simplify(source);
                Assert.AreEqual(first, Infix(result), $"{family} {n} under {policy}");
                Assert.AreEqual(second, Infix(simplifier.Simplify(result)), $"{family} {n} (second pass) under {policy}");
            }
        }
    }

    /// <summary>The parameters of a family tree, in order of first occurrence.</summary>
    /// <param name="tree">The tree.</param>
    /// <returns>The parameters.</returns>
    private static ParameterExpression[] ParametersOf(Expression tree)
    {
        var found = new List<ParameterExpression>();
        var pending = new Stack<Expression>();
        pending.Push(tree);
        while (pending.Count > 0)
        {
            switch (pending.Pop())
            {
                case ParameterExpression parameter when !found.Contains(parameter):
                    found.Add(parameter);
                    break;
                case BinaryExpression binary:
                    pending.Push(binary.Right);
                    pending.Push(binary.Left);
                    break;
                case UnaryExpression unary:
                    pending.Push(unary.Operand);
                    break;
                case MethodCallExpression call:
                    foreach (Expression argument in call.Arguments.Reverse()) pending.Push(argument);
                    break;
            }
        }

        return [.. found];
    }

    /// <summary>
    /// On finite, zero, negative zero, infinite, NaN, tiny and huge operands and zero divisors, the result evaluates like the
    /// source within the reassociation latitude (S3): equal, or both non-finite, or within a relative <c>1e-9</c>.
    /// </summary>
    [TestMethod]
    public void Values_StayWithinTheReassociationLatitude()
    {
        double[] samples = [3.0, -2.0, 0.0, -0.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.MaxValue, double.Epsilon, 1e-310, 1e300, -7.5];
        foreach (string family in new[] { "subdivR", "subdivB", "subdivBM", "subdivRD" })
        {
            Expression source = Family(family, 6);
            ParameterExpression[] parameters = ParametersOf(source);
            Delegate f = Expression.Lambda(source, parameters).Compile();
            Delegate g = Expression.Lambda(new ExpressionSimplifier().Simplify(source), parameters).Compile();
            var random = new Random(family.Length);
            for (int sample = 0; sample < 400; sample++)
            {
                object[] values = [.. parameters.Select(_ => (object)samples[random.Next(samples.Length)])];
                double expected = (double)f.DynamicInvoke(values)!;
                double actual = (double)g.DynamicInvoke(values)!;
                // The S3 latitude: a reassociated sum may round differently, and with extreme operands (a huge value, or a tiny
                // divisor making a huge quotient) it may cancel, overflow or produce NaN at another point than the source.
                bool extreme = values.Any(static v => Math.Abs((double)v) > 1e290 || ((double)v != 0 && Math.Abs((double)v) < 1e-290));
                bool close = extreme || expected.Equals(actual) || !double.IsFinite(expected) || !double.IsFinite(actual)
                    || Math.Abs(expected) > 1e290 || Math.Abs(expected - actual) <= 1e-9 * Math.Max(1, Math.Abs(expected));
                Assert.IsTrue(close, $"{family} at [{string.Join(", ", values)}]: {expected} -> {actual}");
            }
        }
    }

    /// <summary>The integral variant keeps its divisions and throws exactly where the source throws (a zero divisor).</summary>
    [TestMethod]
    public void IntegralVariant_KeepsItsExceptions()
    {
        Expression source = Family("subdivRL", 4);
        ParameterExpression[] parameters = ParametersOf(source);
        Delegate f = Expression.Lambda(source, parameters).Compile();
        Delegate g = Expression.Lambda(new ExpressionSimplifier().Simplify(source), parameters).Compile();
        foreach (long divisor in new[] { 0L, 1L, -1L, 7L })
        {
            object[] values = [.. parameters.Select((p, i) => (object)(p.Name == "x0" ? divisor : 10L * i + 3))];
            Assert.AreEqual(Run(f, values), Run(g, values), divisor.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>Invokes <paramref name="compiled"/> and renders its value or the type of its exception.</summary>
    /// <param name="compiled">The delegate.</param>
    /// <param name="values">The arguments.</param>
    /// <returns>The rendering.</returns>
    private static string Run(Delegate compiled, object[] values)
    {
        try
        {
            return Convert.ToString(compiled.DynamicInvoke(values), CultureInfo.InvariantCulture)!;
        }
        catch (TargetInvocationException exception)
        {
            return exception.InnerException!.GetType().Name;
        }
    }

    /// <summary>The log of <see cref="Observed"/>.</summary>
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

    /// <summary>
    /// Observable evaluations inside and around a grouped chain happen as often and in the order of the source, and the
    /// simplification runs none of them.
    /// </summary>
    [TestMethod]
    public void ObservableEvaluations_AreKept()
    {
        MethodInfo observed = typeof(ExpressionSimplifierGroupedQuotientReferenceTests).GetMethod(nameof(Observed))!;
        Expression group = Family("subdivR", 4);
        ParameterExpression[] parameters = ParametersOf(group);
        Expression[] sources =
        [
            Expression.Add(Expression.Call(observed, parameters[1]), group),
            Expression.Subtract(group, Expression.Call(observed, parameters[2])),
            Expression.Subtract(Expression.Constant(1.0), Expression.Subtract(Expression.Divide(parameters[1], parameters[0]), Expression.Call(observed, parameters[3]))),
        ];

        foreach (Expression source in sources)
        {
            ParameterExpression[] all = ParametersOf(source);
            s_log = [];
            Expression result = new ExpressionSimplifier().Simplify(source);
            Assert.AreEqual(0, s_log.Count, "the simplification runs no user code");
            Delegate f = Expression.Lambda(source, all).Compile();
            Delegate g = Expression.Lambda(result, all).Compile();
            object[] values = [.. all.Select((_, i) => (object)(2.0 + i))];
            s_log = [];
            f.DynamicInvoke(values);
            List<double> expected = s_log;
            s_log = [];
            g.DynamicInvoke(values);
            CollectionAssert.AreEqual(expected, s_log, source.ToString());
        }
    }
}
