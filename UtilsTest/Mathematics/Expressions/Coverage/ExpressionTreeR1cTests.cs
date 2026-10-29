using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>
/// ETC R1c (compatibility entry C-04): structural equality, hashing and canonical keys of <see cref="ConditionalExpression"/>,
/// <see cref="InvocationExpression"/>, <see cref="TypeBinaryExpression"/>, <see cref="DefaultExpression"/>,
/// <see cref="NewExpression"/> and <see cref="NewArrayExpression"/>, their parameter binding, their properties (reflexive,
/// symmetric, transitive equality; hash consistent with equality; antisymmetric, transitive keys), and the boundary between
/// structural equality and algebraic interchangeability.
/// </summary>
[TestClass]
[TestCategory("ETC-R1c")]
public class ExpressionTreeR1cTests
{
    /// <summary>The free parameters of the samples.</summary>
    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");

    /// <summary>A second free parameter, with the same name as <see cref="X"/> on purpose.</summary>
    private static readonly ParameterExpression SameNameX = Expression.Parameter(typeof(double), "x");

    /// <summary>A free Boolean parameter.</summary>
    private static readonly ParameterExpression B = Expression.Parameter(typeof(bool), "b");

    /// <summary>A delegate that counts its invocations, to prove comparison and hashing never invoke it.</summary>
    private static int _delegateCalls;

    /// <summary>The counting delegate.</summary>
    private static readonly Func<double, double> Counting = value => { _delegateCalls++; return value; };

    /// <summary>A class whose constructor counts its invocations.</summary>
    public sealed class Counted
    {
        /// <summary>The number of constructions.</summary>
        public static int Constructions;

        /// <summary>The first value.</summary>
        public double A;

        /// <summary>The second value.</summary>
        public double B;

        /// <summary>Initializes an instance, counting the call.</summary>
        /// <param name="a">The first value.</param>
        /// <param name="b">The second value.</param>
        public Counted(double a, double b)
        {
            Constructions++;
            A = a;
            B = b;
        }

        /// <summary>Initializes an instance from one value, counting the call.</summary>
        /// <param name="a">The value.</param>
        public Counted(double a)
            : this(a, 0.0)
        {
        }
    }

    /// <summary>The two-argument constructor of <see cref="Counted"/>.</summary>
    private static readonly ConstructorInfo Pair = typeof(Counted).GetConstructor([typeof(double), typeof(double)])!;

    /// <summary>The one-argument constructor of <see cref="Counted"/>.</summary>
    private static readonly ConstructorInfo Single = typeof(Counted).GetConstructor([typeof(double)])!;

    /// <summary>Builds, for every new family, a factory of a sample and of the same sample differing in exactly one field.</summary>
    /// <returns>The named factories: <c>Build</c> gives a fresh, structurally identical sample on every call; <c>Variant</c> differs in one field.</returns>
    private static (string Name, Func<Expression> Build, Func<Expression> Variant)[] Families()
    {
        Expression Add(double c) => Expression.Add(X, Expression.Constant(c));
        MemberInfo[] members = [typeof(Counted).GetField(nameof(Counted.A))!, typeof(Counted).GetField(nameof(Counted.B))!];
        MemberInfo[] swapped = [members[1], members[0]];
        return
        [
            ("conditional", () => Expression.Condition(B, Add(1), Add(2)), () => Expression.Condition(B, Add(2), Add(1))),
            ("invoke", () => Expression.Invoke(Expression.Constant(Counting), Add(1)), () => Expression.Invoke(Expression.Constant(Counting), Add(3))),
            ("typeIs", () => Expression.TypeIs(Expression.Convert(Add(1), typeof(object)), typeof(double)), () => Expression.TypeIs(Expression.Convert(Add(1), typeof(object)), typeof(int))),
            ("typeEqual", () => Expression.TypeEqual(Expression.Convert(Add(1), typeof(object)), typeof(double)), () => Expression.TypeIs(Expression.Convert(Add(1), typeof(object)), typeof(double))),
            ("default", () => Expression.Default(typeof(double)), () => Expression.Default(typeof(float))),
            ("new", () => Expression.New(Pair, Add(1), Add(2)), () => Expression.New(Pair, Add(1), Add(3))),
            ("newCtor", () => Expression.New(Pair, Add(1), Expression.Constant(0.0)), () => Expression.New(Single, Add(1))),
            ("newMembers", () => Expression.New(Pair, [Add(1), Add(2)], members), () => Expression.New(Pair, [Add(1), Add(2)], swapped)),
            ("newValueDefault", () => Expression.New(typeof(DateTime)), () => Expression.New(typeof(TimeSpan))),
            ("newArrayInit", () => Expression.NewArrayInit(typeof(double), Add(1), Add(2)), () => Expression.NewArrayInit(typeof(double), Add(2), Add(1))),
            ("newArrayBounds", () => Expression.NewArrayBounds(typeof(double), Expression.Constant(2), Expression.Constant(3)), () => Expression.NewArrayBounds(typeof(double), Expression.Constant(3), Expression.Constant(2))),
            ("newArrayKind", () => Expression.NewArrayInit(typeof(int), Expression.Constant(2)), () => Expression.NewArrayBounds(typeof(int), Expression.Constant(2))),
        ];
    }

