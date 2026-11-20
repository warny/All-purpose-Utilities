using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using static UtilsTest.Security.Expressions.ExpressionResourceTestTrees;

namespace UtilsTest.Security.Expressions;

/// <summary>
/// Security invariants of <see cref="ExpressionOptimiser"/> (audit A10-C, ETC): an impersonating reflection object is never
/// read nor trusted, every walk is stack-guarded and counted against the shared resource session, hostile nestings of every
/// family end with a controlled refusal instead of a stack overflow, shared DAGs and wide trees are bounded, refusals are
/// never turned into results, and the session is restored.
/// </summary>
[TestClass]
public class ExpressionOptimiserSecurityTests
{
    /// <summary>Upper bound of the worker threads.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <summary>The small worker stack of the stack-guard tests.</summary>
    private const int SmallStack = 256 * 1024;

    /// <summary>A depth far beyond what a 256 KiB stack can recurse.</summary>
    private const int HostileDepth = 100_000;

    /// <summary>An <see cref="int"/> parameter.</summary>
    private static readonly ParameterExpression I = Expression.Parameter(typeof(int), "i");

    /// <summary>Adds two integers (impersonated below).</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The sum.</returns>
    public static int AddInts(int left, int right) => left + right;

    /// <summary>Multiplies two integers (impersonated below).</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The product.</returns>
    public static int MultiplyInts(int left, int right) => left * right;

    /// <summary>Negates an integer (impersonated below).</summary>
    /// <param name="value">The operand.</param>
    /// <returns>The negation.</returns>
    public static int NegateInt(int value) => -value;

    /// <summary>Returns an integer (impersonated below as a conversion).</summary>
    /// <param name="value">The operand.</param>
    /// <returns><paramref name="value"/>.</returns>
    public static int IdentityInt(int value) => value;

    /// <summary>A node of a linked structure, initialized through nested member bindings.</summary>
    public sealed class Node
    {
        /// <summary>The next node, created lazily.</summary>
        private Node? _next;

        /// <summary>Gets the next node.</summary>
        public Node Next => _next ??= new Node();

        /// <summary>A value.</summary>
        public double Value;
    }

    /// <summary>
    /// An identity, a product by zero, a double negation and an identity conversion built on impersonating operator methods are
    /// kept as built, and the optimiser never calls the impersonating objects (beyond the framework's type reads).
    /// </summary>
    [TestMethod]
    public void ImpersonatingOperatorMethods_AreNeverReadNorRemoved()
    {
        var add = new ImpersonatingMethod(typeof(ExpressionOptimiserSecurityTests).GetMethod(nameof(AddInts))!);
        var multiply = new ImpersonatingMethod(typeof(ExpressionOptimiserSecurityTests).GetMethod(nameof(MultiplyInts))!);
        var negate = new ImpersonatingMethod(typeof(ExpressionOptimiserSecurityTests).GetMethod(nameof(NegateInt))!);
        var convert = new ImpersonatingMethod(typeof(ExpressionOptimiserSecurityTests).GetMethod(nameof(IdentityInt))!);
        (string Name, Expression Tree, ImpersonatingMethod Method)[] cases =
        [
            ("x + 0", Expression.Add(I, Expression.Constant(0), add), add),
            ("0 + x", Expression.Add(Expression.Constant(0), I, add), add),
            ("x * 1", Expression.Multiply(I, Expression.Constant(1), multiply), multiply),
            ("x * 0", Expression.Multiply(I, Expression.Constant(0), multiply), multiply),
            ("0 * x", Expression.Multiply(Expression.Constant(0), I, multiply), multiply),
            ("-(-x)", Expression.Negate(Expression.Negate(I, negate), negate), negate),
            ("convert", Expression.Convert(I, typeof(int), convert), convert),
        ];

        foreach ((string name, Expression tree, ImpersonatingMethod method) in cases)
        {
            method.Trap.Reset();
            method.Trap.Armed = true;
            Expression optimized;
            try
            {
                optimized = new ExpressionOptimiser().Optimize(tree);
            }
            finally
            {
                method.Trap.Armed = false;
            }

            Assert.AreEqual(0, method.Trap.ArmedCalls, name + ": " + string.Join(", ", method.Trap.Calls));
            Assert.AreSame(tree, optimized, name);
        }
    }

