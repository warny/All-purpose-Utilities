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
/// S5 P18 Architecture C (stages C1 and C2): the rules admitted outside the effect gate under a non-default policy never trust nor
/// read a custom reflection object, count the behavior analysis they run in the operation's structural budget (a refusal exactly
/// one visit below the measured cost), stay bounded on the pathological nesting of products by zero, and end a hostile nesting
/// with a controlled stack refusal that leaves the thread usable.
/// </summary>
[TestClass]
public class ExpressionSimplifierPolicyRulesSecurityTests
{
    /// <summary>Upper bound of the worker threads.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <summary>The small worker stack of the stack-guard tests.</summary>
    private const int SmallStack = 256 * 1024;

    /// <summary>A depth far beyond any stack measured in R0.</summary>
    private const int HostileDepth = 100_000;

    /// <summary>The most permissive policy, under which the audited rules drop whatever the analysis allows.</summary>
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

    /// <summary>Builds <c>((Opaque(p) * 0) * 0) … * 0</c> iteratively (no recursion, whatever the depth).</summary>
    /// <param name="depth">The number of products.</param>
    /// <returns>The nesting.</returns>
    private static Expression ZeroChain(int depth)
    {
        Expression tree = Expression.Call(OpaqueMethod, P);
        for (int level = 0; level < depth; level++) tree = Expression.Multiply(tree, Expression.Constant(0.0));
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
    /// A call through an impersonation of <see cref="Math.Sin(double)"/> is unknown code for the rules admitted outside the gate
    /// too: under the most permissive policy no identity drops it (<c>t * 0</c>, <c>0 * t</c>, <c>0 / t</c>, <c>t ^ 0</c>,
    /// <c>0 ^ t</c>), the identities that keep it (<c>t + 0</c>, <c>t * 1</c>) keep it once, and the interpreted result evaluates it
    /// as often as the source. The simplification reads no member of the impersonation that the historical admission does not
    /// already read on the same tree. Those reads (<c>ContainsGenericParameters</c>, <c>Attributes</c>, <c>ReturnType</c>, and
    /// <c>GetHashCode</c>/<c>Equals</c> of the framework's parameter cache) are the framework's validation when the preparation
    /// rebuilds the call node: residual L-1 of the A-10 note, which is why the members cannot simply be armed to throw here. No
    /// engine decision reads them (covered by <c>ExpressionReflectionProvenanceTests</c>).
    /// </summary>
    [TestMethod]
    public void ImpersonatingObjects_AreNeverDroppedNorRead()
    {
        var sin = new ImpersonatingMethod(typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!);
        Expression fake = Expression.Call(sin, P);
        Expression[] sources =
        [
            Expression.Multiply(fake, Expression.Constant(0.0)),
            Expression.Multiply(Expression.Constant(0.0), fake),
            Expression.Divide(Expression.Constant(0.0), fake),
            Expression.Power(fake, Expression.Constant(0.0)),
            Expression.Power(Expression.Constant(0.0), fake),
            Expression.Add(fake, Expression.Constant(0.0)),
            Expression.Multiply(fake, Expression.Constant(1.0)),
        ];

        for (int index = 0; index < sources.Length; index++)
        {
            Expression source = sources[index];
            sin.Trap.Reset();
            new ExpressionSimplifier().Simplify(source);
            string[] historical = [.. sin.Trap.Calls.Distinct()];
            foreach (ExpressionSimplifier simplifier in Simplifiers(ExpressionResourceLimits.Default, Permissive))
            {
                sin.Trap.Reset();
                Expression simplified = simplifier.Simplify(source);
                string[] read = [.. sin.Trap.Calls.Distinct()];

                // No ToString of the trees here: it would read the impersonating method's metadata.
                string context = $"{simplifier.GetType().Name} source #{index}: {string.Join(", ", read)}";
                Assert.IsTrue(read.All(historical.Contains), $"{context} against {string.Join(", ", historical)}");
                Assert.AreEqual(1, Occurrences(simplified, sin), context);

                Delegate function = Expression.Lambda(simplified, P).Compile(preferInterpretation: true);
                sin.Trap.Reset();
                function.DynamicInvoke(0.5);
                Assert.AreEqual(1, sin.Trap.Evaluations, simplifier.GetType().Name);
            }
        }
    }

    /// <summary>
    /// The flag of the rules admitted outside the gate is per thread: user code reached during a simplification (here the members
    /// of an impersonating method, read by the framework and by the engine walks) may run another simplification, of another
    /// policy, on the same thread. Every combination of the default and the most permissive policy, outer and inner, gives the
    /// nested operation the result its instance gives alone (<c>clock * 0</c> kept by the default, dropped by the permissive
    /// policy), finds the flag unchanged after it, and leaves the outer result and the cleared flag as without the nesting. Some of
    /// those nested operations start while an audited rule runs, the flag set: the nested instance clears it for its own nodes.
    /// </summary>
    [TestMethod]
    public void UserCode_RunningAnotherPolicy_KeepsEachAdmission()
    {
        var sin = new ImpersonatingMethod(typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!);
        Expression fake = Expression.Call(sin, P);
        Expression[] sources =
        [
            Expression.Multiply(fake, Expression.Constant(0.0)),
            Expression.Divide(Expression.Constant(0.0), fake),
            Expression.Power(fake, Expression.Constant(0.0)),
            Expression.Add(fake, Expression.Constant(0.0)),
            Expression.Multiply(fake, Expression.Constant(1.0)),
        ];
        Expression clock = Expression.Convert(Expression.Call(typeof(System.Diagnostics.Stopwatch).GetMethod(nameof(System.Diagnostics.Stopwatch.GetTimestamp))!), typeof(double));
        Expression innerSource = Expression.Multiply(clock, Expression.Constant(0.0));
        ExpressionSimplifierOptions[] policies = [ExpressionSimplifierOptions.Default, Permissive];
        Assert.AreNotEqual(
            ExpressionTraceFormatter.Format(new ExpressionSimplifier().Simplify(innerSource)),
            ExpressionTraceFormatter.Format(new ExpressionSimplifier { Options = Permissive }.Simplify(innerSource)),
            "the inner tree tells the two admissions apart");

        int setFlagHits = 0;
        try
        {
            foreach (ExpressionSimplifierOptions outerOptions in policies)
            {
                foreach (ExpressionSimplifierOptions innerOptions in policies)
                {
                    var inner = new ExpressionSimplifier { Options = innerOptions };
                    string innerAlone = ExpressionTraceFormatter.Format(inner.Simplify(innerSource));
                    for (int index = 0; index < sources.Length; index++)
                    {
                        string context = $"outer {outerOptions}, inner {innerOptions}, source #{index}";
                        sin.Trap.Observer = null;
                        int aloneCount = Occurrences(new ExpressionSimplifier { Options = outerOptions }.Simplify(sources[index]), sin);

                        var nested = new System.Collections.Generic.List<(bool Before, bool After, string Result)>();
                        bool nesting = false;
                        sin.Trap.Observer = _ =>
                        {
                            if (nesting) return;
                            nesting = true;
                            try
                            {
                                bool before = ExpressionTransformer.RuleRunsOutsideGate;
                                string result = ExpressionTraceFormatter.Format(inner.Simplify(innerSource));
                                nested.Add((before, ExpressionTransformer.RuleRunsOutsideGate, result));
                            }
                            finally
                            {
                                nesting = false;
                            }
                        };

                        Expression simplified = new ExpressionSimplifier { Options = outerOptions }.Simplify(sources[index]);
                        sin.Trap.Observer = null;
                        Assert.AreEqual(aloneCount, Occurrences(simplified, sin), context);
                        Assert.IsTrue(nested.Count > 0, context);
                        Assert.IsTrue(nested.All(n => n.Result == innerAlone), $"{context}: {innerAlone}");
                        Assert.IsTrue(nested.All(n => n.Before == n.After), context);
                        Assert.IsFalse(ExpressionTransformer.RuleRunsOutsideGate, context);
                        setFlagHits += nested.Count(n => n.Before);
                    }
                }
            }
        }
        finally
        {
            sin.Trap.Observer = null;
        }

        // The members are read by audited rules too (the type of the operand), so the nesting is exercised with the flag set.
        Assert.IsTrue(setFlagHits > 0, "a nested operation started while an audited rule ran");
    }

    /// <summary>
    /// The behavior analysis an audited rule runs is counted in the operation: under a non-default policy <c>F() * 0</c> (with an
    /// unknown <c>F</c>, so the drop is refused after the analysis) costs more structural visits than under the default, and the
    /// simplification is refused exactly one structural or transformation visit below its measured cost, for the exact and a
    /// derived simplifier.
    /// </summary>
    [TestMethod]
    public void AuditedRuleAnalysis_IsCountedInTheBudget()
    {
        Expression tree = Expression.Multiply(CallChain(20), Expression.Constant(0.0));
        for (int kind = 0; kind < 2; kind++)
        {
            (long defaultTransform, long defaultStructural) = Measure(Simplifiers(ExpressionResourceLimits.Unlimited, ExpressionSimplifierOptions.Default)[kind], tree);
            (long transform, long structural) = Measure(Simplifiers(ExpressionResourceLimits.Unlimited, Permissive)[kind], tree);
            Assert.AreEqual(defaultTransform, transform, "the refused drop transforms nothing more");
            Assert.IsTrue(structural > defaultStructural, $"{structural} > {defaultStructural}: the analysis is counted");

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

    /// <summary>
    /// The pathological nesting <c>((F() * 0) * 0) … * 0</c> with an unknown <c>F</c> makes every level analyze the operand below
    /// it: the structural work grows quadratically with the depth but stays bounded by the budget, the default limits
    /// accept the 400-level nesting (well below their structural budget), and the transformation work is the default's.
    /// </summary>
    [TestMethod]
    public void NestedProductsByZero_StayBounded()
    {
        long previous = 0;
        foreach (int depth in new[] { 100, 200, 400 })
        {
            Expression tree = ZeroChain(depth);
            (long defaultTransform, _) = Measure(new ExpressionSimplifier(ExpressionResourceLimits.Unlimited), tree);
            (long transform, long structural) = Measure(new ExpressionSimplifier(ExpressionResourceLimits.Unlimited) { Options = Permissive }, tree);
            Assert.AreEqual(defaultTransform, transform, $"depth {depth}");
            Assert.IsTrue(structural < ExpressionResourceLimits.Default.MaxStructuralVisits / 10, $"depth {depth}: {structural}");
            if (previous > 0) Assert.IsTrue(structural > 2.5 * previous, $"depth {depth}: {structural} against {previous} (quadratic)");
            previous = structural;
            new ExpressionSimplifier { Options = Permissive }.Simplify(tree);
        }
    }

    /// <summary>
    /// Without limits, only the stack guard stops a hostile nesting of products by zero over an unknown call: under a non-default
    /// policy the simplification ends with a controlled <see cref="ExpressionResourceLimitKind.Stack"/> refusal on a 256 KiB
    /// stack, closes its operation, clears the admission flag, and the thread can simplify again.
    /// </summary>
    [TestMethod]
    public void HostileNesting_EndsWithAStackRefusal()
    {
        Expression deep = ZeroChain(HostileDepth);
        Expression small = Expression.Multiply(Expression.Call(OpaqueMethod, P), Expression.Constant(1.0));
        foreach (ExpressionSimplifier simplifier in Simplifiers(ExpressionResourceLimits.Unlimited, Permissive))
        {
            RunOnThread(() =>
            {
                ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => simplifier.Simplify(deep));
                Assert.AreEqual(ExpressionResourceLimitKind.Stack, exception.Kind);
                Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
                Assert.IsFalse(ExpressionTransformer.RuleRunsOutsideGate);
                Assert.AreEqual(ExpressionType.Call, simplifier.Simplify(small).NodeType);
            }, SmallStack, Timeout);
        }
    }

    /// <summary>
    /// The analysis an audited rule runs joins the simplification under the instance's own limits: an unlimited simplifier drops
    /// an operand larger than the default node limit (an array of 60,000 constants, allocated, whose length is read) without being
    /// refused, while a simplifier with the default limits refuses the input before any rule runs.
    /// </summary>
    [TestMethod]
    public void AuditedRuleAnalysis_UsesTheInstanceLimits()
    {
        Expression[] elements = Enumerable.Range(0, 60_000).Select(k => (Expression)Expression.Constant((double)k)).ToArray();
        Expression operand = Expression.Convert(Expression.ArrayLength(Expression.NewArrayInit(typeof(double), elements)), typeof(double));
        Expression source = Expression.Multiply(operand, Expression.Constant(0.0));
        var options = new ExpressionSimplifierOptions { AggressiveSimplification = true, ExceptionPreservation = ExpressionExceptionPreservation.None };

        Expression result = new ExpressionSimplifier(ExpressionResourceLimits.Unlimited) { Options = options }.Simplify(source);
        Assert.AreEqual(0.0, (result as ConstantExpression)?.Value);

        ExpressionResourceLimitException refused = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpressionSimplifier { Options = options }.Simplify(source));
        Assert.AreEqual(ExpressionResourceLimitKind.Nodes, refused.Kind);
    }

    /// <summary>
    /// A resource refusal that leaves an audited rule with an exception clears the outside-gate admission flag: for every structural
    /// budget below the cost of <c>{ v = x; v * 2 } * 0</c> under a relaxed policy, the simplification either completes or is
    /// refused, and the flag is cleared afterwards; a small budget refuses it.
    /// </summary>
    [TestMethod]
    public void ResourceRefusal_ClearsTheOutsideGateFlag()
    {
        ParameterExpression local = Expression.Variable(typeof(double), "v");
        Expression block = Expression.Block(typeof(double), [local], Expression.Assign(local, P), Expression.Multiply(local, Expression.Constant(2.0)));
        Expression dropping = Expression.Multiply(block, Expression.Constant(0.0));
        var options = new ExpressionSimplifierOptions { AggressiveSimplification = true };
        bool refused = false;
        for (long budget = 1; budget < 200 && !refused; budget++)
        {
            try
            {
                new ExpressionSimplifier(ExpressionResourceLimits.Default with { MaxStructuralVisits = budget }) { Options = options }.Simplify(dropping);
            }
            catch (ExpressionResourceLimitException)
            {
                refused = true;
            }

            Assert.IsFalse(ExpressionTransformer.RuleRunsOutsideGate, $"budget {budget}");
            Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth, $"budget {budget}");
        }

        Assert.IsTrue(refused, "a small structural budget refuses the simplification");
    }

