using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Regression coverage for roadmap stage S5 P10 (subtractive-chain canonicalization, see
/// <c>Utils/TODO-2026-09-12-expression-simplifier-roadmap.md</c>): additive canonicalization must not hand the
/// zero minuend of a rebuilt negative group back to the enclosing sum as a term of its own, must cancel opposite
/// occurrences of the same opaque term, and must keep its deterministic right-associated shape.
/// </summary>
/// <remarks>
/// Every test goes through the public <c>new ExpressionSimplifier().Simplify(...)</c> entry point and asserts
/// on the raw resulting tree through <see cref="Dump"/>, which records node type, static type, operator method,
/// lifting, exact constant bits and parameter identity, never <see cref="Expression.ToString()"/> alone.
/// </remarks>
[TestClass]
public class ExpressionSimplifierAdditiveCanonicalizationTests
{
    // ------------------------------------------------------------------------------------------
    // Shared helpers
    // ------------------------------------------------------------------------------------------

    /// <summary>A user-defined subtraction, so a node built with it is never ordinary arithmetic.</summary>
    /// <param name="a">The minuend.</param>
    /// <param name="b">The subtrahend.</param>
    /// <returns>A value that differs from <c>a - b</c>, so an erased custom operator is observable.</returns>
    public static double CustomSubtract(double a, double b) => a - b - 1000.0;

    /// <summary>The <see cref="MethodInfo"/> of <see cref="CustomSubtract"/>.</summary>
    private static readonly MethodInfo CustomSubtractMethod = typeof(ExpressionSimplifierAdditiveCanonicalizationTests).GetMethod(nameof(CustomSubtract))!;

    /// <summary>A derived simplifier with no overrides, used to check that subclasses get the same canonical form.</summary>
    private sealed class MinimalDerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>Simplifies <paramref name="expression"/> with a fresh built-in simplifier.</summary>
    /// <param name="expression">The source expression.</param>
    /// <returns>The simplified expression.</returns>
    private static Expression Simplify(Expression expression) => new ExpressionSimplifier().Simplify(expression);

    /// <summary>
    /// Renders the complete structure of <paramref name="expression"/>: node type, static type, operator method,
    /// lifting, exact constant bits and parameter identity (by order of first appearance), so two trees render
    /// identically only when they are structurally identical up to parameter instances in the same positions.
    /// </summary>
    /// <param name="expression">The tree to render.</param>
    /// <returns>The structural rendering.</returns>
    private static string Dump(Expression expression)
    {
        var parameterIds = new Dictionary<ParameterExpression, int>();
        var builder = new StringBuilder();
        Write(expression, builder, parameterIds);
        return builder.ToString();
    }

    /// <summary>Appends the structural rendering of one node to <paramref name="builder"/>.</summary>
    /// <param name="expression">The node.</param>
    /// <param name="builder">The destination.</param>
    /// <param name="parameterIds">Parameter identities assigned so far.</param>
    private static void Write(Expression expression, StringBuilder builder, Dictionary<ParameterExpression, int> parameterIds)
    {
        switch (expression)
        {
            case BinaryExpression binary:
                builder.Append('(').Append(binary.NodeType).Append(':').Append(binary.Type.Name);
                if (binary.Method is not null) builder.Append(":m=").Append(binary.Method.Name);
                if (binary.IsLifted) builder.Append(":lifted");
                if (binary.IsLiftedToNull) builder.Append(":liftedToNull");
                builder.Append(' ');
                Write(binary.Left, builder, parameterIds);
                builder.Append(' ');
                Write(binary.Right, builder, parameterIds);
                builder.Append(')');
                break;
            case UnaryExpression unary:
                builder.Append('(').Append(unary.NodeType).Append(':').Append(unary.Type.Name);
                if (unary.Method is not null) builder.Append(":m=").Append(unary.Method.Name);
                builder.Append(' ');
                Write(unary.Operand, builder, parameterIds);
                builder.Append(')');
                break;
            case ConstantExpression constant:
                builder.Append("c:").Append(constant.Type.Name).Append('=').Append(ConstantBits(constant.Value));
                break;
            case ParameterExpression parameter:
                if (!parameterIds.TryGetValue(parameter, out int id))
                {
                    id = parameterIds.Count;
                    parameterIds[parameter] = id;
                }
                builder.Append('p').Append(id).Append(':').Append(parameter.Type.Name);
                break;
            case LambdaExpression lambda:
                builder.Append("(lambda");
                foreach (ParameterExpression parameter in lambda.Parameters)
                {
                    builder.Append(' ');
                    Write(parameter, builder, parameterIds);
                }
                builder.Append(' ');
                Write(lambda.Body, builder, parameterIds);
                builder.Append(')');
                break;
            case MemberExpression member:
                builder.Append("(member:").Append(member.Member.Name).Append(' ');
                if (member.Expression is not null) Write(member.Expression, builder, parameterIds);
                builder.Append(')');
                break;
            case MethodCallExpression call:
                builder.Append("(call:").Append(call.Method.Name);
                foreach (Expression argument in call.Arguments)
                {
                    builder.Append(' ');
                    Write(argument, builder, parameterIds);
                }
                builder.Append(')');
                break;
            default:
                builder.Append('?').Append(expression.NodeType);
                break;
        }
    }

    /// <summary>Renders a constant value exactly: IEEE bits for <see cref="double"/>/<see cref="float"/>, the four words of a <see cref="decimal"/>, invariant text otherwise.</summary>
    /// <param name="value">The constant value.</param>
    /// <returns>The exact rendering.</returns>
    private static string ConstantBits(object? value) => value switch
    {
        null => "null",
        double d => BitConverter.DoubleToInt64Bits(d).ToString("X16", CultureInfo.InvariantCulture),
        float f => BitConverter.SingleToInt32Bits(f).ToString("X8", CultureInfo.InvariantCulture),
        decimal m => string.Join(",", decimal.GetBits(m)),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
    };

    /// <summary>Depth-first enumeration of the nodes of <paramref name="root"/> for the node kinds these tests build.</summary>
    /// <param name="root">The tree to walk.</param>
    /// <returns>Every node, root first.</returns>
    private static IEnumerable<Expression> Nodes(Expression? root)
    {
        if (root is null) yield break;
        yield return root;
        IEnumerable<Expression?> children = root switch
        {
            BinaryExpression binary => [binary.Left, binary.Right],
            UnaryExpression unary => [unary.Operand],
            LambdaExpression lambda => [lambda.Body],
            MemberExpression member => [member.Expression],
            MethodCallExpression call => call.Arguments,
            _ => [],
        };
        foreach (Expression? child in children)
        {
            foreach (Expression node in Nodes(child)) yield return node;
        }
    }

    /// <summary>Whether <paramref name="expression"/> is a numeric zero constant of any of the types used here.</summary>
    /// <param name="expression">The node to test.</param>
    /// <returns><see langword="true"/> for a zero constant.</returns>
    private static bool IsZero(Expression expression) => expression is ConstantExpression { Value: { } value }
        && value switch
        {
            double d => d == 0,
            float f => f == 0,
            decimal m => m == 0,
            int i => i == 0,
            uint u => u == 0,
            long l => l == 0,
            ulong ul => ul == 0,
            _ => false,
        };

    /// <summary>Counts the zero constants of <paramref name="expression"/>.</summary>
    /// <param name="expression">The tree to inspect.</param>
    /// <returns>The number of zero constants.</returns>
    private static int ZeroCount(Expression expression) => Nodes(expression).Count(IsZero);

    /// <summary>
    /// Asserts that <paramref name="expression"/> contains no redundant zero: no <c>Add</c> with a zero operand,
    /// no <c>Subtract</c> with a zero subtrahend, and a zero constant only as the minuend of <c>0 - term</c>
    /// (the representation of a negative group).
    /// </summary>
    /// <param name="expression">The tree to inspect.</param>
    private static void AssertNoRedundantZero(Expression expression)
    {
        int zeroMinuends = 0;
        foreach (Expression node in Nodes(expression))
        {
            if (node is not BinaryExpression binary) continue;
            if (binary.NodeType == ExpressionType.Add)
            {
                Assert.IsFalse(IsZero(binary.Left) || IsZero(binary.Right), $"Zero addition left in {expression}.");
            }
            else if (binary.NodeType == ExpressionType.Subtract)
            {
                Assert.IsFalse(IsZero(binary.Right), $"Zero subtrahend left in {expression}.");
                if (IsZero(binary.Left))
                {
                    Assert.IsFalse(IsZero(binary.Right) || binary.Right is BinaryExpression { NodeType: ExpressionType.Add or ExpressionType.Subtract, Method: null }, $"Unexpected zero minuend in {expression}.");
                    zeroMinuends++;
                }
            }
        }

        Assert.AreEqual(zeroMinuends, ZeroCount(expression), $"Every zero must be the minuend of a negative group in {expression}.");
    }

    /// <summary>Compiles <paramref name="body"/> over <paramref name="parameters"/> and evaluates it.</summary>
    /// <param name="body">The expression body.</param>
    /// <param name="parameters">The free parameters, in argument order.</param>
    /// <param name="arguments">The argument values.</param>
    /// <returns>The evaluated value.</returns>
    private static object? Evaluate(Expression body, ParameterExpression[] parameters, params object?[] arguments)
        => Expression.Lambda(body, parameters).Compile().DynamicInvoke(arguments);

    /// <summary>The benchmark family B term <c>i</c>: coefficient <c>i + 2</c>, coefficient/parameter order alternating by parity.</summary>
    /// <param name="parameter">The term's parameter.</param>
    /// <param name="index">The term's position.</param>
    /// <returns>The source term.</returns>
    private static Expression FamilyTerm(ParameterExpression parameter, int index)
        => index % 2 == 0
            ? Expression.Multiply(Expression.Constant((double)(index + 2)), parameter)
            : Expression.Multiply(parameter, Expression.Constant((double)(index + 2)));

    /// <summary>Builds <paramref name="count"/> distinct free <see cref="double"/> parameters <c>x0, x1, ...</c>.</summary>
    /// <param name="count">The number of parameters.</param>
    /// <returns>The parameters.</returns>
    private static ParameterExpression[] Parameters(int count)
        => Enumerable.Range(0, count).Select(i => Expression.Parameter(typeof(double), "x" + i)).ToArray();

    /// <summary>The benchmark family B source: the left-associated subtraction chain <c>((t0 - t1) - t2) - ...</c> (same shape as the P9 benchmarks).</summary>
    /// <param name="parameters">One parameter per term.</param>
    /// <returns>The source chain.</returns>
    private static Expression FamilyB(ParameterExpression[] parameters)
    {
        Expression chain = FamilyTerm(parameters[0], 0);
        for (int i = 1; i < parameters.Length; i++) chain = Expression.Subtract(chain, FamilyTerm(parameters[i], i));
        return chain;
    }

    /// <summary>The expected canonical family B result: <c>(2 * x0) + ((0 - (3 * x1)) + (... + (0 - (c * xn))))</c>.</summary>
    /// <param name="parameters">The parameters used to build the source.</param>
    /// <returns>The expected tree.</returns>
    private static Expression ExpectedFamilyB(ParameterExpression[] parameters)
    {
        Expression Term(int i) => Expression.Multiply(Expression.Constant((double)(i + 2)), parameters[i]);
        Expression Negative(int i) => Expression.Subtract(Expression.Constant(0.0), Term(i));

        int last = parameters.Length - 1;
        Expression chain = Negative(last);
        for (int i = last - 1; i >= 1; i--) chain = Expression.Add(Negative(i), chain);
        return Expression.Add(Term(0), chain);
    }

    // ------------------------------------------------------------------------------------------
    // 1. Basic additive forms
    // ------------------------------------------------------------------------------------------

    /// <summary><c>x + 0</c>, <c>0 + x</c> and <c>x - 0</c> return the same parameter; <c>0 - x</c> becomes an ordinary negation.</summary>
    [TestMethod]
    public void NeutralZero_BasicForms()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression zero = Expression.Constant(0.0);

        Assert.AreSame(x, Simplify(Expression.Add(x, zero)));
        Assert.AreSame(x, Simplify(Expression.Add(zero, x)));
        Assert.AreSame(x, Simplify(Expression.Subtract(x, zero)));

        var negate = (UnaryExpression)Simplify(Expression.Subtract(zero, x));
        Assert.AreEqual(ExpressionType.Negate, negate.NodeType);
        Assert.AreSame(x, negate.Operand);
    }

    /// <summary><c>0 + 0</c> and <c>0 - 0</c> fold to a single zero constant.</summary>
    [TestMethod]
    public void ZeroPlusOrMinusZero_IsOneZeroConstant()
    {
        Expression zero = Expression.Constant(0.0);
        Assert.AreEqual("c:Double=0000000000000000", Dump(Simplify(Expression.Add(zero, zero))));
        Assert.AreEqual("c:Double=0000000000000000", Dump(Simplify(Expression.Subtract(zero, zero))));
    }

    /// <summary><c>x - x</c> and <c>x + x - 2*x</c> are zero.</summary>
    [TestMethod]
    public void SelfCancellation_IsZero()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Assert.AreEqual("c:Double=0000000000000000", Dump(Simplify(Expression.Subtract(x, x))));
        Assert.AreEqual("c:Double=0000000000000000", Dump(Simplify(Expression.Subtract(Expression.Add(x, x), Expression.Multiply(Expression.Constant(2.0), x)))));
    }

    /// <summary>
    /// <c>(x - y) + y</c> cancels to <c>x</c> during canonicalization. Before P10 the opposite occurrences of <c>y</c>
    /// were rebuilt as one <c>y - y</c> group next to a stray zero: <c>(0 + ((y - y) + x))</c>.
    /// </summary>
    [TestMethod]
    public void OppositeOccurrences_Cancel()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        Assert.AreSame(x, Simplify(Expression.Add(Expression.Subtract(x, y), y)));
    }

    /// <summary><c>((x - y) - x) + y</c>: every group cancels, so the result is the zero constant (before P10: <c>((0 + 0) + ((y - y) + (x - x)))</c>).</summary>
    [TestMethod]
    public void EveryGroupCancels_IsZero()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        Expression source = Expression.Add(Expression.Subtract(Expression.Subtract(x, y), x), y);
        Assert.AreEqual("c:Double=0000000000000000", Dump(Simplify(source)));
    }

    /// <summary>Several cancelling groups next to a surviving term: <c>(x + y) + z - x - y</c> is <c>z</c>.</summary>
    [TestMethod]
    public void MultipleCancellingGroups_LeaveTheSurvivor()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        ParameterExpression z = Expression.Parameter(typeof(double), "z");
        Expression source = Expression.Subtract(Expression.Subtract(Expression.Add(Expression.Add(x, y), z), x), y);
        Assert.AreSame(z, Simplify(source));
    }

    /// <summary>
    /// A left-associated subtraction chain of plain parameters keeps exactly one zero per negative term, as the
    /// minuend of its <c>0 - term</c> group (before P10: <c>(0 + (0 + 0))</c> was prepended for four terms).
    /// </summary>
    [TestMethod]
    public void SubtractionChain_KeepsOneZeroPerNegativeTerm()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        ParameterExpression z = Expression.Parameter(typeof(double), "z");
        ParameterExpression w = Expression.Parameter(typeof(double), "w");
        Expression source = Expression.Subtract(Expression.Subtract(Expression.Subtract(x, y), z), w);

        Expression result = Simplify(source);

        AssertNoRedundantZero(result);
        Assert.AreEqual(3, ZeroCount(result));
        Assert.AreEqual(1.0 - 2.0 - 3.0 - 4.0, Evaluate(result, [x, y, z, w], 1.0, 2.0, 3.0, 4.0));
    }

    /// <summary>Alternating and nested additions/subtractions keep no redundant zero and evaluate like their source.</summary>
    [TestMethod]
    public void AlternatingAndNestedChains_HaveNoRedundantZero()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        ParameterExpression z = Expression.Parameter(typeof(double), "z");
        ParameterExpression w = Expression.Parameter(typeof(double), "w");
        ParameterExpression[] all = [x, y, z, w];
        Expression[] sources =
        [
            Expression.Subtract(Expression.Add(Expression.Subtract(x, y), z), w),
            Expression.Add(Expression.Subtract(x, Expression.Subtract(y, z)), Expression.Subtract(w, Expression.Subtract(x, y))),
            Expression.Add(Expression.Subtract(x, y), Expression.Subtract(z, w)),
            Expression.Subtract(Expression.Subtract(Expression.Multiply(Expression.Constant(2.0), x), Expression.Multiply(Expression.Constant(3.0), y)), Expression.Multiply(Expression.Constant(4.0), z)),
        ];

        foreach (Expression source in sources)
        {
            Expression result = Simplify(source);
            AssertNoRedundantZero(result);
            Assert.AreEqual(Evaluate(source, all, 1.5, 2.25, 4.0, 8.5), Evaluate(result, all, 1.5, 2.25, 4.0, 8.5), source.ToString());
        }
    }

    /// <summary>Repeated simplification of these canonical results is a fixed point (structurally, including metadata).</summary>
    [TestMethod]
    public void CanonicalResults_AreIdempotent()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        Expression[] sources =
        [
            Expression.Subtract(x, y),
            Expression.Add(Expression.Subtract(x, y), y),
            Expression.Add(Expression.Subtract(x, y), Expression.Parameter(typeof(double), "z")),
            Expression.Subtract(Expression.Multiply(Expression.Constant(2.0), x), Expression.Multiply(Expression.Constant(3.0), y)),
            Expression.Subtract(Expression.Negate(x), y),
            Expression.Subtract(x, x),
        ];

        foreach (Expression source in sources)
        {
            Expression first = Simplify(source);
            Expression second = Simplify(first);
            Assert.AreEqual(Dump(first), Dump(second), source.ToString());
        }
    }

    // ------------------------------------------------------------------------------------------
    // 2. Benchmark family B
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// The benchmark family B chain (n = 4, 8, 12, 16) simplifies to the exact right-associated form
    /// <c>(2 * x0) + ((0 - (3 * x1)) + ...)</c>: n - 1 zeros, all minuends, and 6n - 3 nodes. Before P10 the
    /// result also carried a <c>0 + (0 + ...)</c> group of (n - 1)(n - 2)/2 zeros (B4: 27 nodes, B16: 303).
    /// </summary>
    [TestMethod]
    [DataRow(4)]
    [DataRow(8)]
    [DataRow(12)]
    [DataRow(16)]
    public void FamilyB_HasExactLinearCanonicalForm(int termCount)
    {
        ParameterExpression[] parameters = Parameters(termCount);

        Expression result = Simplify(FamilyB(parameters));

        Assert.AreEqual(Dump(ExpectedFamilyB(parameters)), Dump(result));
        AssertNoRedundantZero(result);
        Assert.AreEqual(termCount - 1, ZeroCount(result));
        Assert.AreEqual(6 * termCount - 3, Nodes(result).Count());
    }

    /// <summary>The family B result evaluates like its source.</summary>
    [TestMethod]
    [DataRow(4)]
    [DataRow(8)]
    [DataRow(12)]
    [DataRow(16)]
    public void FamilyB_EvaluatesLikeSource(int termCount)
    {
        ParameterExpression[] parameters = Parameters(termCount);
        Expression source = FamilyB(parameters);
        object?[] arguments = Enumerable.Range(0, termCount).Select(i => (object?)(i * 0.75 - 3.0)).ToArray();

        Assert.AreEqual((double)Evaluate(source, parameters, arguments)!, (double)Evaluate(Simplify(source), parameters, arguments)!, 1e-9);
    }

    /// <summary>Two independently built family B sources (fresh parameter and node instances) produce the same structure.</summary>
    [TestMethod]
    [DataRow(4)]
    [DataRow(12)]
    public void FamilyB_IsDeterministicAcrossRebuiltSources(int termCount)
    {
        Assert.AreEqual(Dump(Simplify(FamilyB(Parameters(termCount)))), Dump(Simplify(FamilyB(Parameters(termCount)))));
    }

    /// <summary>
    /// Characterization: re-simplifying a family B result is not a fixed point (the subtraction rules rewrite each
    /// <c>0 - term</c>, and the rule set cycles through n - 1 shapes, as it did before P10), but no pass ever
    /// re-accumulates zeros: every pass keeps at most n - 1 zeros, all minuends, and never grows past the first
    /// pass's size.
    /// </summary>
    [TestMethod]
    [DataRow(4)]
    [DataRow(8)]
    public void FamilyB_RepeatedPasses_NeverReaccumulateZeros(int termCount)
    {
        Expression current = Simplify(FamilyB(Parameters(termCount)));
        int firstSize = Nodes(current).Count();

        for (int pass = 2; pass <= 2 * termCount; pass++)
        {
            current = Simplify(current);
            AssertNoRedundantZero(current);
            Assert.IsTrue(ZeroCount(current) <= termCount - 1, $"pass {pass}: {current}");
            Assert.IsTrue(Nodes(current).Count() <= firstSize, $"pass {pass}: {current}");
        }
    }

    // ------------------------------------------------------------------------------------------
    // 3. Types and semantic boundaries
    // ------------------------------------------------------------------------------------------

    /// <summary><see cref="float"/> and <see cref="decimal"/> chains: rebuilt zeros are the result type's own zero (bits 0, scale 0) and opposite terms cancel.</summary>
    [TestMethod]
    public void FloatAndDecimalChains_UseTheirOwnZero()
    {
        foreach ((Type type, string zero) in new[] { (typeof(float), "c:Single=00000000"), (typeof(decimal), "c:Decimal=0,0,0,0") })
        {
            ParameterExpression a = Expression.Parameter(type, "a");
            ParameterExpression b = Expression.Parameter(type, "b");
            ParameterExpression c = Expression.Parameter(type, "c");

            Expression result = Simplify(Expression.Subtract(Expression.Subtract(a, b), c));
            AssertNoRedundantZero(result);
            Assert.AreEqual(2, ZeroCount(result));
            Assert.IsTrue(Nodes(result).OfType<ConstantExpression>().All(constant => Dump(constant) == zero), result.ToString());
            Assert.AreEqual(type, result.Type);

            Assert.AreSame(a, Simplify(Expression.Add(Expression.Subtract(a, b), b)));
        }

        ParameterExpression x = Expression.Parameter(typeof(decimal), "x");
        ParameterExpression y = Expression.Parameter(typeof(decimal), "y");
        ParameterExpression z = Expression.Parameter(typeof(decimal), "z");
        Expression chain = Simplify(Expression.Subtract(Expression.Subtract(x, y), z));
        Assert.AreEqual(1.25m - 0.5m - 2.125m, Evaluate(chain, [x, y, z], 1.25m, 0.5m, 2.125m));
    }

    /// <summary>
    /// Signed zero is treated symbolically, exactly like <c>AdditionWithZero</c>: <c>x + (-0.0)</c> is <c>x</c>,
    /// and the zero of a rebuilt negative group is always <c>+0.0</c>.
    /// </summary>
    [TestMethod]
    public void SignedZero_IsSymbolic()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        Assert.AreSame(x, Simplify(Expression.Add(x, Expression.Constant(-0.0))));

        Expression result = Simplify(Expression.Subtract(Expression.Subtract(x, y), Expression.Parameter(typeof(double), "z")));
        Assert.IsTrue(Nodes(result).OfType<ConstantExpression>().All(constant => Dump(constant) == "c:Double=0000000000000000"), result.ToString());
    }

    /// <summary>
    /// Opposite occurrences of a non-finite constant are not cancelled: <c>(NaN - x) - NaN</c> and
    /// <c>(inf - x) - inf</c> reach the canonicalization with both constants as terms and still evaluate to
    /// <c>NaN</c>, like their sources (cancelling them would yield <c>-x</c>).
    /// </summary>
    [TestMethod]
    public void NonFiniteConstants_AreNotCancelled()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        foreach (double value in new[] { double.NaN, double.PositiveInfinity })
        {
            Expression source = Expression.Subtract(Expression.Subtract(Expression.Constant(value), x), Expression.Constant(value));
            Expression result = Simplify(source);
            Assert.IsTrue(double.IsNaN((double)Evaluate(source, [x], 5.0)!));
            Assert.IsTrue(double.IsNaN((double)Evaluate(result, [x], 5.0)!), result.ToString());
        }
    }

    /// <summary>
    /// A finite <see cref="double"/> constant term beyond the <see cref="decimal"/> range is not mistaken for, or
    /// converted while testing for, a neutral zero: <c>(1e300 - x) - y</c> simplifies without throwing and keeps the
    /// constant.
    /// </summary>
    [TestMethod]
    public void LargeFloatingPointConstantTerm_IsKept()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        Expression source = Expression.Subtract(Expression.Subtract(Expression.Constant(1e300), x), y);

        Expression result = Simplify(source);

        Assert.AreEqual(1e300 - 1.0 - 2.0, Evaluate(result, [x, y], 1.0, 2.0));
        Assert.IsTrue(Nodes(result).OfType<ConstantExpression>().Any(constant => constant.Value is 1e300), result.ToString());
    }

    /// <summary>Signed integers keep wrapping arithmetic exactly, and cancellation still applies.</summary>
    [TestMethod]
    public void SignedIntegerChains_EvaluateLikeSource()
    {
        ParameterExpression a = Expression.Parameter(typeof(int), "a");
        ParameterExpression b = Expression.Parameter(typeof(int), "b");
        ParameterExpression c = Expression.Parameter(typeof(int), "c");
        Expression source = Expression.Subtract(Expression.Subtract(a, b), c);

        Expression result = Simplify(source);

        AssertNoRedundantZero(result);
        Assert.AreEqual(Evaluate(source, [a, b, c], int.MinValue, 7, int.MaxValue), Evaluate(result, [a, b, c], int.MinValue, 7, int.MaxValue));
        Assert.AreSame(a, Simplify(Expression.Add(Expression.Subtract(a, b), b)));
    }

    /// <summary>
    /// Unsigned integers have no ordinary negation. Before P10, <c>(a - b) - c</c>, <c>0 - a</c> and re-simplifying
    /// <c>a - b</c> threw <see cref="InvalidOperationException"/> (<c>SubstractionWithZero</c> built
    /// <c>Negate(uint)</c>); <c>0 - term</c> is now kept as the simplest form and results stay stable.
    /// </summary>
    [TestMethod]
    public void UnsignedIntegerChains_DoNotThrowAndAreStable()
    {
        foreach (Type type in new[] { typeof(uint), typeof(ulong) })
        {
            ParameterExpression a = Expression.Parameter(type, "a");
            ParameterExpression b = Expression.Parameter(type, "b");
            ParameterExpression c = Expression.Parameter(type, "c");
            Expression zero = Expression.Constant(Convert.ChangeType(0, type), type);
            object?[] arguments = [Convert.ChangeType(5, type), Convert.ChangeType(7, type), Convert.ChangeType(11, type)];

            foreach (Expression source in new[]
            {
                Expression.Subtract(a, b),
                Expression.Subtract(Expression.Subtract(a, b), c),
                Expression.Subtract(Expression.Subtract(a, b), a),
                Expression.Subtract(zero, a),
            })
            {
                Expression first = Simplify(source);
                Expression second = Simplify(first);
                Assert.AreEqual(Dump(first), Dump(second), source.ToString());
                Assert.AreEqual(Evaluate(source, [a, b, c], arguments), Evaluate(first, [a, b, c], arguments), source.ToString());
            }
        }
    }

    /// <summary><c>SubstractionWithZero(0 - right)</c> declines a type without an ordinary negation instead of throwing, and still negates a signed type.</summary>
    [TestMethod]
    public void SubstractionWithZero_DeclinesUnsignedTypes()
    {
        var simplifier = new ExpressionSimplifier();
        ParameterExpression unsigned = Expression.Parameter(typeof(uint), "u");
        ConstantExpression unsignedZero = Expression.Constant(0u);
        Assert.IsNull(simplifier.SubstractionWithZero(Expression.Subtract(unsignedZero, unsigned), unsignedZero, unsigned));

        ParameterExpression signed = Expression.Parameter(typeof(int), "i");
        ConstantExpression signedZero = Expression.Constant(0);
        Expression negated = simplifier.SubstractionWithZero(Expression.Subtract(signedZero, signed), signedZero, signed);
        Assert.AreEqual(ExpressionType.Negate, negated.NodeType);
    }

    /// <summary>Lifted nullable chains are not canonicalized: the tree is unchanged and <c>null</c> still propagates.</summary>
    [TestMethod]
    public void LiftedNullableChains_AreLeftUntouched()
    {
        ParameterExpression a = Expression.Parameter(typeof(int?), "a");
        ParameterExpression b = Expression.Parameter(typeof(int?), "b");
        ParameterExpression c = Expression.Parameter(typeof(int?), "c");
        Expression source = Expression.Add(Expression.Subtract(Expression.Subtract(a, b), c), b);

        Expression result = Simplify(source);

        Assert.AreEqual(Dump(source), Dump(result));
        Assert.IsNull(Evaluate(result, [a, b, c], 1, null, 3));
        Assert.AreEqual(Evaluate(source, [a, b, c], 1, 2, 3), Evaluate(result, [a, b, c], 1, 2, 3));
    }

    /// <summary>
    /// A nested custom subtraction stays one atomic term with its method: two structurally identical custom
    /// nodes of opposite sign cancel as wholes, while a lone one is kept intact.
    /// </summary>
    [TestMethod]
    public void NestedCustomOperators_StayAtomic()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        ParameterExpression z = Expression.Parameter(typeof(double), "z");

        Expression cancelled = Expression.Add(Expression.Subtract(x, Expression.Subtract(y, z, CustomSubtractMethod)), Expression.Subtract(y, z, CustomSubtractMethod));
        Assert.AreSame(x, Simplify(cancelled));

        Expression kept = Expression.Subtract(Expression.Subtract(x, Expression.Subtract(y, z, CustomSubtractMethod)), z);
        Expression result = Simplify(kept);
        Assert.IsTrue(Nodes(result).OfType<BinaryExpression>().Any(node => node.Method == CustomSubtractMethod), result.ToString());
        AssertNoRedundantZero(result);
        Assert.AreEqual(Evaluate(kept, [x, y, z], 1.0, 2.0, 3.0), Evaluate(result, [x, y, z], 1.0, 2.0, 3.0));
    }

    /// <summary>A custom root subtraction is not canonicalized: a zero subtrahend under it is kept.</summary>
    [TestMethod]
    public void CustomRootSubtraction_KeepsItsZeroOperand()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression source = Expression.Subtract(x, Expression.Constant(0.0), CustomSubtractMethod);
        Assert.AreEqual(Dump(source), Dump(Simplify(source)));
    }

    /// <summary>
    /// A function-like group clusters distinct calls with the same arguments (<c>sin(x)</c> and <c>cos(x)</c>), so
    /// it is never cancelled: <c>(sin(x) + y) - cos(x)</c> still evaluates like its source.
    /// </summary>
    [TestMethod]
    public void FunctionLikeGroups_AreNotCancelled()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        Expression sin = Expression.Call(typeof(Math).GetMethod(nameof(Math.Sin))!, x);
        Expression cos = Expression.Call(typeof(Math).GetMethod(nameof(Math.Cos))!, x);
        Expression source = Expression.Subtract(Expression.Add(sin, y), cos);

        Expression result = Simplify(source);

        Assert.AreEqual((double)Evaluate(source, [x, y], 0.3, 2.0)!, (double)Evaluate(result, [x, y], 0.3, 2.0)!, 1e-12);
        Assert.AreEqual(2, Nodes(result).OfType<MethodCallExpression>().Count(), result.ToString());
    }

    // ------------------------------------------------------------------------------------------
    // 4. S4 structural canonicalization
    // ------------------------------------------------------------------------------------------

    /// <summary>Inside a lambda, bound parameters cancel, and alpha-equivalent lambdas give identical structures.</summary>
    [TestMethod]
    public void LambdaBoundParameters_CancelAndAreAlphaEquivalent()
    {
        static LambdaExpression Build(string first, string second)
        {
            ParameterExpression a = Expression.Parameter(typeof(double), first);
            ParameterExpression b = Expression.Parameter(typeof(double), second);
            return Expression.Lambda(Expression.Subtract(Expression.Subtract(Expression.Add(Expression.Subtract(a, b), b), b), a), a, b);
        }

        var left = (LambdaExpression)Simplify(Build("a", "b"));
        var right = (LambdaExpression)Simplify(Build("q", "p"));

        // a cancels; the single remaining negative group is the canonical "0 - b".
        Assert.AreEqual(Dump(left), Dump(right));
        var subtract = (BinaryExpression)left.Body;
        Assert.AreEqual(ExpressionType.Subtract, subtract.NodeType);
        Assert.AreEqual("c:Double=0000000000000000", Dump(subtract.Left));
        Assert.AreSame(left.Parameters[1], subtract.Right);
    }

    /// <summary>Distinct free parameters sharing a name never cancel each other: <c>(x - x') + x'</c> keeps the first <c>x</c> instance.</summary>
    [TestMethod]
    public void SameNamedFreeParameters_StayDistinct()
    {
        ParameterExpression first = Expression.Parameter(typeof(double), "x");
        ParameterExpression second = Expression.Parameter(typeof(double), "x");

        Assert.AreSame(first, Simplify(Expression.Add(Expression.Subtract(first, second), second)));
        Expression kept = Simplify(Expression.Subtract(first, second));
        Assert.AreEqual(2.0, Evaluate(kept, [first, second], 5.0, 3.0));
    }

    /// <summary>The term order does not depend on <see cref="Expression.ToString()"/>: renaming the parameters gives the same structure.</summary>
    [TestMethod]
    public void TermOrder_DoesNotDependOnNames()
    {
        static Expression Build(string first, string second)
        {
            ParameterExpression a = Expression.Parameter(typeof(double), first);
            ParameterExpression b = Expression.Parameter(typeof(double), second);
            return Expression.Subtract(Expression.Multiply(Expression.Constant(5.0), a), Expression.Multiply(b, Expression.Constant(2.0)));
        }

        Assert.AreEqual(Dump(Simplify(Build("a", "b"))), Dump(Simplify(Build("zz", "aa"))));
    }

    /// <summary>Member-access terms group structurally and cancel: <c>(holder.Value - y) + holder.Value</c> keeps one member access.</summary>
    [TestMethod]
    public void MemberTerms_GroupAndCancelStructurally()
    {
        var holder = new ValueHolder { Value = 4.0 };
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        Expression Member() => Expression.Field(Expression.Constant(holder), nameof(ValueHolder.Value));

        Expression result = Simplify(Expression.Subtract(Expression.Add(Expression.Subtract(Member(), y), y), Member()));

        Assert.AreEqual("c:Double=0000000000000000", Dump(result));
    }

    /// <summary>A plain holder for <see cref="MemberTerms_GroupAndCancelStructurally"/>.</summary>
    public sealed class ValueHolder
    {
        /// <summary>The held value.</summary>
        public double Value;
    }

    // ------------------------------------------------------------------------------------------
    // 5. Subclass compatibility
    // ------------------------------------------------------------------------------------------

    /// <summary>A derived simplifier with no overrides produces the same canonical family B form and the same cancellations as the built-in type.</summary>
    [TestMethod]
    public void DerivedSimplifier_MatchesBuiltInCanonicalForm()
    {
        ParameterExpression[] parameters = Parameters(6);
        Expression source = FamilyB(parameters);
        Assert.AreEqual(Dump(Simplify(source)), Dump(new MinimalDerivedSimplifier().Simplify(source)));

        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        Assert.AreSame(x, new MinimalDerivedSimplifier().Simplify(Expression.Add(Expression.Subtract(x, y), y)));
    }
}
