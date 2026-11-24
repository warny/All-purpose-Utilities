using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;
using static UtilsTest.Security.Expressions.ExpressionResourceTestTrees;

namespace UtilsTest.Security.Expressions;

/// <summary>
/// Security invariants of the boundary finishing of refused chains (mission C3c, consistency of the level and whole-chain
/// factoring, <c>docs/expressions/SimplifierFlatten.md</c> §21): it never reads an extension node nor a custom reflection object,
/// its state never leaks out of an operation, a refusal or a thread, and its work is counted, linear in the chain and refused
/// just above each limit.
/// </summary>
[TestClass]
public class ExpressionSimplifierFactoringConsistencyResourceTests
{
    /// <summary>The stack size of the worker threads.</summary>
    private const int SmallStack = 256 * 1024;

    /// <summary>The maximum duration of a worker thread.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(120);

    /// <summary>Limits without any bound.</summary>
    private static readonly ExpressionResourceLimits NoLimits = ExpressionResourceLimits.Unlimited;

    /// <summary>
    /// Builds <c>(2 - 1) + x0*x1 + x0*x2 + ...</c>, left-associated, iteratively: refused by its constant pair, a factorable chain
    /// once the levels fold it, factored at its boundary.
    /// </summary>
    /// <param name="terms">The number of products.</param>
    /// <returns>The chain.</returns>
    private static Expression RefusedFactorableChain(int terms)
    {
        ParameterExpression x0 = Expression.Parameter(typeof(double), "x0");
        Expression chain = Expression.Subtract(Expression.Constant(2.0), Expression.Constant(1.0));
        for (int i = 1; i <= terms; i++) chain = Expression.Add(chain, Expression.Multiply(x0, Expression.Parameter(typeof(double), "x" + i)));
        return chain;
    }

    /// <summary>Simplifies <paramref name="e"/> inside an outer operation and returns the visit counters of the whole call.</summary>
    /// <param name="simplifier">The simplifier.</param>
    /// <param name="e">The expression.</param>
    /// <returns>The transformation and structural visits.</returns>
    private static (long Transform, long Structural) Count(ExpressionSimplifier simplifier, Expression e)
        => ExpressionResourceBudget.Run(NoLimits, null, null, null, (simplifier, e), static state =>
        {
            state.simplifier.Simplify(state.e);
            return (ExpressionResourceBudget.TransformVisits, ExpressionResourceBudget.StructuralVisits);
        });

    /// <summary>An extension node whose every member throws once armed.</summary>
    private sealed class ArmedExtension : Expression
    {
        /// <summary>Gets or sets whether the members throw.</summary>
        public bool Armed { get; set; }

        /// <summary>Gets the number of calls made while armed.</summary>
        public int Calls { get; private set; }

        /// <summary>Counts a call and throws when armed.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="value">The value returned while not armed.</param>
        /// <returns><paramref name="value"/>.</returns>
        private T Hit<T>(T value)
        {
            if (!Armed) return value;
            Calls++;
            throw new InvalidOperationException("A member of an extension node was called.");
        }

        /// <inheritdoc/>
        public override ExpressionType NodeType => Hit(ExpressionType.Extension);

        /// <inheritdoc/>
        public override Type Type => Hit(typeof(double));

        /// <inheritdoc/>
        public override bool CanReduce => Hit(false);

        /// <inheritdoc/>
        public override string ToString() => Hit("extension");
    }

    /// <summary>A plain multiplication, impersonated as a user-defined operator method.</summary>
    /// <param name="x">The left operand.</param>
    /// <param name="y">The right operand.</param>
    /// <returns>The product.</returns>
    public static double Times(double x, double y) => x * y;

    /// <summary>
    /// The boundary test reads the result of a refused chain with type tests only: a result holding an armed extension node, a
    /// call of an impersonating method or a product carrying an impersonating operator is returned as is, without any member of
    /// them being read, while the same result without the hostile node is factored.
    /// </summary>
    [TestMethod]
    public void BoundaryFinishing_NeverReadsHostileNodes()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a"), b = Expression.Parameter(typeof(double), "b"), c = Expression.Parameter(typeof(double), "c");
        var extension = new ArmedExtension();
        MethodInfo sin = typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!;
        var callMethod = new ImpersonatingMethod(sin);
        var operatorMethod = new ImpersonatingMethod(typeof(ExpressionSimplifierFactoringConsistencyResourceTests).GetMethod(nameof(Times))!);

        // A canonical result whose terms share a: factored when every term is factorable.
        Expression shared = Expression.Add(Expression.Multiply(a, b), Expression.Multiply(a, c));
        Expression[] hostile =
        [
            Expression.Add(shared, Expression.Multiply(a, extension)),
            Expression.Add(shared, extension),
            Expression.Add(shared, Expression.Multiply(a, Expression.Call(callMethod, c))),
            Expression.Add(shared, Expression.Multiply(a, c, operatorMethod)),
        ];
        var simplifier = new ExpressionSimplifier();
        Expression factored = ExpressionResourceBudget.Run(NoLimits, simplifier, null, null, (simplifier, shared), static state => state.simplifier.FinishRefusedChain(state.shared, state.shared));
        Assert.AreNotSame(shared, factored, "The same result without the hostile node is factored.");

