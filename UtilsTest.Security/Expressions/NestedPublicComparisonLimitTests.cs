using System;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Security.Expressions;

/// <summary>
/// S5 P19d review (R1): a public <see cref="ExpressionComparer.Equals(Expression?, Expression?)"/> made from user code while a
/// simplification runs is an independent operation with its own limits. Its comparer simplifications must never be served
/// by the memo the running operation retains (S5 P19d): a hit would skip work that its own structural budget is meant to
/// bound, so a budget that refuses the comparison on its own would accept it inside an operation. The running operation's
/// memo, retention flag and resource counters are restored exactly after the nested call, whether it succeeds, is refused
/// or throws.
/// </summary>
[TestClass]
public class NestedPublicComparisonLimitTests
{
    /// <summary>The allow-listed <see cref="Math.Truncate(double)"/> function of the opaque carrier.</summary>
    private static readonly MethodInfo Truncate = typeof(Math).GetMethod(nameof(Math.Truncate), [typeof(double)])!;

    /// <summary>A free parameter of type <see cref="double"/>.</summary>
    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");

    /// <summary>A constant value whose <see cref="Equals(object?)"/> runs a callback, then returns <see langword="false"/>.</summary>
    private sealed class CallbackConstant
    {
        /// <summary>The callback run by <see cref="Equals(object?)"/>.</summary>
        private readonly Action _callback;

        /// <summary>Initializes the constant.</summary>
        /// <param name="callback">The callback run by <see cref="Equals(object?)"/>.</param>
        public CallbackConstant(Action callback) => _callback = callback;

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            _callback();
            return false;
        }