    /// <summary>
    /// The internal analysis entry the audited rules use joins the running operation: it opens no nested operation and does not
    /// validate its input again (a tree above the running node limit is analyzed, where the public entry refuses it), every analyzed
    /// node counts one structural visit (refused exactly one visit below), and a shared DAG, walked once per path, ends with a
    /// structural refusal instead of an unbounded walk. Without a running operation it is the public entry, validation included.
    /// </summary>
    [TestMethod]
    public void InternalAnalysis_JoinsTheRunningOperation()
    {
        Expression tree = CallChain(40);
        long nodes = CountNodes(tree);
        ExpressionResourceLimits tight = ExpressionResourceLimits.Default with { MaxNodes = 10 };

        (long visits, int depth) = ExpressionResourceBudget.Run(tight, null, null, null, tree, static root =>
        {
            long before = ExpressionResourceBudget.StructuralVisits;
            int depthBefore = ExpressionResourceBudget.OperationDepth;
            ExpressionBehaviorAnalysis.AnalyzeWithinOperation(root, ExpressionResourceLimits.Unlimited);
            Assert.AreEqual(depthBefore, ExpressionResourceBudget.OperationDepth);
            return (ExpressionResourceBudget.StructuralVisits - before, depthBefore);
        });
        Assert.AreEqual(nodes, visits, "one structural visit per analyzed node");
        Assert.AreEqual(1, depth);

        Assert.AreEqual(ExpressionResourceLimitKind.Nodes, Assert.ThrowsExactly<ExpressionResourceLimitException>(
            () => ExpressionResourceBudget.Run(tight, null, null, null, tree, static root => ExpressionBehaviorAnalysis.Analyze(root, ExpressionResourceLimits.Unlimited))).Kind);
        Assert.AreEqual(ExpressionResourceLimitKind.Nodes, Assert.ThrowsExactly<ExpressionResourceLimitException>(
            () => ExpressionBehaviorAnalysis.AnalyzeWithinOperation(tree, tight)).Kind);

        ExpressionResourceLimits exact = ExpressionResourceLimits.Unlimited with { MaxStructuralVisits = nodes };
        ExpressionResourceBudget.Run(exact, null, null, null, tree, static root => ExpressionBehaviorAnalysis.AnalyzeWithinOperation(root, ExpressionResourceLimits.Unlimited));
        Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, Assert.ThrowsExactly<ExpressionResourceLimitException>(
            () => ExpressionResourceBudget.Run(exact with { MaxStructuralVisits = nodes - 1 }, null, null, null, tree,
                static root => ExpressionBehaviorAnalysis.AnalyzeWithinOperation(root, ExpressionResourceLimits.Unlimited))).Kind);

