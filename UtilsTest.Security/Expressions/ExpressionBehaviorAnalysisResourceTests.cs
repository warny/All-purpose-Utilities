using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using static UtilsTest.Security.Expressions.ExpressionResourceTestTrees;

namespace UtilsTest.Security.Expressions;

/// <summary>
/// S5 P18 Architecture A: <see cref="ExpressionBehaviorAnalysis"/> runs under the shared resource session. Hostile nestings
/// of every family it recurses into end with a controlled refusal on a small stack and leave the thread usable; the default
/// limits refuse them before any recursion; a shared DAG cannot evade the validation or the visit budget; the visit budget
/// refuses exactly one visit below the measured cost; and an analysis started inside a running operation joins it.
/// </summary>
[TestClass]
public class ExpressionBehaviorAnalysisResourceTests
{
    /// <summary>Upper bound of the worker threads.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <summary>The small worker stack of the stack-guard tests.</summary>
    private const int SmallStack = 256 * 1024;

    /// <summary>A depth far beyond any stack measured in R0.</summary>
    private const int HostileDepth = 100_000;

    /// <summary>Limits disabling validation and work budgets: only the stack guard can stop the recursion.</summary>
    private static readonly ExpressionResourceLimits NoLimits = ExpressionResourceLimits.Unlimited;

    /// <summary>A block variable, which makes the closures of <see cref="EscapingLevel"/> capture-checked.</summary>
    private static readonly ParameterExpression V = Expression.Variable(typeof(double), "v");

    /// <summary>Wraps an expression in a lambda invoked in place: <c>(y =&gt; e + y)(p)</c>.</summary>
    /// <param name="e">The wrapped expression.</param>
    /// <returns>The wrapper.</returns>
    private static Expression InvokedLevel(Expression e)
    {
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        return Expression.Invoke(Expression.Lambda<Func<double, double>>(Expression.Add(e, y), y), P);
    }

    /// <summary>Wraps an expression in a block declaring a variable: <c>{ double w; e }</c>.</summary>
    /// <param name="e">The wrapped expression.</param>
    /// <returns>The wrapper.</returns>
    private static Expression BlockLevel(Expression e) => Expression.Block([Expression.Variable(typeof(double), "w")], e);

    /// <summary>Wraps an expression in the test of a conditional: <c>e &gt; 0 ? p : p</c>.</summary>
    /// <param name="e">The wrapped expression.</param>
    /// <returns>The wrapper.</returns>
    /// <remarks>The nesting goes through the test: the framework factory makes a nesting through the branches quadratic to build.</remarks>
    private static Expression ConditionalLevel(Expression e) => Expression.Condition(Expression.GreaterThan(e, Expression.Constant(0.0)), P, P);

    /// <summary>
    /// Wraps an expression in a closure passed as a value to a delegate call: <c>(() =&gt; e + v).Invoke()</c>. The closure
    /// escapes (it is the receiver of an unknown call) and captures the block variable <see cref="V"/>, so its body is walked.
    /// </summary>
    /// <param name="e">The wrapped expression.</param>
    /// <returns>The wrapper.</returns>
    private static Expression EscapingLevel(Expression e)
    {
        LambdaExpression closure = Expression.Lambda<Func<double>>(Expression.Add(e, V));
        return Expression.Call(closure, typeof(Func<double>).GetMethod(nameof(Func<double>.Invoke))!);
    }

    /// <summary>The hostile nestings of every family the analysis recurses into, built once for the tests of this class.</summary>
    private static readonly Lazy<(string Family, Expression Deep)[]> Nestings = new(() =>
    [
        ("negate", NegateChain(HostileDepth)),
        ("call", Chain(HostileDepth, static e => Expression.Call(AbsMethod, e))),
        ("invoked lambda", Chain(HostileDepth, InvokedLevel)),
        ("block", Chain(HostileDepth, BlockLevel)),
        ("conditional", Chain(HostileDepth, ConditionalLevel)),
        ("escaping closure", Expression.Block([V], Chain(HostileDepth, EscapingLevel))),
    ]);

    /// <summary>The hostile nestings of every family the analysis recurses into.</summary>
    /// <returns>The nestings, by family name.</returns>
    private static IEnumerable<(string Family, Expression Deep)> HostileNestings() => Nestings.Value;