        /// <inheritdoc/>
        public override int GetHashCode() => 0;
    }

    /// <summary>Marker exception thrown by <see cref="HostileConstant"/>.</summary>
    private sealed class HostileConstantException : Exception { }

    /// <summary>A constant value whose <see cref="Equals(object?)"/> always throws.</summary>
    private sealed class HostileConstant
    {
        /// <inheritdoc/>
        public override bool Equals(object? obj) => throw new HostileConstantException();

        /// <inheritdoc/>
        public override int GetHashCode() => 0;
    }

    /// <summary>
    /// Builds the algebraically safe carrier of an opaque constant, <c>Math.Truncate((value as double?) ?? 1.0)</c> (the
    /// <c>SafeOpaqueTerm</c> of the unit tests): the simplifier may merge such terms, so their comparisons reach the value.
    /// </summary>
    /// <param name="value">The opaque constant value.</param>
    /// <returns>The carrier term, which evaluates to <c>1.0</c>.</returns>
    private static Expression Carrier(object value)
        => Expression.Call(
            Truncate,
            Expression.Coalesce(Expression.TypeAs(Expression.Constant(value, typeof(object)), typeof(double?)), Expression.Constant(1.0)));

    /// <summary>Builds <c>2 * a + 3 * b</c> over two opaque carriers, whose factoring compares <paramref name="a"/> and <paramref name="b"/>.</summary>
    /// <param name="a">The first opaque value.</param>
    /// <param name="b">The second opaque value.</param>
    /// <returns>The source tree.</returns>
    private static Expression OpaqueSum(object a, object b)
        => Expression.Add(
            Expression.Multiply(Expression.Constant(2.0), Carrier(a)),
            Expression.Multiply(Expression.Constant(3.0), Carrier(b)));

    /// <summary>
    /// The first operand of the nested comparison, <c>(2x + 1) + 3x</c>: an arithmetic depth of 3 activates the memo and
    /// has a strict key, and its simplification consumes structural visits. Every call builds new nodes.
    /// </summary>
    /// <returns>A new tree.</returns>
    private static Expression Left()
        => Expression.Add(
            Expression.Add(Expression.Multiply(Expression.Constant(2.0), X), Expression.Constant(1.0)),
            Expression.Multiply(Expression.Constant(3.0), X));

    /// <summary>The second operand of the nested comparison, <c>(1 + 5x) * 2</c>; every call builds new nodes.</summary>
    /// <returns>A new tree.</returns>
    private static Expression Right()
        => Expression.Multiply(
            Expression.Add(Expression.Constant(1.0), Expression.Multiply(Expression.Constant(5.0), X)),
            Expression.Constant(2.0));

    /// <summary>Gets limits whose only binding constraint is <paramref name="structuralVisits"/>.</summary>
    /// <param name="structuralVisits">The structural budget.</param>
    /// <returns>The limits.</returns>
    private static ExpressionResourceLimits StructuralBudget(long structuralVisits) => new()
    {
        MaxDepth = int.MaxValue,
        MaxNodes = long.MaxValue,
        MaxTransformVisits = long.MaxValue,
        MaxStructuralVisits = structuralVisits,
    };

    /// <summary>
    /// The thread's comparison-memo and resource state, as seen from the user code running inside an operation.
    /// </summary>
    /// <param name="MemoDepth">The comparison-memo depth.</param>
    /// <param name="MemoActive">Whether the comparison memo is active.</param>
    /// <param name="MemoRetained">Whether the comparison memo is retained by a transformation frame (S5 P19d).</param>
    /// <param name="Entries">The entries of the strict memo.</param>
    /// <param name="ObservedEntries">The entries of the observed memo.</param>
    /// <param name="OperationDepth">The operation depth of the resource session.</param>
    /// <param name="StructuralDeadline">The active structural deadline.</param>
    /// <param name="StructuralLimit">The active structural limit.</param>
    /// <param name="StructuralBase">The base of the active structural limit.</param>
    /// <param name="TransformDeadline">The active transformation deadline.</param>
    private readonly record struct ThreadState(
        int MemoDepth,
        bool MemoActive,
        bool MemoRetained,
        int Entries,
        int ObservedEntries,
        int OperationDepth,
        long StructuralDeadline,
        long StructuralLimit,
        long StructuralBase,
        long TransformDeadline)
    {
        /// <summary>Captures the current thread's state; an operation must be running.</summary>
        /// <returns>The state.</returns>
        public static ThreadState Capture()
        {
            ExpressionResourceBudget.Session session = ExpressionResourceBudget.ActiveSession ?? throw new AssertFailedException("no running operation");
            return new ThreadState(
                ExpressionSimplifier.ComparisonMemoDepth,
                ExpressionSimplifier.IsComparisonMemoActive,
                ExpressionSimplifier.IsComparisonMemoRetained,
                ExpressionSimplifier.ComparisonMemoEntryCount,
                ExpressionSimplifier.ObservedComparisonMemoEntryCount,
                session.OperationDepth,
                session.StructuralDeadline,
                session.StructuralLimit,
                session.StructuralBase,
                session.TransformDeadline);
        }
    }

    /// <summary>
    /// Measures the structural visits of a public comparison of <see cref="Left"/> and <see cref="Right"/> with an empty memo:
    /// run inside a bare operation with no transformation frame, so the nested budgets apply from the counters already
    /// consumed exactly as they do inside a simplification, but no memo can serve it.
    /// </summary>
    /// <param name="result">The comparison result.</param>
    /// <returns>The structural visits consumed by the comparison.</returns>
    private static long MeasureDetachedCost(out bool result)
    {
        (long Cost, bool Result) measured = ExpressionResourceBudget.Run(ExpressionResourceLimits.Unlimited, null, null, null, 0, static _ =>
        {
            Assert.IsFalse(ExpressionSimplifier.IsComparisonMemoRetained);
            long before = ExpressionResourceBudget.StructuralVisits;
            bool equal = ExpressionComparer.Create(ExpressionResourceLimits.Unlimited).Equals(Left(), Right());
            return (ExpressionResourceBudget.StructuralVisits - before, equal);
        });
        result = measured.Result;
        return measured.Cost;
    }

    /// <summary>Asserts that the current thread holds no comparison-memo state and runs no operation.</summary>
    private static void AssertNoState()
    {
        Assert.AreEqual(0, ExpressionSimplifier.ComparisonMemoDepth);
        Assert.IsFalse(ExpressionSimplifier.IsComparisonMemoActive);
        Assert.IsFalse(ExpressionSimplifier.HasComparisonMemo);
        Assert.IsFalse(ExpressionSimplifier.HasObservedComparisonMemo);
        Assert.IsFalse(ExpressionSimplifier.IsComparisonMemoRetained);
        Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
    }

    /// <summary>Unwraps the reflective rule-invocation layers of an exception.</summary>
    /// <param name="exception">The caught exception.</param>
    /// <returns>The innermost exception.</returns>
    private static Exception Unwrap(Exception exception)
    {
        while (exception is TargetInvocationException { InnerException: { } inner }) exception = inner;
        return exception;
    }

    /// <summary>
    /// R1: inside an unlimited simplification whose retained memo already holds the simplifications of both operands, a
    /// nested public comparison of fresh copies of them costs exactly what it costs with an empty memo (it never reuses the
    /// caller's entries); its own structural budget is then exact: the measured cost is accepted, one visit less is refused
    /// with a <see cref="ExpressionResourceLimitKind.StructuralWork"/> exception that reaches the user code unchanged. The
    /// budget applies from the counters already consumed, the refusal consumes exactly the budget plus the refused visit, and
    /// after each call the caller's memo (entries, depth, activation, retention), its resource deadlines and its later reuse
    /// are restored. No state survives the operation.
    /// </summary>
    [TestMethod]
    public void NestedPublicEquals_AppliesItsOwnStructuralBudget_WithoutTheRetainedMemo()
    {
        long cost = MeasureDetachedCost(out bool expected);
        Assert.IsTrue(cost > 1, $"the nested comparison must consume structural visits (cost {cost})");
        AssertNoState();

        bool ran = false;
        var spy = new CallbackConstant(() =>
        {
            if (ran || ExpressionSimplifier.ComparisonMemoDepth != 0) return;
            ran = true;
            Assert.IsTrue(ExpressionSimplifier.IsComparisonMemoRetained, "the spy runs between two sessions of the operation");

            // Fill the operation's retained memo with both operands' simplifications.
            Expression callerLeft = ExpressionComparer.SimplifyForComparison(Left());
            Expression callerRight = ExpressionComparer.SimplifyForComparison(Right());
            Assert.AreSame(callerLeft, ExpressionComparer.SimplifyForComparison(Left()), "the caller's memo serves the left operand");
            Assert.AreSame(callerRight, ExpressionComparer.SimplifyForComparison(Right()), "the caller's memo serves the right operand");
            ThreadState saved = ThreadState.Capture();
            Assert.IsTrue(saved.Entries >= 2, $"the caller's memo holds both operands ({saved.Entries})");

            void AssertRestored(string label)
            {
                Assert.AreEqual(saved, ThreadState.Capture(), label);
                Assert.AreSame(callerLeft, ExpressionComparer.SimplifyForComparison(Left()), label + ": the caller's entry is reused");
                Assert.AreEqual(saved, ThreadState.Capture(), label + ": a hit adds no entry");
            }

            // 1. Unlimited: the nested comparison does the whole work again (no hit on the caller's entries).
            long before = ExpressionResourceBudget.StructuralVisits;
            Assert.AreEqual(expected, ExpressionComparer.Create(ExpressionResourceLimits.Unlimited).Equals(Left(), Right()));
            Assert.AreEqual(cost, ExpressionResourceBudget.StructuralVisits - before, "the nested comparison must not reuse the caller's memo");
            AssertRestored("unlimited");

            // 2. Exactly sufficient budget.
            before = ExpressionResourceBudget.StructuralVisits;
            Assert.AreEqual(expected, ExpressionComparer.Create(StructuralBudget(cost)).Equals(Left(), Right()));
            Assert.AreEqual(cost, ExpressionResourceBudget.StructuralVisits - before);
            AssertRestored("exact budget");

            // 3. One visit short: a controlled refusal, never absorbed into a Boolean result.
            before = ExpressionResourceBudget.StructuralVisits;
            ExpressionResourceLimitException refusal = Assert.ThrowsExactly<ExpressionResourceLimitException>(
                () => ExpressionComparer.Create(StructuralBudget(cost - 1)).Equals(Left(), Right()));
            Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, refusal.Kind);
            Assert.AreEqual(cost - 1, refusal.Limit);
            Assert.AreEqual(cost, ExpressionResourceBudget.StructuralVisits - before, "the refused visit is the first one past the nested budget");
            AssertRestored("refused");
        });

        AssertNoState();
        new ExpressionSimplifier(ExpressionResourceLimits.Unlimited).Simplify(OpaqueSum(spy, new object()));

        Assert.IsTrue(ran, "The spy must run between two comparer sessions.");
        AssertNoState();
    }

    /// <summary>
    /// R1: a nested public comparison that fails during the comparison itself (an opaque constant whose
    /// <see cref="object.Equals(object?)"/> throws while the comparer factors its operands), rather than on a limit, propagates
    /// the user exception to the user code that called it and restores the caller's memo and resource state exactly; the
    /// operation then completes normally and leaves no state.
    /// </summary>
    [TestMethod]
    public void NestedPublicEquals_ThatThrows_RestoresTheOperation()
    {
        bool ran = false;
        Exception? failure = null;
        var spy = new CallbackConstant(() =>
        {
            if (ran || ExpressionSimplifier.ComparisonMemoDepth != 0) return;
            ran = true;
            Expression callerLeft = ExpressionComparer.SimplifyForComparison(Left());
            ThreadState saved = ThreadState.Capture();

            Expression hostile = OpaqueSum(new HostileConstant(), new HostileConstant());
            try
            {
                ExpressionComparer.Create(ExpressionResourceLimits.Default).Equals(hostile, Left());
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            Assert.AreEqual(saved, ThreadState.Capture());
            Assert.AreSame(callerLeft, ExpressionComparer.SimplifyForComparison(Left()), "the caller's entry is intact");
        });

        new ExpressionSimplifier(ExpressionResourceLimits.Unlimited).Simplify(OpaqueSum(spy, new object()));

        Assert.IsTrue(ran, "The spy must run between two comparer sessions.");
        Assert.IsNotNull(failure, "the nested comparison must throw");
        Assert.IsInstanceOfType<HostileConstantException>(Unwrap(failure));
        AssertNoState();
    }
}
