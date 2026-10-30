using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;
using static UtilsTest.Security.Expressions.ExpressionResourceTestTrees;

namespace UtilsTest.Security.Expressions;

/// <summary>
/// ETC R3: the initializer and index families (MemberInit with its member, nested member and list bindings, ListInit with its
/// initializers, Index) under the shared resource session. Hostile nestings, including bindings nested without any expression
/// node in between, end with a controlled refusal on a small stack on every walk (traversal, substitution and its capture
/// check, inlining analysis, comparison, hashing, canonical keys); wide binding, initializer and argument lists cannot evade
/// the cumulative work budget; shared DAGs in initializer arguments are bounded and validated without unfolding; a refusal is
/// never turned into a result; a re-entrant comparison only tightens the limits; no user code runs and no scope stays open.
/// </summary>
[TestClass]
public class ExpressionInitializerResourceTests
{
    /// <summary>Upper bound of the worker threads.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <summary>The small worker stack of the stack-guard tests.</summary>
    private const int SmallStack = 256 * 1024;

    /// <summary>A depth far beyond any stack measured in R0.</summary>
    private const int HostileDepth = 100_000;

    /// <summary>Limits disabling validation and work budgets: only the stack guard can stop the recursion.</summary>
    private static readonly ExpressionResourceLimits NoLimits = ExpressionResourceLimits.Unlimited;

    /// <summary><see cref="StrongBox{T}.Value"/>.</summary>
    private static readonly FieldInfo BoxValue = typeof(StrongBox<double>).GetField(nameof(StrongBox<double>.Value))!;

    /// <summary>The indexer of <see cref="List{T}"/>.</summary>
    private static readonly PropertyInfo ListItem = typeof(List<double>).GetProperty("Item")!;

    /// <summary>A node of a linked structure, initialized through nested member bindings.</summary>
    public sealed class Node
    {
        /// <summary>The next node, created eagerly up to the nesting the tests need (lazily on read).</summary>
        private Node? _next;

        /// <summary>Gets the next node.</summary>
        public Node Next => _next ??= new Node();

        /// <summary>A value.</summary>
        public double Value;

        /// <summary>A list.</summary>
        public readonly List<double> Items = [];
    }

    /// <summary>A collection with a sixteen-parameter <c>Add</c>, for wide element initializers.</summary>
    public sealed class WideList : IEnumerable<double>
    {
        /// <summary>The sum of the added values.</summary>
        public double Sum;

        /// <summary>Adds sixteen values.</summary>
        /// <param name="a0">Value 0.</param><param name="a1">Value 1.</param><param name="a2">Value 2.</param><param name="a3">Value 3.</param>
        /// <param name="a4">Value 4.</param><param name="a5">Value 5.</param><param name="a6">Value 6.</param><param name="a7">Value 7.</param>
        /// <param name="a8">Value 8.</param><param name="a9">Value 9.</param><param name="a10">Value 10.</param><param name="a11">Value 11.</param>
        /// <param name="a12">Value 12.</param><param name="a13">Value 13.</param><param name="a14">Value 14.</param><param name="a15">Value 15.</param>
        public void Add(double a0, double a1, double a2, double a3, double a4, double a5, double a6, double a7, double a8, double a9, double a10, double a11, double a12, double a13, double a14, double a15)
            => Sum += a0 + a1 + a2 + a3 + a4 + a5 + a6 + a7 + a8 + a9 + a10 + a11 + a12 + a13 + a14 + a15;

        /// <inheritdoc/>
        public IEnumerator<double> GetEnumerator() => Enumerable.Empty<double>().GetEnumerator();

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// A class counting every user call (constructor, setter, getter, <c>Add</c>, indexer accessor) on the current thread, so
    /// a test can prove that the engine never runs user code while it analyses a tree.
    /// </summary>
    public sealed class Counting : IEnumerable<double>
    {
        /// <summary>The number of user calls on this thread.</summary>
        [ThreadStatic]
        public static int Calls;

        /// <summary>Initializes the instance (a user call).</summary>
        public Counting() => Calls++;

