using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// The flatten rule of <see cref="ExpressionSimplifier"/> (PR B, <c>docs/expressions/SimplifierFlatten.md</c>): a linear
/// additive chain is canonicalized once from its terms and gives exactly the result of the level by level treatment; every
/// other chain is declined before anything is prepared; a derived simplifier keeps the level by level treatment.
/// </summary>
/// <remarks>
/// The reference of every comparison is <see cref="LevelByLevelSimplifier"/>, a derived simplifier without any member: its
/// plan does not hold the private flatten rule, so it simplifies level by level, as every simplifier did before the rule
/// existed. Trees are compared through <see cref="Dump"/> (node types, static types, operator methods, exact constant
/// values and parameter instances), never through <see cref="Expression.ToString()"/>.
/// </remarks>
[TestClass]
public class ExpressionSimplifierFlattenTests
{
    /// <summary>The name of the flatten rule of the simplifier.</summary>
    private const string FlattenRule = "FlattenAdditiveChain";

    /// <summary>A derived simplifier without any member: it keeps the level by level treatment.</summary>
    private sealed class LevelByLevelSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>Free <see cref="double"/> parameters.</summary>
    private static readonly ParameterExpression[] D = [.. Enumerable.Range(0, 80).Select(static i => Expression.Parameter(typeof(double), "d" + i))];

    /// <summary>Free <see cref="int"/> parameters.</summary>
    private static readonly ParameterExpression[] I = [.. Enumerable.Range(0, 12).Select(static i => Expression.Parameter(typeof(int), "i" + i))];

    /// <summary>Free <see cref="uint"/> parameters.</summary>
    private static readonly ParameterExpression[] U = [.. Enumerable.Range(0, 12).Select(static i => Expression.Parameter(typeof(uint), "u" + i))];

    /// <summary>Free <see cref="float"/> parameters.</summary>
    private static readonly ParameterExpression[] F = [.. Enumerable.Range(0, 12).Select(static i => Expression.Parameter(typeof(float), "f" + i))];

    /// <summary>Free <see cref="long"/> parameters.</summary>
    private static readonly ParameterExpression[] L = [.. Enumerable.Range(0, 12).Select(static i => Expression.Parameter(typeof(long), "l" + i))];

    /// <summary>Free <see cref="decimal"/> parameters.</summary>
    private static readonly ParameterExpression[] M = [.. Enumerable.Range(0, 4).Select(static i => Expression.Parameter(typeof(decimal), "m" + i))];

    /// <summary>Builds a constant.</summary>
    /// <typeparam name="T">The constant type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The constant.</returns>
    private static ConstantExpression C<T>(T value) => Expression.Constant(value, typeof(T));

    /// <summary>Shorthand of <see cref="Expression.Add(Expression, Expression)"/>.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The sum.</returns>
    private static BinaryExpression Add(Expression left, Expression right) => Expression.Add(left, right);

    /// <summary>Shorthand of <see cref="Expression.Subtract(Expression, Expression)"/>.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The difference.</returns>
    private static BinaryExpression Sub(Expression left, Expression right) => Expression.Subtract(left, right);

    /// <summary>Shorthand of <see cref="Expression.Multiply(Expression, Expression)"/>.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The product.</returns>
    private static BinaryExpression Mul(Expression left, Expression right) => Expression.Multiply(left, right);

    /// <summary>A function the effect analysis does not know: its calls are never dropped, merged or moved.</summary>
    /// <returns>The number of calls so far.</returns>
    public static double Unknown() => ++UnknownCalls;

    /// <summary>The number of calls of <see cref="Unknown"/>.</summary>
    private static int UnknownCalls;

    /// <summary>A value whose equality members are logged: user code the comparisons of the simplifier may run.</summary>
    private sealed class LoggedValue
    {
        /// <summary>The calls of <see cref="Equals(object?)"/> and <see cref="GetHashCode"/>, in order.</summary>
        public static List<string> Log { get; } = [];

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

    /// <summary>Builds a call carrying a <see cref="LoggedValue"/> constant.</summary>
    /// <param name="id">The identity of the value.</param>
    /// <returns>The call.</returns>
    private static Expression Opaque(int id)
        => Expression.Call(typeof(ExpressionSimplifierFlattenTests).GetMethod(nameof(Carrier))!, Expression.Constant(new LoggedValue { Id = id }, typeof(object)));

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
                        float value => BitConverter.SingleToInt32Bits(value).ToString("x8", CultureInfo.InvariantCulture),
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
                builder.Append(call.Method.Name).Append('(');
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

    /// <summary>Simplifies <paramref name="source"/> inside a detailed trace.</summary>
    /// <param name="simplifier">The simplifier.</param>
    /// <param name="source">The source tree.</param>
    /// <returns>The result and the events.</returns>
    private static (Expression Result, ExpressionTransformEvent[] Events) Traced(ExpressionSimplifier simplifier, Expression source)
    {
        var collector = new ExpressionTraceCollector(1_000_000, ExpressionTraceRetention.KeepFirst);
        Expression result;
        using (ExpressionTransformTracing.Begin(collector, ExpressionTraceDetail.Detailed))
        {
            result = simplifier.Simplify(source);
        }

        return (result, collector.GetEvents());
    }