    /// <summary>
    /// Without limits, only the stack guard stops a hostile nesting: the analysis (of an expression and of an invocation)
    /// ends with a controlled <see cref="ExpressionResourceLimitKind.Stack"/> refusal, closes its operation, and the thread can
    /// analyze again.
    /// </summary>
    [TestMethod]
    public void HostileNestings_EndWithAStackRefusal()
    {
        foreach ((string family, Expression deep) in HostileNestings())
        {
            LambdaExpression lambda = Expression.Lambda(deep, P);
            RunOnThread(() =>
            {
                ExpressionResourceLimitException analysis = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => ExpressionBehaviorAnalysis.Analyze(deep, NoLimits), family);
                Assert.AreEqual(ExpressionResourceLimitKind.Stack, analysis.Kind, family);
                ExpressionResourceLimitException invocation = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => ExpressionBehaviorAnalysis.AnalyzeInvocation(lambda, NoLimits), family);
                Assert.AreEqual(ExpressionResourceLimitKind.Stack, invocation.Kind, family);
                Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth, family);
                Assert.AreEqual(ExpressionBehaviors.ReadsMutableState, ExpressionBehaviorAnalysis.Analyze(NegateChain(10)).Flags, family);
            }, SmallStack, Timeout);
        }
    }

    /// <summary>With the default limits, every hostile nesting is refused by the validator, before any recursion.</summary>
    [TestMethod]
    public void HostileNestings_AreRefusedByDefaultLimits()
    {
        foreach ((string family, Expression deep) in HostileNestings())
        {
            RunOnThread(() =>
            {
                ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => ExpressionBehaviorAnalysis.Analyze(deep), family);
                Assert.AreEqual(ExpressionResourceLimitKind.Depth, exception.Kind, family);
            }, SmallStack, Timeout);
        }
    }

    /// <summary>
    /// A shared DAG is walked once per use, like every engine walk: the default node limit refuses an exponential unfolding,
    /// and without a node limit the visit budget does.
    /// </summary>
    [TestMethod]
    public void SharedDag_CannotEvadeTheLimits()
    {
        Expression dag = SharedDag(40);
        ExpressionResourceLimitException nodes = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => ExpressionBehaviorAnalysis.Analyze(dag));
        Assert.AreEqual(ExpressionResourceLimitKind.Nodes, nodes.Kind);

        ExpressionResourceLimits visits = ExpressionResourceLimits.Default with { MaxNodes = long.MaxValue, MaxStructuralVisits = 100_000 };
        ExpressionResourceLimitException work = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => ExpressionBehaviorAnalysis.Analyze(dag, visits));
        Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, work.Kind);

        Assert.AreEqual(ExpressionBehaviors.ReadsMutableState, ExpressionBehaviorAnalysis.Analyze(SharedDag(10)).Flags);
    }

    /// <summary>The visit budget is exact: the measured number of visits succeeds, one less is refused.</summary>
    [TestMethod]
    public void VisitBudget_IsExact()
    {
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        Expression tree = Expression.Block(
            [V],
            Expression.Assign(V, Expression.Invoke(Expression.Lambda<Func<double, double>>(Expression.Call(AbsMethod, y), y), P)),
            Expression.Lambda<Func<double>>(V),
            Expression.Condition(Expression.GreaterThan(V, Expression.Constant(0.0)), V, Expression.Negate(P)));

        long cost = ExpressionResourceBudget.Run(NoLimits, null, null, null, tree, static t =>
        {
            long before = ExpressionResourceBudget.StructuralVisits;
            ExpressionBehaviorAnalysis.Analyze(t, NoLimits);
            return ExpressionResourceBudget.StructuralVisits - before;
        });

        Assert.IsTrue(cost > 10, $"cost {cost}");
        ExpressionBehaviorAnalysis.Analyze(tree, ExpressionResourceLimits.Default with { MaxStructuralVisits = cost });
        ExpressionResourceLimitException refused = Assert.ThrowsExactly<ExpressionResourceLimitException>(
            () => ExpressionBehaviorAnalysis.Analyze(tree, ExpressionResourceLimits.Default with { MaxStructuralVisits = cost - 1 }));
        Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, refused.Kind);
    }

    /// <summary>
    /// An analysis started while another operation runs on the thread joins it: its visits count against the running
    /// budget, and its own limits can only tighten it.
    /// </summary>
    [TestMethod]
    public void NestedAnalysis_JoinsTheRunningOperation()
    {
        Expression chain = NegateChain(200);
        ExpressionResourceLimits outer = ExpressionResourceLimits.Default with { MaxStructuralVisits = 100 };
        ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(
            () => ExpressionResourceBudget.Run(outer, null, null, null, chain, static c => ExpressionBehaviorAnalysis.Analyze(c, ExpressionResourceLimits.Unlimited)));
        Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, exception.Kind, "the nested analysis cannot loosen the running budget");
        Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
    }

    /// <summary>
    /// A user subclass of <see cref="MethodInfo"/> is never read: the analysis classifies it as unknown code from its class
    /// alone, twice (it is not cached either, which would call its <see cref="object.GetHashCode"/>).
    /// </summary>
    [TestMethod]
    public void UserMethodInfo_IsNeverRead()
    {
        var calls = new List<string>();
        MethodInfo hostile = new TrapMethodInfo(calls);
        Assert.AreEqual(ExpressionBehaviors.UnknownCode, ExpressionBehaviorAnalysis.AnalyzeMethod(hostile).Flags);
        Assert.AreEqual(ExpressionBehaviors.UnknownCode, ExpressionBehaviorAnalysis.AnalyzeMethod(hostile).Flags);
        Assert.AreEqual(0, calls.Count, string.Join(", ", calls));
    }

    /// <summary>A user <see cref="MethodInfo"/> whose every member records its call and throws.</summary>
    private sealed class TrapMethodInfo : MethodInfo
    {
        /// <summary>The recorded calls.</summary>
        private readonly List<string> _calls;

        /// <summary>Initializes the trap.</summary>
        /// <param name="calls">The list receiving the calls.</param>
        public TrapMethodInfo(List<string> calls) => _calls = calls;

        /// <summary>Records a call and returns the exception to throw.</summary>
        /// <param name="member">The member name.</param>
        /// <returns>The exception.</returns>
        private InvalidOperationException Trap(string member)
        {
            _calls.Add(member);
            return new InvalidOperationException(member);
        }

        /// <inheritdoc/>
        public override string Name => throw Trap(nameof(Name));

        /// <inheritdoc/>
        public override Type? DeclaringType => throw Trap(nameof(DeclaringType));

        /// <inheritdoc/>
        public override Type? ReflectedType => throw Trap(nameof(ReflectedType));

        /// <inheritdoc/>
        public override MethodAttributes Attributes => throw Trap(nameof(Attributes));

        /// <inheritdoc/>
        public override RuntimeMethodHandle MethodHandle => throw Trap(nameof(MethodHandle));

        /// <inheritdoc/>
        public override ICustomAttributeProvider ReturnTypeCustomAttributes => throw Trap(nameof(ReturnTypeCustomAttributes));

        /// <inheritdoc/>
        public override MethodInfo GetBaseDefinition() => throw Trap(nameof(GetBaseDefinition));

        /// <inheritdoc/>
        public override object[] GetCustomAttributes(bool inherit) => throw Trap(nameof(GetCustomAttributes));

        /// <inheritdoc/>
        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => throw Trap(nameof(GetCustomAttributes));

        /// <inheritdoc/>
        public override MethodImplAttributes GetMethodImplementationFlags() => throw Trap(nameof(GetMethodImplementationFlags));

        /// <inheritdoc/>
        public override ParameterInfo[] GetParameters() => throw Trap(nameof(GetParameters));

        /// <inheritdoc/>
        public override object? Invoke(object? obj, BindingFlags invokeAttr, Binder? binder, object?[]? parameters, System.Globalization.CultureInfo? culture) => throw Trap(nameof(Invoke));

        /// <inheritdoc/>
        public override bool IsDefined(Type attributeType, bool inherit) => throw Trap(nameof(IsDefined));

        /// <inheritdoc/>
        public override bool Equals(object? obj) => throw Trap(nameof(Equals));

        /// <inheritdoc/>
        public override int GetHashCode() => throw Trap(nameof(GetHashCode));
    }
}