        /// <summary>Gets or sets a value (user calls).</summary>
        public double Value
        {
            get { Calls++; return 0.0; }
            set => Calls++;
        }

        /// <summary>Gets the nested instance (a user call).</summary>
        public Counting Child { get { Calls++; return this; } }

        /// <summary>Gets or sets a cell (user calls).</summary>
        /// <param name="index">The index.</param>
        /// <returns>Zero.</returns>
        public double this[int index]
        {
            get { Calls++; return 0.0; }
            set => Calls++;
        }

        /// <summary>Adds a value (a user call).</summary>
        /// <param name="value">The value.</param>
        public void Add(double value) => Calls++;

        /// <inheritdoc/>
        public IEnumerator<double> GetEnumerator() { Calls++; return Enumerable.Empty<double>().GetEnumerator(); }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>Wraps a double expression in a member initialization read back: <c>new StrongBox&lt;double&gt; { Value = e }.Value</c>.</summary>
    /// <param name="e">The wrapped expression.</param>
    /// <returns>The wrapper.</returns>
    private static Expression MemberInitLevel(Expression e)
        => Expression.Field(Expression.MemberInit(Expression.New(typeof(StrongBox<double>)), Expression.Bind(BoxValue, e)), BoxValue);

    /// <summary>Wraps a double expression in a list initialization read through the indexer: <c>new List&lt;double&gt; { e }[0]</c>.</summary>
    /// <param name="e">The wrapped expression.</param>
    /// <returns>The wrapper.</returns>
    private static Expression ListInitLevel(Expression e)
        => Expression.MakeIndex(Expression.ListInit(Expression.New(typeof(List<double>)), e), ListItem, [Expression.Constant(0)]);

    /// <summary>Wraps a double expression in an array access: <c>new[] { e }[0]</c> (an <see cref="IndexExpression"/>).</summary>
    /// <param name="e">The wrapped expression.</param>
    /// <returns>The wrapper.</returns>
    private static Expression ArrayLevel(Expression e)
        => Expression.ArrayAccess(Expression.NewArrayInit(typeof(double), e), Expression.Constant(0));

    /// <summary>Wraps a double expression in a list binding read back: <c>new Node { Items = { e } }.Value + p</c>.</summary>
    /// <param name="e">The wrapped expression.</param>
    /// <returns>The wrapper.</returns>
    private static Expression ListBindingLevel(Expression e)
        => Expression.Add(
            Expression.Field(
                Expression.MemberInit(Expression.New(typeof(Node)), Expression.ListBind(typeof(Node).GetField(nameof(Node.Items))!, Expression.ElementInit(typeof(List<double>).GetMethod(nameof(List<double>.Add))!, e))),
                nameof(Node.Value)),
            P);

    /// <summary>
    /// Builds <c>new Node { Next = { Next = { ... { Value = p } } } }.Value</c>: <paramref name="depth"/> member bindings nested
    /// inside one initialization, with no expression node between two levels.
    /// </summary>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The tree.</returns>
    private static Expression DeepMemberBindings(int depth)
    {
        MemberInfo next = typeof(Node).GetProperty(nameof(Node.Next))!;
        MemberBinding binding = Expression.Bind(typeof(Node).GetField(nameof(Node.Value))!, P);
        for (int i = 0; i < depth; i++) binding = Expression.MemberBind(next, binding);
        return Expression.Field(Expression.MemberInit(Expression.New(typeof(Node)), binding), nameof(Node.Value));
    }

    /// <summary>The hostile nestings of the scenarios, each built fresh by its factory.</summary>
    private static readonly (string Name, Func<int, Expression> Build)[] Nestings =
    [
        ("member init", depth => Chain(depth, MemberInitLevel)),
        ("list init and indexer", depth => Chain(depth, ListInitLevel)),
        ("array index", depth => Chain(depth, ArrayLevel)),
        ("list binding", depth => Chain(depth, ListBindingLevel)),
        ("nested member bindings", DeepMemberBindings),
    ];

