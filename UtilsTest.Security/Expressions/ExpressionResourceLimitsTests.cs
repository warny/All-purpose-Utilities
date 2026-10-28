using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;
using static UtilsTest.Security.Expressions.ExpressionResourceTestTrees;

namespace UtilsTest.Security.Expressions;

/// <summary>
/// ETC R1a-Security: resource-exhaustion invariants of the expression-tree engine. Inputs over the depth or node limit
/// are refused before any work; transformation and structural work stop at their budgets; nested public calls share
/// and can only tighten the running budget; every refusal is an <see cref="ExpressionResourceLimitException"/> that
/// leaves no ambient state behind.
/// </summary>
[TestClass]
public class ExpressionResourceLimitsTests
{
    /// <summary>Upper bound of the worker threads.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    // ------------------------------------------------------------------------------------------
    // Configuration.
    // ------------------------------------------------------------------------------------------

    /// <summary>The default limits are the documented ones, and <see cref="ExpressionResourceLimits.Unlimited"/> disables every bounded check.</summary>
    [TestMethod]
    public void Limits_DefaultsAndUnlimited_AreTheDocumentedValues()
    {
        ExpressionResourceLimits defaults = ExpressionResourceLimits.Default;
        Assert.AreEqual(512, defaults.MaxDepth);
        Assert.AreEqual(50_000L, defaults.MaxNodes);
        Assert.AreEqual(1_000_000L, defaults.MaxTransformVisits);
        Assert.AreEqual(10_000_000L, defaults.MaxStructuralVisits);
        Assert.AreEqual(defaults, new ExpressionResourceLimits());

        ExpressionResourceLimits unlimited = ExpressionResourceLimits.Unlimited;
        Assert.AreEqual(int.MaxValue, unlimited.MaxDepth);
        Assert.AreEqual(long.MaxValue, unlimited.MaxNodes);
        Assert.AreEqual(long.MaxValue, unlimited.MaxTransformVisits);
        Assert.AreEqual(long.MaxValue, unlimited.MaxStructuralVisits);
    }

