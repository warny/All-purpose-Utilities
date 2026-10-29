using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Validates the S5 P15 observed comparison memo and the factor probe's observed reuse: comparer simplifications that
/// ran no user code are reused for strictly identical operands (opaque constants by reference identity), while a
/// simplification that called a constant's own <see cref="object.Equals(object?)"/> or
/// <see cref="object.GetHashCode"/>, or threw, is never reused.
/// </summary>
[TestClass]
public class ExpressionSimplifierObservedComparisonMemoTests
{
    /// <summary>Returns a constant for an opaque argument, embedding user objects in numeric terms.</summary>
    /// <param name="value">An opaque value, ignored.</param>
    /// <returns>Always one.</returns>
    public static double IdentityFromObject(object value) => 1.0;

    /// <summary>Returns the sum of two values for a custom-operator expression.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>The sum.</returns>
    public static double CustomAdd(double left, double right) => left + right;

    /// <summary>Returns the opposite of a value for a custom-operator expression.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The opposite.</returns>
    public static double CustomNegate(double value) => -value;

    /// <summary>The method embedding opaque constants in numeric terms.</summary>
    private static readonly MethodInfo Identity = typeof(ExpressionSimplifierObservedComparisonMemoTests).GetMethod(nameof(IdentityFromObject))!;

    /// <summary>The <see cref="Math.Sin(double)"/> method.</summary>
    private static readonly MethodInfo Sin = typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!;

    /// <summary>Marker exception thrown by a hostile constant.</summary>
    private sealed class HostileConstantException : Exception { }

    /// <summary>A constant value counting its <see cref="Equals(object?)"/>/<see cref="GetHashCode"/> calls.</summary>
    private sealed class CountingConstant
    {
        /// <summary>The answer of <see cref="Equals(object?)"/>.</summary>
        private readonly bool _equal;

        /// <summary>Whether <see cref="Equals(object?)"/> throws.</summary>
        private readonly bool _throws;

        /// <summary>A callback run by <see cref="Equals(object?)"/>, or <see langword="null"/>.</summary>
        private readonly Action? _callback;

        /// <summary>Initializes the constant.</summary>
        /// <param name="equal">The answer of <see cref="Equals(object?)"/>.</param>
        /// <param name="throws">Whether <see cref="Equals(object?)"/> throws.</param>
        /// <param name="callback">A callback run by <see cref="Equals(object?)"/>, or <see langword="null"/>.</param>
        public CountingConstant(bool equal = false, bool throws = false, Action? callback = null)
        {
            _equal = equal;
            _throws = throws;
            _callback = callback;
        }

        /// <summary>Gets the number of <see cref="Equals(object?)"/> calls.</summary>
        public int EqualsCallCount { get; private set; }

