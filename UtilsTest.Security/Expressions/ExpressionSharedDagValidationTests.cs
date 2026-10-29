using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;
using static UtilsTest.Security.Expressions.ExpressionResourceTestTrees;

namespace UtilsTest.Security.Expressions;

/// <summary>
/// ETC R2 follow-up: shared expression DAGs are validated in linear time. The fast unfolded walk keeps deciding inputs of
/// at most 65,536 occurrences (every default validation); a larger input under a higher limit is decided by the exact
/// measurement over distinct nodes (reference identity), never by unfolding. Depth, distinct and logical counts, refusals
/// (kind, limit, observed, saturation) and the absence of user code, stack overflow and retained references are checked.
/// </summary>
[TestClass]
public class ExpressionSharedDagValidationTests
{
    /// <summary>Upper bound of the worker threads.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <summary>The small worker stack of the stack tests.</summary>
    private const int SmallStack = 256 * 1024;

    /// <summary>Validates <paramref name="root"/> and returns the refusal, or <see langword="null"/> when accepted.</summary>
    /// <param name="root">The input.</param>
    /// <param name="maxDepth">The depth limit.</param>
    /// <param name="maxNodes">The node limit.</param>
    /// <returns>The refusal, or <see langword="null"/>.</returns>
    private static ExpressionResourceLimitException? TryValidate(Expression root, int maxDepth, long maxNodes)
    {
        try
        {
            ExpressionShapeValidator.Validate(root, maxDepth, maxNodes);
            return null;
        }
        catch (ExpressionResourceLimitException exception)
        {
            return exception;
        }
    }

    /// <summary>
    /// Asserts the exact acceptance boundaries of <paramref name="root"/>: accepted at its own depth and logical size, refused
    /// for <c>Depth</c> one level below (observed = its depth) and for <c>Nodes</c> one item below (observed = its size).
    /// </summary>
    /// <param name="root">The input.</param>
    /// <param name="depth">Its depth.</param>
    /// <param name="logical">Its logical size (below <see cref="long.MaxValue"/>).</param>
    /// <param name="label">The case name.</param>
    private static void AssertBoundaries(Expression root, int depth, long logical, string label)
    {
        Assert.IsNull(TryValidate(root, depth, logical), label + ": exact limits accepted");

        ExpressionResourceLimitException? tooDeep = TryValidate(root, depth - 1, long.MaxValue);
        Assert.IsNotNull(tooDeep, label + ": depth");
        Assert.AreEqual(ExpressionResourceLimitKind.Depth, tooDeep.Kind, label);
        Assert.AreEqual(depth - 1, tooDeep.Limit, label);

        ExpressionResourceLimitException? tooLarge = TryValidate(root, int.MaxValue, logical - 1);
        Assert.IsNotNull(tooLarge, label + ": nodes");
        Assert.AreEqual(ExpressionResourceLimitKind.Nodes, tooLarge.Kind, label);
        Assert.AreEqual(logical - 1, tooLarge.Limit, label);
        Assert.AreEqual(logical, tooLarge.Observed, label);
    }