    /// <summary>Runs <paramref name="key"/> comparison of two trees through the internal canonical order.</summary>
    /// <param name="x">The first tree.</param>
    /// <param name="y">The second tree.</param>
    /// <returns>The sign of the comparison.</returns>
    private static int Order(Expression x, Expression y) => Math.Sign(ExpressionCanonicalOrder.Compare(x, y, []));

    /// <summary>
    /// For every family, two independently built samples are equal (public and raw comparers), hash equally, find each other
    /// in a <see cref="HashSet{T}"/> and a <see cref="Dictionary{TKey, TValue}"/>, and tie in the canonical order; a sample
    /// differing in one field is unequal and ordered antisymmetrically.
    /// </summary>
    [TestMethod]
    public void EveryFamily_EqualPairs_UnequalVariants()
    {
        foreach ((string name, Func<Expression> build, Func<Expression> variant) in Families())
        {
            Expression first = build();
            Expression second = build();
            Expression other = variant();

            Assert.IsTrue(ExpressionComparer.Default.Equals(first, second), name);
            Assert.AreEqual(ExpressionComparer.Default.GetHashCode(first), ExpressionComparer.Default.GetHashCode(second), name);
            Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(first, second), name);
            Assert.AreEqual(ExpressionComparer.StructuralHashRaw(first), ExpressionComparer.StructuralHashRaw(second), name);
            Assert.AreEqual(0, Order(first, second), name);

            var set = new HashSet<Expression>(ExpressionComparer.Default) { first };
            Assert.IsTrue(set.Contains(second), name);
            var map = new Dictionary<Expression, string>(ExpressionComparer.Default) { [first] = name };
            Assert.AreEqual(name, map[second], name);

            Assert.IsFalse(ExpressionComparer.Default.Equals(first, other), name);
            Assert.IsFalse(ExpressionComparer.StructuralEqualsRaw(first, other), name);
            Assert.IsFalse(set.Contains(other), name);
            Assert.AreNotEqual(0, Order(first, other), name);
            Assert.AreEqual(-Order(first, other), Order(other, first), name);
        }
    }

    /// <summary>Nested samples (each family holding another) are equal when built independently and unequal when an inner field differs.</summary>
    [TestMethod]
    public void NestedFamilies_AreComparedAtEveryLevel()
    {
        Expression Nested(double leaf) => Expression.Condition(
            Expression.TypeIs(Expression.Convert(Expression.Invoke(Expression.Constant(Counting), Expression.Field(Expression.New(Pair, X, Expression.Constant(leaf)), nameof(Counted.B))), typeof(object)), typeof(double)),
            Expression.ArrayIndex(Expression.NewArrayInit(typeof(double), X, Expression.Default(typeof(double))), Expression.Constant(0)),
            Expression.Constant(leaf));

        Assert.IsTrue(ExpressionComparer.Default.Equals(Nested(1.0), Nested(1.0)));
        Assert.AreEqual(ExpressionComparer.Default.GetHashCode(Nested(1.0)), ExpressionComparer.Default.GetHashCode(Nested(1.0)));
        Assert.AreEqual(0, Order(Nested(1.0), Nested(1.0)));
        Assert.IsFalse(ExpressionComparer.Default.Equals(Nested(1.0), Nested(2.0)));
        Assert.AreNotEqual(0, Order(Nested(1.0), Nested(2.0)));
    }

    /// <summary>
    /// Parameter binding: inside a lambda, two alpha-equivalent bodies holding the new families are equal; a free parameter
    /// keeps its identity (same name is not enough); bound versus free, and swapped declaration positions, are unequal.
    /// </summary>
    [TestMethod]
    public void ParameterBinding_IsRespectedInsideTheNewFamilies()
    {
        LambdaExpression Lambda(string first, string second, bool swap)
        {
            ParameterExpression p = Expression.Parameter(typeof(double), first);
            ParameterExpression q = Expression.Parameter(typeof(double), second);
            Expression body = Expression.Condition(B, Expression.Invoke(Expression.Constant(Counting), swap ? q : p), Expression.Field(Expression.New(Pair, p, q), nameof(Counted.A)));
            return Expression.Lambda(body, p, q);
        }

        Assert.IsTrue(ExpressionComparer.Default.Equals(Lambda("p", "q", false), Lambda("a", "b", false)));
        Assert.AreEqual(ExpressionComparer.Default.GetHashCode(Lambda("p", "q", false)), ExpressionComparer.Default.GetHashCode(Lambda("a", "b", false)));
        Assert.AreEqual(0, Order(Lambda("p", "q", false), Lambda("a", "b", false)));
        Assert.IsFalse(ExpressionComparer.Default.Equals(Lambda("p", "q", false), Lambda("p", "q", true)));

        Expression free = Expression.NewArrayInit(typeof(double), X);
        Expression sameName = Expression.NewArrayInit(typeof(double), SameNameX);
        Assert.IsFalse(ExpressionComparer.Default.Equals(free, sameName), "A name is not a parameter.");
        Assert.IsTrue(ExpressionComparer.Default.Equals(free, Expression.NewArrayInit(typeof(double), X)));

        ParameterExpression bound = Expression.Parameter(typeof(double), "x");
        Expression boundLambda = Expression.Lambda(Expression.NewArrayInit(typeof(double), bound), bound);
        Expression freeLambda = Expression.Lambda(Expression.NewArrayInit(typeof(double), X), Expression.Parameter(typeof(double), "x"));
        Assert.IsFalse(ExpressionComparer.Default.Equals(boundLambda, freeLambda), "Bound and free references differ.");
    }

    /// <summary>Equality and hashing never invoke the delegate of an invocation or the constructor of a creation.</summary>
    [TestMethod]
    public void ComparisonAndHashing_RunNoUserCode()
    {
        _delegateCalls = 0;
        Counted.Constructions = 0;
        foreach ((_, Func<Expression> build, Func<Expression> variant) in Families())
        {
            ExpressionComparer.Default.Equals(build(), build());
            ExpressionComparer.Default.Equals(build(), variant());
            ExpressionComparer.Default.GetHashCode(build());
            Order(build(), variant());
        }

        Assert.AreEqual(0, _delegateCalls);
        Assert.AreEqual(0, Counted.Constructions);
    }

    /// <summary>
    /// Bounded generated corpus: equality is reflexive, symmetric and transitive, the hash is consistent with equality, and
    /// the canonical order is reflexive, antisymmetric, transitive and consistent with equality (equal trees tie).
    /// </summary>
    [TestMethod]
    public void Properties_HoldOnABoundedCorpus()
    {
        var corpus = new List<Expression>();
        foreach ((_, Func<Expression> build, Func<Expression> variant) in Families())
        {
            corpus.Add(build());
            corpus.Add(build());
            corpus.Add(variant());
        }

        corpus.Add(Expression.Condition(B, Expression.Default(typeof(double)), X));
        corpus.Add(Expression.Invoke(Expression.Constant(Counting), Expression.Default(typeof(double))));

        int n = corpus.Count;
        var equal = new bool[n, n];
        var order = new int[n, n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                equal[i, j] = ExpressionComparer.StructuralEqualsRaw(corpus[i], corpus[j]);
                order[i, j] = Order(corpus[i], corpus[j]);
            }
        }

        for (int i = 0; i < n; i++)
        {
            Assert.IsTrue(equal[i, i], $"reflexive {i}");
            Assert.AreEqual(0, order[i, i], $"order reflexive {i}");
            for (int j = 0; j < n; j++)
            {
                Assert.AreEqual(equal[i, j], equal[j, i], $"symmetric {i},{j}");
                Assert.AreEqual(-order[i, j], order[j, i], $"antisymmetric {i},{j}");
                if (equal[i, j])
                {
                    Assert.AreEqual(ExpressionComparer.StructuralHashRaw(corpus[i]), ExpressionComparer.StructuralHashRaw(corpus[j]), $"hash {i},{j}");
                    Assert.AreEqual(0, order[i, j], $"equal trees tie {i},{j}");
                }

                for (int k = 0; k < n; k++)
                {
                    if (equal[i, j] && equal[j, k]) Assert.IsTrue(equal[i, k], $"transitive {i},{j},{k}");
                    if (order[i, j] <= 0 && order[j, k] <= 0) Assert.IsTrue(order[i, k] <= 0, $"order transitive {i},{j},{k}");
                }
            }
        }
    }

    /// <summary>
    /// The public comparer (which simplifies first) and the raw comparer agree on the new families, and the public hash is
    /// consistent with the public equality on pairs that only become equal after simplification.
    /// </summary>
    [TestMethod]
    public void PublicAndRawPaths_AreConsistent()
    {
        Expression simplifiable = Expression.Condition(B, Expression.Multiply(X, Expression.Constant(1.0)), Expression.Add(X, Expression.Constant(0.0)));
        Expression simplified = Expression.Condition(B, X, X);

        Assert.IsTrue(ExpressionComparer.Default.Equals(simplifiable, simplified));
        Assert.AreEqual(ExpressionComparer.Default.GetHashCode(simplifiable), ExpressionComparer.Default.GetHashCode(simplified));
        Assert.IsFalse(ExpressionComparer.StructuralEqualsRaw(simplifiable, simplified), "The raw comparer does not simplify.");
    }

    /// <summary>
    /// Structural equality is not interchangeability (§12 of the roadmap): two identical creations, array creations and
    /// invocations compare equal, yet the simplifier never merges, cancels or drops them, and their evaluations are kept.
    /// </summary>
    [TestMethod]
    public void StructuralEquality_DoesNotMakeAllocationsOrInvocationsInterchangeable()
    {
        Expression Created() => Expression.Field(Expression.New(Single, X), nameof(Counted.A));
        Expression Element() => Expression.ArrayIndex(Expression.NewArrayInit(typeof(double), X), Expression.Constant(0));
        Expression Invoked() => Expression.Invoke(Expression.Constant(Counting), X);
        var simplifier = new ExpressionSimplifier();

        foreach (Func<Expression> make in new Func<Expression>[] { Created, Element, Invoked })
        {
            Assert.IsTrue(ExpressionComparer.Default.Equals(make(), make()));
            Expression difference = simplifier.Simplify(Expression.Subtract(make(), make()));
            Expression sum = simplifier.Simplify(Expression.Add(make(), make()));
            Assert.AreNotEqual(ExpressionType.Constant, difference.NodeType, difference.ToString());
            Assert.AreNotEqual(ExpressionType.Multiply, sum.NodeType, sum.ToString());
        }

        Counted.Constructions = 0;
        _delegateCalls = 0;
        Expression body = simplifier.Simplify(Expression.Add(Expression.Subtract(Created(), Created()), Expression.Subtract(Invoked(), Invoked())));
        Assert.AreEqual(0.0, Expression.Lambda<Func<double, double>>(body, X).Compile()(2.0));
        Assert.AreEqual(2, Counted.Constructions);
        Assert.AreEqual(2, _delegateCalls);
    }

    /// <summary>
    /// Canonicalization with the new families as terms converges regardless of source order, and is idempotent: two pure
    /// conditionals and a default ordered in two directions give the same tree, and simplifying it again changes nothing.
    /// </summary>
    [TestMethod]
    public void Canonicalization_WithNewFamilies_ConvergesAndIsIdempotent()
    {
        Expression first = Expression.Condition(B, X, Expression.Constant(1.0));
        Expression second = Expression.Condition(B, Expression.Constant(2.0), X);
        Expression third = Expression.Multiply(Expression.Constant(3.0), Expression.Default(typeof(double)));
        var simplifier = new ExpressionSimplifier();

        Expression forward = simplifier.Simplify(Expression.Add(Expression.Add(first, second), third));
        Expression backward = simplifier.Simplify(Expression.Add(Expression.Add(third, second), first));

        Assert.AreEqual(forward.ToString(), backward.ToString());
        Assert.AreEqual(forward.ToString(), simplifier.Simplify(forward).ToString());
        Assert.IsTrue(ExpressionComparer.Default.Equals(forward, backward));
    }
}