        /// <summary>Gets the number of <see cref="GetHashCode"/> calls.</summary>
        public int HashCallCount { get; private set; }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            EqualsCallCount++;
            _callback?.Invoke();
            if (_throws) throw new HostileConstantException();
            return _equal;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            HashCallCount++;
            return 0;
        }
    }

    /// <summary>An extension node whose members are user code.</summary>
    private sealed class OpaqueExtension : Expression
    {
        /// <inheritdoc/>
        public override ExpressionType NodeType => ExpressionType.Extension;

        /// <inheritdoc/>
        public override Type Type => typeof(double);
    }

    /// <summary>Builds the term <c>coefficient * IdentityFromObject(value)</c>.</summary>
    /// <param name="coefficient">The numeric coefficient.</param>
    /// <param name="value">The opaque value.</param>
    /// <returns>The term.</returns>
    private static Expression OpaqueTerm(double coefficient, object value)
        => Expression.Multiply(Expression.Constant(coefficient), SafeOpaqueTerm.Carrier(value));

    /// <summary>
    /// Builds the subtraction chain <c>100*Id(first) - x1*3 - 4*x2 - ...</c> whose opaque first term sorts last (the
    /// P15 pathological shape), or the same chain over <paramref name="first"/> when it is a known-safe value.
    /// </summary>
    /// <param name="n">The number of terms.</param>
    /// <param name="first">The value of the first term.</param>
    /// <returns>The chain.</returns>
    private static Expression TailSortedChain(int n, object first)
    {
        ParameterExpression[] xs = Enumerable.Range(0, n).Select(i => Expression.Parameter(typeof(double), "x" + i)).ToArray();
        Expression accumulator = OpaqueTerm(100, first);
        for (int i = 1; i < n; i++)
        {
            Expression term = i % 2 == 0
                ? Expression.Multiply(Expression.Constant((double)(i + 2)), xs[i])
                : Expression.Multiply(xs[i], Expression.Constant((double)(i + 2)));
            accumulator = Expression.Subtract(accumulator, term);
        }

        return accumulator;
    }

    /// <summary>Counts the comparer simplifications of one public simplification, through the diagnostic capture.</summary>
    /// <param name="source">The expression to simplify.</param>
    /// <returns>The number of comparer-triggered simplifications.</returns>
    private static int CountComparerSimplifications(Expression source)
    {
        ExpressionComparer.BeginDiagnosticCapture();
        try
        {
            new ExpressionSimplifier().Simplify(source);
        }
        finally
        {
            ExpressionComparer.EndDiagnosticCapture();
        }

        ExpressionComparer.BeginDiagnosticCapture();
        new ExpressionSimplifier().Simplify(source);
        return ExpressionComparer.EndDiagnosticCapture().Count;
    }

    /// <summary>Asserts that the current thread holds no comparison-memo state of either tier.</summary>
    private static void AssertNoMemoState()
    {
        Assert.AreEqual(0, ExpressionSimplifier.ComparisonMemoDepth);
        Assert.IsFalse(ExpressionSimplifier.IsComparisonMemoActive);
        Assert.IsFalse(ExpressionSimplifier.HasComparisonMemo);
        Assert.IsFalse(ExpressionSimplifier.HasObservedComparisonMemo);
    }

    /// <summary>Asserts that <paramref name="action"/> throws <typeparamref name="T"/>, possibly wrapped by reflective rule invocation.</summary>
    /// <typeparam name="T">The expected innermost exception type.</typeparam>
    /// <param name="action">The action.</param>
    private static void AssertThrowsInnermost<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            while (exception.InnerException is not null) exception = exception.InnerException;
            Assert.IsInstanceOfType(exception, typeof(T));
            return;
        }

        Assert.Fail($"Expected {typeof(T).Name}.");
    }

    // ------------------------------------------------------------------------------------------
    // User-code counter
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// The comparer counts every call it makes into an opaque value's own <see cref="object.Equals(object?)"/> or
    /// <see cref="object.GetHashCode"/>, and nothing else: known-safe values and the reference-identity raw policy are not
    /// counted.
    /// </summary>
    [TestMethod]
    public void UserCodeCounter_CountsOpaqueEqualsAndHashOnly()
    {
        var a = new CountingConstant();
        var b = new CountingConstant();
        Expression ca = Expression.Constant(a, typeof(object));
        Expression cb = Expression.Constant(b, typeof(object));

        long before = ExpressionComparer.UserCodeInvocationCount;
        ExpressionComparer.StructuralEqualsAfterSimplification(ca, cb);
        Assert.AreEqual(before + 1, ExpressionComparer.UserCodeInvocationCount);
        Assert.AreEqual(1, a.EqualsCallCount);

        before = ExpressionComparer.UserCodeInvocationCount;
        ExpressionComparer.Default.GetHashCode(ca);
        Assert.AreEqual(before + 1, ExpressionComparer.UserCodeInvocationCount);
        Assert.AreEqual(1, a.HashCallCount);

        before = ExpressionComparer.UserCodeInvocationCount;
        ExpressionComparer.StructuralEqualsRaw(ca, cb);
        ExpressionComparer.StructuralHashRaw(ca);
        ExpressionComparer.StructuralEqualsAfterSimplification(Expression.Constant("text", typeof(object)), Expression.Constant("text", typeof(object)));
        ExpressionComparer.Default.GetHashCode(Expression.Constant("text"));
        Assert.AreEqual(before, ExpressionComparer.UserCodeInvocationCount);
        Assert.AreEqual(1, a.EqualsCallCount);
        Assert.AreEqual(1, a.HashCallCount);

        var hostile = new CountingConstant(throws: true);
        before = ExpressionComparer.UserCodeInvocationCount;
        Assert.ThrowsExactly<HostileConstantException>(() => ExpressionComparer.StructuralEqualsAfterSimplification(Expression.Constant(hostile, typeof(object)), cb));
        Assert.AreEqual(before + 1, ExpressionComparer.UserCodeInvocationCount, "A call that throws is counted before it runs.");
    }

    // ------------------------------------------------------------------------------------------
    // Strict observed keys
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Opaque constants are keyed by reference identity: rebuilt operands over the same value share a key, distinct
    /// values never do (even when their own <see cref="object.Equals(object?)"/> answers true), and building or
    /// comparing keys runs no user code.
    /// </summary>
    [TestMethod]
    public void ObservedKeys_UseReferenceIdentity_WithoutUserCode()
    {
        var shared = new CountingConstant(equal: true);
        var other = new CountingConstant(equal: true);
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression first = Expression.Add(OpaqueTerm(2, shared), x);
        Expression rebuilt = Expression.Add(OpaqueTerm(2, shared), x);
        Expression distinct = Expression.Add(OpaqueTerm(2, other), x);
        long before = ExpressionComparer.UserCodeInvocationCount;

        Assert.IsTrue(ExpressionSimplifier.ObservedComparisonMemoKeysEqual(first, rebuilt));
        Assert.IsFalse(ExpressionSimplifier.ObservedComparisonMemoKeysEqual(first, distinct));
        Assert.IsFalse(ExpressionSimplifier.ObservedComparisonMemoKeysEqual(first, Expression.Add(x, OpaqueTerm(2, shared))), "Keys are oriented.");
        Assert.IsFalse(ExpressionSimplifier.ObservedComparisonMemoKeysEqual(first, Expression.Add(OpaqueTerm(2, shared), Expression.Parameter(typeof(double), "x"))));
        Assert.IsFalse(ExpressionSimplifier.ObservedComparisonMemoKeysEqual(Expression.Constant(0.0), Expression.Constant(-0.0)), "Signed zeros are distinct.");

        Assert.AreEqual(0, shared.EqualsCallCount + shared.HashCallCount + other.EqualsCallCount + other.HashCallCount);
        Assert.AreEqual(before, ExpressionComparer.UserCodeInvocationCount);
    }

    /// <summary>
    /// The observed tier accepts calls, members, conversions and opaque or null constants; it rejects lambdas,
    /// conditionals, quotes, extension nodes, conversion lambdas, user-defined operators (as P9 does) and numeric-like
    /// values outside the primitive types.
    /// The P9 eligibility of the same operands is unchanged.
    /// </summary>
    [TestMethod]
    public void ObservedEligibility_Boundaries()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression call = Expression.Call(Sin, x);
        Expression opaque = OpaqueTerm(2, new CountingConstant());
        Expression[] eligible =
        [
            call,
            Expression.Add(opaque, x),
            Expression.Property(Expression.Constant("text"), nameof(string.Length)),
            Expression.Convert(x, typeof(float)),
            Expression.Constant(new CountingConstant(), typeof(object)),
            Expression.Constant(null, typeof(object)),
            Expression.Multiply(Expression.Constant(2.0), x),
        ];
        Expression[] ineligible =
        [
            Expression.Lambda(Expression.Add(x, x), x),
            Expression.Add(Expression.Condition(Expression.Constant(true), x, x), x),
            Expression.Quote(Expression.Lambda(x, x)),
            Expression.Add(new OpaqueExtension(), x),
            Expression.Constant(new BigInteger(3), typeof(object)),
            Expression.Constant(1.0, typeof(object)),
            Expression.Add(x, Expression.Constant(1.0), typeof(ExpressionSimplifierObservedComparisonMemoTests).GetMethod(nameof(CustomAdd))),
            Expression.Negate(x, typeof(ExpressionSimplifierObservedComparisonMemoTests).GetMethod(nameof(CustomNegate))),
            Expression.Coalesce(Expression.Parameter(typeof(double?), "n"), Expression.Constant(1.0), Expression.Lambda(Expression.Constant(2.0), Expression.Parameter(typeof(double?), "m"))),
        ];

        foreach (Expression expression in eligible) Assert.IsTrue(ExpressionSimplifier.IsObservedComparisonMemoEligible(expression), expression.ToString());
        foreach (Expression expression in ineligible) Assert.IsFalse(ExpressionSimplifier.IsObservedComparisonMemoEligible(expression), expression.ToString());

        Assert.IsFalse(ExpressionSimplifier.IsComparisonMemoEligible(call));
        Assert.IsFalse(ExpressionSimplifier.IsComparisonMemoEligible(opaque));
        Assert.IsTrue(ExpressionSimplifier.IsComparisonMemoEligible(Expression.Multiply(Expression.Constant(2.0), x)));
    }

    // ------------------------------------------------------------------------------------------
    // Session behavior
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// In an activated session, a call-bearing operand whose simplification ran no user code is stored in the observed
    /// tier (not in the P9 memo) and reused for a rebuilt identical operand; a hit adds no entry.
    /// </summary>
    [TestMethod]
    public void ActivatedSession_ReusesUserCodeFreeCallBearingResult()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Func<Expression> build = () => Expression.Subtract(Expression.Multiply(Expression.Constant(2.0), Expression.Call(Sin, x)), Expression.Multiply(x, Expression.Constant(3.0)));
        string expected = new ExpressionSimplifier().Simplify(build()).ToString();

        using (ExpressionSimplifier.BeginActivatedComparisonMemoSessionForTesting())
        {
            Expression first = ExpressionComparer.SimplifyForComparison(build());
            int entries = ExpressionSimplifier.ObservedComparisonMemoEntryCount;
            Expression second = ExpressionComparer.SimplifyForComparison(build());

            Assert.IsTrue(entries >= 1);
            Assert.AreSame(first, second);
            Assert.AreEqual(entries, ExpressionSimplifier.ObservedComparisonMemoEntryCount);
            Assert.AreEqual(expected, first.ToString());
        }

        AssertNoMemoState();
    }

    /// <summary>
    /// A simplification that called an opaque constant's own <see cref="object.Equals(object?)"/> is not stored: the
    /// same operand, simplified again in the same session, calls the user code again and builds a new tree.
    /// </summary>
    [TestMethod]
    public void ActivatedSession_DoesNotReuseSimplificationThatRanUserCode()
    {
        var c0 = new CountingConstant();
        var c1 = new CountingConstant();
        Expression operand = Expression.Add(OpaqueTerm(2, c0), OpaqueTerm(3, c1));

        using (ExpressionSimplifier.BeginActivatedComparisonMemoSessionForTesting())
        {
            Expression first = ExpressionComparer.SimplifyForComparison(operand);
            int calls = c0.EqualsCallCount + c1.EqualsCallCount;
            Expression second = ExpressionComparer.SimplifyForComparison(operand);

            Assert.IsTrue(calls > 0, "The factor comparison must have reached the opaque constants.");
            Assert.AreEqual(2 * calls, c0.EqualsCallCount + c1.EqualsCallCount);
            Assert.AreNotSame(first, second);
            Assert.AreEqual(first.ToString(), second.ToString());
        }

        AssertNoMemoState();
    }

    /// <summary>A user-code exception is never cached: it propagates on every attempt and the session state is restored.</summary>
    [TestMethod]
    public void ActivatedSession_UserCodeExceptionsAreNeverCached()
    {
        var hostile = new CountingConstant(throws: true);
        Expression operand = Expression.Add(OpaqueTerm(2, hostile), OpaqueTerm(3, new CountingConstant()));

        using (ExpressionSimplifier.BeginActivatedComparisonMemoSessionForTesting())
        {
            AssertThrowsInnermost<HostileConstantException>(() => ExpressionComparer.SimplifyForComparison(operand));
            AssertThrowsInnermost<HostileConstantException>(() => ExpressionComparer.SimplifyForComparison(operand));
            Assert.AreEqual(2, hostile.EqualsCallCount);
            Assert.AreEqual(1, ExpressionSimplifier.ComparisonMemoDepth);
        }

        AssertNoMemoState();
    }

    /// <summary>
    /// A public simplification started from user code inside an active session neither sees nor populates the caller's
    /// observed memo, and the caller's entries are intact afterwards.
    /// </summary>
    [TestMethod]
    public void IndependentPublicCall_FromUserCode_IsDetachedFromObservedMemo()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression cached = Expression.Add(Expression.Call(Sin, x), x);
        bool observedMemo = true;
        int callbacks = 0;
        var spy = new CountingConstant(callback: () =>
        {
            callbacks++;
            observedMemo = ExpressionSimplifier.HasObservedComparisonMemo;
        });
        Expression independent = Expression.Add(OpaqueTerm(2, spy), OpaqueTerm(3, new CountingConstant()));

        using (ExpressionSimplifier.BeginActivatedComparisonMemoSessionForTesting())
        {
            Expression cachedResult = ExpressionComparer.SimplifyForComparison(cached);
            int entries = ExpressionSimplifier.ObservedComparisonMemoEntryCount;
            Assert.IsTrue(entries >= 1);

            new ExpressionSimplifier().Simplify(independent);

            Assert.IsTrue(callbacks > 0);
            Assert.IsFalse(observedMemo);
            Assert.AreEqual(entries, ExpressionSimplifier.ObservedComparisonMemoEntryCount);
            Assert.AreSame(cachedResult, ExpressionComparer.SimplifyForComparison(cached));
        }

        AssertNoMemoState();
    }

    /// <summary>Another thread never sees the observed memo of an open session.</summary>
    [TestMethod]
    public void ObservedMemo_IsThreadLocal()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        bool otherThreadHasMemo = true;
        Exception? failure = null;

        using (ExpressionSimplifier.BeginActivatedComparisonMemoSessionForTesting())
        {
            ExpressionComparer.SimplifyForComparison(Expression.Add(Expression.Call(Sin, x), x));
            Assert.IsTrue(ExpressionSimplifier.HasObservedComparisonMemo);

            var thread = new Thread(() =>
            {
                try
                {
                    otherThreadHasMemo = ExpressionSimplifier.HasObservedComparisonMemo;
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });
            thread.Start();
            Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(30)));
        }

        Assert.IsNull(failure);
        Assert.IsFalse(otherThreadHasMemo);
        AssertNoMemoState();
    }

    // ------------------------------------------------------------------------------------------
    // End-to-end work reduction
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// A chain whose only opaque term is never compared costs exactly the comparer simplifications of the same chain
    /// over a known-safe constant: the factor probe no longer simplifies such a sub-chain twice per decision. Before
    /// P15 the opaque chain cost 8,429 comparer simplifications against 2,104 for n = 6.
    /// </summary>
    /// <param name="n">The number of terms.</param>
    [TestMethod]
    [DataRow(4)]
    [DataRow(6)]
    [DataRow(8)]
    public void UncomparedOpaqueTerm_CostsTheSameAsSafeControl(int n)
    {
        int opaque = CountComparerSimplifications(TailSortedChain(n, new CountingConstant()));
        int safe = CountComparerSimplifications(TailSortedChain(n, "safe"));

        Assert.AreEqual(safe, opaque);
    }

    /// <summary>
    /// The pathological chain (opaque term sorted last) no longer grows by a factor of about five per term: nine terms
    /// cost under 60,000 comparer simplifications (about 980,000 before P15), with no user call.
    /// </summary>
    [TestMethod]
    public void TailSortedOpaqueChain_ComparerWorkIsBounded()
    {
        var constant = new CountingConstant();
        int events = CountComparerSimplifications(TailSortedChain(9, constant));

        Assert.IsTrue(events < 60_000, $"{events} comparer simplifications.");
        Assert.AreEqual(0, constant.EqualsCallCount + constant.HashCallCount);
        AssertNoMemoState();
    }
}