    /// <summary>The optimiser walks bindings itself: the impersonating members of a member initialization are never read when nothing changes.</summary>
    [TestMethod]
    public void ImpersonatingMember_InAnUnchangedInitialization_IsNeverRead()
    {
        var field = new ImpersonatingField(typeof(Node).GetField(nameof(Node.Value))!);
        Expression init = Expression.MemberInit(Expression.New(typeof(Node)), Expression.Bind(field, Expression.Constant(1.0)));
        field.Trap.Reset();
        field.Trap.Armed = true;
        try
        {
            Assert.AreSame(init, new ExpressionOptimiser().Optimize(init));
        }
        finally
        {
            field.Trap.Armed = false;
        }

        Assert.AreEqual(0, field.Trap.ArmedCalls, string.Join(", ", field.Trap.Calls));
    }

    /// <summary>Hostile nestings of the families the optimiser walks.</summary>
    private static readonly (string Name, Func<int, Expression> Build)[] Nestings =
    [
        ("unary negations", NegateChain),
        ("unary conversions", depth => Chain(depth, static e => Expression.Convert(e, typeof(double)))),
        ("binary additions", depth => Chain(depth, static e => Expression.Add(e, P))),
        ("right-nested products", depth => Chain(depth, static e => Expression.Multiply(P, e))),
        ("calls", CallChain),
        ("conditionals", depth => Chain(depth, static e => Expression.Condition(Expression.Constant(false), Expression.Constant(0.0), e))),
        ("conditionals, discarded branch kept", depth => Chain(depth, static e => Expression.Condition(Expression.Constant(false), Expression.Loop(Expression.Empty(), Expression.Label(typeof(double))), e, typeof(double)))),
        ("blocks", depth => Chain(depth, static e => Expression.Block(Expression.Empty(), e))),
        ("lambdas", depth => Chain(depth, static e => Expression.Invoke(Expression.Lambda<Func<double>>(e)))),
        ("member init", depth => Chain(depth, static e => Expression.Field(Expression.MemberInit(Expression.New(typeof(StrongBox<double>)), Expression.Bind(typeof(StrongBox<double>).GetField(nameof(StrongBox<double>.Value))!, e)), nameof(StrongBox<double>.Value)))),
        ("list init", depth => Chain(depth, static e => Expression.Add(Expression.Convert(Expression.Property(Expression.ListInit(Expression.New(typeof(List<double>)), e), "Count"), typeof(double)), P))),
        ("nested member bindings", DeepMemberBindings),
        ("switch cases", depth => Chain(depth, static e => Expression.Switch(Expression.Constant(1), P, Expression.SwitchCase(e, Expression.Constant(1))))),
        ("catch blocks", depth => Chain(depth, static e => Expression.TryCatch(P, Expression.Catch(typeof(Exception), e)))),
    ];

    /// <summary>Builds <c>new Node { Next = { Next = { ... { Value = p } } } }.Value</c>: member bindings nested without any expression node in between.</summary>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The tree.</returns>
    private static Expression DeepMemberBindings(int depth)
    {
        MemberInfo next = typeof(Node).GetProperty(nameof(Node.Next))!;
        MemberBinding binding = Expression.Bind(typeof(Node).GetField(nameof(Node.Value))!, Expression.Multiply(P, Expression.Constant(1.0)));
        for (int i = 0; i < depth; i++) binding = Expression.MemberBind(next, binding);
        return Expression.Field(Expression.MemberInit(Expression.New(typeof(Node)), binding), nameof(Node.Value));
    }