    /// <summary>
    /// <c>e[n+1] = e[n] + e[n]</c>: n + 1 distinct nodes, depth n + 1, 2^(n+1) - 1 occurrences (saturated beyond 62 levels).
    /// <see cref="ExpressionResourceLimits.Unlimited"/> accepts it at once, the default limits refuse it (<c>Nodes</c>, with
    /// the exact observed size), a depth limit refuses it (<c>Depth</c>), and finite high limits decide it exactly through
    /// the shared measurement, saturation included. The occurrences are never unfolded.
    /// </summary>
    [TestMethod]
    public void DoublingDag_IsMeasuredAndValidatedLinearly()
    {
        foreach (int levels in new[] { 20, 40, 60, 70 })
        {
            Expression dag = SharedDag(levels);
            long logical = levels >= 62 ? long.MaxValue : (1L << (levels + 1)) - 1;
            string label = $"levels {levels}";

            var watch = Stopwatch.StartNew();
            ExpressionShape shape = ExpressionShapeValidator.Measure(dag);
            Assert.AreEqual(new ExpressionShape(levels + 1, logical, levels + 1, 0, false), shape, label);

            ExpressionShapeValidator.Validate(dag, int.MaxValue, long.MaxValue);
            ExpressionShapeValidator.Validate(dag, levels + 1, long.MaxValue);

            ExpressionResourceLimitException? byDefault = TryValidate(dag, ExpressionResourceLimits.DefaultMaxDepth, ExpressionResourceLimits.DefaultMaxNodes);
            Assert.AreEqual(ExpressionResourceLimitKind.Nodes, byDefault!.Kind, label);
            Assert.AreEqual(ExpressionResourceLimits.DefaultMaxNodes, byDefault.Limit, label);
            Assert.AreEqual(logical, byDefault.Observed, label);

            ExpressionResourceLimitException? tooDeep = TryValidate(dag, levels / 2, long.MaxValue);
            Assert.AreEqual(ExpressionResourceLimitKind.Depth, tooDeep!.Kind, label);

            // Finite limits above the fast walk's budget: decided by the shared measurement.
            ExpressionResourceLimitException? high = TryValidate(dag, 1_000, 1_000_000);
            if (levels >= 19)
            {
                Assert.AreEqual(ExpressionResourceLimitKind.Nodes, high!.Kind, label);
                Assert.AreEqual(1_000_000L, high.Limit, label);
                Assert.AreEqual(logical, high.Observed, label);
            }

            if (logical < long.MaxValue)
            {
                AssertBoundaries(dag, levels + 1, logical, label);
            }
            else
            {
                ExpressionResourceLimitException? saturated = TryValidate(dag, 1_000, long.MaxValue - 1);
                Assert.AreEqual(ExpressionResourceLimitKind.Nodes, saturated!.Kind, label);
                Assert.AreEqual(long.MaxValue - 1, saturated.Limit, label);
                Assert.AreEqual(long.MaxValue, saturated.Observed, label + ": saturated observation");
                Assert.IsNull(TryValidate(dag, 1_000, long.MaxValue), label + ": a saturated size never exceeds long.MaxValue");
            }

            watch.Stop();
            Assert.IsTrue(watch.Elapsed < TimeSpan.FromSeconds(5), $"{label}: {watch.Elapsed}");
        }
    }

    /// <summary>
    /// Shared and unshared graphs of the same logical tree validate identically: same acceptance boundaries, same refusal
    /// kinds and observed values, on both decision paths (below and above the fast walk's budget).
    /// </summary>
    [TestMethod]
    public void SharedAndUnsharedEquivalentTrees_ValidateIdentically()
    {
        Expression Unfolded(int levels) => levels == 0 ? P : Expression.Add(Unfolded(levels - 1), Unfolded(levels - 1));

        foreach (int levels in new[] { 10, 16, 17 })
        {
            Expression shared = SharedDag(levels);
            Expression tree = Unfolded(levels);
            ExpressionShape sharedShape = ExpressionShapeValidator.Measure(shared);
            ExpressionShape treeShape = ExpressionShapeValidator.Measure(tree);
            Assert.AreEqual(sharedShape.LogicalNodes, treeShape.LogicalNodes);
            Assert.AreEqual(sharedShape.Depth, treeShape.Depth);
            Assert.AreEqual(treeShape.LogicalNodes - 1, treeShape.DistinctNodes - 1 + (1L << levels) - 1, "the unfolded tree only shares its parameter leaf");

            AssertBoundaries(shared, sharedShape.Depth, sharedShape.LogicalNodes, $"shared {levels}");
            AssertBoundaries(tree, treeShape.Depth, treeShape.LogicalNodes, $"tree {levels}");
        }
    }

    /// <summary>
    /// A partially shared DAG (a chain where one level in three reuses the previous node twice) is counted exactly and
    /// validated with exact boundaries above the fast walk's budget.
    /// </summary>
    [TestMethod]
    public void PartiallySharedDag_IsCountedExactly()
    {
        Expression e = P;
        long logical = 1;
        long distinct = 1;
        for (int i = 0; i < 60; i++)
        {
            if (i % 3 == 0)
            {
                e = Expression.Add(e, e);
                logical = 2 * logical + 1;
                distinct += 1;
            }
            else
            {
                e = Expression.Add(e, Expression.Constant((double)i));
                logical = logical + 2;
                distinct += 2;
            }
        }

        Assert.AreEqual(new ExpressionShape(distinct, logical, 61, 0, false), ExpressionShapeValidator.Measure(e));
        Assert.IsTrue(logical > 65_536);
        AssertBoundaries(e, 61, logical, "partially shared");
    }