    /// <summary>Whether the flatten rule produced the result of the node <paramref name="root"/>.</summary>
    /// <param name="events">The events of a simplification.</param>
    /// <param name="root">A node of the source.</param>
    /// <returns><see langword="true"/> when the rule accepted the chain rooted at <paramref name="root"/>.</returns>
    private static bool AcceptedAt(ExpressionTransformEvent[] events, Expression root)
        => events.Any(e => e.Kind == ExpressionTransformEventKind.RuleApplied && e.Rule!.Name == FlattenRule && ReferenceEquals(e.Input, root));

    /// <summary>Whether the chain rooted at <paramref name="root"/> was refused by the admission of the simplifier, before any extraction.</summary>
    /// <param name="events">The events of a simplification.</param>
    /// <param name="root">A node of the source.</param>
    /// <returns><see langword="true"/> when the chain rooted at <paramref name="root"/> was not admitted.</returns>
    private static bool DeclinedAt(ExpressionTransformEvent[] events, Expression root)
        => events.Any(e => e.Kind == ExpressionTransformEventKind.RuleRejected && e.Rule!.Name == FlattenRule
            && e.Rejection == ExpressionRuleRejection.ChainNotAdmitted && ReferenceEquals(e.Input, root))
            && !events.Any(e => e.Kind == ExpressionTransformEventKind.ChainFlattened && ReferenceEquals(e.Input, root));

    /// <summary>
    /// Asserts that the chain rooted at <paramref name="source"/> is accepted and that its result, under every policy, is the
    /// one of the level by level treatment, for the first and the second pass.
    /// </summary>
    /// <param name="label">The case.</param>
    /// <param name="source">The source chain.</param>
    private static void AssertAccepted(string label, Expression source)
    {
        string expected = Dump(new LevelByLevelSimplifier().Simplify(source));
        string expectedSecond = Dump(new LevelByLevelSimplifier().Simplify(new LevelByLevelSimplifier().Simplify(source)));
        foreach (ExpressionSimplifierOptions policy in Policies)
        {
            (Expression result, ExpressionTransformEvent[] events) = Traced(new ExpressionSimplifier { Options = policy }, source);
            Assert.IsTrue(AcceptedAt(events, source), label + ": the chain must be accepted under " + policy);
            Assert.AreEqual(expected, Dump(result), label + " under " + policy);
            Assert.AreEqual(expectedSecond, Dump(new ExpressionSimplifier { Options = policy }.Simplify(result)), label + " (second pass) under " + policy);
        }

        Assert.AreEqual(expected, Dump(new ExpressionSimplifier().Simplify(source)), label + " (untraced)");
    }

    /// <summary>
    /// Asserts that the chain rooted at <paramref name="source"/> is declined by the flatten rule and that its result is the
    /// one of the level by level treatment.
    /// </summary>
    /// <param name="label">The case.</param>
    /// <param name="source">The source chain.</param>
    private static void AssertDeclined(string label, Expression source)
    {
        (Expression result, ExpressionTransformEvent[] events) = Traced(new ExpressionSimplifier(), source);
        Assert.IsTrue(DeclinedAt(events, source), label + ": the chain must be declined");
        Assert.IsFalse(AcceptedAt(events, source), label);
        Assert.AreEqual(Dump(new LevelByLevelSimplifier().Simplify(source)), Dump(result), label);
    }

    /// <summary>Builds a left-associated sum of <paramref name="terms"/>.</summary>
    /// <param name="terms">The terms.</param>
    /// <returns>The sum.</returns>
    private static Expression LeftSum(IEnumerable<Expression> terms) => terms.Aggregate(static (left, right) => Add(left, right));

    /// <summary>Builds a right-associated sum of <paramref name="terms"/>.</summary>
    /// <param name="terms">The terms.</param>
    /// <returns>The sum.</returns>
    private static Expression RightSum(IEnumerable<Expression> terms) => terms.Reverse().Aggregate(static (right, left) => Add(left, right));

    /// <summary>Builds a balanced sum of <paramref name="terms"/>.</summary>
    /// <param name="terms">The terms.</param>
    /// <returns>The sum.</returns>
    private static Expression BalancedSum(IReadOnlyList<Expression> terms)
        => terms.Count == 1 ? terms[0] : Add(BalancedSum([.. terms.Take(terms.Count / 2)]), BalancedSum([.. terms.Skip(terms.Count / 2)]));

