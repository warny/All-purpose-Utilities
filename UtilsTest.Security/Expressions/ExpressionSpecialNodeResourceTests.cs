using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;
using static UtilsTest.Security.Expressions.ExpressionResourceTestTrees;

namespace UtilsTest.Security.Expressions;

/// <summary>
/// ETC R4: dynamic operations, quoted expressions, lambdas rebuilt through <c>CopyExpression</c> and extension nodes under the
/// shared resource session. Hostile nestings end with a controlled refusal on a small stack on every walk; wide argument lists
/// and shared DAGs cannot evade the budgets; a quote that the simplifier does not enter costs nothing; no binder and no member
/// of an extension node is ever called while the engine measures, walks or refuses a tree; a refusal leaves the thread usable.
/// </summary>
[TestClass]
public class ExpressionSpecialNodeResourceTests
{
    /// <summary>Upper bound of the worker threads.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <summary>The small worker stack of the stack-guard tests.</summary>
    private const int SmallStack = 256 * 1024;

    /// <summary>A depth far beyond any stack measured in R0.</summary>
    private const int HostileDepth = 100_000;

    /// <summary>Limits disabling validation and work budgets: only the stack guard can stop the recursion.</summary>
    private static readonly ExpressionResourceLimits NoLimits = ExpressionResourceLimits.Unlimited;

    /// <summary>A binder that counts every call made into it and throws when bound.</summary>
    private sealed class TrapBinder : CallSiteBinder
    {
        /// <summary>The number of calls made into any trap binder on any thread.</summary>
        public static int Calls;

        /// <inheritdoc/>
        public override Expression Bind(object[] args, ReadOnlyCollection<ParameterExpression> parameters, LabelTarget returnLabel)
        {
            System.Threading.Interlocked.Increment(ref Calls);
            throw new InvalidOperationException("The engine must never bind.");
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            System.Threading.Interlocked.Increment(ref Calls);
            return ReferenceEquals(this, obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            System.Threading.Interlocked.Increment(ref Calls);
            return 0;
        }
    }

    /// <summary>
    /// An extension node whose every member throws once <see cref="Armed"/> (the engine must never call one). Its getters answer
    /// while it is not armed, so the framework factories can build a tree around it first.
    /// </summary>
    private sealed class TrapExtension : Expression
    {
        /// <summary>The number of calls made into an armed trap extension on any thread.</summary>
        public static int Calls;

        /// <summary>Gets or sets whether the members of this node throw.</summary>
        public bool Armed { get; set; }

        /// <summary>Records a call and returns the exception to throw.</summary>
        /// <returns>The exception.</returns>
        private static Exception Trap()
        {
            System.Threading.Interlocked.Increment(ref Calls);
            return new InvalidOperationException("The engine must never call into an extension node.");
        }

        /// <inheritdoc/>
        public override ExpressionType NodeType => Armed ? throw Trap() : ExpressionType.Extension;

        /// <inheritdoc/>
        public override Type Type => Armed ? throw Trap() : typeof(object);

        /// <inheritdoc/>
        public override bool CanReduce => throw Trap();

        /// <inheritdoc/>
        public override Expression Reduce() => throw Trap();

        /// <inheritdoc/>
        protected override Expression VisitChildren(ExpressionVisitor visitor) => throw Trap();

        /// <inheritdoc/>
        protected override Expression Accept(ExpressionVisitor visitor) => throw Trap();

        /// <inheritdoc/>
        public override bool Equals(object? obj) => throw Trap();

        /// <inheritdoc/>
        public override int GetHashCode() => throw Trap();

        /// <inheritdoc/>
        public override string ToString() => throw Trap();
    }

    /// <summary>A transformer exposing the protected static <c>CopyExpression</c>.</summary>
    private sealed class CopyingTransformer : ExpressionTransformer
    {
        /// <summary>Calls the protected static <c>CopyExpression</c>.</summary>
        /// <param name="e">The node.</param>
        /// <param name="parameters">The children.</param>
        /// <returns>The copy.</returns>
        public static Expression Copy(Expression e, params Expression[] parameters) => CopyExpression(e, parameters);

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);
    }

    /// <summary>Wraps a double expression in a dynamic operation returning <see cref="double"/>.</summary>
    /// <param name="e">The wrapped expression.</param>
    /// <returns>The wrapper.</returns>
    private static Expression DynamicLevel(Expression e) => Expression.Dynamic(SharedBinder, typeof(double), e, P);