    /// <summary>Runs <paramref name="walk"/> inside an operation without any bounded limit.</summary>
    /// <param name="walk">The walk.</param>
    /// <returns>Its result.</returns>
    private static object RunUnlimited(Func<object> walk)
        => ExpressionResourceBudget.Run(NoLimits, null, null, null, walk, static w => w());

    /// <summary>
    /// Every walk ends a hostile nesting of each R3 family with a controlled Stack refusal on a 256 KiB thread: transformation,
    /// substitution (and the capture check of a substitution entering a binder, and the free-variable walk of a deep
    /// replacement), the inlining analysis, the comparer, hashing and canonical keys. The session and the S4 scope stack are
    /// restored and the thread stays usable.
    /// </summary>
    [TestMethod]
    public void HostileNesting_OfEveryR3Family_IsRefusedOnASmallStack()
    {
        foreach ((string family, Func<int, Expression> build) in Nestings)
        {
            Expression deep = build(HostileDepth);
            Expression deep2 = build(HostileDepth);
            ParameterExpression v = Expression.Parameter(typeof(double), "v");
            ParameterExpression q = Expression.Parameter(typeof(double), "q");
            Expression binder = Expression.Lambda<Func<double, double>>(Expression.Add(deep, v), v);
            // A loop is a substitution barrier: the inlining analysis searches it with its reference finder.
            LabelTarget exit = Expression.Label(typeof(double), "exit");
            LambdaExpression invoked = Expression.Lambda<Func<double, double>>(Expression.Loop(Expression.Break(exit, Expression.Add(deep, q)), exit), q);
            (string Name, Action Run)[] paths =
            [
                ("simplify", () => new ExpressionSimplifier(NoLimits).Simplify(deep)),
                ("generic transform", () => new RecursiveTransformer(NoLimits).Transform(deep)),
                ("substitution", () => new RecursiveTransformer(NoLimits).Replace(deep, P, Expression.Constant(1.0))),
                ("capture check", () => new RecursiveTransformer(NoLimits).Replace(binder, P, v)),
                ("deep replacement", () => new RecursiveTransformer(NoLimits).Replace(Expression.Lambda<Func<double, double>>(Expression.Add(P, v), v), P, Expression.Add(deep, v))),
                ("inlining analysis", () => RunUnlimited(() => InvocationInliningAnalysis.Analyze(invoked, Expression.Invoke(invoked, Expression.Constant(1.0))))),
                ("comparer equals", () => ExpressionComparer.Create(NoLimits).Equals(deep, deep2)),
                ("comparer hash", () => ExpressionComparer.Create(NoLimits).GetHashCode(deep)),
                ("raw equality", () => RunUnlimited(() => ExpressionComparer.StructuralEqualsRaw(deep, deep2))),
                ("raw hash", () => RunUnlimited(() => ExpressionComparer.StructuralHashRaw(deep))),
                ("canonical key", () => RunUnlimited(() => ExpressionCanonicalOrder.BuildKey(deep, []))),
            ];

            foreach ((string name, Action run) in paths)
            {
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
    }

    /// <summary>With the default limits, a hostile nesting of each R3 family is refused by the validator before any recursion (bindings count as levels).</summary>
    [TestMethod]
    public void DefaultLimits_RefuseHostileNestingBeforeRecursing()
    {
        foreach ((string family, Func<int, Expression> build) in Nestings)
        {
            Expression deep = build(HostileDepth);
            RunOnThread(() =>
            {
                ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpressionSimplifier().Simplify(deep), family);
                Assert.AreEqual(ExpressionResourceLimitKind.Depth, exception.Kind, family);
            }, SmallStack, Timeout);
        }
    }

    /// <summary>
    /// Structural equality, hashing and keys of every R3 family consume the structural budget (bindings and initializers
    /// included): equal trees compare equal, hash equally and tie, and an exhausted budget is an exception, never
    /// <see langword="false"/>, a hash, a key or a partial substitution.
    /// </summary>
    [TestMethod]
    public void StructuralWalks_OfEveryR3Family_AreBudgeted()
    {
        foreach ((string family, Func<int, Expression> build) in Nestings)
        {
            Expression a = build(40);
            Expression b = build(40);
            Assert.IsTrue((bool)RunUnlimited(() => ExpressionComparer.StructuralEqualsRaw(a, b)), family);
            Assert.AreEqual(RunUnlimited(() => ExpressionComparer.StructuralHashRaw(a)), RunUnlimited(() => ExpressionComparer.StructuralHashRaw(b)), family);
            Assert.AreEqual(0, (int)RunUnlimited(() => ExpressionCanonicalOrder.Compare(a, b, [])), family);

            var tight = ExpressionResourceLimits.Default with { MaxStructuralVisits = 20 };
            (string Name, Func<object> Walk)[] walks =
            [
                ("raw equality", () => ExpressionComparer.StructuralEqualsRaw(a, b)),
                ("raw hash", () => ExpressionComparer.StructuralHashRaw(a)),
                ("canonical key", () => ExpressionCanonicalOrder.BuildKey(a, [])),
                ("substitution", () => new RecursiveTransformer(tight).Replace(a, P, Expression.Constant(1.0))),
            ];
            foreach ((string name, Func<object> walk) in walks)
            {
                ExpressionResourceLimitException refusal = Assert.ThrowsExactly<ExpressionResourceLimitException>(
                    () => ExpressionResourceBudget.Run(tight, null, null, null, walk, static w => w()), family + " / " + name);
                Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, refusal.Kind, family + " / " + name);
            }

            Assert.ThrowsExactly<ExpressionResourceLimitException>(() => ExpressionComparer.Create(tight).Equals(a, b), family);
            Assert.ThrowsExactly<ExpressionResourceLimitException>(() => ExpressionComparer.Create(tight).GetHashCode(a), family);
        }
    }

