using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;
using static UtilsTest.Security.Expressions.ExpressionResourceTestTrees;

namespace UtilsTest.Security.Expressions;

/// <summary>
/// S5 P18 Architecture C, stage C2b: the <c>NaN</c> fold of a non-default policy reads no member of an opaque node or of a custom
/// reflection object that the historical fold does not read, never drops them, applies the policy of its own instance in nested
/// simplifications, counts its behavior analysis in the operation's structural budget (a refusal exactly one visit below the
/// measured cost), stays bounded on nested refused folds and ends a hostile nesting with a controlled stack refusal.
/// </summary>
[TestClass]
public class ExpressionSimplifierPolicyNaNSecurityTests
{
    /// <summary>Upper bound of the worker threads.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <summary>The small worker stack of the stack-guard tests.</summary>
    private const int SmallStack = 256 * 1024;

    /// <summary>A depth far beyond any stack measured in R0.</summary>
    private const int HostileDepth = 100_000;

    /// <summary>The most permissive policy, under which the fold drops whatever the analysis allows.</summary>
    private static readonly ExpressionSimplifierOptions Permissive = new()
    {
        TrustPolicy = ExpressionTrustPolicy.TrustDeclarations,
        AggressiveSimplification = true,
        ExceptionPreservation = ExpressionExceptionPreservation.None,
    };