    /// <summary>
    /// A hostile nesting of every family ends with a controlled Stack refusal on a 256 KiB thread when the shape and work limits
    /// are lifted, the session is restored and the thread stays usable.
    /// </summary>
    [TestMethod]
    public void HostileNesting_OfEveryFamily_IsRefusedOnASmallStack()
    {
        foreach ((string family, Func<int, Expression> build) in Nestings)
        {
            Expression deep = build(HostileDepth);
            RunOnThread(() =>
            {
                ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(
                    () => new ExpressionOptimiser(ExpressionResourceLimits.Unlimited).Optimize(deep), family);
                Assert.AreEqual(ExpressionResourceLimitKind.Stack, exception.Kind, family);
                Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth, family);
                Assert.AreSame(P, new ExpressionOptimiser().Optimize(Expression.Multiply(P, Expression.Constant(1.0))), family);
            }, SmallStack, Timeout);
        }
    }

    /// <summary>With the default limits, a hostile nesting of every family is refused by the validator before any recursion.</summary>
    [TestMethod]
    public void DefaultLimits_RefuseHostileNestingBeforeRecursing()
    {
        foreach ((string family, Func<int, Expression> build) in Nestings)
        {
            Expression deep = build(HostileDepth);
            RunOnThread(() =>
            {
                ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpressionOptimiser().Optimize(deep), family);
                Assert.AreEqual(ExpressionResourceLimitKind.Depth, exception.Kind, family);
            }, SmallStack, Timeout);
        }
    }

    /// <summary>A moderate nesting of every family, within the default depth, is optimised on a thread with the default stack size.</summary>
    [TestMethod]
    public void ModerateNesting_OfEveryFamily_IsOptimised()
    {
        foreach ((string family, Func<int, Expression> build) in Nestings)
        {
            Expression tree = build(80);
            RunOnThread(() => Assert.IsNotNull(new ExpressionOptimiser().Optimize(tree), family), 1024 * 1024, Timeout);
        }
    }

    /// <summary>
    /// The structural budget counts exactly one visit per node: a chain of <c>n</c> calls over a parameter (<c>n + 1</c> nodes)
    /// is optimised with a budget of <c>n + 1</c> visits and refused with <c>n</c>, the refusal reporting the limit.
    /// </summary>
    [TestMethod]
    public void StructuralBudget_IsExactAtTheLimit()
    {
        const int Levels = 200;
        Expression chain = CallChain(Levels);
        Assert.AreSame(chain, new ExpressionOptimiser(ExpressionResourceLimits.Default with { MaxStructuralVisits = Levels + 1 }).Optimize(chain));

        ExpressionResourceLimitException refusal = Assert.ThrowsExactly<ExpressionResourceLimitException>(
            () => new ExpressionOptimiser(ExpressionResourceLimits.Default with { MaxStructuralVisits = Levels }).Optimize(chain));
        Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, refusal.Kind);
        Assert.AreEqual(Levels, refusal.Limit);
        Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
    }

    /// <summary>Bindings, initializers, switch cases and catch blocks count one structural visit each, like nodes.</summary>
    [TestMethod]
    public void Containers_AreCountedByTheStructuralBudget()
    {
        FieldInfo value = typeof(StrongBox<double>).GetField(nameof(StrongBox<double>.Value))!;
        Expression wide = Expression.MemberInit(Expression.New(typeof(StrongBox<double>)), Enumerable.Range(0, 1000).Select(_ => Expression.Bind(value, P)));

        // 1 MemberInit + 1 New + 1000 bindings + 1000 parameters.
        Assert.AreSame(wide, new ExpressionOptimiser(ExpressionResourceLimits.Default with { MaxStructuralVisits = 2002 }).Optimize(wide));
        Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, Assert.ThrowsExactly<ExpressionResourceLimitException>(
            () => new ExpressionOptimiser(ExpressionResourceLimits.Default with { MaxStructuralVisits = 2001 }).Optimize(wide)).Kind);

        // 1 Switch + 1 value + 1 default + 300 x (case + test + body).
        Expression switched = Expression.Switch(Expression.Constant(1), P, Enumerable.Range(0, 300).Select(i => Expression.SwitchCase(P, Expression.Constant(i))).ToArray());
        Assert.AreSame(switched, new ExpressionOptimiser(ExpressionResourceLimits.Default with { MaxStructuralVisits = 903 }).Optimize(switched));
        Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpressionOptimiser(ExpressionResourceLimits.Default with { MaxStructuralVisits = 902 }).Optimize(switched));

        // 1 Try + 1 body + 300 x (catch + body).
        Expression handled = Expression.TryCatch(P, Enumerable.Range(0, 300).Select(_ => Expression.Catch(typeof(Exception), P)).ToArray());
        Assert.AreSame(handled, new ExpressionOptimiser(ExpressionResourceLimits.Default with { MaxStructuralVisits = 602 }).Optimize(handled));
        Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpressionOptimiser(ExpressionResourceLimits.Default with { MaxStructuralVisits = 601 }).Optimize(handled));
    }

    /// <summary>The input is validated against the depth and node limits at their exact boundary before any work.</summary>
    [TestMethod]
    public void ShapeLimits_AreExactAtTheLimit()
    {
        Expression chain = CallChain(99); // 100 nodes, depth 100.
        Assert.AreSame(chain, new ExpressionOptimiser(Shape(maxDepth: 100)).Optimize(chain));
        Assert.AreEqual(ExpressionResourceLimitKind.Depth, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpressionOptimiser(Shape(maxDepth: 99)).Optimize(chain)).Kind);
        Assert.AreSame(chain, new ExpressionOptimiser(Shape(maxNodes: 100)).Optimize(chain));
        Assert.AreEqual(ExpressionResourceLimitKind.Nodes, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpressionOptimiser(Shape(maxNodes: 99)).Optimize(chain)).Kind);
    }

    /// <summary>
    /// A heavily shared DAG (<c>e = e + e</c>, 40 levels) is refused by the default node limit (logical nodes), and, without shape
    /// limits, by the structural budget, quickly: it is never unfolded beyond the budget.
    /// </summary>
    [TestMethod]
    public void SharedDag_IsBoundedByTheLimits()
    {
        Expression dag = SharedDag(40);
        Assert.AreEqual(ExpressionResourceLimitKind.Nodes, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpressionOptimiser().Optimize(dag)).Kind);

        var watch = Stopwatch.StartNew();
        Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, Assert.ThrowsExactly<ExpressionResourceLimitException>(
            () => new ExpressionOptimiser(ExpressionResourceLimits.Unlimited with { MaxStructuralVisits = 100_000 }).Optimize(dag)).Kind);
        watch.Stop();
        Assert.IsTrue(watch.Elapsed < TimeSpan.FromSeconds(10), "The shared DAG must never be unfolded beyond the budget: " + watch.Elapsed);

        // A small shared DAG is optimised and keeps its sharing when nothing changes.
        Expression small = SharedDag(10);
        Assert.AreSame(small, new ExpressionOptimiser().Optimize(small));
    }

    /// <summary>
    /// A shared DAG whose products by zero are folded at every level (an effect analysis per level) is walked per logical node
    /// like every engine walk: within the default node limit it is folded, and with the shape limits lifted the structural
    /// budget, which also counts the effect analysis, refuses its unfolding quickly.
    /// </summary>
    [TestMethod]
    public void SharedProductsByZero_AreBoundedByTheBudget()
    {
        static Expression Build(int levels)
        {
            Expression e = Expression.Parameter(typeof(int), "x");
            for (int i = 0; i < levels; i++) e = Expression.Multiply(Expression.Add(e, e), Expression.Constant(0));
            return e;
        }

        Assert.AreEqual(0, ((ConstantExpression)new ExpressionOptimiser().Optimize(Build(12))).Value);

        var watch = Stopwatch.StartNew();
        Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, Assert.ThrowsExactly<ExpressionResourceLimitException>(
            () => new ExpressionOptimiser(ExpressionResourceLimits.Unlimited with { MaxStructuralVisits = 100_000 }).Optimize(Build(40))).Kind);
        watch.Stop();
        Assert.IsTrue(watch.Elapsed < TimeSpan.FromSeconds(10), watch.Elapsed.ToString());
    }

    /// <summary>
    /// An optimisation started inside a running operation joins it: it can only tighten the limits, its refusal propagates
    /// unchanged as the outer operation's, and the enclosing state is restored.
    /// </summary>
    [TestMethod]
    public void NestedOptimisation_JoinsTheRunningOperation()
    {
        Expression chain = CallChain(100);
        var tight = ExpressionResourceLimits.Default with { MaxStructuralVisits = 50 };
        ExpressionResourceLimitException refusal = Assert.ThrowsExactly<ExpressionResourceLimitException>(
            () => ExpressionResourceBudget.Run(tight, null, null, null, chain, static c => new ExpressionOptimiser(ExpressionResourceLimits.Unlimited).Optimize(c)));
        Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, refusal.Kind);
        Assert.AreEqual(50, refusal.Limit);
        Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);

        Expression result = ExpressionResourceBudget.Run(ExpressionResourceLimits.Default, null, null, null, chain, static c =>
        {
            Expression inner = new ExpressionOptimiser().Optimize(c);
            Assert.AreEqual(1, ExpressionResourceBudget.OperationDepth);
            return inner;
        });
        Assert.AreSame(chain, result);
    }

    /// <summary>A null argument and a null resource limit are refused with <see cref="ArgumentNullException"/>.</summary>
    [TestMethod]
    public void NullArguments_AreRefused()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new ExpressionOptimiser().Optimize(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ExpressionOptimiser(null!));
        Assert.AreSame(ExpressionResourceLimits.Default, new ExpressionOptimiser().ResourceLimits);
    }
}
