using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;
using static UtilsTest.Security.Expressions.ExpressionResourceTestTrees;

namespace UtilsTest.Security.Expressions;

/// <summary>
/// Security invariants of the grouped quotient chains of <see cref="ExpressionSimplifier"/>
/// (<c>docs/expressions/SimplifierFlatten.md</c> §16): the admission never reads an extension node nor a custom reflection
/// object, the work of an admitted chain is counted and refused just above each limit, a chain of any depth is simplified on a
/// 256 KiB stack while a refused deep one keeps the controlled stack refusal, a shared DAG is refused in a number of visits
/// linear in its distinct nodes, and the refusals of the family are recorded once and isolated between operations.
/// </summary>
[TestClass]
public class ExpressionSimplifierGroupedQuotientSecurityTests
{
    /// <summary>The stack size of the worker threads.</summary>
    private const int SmallStack = 256 * 1024;

    /// <summary>The maximum duration of a worker thread.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(120);

    /// <summary>Limits without any bound.</summary>
    private static readonly ExpressionResourceLimits NoLimits = ExpressionResourceLimits.Unlimited;

    /// <summary>Builds <c>1 - (x1/x0 - (x2/x0 - (… - last)))</c> iteratively, with <paramref name="n"/> quotients before <paramref name="last"/>.</summary>
    /// <param name="n">The number of quotients.</param>
    /// <param name="last">The deepest term, or <see langword="null"/> for one more quotient.</param>
    /// <returns>The chain.</returns>
    private static Expression RightGroup(int n, Expression? last = null)
    {
        ParameterExpression x0 = Expression.Parameter(typeof(double), "x0");
        Expression chain = last ?? Expression.Divide(Expression.Parameter(typeof(double), "x" + (n + 1)), x0);
        for (int i = n; i >= 1; i--) chain = Expression.Subtract(Expression.Divide(Expression.Parameter(typeof(double), "x" + i), x0), chain);
        return Expression.Subtract(Expression.Constant(1.0), chain);
    }

