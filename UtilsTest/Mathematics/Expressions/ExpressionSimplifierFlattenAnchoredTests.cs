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
/// The anchored chains of the flatten rule of <see cref="ExpressionSimplifier"/> (Flatten mission C2,
/// <c>docs/expressions/SimplifierFlatten.md</c> §10): a chain of linear terms and one call that is not inert, held by a node of
/// the spine, is simplified at once under the historical admission, with exactly the level by level result, the same
/// evaluations and the same user code; every other chain, and every chain under an explicit policy, keeps the treatment of
/// mission C1.
/// </summary>
/// <remarks>
/// The reference of every comparison is <see cref="LevelByLevelSimplifier"/>, a derived simplifier without any member, which
/// keeps the level by level treatment. Trees are compared through <see cref="Dump"/> (node types, static types, operator
/// methods, exact constants, parameter instances).
/// </remarks>
[TestClass]
public class ExpressionSimplifierFlattenAnchoredTests
{
    /// <summary>A derived simplifier without any member: it keeps the level by level treatment.</summary>
    private sealed class LevelByLevelSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>Free <see cref="double"/> parameters.</summary>
    private static readonly ParameterExpression[] D = [.. Enumerable.Range(0, 128).Select(static i => Expression.Parameter(typeof(double), "d" + i))];

    /// <summary>Free <see cref="int"/> parameters.</summary>
    private static readonly ParameterExpression[] I = [.. Enumerable.Range(0, 32).Select(static i => Expression.Parameter(typeof(int), "i" + i))];

    /// <summary>The number of calls of <see cref="Unknown"/> and <see cref="UnknownInt"/>.</summary>
    [ThreadStatic]
    private static int s_calls;

    /// <summary>A function the effect analysis does not know: its calls are observable.</summary>
    /// <returns>The number of calls so far.</returns>
    public static double Unknown() => ++s_calls;

    /// <summary>A function of one argument the effect analysis does not know.</summary>
    /// <param name="value">The argument.</param>
    /// <returns>The argument plus the number of calls so far.</returns>
    public static double UnknownOf(double value) => value + ++s_calls;

    /// <summary>An <see cref="int"/> function the effect analysis does not know.</summary>
    /// <returns>The number of calls so far.</returns>
    public static int UnknownInt() => ++s_calls;

    /// <summary>A function that always throws.</summary>
    /// <returns>Never returns.</returns>
    public static double Throwing() => throw new InvalidOperationException("Throwing was evaluated.");

    /// <summary>A value whose equality members are logged: user code a simplification may run.</summary>
    private sealed class LoggedValue
    {
        /// <summary>The calls of <see cref="Equals(object?)"/> and <see cref="GetHashCode"/>, in order.</summary>
        [ThreadStatic]
        private static List<string>? s_log;

        /// <summary>Gets the log of this thread.</summary>
        public static List<string> Log => s_log ??= [];