        extension.Armed = true;
        callMethod.Trap.Reset();
        callMethod.Trap.Armed = true;
        operatorMethod.Trap.Reset();
        operatorMethod.Trap.Armed = true;
        try
        {
            foreach (Expression result in hostile)
            {
                Assert.AreSame(result, ExpressionResourceBudget.Run(NoLimits, simplifier, null, null, (simplifier, result), static state => state.simplifier.FinishRefusedChain(state.result, state.result)));
            }
        }
        finally
        {
            // See ExpressionFlattenResourceTests: an armed impersonating method must not stay in the framework's caches.
            callMethod.Trap.Armed = false;
            operatorMethod.Trap.Armed = false;
            extension.Armed = false;
        }

        Assert.AreEqual(0, extension.Calls);
        Assert.AreEqual(0, callMethod.Trap.ArmedCalls + callMethod.Trap.TypeReads);
        Assert.AreEqual(0, operatorMethod.Trap.ArmedCalls + operatorMethod.Trap.TypeReads);
    }

    /// <summary>Outside an operation, the boundary finishing does nothing (the factorable family is admitted inside one only).</summary>
    [TestMethod]
    public void BoundaryFinishing_OutsideAnOperation_ReturnsTheResult()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a"), b = Expression.Parameter(typeof(double), "b"), c = Expression.Parameter(typeof(double), "c");
        Expression shared = Expression.Add(Expression.Multiply(a, b), Expression.Multiply(a, c));
        Assert.AreSame(shared, new ExpressionSimplifier().FinishRefusedChain(shared, shared));
    }

    /// <summary>
    /// The state that marks the inside of a refused chain never leaks: it is cleared when a refusal of the budget leaves the
    /// chain, a nested operation (a comparison a rule makes, a public call from user code) starts as the boundary of its own
    /// chain and restores the caller's state, and the instance factors again after a refusal.
    /// </summary>
    [TestMethod]
    public void BoundaryState_NeverLeaks_ThroughRefusalsAndNestedOperations()
    {
        Expression chain = RefusedFactorableChain(16);
        (long transform, long structural) = Count(new ExpressionSimplifier(NoLimits), chain);
        var limited = new ExpressionSimplifier(NoLimits with { MaxStructuralVisits = structural - 1 });
        Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => limited.Simplify(chain)).Kind);
        Assert.IsFalse(ExpressionTransformer.WithinRefusedChain, "A refusal leaves the state cleared.");
        Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);

        string expected = ExpressionTraceFormatter.Format(new ExpressionSimplifier().Simplify(chain));
        StringAssert.StartsWith(expected, "1 + (x0 * ", "The boundary factors the chain.");

        ExpressionTransformer.WithinRefusedChain = true;
        try
        {
            Assert.AreEqual(expected, ExpressionTraceFormatter.Format(new ExpressionSimplifier().Simplify(chain)), "A nested operation is the boundary of its own chain.");
            Assert.IsTrue(ExpressionTransformer.WithinRefusedChain, "The caller's state is restored.");
        }
        finally
        {
            ExpressionTransformer.WithinRefusedChain = false;
        }

        Assert.AreEqual(transform, Count(new ExpressionSimplifier(NoLimits), chain).Transform, "The refused instance's work is unchanged afterwards.");
    }

    /// <summary>
    /// The work of a refused chain finished at its boundary is counted: each budget refuses it just above its limit with an
    /// exception of the right kind, and the structural work of the finishing (two walks and one canonicalization of the result)
    /// stays linear in the chain.
    /// </summary>
    [TestMethod]
    public void BoundaryFinishing_IsCounted_AndRefusedJustAboveEachLimit()
    {
        Expression chain = RefusedFactorableChain(32);
        (long transform, long structural) = Count(new ExpressionSimplifier(NoLimits), chain);
        Assert.IsNotNull(new ExpressionSimplifier(NoLimits with { MaxTransformVisits = transform }).Simplify(chain));
        Assert.AreEqual(ExpressionResourceLimitKind.Work, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpressionSimplifier(NoLimits with { MaxTransformVisits = transform - 1 }).Simplify(chain)).Kind);
        Assert.IsNotNull(new ExpressionSimplifier(NoLimits with { MaxStructuralVisits = structural }).Simplify(chain));
        Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpressionSimplifier(NoLimits with { MaxStructuralVisits = structural - 1 }).Simplify(chain)).Kind);
        Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);

        // The finishing alone, on the canonical result of the levels of a long chain: linear in its terms.
        long Finishing(int terms)
        {
            Expression levels = LevelResult(terms);
            return ExpressionResourceBudget.Run(NoLimits, null, null, null, levels, static result =>
            {
                long before = ExpressionResourceBudget.StructuralVisits;
                new ExpressionSimplifier(NoLimits).FinishRefusedChain(result, result);
                return ExpressionResourceBudget.StructuralVisits - before;
            });
        }

        // A deep sum is keyed recursively by the canonical order: a large stack, so that only the counters are measured.
        long small = 0, large = 0;
        RunOnThread(() =>
        {
            small = Finishing(1_000);
            large = Finishing(4_000);
        }, 64 * 1024 * 1024, Timeout);
        Assert.IsTrue(large < 5 * small, $"{small} then {large} structural visits for four times the terms");
    }

    /// <summary>
    /// Builds the canonical result the levels give a refused chain of <paramref name="terms"/> products sharing <c>x0</c>:
    /// <c>1 + (x0*x1 + (x0*x2 + ...))</c>, right-associated, iteratively.
    /// </summary>
    /// <param name="terms">The number of products.</param>
    /// <returns>The result.</returns>
    private static Expression LevelResult(int terms)
    {
        ParameterExpression x0 = Expression.Parameter(typeof(double), "x0");
        Expression chain = Expression.Multiply(x0, Expression.Parameter(typeof(double), "x" + terms));
        for (int i = terms - 1; i >= 1; i--) chain = Expression.Add(Expression.Multiply(x0, Expression.Parameter(typeof(double), "x" + i)), chain);
        return Expression.Add(Expression.Constant(1.0), chain);
    }

    /// <summary>
    /// On a 256 KiB thread and without limits, the boundary finishing never turns a refused chain the levels simplify into a stack
    /// refusal: at every size where the levels alone (the finishing switched off, the result of <c>master</c>) succeed, the
    /// finished simplification succeeds too, and where they run out of stack both are the same controlled refusal.
    /// </summary>
    [TestMethod]
    public void BoundaryFinishing_NeverRefusesWhatTheLevelsAccept_OnASmallStack()
    {
        var outcomes = new List<string>();
        RunOnThread(() =>
        {
            foreach (int terms in new[] { 64, 128, 256, 512, 1_024, 2_048, 4_096 })
            {
                Expression chain = RefusedFactorableChain(terms);
                bool levels = Succeeds(chain, finishing: false);
                bool finished = Succeeds(chain, finishing: true);
                outcomes.Add($"{terms}: levels {levels}, finished {finished}");
                Assert.IsTrue(finished || !levels, string.Join("; ", outcomes));
                Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
                Assert.IsFalse(ExpressionTransformer.WithinRefusedChain);
            }
        }, SmallStack, Timeout);
    }

    /// <summary>Simplifies <paramref name="chain"/> without limits, with or without the boundary finishing.</summary>
    /// <param name="chain">The chain.</param>
    /// <param name="finishing">Whether the boundary finishing runs.</param>
    /// <returns><see langword="true"/> on success, <see langword="false"/> on a stack refusal.</returns>
    private static bool Succeeds(Expression chain, bool finishing)
    {
        ExpressionSimplifier.RefusedChainFinishingDisabled = !finishing;
        try
        {
            new ExpressionSimplifier(NoLimits).Simplify(chain);
            return true;
        }
        catch (ExpressionResourceLimitException exception) when (exception.Kind == ExpressionResourceLimitKind.Stack)
        {
            return false;
        }
        finally
        {
            ExpressionSimplifier.RefusedChainFinishingDisabled = false;
        }
    }

    /// <summary>
    /// The state is per thread: refused chains simplified concurrently on several threads, factorable or not, give the trees of a
    /// sequential run.
    /// </summary>
    [TestMethod]
    public void BoundaryFinishing_IsIsolatedBetweenThreads()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x"), y = Expression.Parameter(typeof(double), "y"), z = Expression.Parameter(typeof(double), "z");
        Expression factorable = Expression.Subtract(Expression.Subtract(Expression.Subtract(Expression.Constant(2.0), Expression.Constant(10.0)), z), Expression.Multiply(x, z));
        Expression quotient = Expression.Add(factorable, Expression.Divide(x, y));
        Expression nested = Expression.Add(Expression.Call(typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!, factorable), quotient);
        Expression[] sources = [factorable, quotient, nested, RefusedFactorableChain(24)];
        string[] expected = sources.Select(source => ExpressionTraceFormatter.Format(new ExpressionSimplifier().Simplify(source))).ToArray();

        var simplifier = new ExpressionSimplifier();
        var failures = new List<string>();
        Parallel.For(0, 400, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
        {
            int index = i % sources.Length;
            string actual = ExpressionTraceFormatter.Format(simplifier.Simplify(sources[index]));
            if (actual != expected[index])
            {
                lock (failures) failures.Add($"{index}: {actual}");
            }
        });

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures.Take(5)));
    }
}
