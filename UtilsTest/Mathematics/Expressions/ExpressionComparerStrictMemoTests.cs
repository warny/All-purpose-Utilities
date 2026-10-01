using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>Verifies the strict safe-arithmetic identity and recursive memo session used by factoring comparisons.</summary>
[TestClass]
public class ExpressionComparerStrictMemoTests
{
    /// <summary>The internal strict equality helper.</summary>
    private static readonly MethodInfo StrictEqualsMethod = GetComparerMethod("StrictMemoEquals");

    /// <summary>The internal strict hash helper.</summary>
    private static readonly MethodInfo StrictHashMethod = GetComparerMethod("StrictMemoHash");

    /// <summary>The existing raw structural equality helper used to characterize public commutativity.</summary>
    private static readonly MethodInfo StructuralEqualsRawMethod = GetComparerMethod("StructuralEqualsRaw");

    /// <summary>The internal memo eligibility helper.</summary>
    private static readonly MethodInfo IsEligibleMethod = GetComparerMethod("IsEligibleForSimplificationMemo");

    /// <summary>The internal session-entry helper.</summary>
    private static readonly MethodInfo EnterSessionMethod = GetComparerMethod("EnterSimplificationMemoSession");

    /// <summary>The internal session-exit helper.</summary>
    private static readonly MethodInfo ExitSessionMethod = GetComparerMethod("ExitSimplificationMemoSession");

    /// <summary>The internal memo-enabled simplification overload.</summary>
    private static readonly MethodInfo MemoSimplifyMethod = typeof(ExpressionComparer).GetMethod(
        "SimplifyForComparison",
        BindingFlags.NonPublic | BindingFlags.Static,
        binder: null,
        types: [typeof(Expression), typeof(bool)],
        modifiers: null)!;

    /// <summary>Finds a required non-public static comparer helper.</summary>
    /// <param name="name">The helper name.</param>
    /// <returns>The reflected helper.</returns>
    private static MethodInfo GetComparerMethod(string name)
        => typeof(ExpressionComparer).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>Invokes strict memo equality.</summary>
    /// <param name="x">The first expression.</param>
    /// <param name="y">The second expression.</param>
    /// <returns>The strict equality result.</returns>
    private static bool StrictEquals(Expression x, Expression y) => (bool)StrictEqualsMethod.Invoke(null, [x, y])!;

    /// <summary>Invokes the existing raw structural equality helper.</summary>
    /// <param name="x">The first expression.</param>
    /// <param name="y">The second expression.</param>
    /// <returns>The raw structural equality result.</returns>
    private static bool StructuralEqualsRaw(Expression x, Expression y)
        => (bool)StructuralEqualsRawMethod.Invoke(null, [x, y])!;

    /// <summary>Invokes strict memo hashing.</summary>
    /// <param name="expression">The expression to hash.</param>
    /// <returns>The structural hash.</returns>
    private static int StrictHash(Expression expression) => (int)StrictHashMethod.Invoke(null, [expression])!;

    /// <summary>Invokes memo eligibility classification.</summary>
    /// <param name="expression">The expression to classify.</param>
    /// <returns>The eligibility result.</returns>
    private static bool IsEligible(Expression expression) => (bool)IsEligibleMethod.Invoke(null, [expression])!;

    /// <summary>Enters a memo session.</summary>
    private static void EnterSession() => EnterSessionMethod.Invoke(null, null);

    /// <summary>Exits a memo session.</summary>
    private static void ExitSession() => ExitSessionMethod.Invoke(null, null);

    /// <summary>Simplifies through the factoring-only memo path.</summary>
    /// <param name="expression">The expression to simplify.</param>
    /// <returns>The completed simplified result.</returns>
    private static Expression MemoSimplify(Expression expression)
        => (Expression)MemoSimplifyMethod.Invoke(null, [expression, true])!;

    /// <summary>Strict memo identity preserves operand orientation even where the public raw comparer is commutative.</summary>
    [TestMethod]
    public void StrictIdentity_DoesNotApplyCommutativeFallback()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        ParameterExpression b = Expression.Parameter(typeof(double), "b");
        Expression add = Expression.Add(a, b);
        Expression swappedAdd = Expression.Add(b, a);
        Expression multiply = Expression.Multiply(a, b);
        Expression swappedMultiply = Expression.Multiply(b, a);