    /// <summary>
    /// A shared subtree under a block, a try expression and a switch, and a <see cref="CatchBlock"/> and a
    /// <see cref="SwitchCase"/> reused by two parents, contribute at every place they are referenced; the containers count
    /// as one item and one level each.
    /// </summary>
    [TestMethod]
    public void SharedSubtreesUnderControlFlowAndReusedContainers_ContributeAtEveryUse()
    {
        Expression e = SharedDag(30);
        long le = (1L << 31) - 1;
        int de = 31;

        CatchBlock handler = Expression.Catch(typeof(Exception), e);
        Expression firstTry = Expression.TryCatch(e, handler);
        Expression secondTry = Expression.TryCatch(e, handler);
        SwitchCase @case = Expression.SwitchCase(e, Expression.Constant(1.0));
        Expression firstSwitch = Expression.Switch(P, e, @case);
        Expression secondSwitch = Expression.Switch(P, e, @case);
        Expression root = Expression.Block(firstTry, secondTry, firstSwitch, secondSwitch, e);

        // Try: itself + body + handler (itself + body). Switch: itself + value + case (itself + test + body) + default.
        long tryLogical = 1 + le + (1 + le);
        long switchLogical = 1 + 1 + (1 + 1 + le) + le;
        long rootLogical = 1 + 2 * tryLogical + 2 * switchLogical + le;
        int rootDepth = de + 3;

        ExpressionShape shape = ExpressionShapeValidator.Measure(root);
        Assert.AreEqual(rootLogical, shape.LogicalNodes);
        Assert.AreEqual(rootDepth, shape.Depth);
        Assert.AreEqual(31L + 8, shape.DistinctNodes, "the 31 nodes of e, plus the block, two tries, one handler, two switches, one case and its test value");
        AssertBoundaries(root, rootDepth, rootLogical, "control flow");
    }

    /// <summary>
    /// A large array and a large list initializer reusing one element are counted per occurrence; the fast walk refuses them
    /// at the node limit, the shared measurement accepts them under a higher limit and refuses them with the exact size.
    /// </summary>
    [TestMethod]
    public void LargeCollections_AreCountedPerOccurrence()
    {
        Expression array = Expression.NewArrayInit(typeof(double), Enumerable.Repeat<Expression>(P, 200_000));
        Expression list = Expression.ListInit(Expression.New(typeof(List<double>)), Enumerable.Repeat<Expression>(P, 100_000));

        Assert.AreEqual(new ExpressionShape(2, 200_001, 2, 0, false), ExpressionShapeValidator.Measure(array));
        Assert.AreEqual(ExpressionResourceLimitKind.Nodes, TryValidate(array, 512, 50_000)!.Kind);
        AssertBoundaries(array, 2, 200_001, "array");

        // List initializer: itself, the creation, then one element initializer (one item, one level) and one argument per element.
        ExpressionShape listShape = ExpressionShapeValidator.Measure(list);
        Assert.AreEqual(1 + 1 + 2 * 100_000L, listShape.LogicalNodes);
        Assert.AreEqual(3, listShape.Depth);
        AssertBoundaries(list, 3, listShape.LogicalNodes, "list");
    }

    /// <summary>
    /// A graph with many distinct items and a heavily shared part (more than 1,024 distinct items, more than 4,194,304
    /// occurrences) goes through every stage and is decided by the full exact measurement, with exact boundaries.
    /// </summary>
    [TestMethod]
    public void LargeDistinctAndHeavilySharedGraph_IsDecidedByTheFullMeasurement()
    {
        Expression dag = SharedDag(30);
        Expression wide = Expression.NewArrayInit(typeof(double), Enumerable.Range(0, 2_000).Select(i => (Expression)Expression.Constant((double)i)));
        Expression root = Expression.Block(dag, wide);
        long logical = 1 + ((1L << 31) - 1) + 1 + 2_000;

        ExpressionShape shape = ExpressionShapeValidator.Measure(root);
        Assert.AreEqual(new ExpressionShape(1 + 31 + 1 + 2_000, logical, 32, 0, false), shape);

        var watch = Stopwatch.StartNew();
        AssertBoundaries(root, 32, logical, "large distinct and shared");
        watch.Stop();
        Assert.IsTrue(watch.Elapsed < TimeSpan.FromSeconds(5), watch.Elapsed.ToString());
    }

    /// <summary>An opaque node counting its getter calls.</summary>
    private sealed class CountingExtension : Expression
    {
        /// <summary>Gets the number of member calls made on this node.</summary>
        public int Calls { get; private set; }

        /// <inheritdoc/>
        public override ExpressionType NodeType { get { Calls++; return ExpressionType.Extension; } }

        /// <inheritdoc/>
        public override Type Type { get { Calls++; return typeof(double); } }

        /// <inheritdoc/>
        public override bool CanReduce { get { Calls++; return true; } }

        /// <inheritdoc/>
        public override Expression Reduce() { Calls++; throw new InvalidOperationException("Reduce must not be called."); }

        /// <inheritdoc/>
        protected override Expression VisitChildren(ExpressionVisitor visitor) { Calls++; throw new InvalidOperationException("VisitChildren must not be called."); }
    }

