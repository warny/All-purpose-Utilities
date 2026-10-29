using System;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;
using static UtilsTest.Security.Expressions.ExpressionResourceTestTrees;

namespace UtilsTest.Security.Expressions;

/// <summary>
/// ETC R2: the binding and control-flow families (blocks, try/catch, switches, loops, jumps and labels, nested lambdas)
/// under the shared resource session. Hostile nestings end with a controlled refusal on a small stack on every walk (traversal,
/// capture-avoiding substitution, comparison, hashing, canonical keys); wide handler and case lists cannot evade the
/// cumulative work budget; a refusal is never turned into a result; a re-entrant comparison from a rule only tightens the
/// limits; an exception thrown inside nested scopes leaves no scope open.
/// </summary>
[TestClass]
public class ExpressionControlFlowResourceTests
{
    /// <summary>Upper bound of the worker threads.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <summary>The small worker stack of the stack-guard tests.</summary>
    private const int SmallStack = 256 * 1024;

    /// <summary>A depth far beyond any stack measured in R0.</summary>
    private const int HostileDepth = 100_000;

    /// <summary>Limits disabling validation and work budgets: only the stack guard can stop the recursion.</summary>
    private static readonly ExpressionResourceLimits NoLimits = ExpressionResourceLimits.Unlimited;

    /// <summary>Wraps a double expression in a block declaring a local assigned from it: <c>{ double v; v = e; v + p }</c>.</summary>
    /// <param name="e">The wrapped expression.</param>
    /// <returns>The wrapper.</returns>
    private static Expression BlockLevel(Expression e)
    {
        ParameterExpression local = Expression.Variable(typeof(double), "v");
        return Expression.Block([local], Expression.Assign(local, e), Expression.Add(local, P));
    }

    /// <summary>Wraps a double expression in <c>try { e } catch (Exception x) when (x != null) { p }</c>.</summary>
    /// <param name="e">The wrapped expression.</param>
    /// <returns>The wrapper.</returns>
    private static Expression TryLevel(Expression e)
    {
        ParameterExpression exception = Expression.Variable(typeof(Exception), "x");
        return Expression.TryCatch(e, Expression.Catch(exception, P, Expression.NotEqual(exception, Expression.Constant(null, typeof(Exception)))));
    }

    /// <summary>Wraps a double expression in <c>switch (p) { case 1: e; default: p }</c>.</summary>
    /// <param name="e">The wrapped expression.</param>
    /// <returns>The wrapper.</returns>
    private static Expression SwitchLevel(Expression e) => Expression.Switch(P, P, Expression.SwitchCase(e, Expression.Constant(1.0)));

    /// <summary>Wraps a double expression in a loop breaking with it: <c>loop { break exit(e); }</c>.</summary>
    /// <param name="e">The wrapped expression.</param>
    /// <returns>The wrapper.</returns>
    private static Expression LoopLevel(Expression e)
    {
        LabelTarget exit = Expression.Label(typeof(double), "exit");
        return Expression.Loop(Expression.Break(exit, e), exit);
    }

    /// <summary>Wraps a double expression in a forward jump to a label: <c>{ return target(e); target: p }</c>.</summary>
    /// <param name="e">The wrapped expression.</param>
    /// <returns>The wrapper.</returns>
    private static Expression JumpLevel(Expression e)
    {
        LabelTarget target = Expression.Label(typeof(double), "target");
        return Expression.Block(Expression.Return(target, e), Expression.Label(target, P));
    }