        Expression dag = P;
        for (int level = 0; level < 40; level++) dag = Expression.Add(dag, dag);
        ExpressionResourceLimits bounded = ExpressionResourceLimits.Unlimited with { MaxStructuralVisits = 100_000 };
        Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, Assert.ThrowsExactly<ExpressionResourceLimitException>(
            () => ExpressionResourceBudget.Run(bounded, null, null, null, dag,
                static root => ExpressionBehaviorAnalysis.AnalyzeWithinOperation(root, ExpressionResourceLimits.Unlimited))).Kind);
        Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
    }

    /// <summary>Counts the nodes of a tree without sharing (each node once per path).</summary>
    /// <param name="tree">The tree.</param>
    /// <returns>The number of nodes.</returns>
    private static long CountNodes(Expression tree)
    {
        var counter = new NodeCounter();
        counter.Visit(tree);
        return counter.Count;
    }

    /// <summary>Counts every node it visits.</summary>
    private sealed class NodeCounter : ExpressionVisitor
    {
        /// <summary>Gets the number of nodes visited.</summary>
        public long Count { get; private set; }

        /// <inheritdoc/>
        public override Expression? Visit(Expression? node)
        {
            if (node is not null) Count++;
            return base.Visit(node);
        }
    }

    /// <summary>A signature matching additions that counts its calls: <see cref="ExpressionSignatureAttribute.Match"/> is user code.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    private sealed class CountingAddSignatureAttribute : ExpressionSignatureAttribute
    {
        /// <summary>The number of calls of <see cref="Match"/> on the current thread.</summary>
        [ThreadStatic]
        public static int Calls;

        /// <summary>Initializes the signature for <see cref="ExpressionType.Add"/>.</summary>
        public CountingAddSignatureAttribute()
            : base(ExpressionType.Add)
        {
        }

        /// <inheritdoc/>
        public override bool Match(Expression e)
        {
            Calls++;
            return e.NodeType == ExpressionType.Add;
        }
    }

    /// <summary>A derived simplifier with two unaudited addition rules whose signatures count their matches; both decline.</summary>
    private sealed class MatchCountingSimplifier : ExpressionSimplifier
    {
        /// <summary>Declines.</summary>
        /// <param name="e">The addition.</param>
        /// <returns><see langword="null"/>.</returns>
        [CountingAddSignature]
        public Expression? FirstUnaudited(BinaryExpression e) => null;

        /// <summary>Declines.</summary>
        /// <param name="e">The addition.</param>
        /// <returns><see langword="null"/>.</returns>
        [CountingAddSignature]
        public Expression? SecondUnaudited(BinaryExpression e) => null;
    }

    /// <summary>
    /// Detailed tracing never matches a signature the untraced run skips: outside the gate (a relaxed policy on <c>F(x) + 0</c>),
    /// once the first candidate has established that only audited rules run, a later unaudited rule is refused before its
    /// <see cref="ExpressionSignatureAttribute.Match"/> is called, traced or not, so the user code of the custom signatures runs
    /// the same number of times at every level of tracing, with the same result; the detailed trace reports the rejection.
    /// </summary>
    [TestMethod]
    public void DetailedTracing_MatchesNoSkippedSignature()
    {
        Expression source = Expression.Add(Expression.Call(OpaqueMethod, P), Expression.Constant(0.0));
        var options = new ExpressionSimplifierOptions { AggressiveSimplification = true };
        string Run(ExpressionTraceDetail? detail, out int calls, out ExpressionTransformEvent[] events)
        {
            var collector = new ExpressionTraceCollector();
            CountingAddSignatureAttribute.Calls = 0;
            Expression result;
            if (detail is { } level)
            {
                using (ExpressionTransformTracing.Begin(collector, level))
                {
                    result = new MatchCountingSimplifier { Options = options }.Simplify(source);
                }
            }
            else
            {
                result = new MatchCountingSimplifier { Options = options }.Simplify(source);
            }

            calls = CountingAddSignatureAttribute.Calls;
            events = collector.GetEvents();
            return ExpressionTraceFormatter.Format(result);
        }

        string untraced = Run(null, out int untracedCalls, out _);
        string standard = Run(ExpressionTraceDetail.Standard, out int standardCalls, out _);
        string detailed = Run(ExpressionTraceDetail.Detailed, out int detailedCalls, out ExpressionTransformEvent[] events);

        Assert.AreEqual(untraced, standard);
        Assert.AreEqual(untraced, detailed);
        Assert.IsTrue(untracedCalls > 0, "the first candidate is matched");
        Assert.AreEqual(untracedCalls, standardCalls);
        Assert.AreEqual(untracedCalls, detailedCalls, "detailed tracing matched a skipped signature");
        Assert.IsTrue(events.Any(e => e.Kind == ExpressionTransformEventKind.RuleRejected && e.Rejection == ExpressionRuleRejection.EffectGate));
        Assert.IsFalse(ExpressionTransformer.RuleRunsOutsideGate);
    }
}