    /// <summary>
    /// A shared opaque node is an opaque leaf on both decision paths: no getter, <c>Reduce</c> or <c>VisitChildren</c> runs,
    /// and it is counted at every use.
    /// </summary>
    [TestMethod]
    public void OpaqueNodes_AreNeverInspected_OnBothPaths()
    {
        var opaque = new CountingExtension();
        Expression e = opaque;
        for (int i = 0; i < 25; i++) e = MakeShared(e);
        int callsAfterBuild = opaque.Calls;

        ExpressionShape shape = ExpressionShapeValidator.Measure(e);
        ExpressionShapeValidator.Validate(e, 1_000, 1L << 40);
        Assert.AreEqual(ExpressionResourceLimitKind.Nodes, TryValidate(e, 1_000, 1_000)!.Kind);
        Assert.AreEqual(ExpressionResourceLimitKind.Nodes, TryValidate(e, 1_000, 1_000_000)!.Kind);

        Assert.AreEqual(1L, shape.OpaqueNodes);
        Assert.AreEqual((1L << 26) - 1, shape.LogicalNodes);
        Assert.AreEqual(callsAfterBuild, opaque.Calls, "validation and measurement run no member of the opaque node");
    }

    /// <summary>Doubles <paramref name="e"/> through a typed-object block, which accepts any operand type without reading it.</summary>
    /// <param name="e">The shared subtree.</param>
    /// <returns><c>{ e; e }</c>.</returns>
    private static Expression MakeShared(Expression e) => Expression.Block(typeof(void), e, e);

    /// <summary>
    /// The shared measurement is iterative: a 300,000-level chain and a 60-level doubling DAG are decided on a 256 KiB
    /// thread without stack overflow, and refusals leave no operation open.
    /// </summary>
    [TestMethod]
    public void SharedMeasurement_NeedsNoCallStack()
    {
        RunOnThread(() =>
        {
            Expression chain = NegateChain(300_000);
            ExpressionShapeValidator.Validate(chain, int.MaxValue - 1, 10_000_000);
            Assert.AreEqual(ExpressionResourceLimitKind.Depth, TryValidate(chain, 299_999, 10_000_000)!.Kind);
            Assert.AreEqual(ExpressionResourceLimitKind.Nodes, TryValidate(SharedDag(60), 1_000, 1_000_000)!.Kind);

            ExpressionResourceLimitException refused = Assert.ThrowsExactly<ExpressionResourceLimitException>(
                () => new ExpressionSimplifier(ExpressionResourceLimits.Default with { MaxNodes = 1_000_000 }).Simplify(SharedDag(60)));
            Assert.AreEqual(ExpressionResourceLimitKind.Nodes, refused.Kind);
            Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
        }, SmallStack, Timeout);
    }

    /// <summary>Builds a large tree, validates it on both paths, and returns a weak reference to it.</summary>
    /// <returns>A weak reference to the validated tree.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference ValidateAndForget()
    {
        Expression tree = Expression.Add(SharedDag(40), NegateChain(100));
        ExpressionShapeValidator.Validate(tree, 1_000, long.MaxValue - 1);
        TryValidate(tree, 1_000, 50_000);
        return new WeakReference(tree);
    }

    /// <summary>No validation path keeps a reference to the validated tree once it returns.</summary>
    [TestMethod]
    public void Validation_RetainsNoReference()
    {
        WeakReference reference = ValidateAndForget();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.IsFalse(reference.IsAlive);
    }

    /// <summary>
    /// The public operations keep their behavior on a shared DAG: a simplifier with a node limit above the fast budget
    /// validates it in linear time and then runs (bounded by its visit budgets), and a nested public comparison inside a
    /// running operation still validates its own input.
    /// </summary>
    [TestMethod]
    public void PublicOperations_ValidateSharedDagsInLinearTime()
    {
        Expression dag = SharedDag(50);
        var limits = ExpressionResourceLimits.Default with { MaxNodes = long.MaxValue, MaxTransformVisits = 10_000 };

        var watch = Stopwatch.StartNew();
        try
        {
            new ExpressionSimplifier(limits).Simplify(dag);
        }
        catch (ExpressionResourceLimitException exception)
        {
            Assert.AreEqual(ExpressionResourceLimitKind.Work, exception.Kind, "the validation accepted the DAG; only a run budget may stop it");
        }

        watch.Stop();
        Assert.IsTrue(watch.Elapsed < TimeSpan.FromSeconds(5), watch.Elapsed.ToString());
        Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
        Assert.AreEqual(ExpressionResourceLimitKind.Nodes, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => ExpressionComparer.Default.Equals(dag, SharedDag(50))).Kind);
    }
}
