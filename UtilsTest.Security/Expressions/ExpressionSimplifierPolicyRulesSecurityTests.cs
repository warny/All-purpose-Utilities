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
    /// The pathological nesting <c>((F() * 0) * 0) … * 0</c> with an unknown <c>F</c> makes every level analyze (and validate) the
    /// operand below it: the structural work grows quadratically with the depth but stays bounded by the budget, the default limits
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
}