    /// <summary>
    /// Wide lists cannot evade the cumulative work budget: a member initialization with 5,000 bindings, a list binding with
    /// 5,000 initializers, 300 sixteen-argument initializers and a list initialization with 5,000 elements are compared,
    /// hashed, keyed, substituted and transformed only within the budgets, and are refused by budgets smaller than their width.
    /// </summary>
    [TestMethod]
    public void WideBindingAndInitializerLists_AreCountedByTheWorkBudget()
    {
        MethodInfo listAdd = typeof(List<double>).GetMethod(nameof(List<double>.Add))!;
        MethodInfo wideAdd = typeof(WideList).GetMethod(nameof(WideList.Add))!;
        Expression ManyBindings() => Expression.MemberInit(Expression.New(typeof(StrongBox<double>)), Enumerable.Range(0, 5000).Select(_ => Expression.Bind(BoxValue, P)));
        Expression ManyListBindings() => Expression.MemberInit(
            Expression.New(typeof(Node)),
            Expression.ListBind(typeof(Node).GetField(nameof(Node.Items))!, Enumerable.Range(0, 5000).Select(_ => Expression.ElementInit(listAdd, P))));
        Expression ManyArguments() => Expression.ListInit(Expression.New(typeof(WideList)), Enumerable.Range(0, 300).Select(_ => Expression.ElementInit(wideAdd, Enumerable.Repeat<Expression>(P, 16))));
        Expression ManyElements() => Expression.ListInit(Expression.New(typeof(List<double>)), Enumerable.Range(0, 5000).Select(_ => P));

        foreach ((string name, Func<Expression> build) in new (string, Func<Expression>)[] { ("many bindings", ManyBindings), ("many list bindings", ManyListBindings), ("many arguments", ManyArguments), ("many elements", ManyElements) })
        {
            Expression a = build();
            Expression b = build();
            Assert.IsTrue((bool)RunUnlimited(() => ExpressionComparer.StructuralEqualsRaw(a, b)), name);
            Assert.AreEqual(RunUnlimited(() => ExpressionComparer.StructuralHashRaw(a)), RunUnlimited(() => ExpressionComparer.StructuralHashRaw(b)), name);

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
                    () => ExpressionResourceBudget.Run(tight, null, null, null, run, static w => w()), name + " / " + walk);
                Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, refusal.Kind, name + " / " + walk);
            }

