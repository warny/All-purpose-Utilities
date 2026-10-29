using System;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;
using static UtilsTest.Security.Expressions.ExpressionResourceTestTrees;

namespace UtilsTest.Security.Expressions;

/// <summary>
/// ETC R1b/R1c: the scalar families traversed since R1b (member access, type tests, creations, array creations), compared,
/// hashed and keyed since R1c (with conditionals and invocations), and the effect analysis of R1b run under the shared
/// resource session: hostile nestings end with a controlled refusal on a small stack, budgets are consumed and never reset by
/// a handler, a refusal is never turned into a result, and a shared DAG is analyzed once.
/// </summary>
[TestClass]
public class ExpressionScalarFamilyResourceTests
{
    /// <summary>Upper bound of the worker threads.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <summary>The small worker stack of the stack-guard tests.</summary>
    private const int SmallStack = 256 * 1024;

    /// <summary>A depth far beyond any stack measured in R0.</summary>
    private const int HostileDepth = 100_000;

    /// <summary>Limits disabling validation and work budgets: only the stack guard can stop the recursion.</summary>
    private static readonly ExpressionResourceLimits NoLimits = ExpressionResourceLimits.Unlimited;

    /// <summary>The <see cref="StrongBox{T}"/> constructor taking the value.</summary>
    private static readonly System.Reflection.ConstructorInfo BoxConstructor = typeof(StrongBox<double>).GetConstructor([typeof(double)])!;

    /// <summary>Wraps a double expression in <c>new StrongBox&lt;double&gt;(e).Value</c> (a creation and a member access per level).</summary>
    /// <param name="e">The wrapped expression.</param>
    /// <returns>The wrapper.</returns>
    private static Expression BoxLevel(Expression e) => Expression.Field(Expression.New(BoxConstructor, e), nameof(StrongBox<double>.Value));

    /// <summary>Wraps a double expression in <c>(e is double) ? 1 : p</c>, a type test under a conditional (each level holds <paramref name="e"/> once, so the tree stays a tree).</summary>
    /// <param name="e">The wrapped expression.</param>
    /// <returns>The wrapper.</returns>
    private static Expression TypeTestLevel(Expression e) => Expression.Condition(Expression.TypeIs(Expression.Convert(e, typeof(object)), typeof(double)), Expression.Constant(1.0), P);

    /// <summary>Wraps a double expression in <c>new[] { e }[0]</c>, an array creation per level.</summary>
    /// <param name="e">The wrapped expression.</param>
    /// <returns>The wrapper.</returns>
    private static Expression ArrayLevel(Expression e) => Expression.ArrayIndex(Expression.NewArrayInit(typeof(double), e), Expression.Constant(0));

    /// <summary>Wraps a double expression in an invocation of a constant delegate.</summary>
    /// <param name="e">The wrapped expression.</param>
    /// <returns>The wrapper.</returns>
    private static Expression InvokeLevel(Expression e) => Expression.Invoke(Expression.Constant((Func<double, double>)Math.Abs), e);

    /// <summary>The families nested by the scenarios.</summary>
    private static readonly (string Name, Func<Expression, Expression> Wrap)[] Families =
    [
        ("member+new", BoxLevel),
        ("typeIs+conditional", TypeTestLevel),
        ("newArray", ArrayLevel),
        ("invoke", InvokeLevel),
    ];

    /// <summary>Runs <paramref name="walk"/> inside an operation without any bounded limit.</summary>
    /// <param name="walk">The walk.</param>
    /// <returns>Its result.</returns>
    private static object RunUnlimited(Func<object> walk)
        => ExpressionResourceBudget.Run(NoLimits, null, null, null, walk, static w => w());