    /// <summary>
    /// The base cases of the brief and the linear chains of every accepted type are accepted, and give the level by level
    /// result under every policy: sums of distinct parameters in every grouping, differences with a term on the right,
    /// constants, products by a constant or by another parameter.
    /// </summary>
    [TestMethod]
    public void LinearChains_AreAccepted_AndGiveTheLevelByLevelResult()
    {
        var cases = new (string Label, Expression Source)[]
        {
            ("a + b", Add(D[0], D[1])),
            ("a - b", Sub(D[0], D[1])),
            ("(a + b) + c", Add(Add(D[0], D[1]), D[2])),
            ("a + (b + c)", Add(D[0], Add(D[1], D[2]))),
            ("c + (b + a)", Add(D[2], Add(D[1], D[0]))),
            ("(c - b) - a", Sub(Sub(D[2], D[1]), D[0])),
            ("(c - b) + a", Add(Sub(D[2], D[1]), D[0])),
            ("a + (b - c)", Add(D[0], Sub(D[1], D[2]))),
            ("(a - b) + (c - d)", Add(Sub(D[0], D[1]), Sub(D[2], D[3]))),
            ("a + 5", Add(D[0], C(5.0))),
            ("5 - a", Sub(C(5.0), D[0])),
            ("(a + 5) + 6", Add(Add(D[0], C(5.0)), C(6.0))),
            ("(a - 5) + 3", Add(Sub(D[0], C(5.0)), C(3.0))),
            ("(a + 5) - 5", Sub(Add(D[0], C(5.0)), C(5.0))),
            ("(a + 1e16) + 1", Add(Add(D[0], C(1e16)), C(1.0))),
            ("((a + 5) + b) - 5", Sub(Add(Add(D[0], C(5.0)), D[1]), C(5.0))),
            ("3*a + 2*b", Add(Mul(C(3.0), D[0]), Mul(C(2.0), D[1]))),
            ("a*3 - b*2 + c", Add(Sub(Mul(D[0], C(3.0)), Mul(D[1], C(2.0))), D[2])),
            ("a*b + c*d + e", Add(Add(Mul(D[0], D[1]), Mul(D[2], D[3])), D[4])),
            ("100*a - b*3 - 4*c - d*5", Sub(Sub(Sub(Mul(C(100.0), D[0]), Mul(D[1], C(3.0))), Mul(C(4.0), D[2])), Mul(D[3], C(5.0)))),
            ("2 + 3*a", Add(C(2.0), Mul(C(3.0), D[0]))),
            ("left sum of 40", LeftSum(D.Take(40))),
            ("right sum of 40", RightSum(D.Take(40))),
            ("balanced sum of 40", BalancedSum([.. D.Take(40)])),
            ("int: (i0 - i1) + 2*i2 + 7 - 9", Sub(Add(Add(Sub(I[0], I[1]), Mul(C(2), I[2])), C(7)), C(9))),
            ("int: MaxValue + i0 + MaxValue", Add(Add(C(int.MaxValue), I[0]), C(int.MaxValue))),
            ("long: l0 - 3*l1 + 5 + 6", Add(Add(Sub(L[0], Mul(C(3L), L[1])), C(5L)), C(6L))),
            ("uint: (u0 - u1) + 3*u2 - 5", Sub(Add(Sub(U[0], U[1]), Mul(C(3u), U[2])), C(5u))),
            ("uint: (u0 - 5) + 3", Add(Sub(U[0], C(5u)), C(3u))),
            ("float: f0 - 2.5*f1 + 1 + f2", Add(Add(Sub(F[0], Mul(C(2.5f), F[1])), C(1f)), F[2])),
        };

        foreach ((string label, Expression source) in cases) AssertAccepted(label, source);
    }

    /// <summary>
    /// Every chain outside the linear family is declined and keeps the level by level result: a repeated parameter, equal
    /// coefficients, a coefficient equal to a constant term, the constants of the zero, one and <c>NaN</c> identities, a
    /// subtraction of a group, a pair of constants, too many constants for the type, a term that is not linear.
    /// </summary>
    [TestMethod]
    public void OtherChains_AreDeclined_AndKeepTheLevelByLevelResult()
    {
        MethodInfo sin = typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!;
        var cases = new (string Label, Expression Source)[]
        {
            ("a + a", Add(D[0], D[0])),
            ("a - a", Sub(D[0], D[0])),
            ("a + (b + a)", Add(D[0], Add(D[1], D[0]))),
            ("(a + b) + (a + b)", Add(Add(D[0], D[1]), Add(D[0], D[1]))),
            ("3*a + 2*a", Add(Mul(C(3.0), D[0]), Mul(C(2.0), D[0]))),
            ("a*b + a", Add(Mul(D[0], D[1]), D[0])),
            ("a*a + b", Add(Mul(D[0], D[0]), D[1])),
            ("2*a + 2*b", Add(Mul(C(2.0), D[0]), Mul(C(2.0), D[1]))),
            ("2 + 2*a", Add(C(2.0), Mul(C(2.0), D[0]))),
            ("a + 0", Add(D[0], C(0.0))),
            ("a + -0.0", Add(D[0], C(-0.0))),
            ("0 - a", Sub(C(0.0), D[0])),
            ("a + NaN", Add(D[0], C(double.NaN))),
            ("a + inf", Add(D[0], C(double.PositiveInfinity))),
            ("a + 1*b", Add(D[0], Mul(C(1.0), D[1]))),
            ("a + -1*b", Add(D[0], Mul(C(-1.0), D[1]))),
            ("a + 0*b", Add(D[0], Mul(C(0.0), D[1]))),
            ("a + NaN*b", Add(D[0], Mul(C(double.NaN), D[1]))),
            ("a + 2*3", Add(D[0], Mul(C(2.0), C(3.0)))),
            ("a - (b + c)", Sub(D[0], Add(D[1], D[2]))),
            ("a - (b - c)", Sub(D[0], Sub(D[1], D[2]))),
            ("a + (b - (c + d))", Add(D[0], Sub(D[1], Add(D[2], D[3])))),
            ("a + (1 + 2)", Add(D[0], Add(C(1.0), C(2.0)))),
            ("(5 - 5) + a", Add(Sub(C(5.0), C(5.0)), D[0])),
            ("double: a + 1 + 2 + 3", Add(Add(Add(D[0], C(1.0)), C(2.0)), C(3.0))),
            ("uint: u0 + 1 + 2 + 3", Add(Add(Add(U[0], C(1u)), C(2u)), C(3u))),
            ("int: ((i0 + 5) - 5) - 3", Sub(Sub(Add(I[0], C(5)), C(5)), C(3))),
            ("int: i0 + MaxValue - MaxValue - 5", Sub(Sub(Add(I[0], C(int.MaxValue)), C(int.MaxValue)), C(5))),
            ("a + (-b)", Add(D[0], Expression.Negate(D[1]))),
            ("a - (-b)", Sub(D[0], Expression.Negate(D[1]))),
            ("a + sin(b)", Add(D[0], Expression.Call(sin, D[1]))),
            ("a + b^2", Add(D[0], Expression.Power(D[1], C(2.0)))),
            ("a + b/c", Add(D[0], Expression.Divide(D[1], D[2]))),
            ("a + (b + c)*d", Add(D[0], Mul(Add(D[1], D[2]), D[3]))),
            ("a + 2*(3*b)", Add(D[0], Mul(C(2.0), Mul(C(3.0), D[1])))),
            ("a + (b +checked c)", Add(D[0], Expression.AddChecked(D[1], D[2]))),
            ("a + (double)i", Add(D[0], Expression.Convert(I[0], typeof(double)))),
        };

        foreach ((string label, Expression source) in cases) AssertDeclined(label, source);
    }