    /// <summary>The binder shared by every level of the dynamic nestings, so two independently built nestings are structurally equal.</summary>
    private static readonly TrapBinder SharedBinder = new();

    /// <summary>Wraps a double expression in a lambda over it, invoked: <c>(y =&gt; e + y)(p)</c>.</summary>
    /// <param name="e">The wrapped expression.</param>
    /// <returns>The wrapper.</returns>
    private static Expression LambdaLevel(Expression e)
    {
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        return Expression.Invoke(Expression.Lambda<Func<double, double>>(Expression.Add(e, y), y), P);
    }

    /// <summary>
    /// Builds <paramref name="depth"/> nested quotes, <c>Quote(() =&gt; (object)Quote(() =&gt; ... (object)p))</c>. Every level
    /// boxes the inner quote, so every lambda has the delegate type <see cref="Func{TResult}"/> of <see cref="object"/>: nesting
    /// the quoted types themselves (<c>Func&lt;Expression&lt;Func&lt;…&gt;&gt;&gt;</c>) overflows the runtime's type loader long before
    /// a hostile depth.
    /// </summary>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The outermost quote.</returns>
    private static Expression QuoteChain(int depth)
    {
        Expression current = P;
        for (int i = 0; i < depth; i++)
        {
            current = Expression.Quote(Expression.Lambda<Func<object>>(Expression.Convert(current, typeof(object))));
        }

        return current;
    }

    /// <summary>Runs <paramref name="walk"/> inside an operation without any bounded limit.</summary>
    /// <param name="walk">The walk.</param>
    /// <returns>Its result.</returns>
    private static object RunUnlimited(Func<object> walk)
        => ExpressionResourceBudget.Run(NoLimits, null, null, null, walk, static w => w());

    /// <summary>
    /// Every walk ends a hostile nesting of dynamic operations, invoked lambdas and quotes (simplified on opt-in) with a
    /// controlled Stack refusal on a 256 KiB thread; the session and the S4 scope stack are restored, the thread stays usable,
    /// and no binder is called.
    /// </summary>
    [TestMethod]
    public void HostileNesting_OfEveryR4Family_IsRefusedOnASmallStack()
    {
        (string Name, Func<int, Expression> Build)[] nestings =
        [
            ("dynamic", depth => Chain(depth, DynamicLevel)),
            ("lambda", depth => Chain(depth, LambdaLevel)),
            ("quote", QuoteChain),
        ];
        int bindsBefore = TrapBinder.Calls;

        foreach ((string family, Func<int, Expression> build) in nestings)
        {
            Expression deep = build(HostileDepth);
            Expression deep2 = build(HostileDepth);
            ParameterExpression v = Expression.Parameter(typeof(double), "v");
            Expression binder = Expression.Lambda(Expression.Block(deep, v), v);
            (string Name, Action Run)[] paths =
            [
                ("simplify (quotes simplified)", () => new ExpressionSimplifier(NoLimits) { SimplifyQuotedExpressions = true }.Simplify(deep)),
                ("generic transform", () => new RecursiveTransformer(NoLimits).Transform(deep)),
                ("substitution", () => new RecursiveTransformer(NoLimits).Replace(deep, P, Expression.Constant(1.0))),
                ("capture check", () => new RecursiveTransformer(NoLimits).Replace(binder, P, v)),
                ("comparer equals", () => ExpressionComparer.Create(NoLimits).Equals(deep, deep2)),
                ("comparer hash", () => ExpressionComparer.Create(NoLimits).GetHashCode(deep)),
                ("raw equality", () => RunUnlimited(() => ExpressionComparer.StructuralEqualsRaw(deep, deep2))),
                ("raw hash", () => RunUnlimited(() => ExpressionComparer.StructuralHashRaw(deep))),
                ("canonical key", () => RunUnlimited(() => ExpressionCanonicalOrder.BuildKey(deep, []))),
            ];

            foreach ((string name, Action run) in paths)
            {
                // Substitution and its capture check never enter a quote: nothing deep to refuse there.
                if (family == "quote" && name is "substitution" or "capture check") continue;
                string label = family + " / " + name;
                RunOnThread(() =>
                {
                    ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(run, label);
                    Assert.AreEqual(ExpressionResourceLimitKind.Stack, exception.Kind, label);
                    Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth, label);
                    Assert.AreEqual(0, ExpressionSimplifier.LexicalScopeDepth, label);
                    Assert.IsTrue(ExpressionComparer.Default.Equals(CallChain(10), CallChain(10)), label);
                }, SmallStack, Timeout);
            }
        }