        Assert.IsTrue(StructuralEqualsRaw(add, swappedAdd));
        Assert.IsFalse(StrictEquals(add, swappedAdd));
        Assert.IsFalse(StrictEquals(multiply, swappedMultiply));
    }

    /// <summary>Strict memo identity preserves binary association.</summary>
    [TestMethod]
    public void StrictIdentity_DoesNotConflateDifferentAssociation()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        ParameterExpression b = Expression.Parameter(typeof(double), "b");
        ParameterExpression c = Expression.Parameter(typeof(double), "c");

        Assert.IsFalse(StrictEquals(Expression.Add(Expression.Add(a, b), c), Expression.Add(a, Expression.Add(b, c))));
    }

    /// <summary>Distinct containers with the same oriented shape and shared parameter leaves are equal and hash alike.</summary>
    [TestMethod]
    public void StrictIdentity_RebuiltIdenticalTree_IsEqualAndHashesEqually()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        ParameterExpression b = Expression.Parameter(typeof(double), "b");
        ParameterExpression c = Expression.Parameter(typeof(double), "c");
        Expression first = Expression.Multiply(Expression.Add(a, b), c);
        Expression second = Expression.Multiply(Expression.Add(a, b), c);

        Assert.IsFalse(ReferenceEquals(first, second));
        Assert.IsFalse(ReferenceEquals(((BinaryExpression)first).Left, ((BinaryExpression)second).Left));
        Assert.IsTrue(StrictEquals(first, second));
        Assert.AreEqual(StrictHash(first), StrictHash(second));
    }

    /// <summary>Free parameters require the exact parameter object, regardless of matching type and name.</summary>
    [TestMethod]
    public void StrictIdentity_DistinctSameNamedParameters_AreNotEqual()
    {
        ParameterExpression first = Expression.Parameter(typeof(double), "x");
        ParameterExpression second = Expression.Parameter(typeof(double), "x");

        Assert.IsFalse(StrictEquals(first, second));
    }

    /// <summary>Eligibility accepts an ordinary native double arithmetic tree.</summary>
    [TestMethod]
    public void Eligibility_OrdinaryNumericArithmetic_IsAccepted()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        Expression expression = Expression.Divide(
            Expression.Add(
                Expression.Multiply(Expression.Constant(2.0), x),
                Expression.Multiply(Expression.Constant(3.0), y)),
            Expression.Constant(4.0));

        Assert.IsTrue(IsEligible(expression));
    }

    /// <summary>Eligibility rejects every deliberately excluded expression category.</summary>
    [TestMethod]
    public void Eligibility_UnsafeOrNonArithmeticNodes_AreRejected()
    {
        ParameterExpression nullable = Expression.Parameter(typeof(int?), "n");
        MethodInfo binaryOperator = typeof(CustomNumber).GetMethod("op_Addition", BindingFlags.Public | BindingFlags.Static)!;
        MethodInfo unaryOperator = typeof(CustomNumber).GetMethod("op_UnaryNegation", BindingFlags.Public | BindingFlags.Static)!;
        ParameterExpression custom = Expression.Parameter(typeof(CustomNumber), "c");

        Expression[] rejected =
        [
            Expression.Call(typeof(double).GetMethod(nameof(double.Sin), [typeof(double)])!, Expression.Constant(1.0)),
            Expression.Lambda(Expression.Constant(1.0)),
            Expression.Constant(new object(), typeof(object)),
            Expression.Add(custom, custom, binaryOperator),
            Expression.Add(nullable, nullable),
            Expression.Negate(custom, unaryOperator),
        ];

        foreach (Expression expression in rejected)
        {
            Assert.IsFalse(IsEligible(expression), $"{expression.NodeType} should be outside the memo boundary.");
        }
    }

    /// <summary>A rebuilt strict-identical source reuses the completed result in one session.</summary>
    [TestMethod]
    public void MemoSession_RebuiltIdenticalTree_ReusesCompletedResult()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression first = Expression.Add(Expression.Multiply(Expression.Constant(2.0), x), Expression.Constant(3.0));
        Expression second = Expression.Add(Expression.Multiply(Expression.Constant(2.0), x), Expression.Constant(3.0));

        EnterSession();
        try
        {
            Assert.AreSame(MemoSimplify(first), MemoSimplify(second));
        }
        finally
        {
            ExitSession();
        }
    }

    /// <summary>The outermost session exit releases prior results instead of reusing them in a later session.</summary>
    [TestMethod]
    public void MemoSession_OutermostExit_ClearsState()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression first = Expression.Add(x, Expression.Constant(3.0));
        Expression second = Expression.Add(x, Expression.Constant(3.0));
        Expression previous;

        EnterSession();
        try
        {
            previous = MemoSimplify(first);
        }
        finally
        {
            ExitSession();
        }

        EnterSession();
        try
        {
            Assert.AreNotSame(previous, MemoSimplify(second));
        }
        finally
        {
            ExitSession();
        }
    }

    /// <summary>A nested session shares the outer cache, while only the outermost exit clears it.</summary>
    [TestMethod]
    public void MemoSession_NestedScope_SharesUntilOutermostExit()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression first = Expression.Add(x, Expression.Constant(3.0));
        Expression second = Expression.Add(x, Expression.Constant(3.0));
        Expression third = Expression.Add(x, Expression.Constant(3.0));

        EnterSession();
        try
        {
            Expression cached = MemoSimplify(first);
            EnterSession();
            try
            {
                Assert.AreSame(cached, MemoSimplify(second));
            }
            finally
            {
                ExitSession();
            }

            Assert.AreSame(cached, MemoSimplify(third));
        }
        finally
        {
            ExitSession();
        }
    }

    /// <summary>A failed eligible simplification is propagated and never installed as a completed cache entry.</summary>
    [TestMethod]
    public void MemoSession_ThrowingSimplification_IsNotCached()
    {
        Expression divisionByZero = Expression.Divide(Expression.Constant(1), Expression.Constant(0));

        EnterSession();
        try
        {
            AssertMemoSimplificationThrows(divisionByZero);
            AssertMemoSimplificationThrows(divisionByZero);
        }
        finally
        {
            ExitSession();
        }
    }

    /// <summary>Independent worker threads do not observe another thread's active memo result.</summary>
    [TestMethod]
    public void MemoSession_ConcurrentThreads_DoNotShareState()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression first = Expression.Add(x, Expression.Constant(3.0));
        Expression second = Expression.Add(x, Expression.Constant(3.0));
        Expression[] results = new Expression[2];

        Parallel.Invoke(
            () =>
            {
                EnterSession();
                try { results[0] = MemoSimplify(first); }
                finally { ExitSession(); }
            },
            () =>
            {
                EnterSession();
                try { results[1] = MemoSimplify(second); }
                finally { ExitSession(); }
            });

        Assert.AreNotSame(results[0], results[1]);
    }

    /// <summary>Asserts that reflection reports a failed memo-enabled simplification.</summary>
    /// <param name="expression">The expression expected to fail.</param>
    private static void AssertMemoSimplificationThrows(Expression expression)
    {
        try
        {
            MemoSimplify(expression);
            Assert.Fail("Simplification should have thrown.");
        }
        catch (TargetInvocationException exception)
        {
            Assert.IsNotNull(exception.InnerException);
        }
    }

    /// <summary>A custom numeric-looking value used solely to construct custom operator nodes.</summary>
    private readonly struct CustomNumber
    {
        /// <summary>Defines a custom addition operator for exclusion testing.</summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        /// <returns>A default value.</returns>
        public static CustomNumber operator +(CustomNumber left, CustomNumber right) => default;

        /// <summary>Defines a custom negation operator for exclusion testing.</summary>
        /// <param name="value">The value.</param>
        /// <returns>A default value.</returns>
        public static CustomNumber operator -(CustomNumber value) => default;
    }
}