    /// <summary>Counts the terms of a right-associated canonical chain, iteratively.</summary>
    /// <param name="chain">The result of the simplification.</param>
    /// <returns>The number of groups of the chain.</returns>
    private static int CountGroups(Expression chain)
    {
        int count = 1;
        while (chain is BinaryExpression { NodeType: ExpressionType.Add } add)
        {
            count++;
            chain = add.Right;
        }

        return count;
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

    /// <summary>Asks the admission of <paramref name="chain"/> inside an operation, as the engine does.</summary>
    /// <param name="simplifier">The simplifier.</param>
    /// <param name="chain">The root.</param>
    /// <returns>The decision.</returns>
    private static ExpressionRuleRejection Admission(ExpressionSimplifier simplifier, Expression chain)
        => ExpressionResourceBudget.Run(NoLimits, simplifier, null, null, (simplifier, chain), static state => state.simplifier.FlattenAdmission(state.chain));

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

    /// <summary>A plain division, impersonated as a user-defined operator method.</summary>
    /// <param name="x">The dividend.</param>
    /// <param name="y">The divisor.</param>
    /// <returns>The quotient.</returns>
    public static double Over(double x, double y) => x / y;

    /// <summary>A plain subtraction, impersonated as a user-defined operator method.</summary>
    /// <param name="x">The left operand.</param>
    /// <param name="y">The right operand.</param>
    /// <returns>The difference.</returns>
    public static double Minus(double x, double y) => x - y;

    /// <summary>
    /// The grouped-quotient test runs without the effect gate, so it must refuse by itself, with type tests only: an armed
    /// extension node as a term, as a numerator or as a denominator is never read, and a quotient or a crossed subtraction
    /// carrying an impersonating method is never crossed nor inspected. Each chain is refused; the same chain without the
    /// hostile node is admitted.
    /// </summary>
    [TestMethod]
    public void Admission_NeverReadsExtensionNodes_NorImpersonatingReflectionObjects()
    {
        ParameterExpression x0 = Expression.Parameter(typeof(double), "x0"), x1 = Expression.Parameter(typeof(double), "x1");
        ParameterExpression x2 = Expression.Parameter(typeof(double), "x2"), x3 = Expression.Parameter(typeof(double), "x3");
        var extension = new ArmedExtension();
        var divide = new ImpersonatingMethod(typeof(ExpressionSimplifierGroupedQuotientSecurityTests).GetMethod(nameof(Over))!);
        var subtract = new ImpersonatingMethod(typeof(ExpressionSimplifierGroupedQuotientSecurityTests).GetMethod(nameof(Minus))!);
        Expression one = Expression.Constant(1.0);
        Expression q1 = Expression.Divide(x1, x0), q2 = Expression.Divide(x2, x0);
        Expression admitted = Expression.Subtract(one, Expression.Subtract(q1, Expression.Subtract(q2, Expression.Divide(x3, x0))));
        Expression[] hostile =
        [
            Expression.Subtract(one, Expression.Subtract(q1, Expression.Subtract(q2, extension))),
            Expression.Subtract(one, Expression.Subtract(q1, Expression.Add(extension, q2))),
            Expression.Subtract(one, Expression.Subtract(q1, Expression.Subtract(q2, Expression.Divide(x3, extension)))),
            Expression.Subtract(one, Expression.Subtract(q1, Expression.Subtract(q2, Expression.Divide(extension, x0)))),
            Expression.Subtract(one, Expression.Subtract(q1, Expression.Subtract(q2, Expression.Divide(x3, x0, divide)))),
            Expression.Subtract(one, Expression.Subtract(q1, Expression.Subtract(q2, Expression.Divide(x3, x0), subtract))),
        ];
        var simplifier = new ExpressionSimplifier();
        Assert.AreEqual(ExpressionRuleRejection.None, Admission(simplifier, admitted));

        extension.Armed = true;
        divide.Trap.Reset();
        divide.Trap.Armed = true;
        subtract.Trap.Reset();
        subtract.Trap.Armed = true;
        try
        {
            foreach (Expression chain in hostile)
            {
                Assert.AreEqual(ExpressionRuleRejection.ChainNotAdmitted, Admission(simplifier, chain));
            }
        }
        finally
        {
            // An armed impersonating method must not stay in the framework's caches (see ExpressionFlattenResourceTests).
            divide.Trap.Armed = false;
            subtract.Trap.Armed = false;
            extension.Armed = false;
        }

        Assert.AreEqual(0, extension.Calls);
        Assert.AreEqual(0, divide.Trap.ArmedCalls + divide.Trap.TypeReads);
        Assert.AreEqual(0, subtract.Trap.ArmedCalls + subtract.Trap.TypeReads);
    }

    /// <summary>
    /// The work of an admitted grouped chain is counted, and each budget refuses it just above its limit with an exception of
    /// the right kind, leaves no operation open, and the same instance works again with the same result.
    /// </summary>
    [TestMethod]
    public void AdmittedChain_IsCounted_AndRefusedJustAboveEachLimit()
    {
        Expression chain = RightGroup(63);
        (long transform, long structural) = Count(new ExpressionSimplifier(NoLimits), chain);
        Assert.AreEqual(3 * 64 + 2, transform);
        Expression expected = new ExpressionSimplifier(NoLimits).Simplify(chain);

        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(expected, new ExpressionSimplifier(NoLimits with { MaxTransformVisits = transform }).Simplify(chain)));
        var workLimited = new ExpressionSimplifier(NoLimits with { MaxTransformVisits = transform - 1 });
        ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => workLimited.Simplify(chain));
        Assert.AreEqual(ExpressionResourceLimitKind.Work, exception.Kind);
        Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
        Assert.AreEqual(5, CountGroups(workLimited.Simplify(RightGroup(3))), "The refused instance works again.");

        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(expected, new ExpressionSimplifier(NoLimits with { MaxStructuralVisits = structural }).Simplify(chain)));
        var structureLimited = new ExpressionSimplifier(NoLimits with { MaxStructuralVisits = structural - 1 });
        exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => structureLimited.Simplify(chain));
        Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, exception.Kind);
        Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
        Assert.AreEqual(5, CountGroups(structureLimited.Simplify(RightGroup(3))), "The refused instance works again.");
    }

    /// <summary>
    /// On a 256 KiB thread and without limits, an admitted grouped chain of 20,000 quotients is simplified: the admission walk,
    /// the extraction and the canonicalization never recurse on the chain. The same chain ending with a call term is refused and
    /// keeps the level by level treatment, whose depth ends in the controlled stack refusal, never a crash; the instance works
    /// again.
    /// </summary>
    [TestMethod]
    public void DeepChains_OnASmallStack()
    {
        const int Terms = 20_000;
        Expression admitted = RightGroup(Terms);
        Expression refused = RightGroup(Terms, Expression.Call(OpaqueMethod, P));
        RunOnThread(() =>
        {
            var simplifier = new ExpressionSimplifier(NoLimits);
            Assert.AreEqual(Terms + 2, CountGroups(simplifier.Simplify(admitted)));
            Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);

            ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => simplifier.Simplify(refused));
            Assert.AreEqual(ExpressionResourceLimitKind.Stack, exception.Kind);
            Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
            Assert.AreEqual(Terms + 2, CountGroups(simplifier.Simplify(admitted)), "The instance works again.");
        }, SmallStack, Timeout);
    }

    /// <summary>
    /// A grouped chain over a shared DAG (every level subtracts a node from itself: <c>2^n</c> logical terms) is refused by the
    /// admission at the second occurrence of a numerator, in a number of visits linear in the distinct nodes, and is never
    /// extracted.
    /// </summary>
    [TestMethod]
    public void SharedDag_IsRefusedInLinearWork()
    {
        ParameterExpression x0 = Expression.Parameter(typeof(double), "x0");
        Expression dag = Expression.Subtract(Expression.Divide(Expression.Parameter(typeof(double), "x1"), x0), Expression.Divide(Expression.Parameter(typeof(double), "x2"), x0));
        for (int i = 0; i < 50; i++) dag = Expression.Subtract(dag, dag);
        Expression chain = Expression.Subtract(Expression.Constant(1.0), dag);
        var simplifier = new ExpressionSimplifier(NoLimits);
        long structural = ExpressionResourceBudget.Run(NoLimits, simplifier, null, null, (simplifier, chain), static state =>
        {
            Assert.AreEqual(ExpressionRuleRejection.ChainNotAdmitted, state.simplifier.FlattenAdmission(state.chain));
            return ExpressionResourceBudget.StructuralVisits;
        });
        Assert.IsTrue(structural < 4 * 52, $"{structural} structural visits for 52 distinct crossed nodes");

        var limited = new ExpressionSimplifier(NoLimits with { MaxTransformVisits = 10_000 });
        Assert.AreEqual(ExpressionResourceLimitKind.Work, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => limited.Simplify(chain)).Kind);
        Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
    }

    /// <summary>
    /// A refused grouped chain records the spine nodes that contain its cause once: every crossed node below it is then refused
    /// by one lookup instead of another walk (one walk of the family for the whole chain), the table belongs to the operation,
    /// and the next operation, on this thread or another, starts without it and gives the same result.
    /// </summary>
    [TestMethod]
    public void KnownGroupedRefusals_AreRecordedOnce_AndIsolated()
    {
        Expression refused = RightGroup(200, Expression.Call(OpaqueMethod, P));
        var statistics = new FlattenAdmissionStatistics();
        ExpressionSimplifier.AdmissionStatistics = statistics;
        Expression first;
        try
        {
            first = new ExpressionSimplifier(NoLimits).Simplify(refused);
        }
        finally
        {
            ExpressionSimplifier.AdmissionStatistics = null;
        }

        Assert.AreEqual(1, statistics.GroupedWalks, "one walk of the family");
        Assert.AreEqual(0, statistics.GroupedQuotient);
        Assert.IsTrue(statistics.KnownGroupedRefusals >= 199, statistics.KnownGroupedRefusals.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.AreEqual(1, statistics.GroupedCauses[(int)FlattenRefusalCause.CallTerm]);

        Expression? other = null;
        RunOnThread(() => other = new ExpressionSimplifier(NoLimits).Simplify(refused), SmallStack * 16, Timeout);
        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(first, other));
        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(first, new ExpressionSimplifier(NoLimits).Simplify(refused)));
    }
}