    /// <summary>
    /// A linear chain with one call that is not inert (Flatten mission C2): before C2 these two cases of
    /// <see cref="OtherChains_AreDeclined_AndKeepTheLevelByLevelResult"/> were declined; they are now anchored chains, accepted
    /// at the root under the default policy, and still give the level by level result, with the same user code.
    /// </summary>
    [TestMethod]
    public void CallChains_AreAnchored_AndKeepTheLevelByLevelResult()
    {
        var cases = new (string Label, Expression Source)[]
        {
            ("a + opaque", Add(D[0], Opaque(1))),
            ("a + unknown()", Add(D[0], Expression.Call(typeof(ExpressionSimplifierFlattenTests).GetMethod(nameof(Unknown))!))),
        };

        foreach ((string label, Expression source) in cases)
        {
            LoggedValue.Log.Clear();
            string expected = Dump(new LevelByLevelSimplifier().Simplify(source));
            string[] expectedLog = [.. LoggedValue.Log];
            LoggedValue.Log.Clear();
            (Expression result, ExpressionTransformEvent[] events) = Traced(new ExpressionSimplifier(), source);
            Assert.IsTrue(AcceptedAt(events, source), label + ": the chain must be accepted");
            Assert.AreEqual(expected, Dump(result), label);
            CollectionAssert.AreEqual(expectedLog, LoggedValue.Log.ToArray(), label);
        }
    }

    /// <summary>
    /// A chain the infrastructure does not cross is not offered to the rule at all: <see cref="decimal"/> (its operators are
    /// methods), checked and lifted operators, and a chain whose root is a negation (the rule declares <c>Add</c> and
    /// <c>Subtract</c> only, so the operand of the negation is a chain of its own).
    /// </summary>
    [TestMethod]
    public void UncrossedChains_AreNotOffered_AndNegationIsNotCrossed()
    {
        ParameterExpression n0 = Expression.Parameter(typeof(int?), "n0"), n1 = Expression.Parameter(typeof(int?), "n1");
        foreach (Expression source in new Expression[] { Add(M[0], M[1]), Expression.AddChecked(I[0], I[1]), Add(n0, n1) })
        {
            (Expression result, ExpressionTransformEvent[] events) = Traced(new ExpressionSimplifier(), source);
            Assert.IsFalse(events.Any(static e => e.Kind == ExpressionTransformEventKind.ChainFlattened), Dump(source));
            Assert.AreEqual(Dump(new LevelByLevelSimplifier().Simplify(source)), Dump(result));
        }

        BinaryExpression sum = Add(D[0], D[1]);
        UnaryExpression negation = Expression.Negate(sum);
        (Expression negated, ExpressionTransformEvent[] negationEvents) = Traced(new ExpressionSimplifier(), negation);
        Assert.IsFalse(negationEvents.Any(e => e.Kind == ExpressionTransformEventKind.ChainFlattened && ReferenceEquals(e.Input, negation)));
        Assert.IsTrue(AcceptedAt(negationEvents, sum), "The operand of the negation is a chain of its own.");
        Assert.AreEqual(Dump(new LevelByLevelSimplifier().Simplify(negation)), Dump(negated));
    }

    /// <summary>
    /// A chain that is not admitted is transformed level by level from its root, and the chains below it are offered in
    /// turn: the linear part of a chain that ends with a term that is not linear is still canonicalized at once, and the
    /// result is the level by level one.
    /// </summary>
    [TestMethod]
    public void SubChains_OfARefusedChain_AreAdmittedOnTheirOwn()
    {
        MethodInfo sin = typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!;
        Expression linear = D.Take(10).Aggregate<Expression>(Sub);
        var cases = new (string Label, Expression Source, Expression Linear)[]
        {
            ("(x0 - ... - x9) + sin(y)", Add(linear, Expression.Call(sin, D[20])), linear),
            ("(x0 - ... - x9) + x0", Add(linear, D[0]), linear),
            ("sin(y) - (x0 - ... - x9)", Sub(Expression.Call(sin, D[20]), linear), linear),
        };

        foreach ((string label, Expression source, Expression part) in cases)
        {
            (Expression result, ExpressionTransformEvent[] events) = Traced(new ExpressionSimplifier(), source);
            Assert.IsTrue(DeclinedAt(events, source), label);
            Assert.IsTrue(AcceptedAt(events, part), label + ": the linear part is a chain of its own");
            Assert.AreEqual(Dump(new LevelByLevelSimplifier().Simplify(source)), Dump(result), label);
        }
    }