        Assert.AreEqual(bindsBefore, TrapBinder.Calls, "No binder may be called.");
    }

    /// <summary>
    /// By default a quote is a boundary of the simplifier: a hostile quote nesting costs one visit and is returned as is, even
    /// on a small stack and without limits; substitution does not enter it either. With the default limits, every R4 nesting is
    /// refused by the validator before any recursion.
    /// </summary>
    [TestMethod]
    public void DefaultQuoteBoundary_AndDefaultLimits_StopHostileNestings()
    {
        Expression quotes = QuoteChain(HostileDepth);
        RunOnThread(() =>
        {
            Assert.AreSame(quotes, new ExpressionSimplifier(NoLimits).Simplify(quotes));
            Assert.AreSame(quotes, new RecursiveTransformer(NoLimits).Replace(quotes, P, Expression.Constant(1.0)));
        }, SmallStack, Timeout);

        foreach (Expression deep in new[] { Chain(HostileDepth, DynamicLevel), Chain(HostileDepth, LambdaLevel), quotes })
        {
            RunOnThread(() =>
            {
                ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpressionSimplifier().Simplify(deep));
                Assert.AreEqual(ExpressionResourceLimitKind.Depth, exception.Kind);
            }, SmallStack, Timeout);
        }
    }

    /// <summary>
    /// <c>CopyExpression</c> of a lambda renamed over a hostile body is bounded: it joins the running operation, or runs inside
    /// an internal one, and ends with a controlled refusal on a small stack; a moderate depth is renamed faithfully.
    /// </summary>
    [TestMethod]
    public void DirectLambdaCopy_IsResourceBounded()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        LambdaExpression deep = Expression.Lambda<Func<double, double>>(Chain(HostileDepth, e => Expression.Add(e, x)), x);
        LambdaExpression moderate = Expression.Lambda<Func<double, double>>(Chain(50, e => Expression.Add(e, x)), x);

        RunOnThread(() =>
        {
            Assert.ThrowsExactly<ExpressionResourceLimitException>(() => CopyingTransformer.Copy(deep, y));
            Assert.ThrowsExactly<ExpressionResourceLimitException>(() => ExpressionResourceBudget.Run(NoLimits, null, null, null, deep, d => CopyingTransformer.Copy(d, y)));
            Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);

            var renamed = (LambdaExpression)CopyingTransformer.Copy(moderate, y);
            Assert.AreSame(y, renamed.Parameters[0]);
        }, SmallStack, Timeout);
    }

    /// <summary>
    /// Wide dynamic argument lists are counted by the budgets: equal trees compare equal, hash equally and tie, and budgets
    /// smaller than the width refuse every walk, never returning a partial result.
    /// </summary>
    [TestMethod]
    public void WideDynamicArgumentLists_AreCountedByTheWorkBudget()
    {
        var binder = new TrapBinder();
        Expression Build() => Expression.Dynamic(binder, typeof(object), Enumerable.Range(0, 5000).Select(_ => (Expression)P));
        Expression a = Build();
        Expression b = Build();

        Assert.IsTrue((bool)RunUnlimited(() => ExpressionComparer.StructuralEqualsRaw(a, b)));
        Assert.AreEqual(RunUnlimited(() => ExpressionComparer.StructuralHashRaw(a)), RunUnlimited(() => ExpressionComparer.StructuralHashRaw(b)));

        var tight = ExpressionResourceLimits.Default with { MaxStructuralVisits = 2000 };
        (string Walk, Func<object> Run)[] walks =
        [
            ("raw equality", () => ExpressionComparer.StructuralEqualsRaw(a, b)),
            ("raw hash", () => ExpressionComparer.StructuralHashRaw(a)),
            ("canonical key", () => ExpressionCanonicalOrder.BuildKey(a, [])),
            ("substitution", () => new RecursiveTransformer(tight).Replace(a, P, Expression.Constant(1.0))),
        ];
        foreach ((string walk, Func<object> run) in walks)
        {
            ExpressionResourceLimitException refusal = Assert.ThrowsExactly<ExpressionResourceLimitException>(
                () => ExpressionResourceBudget.Run(tight, null, null, null, run, static w => w()), walk);
            Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, refusal.Kind, walk);
        }

        ExpressionResourceLimitException transform = Assert.ThrowsExactly<ExpressionResourceLimitException>(
            () => new RecursiveTransformer(ExpressionResourceLimits.Default with { MaxTransformVisits = 2000 }).Transform(a));
        Assert.AreEqual(ExpressionResourceLimitKind.Work, transform.Kind);
        Assert.IsInstanceOfType<DynamicExpression>(new ExpressionSimplifier().Simplify(a));
    }

    /// <summary>
    /// A shared DAG in dynamic arguments (each level passes the previous one twice: 2^40 paths) is validated without unfolding
    /// and refused by the work budget under unlimited shape limits, by the node limit by default, within the timeout.
    /// </summary>
    [TestMethod]
    public void SharedDagInDynamicArguments_IsBounded()
    {
        var binder = new TrapBinder();
        Expression dag = P;
        for (int i = 0; i < 40; i++) dag = Expression.Dynamic(binder, typeof(double), dag, dag);

        RunOnThread(() =>
        {
            Assert.AreEqual(ExpressionResourceLimitKind.Nodes, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpressionSimplifier().Simplify(dag)).Kind);
            Assert.AreEqual(ExpressionResourceLimitKind.Work, Assert.ThrowsExactly<ExpressionResourceLimitException>(
                () => new ExpressionSimplifier(ExpressionResourceLimits.Default with { MaxNodes = long.MaxValue, MaxTransformVisits = 50_000 }).Simplify(dag)).Kind);
        }, 1024 * 1024, Timeout);
    }

    /// <summary>
    /// Extension nodes whose every member throws are measured, validated, refused and passed through without any call into
    /// them: at the root, at the bottom of a hostile nesting (refused on a small stack or by the validator), inside dynamic
    /// arguments and quotes, through comparisons and hashing, repeatedly and concurrently. A refusal is followed by a success.
    /// (A supported parent rebuilt by a framework factory reads its operands' types, as when it was built: such shapes are
    /// covered by the unit tests, which tell framework reads from engine reads.)
    /// </summary>
    [TestMethod]
    public void TrapExtensions_AreNeverCalled_EvenWhileRefusing()
    {
        int before = TrapExtension.Calls;
        var root = new TrapExtension();
        Expression deep = Chain(HostileDepth, e => Expression.Dynamic(new TrapBinder(), typeof(object), e));
        deep = Expression.Dynamic(new TrapBinder(), typeof(object), root, deep);
        Expression quoted = Expression.Quote(Expression.Lambda<Func<object>>(root));
        root.Armed = true;

        RunOnThread(() =>
        {
            Assert.AreSame(root, new ExpressionSimplifier().Simplify(root));
            Assert.AreSame(root, new ExpressionSimplifier(NoLimits).Simplify(root));
            Assert.IsTrue(ExpressionComparer.Default.Equals(root, root));
            Assert.IsFalse(ExpressionComparer.Default.Equals(root, new TrapExtension { Armed = true }));
            ExpressionComparer.Default.GetHashCode(root);
            Assert.AreEqual(ExpressionResourceLimitKind.Depth, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpressionSimplifier().Simplify(deep)).Kind);
            Assert.AreEqual(ExpressionResourceLimitKind.Stack, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpressionSimplifier(NoLimits).Simplify(deep)).Kind);
            Assert.AreEqual(ExpressionResourceLimitKind.Stack, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => ExpressionComparer.Create(NoLimits).GetHashCode(deep)).Kind);
            Assert.AreSame(root, new ExpressionSimplifier().Simplify(root), "A refusal leaves the thread usable.");
        }, SmallStack, Timeout);

        Parallel.For(0, 64, i =>
        {
            Assert.AreSame(root, new ExpressionSimplifier().Simplify(root));
            Assert.AreSame(quoted, new ExpressionSimplifier().Simplify(quoted));
            Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
        });

        Assert.AreEqual(before, TrapExtension.Calls, "No member of an extension node may be called.");
    }
}