    /// <summary>
    /// Wraps a double expression in an invoked nested lambda capturing it: <c>((x) =&gt; e + x)(Opaque(p))</c> (a binder per
    /// level; the observable argument keeps the simplifier from inlining it).
    /// </summary>
    /// <param name="e">The wrapped expression.</param>
    /// <returns>The wrapper.</returns>
    private static Expression LambdaLevel(Expression e)
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        return Expression.Invoke(Expression.Lambda<Func<double, double>>(Expression.Add(e, x), x), Expression.Call(OpaqueMethod, P));
    }

    /// <summary>The families nested by the scenarios.</summary>
    private static readonly (string Name, Func<Expression, Expression> Wrap)[] Families =
    [
        ("block", BlockLevel),
        ("try/catch", TryLevel),
        ("switch", SwitchLevel),
        ("loop", LoopLevel),
        ("goto/label", JumpLevel),
        ("nested lambda", LambdaLevel),
    ];

    /// <summary>Runs <paramref name="walk"/> inside an operation without any bounded limit.</summary>
    /// <param name="walk">The walk.</param>
    /// <returns>Its result.</returns>
    private static object RunUnlimited(Func<object> walk)
        => ExpressionResourceBudget.Run(NoLimits, null, null, null, walk, static w => w());

    /// <summary>
    /// Every walk ends a hostile nesting of each R2 family with a controlled Stack refusal on a 256 KiB thread (substitution
    /// through binders included), restores the session and the S4 scope stack, and leaves the thread usable.
    /// </summary>
    [TestMethod]
    public void HostileNesting_OfEveryR2Family_IsRefusedOnASmallStack()
    {
        foreach ((string family, Func<Expression, Expression> wrap) in Families)
        {
            Expression deep = Chain(HostileDepth, wrap);
            Expression deep2 = Chain(HostileDepth, wrap);
            (string Name, Action Run)[] paths =
            [
                ("simplify", () => new ExpressionSimplifier(NoLimits).Simplify(deep)),
                ("generic transform", () => new RecursiveTransformer(NoLimits).Transform(deep)),
                ("substitution", () => new RecursiveTransformer(NoLimits).Replace(deep, P, Expression.Constant(1.0))),
                ("capturing substitution", () => new RecursiveTransformer(NoLimits).Replace(deep, P, Expression.Variable(typeof(double), "v"))),
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

    /// <summary>With the default limits, a hostile nesting of each R2 family is refused by the validator before any recursion.</summary>
    [TestMethod]
    public void DefaultLimits_RefuseHostileNestingBeforeRecursing()
    {
        foreach ((string family, Func<Expression, Expression> wrap) in Families)
        {
            Expression deep = Chain(HostileDepth, wrap);
            RunOnThread(() =>
            {
                ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpressionSimplifier().Simplify(deep), family);
                Assert.AreEqual(ExpressionResourceLimitKind.Depth, exception.Kind, family);
            }, SmallStack, Timeout);
        }
    }

    /// <summary>
    /// Structural equality, hashing and keys of every R2 family consume the structural budget: equal trees compare equal, hash
    /// equally and tie, and an exhausted budget is an exception, never <see langword="false"/>, a hash or a key.
    /// </summary>
    [TestMethod]
    public void StructuralWalks_OfEveryR2Family_AreBudgeted()
    {
        foreach ((string family, Func<Expression, Expression> wrap) in Families)
        {
            Expression a = Chain(40, wrap);
            Expression b = Chain(40, wrap);
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
    /// Wide handler, case and label lists cannot evade the cumulative work budget: comparing, hashing, keying and substituting
    /// a try with 5,000 handlers, a switch with 5,000 cases or a block with 2,500 jump/label pairs is refused by a budget
    /// smaller than their width, and succeeds without limits.
    /// </summary>
    [TestMethod]
    public void WideContainerLists_AreCountedByTheWorkBudget()
    {
        Expression WideTry() => Expression.TryCatch(P, Enumerable.Range(0, 5000).Select(_ => Expression.Catch(typeof(Exception), P)).ToArray());
        Expression WideSwitch() => Expression.Switch(P, P, Enumerable.Range(0, 5000).Select(i => Expression.SwitchCase(P, Expression.Constant((double)i))).ToArray());
        Expression ManyLabels()
        {
            var statements = Enumerable.Range(0, 2500).SelectMany(i =>
            {
                LabelTarget target = Expression.Label("l" + i);
                return new Expression[] { Expression.Goto(target), Expression.Label(target) };
            }).Append(P);
            return Expression.Block(statements);
        }

        foreach ((string name, Func<Expression> build) in new (string, Func<Expression>)[] { ("wide try", WideTry), ("wide switch", WideSwitch), ("many labels", ManyLabels) })
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
        }
    }

    /// <summary>A shared DAG of blocks is compared, hashed and substituted per logical node: the budget bounds an exponential DAG.</summary>
    [TestMethod]
    public void SharedDagOfBlocks_IsBoundedByTheBudget()
    {
        Expression dag = Chain(40, static e => Expression.Add(BlockLevel(e), BlockLevel(e)));
        var tight = ExpressionResourceLimits.Default with { MaxStructuralVisits = 100_000 };

        foreach ((string name, Func<object> walk) in new (string, Func<object>)[]
        {
            ("raw equality", () => ExpressionComparer.StructuralEqualsRaw(dag, dag)),
            ("raw hash", () => ExpressionComparer.StructuralHashRaw(dag)),
        })
        {
            ExpressionResourceLimitException refusal = Assert.ThrowsExactly<ExpressionResourceLimitException>(
                () => ExpressionResourceBudget.Run(tight, null, null, null, walk, static w => w()), name);
            Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, refusal.Kind, name);
        }

        // The public substitution validates its input first: with the default node limit the DAG is refused by the validator.
        Assert.AreEqual(ExpressionResourceLimitKind.Nodes, Assert.ThrowsExactly<ExpressionResourceLimitException>(
            () => new RecursiveTransformer(tight).Replace(dag, P, Expression.Constant(1.0))).Kind);

        // Joined to a running operation (no second validation), the substitution walk itself is refused by the structural budget.
        var transformer = new RecursiveTransformer(tight);
        Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, Assert.ThrowsExactly<ExpressionResourceLimitException>(
            () => ExpressionResourceBudget.Run(tight, transformer, null, null, dag, d => transformer.Replace(d, P, Expression.Constant(1.0)))).Kind);
    }

    /// <summary>A transformer whose rule on blocks compares the block with the public comparer (a re-entrant public call).</summary>
    private sealed class ComparingTransformer : ExpressionTransformer
    {
        /// <summary>Initializes the transformer.</summary>
        /// <param name="limits">Its limits.</param>
        public ComparingTransformer(ExpressionResourceLimits limits) : base(limits) { }

        /// <summary>Compares the block with a rebuilt copy, then keeps it.</summary>
        /// <param name="block">The block.</param>
        /// <returns>The block.</returns>
        [ExpressionSignature(ExpressionType.Block)]
        private Expression Compare(BlockExpression block)
        {
            ExpressionComparer.Default.Equals(block, Expression.Block(block.Variables, block.Expressions));
            return block;
        }

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <inheritdoc/>
        protected override Expression PrepareExpression(Expression e) => TransformCore(e);

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => CopyExpression(e, parameters);
    }

    /// <summary>
    /// A re-entrant public comparison from a rule on blocks joins the running operation's tighter limits: it is refused with
    /// the work budget of the outer transformation (wrapped by the reflective rule call, never converted to a result), and
    /// the session is restored.
    /// </summary>
    [TestMethod]
    public void ReentrantComparisonFromARule_OnlyTightensTheLimits()
    {
        Expression tree = Chain(30, BlockLevel);
        var transformer = new ComparingTransformer(ExpressionResourceLimits.Default with { MaxStructuralVisits = 50 });

        Exception thrown = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => transformer.Transform(tree));

        Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, ((ExpressionResourceLimitException)thrown).Kind);
        Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
        Assert.IsInstanceOfType<BlockExpression>(new ComparingTransformer(ExpressionResourceLimits.Default).Transform(tree));
    }

    /// <summary>A transformer whose <c>PrepareExpression</c> fails on a marker constant, inside nested scopes.</summary>
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
    /// An exception thrown while a block, a catch handler and a nested lambda scope are open (in a statement, a filter, or a
    /// declaration that does not stay a parameter) leaves no S4 scope and no operation open.
    /// </summary>
    [TestMethod]
    public void ExceptionsInsideScopes_LeaveNoScopeOpen()
    {
        ParameterExpression local = Expression.Variable(typeof(double), "local");
        ParameterExpression exception = Expression.Variable(typeof(Exception), "e");
        Expression[] failing =
        [
            Expression.Lambda<Func<double>>(Expression.Block([local], Expression.Constant(13.5))),
            Expression.TryCatch(P, Expression.Catch(exception, P, Expression.Equal(Expression.Constant(13.5), P))),
            Expression.Block([local], Expression.Invoke(Expression.Lambda<Func<double>>(Expression.Block(Expression.Constant(13.5))))),
        ];

        foreach (Expression tree in failing)
        {
            Assert.ThrowsExactly<InvalidOperationException>(() => new FailingSimplifier().Simplify(tree));
            Assert.AreEqual(0, ExpressionSimplifier.LexicalScopeDepth);
            Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
        }

        Assert.IsTrue(ExpressionComparer.Default.Equals(new ExpressionSimplifier().Simplify(Expression.Block([local], Expression.Add(P, Expression.Constant(0.0)))), Expression.Block([local], P)));
    }
}