    /// <summary>
    /// Differential check over every grouping: every tree of two and three terms over a pool of linear and non-linear terms,
    /// with every assignment of <c>+</c> and <c>-</c>, and seeded random trees of up to ten terms, free and lambda-bound,
    /// give the level by level result; a significant part of them is accepted.
    /// </summary>
    [TestMethod]
    public void EveryGrouping_GivesTheLevelByLevelResult()
    {
        MethodInfo sin = typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!;
        Expression[] pool =
        [
            D[0], D[1], D[2], C(0.0), C(1.0), C(2.0), C(3.0),
            Mul(C(2.0), D[0]), Mul(C(2.0), D[3]), Mul(D[4], C(3.0)), Mul(D[5], D[6]), Mul(C(-1.0), D[7]),
            Expression.Negate(D[8]), Sub(C(0.0), D[9]), Add(D[10], D[11]), C(double.NaN), Expression.Call(sin, D[0]),
        ];
        Func<Expression, Expression, Expression>[] operators = [Add, Sub];
        int cases = 0, accepted = 0;

        void Check(Expression source)
        {
            (Expression result, ExpressionTransformEvent[] events) = Traced(new ExpressionSimplifier(), source);
            Expression root = source is LambdaExpression lambda ? lambda.Body : source;
            cases++;
            if (AcceptedAt(events, root))
            {
                accepted++;

                // The rule receives every chain without the effect gate: what it accepts must be what the gate admits.
                Assert.IsTrue(ExpressionEffectAnalysis.HasInertOperands(root), Dump(source));
            }
            Assert.AreEqual(Dump(new LevelByLevelSimplifier().Simplify(source)), Dump(result), Dump(source));
        }

        foreach (Expression x in pool)
        {
            foreach (Expression y in pool)
            {
                foreach (var outer in operators)
                {
                    Check(outer(x, y));
                    foreach (Expression z in pool.Take(11))
                    {
                        foreach (var inner in operators)
                        {
                            Check(outer(inner(x, y), z));
                            Check(outer(z, inner(x, y)));
                        }
                    }
                }
            }
        }

        Assert.IsTrue(accepted > cases / 20, $"{accepted} accepted chains out of {cases}");

        int randomAccepted = 0;
        for (int seed = 0; seed < 1500; seed++)
        {
            var random = new Random(seed);
            var free = new List<ParameterExpression>(D.Take(14));
            Expression Leaf()
            {
                ParameterExpression Parameter()
                {
                    if (free.Count == 0 || random.Next(10) == 0) return D[random.Next(14)];
                    ParameterExpression parameter = free[random.Next(free.Count)];
                    free.Remove(parameter);
                    return parameter;
                }

                ConstantExpression Constant() => C((double)(2 + random.Next(40)));
                return random.Next(20) switch
                {
                    < 9 => Parameter(),
                    < 11 => Constant(),
                    < 15 => Mul(Constant(), Parameter()),
                    < 17 => Mul(Parameter(), Constant()),
                    < 19 => Mul(Parameter(), Parameter()),
                    _ => pool[random.Next(pool.Length)],
                };
            }

            Expression Tree(int terms)
            {
                if (terms == 1) return Leaf();
                int left = random.Next(3) == 0 ? 1 + random.Next(terms - 1) : terms - 1;
                Expression leftTree = Tree(left), rightTree = Tree(terms - left);
                return random.Next(3) == 0 && terms - left == 1 ? Sub(leftTree, rightTree) : Add(leftTree, rightTree);
            }

            Expression tree = Tree(2 + random.Next(9));
            int before = accepted;
            Check(seed % 2 == 0 ? tree : Expression.Lambda(tree, D.Take(14).Reverse()));
            randomAccepted += accepted - before;
        }

        Assert.IsTrue(randomAccepted > 300, $"{randomAccepted} accepted random chains");
    }

    /// <summary>
    /// An accepted chain prepares each of its terms exactly once, in source order, and nothing else: one treatment for the
    /// chain, one per term, and for a product term the treatments of its own preparation, none of them repeated.
    /// </summary>
    [TestMethod]
    public void AcceptedChain_PreparesEachTermOnce_InSourceOrder()
    {
        Expression[] terms = [Mul(D[0], C(3.0)), D[1], Mul(C(4.0), D[2]), C(7.0), Mul(D[3], D[4])];
        Expression source = Sub(Add(Add(Sub(terms[0], terms[1]), terms[2]), terms[3]), terms[4]);

        (_, ExpressionTransformEvent[] events) = Traced(new ExpressionSimplifier(), source);

        Assert.IsTrue(AcceptedAt(events, source));
        Expression[] entered = [.. events.Where(static e => e.Kind == ExpressionTransformEventKind.TransformEnter).Select(static e => e.Input!)];
        Assert.AreSame(source, entered[0]);
        Expression[] enteredTerms = [.. entered.Where(e => terms.Any(term => ReferenceEquals(term, e)))];
        Assert.AreEqual(terms.Length, enteredTerms.Length, "Each term is prepared exactly once.");
        for (int i = 0; i < terms.Length; i++) Assert.AreSame(terms[i], enteredTerms[i], "Terms are prepared in source order.");
        Assert.IsFalse(entered.Skip(1).Any(static e => e.NodeType is ExpressionType.Add or ExpressionType.Subtract), "No inner node of the chain is treated.");
        Assert.AreEqual(0, events.Count(static e => e.Kind == ExpressionTransformEventKind.RuleApplied && e.Rule!.Name.Contains("OfEqualsElements", StringComparison.Ordinal)));
    }