            ExpressionResourceLimitException transform = Assert.ThrowsExactly<ExpressionResourceLimitException>(
                () => new RecursiveTransformer(ExpressionResourceLimits.Default with { MaxTransformVisits = 2000 }).Transform(a), name);
            Assert.AreEqual(ExpressionResourceLimitKind.Work, transform.Kind, name);
            Assert.IsInstanceOfType(new ExpressionSimplifier().Simplify(a), a.GetType(), name);
        }
    }

    /// <summary>
    /// A very large list initialization (100,000 elements) is refused by the default node limit before any work, and under
    /// unlimited shape limits by the transformation budget, never by a stack overflow or an unbounded run.
    /// </summary>
    [TestMethod]
    public void VeryLargeListInitialization_IsRefusedByTheLimits()
    {
        Expression huge = Expression.ListInit(Expression.New(typeof(List<double>)), Enumerable.Range(0, 100_000).Select(_ => Expression.Multiply(P, Expression.Constant(1.0))));

        Assert.AreEqual(ExpressionResourceLimitKind.Nodes, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpressionSimplifier().Simplify(huge)).Kind);
        Assert.AreEqual(ExpressionResourceLimitKind.Work, Assert.ThrowsExactly<ExpressionResourceLimitException>(
            () => new ExpressionSimplifier(ExpressionResourceLimits.Default with { MaxNodes = long.MaxValue, MaxTransformVisits = 50_000 }).Simplify(huge)).Kind);
    }

    /// <summary>
    /// A shared DAG in initializer arguments (<c>e = new List { e, e }[0]</c>, 40 levels) is compared, hashed and substituted per
    /// logical node: the budget bounds it, the public substitution refuses it by the default node limit, and without shape
    /// limits it is validated in linear time (never unfolded), so the walk itself is refused by the structural budget.
    /// </summary>
    [TestMethod]
    public void SharedDagInInitializerArguments_IsBoundedByTheBudget()
    {
        Expression dag = Chain(40, static e => Expression.MakeIndex(Expression.ListInit(Expression.New(typeof(List<double>)), e, e), ListItem, [Expression.Constant(0)]));
        var tight = ExpressionResourceLimits.Default with { MaxStructuralVisits = 100_000 };

        foreach ((string name, Func<object> walk) in new (string, Func<object>)[]
        {
            ("raw equality", () => ExpressionComparer.StructuralEqualsRaw(dag, dag)),
            ("raw hash", () => ExpressionComparer.StructuralHashRaw(dag)),
            ("canonical key", () => ExpressionCanonicalOrder.BuildKey(dag, [])),
        })
        {
            ExpressionResourceLimitException refusal = Assert.ThrowsExactly<ExpressionResourceLimitException>(
                () => ExpressionResourceBudget.Run(tight, null, null, null, walk, static w => w()), name);
            Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, refusal.Kind, name);
        }

        Assert.AreEqual(ExpressionResourceLimitKind.Nodes, Assert.ThrowsExactly<ExpressionResourceLimitException>(
            () => new RecursiveTransformer(tight).Replace(dag, P, Expression.Constant(1.0))).Kind);

        var transformer = new RecursiveTransformer(tight);
        Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, Assert.ThrowsExactly<ExpressionResourceLimitException>(
            () => ExpressionResourceBudget.Run(tight, transformer, null, null, dag, d => transformer.Replace(d, P, Expression.Constant(1.0)))).Kind);

        var watch = Stopwatch.StartNew();
        Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, Assert.ThrowsExactly<ExpressionResourceLimitException>(
            () => new RecursiveTransformer(NoLimits with { MaxStructuralVisits = 100_000 }).Replace(dag, P, Expression.Constant(1.0))).Kind);
        Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, Assert.ThrowsExactly<ExpressionResourceLimitException>(
            () => new RecursiveTransformer(ExpressionResourceLimits.Default with { MaxNodes = long.MaxValue, MaxStructuralVisits = 100_000 }).Replace(dag, P, Expression.Constant(1.0))).Kind);
        ExpressionShapeValidator.Validate(dag, int.MaxValue, long.MaxValue);
        watch.Stop();
        Assert.IsTrue(watch.Elapsed < TimeSpan.FromSeconds(10), "The shared DAG must never be unfolded: " + watch.Elapsed);
    }

    /// <summary>A transformer whose rule on member initializations compares the node with the public comparer (a re-entrant public call).</summary>
    private sealed class ComparingTransformer : ExpressionTransformer
    {
        /// <summary>Initializes the transformer.</summary>
        /// <param name="limits">Its limits.</param>
        public ComparingTransformer(ExpressionResourceLimits limits) : base(limits) { }

        /// <summary>Compares the initialization with itself through the public comparer, then keeps it.</summary>
        /// <param name="node">The member initialization.</param>
        /// <returns>The node.</returns>
        [ExpressionSignature(ExpressionType.MemberInit)]
        private Expression Compare(MemberInitExpression node)
        {
            ExpressionComparer.Default.Equals(node, Expression.MemberInit(node.NewExpression, node.Bindings));
            return node;
        }

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <inheritdoc/>
        protected override Expression PrepareExpression(Expression e) => TransformCore(e);

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => CopyExpression(e, parameters);
    }

    /// <summary>
    /// A re-entrant public comparison from a rule on member initializations joins the running operation's tighter limits: it
    /// is refused with the outer structural budget (never converted into a result), and the session is restored.
    /// </summary>
    [TestMethod]
    public void ReentrantComparisonFromARule_OnlyTightensTheLimits()
    {
        Expression tree = Chain(30, MemberInitLevel);
        var transformer = new ComparingTransformer(ExpressionResourceLimits.Default with { MaxStructuralVisits = 50 });

        ExpressionResourceLimitException thrown = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => transformer.Transform(tree));

        Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, thrown.Kind);
        Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
        Assert.IsInstanceOfType<MemberExpression>(new ComparingTransformer(ExpressionResourceLimits.Default).Transform(tree));
    }

    /// <summary>A simplifier whose <c>PrepareExpression</c> fails on a marker constant.</summary>
    private sealed class FailingSimplifier : ExpressionSimplifier
    {
        /// <summary>Initializes the simplifier with unlimited resources.</summary>
        public FailingSimplifier() : base(ExpressionResourceLimits.Unlimited) { }

        /// <inheritdoc/>
        protected override Expression PrepareExpression(Expression e)
        {
            if (e is ConstantExpression { Value: 13.5 }) throw new InvalidOperationException("marker");
            return base.PrepareExpression(e);
        }
    }

    /// <summary>
    /// An exception thrown inside a binding, an initializer or an index argument, under a lambda and a block scope, leaves no
    /// S4 scope and no operation open, and the next simplification on the thread works.
    /// </summary>
    [TestMethod]
    public void ExceptionsInsideBindings_LeaveNoScopeOpen()
    {
        ParameterExpression local = Expression.Variable(typeof(double), "local");
        Expression marker = Expression.Constant(13.5);
        Expression[] failing =
        [
            Expression.Lambda<Func<double>>(Expression.Block([local], MemberInitLevel(marker))),
            Expression.Lambda<Func<double>>(Expression.Block([local], DeepMemberBindingsWith(marker))),
            Expression.Block([local], ListInitLevel(Expression.Add(local, marker))),
            Expression.Lambda<Func<double, double>>(ArrayLevel(Expression.ArrayAccess(Expression.NewArrayInit(typeof(double), P), Expression.Convert(marker, typeof(int)))), P),
        ];

        foreach (Expression tree in failing)
        {
            Assert.ThrowsExactly<InvalidOperationException>(() => new FailingSimplifier().Simplify(tree));
            Assert.AreEqual(0, ExpressionSimplifier.LexicalScopeDepth);
            Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
        }

        Assert.IsTrue(ExpressionComparer.Default.Equals(new ExpressionSimplifier().Simplify(MemberInitLevel(Expression.Add(P, Expression.Constant(0.0)))), MemberInitLevel(P)));
    }

    /// <summary>Builds a three-level nested member binding whose innermost value is <paramref name="value"/>.</summary>
    /// <param name="value">The innermost assigned value.</param>
    /// <returns>The tree.</returns>
    private static Expression DeepMemberBindingsWith(Expression value)
    {
        MemberInfo next = typeof(Node).GetProperty(nameof(Node.Next))!;
        MemberBinding binding = Expression.Bind(typeof(Node).GetField(nameof(Node.Value))!, value);
        for (int i = 0; i < 3; i++) binding = Expression.MemberBind(next, binding);
        return Expression.Field(Expression.MemberInit(Expression.New(typeof(Node)), binding), nameof(Node.Value));
    }

    /// <summary>
    /// The engine never runs user code while it analyses initializations and index accesses: simplification, substitution,
    /// inlining analysis, comparison, hashing and keys of a tree full of counted constructors, setters, getters, <c>Add</c>
    /// calls and indexer accessors leave the call counter at zero.
    /// </summary>
    [TestMethod]
    public void Analysis_NeverRunsUserCode()
    {
        MemberInfo value = typeof(Counting).GetProperty(nameof(Counting.Value))!;
        MemberInfo child = typeof(Counting).GetProperty(nameof(Counting.Child))!;
        PropertyInfo item = typeof(Counting).GetProperty("Item")!;
        MethodInfo add = typeof(Counting).GetMethod(nameof(Counting.Add))!;
        Expression Build() => Expression.Add(
            Expression.MakeIndex(
                Expression.MemberInit(Expression.New(typeof(Counting)), Expression.Bind(value, P), Expression.MemberBind(child, Expression.Bind(value, P))),
                item,
                [Expression.Constant(0)]),
            Expression.Property(Expression.ListInit(Expression.New(typeof(Counting)), Expression.ElementInit(add, P)), value as PropertyInfo ?? throw new InvalidOperationException()));

        ParameterExpression q = Expression.Parameter(typeof(double), "q");
        LambdaExpression lambda = Expression.Lambda<Func<double, double>>(Build(), q);
        Counting.Calls = 0;
        Expression a = Build();
        Expression b = Build();
        new ExpressionSimplifier().Simplify(a);
        new RecursiveTransformer().Replace(a, P, Expression.Constant(2.0));
        new ExpressionSimplifier().Simplify(Expression.Invoke(lambda, Expression.Constant(1.0)));
        Assert.IsTrue(ExpressionComparer.Default.Equals(a, b));
        Assert.AreEqual(ExpressionComparer.Default.GetHashCode(a), ExpressionComparer.Default.GetHashCode(b));
        RunUnlimited(() => ExpressionCanonicalOrder.BuildKey(a, []));
        Assert.AreEqual(0, Counting.Calls);
    }

    /// <summary>
    /// No operation keeps a reference to the trees it analysed: after simplifying, substituting, comparing, hashing and keying
    /// an initialization, the tree is collectable.
    /// </summary>
    [TestMethod]
    public void Operations_RetainNoReferenceToTheirTrees()
    {
        WeakReference reference = AnalyseAndForget();
        for (int i = 0; i < 3 && reference.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.IsFalse(reference.IsAlive, "An operation retained the tree.");
    }

    /// <summary>Builds an initialization, runs every operation on it, and returns a weak reference to it.</summary>
    /// <returns>The weak reference.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AnalyseAndForget()
    {
        Expression tree = ListBindingLevel(MemberInitLevel(ListInitLevel(ArrayLevel(Expression.Multiply(P, Expression.Constant(1.0))))));
        Expression twin = ListBindingLevel(MemberInitLevel(ListInitLevel(ArrayLevel(Expression.Multiply(P, Expression.Constant(1.0))))));
        new ExpressionSimplifier().Simplify(tree);
        new RecursiveTransformer().Replace(tree, P, Expression.Constant(2.0));
        ExpressionComparer.Default.Equals(tree, twin);
        ExpressionComparer.Default.GetHashCode(tree);
        RunUnlimited(() => ExpressionCanonicalOrder.BuildKey(tree, []));
        return new WeakReference(tree);
    }
}
