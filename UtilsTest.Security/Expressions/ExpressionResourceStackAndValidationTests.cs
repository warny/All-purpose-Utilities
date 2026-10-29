using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;
using static UtilsTest.Security.Expressions.ExpressionResourceTestTrees;

namespace UtilsTest.Security.Expressions;

/// <summary>
/// ETC R1a-Security: stack safety of every guarded recursive walk, correctness and boundedness of the non-recursive
/// structural validator, and the contract future R1b/R1c handlers inherit from the shared budget infrastructure.
/// </summary>
/// <remarks>
/// Deep inputs run on dedicated worker threads with explicit stacks (256 KiB, the smallest stack the R0 campaign
/// measured), with the depth and node limits disabled so the recursion itself reaches the stack guard: the guard must
/// end every walk with a controlled <see cref="ExpressionResourceLimitException"/> of kind
/// <see cref="ExpressionResourceLimitKind.Stack"/>, never with a process-terminating stack overflow.
/// </remarks>
[TestClass]
public class ExpressionResourceStackAndValidationTests
{
    /// <summary>Upper bound of the worker threads.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <summary>The small worker stack of the stack-guard tests.</summary>
    private const int SmallStack = 256 * 1024;

    /// <summary>A depth far beyond any stack measured in R0 (2,000 levels on 1 MB).</summary>
    private const int HostileDepth = 100_000;

    /// <summary>Limits disabling validation and work budgets: only the stack guard can stop the recursion.</summary>
    private static readonly ExpressionResourceLimits NoLimits = ExpressionResourceLimits.Unlimited;