    /// <summary>
    /// The work of an accepted chain of <c>n</c> parameters is one transformation visit for the chain and one per term,
    /// against <c>2n - 1</c> node treatments plus the comparer simplifications of the factoring probes level by level; a
    /// refused chain is not extracted and never costs more visits than level by level.
    /// </summary>
    [TestMethod]
    public void AcceptedChain_CostsOneVisitPerTerm_PlusOne()
    {
        static long Visits(ExpressionSimplifier simplifier, Expression source)
            => ExpressionResourceBudget.Run(ExpressionResourceLimits.Unlimited, null, null, null, (simplifier, source), static state =>
            {
                state.simplifier.Simplify(state.source);
                return ExpressionResourceBudget.TransformVisits;
            });

        foreach (int n in new[] { 2, 8, 32 })
        {
            Expression source = LeftSum(D.Take(n));
            long accepted = Visits(new ExpressionSimplifier(), source);
            long levelByLevel = Visits(new LevelByLevelSimplifier(), source);
            Assert.AreEqual(n + 1, accepted, $"n = {n}");
            Assert.IsTrue(levelByLevel >= 2 * n - 1, $"n = {n}: {levelByLevel}");

            Expression refused = Add(source, D[0]);
            Assert.IsTrue(Visits(new ExpressionSimplifier(), refused) <= Visits(new LevelByLevelSimplifier(), refused), $"refused, n = {n}");
        }
    }

    /// <summary>A simplifier that records what its extension points receive.</summary>
    private class RecordingSimplifier : ExpressionSimplifier
    {
        /// <summary>Gets the expressions given to <see cref="PrepareExpression"/>.</summary>
        public List<Expression> Prepared { get; } = [];

        /// <summary>Gets the expressions given to <see cref="FinalizeExpression"/>.</summary>
        public List<Expression> Finalized { get; } = [];

        /// <inheritdoc/>
        protected override Expression PrepareExpression(Expression e)
        {
            Prepared.Add(e);
            return base.PrepareExpression(e);
        }

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
        {
            Finalized.Add(e);
            return base.FinalizeExpression(e, parameters);
        }
    }

    /// <summary>A simplifier with one more rule on additions.</summary>
    private sealed class ExtraRuleSimplifier : ExpressionSimplifier
    {
        /// <summary>Gets the number of additions the rule received.</summary>
        public int Received { get; private set; }

        /// <summary>Counts the additions and declines.</summary>
        /// <param name="e">The addition.</param>
        /// <param name="left">The prepared left operand.</param>
        /// <param name="right">The prepared right operand.</param>
        /// <returns><see langword="null"/>.</returns>
        [ExpressionSignature(ExpressionType.Add)]
        public Expression? Count(BinaryExpression e, Expression left, Expression right)
        {
            Received++;
            return null;
        }
    }

    /// <summary>A simplifier hiding a base rule.</summary>
    private sealed class HidingSimplifier : ExpressionSimplifier
    {
        /// <summary>Gets the number of additions the hiding rule received.</summary>
        public int Received { get; private set; }

        /// <summary>Hides the base factoring rule and declines.</summary>
        /// <param name="e">The addition.</param>
        /// <param name="left">The prepared left operand.</param>
        /// <param name="right">The prepared right operand.</param>
        /// <returns><see langword="null"/>.</returns>
        [ExpressionSignature(ExpressionType.Add)]
        public new Expression? AdditionOfEqualsElements(BinaryExpression e, Expression left, Expression right)
        {
            Received++;
            return null;
        }
    }

    /// <summary>A simplifier that declares its own flatten rule.</summary>
    private sealed class OwnFlattenSimplifier : ExpressionSimplifier
    {
        /// <summary>Gets the term counts of the chains the rule received.</summary>
        public List<int> Received { get; } = [];

        /// <summary>Records the chain and declines.</summary>
        /// <param name="terms">The terms.</param>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract)]
        private Expression? Own(FlattenedTerm[] terms)
        {
            Received.Add(terms.Length);
            return null;
        }
    }