    /// <summary>
    /// Every public path ends a hostile nesting of each new family with a controlled Stack refusal on a 256 KiB thread,
    /// restores the session, and leaves the thread usable.
    /// </summary>
    [TestMethod]
    public void HostileNesting_OfEveryNewFamily_IsRefusedOnASmallStack()
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
                ("comparer equals", () => ExpressionComparer.Create(NoLimits).Equals(deep, deep2)),
                ("comparer hash", () => ExpressionComparer.Create(NoLimits).GetHashCode(deep)),
                ("raw equality", () => RunUnlimited(() => ExpressionComparer.StructuralEqualsRaw(deep, deep2))),
                ("raw hash", () => RunUnlimited(() => ExpressionComparer.StructuralHashRaw(deep))),
                ("canonical key", () => RunUnlimited(() => ExpressionCanonicalOrder.BuildKey(deep, []))),
                ("effect analysis", () => RunUnlimited(() => ExpressionEffectAnalysis.Analyze(deep))),
            ];

            // An invocation is observable as a whole: its analysis does not recurse, so it cannot reach the stack guard.
            if (family == "invoke")
            {
                Assert.AreEqual(ExpressionEffects.Observable, (ExpressionEffects)RunUnlimited(() => ExpressionEffectAnalysis.Analyze(deep)));
                paths = paths.Where(path => path.Name != "effect analysis").ToArray();
            }

            foreach ((string name, Action run) in paths)
            {
                string label = family + " / " + name;
                RunOnThread(() =>
                {
                    ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(run, label);
                    Assert.AreEqual(ExpressionResourceLimitKind.Stack, exception.Kind, label);
                    Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth, label);
                    Assert.IsTrue(ExpressionComparer.Default.Equals(CallChain(10), CallChain(10)), label);
                }, SmallStack, Timeout);
            }
        }
    }

    /// <summary>With the default limits, a hostile nesting of each new family is refused by the validator before any recursion.</summary>
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
    /// The effect analysis counts one structural visit per node analysis; a small shared DAG is walked by the bounded pass
    /// (121 analyses for 61 distinct nodes), and an exponential DAG falls back to the table after the bounded pass, so its
    /// analysis stays linear in the distinct nodes; an exhausted budget is an exception rather than a classification.
    /// </summary>
    [TestMethod]
    public void EffectAnalysis_IsBudgetedAndAnalyzesSharedNodesOnce()
    {
        Expression shared = Chain(30, BoxLevel);
        Expression dag = Expression.Add(shared, shared);

        long visits = (long)RunUnlimited(() =>
        {
            ExpressionEffectAnalysis.Analyze(dag);
            return ExpressionResourceBudget.StructuralVisits;
        });

        // The add, then a member access and a creation per level, each copy of the shared chain walked once.
        Assert.AreEqual(121L, visits);

        Expression exponential = Chain(40, static e => Expression.Add(BoxLevel(e), BoxLevel(e)));
        long exponentialVisits = (long)RunUnlimited(() =>
        {
            ExpressionEffectAnalysis.Analyze(exponential);
            return ExpressionResourceBudget.StructuralVisits;
        });

        // 2^40 logical nodes, 200 distinct ones: at most the bounded pass (512) plus one analysis per distinct node.
        Assert.IsTrue(exponentialVisits <= 512 + 200, exponentialVisits.ToString());
        ExpressionResourceLimitException budget = Assert.ThrowsExactly<ExpressionResourceLimitException>(() =>
            ExpressionResourceBudget.Run(ExpressionResourceLimits.Default with { MaxStructuralVisits = 20 }, null, null, null, dag, static e => ExpressionEffectAnalysis.Analyze(e)));
        Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, budget.Kind);
    }

    /// <summary>
    /// Structural equality, hashing and keys of every new family (ETC R1c) consume the structural budget: equal trees compare
    /// equal and hash equally, and an exhausted budget is an exception, never <see langword="false"/>, a hash or a key.
    /// </summary>
    [TestMethod]
    public void StructuralWalks_OfEveryNewFamily_AreBudgeted()
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
    /// The traversal of every new family is counted by the transformation budget (one visit per node), so a budget just below
    /// the size of the tree is refused and a budget equal to it is enough.
    /// </summary>
    [TestMethod]
    public void Traversal_OfEveryNewFamily_IsCountedPerNode()
    {
        foreach ((string family, Func<Expression, Expression> wrap) in Families)
        {
            Expression tree = Chain(10, wrap);
            long visits = (long)RunUnlimited(() =>
            {
                new RecursiveTransformer(ExpressionResourceLimits.Default).Transform(tree);
                return ExpressionResourceBudget.TransformVisits;
            });
            Assert.IsTrue(visits > 10, family);

            ExpressionResourceLimitException work = Assert.ThrowsExactly<ExpressionResourceLimitException>(
                () => new RecursiveTransformer(ExpressionResourceLimits.Default with { MaxTransformVisits = visits - 1 }).Transform(tree), family);
            Assert.AreEqual(ExpressionResourceLimitKind.Work, work.Kind, family);
            new RecursiveTransformer(ExpressionResourceLimits.Default with { MaxTransformVisits = visits }).Transform(tree);
        }
    }
}