    /// <summary>Zero and negative limits are rejected, including through a <c>with</c> expression.</summary>
    [TestMethod]
    public void Limits_ZeroOrNegative_AreRejected()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ExpressionResourceLimits { MaxDepth = 0 });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ExpressionResourceLimits { MaxNodes = -1 });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ExpressionResourceLimits.Default with { MaxTransformVisits = 0 });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ExpressionResourceLimits.Default with { MaxStructuralVisits = long.MinValue });
        Assert.ThrowsExactly<ArgumentNullException>(() => new ExpressionSimplifier(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => ExpressionComparer.Create(null!));
    }

    /// <summary>Limits belong to the instance: two simplifiers with different limits decide independently on the same input.</summary>
    [TestMethod]
    public void Limits_ArePerInstance()
    {
        Expression tree = NegateChain(40);
        var strict = new ExpressionSimplifier(Shape(maxDepth: 16));
        var relaxed = new ExpressionSimplifier();

        Assert.ThrowsExactly<ExpressionResourceLimitException>(() => strict.Simplify(tree));
        Assert.IsNotNull(relaxed.Simplify(tree));
        Assert.AreSame(ExpressionResourceLimits.Default, ExpressionComparer.Default.ResourceLimits);
        Assert.AreEqual(16, ExpressionComparer.Create(Shape(maxDepth: 16)).ResourceLimits.MaxDepth);
        Assert.AreSame(ExpressionResourceLimits.Default, new RecursiveTransformer().Limits);
    }

    // ------------------------------------------------------------------------------------------
    // Depth and node validation.
    // ------------------------------------------------------------------------------------------

    /// <summary>Every public entry point accepts a tree exactly at the depth limit and refuses one level more, with the exact observed depth.</summary>
    [TestMethod]
    public void Depth_JustBelowAndAbove_TheLimit()
    {
        ExpressionResourceLimits limits = Shape(maxDepth: 64);
        Expression atLimit = NegateChain(63);
        Expression overLimit = NegateChain(64);
        Expression other = NegateChain(63);
        var simplifier = new ExpressionSimplifier(limits);
        ExpressionComparer comparer = ExpressionComparer.Create(limits);
        var transformer = new RecursiveTransformer(limits);

        simplifier.Simplify(atLimit);
        comparer.GetHashCode(atLimit);
        Assert.IsTrue(comparer.Equals(atLimit, other));
        transformer.Transform(atLimit);
        transformer.Replace(atLimit, P, Expression.Constant(1.0));

        Action[] refused =
        [
            () => simplifier.Simplify(overLimit),
            () => comparer.GetHashCode(overLimit),
            () => comparer.Equals(overLimit, other),
            () => comparer.Equals(other, overLimit),
            () => transformer.Transform(overLimit),
            () => transformer.Replace(overLimit, P, Expression.Constant(1.0)),
        ];
        foreach (Action action in refused)
        {
            ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(action);
            Assert.AreEqual(ExpressionResourceLimitKind.Depth, exception.Kind);
            Assert.AreEqual(64L, exception.Limit);
            Assert.AreEqual(65L, exception.Observed);
        }
    }

    /// <summary>A shared DAG with few distinct nodes is measured by its logical (unfolded) size, just below and just above the limit.</summary>
    [TestMethod]
    public void Nodes_LogicalCount_OfASharedDag_IsEnforced()
    {
        ExpressionResourceLimits limits = Shape(maxNodes: 1_000);
        var transformer = new RecursiveTransformer(limits);

        // 9 additions: 10 distinct nodes, 1,023 logical nodes; 8 additions: 511.
        transformer.Transform(SharedDag(8));
        ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => transformer.Transform(SharedDag(9)));

        Assert.AreEqual(ExpressionResourceLimitKind.Nodes, exception.Kind);
        Assert.AreEqual(1_000L, exception.Limit);
        Assert.AreEqual(1_023L, exception.Observed);
    }

    /// <summary>A wide tree (one array of many elements) is bounded by the node limit, and a balanced tree below it is accepted.</summary>
    [TestMethod]
    public void Nodes_WideAndBalancedTrees()
    {
        ExpressionResourceLimits limits = Shape(maxNodes: 2_000);
        var simplifier = new ExpressionSimplifier(limits);
        Expression wide = Expression.NewArrayInit(typeof(double), System.Linq.Enumerable.Repeat<Expression>(P, 3_000));
        (Expression sum, _) = LeftSum(64);

        ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => simplifier.Simplify(wide));
        Assert.AreEqual(ExpressionResourceLimitKind.Nodes, exception.Kind);
        Assert.AreEqual(3_001L, exception.Observed);
        Assert.IsNotNull(simplifier.Simplify(sum));
    }

    // ------------------------------------------------------------------------------------------
    // Work budgets.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// The transformation budget counts one visit per node: a generic transformer over <c>n + 1</c> nodes succeeds with a
    /// budget of <c>n + 1</c> visits and stops at <c>n + 2</c> with one visit fewer.
    /// </summary>
    [TestMethod]
    public void Work_TransformBudget_JustBelowAndAbove()
    {
        Expression tree = NegateChain(99);

        new RecursiveTransformer(ExpressionResourceLimits.Default with { MaxTransformVisits = 100 }).Transform(tree);
        ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(
            () => new RecursiveTransformer(ExpressionResourceLimits.Default with { MaxTransformVisits = 99 }).Transform(tree));

        Assert.AreEqual(ExpressionResourceLimitKind.Work, exception.Kind);
        Assert.AreEqual(99L, exception.Limit);
        Assert.AreEqual(100L, exception.Observed);
    }

    /// <summary>
    /// A tiny tree that passes validation but whose simplification explodes (S5 family g, comparer re-simplifications
    /// included) is stopped by the transformation budget.
    /// </summary>
    [TestMethod]
    public void Work_CombinatorialSimplification_IsStoppedByTheBudget()
    {
        Expression tree = EqualCoefficientChain(16);
        var simplifier = new ExpressionSimplifier(ExpressionResourceLimits.Default with { MaxTransformVisits = 20_000 });

        ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => simplifier.Simplify(tree));

        Assert.AreEqual(ExpressionResourceLimitKind.Work, exception.Kind);
        Assert.AreEqual(20_000L, exception.Limit);
        Assert.AreEqual(20_001L, exception.Observed);
    }

    /// <summary>The structural budget bounds raw equality, raw hashing, canonical keys, key comparison and substitution, one visit per node.</summary>
    [TestMethod]
    public void StructuralWork_EveryStructuralWalk_JustBelowAndAbove()
    {
        Expression x = NegateChain(49);
        Expression y = NegateChain(49);
        (string Name, int Visits, Func<object> Walk)[] walks =
        [
            ("raw equality", 50, () => ExpressionComparer.StructuralEqualsRaw(x, y)),
            ("raw hash", 50, () => ExpressionComparer.StructuralHashRaw(x)),
            ("canonical key", 50, () => ExpressionCanonicalOrder.BuildKey(x, [])),
            ("canonical comparison", 150, () => ExpressionCanonicalOrder.Compare(x, y, [])),
            ("substitution", 50, () => new RecursiveTransformer().Replace(x, P, Expression.Constant(1.0))),
        ];

        foreach ((string name, int visits, Func<object> walk) in walks)
        {
            RunWithStructuralBudget(visits, walk);
            ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => RunWithStructuralBudget(visits - 1, walk), name);
            Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, exception.Kind, name);
            Assert.AreEqual(visits - 1L, exception.Limit, name);
            Assert.AreEqual((long)visits, exception.Observed, name);
        }
    }

    /// <summary>Runs <paramref name="walk"/> inside an operation whose structural budget is <paramref name="visits"/>.</summary>
    /// <param name="visits">The structural budget.</param>
    /// <param name="walk">The walk.</param>
    private static void RunWithStructuralBudget(long visits, Func<object> walk)
        => ExpressionResourceBudget.Run(ExpressionResourceLimits.Default with { MaxStructuralVisits = visits }, null, null, null, walk, static w => w());

    /// <summary>
    /// The public comparer and its hash fail with the structural budget, never with <see langword="false"/> or a fallback
    /// hash; distinct unequal trees and an unsupported family still answer <see langword="false"/>.
    /// </summary>
    [TestMethod]
    public void Comparer_RefusalIsDistinctFromInequalityAndUnsupported()
    {
        ExpressionComparer strict = ExpressionComparer.Create(ExpressionResourceLimits.Default with { MaxStructuralVisits = 10 });
        Expression x = CallChain(30);
        Expression y = CallChain(30);
        Expression conditional1 = Expression.Condition(Expression.Constant(true), P, P);
        Expression conditional2 = Expression.Condition(Expression.Constant(true), P, P);

        Assert.IsFalse(ExpressionComparer.Default.Equals(NegateChain(3), NegateChain(4)));
        Assert.IsFalse(ExpressionComparer.Default.Equals(conditional1, conditional2));
        Assert.IsTrue(ExpressionComparer.Default.Equals(x, y));
        Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => strict.Equals(x, y)).Kind);
        Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => strict.GetHashCode(x)).Kind);
    }

    /// <summary>
    /// <c>Equals(x, x)</c> on the same instance keeps its O(1) reference fast path even when <c>x</c> exceeds the limits
    /// (documented compatibility exception); two distinct instances and <c>GetHashCode</c> are still validated.
    /// </summary>
    [TestMethod]
    public void Comparer_ReferenceFastPath_DoesNotValidate_ButDistinctInstancesAndHashDo()
    {
        ExpressionComparer strict = ExpressionComparer.Create(Shape(maxDepth: 8));
        Expression deep = NegateChain(20);

        Assert.IsTrue(strict.Equals(deep, deep));
        Assert.AreEqual(ExpressionResourceLimitKind.Depth, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => strict.Equals(deep, NegateChain(20))).Kind);
        Assert.AreEqual(ExpressionResourceLimitKind.Depth, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => strict.GetHashCode(deep)).Kind);
        Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new HashSet<Expression>(strict) { deep });
    }

    // ------------------------------------------------------------------------------------------
    // Nested public calls.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// A nested public call shares the outer counters: the outer budget keeps counting through it, and a looser nested
    /// instance cannot extend it.
    /// </summary>
    [TestMethod]
    public void Nested_LooserCall_CannotExtendTheOuterBudget()
    {
        Expression tree = NegateChain(199);
        var unlimitedInner = new RecursiveTransformer(ExpressionResourceLimits.Unlimited);

        ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() =>
            ExpressionResourceBudget.Run(ExpressionResourceLimits.Default with { MaxTransformVisits = 150 }, null, null, null, 0, _ => unlimitedInner.Transform(tree)));

        Assert.AreEqual(ExpressionResourceLimitKind.Work, exception.Kind);
        Assert.AreEqual(150L, exception.Limit);
    }

    /// <summary>A stricter nested call applies its own budget (counted from its entry) and its own depth limit, then the outer limits come back.</summary>
    [TestMethod]
    public void Nested_StricterCall_TightensForItsOwnDuration()
    {
        var strictInner = new RecursiveTransformer(ExpressionResourceLimits.Default with { MaxTransformVisits = 10, MaxDepth = 8 });
        var outerTransformer = new RecursiveTransformer();
        long visitsAfterInner = -1;

        ExpressionResourceBudget.Run(ExpressionResourceLimits.Default, null, null, null, 0, _ =>
        {
            outerTransformer.Transform(NegateChain(49));
            long before = ExpressionResourceBudget.TransformVisits;
            // 13 nodes, depth 7: within the inner depth limit, over its budget of 10 visits.
            ExpressionResourceLimitException work = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => strictInner.Transform(Expression.Add(NegateChain(5), NegateChain(5))));
            Assert.AreEqual(ExpressionResourceLimitKind.Work, work.Kind);
            Assert.AreEqual(10L, work.Limit);
            Assert.AreEqual(11L, work.Observed);
            Assert.AreEqual(ExpressionResourceLimitKind.Depth, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => strictInner.Transform(NegateChain(8))).Kind);

            // The outer operation goes on with its own limits, and the inner visits stayed counted.
            outerTransformer.Transform(NegateChain(49));
            visitsAfterInner = ExpressionResourceBudget.TransformVisits;
            Assert.IsTrue(visitsAfterInner >= before + 11 + 50);
            return 0;
        });

        Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
        Assert.AreEqual(0L, ExpressionResourceBudget.TransformVisits);
    }

    /// <summary>A constant whose <see cref="object.Equals(object?)"/> calls the simplifier joins the comparison's budget.</summary>
    [TestMethod]
    public void Reentrancy_ConstantEquals_CallingTheSimplifier_IsBounded()
    {
        var heavy = new ReentrantValue(() => new ExpressionSimplifier(ExpressionResourceLimits.Unlimited).Simplify(EqualCoefficientChain(18)));
        ExpressionComparer comparer = ExpressionComparer.Create(ExpressionResourceLimits.Default with { MaxTransformVisits = 5_000 });

        ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(
            () => comparer.Equals(Expression.Constant(heavy), Expression.Constant(new ReentrantValue(() => null))));

        Assert.AreEqual(ExpressionResourceLimitKind.Work, exception.Kind);
        Assert.AreEqual(5_000L, exception.Limit);
        AssertNoAmbientState();
    }

    /// <summary>A constant whose <see cref="object.GetHashCode"/> calls the comparer joins the hash's budget.</summary>
    [TestMethod]
    public void Reentrancy_ConstantGetHashCode_CallingTheComparer_IsBounded()
    {
        var heavy = new ReentrantValue(() => ExpressionComparer.Create(ExpressionResourceLimits.Unlimited).GetHashCode(EqualCoefficientChain(18)));
        ExpressionComparer comparer = ExpressionComparer.Create(ExpressionResourceLimits.Default with { MaxTransformVisits = 5_000 });

        ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => comparer.GetHashCode(Expression.Constant(heavy)));

        Assert.AreEqual(5_000L, exception.Limit);
        AssertNoAmbientState();
    }

    /// <summary>A rule that calls the comparer, and a transformer that re-enters its own public entry point, stay inside the outer budget.</summary>
    [TestMethod]
    public void Reentrancy_RuleAndSelfReentry_AreBounded()
    {
        var reentrant = new SelfReentrantTransformer(ExpressionResourceLimits.Default with { MaxTransformVisits = 2_000 });

        ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => reentrant.Transform(NegateChain(30)));

        Assert.AreEqual(ExpressionResourceLimitKind.Work, exception.Kind);
        Assert.AreEqual(2_000L, exception.Limit);
        Assert.IsTrue(reentrant.ComparerCalls > 0);
        AssertNoAmbientState();
    }

    // ------------------------------------------------------------------------------------------
    // State restoration and isolation.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// After refusals at every phase (validation, transformation budget inside a comparison session, structural budget,
    /// stack), the thread's ambient state is clean and an ordinary simplification gives the usual result.
    /// </summary>
    [TestMethod]
    public void Failures_AtEveryPhase_LeaveNoAmbientState()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        var reference = (LambdaExpression)new ExpressionSimplifier().Simplify(Expression.Lambda(Expression.Add(Expression.Multiply(x, Expression.Constant(2.0)), Expression.Multiply(x, Expression.Constant(3.0))), x));
        Action[] failures =
        [
            () => new ExpressionSimplifier(Shape(maxDepth: 4)).Simplify(NegateChain(10)),
            () => new ExpressionSimplifier(ExpressionResourceLimits.Default with { MaxTransformVisits = 3_000 }).Simplify(Expression.Lambda(EqualCoefficientChain(14))),
            () => ExpressionComparer.Create(ExpressionResourceLimits.Default with { MaxStructuralVisits = 40 }).Equals(CallChain(60), CallChain(60)),
            () => RunOnThread(() => new ExpressionSimplifier(ExpressionResourceLimits.Unlimited).Simplify(NegateChain(200_000)), 256 * 1024, Timeout),
        ];

        foreach (Action failure in failures)
        {
            Assert.ThrowsExactly<ExpressionResourceLimitException>(failure);
            AssertNoAmbientState();
            var again = (LambdaExpression)new ExpressionSimplifier().Simplify(Expression.Lambda(Expression.Add(Expression.Multiply(x, Expression.Constant(2.0)), Expression.Multiply(x, Expression.Constant(3.0))), x));
            Assert.IsTrue(ExpressionComparer.Default.Equals(reference, again));
        }
    }

    /// <summary>Asserts that no expression operation, comparison session, lexical scope or diagnostic state survives on this thread.</summary>
    private static void AssertNoAmbientState()
    {
        Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
        Assert.AreEqual(0L, ExpressionResourceBudget.TransformVisits);
        Assert.AreEqual(0L, ExpressionResourceBudget.StructuralVisits);
        Assert.AreEqual(0, ExpressionSimplifier.ComparisonMemoDepth);
        Assert.IsFalse(ExpressionSimplifier.IsComparisonMemoActive);
        Assert.IsFalse(ExpressionSimplifier.HasComparisonMemo);
        Assert.IsFalse(ExpressionSimplifier.HasObservedComparisonMemo);
        Assert.AreEqual(0, ExpressionSimplifier.LexicalScopeDepth);
        Assert.AreEqual(0, ExpressionSimplifier.ExpressionMetricsScopeDepth);
        Assert.AreEqual(0, ExpressionComparer.ComparisonSimplificationDepth);
    }

    /// <summary>Concurrent operations with different limits on different threads never share counters or limits.</summary>
    [TestMethod]
    public void Concurrency_ThreadsKeepIndependentBudgets()
    {
        using var start = new Barrier(4);
        var strict = new RecursiveTransformer(ExpressionResourceLimits.Default with { MaxTransformVisits = 1_000 });
        var relaxed = new RecursiveTransformer();
        Expression big = NegateChain(500);

        // 1,205 nodes of depth 402: valid input, over the strict budget of 1,000 visits.
        Expression overBudget = Expression.Add(Expression.Add(NegateChain(400), NegateChain(400)), NegateChain(400));
        void Strict()
        {
            start.SignalAndWait();
            for (int i = 0; i < 50; i++)
            {
                ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => strict.Transform(overBudget));
                Assert.AreEqual(ExpressionResourceLimitKind.Work, exception.Kind);
                Assert.AreEqual(1_001L, exception.Observed);
            }
        }

        Task[] tasks =
        [
            Task.Run(Strict),
            Task.Run(Strict),
            Task.Run(() => { start.SignalAndWait(); for (int i = 0; i < 50; i++) relaxed.Transform(big); }),
            Task.Run(() => { start.SignalAndWait(); for (int i = 0; i < 50; i++) relaxed.Transform(big); }),
        ];

        Assert.IsTrue(Task.WaitAll(tasks, Timeout));
    }

    /// <summary>A value whose equality and hash run arbitrary engine calls (user code re-entering the engine).</summary>
    private sealed class ReentrantValue
    {
        /// <summary>The engine call made by <see cref="Equals(object?)"/> and <see cref="GetHashCode"/>.</summary>
        private readonly Func<object?> _call;

        /// <summary>Initializes the value.</summary>
        /// <param name="call">The engine call.</param>
        public ReentrantValue(Func<object?> call) => _call = call;

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            _call();
            return ReferenceEquals(this, obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            _call();
            return 0;
        }
    }

    /// <summary>A transformer whose rule calls the public comparer and re-enters its own public <see cref="Transform"/>.</summary>
    private sealed class SelfReentrantTransformer : ExpressionTransformer
    {
        /// <summary>Initializes the transformer.</summary>
        /// <param name="limits">Its limits.</param>
        public SelfReentrantTransformer(ExpressionResourceLimits limits)
            : base(limits)
        {
        }

        /// <summary>Gets the number of comparer calls made by the rule.</summary>
        public int ComparerCalls { get; private set; }

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <summary>Compares the operand with itself through a fresh instance and re-enters <see cref="Transform"/> on it.</summary>
        /// <param name="negate">The node.</param>
        /// <returns>The node.</returns>
        [ExpressionSignature(ExpressionType.Negate)]
        private Expression Reenter(UnaryExpression negate)
        {
            ComparerCalls++;
            ExpressionComparer.Default.Equals(negate.Operand, NegateChain(3));
            Transform(negate.Operand);
            return negate;
        }

        /// <inheritdoc/>
        protected override Expression PrepareExpression(Expression e) => TransformCore(e);

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => CopyExpression(e, parameters);
    }
}