    /// <summary>
    /// The rule is private to the built-in type: a derived simplifier, with or without members, does not inherit it. Its
    /// <see cref="ExpressionSimplifier.PrepareExpression"/> and <see cref="ExpressionSimplifier.FinalizeExpression"/> overrides
    /// receive every node of an otherwise accepted chain, its own rules and a rule hiding a base rule receive every addition,
    /// and no chain is extracted. A derived simplifier that declares its own flatten rule receives its chains, and only its
    /// own rule.
    /// </summary>
    [TestMethod]
    public void DerivedSimplifiers_KeepTheLevelByLevelTreatment()
    {
        const int Terms = 6;
        Expression source = LeftSum(D.Take(Terms));
        string expected = Dump(new LevelByLevelSimplifier().Simplify(source));

        (Expression plain, ExpressionTransformEvent[] plainEvents) = Traced(new LevelByLevelSimplifier(), source);
        // The comparisons of the level by level treatment simplify with the built-in type, whose chains are flattened: only
        // the events of the derived simplifier itself are checked.
        Assert.IsFalse(plainEvents.Any(e => e.Kind == ExpressionTransformEventKind.ChainFlattened && ReferenceEquals(e.Input, source)));
        Assert.IsFalse(plainEvents.Any(static e => e.Rule?.Name == FlattenRule && e.Rule.TransformerType == typeof(LevelByLevelSimplifier)));
        Assert.AreEqual(expected, Dump(plain));

        var recording = new RecordingSimplifier();
        Assert.AreEqual(expected, Dump(recording.Simplify(source)));
        Assert.AreEqual(Terms - 1, recording.Prepared.Count(static e => e.NodeType == ExpressionType.Add && D.Take(Terms).Contains(((BinaryExpression)e).Right)) + 1,
            "Every inner addition of the source is prepared by its parent.");
        Assert.IsTrue(D.Take(Terms).All(parameter => recording.Prepared.Contains(parameter)));
        Assert.IsTrue(recording.Finalized.Count(static e => e.NodeType == ExpressionType.Add) >= Terms - 1, "Every addition is finalized.");

        var extra = new ExtraRuleSimplifier();
        Assert.AreEqual(expected, Dump(extra.Simplify(source)));
        Assert.IsTrue(extra.Received >= Terms - 1, $"{extra.Received} additions received");

        var hiding = new HidingSimplifier();
        Assert.AreEqual(expected, Dump(hiding.Simplify(source)));
        Assert.IsTrue(hiding.Received >= Terms - 1, $"{hiding.Received} additions received");

        var own = new OwnFlattenSimplifier();
        (Expression ownResult, ExpressionTransformEvent[] ownEvents) = Traced(own, source);
        Assert.AreEqual(expected, Dump(ownResult));
        Assert.AreEqual(Terms, own.Received[0]);
        Assert.IsFalse(ownEvents.Any(static e => e.Rule?.Name == FlattenRule && e.Rule.TransformerType == typeof(OwnFlattenSimplifier)));

        // The admission of a derived simplifier is the effect gate, not the linear test of the built-in type: its own rule
        // receives an inert chain that is not linear, and never a chain holding an unknown call.
        own.Received.Clear();
        own.Simplify(Add(Add(D[0], D[0]), Expression.Power(D[1], C(2.0))));
        Assert.AreEqual(3, own.Received[0]);
        own.Received.Clear();
        own.Simplify(Add(D[0], Expression.Call(typeof(ExpressionSimplifierFlattenTests).GetMethod(nameof(Unknown))!)));
        Assert.AreEqual(0, own.Received.Count);

        MethodInfo rule = typeof(ExpressionSimplifier).GetMethod(FlattenRule, BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.IsTrue(rule.IsPrivate, "The flatten rule must stay private: a derived simplifier must not inherit it.");
        Assert.IsNull(typeof(LevelByLevelSimplifier).GetMethod(FlattenRule, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));
    }

    /// <summary>
    /// Inside a lambda the parameters are bound: the accepted chain is ordered by declaration position, as level by level,
    /// and the parameter instances are kept. A chain of the body and a chain outside the lambda are separate chains.
    /// </summary>
    [TestMethod]
    public void BoundParameters_AreOrderedByTheirScope()
    {
        Expression body = Add(Sub(Add(D[2], Mul(C(3.0), D[0])), D[3]), D[1]);
        LambdaExpression lambda = Expression.Lambda(body, D[0], D[1], D[2], D[3]);

        (Expression result, ExpressionTransformEvent[] events) = Traced(new ExpressionSimplifier(), lambda);

        Assert.IsTrue(AcceptedAt(events, body));
        Assert.AreEqual(Dump(new LevelByLevelSimplifier().Simplify(lambda)), Dump(result));
        Assert.AreNotEqual(Dump(new ExpressionSimplifier().Simplify(body)), Dump(((LambdaExpression)result).Body), "Bound and free parameters do not sort alike.");

        // Nested scopes: the inner body is a chain of its own, simplified inside both scopes.
        Expression inner = Add(Add(D[5], D[0]), D[4]);
        LambdaExpression nested = Expression.Lambda(Add(Add(D[1], D[0]), Expression.Invoke(Expression.Lambda(inner, D[4], D[5]), D[2], D[3])), D[0], D[1], D[2], D[3]);
        Assert.AreEqual(Dump(new LevelByLevelSimplifier().Simplify(nested)), Dump(new ExpressionSimplifier().Simplify(nested)));
    }

    /// <summary>
    /// The rules that rebuild an additive node and transform it again (<c>a - (-b)</c>, <c>-(a - b)</c>, <c>a + (-b)</c> and
    /// its prepared-operand path) give the level by level result: the rebuilt node is a chain like any other, accepted or
    /// declined on the same conditions, and its already prepared operands are never observed twice.
    /// </summary>
    [TestMethod]
    public void RebuiltAdditiveNodes_GiveTheLevelByLevelResult()
    {
        Expression[] sources =
        [
            Sub(D[0], Expression.Negate(D[1])),
            Expression.Negate(Sub(D[0], D[1])),
            Add(D[0], Expression.Negate(D[1])),
            Add(Expression.Negate(Add(D[0], D[1])), Add(D[2], D[3])),
            Add(Add(D[0], D[1]), Expression.Negate(Sub(Mul(C(3.0), D[2]), D[3]))),
            Sub(Sub(C(0.0), D[0]), Sub(C(0.0), Add(D[1], D[2]))),
            Add(Add(Sub(C(0.0), D[1]), Add(Sub(C(0.0), D[0]), D[2])), C(0.0)),
        ];

        foreach (Expression source in sources)
        {
            Assert.AreEqual(Dump(new LevelByLevelSimplifier().Simplify(source)), Dump(new ExpressionSimplifier().Simplify(source)), Dump(source));
        }
    }

    /// <summary>
    /// The effect gate still protects observable evaluations: a chain holding an unknown call is refused before the rule, the
    /// call is neither dropped, merged nor moved, and the cases of the brief keep the level by level result under every policy.
    /// </summary>
    [TestMethod]
    public void ObservableEvaluations_AreNeitherDroppedNorMoved()
    {
        Expression Call() => Expression.Call(typeof(ExpressionSimplifierFlattenTests).GetMethod(nameof(Unknown))!);
        Expression[] sources =
        [
            Add(Add(D[0], Call()), D[1]),
            Sub(Add(D[0], Call()), Call()),
            Add(Call(), Expression.Negate(Call())),
            Add(Add(Call(), C(0.0)), D[0]),
            Add(Add(C(double.NaN), Call()), D[0]),
            Mul(C(0.0), Add(C(4.0), Call())),
        ];

        foreach (Expression source in sources)
        {
            for (int p = 0; p < Policies.Length; p++)
            {
                ExpressionSimplifierOptions policy = Policies[p];
                (Expression result, ExpressionTransformEvent[] events) = Traced(new ExpressionSimplifier { Options = policy }, source);

                // Flatten mission C2: under the default policy (the first one), a chain may hold one unknown call, its anchor;
                // under every other policy, no chain holding an unknown call is accepted.
                Assert.IsFalse(events.Any(e => e.Kind == ExpressionTransformEventKind.RuleApplied && e.Rule!.Name == FlattenRule
                        && Dump(e.Input).Split(nameof(Unknown)).Length - 1 is int calls && calls > (p == 0 ? 1 : 0)),
                    "No chain holding more unknown calls than its anchor is accepted.");
                Assert.AreEqual(Dump(new LevelByLevelSimplifier { Options = policy }.Simplify(source)), Dump(result), Dump(source) + " under " + policy);
                int sourceCalls = Dump(source).Split(nameof(Unknown)).Length - 1, resultCalls = Dump(result).Split(nameof(Unknown)).Length - 1;
                Assert.AreEqual(sourceCalls, resultCalls, Dump(source) + " under " + policy);
            }
        }
    }

    /// <summary>
    /// The rule runs no user code: a chain holding custom constants is declined before any of them is compared or hashed, and
    /// the whole simplification makes exactly the user calls of the level by level treatment, in the same order.
    /// </summary>
    [TestMethod]
    public void UserCode_RunsExactlyAsLevelByLevel()
    {
        Expression[] sources =
        [
            Add(Add(D[0], Opaque(1)), D[1]),
            Sub(Add(Mul(C(100.0), Opaque(1)), Mul(C(3.0), Opaque(2))), Mul(C(4.0), D[0])),
            Add(Add(Opaque(1), Opaque(1)), Add(D[0], D[1])),
            Add(Add(Add(D[0], D[1]), D[2]), Mul(D[3], Opaque(3))),
        ];

        foreach (Expression source in sources)
        {
            LoggedValue.Log.Clear();
            string expected = Dump(new LevelByLevelSimplifier().Simplify(source));
            string[] expectedLog = [.. LoggedValue.Log];

            LoggedValue.Log.Clear();
            string actual = Dump(new ExpressionSimplifier().Simplify(source));
            string[] actualLog = [.. LoggedValue.Log];

            Assert.AreEqual(expected, actual, Dump(source));
            CollectionAssert.AreEqual(expectedLog, actualLog, Dump(source));
        }
    }

    /// <summary>
    /// The rule keeps no state: concurrent simplifications on one instance, and a simplification nested in another one's
    /// comparison, give the result of a single call.
    /// </summary>
    [TestMethod]
    public void ConcurrentCalls_OnOneInstance_GiveTheSameResults()
    {
        Expression[] sources = [.. Enumerable.Range(2, 30).Select(n => n % 2 == 0 ? LeftSum(D.Skip(n).Take(n)) : Add(LeftSum(D.Take(n)), D[0]))];
        string[] expected = [.. sources.Select(static source => Dump(new LevelByLevelSimplifier().Simplify(source)))];
        var simplifier = new ExpressionSimplifier();

        Parallel.For(0, 400, i =>
        {
            int index = i % sources.Length;
            Assert.AreEqual(expected[index], Dump(simplifier.Simplify(sources[index])));
        });
    }
}