    /// <summary>A derived simplifier overriding nothing.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
        /// <summary>Initializes a derived simplifier with explicit limits.</summary>
        /// <param name="limits">The limits.</param>
        public DerivedSimplifier(ExpressionResourceLimits limits)
            : base(limits)
        {
        }
    }

    /// <summary>
    /// An opaque extension node of type <see cref="double"/> that records every read of its members, and can run a nested
    /// simplification or throw from its <see cref="NodeType"/> getter.
    /// </summary>
    private sealed class RecordingNode : Expression
    {
        /// <summary>Gets the members read, in order.</summary>
        public List<string> Reads { get; } = [];

        /// <summary>Gets or sets an action run (not re-entered) from each read of <see cref="NodeType"/>.</summary>
        public Action? OnNodeType { get; set; }

        /// <summary>Whether <see cref="OnNodeType"/> runs.</summary>
        private bool _running;

        /// <inheritdoc/>
        public override ExpressionType NodeType
        {
            get
            {
                Reads.Add(nameof(NodeType));
                if (OnNodeType is { } action && !_running)
                {
                    _running = true;
                    try
                    {
                        action();
                    }
                    finally
                    {
                        _running = false;
                    }
                }

                return ExpressionType.Extension;
            }
        }

        /// <inheritdoc/>
        public override Type Type
        {
            get
            {
                Reads.Add(nameof(Type));
                return typeof(double);
            }
        }

        /// <inheritdoc/>
        public override bool CanReduce
        {
            get
            {
                Reads.Add(nameof(CanReduce));
                return false;
            }
        }

        /// <inheritdoc/>
        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            Reads.Add(nameof(VisitChildren));
            return this;
        }
    }

    /// <summary>Simplifiers of one policy, exact and derived.</summary>
    /// <param name="limits">The limits.</param>
    /// <param name="options">The policy.</param>
    /// <returns>The simplifiers.</returns>
    private static ExpressionSimplifier[] Simplifiers(ExpressionResourceLimits limits, ExpressionSimplifierOptions options)
        => [new ExpressionSimplifier(limits) { Options = options }, new DerivedSimplifier(limits) { Options = options }];

    /// <summary>Measures the visits of one simplification.</summary>
    /// <param name="simplifier">The simplifier (it joins the measuring operation).</param>
    /// <param name="tree">The tree.</param>
    /// <returns>The transformation and structural visits.</returns>
    private static (long Transform, long Structural) Measure(ExpressionSimplifier simplifier, Expression tree)
        => ExpressionResourceBudget.Run(ExpressionResourceLimits.Unlimited, null, null, null, (simplifier, tree), static state =>
        {
            long transform = ExpressionResourceBudget.TransformVisits, structural = ExpressionResourceBudget.StructuralVisits;
            state.simplifier.Simplify(state.tree);
            return (ExpressionResourceBudget.TransformVisits - transform, ExpressionResourceBudget.StructuralVisits - structural);
        });

    /// <summary>The <c>NaN</c> constant of every tree.</summary>
    private static ConstantExpression NaN => Expression.Constant(double.NaN);

    /// <summary>
    /// Builds <c>NaN + (NaN * (NaN + … Opaque(p)))</c> iteratively (no recursion, whatever the depth), alternating the operators
    /// so that no chain is flattened: every level is a refused fold over the level below.
    /// </summary>
    /// <param name="depth">The number of operations.</param>
    /// <returns>The nesting.</returns>
    internal static Expression RefusedNaNChain(int depth)
    {
        Expression tree = Expression.Call(OpaqueMethod, P);
        for (int level = 0; level < depth; level++) tree = level % 2 == 0 ? Expression.Add(NaN, tree) : Expression.Multiply(NaN, tree);
        return tree;
    }

    /// <summary>Counts the occurrences of a method in a tree.</summary>
    /// <param name="tree">The tree.</param>
    /// <param name="method">The method.</param>
    /// <returns>The number of calls of <paramref name="method"/>.</returns>
    private static int Occurrences(Expression tree, MethodInfo method)
    {
        var counter = new CallCounter(method);
        counter.Visit(tree);
        return counter.Count;
    }

    /// <summary>Counts the calls of one method by reference.</summary>
    /// <param name="method">The method.</param>
    private sealed class CallCounter(MethodInfo method) : ExpressionVisitor
    {
        /// <summary>Gets the number of calls found.</summary>
        public int Count { get; private set; }

        /// <inheritdoc/>
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (ReferenceEquals(node.Method, method)) Count++;
            return base.VisitMethodCall(node);
        }
    }

    /// <summary>
    /// An opaque operand is never dropped, and the fold of the most permissive policy reads exactly the members of the opaque node
    /// that the historical fold reads, in the same order (the behavior analysis classifies it by its class before reading
    /// anything). A getter that throws ends both simplifications the same way.
    /// </summary>
    [TestMethod]
    public void OpaqueNodes_AreNeverDroppedNorReadMore()
    {
        foreach (Func<Expression, Expression> make in new Func<Expression, Expression>[] { t => Expression.Add(NaN, t), t => Expression.Multiply(t, NaN), t => Expression.Divide(NaN, Expression.Call(AbsMethod, t)) })
        {
            // The reference is the same class under the default policy: a derived simplifier reads more than the exact one
            // whatever the policy (it is excluded from the exact-type reuse paths).
            for (int kind = 0; kind < 2; kind++)
            {
                var historicalNode = new RecordingNode();
                Expression historical = Simplifiers(ExpressionResourceLimits.Default, ExpressionSimplifierOptions.Default)[kind].Simplify(make(historicalNode));
                Assert.IsNotInstanceOfType<ConstantExpression>(historical);
                ExpressionSimplifier simplifier = Simplifiers(ExpressionResourceLimits.Default, Permissive)[kind];
                var node = new RecordingNode();
                Expression simplified = simplifier.Simplify(make(node));
                Assert.IsNotInstanceOfType<ConstantExpression>(simplified, simplifier.GetType().Name);
                CollectionAssert.AreEqual(historicalNode.Reads, node.Reads, $"{simplifier.GetType().Name}: {string.Join(",", node.Reads)} against {string.Join(",", historicalNode.Reads)}");
            }
        }

        string Outcome(ExpressionSimplifier simplifier)
        {
            var node = new RecordingNode { OnNodeType = () => throw new InvalidOperationException("node type") };
            try
            {
                simplifier.Simplify(Expression.Add(NaN, node));
                return "simplified";
            }
            catch (Exception exception)
            {
                return exception.GetType().Name;
            }
        }

        string expected = Outcome(new ExpressionSimplifier());
        foreach (ExpressionSimplifier simplifier in Simplifiers(ExpressionResourceLimits.Default, Permissive))
        {
            Assert.AreEqual(expected, Outcome(simplifier), simplifier.GetType().Name);
        }
    }

    /// <summary>
    /// A call through an impersonation of <see cref="Math.Sin(double)"/> next to a <c>NaN</c> constant is unknown code for the
    /// fold too: under the most permissive policy it is kept once and evaluated as often as in the source. The fold's decision,
    /// the behavior analysis of the dropped operand, reads no member of the impersonation at all. The whole simplification reads
    /// the same members other than the node types as the same class under the default policy, in the same order: those reads are
    /// the framework's validation when a call node is rebuilt, residual L-1 of the A-10 note, and no engine decision reads them.
    /// The node type reads (<c>ReturnType</c>, the getter behind <see cref="Expression.Type"/>) are left out of the comparison:
    /// the rules a non-default policy admits outside the gate (C1/C2) rebuild more nodes on <c>fake / NaN</c> and read it more
    /// often, with or without the C2b fold.
    /// </summary>
    [TestMethod]
    public void ImpersonatingObjects_AreNeverDroppedNorReadMore()
    {
        var sin = new ImpersonatingMethod(typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!);
        Expression fake = Expression.Call(sin, P);
        Expression[] sources =
        [
            Expression.Add(NaN, fake),
            Expression.Subtract(fake, NaN),
            Expression.Multiply(NaN, fake),
            Expression.Divide(fake, NaN),
            Expression.Add(NaN, Expression.Multiply(Expression.Constant(2.0), fake)),
        ];

        foreach (Expression operand in new[] { fake, Expression.Multiply(Expression.Constant(2.0), fake) })
        {
            sin.Trap.Reset();
            ExpressionBehavior behavior = ExpressionBehaviorAnalysis.Analyze(operand, ExpressionResourceLimits.Default);
            Assert.IsFalse(ExpressionBehaviorPermissions.CanDiscard(behavior, Permissive));
            Assert.IsTrue(sin.Trap.Calls.IsEmpty, string.Join(", ", sin.Trap.Calls));
        }

        static string[] Untyped(IEnumerable<string> reads) => [.. reads.Where(static read => read != "ReturnType")];

        for (int index = 0; index < sources.Length; index++)
        {
            for (int kind = 0; kind < 2; kind++)
            {
                // The reference is the same class under the default policy (see OpaqueNodes_AreNeverDroppedNorReadMore).
                sin.Trap.Reset();
                Simplifiers(ExpressionResourceLimits.Default, ExpressionSimplifierOptions.Default)[kind].Simplify(sources[index]);
                string[] historical = Untyped(sin.Trap.Calls);
                ExpressionSimplifier simplifier = Simplifiers(ExpressionResourceLimits.Default, Permissive)[kind];
                sin.Trap.Reset();
                Expression simplified = simplifier.Simplify(sources[index]);
                string[] read = Untyped(sin.Trap.Calls);

                // No ToString of the trees here: it would read the impersonating method's metadata.
                string context = $"{simplifier.GetType().Name} source #{index}";
                CollectionAssert.AreEqual(historical, read, $"{context}: {string.Join(", ", read)} against {string.Join(", ", historical)}");
                Assert.AreEqual(1, Occurrences(simplified, sin), context);

                Delegate function = Expression.Lambda(simplified, P).Compile(preferInterpretation: true);
                sin.Trap.Reset();
                function.DynamicInvoke(0.5);
                Assert.AreEqual(1, sin.Trap.Evaluations, context);
            }
        }
    }

    /// <summary>
    /// User code reached during a simplification (the <see cref="Expression.NodeType"/> of an opaque node, the members of an
    /// impersonating method) may run another simplification, of another policy, on the same thread. Every combination of the
    /// default and the most permissive policy, outer and inner, gives the nested operation the result its instance gives alone
    /// (<c>NaN + clock</c> kept by the default, folded by the permissive policy), and leaves the outer result, the operation depth,
    /// and the outside-gate flag as without the nesting.
    /// </summary>
    [TestMethod]
    public void UserCode_RunningAnotherPolicy_KeepsEachFold()
    {
        Expression clock = Expression.Convert(Expression.Call(typeof(System.Diagnostics.Stopwatch).GetMethod(nameof(System.Diagnostics.Stopwatch.GetTimestamp))!), typeof(double));
        Expression innerSource = Expression.Add(NaN, clock);
        ExpressionSimplifierOptions[] policies = [ExpressionSimplifierOptions.Default, Permissive];
        Assert.AreNotEqual(
            ExpressionTraceFormatter.Format(new ExpressionSimplifier().Simplify(innerSource)),
            ExpressionTraceFormatter.Format(new ExpressionSimplifier { Options = Permissive }.Simplify(innerSource)),
            "the inner tree tells the two policies apart");

        var sin = new ImpersonatingMethod(typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!);
        try
        {
            foreach (ExpressionSimplifierOptions outerOptions in policies)
            {
                foreach (ExpressionSimplifierOptions innerOptions in policies)
                {
                    string context = $"outer {outerOptions}, inner {innerOptions}";
                    var inner = new ExpressionSimplifier { Options = innerOptions };
                    string innerAlone = ExpressionTraceFormatter.Format(inner.Simplify(innerSource));
                    var nested = new List<(bool Before, bool After, int DepthBefore, int DepthAfter, string Result)>();
                    void Nest()
                    {
                        bool before = ExpressionTransformer.RuleRunsOutsideGate;
                        int depth = ExpressionResourceBudget.OperationDepth;
                        string result = ExpressionTraceFormatter.Format(inner.Simplify(innerSource));
                        nested.Add((before, ExpressionTransformer.RuleRunsOutsideGate, depth, ExpressionResourceBudget.OperationDepth, result));
                    }

                    // From an opaque node.
                    var node = new RecordingNode();
                    Expression nodeSource = Expression.Add(NaN, node);
                    string nodeAlone = ExpressionTraceFormatter.Format(new ExpressionSimplifier { Options = outerOptions }.Simplify(nodeSource));
                    node.OnNodeType = Nest;
                    Assert.AreEqual(nodeAlone, ExpressionTraceFormatter.Format(new ExpressionSimplifier { Options = outerOptions }.Simplify(nodeSource)), context);

                    // From an impersonating method.
                    Expression fakeSource = Expression.Multiply(NaN, Expression.Call(sin, P));
                    sin.Trap.Observer = null;
                    int aloneCount = Occurrences(new ExpressionSimplifier { Options = outerOptions }.Simplify(fakeSource), sin);
                    bool nesting = false;
                    sin.Trap.Observer = _ =>
                    {
                        if (nesting) return;
                        nesting = true;
                        try
                        {
                            Nest();
                        }
                        finally
                        {
                            nesting = false;
                        }
                    };
                    Expression simplified = new ExpressionSimplifier { Options = outerOptions }.Simplify(fakeSource);
                    sin.Trap.Observer = null;
                    Assert.AreEqual(aloneCount, Occurrences(simplified, sin), context);

                    Assert.IsTrue(nested.Count > 0, context);
                    Assert.IsTrue(nested.All(n => n.Result == innerAlone), $"{context}: {innerAlone}");
                    Assert.IsTrue(nested.All(n => n.Before == n.After && n.DepthBefore == n.DepthAfter), context);
                    Assert.IsFalse(ExpressionTransformer.RuleRunsOutsideGate, context);
                    Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth, context);
                }
            }
        }
        finally
        {
            sin.Trap.Observer = null;
        }
    }

    /// <summary>
    /// The behavior analysis of the fold is counted in the operation: under a non-default policy <c>NaN + F(…)</c> (an unknown
    /// <c>F</c>: the drop is refused after the analysis) costs more structural visits than under the default, and as many
    /// transformation visits; an admitted drop (<c>NaN + Abs(…)</c>) is counted as well. Each simplification is refused exactly one
    /// structural or transformation visit below its measured cost, for the exact and a derived simplifier.
    /// </summary>
    [TestMethod]
    public void FoldAnalysis_IsCountedInTheBudget()
    {
        Expression refused = Expression.Add(NaN, CallChain(20));
        Expression admitted = Expression.Add(NaN, Chain(20, static e => Expression.Call(AbsMethod, e)));
        foreach (Expression tree in new[] { refused, admitted })
        {
            for (int kind = 0; kind < 2; kind++)
            {
                (long defaultTransform, long defaultStructural) = Measure(Simplifiers(ExpressionResourceLimits.Unlimited, ExpressionSimplifierOptions.Default)[kind], tree);
                (long transform, long structural) = Measure(Simplifiers(ExpressionResourceLimits.Unlimited, Permissive)[kind], tree);
                if (ReferenceEquals(tree, refused))
                {
                    Assert.AreEqual(defaultTransform, transform, "the refused drop transforms nothing more");
                    Assert.IsTrue(structural > defaultStructural, $"{structural} > {defaultStructural}: the analysis is counted");
                }

                ExpressionResourceLimits exact = ExpressionResourceLimits.Default with { MaxTransformVisits = transform, MaxStructuralVisits = structural };
                Simplifiers(exact, Permissive)[kind].Simplify(tree);
                Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, Assert.ThrowsExactly<ExpressionResourceLimitException>(
                    () => Simplifiers(exact with { MaxStructuralVisits = structural - 1 }, Permissive)[kind].Simplify(tree)).Kind);
                Assert.AreEqual(ExpressionResourceLimitKind.Work, Assert.ThrowsExactly<ExpressionResourceLimitException>(
                    () => Simplifiers(exact with { MaxTransformVisits = transform - 1 }, Permissive)[kind].Simplify(tree)).Kind);
                Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
                Assert.IsFalse(ExpressionTransformer.RuleRunsOutsideGate);
            }
        }
    }

    /// <summary>
    /// The nestings of refused folds <c>NaN + (NaN * (NaN + … F()))</c> and <c>NaN + S * (NaN + S * (… F()))</c>, with an unknown
    /// <c>F</c> and an admissible <c>S</c>, analyze only the bottom level: every other level directly evaluates a <c>NaN</c> node the
    /// policy left unfolded, and is refused without analysis. The structural work of the most permissive policy exceeds the
    /// default's by the same constant at every depth (without the shortcut the excess grew quadratically), the transformation work
    /// is the default's, and the default limits accept the 250-level nestings (the product nesting has two nodes per level).
    /// </summary>
    [TestMethod]
    public void NestedRefusedFolds_StayBounded()
    {
        foreach (Func<int, Expression> build in new Func<int, Expression>[] { RefusedNaNChain, RefusedNaNProductChain })
        {
            long? excess = null;
            foreach (int depth in new[] { 64, 128, 250 })
            {
                Expression tree = build(depth);
                (long defaultTransform, long defaultStructural) = Measure(new ExpressionSimplifier(ExpressionResourceLimits.Unlimited), tree);
                (long transform, long structural) = Measure(new ExpressionSimplifier(ExpressionResourceLimits.Unlimited) { Options = Permissive }, tree);
                Assert.AreEqual(defaultTransform, transform, $"depth {depth}");
                Assert.IsTrue(structural > defaultStructural, $"depth {depth}: the bottom analysis is counted");
                excess ??= structural - defaultStructural;
                Assert.AreEqual(excess, structural - defaultStructural, $"depth {depth}: {structural} against {defaultStructural}");
                new ExpressionSimplifier { Options = Permissive }.Simplify(tree);
            }
        }
    }

    /// <summary>
    /// Builds <c>NaN + S * (NaN + S * (… Opaque(p)))</c> iteratively, with <c>S = Abs(p)</c> shared by every level (admissible,
    /// but each level's operand holds the refused level below).
    /// </summary>
    /// <param name="depth">The number of levels.</param>
    /// <returns>The nesting.</returns>
    internal static Expression RefusedNaNProductChain(int depth)
    {
        Expression shared = Expression.Call(AbsMethod, P);
        Expression tree = Expression.Call(OpaqueMethod, P);
        for (int level = 0; level < depth; level++) tree = Expression.Add(NaN, Expression.Multiply(shared, tree));
        return tree;
    }

    /// <summary>
    /// Without limits, only the stack guard stops a hostile nesting of refused folds: under a non-default policy the
    /// simplification ends with a controlled <see cref="ExpressionResourceLimitKind.Stack"/> refusal on a 256 KiB stack, closes its
    /// operation, leaves the outside-gate flag cleared, and the thread can simplify again.
    /// </summary>
    [TestMethod]
    public void HostileNesting_EndsWithAStackRefusal()
    {
        Expression deep = RefusedNaNChain(HostileDepth);
        Expression small = Expression.Add(NaN, Expression.Call(OpaqueMethod, P));
        foreach (ExpressionSimplifier simplifier in Simplifiers(ExpressionResourceLimits.Unlimited, Permissive))
        {
            RunOnThread(() =>
            {
                ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => simplifier.Simplify(deep));
                Assert.AreEqual(ExpressionResourceLimitKind.Stack, exception.Kind);
                Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
                Assert.IsFalse(ExpressionTransformer.RuleRunsOutsideGate);
                Assert.AreEqual(ExpressionType.Add, simplifier.Simplify(small).NodeType);
            }, SmallStack, Timeout);
        }
    }
}