        /// <summary>Gets the identity of the value.</summary>
        public int Id { get; init; }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            Log.Add("E" + Id);
            return obj is LoggedValue other && other.Id == Id;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            Log.Add("H" + Id);
            return Id;
        }
    }

    /// <summary>Turns a <see cref="LoggedValue"/> into a number.</summary>
    /// <param name="value">The value.</param>
    /// <returns>Its identity.</returns>
    public static double Carrier(object value) => ((LoggedValue)value).Id;

    /// <summary>Builds a call of a method of this class.</summary>
    /// <param name="name">The method name.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The call.</returns>
    private static Expression Call(string name, params Expression[] arguments)
        => Expression.Call(typeof(ExpressionSimplifierFlattenAnchoredTests).GetMethod(name)!, arguments);

    /// <summary>Builds a constant of the type of <paramref name="value"/>.</summary>
    /// <typeparam name="T">The constant type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The constant.</returns>
    private static ConstantExpression C<T>(T value) => Expression.Constant(value, typeof(T));

    /// <summary>The policies of the brief: default, declaration trust, aggressive, partial exception preservation, permissive.</summary>
    private static readonly ExpressionSimplifierOptions[] Policies =
    [
        ExpressionSimplifierOptions.Default,
        new() { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations },
        new() { AggressiveSimplification = true },
        new() { ExceptionPreservation = ExpressionExceptionPreservation.Runtime | ExpressionExceptionPreservation.UserCode },
        new() { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations, AggressiveSimplification = true, ExceptionPreservation = ExpressionExceptionPreservation.None },
    ];

    /// <summary>Renders the complete structure of a tree: node types, static types, operator methods, exact constants, parameter instances.</summary>
    /// <param name="expression">The tree.</param>
    /// <returns>The rendering.</returns>
    private static string Dump(Expression? expression)
    {
        var builder = new StringBuilder();
        Write(builder, expression);
        return builder.ToString();
    }

    /// <summary>Appends the rendering of <paramref name="expression"/> (see <see cref="Dump"/>).</summary>
    /// <param name="builder">The output.</param>
    /// <param name="expression">The tree.</param>
    private static void Write(StringBuilder builder, Expression? expression)
    {
        switch (expression)
        {
            case null:
                builder.Append("null");
                break;
            case ParameterExpression parameter:
                builder.Append(parameter.Name).Append('#').Append(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(parameter));
                break;
            case ConstantExpression constant:
                builder.Append('[').Append(constant.Type.Name).Append(':')
                    .Append(constant.Value switch
                    {
                        double value => BitConverter.DoubleToInt64Bits(value).ToString("x16", CultureInfo.InvariantCulture),
                        LoggedValue value => "logged" + value.Id,
                        IFormattable value => value.ToString(null, CultureInfo.InvariantCulture),
                        null => "null",
                        _ => constant.Value.GetType().Name,
                    })
                    .Append(']');
                break;
            case BinaryExpression binary:
                builder.Append('(').Append(binary.NodeType).Append('<').Append(binary.Type.Name).Append('>');
                if (binary.Method is not null) builder.Append('!').Append(binary.Method.Name);
                builder.Append(' ');
                Write(builder, binary.Left);
                builder.Append(' ');
                Write(builder, binary.Right);
                builder.Append(')');
                break;
            case UnaryExpression unary:
                builder.Append('(').Append(unary.NodeType).Append('<').Append(unary.Type.Name).Append("> ");
                Write(builder, unary.Operand);
                builder.Append(')');
                break;
            case MethodCallExpression call:
                builder.Append(call.Method.DeclaringType!.Name).Append('.').Append(call.Method.Name).Append('(');
                foreach (Expression argument in call.Arguments)
                {
                    Write(builder, argument);
                    builder.Append(',');
                }

                builder.Append(')');
                break;
            case LambdaExpression lambda:
                builder.Append("lambda(");
                foreach (ParameterExpression parameter in lambda.Parameters) Write(builder, parameter);
                builder.Append(")=>");
                Write(builder, lambda.Body);
                break;
            default:
                builder.Append('<').Append(expression.GetType().Name).Append('>');
                break;
        }
    }

    /// <summary>Builds a left-associated sum of <paramref name="terms"/>.</summary>
    /// <param name="terms">The terms.</param>
    /// <returns>The sum.</returns>
    private static Expression LeftSum(IEnumerable<Expression> terms) => terms.Aggregate(static (left, right) => Expression.Add(left, right));

    /// <summary>Builds a right-associated sum of <paramref name="terms"/>.</summary>
    /// <param name="terms">The terms.</param>
    /// <returns>The sum.</returns>
    private static Expression RightSum(IEnumerable<Expression> terms) => terms.Reverse().Aggregate(static (right, left) => Expression.Add(left, right));

    /// <summary>
    /// Builds a comb: <paramref name="anchor"/> at the bottom, then each term added on the side given by
    /// <paramref name="sides"/> (bit <c>i</c> set: the term is the left operand of level <c>i</c>), subtracted when the bit
    /// of <paramref name="subtractions"/> is set and the term is on the right.
    /// </summary>
    /// <param name="anchor">The bottom operand.</param>
    /// <param name="terms">The terms, from the bottom level up.</param>
    /// <param name="sides">The sides.</param>
    /// <param name="subtractions">The subtractions.</param>
    /// <returns>The comb.</returns>
    private static Expression Comb(Expression anchor, IReadOnlyList<Expression> terms, long sides, long subtractions)
    {
        Expression e = anchor;
        for (int i = 0; i < terms.Count; i++)
        {
            bool left = (sides >> i & 1) != 0;
            bool subtract = !left && (subtractions >> i & 1) != 0;
            e = left ? Expression.Add(terms[i], e) : subtract ? Expression.Subtract(e, terms[i]) : Expression.Add(e, terms[i]);
        }

        return e;
    }

    /// <summary>Runs one simplification with the admission counters installed.</summary>
    /// <param name="simplifier">The simplifier.</param>
    /// <param name="tree">The tree.</param>
    /// <param name="forceFallback">Whether anchored chains take the level by level fallback.</param>
    /// <returns>The result and the counters.</returns>
    private static (Expression Result, FlattenAdmissionStatistics Statistics) Measure(ExpressionSimplifier simplifier, Expression tree, bool forceFallback = false)
    {
        var statistics = new FlattenAdmissionStatistics { ForceAnchoredFallback = forceFallback };
        ExpressionSimplifier.AdmissionStatistics = statistics;
        try
        {
            return (simplifier.Simplify(tree), statistics);
        }
        finally
        {
            ExpressionSimplifier.AdmissionStatistics = null;
        }
    }

    /// <summary>
    /// Asserts that <paramref name="source"/> is an anchored chain: under the default policy it is accepted at its root and
    /// gives the level by level result, for the first and the second pass, with the same user code and through the fallback
    /// too; under every other policy it is refused and gives the level by level result of that policy.
    /// </summary>
    /// <param name="label">The case.</param>
    /// <param name="source">The source chain.</param>
    private static void AssertAnchored(string label, Expression source)
    {
        LoggedValue.Log.Clear();
        Expression expected = new LevelByLevelSimplifier().Simplify(source);
        string expectedLog = string.Join(",", LoggedValue.Log);
        string expectedSecond = Dump(new LevelByLevelSimplifier().Simplify(expected));

        LoggedValue.Log.Clear();
        (Expression result, FlattenAdmissionStatistics statistics) = Measure(new ExpressionSimplifier(), source);
        Assert.AreEqual(1, statistics.Anchored, label + ": the root must be admitted as an anchored chain");
        Assert.AreEqual(1, statistics.AnchoredCanonicalizations, label);
        Assert.AreEqual(1 + statistics.Accepted, statistics.Admissions, label + ": below the root, only the linear operand of the anchor's node is offered");
        Assert.AreEqual(Dump(expected), Dump(result), label);
        Assert.AreEqual(expectedLog, string.Join(",", LoggedValue.Log), label + ": user code");
        Assert.AreEqual(expectedSecond, Dump(new ExpressionSimplifier().Simplify(result)), label + " (second pass)");

        LoggedValue.Log.Clear();
        (Expression fallback, FlattenAdmissionStatistics fallbackStatistics) = Measure(new ExpressionSimplifier(), source, forceFallback: true);
        Assert.AreEqual(1, fallbackStatistics.AnchoredFallbacks, label);
        Assert.AreEqual(Dump(expected), Dump(fallback), label + " (fallback)");
        Assert.AreEqual(expectedLog, string.Join(",", LoggedValue.Log), label + ": user code (fallback)");

        foreach (ExpressionSimplifierOptions policy in Policies.Skip(1))
        {
            (Expression policyResult, FlattenAdmissionStatistics policyStatistics) = Measure(new ExpressionSimplifier { Options = policy }, source);
            Assert.AreEqual(0, policyStatistics.Anchored, label + " under " + policy);
            Assert.AreEqual(Dump(new LevelByLevelSimplifier { Options = policy }.Simplify(source)), Dump(policyResult), label + " under " + policy);
        }
    }

    /// <summary>
    /// Asserts that <paramref name="source"/> is not an anchored chain at its root and keeps the level by level result.
    /// </summary>
    /// <param name="label">The case.</param>
    /// <param name="source">The source chain.</param>
    private static void AssertNotAnchoredAtRoot(string label, Expression source)
    {
        LoggedValue.Log.Clear();
        string expected = Dump(new LevelByLevelSimplifier().Simplify(source));
        string expectedLog = string.Join(",", LoggedValue.Log);
        LoggedValue.Log.Clear();
        var statistics = new FlattenAdmissionStatistics();
        var simplifier = new ExpressionSimplifier();
        bool rootAdmitted = ExpressionResourceBudget.Run(ExpressionResourceLimits.Unlimited, null, null, null, (simplifier, source), static state
            => state.simplifier.FlattenAdmission(state.source) == ExpressionRuleRejection.None);
        Assert.IsFalse(rootAdmitted, label + ": the root must be refused");
        (Expression result, _) = Measure(new ExpressionSimplifier(), source);
        Assert.AreEqual(expected, Dump(result), label);
        Assert.AreEqual(expectedLog, string.Join(",", LoggedValue.Log), label + ": user code");
    }

    /// <summary>
    /// The families of the brief and their variants are anchored chains: <c>Function() + x0 + ... + xN</c> in a left or a
    /// right grouping and in combs, <c>x0 + ... + x50 + Function() + x51 + ... + xN</c> (the linear prefix is a unit), an
    /// anchor with arguments, a throwing call, a call that may throw, subtracted terms, a subtracted anchor, products by
    /// coefficients, one or two constants, <see cref="int"/> chains and lambda-bound parameters.
    /// </summary>
    [TestMethod]
    public void AnchoredChains_GiveTheLevelByLevelResult()
    {
        MethodInfo clamp = typeof(double).GetMethod(nameof(double.Clamp), [typeof(double), typeof(double), typeof(double)])!;
        var random = new Random(20261115);
        var cases = new List<(string Label, Expression Source)>
        {
            ("F + d1", Expression.Add(Call(nameof(Unknown)), D[1])),
            ("d1 - F", Expression.Subtract(D[1], Call(nameof(Unknown)))),
            ("left F + d1 + ... + d40", LeftSum([Call(nameof(Unknown)), .. D.Skip(1).Take(40)])),
            ("right d0 + ... + d39 + F", RightSum([.. D.Take(40), Call(nameof(Unknown))])),
            ("left middle", LeftSum([.. D.Take(20), Call(nameof(Unknown)), .. D.Skip(21).Take(20)])),
            ("left middle, linear prefix with constants", LeftSum([C(2.0), .. D.Take(10), Call(nameof(Unknown)), .. D.Skip(21).Take(5)])),
            ("left F(d0) with coefficients", LeftSum([Call(nameof(UnknownOf), D[0]), Expression.Multiply(C(3.0), D[1]), Expression.Multiply(D[2], C(5.0)), Expression.Multiply(D[3], D[4]), C(7.0), D[5]])),
            ("left throwing", LeftSum([Call(nameof(Throwing)), D[1], D[2], C(1.0)])),
            ("left clamp (may throw)", LeftSum([Expression.Call(clamp, D[0], C(1.0), C(2.0)), D[1], D[2], D[3]])),
            ("left two constants cancelling", LeftSum([Call(nameof(Unknown)), C(5.0), D[1], C(-5.0)])),
            ("left carrier", LeftSum([Call(nameof(Carrier), Expression.Constant(new LoggedValue { Id = 1 }, typeof(object))), D[1], D[2]])),
            ("int left", LeftSum([Expression.Call(typeof(ExpressionSimplifierFlattenAnchoredTests).GetMethod(nameof(UnknownInt))!), .. I.Skip(1).Take(20), C(3)])),
            ("int subtractions", Comb(Expression.Call(typeof(ExpressionSimplifierFlattenAnchoredTests).GetMethod(nameof(UnknownInt))!), [.. I.Skip(1).Take(12)], 0b001001001001, 0b110110110110)),
            ("bound", Expression.Lambda(LeftSum([Call(nameof(Unknown)), .. D.Skip(1).Take(10)]), D.Skip(1).Take(10))),
        };

        for (int c = 0; c < 60; c++)
        {
            int count = random.Next(1, 30);
            var terms = new List<Expression>();
            for (int i = 0; i < count; i++)
            {
                terms.Add(random.Next(4) switch
                {
                    0 => Expression.Multiply(C(i + 2.0), D[i + 1]),
                    1 => Expression.Multiply(D[i + 1], D[i + 60]),
                    _ => D[i + 1],
                });
            }

            if (random.Next(3) == 0) terms[random.Next(count)] = C(random.Next(1, 4) - 0.5);
            Expression anchor = random.Next(2) == 0 ? Call(nameof(Unknown)) : Expression.Call(clamp, D[0], C(-1.0), C(1.0));
            cases.Add(($"comb {c}", Comb(anchor, terms, random.NextInt64(), random.NextInt64())));
        }

        foreach ((string label, Expression source) in cases)
        {
            if (source is LambdaExpression lambda)
            {
                LoggedValue.Log.Clear();
                Assert.AreEqual(Dump(new LevelByLevelSimplifier().Simplify(source)), Dump(new ExpressionSimplifier().Simplify(source)), label);
                Assert.AreEqual(1, Measure(new ExpressionSimplifier(), source).Statistics.Anchored, label);
                continue;
            }

            AssertAnchored(label, source);
        }
    }

    /// <summary>
    /// The other chains with a call keep the treatment of mission C1, and the level by level result: an inert call
    /// (<c>Math.Sin</c>, whose rules may apply), two anchors, an anchor below a node with two crossed operands, an anchor with
    /// a repeated parameter or three constants, a subtracted group above the anchor, a negated call, an anchor whose method
    /// is not a runtime reflection object.
    /// </summary>
    [TestMethod]
    public void OtherChainsWithCalls_KeepTheirTreatment()
    {
        MethodInfo sin = typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!;
        Expression f = Call(nameof(Unknown));
        var cases = new (string Label, Expression Source)[]
        {
            ("sin", LeftSum([Expression.Call(sin, D[0]), D[1], D[2], D[3]])),
            ("two anchors", LeftSum([Call(nameof(Unknown)), D[1], Call(nameof(Unknown)), D[2]])),
            ("anchor below a branch", Expression.Add(LeftSum([Call(nameof(Unknown)), D[1]]), LeftSum([D[2], D[3]]))),
            ("anchor and repeated parameter", LeftSum([Call(nameof(Unknown)), D[1], D[2], D[1]])),
            ("anchor and three constants", LeftSum([Call(nameof(Unknown)), C(1.0), D[1], C(2.0), C(3.0)])),
            ("subtracted group above the anchor", Expression.Subtract(D[0], LeftSum([Call(nameof(Unknown)), D[1]]))),
            ("negated call", LeftSum([Expression.Negate(Call(nameof(Unknown))), D[1], D[2]])),
            ("anchor and a zero", LeftSum([Call(nameof(Unknown)), C(0.0), D[1]])),
            ("anchor and an equal coefficient", LeftSum([Call(nameof(Unknown)), Expression.Multiply(C(2.0), D[1]), Expression.Multiply(C(2.0), D[2])])),
        };

        foreach ((string label, Expression source) in cases) AssertNotAnchoredAtRoot(label, source);
    }

    /// <summary>
    /// An anchored chain keeps every evaluation: compiled, the simplified chain calls the anchor as many times as the source
    /// and throws the same exception.
    /// </summary>
    [TestMethod]
    public void AnchoredChains_KeepEvaluations()
    {
        ParameterExpression[] parameters = [.. D.Take(4)];
        Expression counted = LeftSum([Call(nameof(Unknown)), D[1], Expression.Multiply(C(3.0), D[2]), D[3]]);
        var simplified = (Expression<Func<double, double, double, double, double>>)new ExpressionSimplifier().Simplify(Expression.Lambda<Func<double, double, double, double, double>>(counted, parameters));
        s_calls = 0;
        double expected = Expression.Lambda<Func<double, double, double, double, double>>(counted, parameters).Compile()(1, 2, 3, 4);
        s_calls = 0;
        Assert.AreEqual(expected, simplified.Compile()(1, 2, 3, 4));
        Assert.AreEqual(1, s_calls);

        Expression throwing = LeftSum([Call(nameof(Throwing)), D[1], D[2], D[3]]);
        var simplifiedThrowing = (Expression<Func<double, double, double, double, double>>)new ExpressionSimplifier().Simplify(Expression.Lambda<Func<double, double, double, double, double>>(throwing, parameters));
        Assert.ThrowsExactly<InvalidOperationException>(() => simplifiedThrowing.Compile()(1, 2, 3, 4));
    }

    /// <summary>
    /// An anchored chain is walked once and simplified by one canonicalization: <c>Function() + x1 + ... + x99</c> is one
    /// admission and one walk of its 99 nodes; its transformation work is one visit for the chain and one per operand.
    /// Under an explicit policy the chain is refused, and every level below it is refused by the table of that policy.
    /// </summary>
    [TestMethod]
    public void AnchoredChain_IsWalkedOnce()
    {
        const int N = 100;
        Expression tree = LeftSum([Call(nameof(Unknown)), .. D.Skip(1).Take(N - 1)]);
        (_, FlattenAdmissionStatistics statistics) = Measure(new ExpressionSimplifier(), tree);
        Assert.AreEqual(1, statistics.Admissions);
        Assert.AreEqual(1, statistics.Walks);
        Assert.AreEqual(N - 1, statistics.NodesExamined);

        long transform = ExpressionResourceBudget.Run(ExpressionResourceLimits.Unlimited, null, null, null, tree, static source =>
        {
            new ExpressionSimplifier().Simplify(source);
            return ExpressionResourceBudget.TransformVisits;
        });
        Assert.AreEqual(1 + N, transform);

        (_, FlattenAdmissionStatistics policy) = Measure(new ExpressionSimplifier { Options = Policies[1] }, tree);
        Assert.AreEqual(1, policy.Causes[(int)FlattenRefusalCause.AnchoredUnderPolicy]);
        Assert.AreEqual(1, policy.Walks);
        Assert.AreEqual(N - 2, policy.KnownRefusals);
    }
}