    // ------------------------------------------------------------------------------------------
    // Stack guards.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Every public recursive path ends a hostile depth with a controlled Stack refusal on a 256 KiB thread, keeps the
    /// original <see cref="InsufficientExecutionStackException"/>, and leaves the thread usable.
    /// </summary>
    [TestMethod]
    public void StackGuard_EveryPublicPath_RefusesHostileDepthOnASmallStack()
    {
        Expression negate = NegateChain(HostileDepth);
        Expression calls = CallChain(HostileDepth);
        Expression calls2 = CallChain(HostileDepth);
        (string Name, Action Run)[] paths =
        [
            ("simplify", () => new ExpressionSimplifier(NoLimits).Simplify(negate)),
            ("simplify calls", () => new ExpressionSimplifier(NoLimits).Simplify(calls)),
            ("generic transform", () => new RecursiveTransformer(NoLimits).Transform(calls)),
            ("substitution", () => new RecursiveTransformer(NoLimits).Replace(calls, P, Expression.Constant(1.0))),
            ("comparer equals", () => ExpressionComparer.Create(NoLimits).Equals(calls, calls2)),
            ("comparer hash", () => ExpressionComparer.Create(NoLimits).GetHashCode(calls)),
            ("raw equality", () => RunUnlimited(() => ExpressionComparer.StructuralEqualsRaw(calls, calls2))),
            ("raw hash", () => RunUnlimited(() => ExpressionComparer.StructuralHashRaw(calls))),
            ("canonical key", () => RunUnlimited(() => ExpressionCanonicalOrder.BuildKey(calls, []))),
        ];

        foreach ((string name, Action run) in paths)
        {
            RunOnThread(() =>
            {
                ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(run, name);
                Assert.AreEqual(ExpressionResourceLimitKind.Stack, exception.Kind, name);
                Assert.IsInstanceOfType<InsufficientExecutionStackException>(exception.InnerException, name);
                Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth, name);

                // The same thread keeps working normally after the refusal.
                Assert.IsTrue(ExpressionComparer.Default.Equals(CallChain(10), CallChain(10)), name);
            }, SmallStack, Timeout);
        }
    }

    /// <summary>The recursive comparison of two canonical keys is guarded too: deep keys built on a large stack cannot overflow a small one.</summary>
    [TestMethod]
    public void StackGuard_CanonicalKeyComparison_IsGuarded()
    {
        object? left = null;
        object? right = null;
        RunOnThread(() =>
        {
            left = RunUnlimited(() => ExpressionCanonicalOrder.BuildKey(CallChain(20_000), []));
            right = RunUnlimited(() => ExpressionCanonicalOrder.BuildKey(CallChain(20_000), []));
        }, 64 * 1024 * 1024, Timeout);

        RunOnThread(() =>
        {
            var x = (IComparable<ExpressionCanonicalOrder.KeyNode>)left!;
            ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(
                () => RunUnlimited(() => x.CompareTo((ExpressionCanonicalOrder.KeyNode)right!)));
            Assert.AreEqual(ExpressionResourceLimitKind.Stack, exception.Kind);
        }, SmallStack, Timeout);
    }

    /// <summary>With the default limits, the same hostile input is refused by the validator before any recursion, on any stack.</summary>
    [TestMethod]
    public void DefaultLimits_RefuseHostileDepthBeforeRecursing()
    {
        Expression calls = CallChain(HostileDepth);
        RunOnThread(() =>
        {
            ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpressionSimplifier().Simplify(calls));
            Assert.AreEqual(ExpressionResourceLimitKind.Depth, exception.Kind);
            Assert.AreEqual(HostileDepth + 1L, exception.Observed);
        }, SmallStack, Timeout);
    }

    /// <summary>Runs <paramref name="walk"/> inside an operation without any bounded limit.</summary>
    /// <param name="walk">The walk.</param>
    /// <returns>Its result.</returns>
    private static object RunUnlimited(Func<object> walk)
        => ExpressionResourceBudget.Run(NoLimits, null, null, null, walk, static w => w());

    // ------------------------------------------------------------------------------------------
    // Non-recursive validator.
    // ------------------------------------------------------------------------------------------

    /// <summary>The exact measurement counts distinct and logical nodes and depth on a tree, a diamond, repeated children and many parents.</summary>
    [TestMethod]
    public void Measure_TreesAndDags_AreCountedExactly()
    {
        Expression tree = Expression.Add(Expression.Negate(P), Expression.Constant(1.0));
        Expression shared = Expression.Negate(P);
        Expression diamond = Expression.Add(Expression.Negate(shared), Expression.Convert(Expression.Not(Expression.Convert(shared, typeof(int))), typeof(double)));
        Expression repeated = Expression.NewArrayInit(typeof(double), shared, shared, shared, shared);
        Expression[] parents = Enumerable.Range(0, 100).Select(_ => (Expression)Expression.Negate(shared)).ToArray();
        Expression manyParents = Expression.NewArrayInit(typeof(double), parents);

        Assert.AreEqual(new ExpressionShape(4, 4, 3, 0, false), ExpressionShapeValidator.Measure(tree));
        Assert.AreEqual(new ExpressionShape(7, 9, 6, 0, false), ExpressionShapeValidator.Measure(diamond));
        Assert.AreEqual(new ExpressionShape(3, 9, 3, 0, false), ExpressionShapeValidator.Measure(repeated));
        Assert.AreEqual(new ExpressionShape(103, 301, 4, 0, false), ExpressionShapeValidator.Measure(manyParents));
    }

    /// <summary>
    /// A DAG of 65 distinct nodes whose unfolded size (2^65 - 1) overflows any counter is measured with saturation in
    /// linear time, and refused by the validator after at most the node limit of steps.
    /// </summary>
    [TestMethod]
    public void ExponentialDag_IsMeasuredWithSaturation_AndRefusedPromptly()
    {
        Expression dag = SharedDag(64);

        ExpressionShape shape = ExpressionShapeValidator.Measure(dag);
        Assert.AreEqual(65L, shape.DistinctNodes);
        Assert.AreEqual(long.MaxValue, shape.LogicalNodes);
        Assert.AreEqual(65, shape.Depth);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => ExpressionShapeValidator.Validate(dag, 512, 50_000));
        watch.Stop();
        Assert.AreEqual(ExpressionResourceLimitKind.Nodes, exception.Kind);
        Assert.AreEqual(long.MaxValue, exception.Observed);
        Assert.IsTrue(watch.Elapsed < TimeSpan.FromSeconds(5), watch.Elapsed.ToString());
    }

    /// <summary>A node with a huge number of children is refused after the node limit, not after walking all of them.</summary>
    [TestMethod]
    public void HugeFanout_IsRefusedAtTheNodeLimit()
    {
        Expression wide = Expression.NewArrayInit(typeof(double), Enumerable.Repeat<Expression>(P, 2_000_000));

        ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => ExpressionShapeValidator.Validate(wide, 512, 1_000));

        Assert.AreEqual(ExpressionResourceLimitKind.Nodes, exception.Kind);
        Assert.AreEqual(1_000L, exception.Limit);
        Assert.IsTrue(exception.Observed > 1_000L);
    }

    /// <summary>
    /// The validator walks every child container of the framework model (switch cases, catch blocks, member bindings,
    /// element initializers, block variables, lambda parameters, conversions), counting containers as one item and one
    /// level each.
    /// </summary>
    [TestMethod]
    public void Validate_WalksEveryContainer()
    {
        ParameterExpression v = Expression.Variable(typeof(double), "v");
        Expression @switch = Expression.Switch(P, Expression.Constant(0.0), Expression.SwitchCase(P, Expression.Constant(1.0), Expression.Constant(2.0)));
        Expression @try = Expression.TryCatch(P, Expression.Catch(Expression.Variable(typeof(Exception), "e"), P, Expression.Constant(true)));
        Expression block = Expression.Block([v], Expression.Assign(v, P), v);
        Expression lambda = Expression.Lambda<Func<double, double>>(P, P);
        Expression memberInit = Expression.MemberInit(
            Expression.New(typeof(Holder)),
            Expression.Bind(typeof(Holder).GetField(nameof(Holder.Value))!, P),
            Expression.MemberBind(typeof(Holder).GetField(nameof(Holder.Inner))!, Expression.Bind(typeof(Holder).GetField(nameof(Holder.Value))!, P)),
            Expression.ListBind(typeof(Holder).GetField(nameof(Holder.Items))!, Expression.ElementInit(typeof(List<double>).GetMethod("Add")!, P)));

        Assert.AreEqual(new ExpressionShape(6, 7, 3, 0, false), ExpressionShapeValidator.Measure(@switch));
        Assert.AreEqual(new ExpressionShape(5, 6, 3, 0, false), ExpressionShapeValidator.Measure(@try));
        Assert.AreEqual(new ExpressionShape(4, 6, 3, 0, false), ExpressionShapeValidator.Measure(block));
        Assert.AreEqual(new ExpressionShape(2, 3, 2, 0, false), ExpressionShapeValidator.Measure(lambda));
        Assert.AreEqual(new ExpressionShape(8, 10, 4, 0, false), ExpressionShapeValidator.Measure(memberInit));
        ExpressionShapeValidator.Validate(memberInit, 4, 10);
        Assert.AreEqual(ExpressionResourceLimitKind.Depth, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => ExpressionShapeValidator.Validate(memberInit, 3, 10)).Kind);
        Assert.AreEqual(ExpressionResourceLimitKind.Nodes, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => ExpressionShapeValidator.Validate(memberInit, 4, 9)).Kind);
    }

    /// <summary>
    /// An extension node is an opaque leaf: validation never calls its <c>Reduce</c>, <c>CanReduce</c> or
    /// <c>VisitChildren</c>, and dispatches on the runtime class, so not even its virtual getters run.
    /// </summary>
    [TestMethod]
    public void Validate_NeverInspectsExtensionNodes()
    {
        var extension = new HostileExtension();
        Expression tree = Expression.Add(P, extension);
        int readsBefore = extension.GetterReads;

        ExpressionShapeValidator.Validate(tree, 512, 50_000);
        ExpressionShapeValidator.Validate(extension, 512, 50_000);
        ExpressionShape shape = ExpressionShapeValidator.Measure(tree);

        Assert.AreEqual(1L, shape.OpaqueNodes);
        Assert.AreEqual(3L, shape.LogicalNodes);
        Assert.AreEqual(readsBefore, extension.GetterReads);
    }

    // ------------------------------------------------------------------------------------------
    // R1b/R1c future-handler contract.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// R1b contract: a new family handler is counted and stack-guarded by the shared infrastructure with no change to the
    /// session logic: its visits are budgeted and a hostile nesting ends with a controlled refusal. R1a-Security modeled the
    /// handler with a rule re-traversing array initializers through <c>TransformCore</c>; since ETC R1b the engine traverses
    /// them itself and a rule receives the already prepared elements (C-03), so the model rule would traverse every level
    /// twice. The test now exercises the real handler through a recursive transformer.
    /// </summary>
    [TestMethod]
    public void FutureTraversalHandler_IsBudgetedAndGuarded()
    {
        Expression nested = Chain(HostileDepth, static e => Expression.NewArrayInit(typeof(object), Expression.Convert(e, typeof(object))));
        Expression small = Chain(10, static e => Expression.NewArrayInit(typeof(object), Expression.Convert(e, typeof(object))));

        // 21 nodes (10 arrays, 10 conversions, p): one visit each, counted by the shared session.
        long visits = (long)RunUnlimited(() =>
        {
            Assert.IsInstanceOfType<NewArrayExpression>(new ArrayTraversingTransformer(ExpressionResourceLimits.Default).Transform(small));
            return ExpressionResourceBudget.TransformVisits;
        });
        Assert.AreEqual(21L, visits);
        ExpressionResourceLimitException work = Assert.ThrowsExactly<ExpressionResourceLimitException>(
            () => new ArrayTraversingTransformer(ExpressionResourceLimits.Default with { MaxTransformVisits = 15 }).Transform(small));
        Assert.AreEqual(ExpressionResourceLimitKind.Work, work.Kind);

        RunOnThread(() =>
        {
            ExpressionResourceLimitException stack = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ArrayTraversingTransformer(NoLimits).Transform(nested));
            Assert.AreEqual(ExpressionResourceLimitKind.Stack, stack.Kind);
        }, SmallStack, Timeout);
    }

    /// <summary>
    /// R1c contract: representative recursive equality and hash walkers for families that are opaque today (deep
    /// conditionals, invocations, nested arrays) consume the shared structural budget and stack guard: equal trees hash
    /// equally, unequal trees compare unequal, and a refusal is an exception, never <see langword="false"/> or a hash.
    /// </summary>
    [TestMethod]
    public void FutureStructuralWalkers_AreBudgetedAndGuarded()
    {
        Func<int, Expression>[] families =
        [
            depth => Chain(depth, static e => Expression.Condition(Expression.Constant(true), e, P)),
            depth => Chain(depth, static e => Expression.Invoke(Expression.Constant((Func<double, double>)Math.Abs), e)),
            depth => Chain(depth, static e => Expression.ArrayIndex(Expression.NewArrayInit(typeof(double), e), Expression.Constant(0))),
        ];

        foreach (Func<int, Expression> family in families)
        {
            Expression a = family(50);
            Expression b = family(50);
            Expression c = family(51);
            Assert.IsTrue((bool)RunUnlimited(() => FutureWalker.AreEqual(a, b)));
            Assert.IsFalse((bool)RunUnlimited(() => FutureWalker.AreEqual(a, c)));
            Assert.AreEqual(RunUnlimited(() => FutureWalker.Hash(a)), RunUnlimited(() => FutureWalker.Hash(b)));

            ExpressionResourceLimitException budget = Assert.ThrowsExactly<ExpressionResourceLimitException>(() =>
                ExpressionResourceBudget.Run(ExpressionResourceLimits.Default with { MaxStructuralVisits = 20 }, null, null, null, 0, _ => FutureWalker.Hash(a)));
            Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, budget.Kind);

            Expression deep = family(HostileDepth);
            RunOnThread(() =>
            {
                ExpressionResourceLimitException stack = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => RunUnlimited(() => FutureWalker.Hash(deep)));
                Assert.AreEqual(ExpressionResourceLimitKind.Stack, stack.Kind);
            }, SmallStack, Timeout);
        }
    }

    /// <summary>
    /// A recursive transformer relying on the engine's own traversal of array initializers (ETC R1b), with no reference
    /// to the resource session.
    /// </summary>
    private sealed class ArrayTraversingTransformer : ExpressionTransformer
    {
        /// <summary>Initializes the transformer.</summary>
        /// <param name="limits">Its limits.</param>
        public ArrayTraversingTransformer(ExpressionResourceLimits limits)
            : base(limits)
        {
        }

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <inheritdoc/>
        protected override Expression PrepareExpression(Expression e) => TransformCore(e);

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => CopyExpression(e, parameters);
    }

    /// <summary>
    /// Test-only model of R1c equality and hashing for families that are opaque today. Each recursive entry consumes one
    /// structural visit (stack check included), exactly as <c>ExpressionComparer.EqualsCore</c>/<c>Hash</c> do.
    /// </summary>
    private static class FutureWalker
    {
        /// <summary>Compares two trees structurally.</summary>
        /// <param name="x">The first tree.</param>
        /// <param name="y">The second tree.</param>
        /// <returns><see langword="true"/> when they are structurally equal.</returns>
        public static object AreEqual(Expression? x, Expression? y)
        {
            ExpressionResourceBudget.ConsumeStructuralVisit();
            if (x is null || y is null) return ReferenceEquals(x, y);
            if (x.NodeType != y.NodeType || x.Type != y.Type) return false;
            return (x, y) switch
            {
                (ConditionalExpression a, ConditionalExpression b) => (bool)AreEqual(a.Test, b.Test) && (bool)AreEqual(a.IfTrue, b.IfTrue) && (bool)AreEqual(a.IfFalse, b.IfFalse),
                (InvocationExpression a, InvocationExpression b) => (bool)AreEqual(a.Expression, b.Expression) && a.Arguments.Count == b.Arguments.Count && a.Arguments.Zip(b.Arguments).All(p => (bool)AreEqual(p.First, p.Second)),
                (NewArrayExpression a, NewArrayExpression b) => a.Expressions.Count == b.Expressions.Count && a.Expressions.Zip(b.Expressions).All(p => (bool)AreEqual(p.First, p.Second)),
                (BinaryExpression a, BinaryExpression b) => (bool)AreEqual(a.Left, b.Left) && (bool)AreEqual(a.Right, b.Right),
                (ConstantExpression a, ConstantExpression b) => Equals(a.Value, b.Value),
                (ParameterExpression a, ParameterExpression b) => ReferenceEquals(a, b),
                _ => false,
            };
        }

        /// <summary>Hashes a tree consistently with <see cref="AreEqual"/>.</summary>
        /// <param name="e">The tree.</param>
        /// <returns>The hash.</returns>
        public static object Hash(Expression? e)
        {
            ExpressionResourceBudget.ConsumeStructuralVisit();
            return e switch
            {
                null => 0,
                ConditionalExpression c => HashCode.Combine(c.NodeType, (int)Hash(c.Test), (int)Hash(c.IfTrue), (int)Hash(c.IfFalse)),
                InvocationExpression i => HashCode.Combine(i.NodeType, (int)Hash(i.Expression), i.Arguments.Aggregate(0, (h, a) => HashCode.Combine(h, (int)Hash(a)))),
                NewArrayExpression n => n.Expressions.Aggregate((int)n.NodeType, (h, a) => HashCode.Combine(h, (int)Hash(a))),
                BinaryExpression b => HashCode.Combine(b.NodeType, (int)Hash(b.Left), (int)Hash(b.Right)),
                ConstantExpression k => HashCode.Combine(k.NodeType, k.Value),
                _ => HashCode.Combine(e.NodeType, RuntimeHelpers.GetHashCode(e)),
            };
        }
    }

    /// <summary>A member-initialization target.</summary>
    private sealed class Holder
    {
        /// <summary>A scalar field.</summary>
        public double Value = 0.0;

        /// <summary>A nested object field.</summary>
        public Holder Inner = null!;

        /// <summary>A list field.</summary>
        public List<double> Items = [];
    }

    /// <summary>An extension node that counts its getter reads and whose reduction hooks throw.</summary>
    private sealed class HostileExtension : Expression
    {
        /// <summary>Gets the number of <see cref="NodeType"/> and <see cref="Type"/> reads.</summary>
        public int GetterReads { get; private set; }

        /// <inheritdoc/>
        public override ExpressionType NodeType
        {
            get
            {
                GetterReads++;
                return ExpressionType.Extension;
            }
        }

        /// <inheritdoc/>
        public override Type Type
        {
            get
            {
                GetterReads++;
                return typeof(double);
            }
        }

        /// <inheritdoc/>
        public override bool CanReduce => throw new InvalidOperationException("CanReduce must not be read.");

        /// <inheritdoc/>
        public override Expression Reduce() => throw new InvalidOperationException("Reduce must not be called.");

        /// <inheritdoc/>
        protected override Expression VisitChildren(ExpressionVisitor visitor) => throw new InvalidOperationException("VisitChildren must not be called.");
    }
}
