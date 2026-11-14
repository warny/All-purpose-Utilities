using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>The exception a <see cref="R4StrictExtension"/> throws when one of its members is called while it is armed.</summary>
internal sealed class R4StrictReadException : Exception
{
    /// <summary>Initializes the exception.</summary>
    /// <param name="member">The member that was called.</param>
    /// <param name="stack">The call stack of that call.</param>
    public R4StrictReadException(string member, string stack)
        : base($"{member} was called on an armed strict extension node.")
    {
        Member = member;
        Stack = stack;
    }

    /// <summary>Gets the member that was called.</summary>
    public string Member { get; }

    /// <summary>Gets the call stack of the call.</summary>
    public string Stack { get; }
}

/// <summary>
/// A strict extension node (ETC R4): every member a caller could use is counted with its call stack, and, once
/// <see cref="Armed"/>, throws <see cref="R4StrictReadException"/> whoever the caller is, a framework factory included (unlike
/// <see cref="R4HostileExtension"/>, which lets the framework factories validate it). It is built while unarmed, so the
/// factories that build the source tree can validate it, then armed before the engine runs.
/// </summary>
internal sealed class R4StrictExtension : Expression
{
    /// <summary>Gets or sets whether every counted member throws.</summary>
    public bool Armed { get; set; }

    /// <summary>Gets the calls per member name.</summary>
    public Dictionary<string, int> Calls { get; } = [];

    /// <summary>Gets the call stack of every counted call, in order.</summary>
    public List<string> Stacks { get; } = [];

    /// <summary>Gets the total number of counted calls.</summary>
    public int TotalCalls => Calls.Values.Sum();

    /// <summary>Clears the counters.</summary>
    public void ResetCalls()
    {
        Calls.Clear();
        Stacks.Clear();
    }

    /// <summary>Counts a call to <paramref name="member"/>, and throws when armed.</summary>
    /// <param name="member">The member name.</param>
    private void Hit(string member)
    {
        Calls[member] = Calls.TryGetValue(member, out int count) ? count + 1 : 1;
        string stack = new System.Diagnostics.StackTrace(1, false).ToString();
        Stacks.Add(stack);
        if (Armed) throw new R4StrictReadException(member, stack);
    }

    /// <inheritdoc/>
    public override ExpressionType NodeType
    {
        get
        {
            Hit(nameof(NodeType));
            return ExpressionType.Extension;
        }
    }

    /// <inheritdoc/>
    public override Type Type
    {
        get
        {
            Hit(nameof(Type));
            return typeof(double);
        }
    }

    /// <inheritdoc/>
    public override bool CanReduce
    {
        get
        {
            Hit(nameof(CanReduce));
            return true;
        }
    }

    /// <inheritdoc/>
    public override Expression Reduce()
    {
        Hit(nameof(Reduce));
        return Constant(3.0);
    }

    /// <inheritdoc/>
    protected override Expression VisitChildren(ExpressionVisitor visitor)
    {
        Hit(nameof(VisitChildren));
        return this;
    }

    /// <inheritdoc/>
    protected override Expression Accept(ExpressionVisitor visitor)
    {
        Hit(nameof(Accept));
        return base.Accept(visitor);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        Hit(nameof(Equals));
        return ReferenceEquals(this, obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        Hit(nameof(GetHashCode));
        return 0;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        Hit(nameof(ToString));
        return "strict";
    }
}

/// <summary>The methods called by the samples of <see cref="ExpressionTreeR4CallRebuildTests"/>.</summary>
public static class R4CallTestMethods
{
    /// <summary>Returns its argument.</summary>
    /// <param name="value">The value.</param>
    /// <returns><paramref name="value"/>.</returns>
    public static double Identity(double value) => value;

    /// <summary>Adds its arguments.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>The sum.</returns>
    public static double Pair(double left, double right) => left + right;

    /// <summary>Creates a holder.</summary>
    /// <param name="factor">The factor of the holder.</param>
    /// <returns>A new holder.</returns>
    public static R4CallHolder Create(double factor) => new(factor);
}

/// <summary>The receiver of the instance calls of <see cref="ExpressionTreeR4CallRebuildTests"/>.</summary>
/// <param name="factor">The factor applied by <see cref="Scale"/>.</param>
public sealed class R4CallHolder(double factor)
{
    /// <summary>Gets the factor.</summary>
    public double Factor { get; } = factor;

    /// <summary>Multiplies <paramref name="value"/> by the factor.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The product.</returns>
    public double Scale(double value) => Factor * value;
}

/// <summary>
/// ETC R4: a call whose children the engine leaves unchanged is not rebuilt. Rebuilding it through
/// <see cref="Expression.Call(MethodInfo, Expression[])"/> made the framework factory validate every argument again, reading the
/// <c>Type</c> and <c>NodeType</c> of an extension node the engine itself never reads; a node that forbids these reads once the
/// tree is built made the simplification throw. A call whose receiver or another argument really changed is still rebuilt, and
/// that rebuild still validates the extension node: the framework factories offer no way to build a call without it.
/// </summary>
[TestClass]
[TestCategory("ETC-R4")]
public class ExpressionTreeR4CallRebuildTests
{
    /// <summary>The free parameter of the samples.</summary>
    private static readonly ParameterExpression P = Expression.Parameter(typeof(double), "p");

    /// <summary><see cref="R4CallTestMethods.Identity"/>.</summary>
    private static readonly MethodInfo IdentityMethod = typeof(R4CallTestMethods).GetMethod(nameof(R4CallTestMethods.Identity))!;

    /// <summary><see cref="R4CallTestMethods.Pair"/>.</summary>
    private static readonly MethodInfo PairMethod = typeof(R4CallTestMethods).GetMethod(nameof(R4CallTestMethods.Pair))!;

    /// <summary><see cref="R4CallTestMethods.Create"/>.</summary>
    private static readonly MethodInfo CreateMethod = typeof(R4CallTestMethods).GetMethod(nameof(R4CallTestMethods.Create))!;

    /// <summary><see cref="R4CallHolder.Scale"/>.</summary>
    private static readonly MethodInfo ScaleMethod = typeof(R4CallHolder).GetMethod(nameof(R4CallHolder.Scale))!;

    /// <summary>A derived simplifier overriding nothing: it finalizes through <c>CopyExpression</c>.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>A derived simplifier recording the call of the root's <c>FinalizeExpression</c> and what <c>base.FinalizeExpression</c> returned.</summary>
    private sealed class ObservingSimplifier : ExpressionSimplifier
    {
        /// <summary>Gets the last node given to <c>FinalizeExpression</c>.</summary>
        public Expression? FinalizeInput { get; private set; }

        /// <summary>Gets the result of <c>base.FinalizeExpression</c> for <see cref="FinalizeInput"/>.</summary>
        public Expression? BaseFinalizeResult { get; private set; }

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
        {
            Expression result = base.FinalizeExpression(e, parameters);
            FinalizeInput = e;
            BaseFinalizeResult = result;
            return result;
        }
    }

    /// <summary>
    /// A generic transformer recursing through <c>PrepareExpression</c>, rewriting the constant <c>1.0</c> into <c>2.0</c>, and
    /// finalizing with <c>CopyExpression</c>; it records the nodes given to both callbacks, in order.
    /// </summary>
    private sealed class RecordingTransformer : ExpressionTransformer
    {
        /// <summary>Gets the nodes given to <c>PrepareExpression</c>, in order.</summary>
        public List<Expression> Prepared { get; } = [];

        /// <summary>Gets the nodes and child lists given to <c>FinalizeExpression</c>, in order.</summary>
        public List<(Expression Node, Expression[] Children)> Finalized { get; } = [];

        /// <summary>Rewrites the marker constant.</summary>
        /// <param name="constant">The constant.</param>
        /// <returns>The rewritten or original constant.</returns>
        [ExpressionSignature(ExpressionType.Constant)]
        private Expression Rewrite(ConstantExpression constant) => constant.Value is 1.0 ? Expression.Constant(2.0) : constant;

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <inheritdoc/>
        protected override Expression PrepareExpression(Expression e)
        {
            Prepared.Add(e);
            return TransformCore(e);
        }

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
        {
            Finalized.Add((e, parameters));
            return CopyExpression(e, parameters);
        }

        /// <summary>Calls the protected <c>ReplaceArguments</c>.</summary>
        /// <param name="e">The tree.</param>
        /// <param name="parameter">The parameter to replace.</param>
        /// <param name="replacement">The replacement.</param>
        /// <returns>The substituted tree.</returns>
        public Expression Replace(Expression e, ParameterExpression parameter, Expression replacement) => ReplaceArguments(e, [parameter], [replacement]);

        /// <summary>Calls the protected static <c>CopyExpression</c>.</summary>
        /// <param name="e">The node to copy.</param>
        /// <param name="parameters">The replacement children.</param>
        /// <returns>The copy.</returns>
        public static Expression Copy(Expression e, params Expression[] parameters) => CopyExpression(e, parameters);
    }

    /// <summary>Arms <paramref name="node"/>, runs <paramref name="action"/>, then disarms it.</summary>
    /// <param name="node">The strict node.</param>
    /// <param name="action">The engine operation.</param>
    private static void Armed(R4StrictExtension node, Action action)
    {
        node.ResetCalls();
        node.Armed = true;
        try
        {
            action();
        }
        finally
        {
            node.Armed = false;
        }
    }

    /// <summary>Describes the calls made into <paramref name="node"/>, with the stack of the first one.</summary>
    /// <param name="node">The strict node.</param>
    /// <returns>A description for an assertion message.</returns>
    private static string DescribeCalls(R4StrictExtension node)
        => string.Join(",", node.Calls.Select(call => $"{call.Key}={call.Value}")) + (node.Stacks.Count > 0 ? Environment.NewLine + node.Stacks[0] : "");

    /// <summary>
    /// The transformers of a test with their names: the exact simplifier, a derived simplifier and a generic transformer.
    /// </summary>
    /// <returns>A fresh instance of each.</returns>
    private static (string Name, Func<Expression, Expression> Run)[] Engines()
    {
        var simplifier = new ExpressionSimplifier();
        var derived = new DerivedSimplifier();
        var transformer = new RecordingTransformer();
        return
        [
            ("simplifier", simplifier.Simplify),
            ("derived simplifier", derived.Simplify),
            ("generic transformer", transformer.Transform),
            ("substitution without match", tree => new RecordingTransformer().Replace(tree, Expression.Parameter(typeof(double), "absent"), Expression.Constant(4.0))),
        ];
    }

    /// <summary>
    /// Test A: a static call whose children are unchanged and hold the extension node is returned as is by every transformer
    /// and by a substitution that replaces nothing, without any call into the armed extension node, the framework factories
    /// included. A call holding such a call (the extension node one level deeper) keeps its historical rebuild, which reads
    /// nothing of the extension node: only the direct children are validated.
    /// </summary>
    [TestMethod]
    public void UnchangedStaticCall_IsNotRebuilt_AndTheExtensionIsNeverRead()
    {
        var node = new R4StrictExtension();
        (string Name, Expression Tree)[] shapes =
        [
            ("Identity(ext)", Expression.Call(IdentityMethod, node)),
            ("Pair(ext, p)", Expression.Call(PairMethod, node, P)),
            ("Pair(p, ext)", Expression.Call(PairMethod, P, node)),
        ];

        foreach ((string shape, Expression tree) in shapes)
        {
            foreach ((string engine, Func<Expression, Expression> run) in Engines())
            {
                Expression result = null!;
                Armed(node, () => result = run(tree));
                Assert.AreEqual(0, node.TotalCalls, $"{shape}, {engine}: {DescribeCalls(node)}");
                Assert.AreSame(tree, result, $"{shape}, {engine}: an unchanged call is kept");
            }
        }

        MethodCallExpression inner = Expression.Call(IdentityMethod, node);
        MethodCallExpression outer = Expression.Call(IdentityMethod, inner);
        foreach ((string engine, Func<Expression, Expression> run) in Engines())
        {
            Expression result = null!;
            Armed(node, () => result = run(outer));
            Assert.AreEqual(0, node.TotalCalls, $"Identity(Identity(ext)), {engine}: {DescribeCalls(node)}");
            Assert.AreNotSame(outer, result, $"{engine}: the outer call over an ordinary child keeps its historical rebuild");
            Assert.AreSame(inner, ((MethodCallExpression)result).Arguments[0], $"{engine}: the inner call is kept");
        }

        // CopyExpression given the very children of the call keeps it too.
        var call = (MethodCallExpression)shapes[1].Tree;
        Armed(node, () => Assert.AreSame(call, RecordingTransformer.Copy(call, node, P)));
        Assert.AreEqual(0, node.TotalCalls, DescribeCalls(node));
    }

    /// <summary>
    /// Ordinary expressions keep their historical rebuild and identities: an unchanged call or invocation over framework nodes
    /// only is rebuilt by the exact simplifier, a derived simplifier, a generic transformer, <c>CopyExpression</c> and a
    /// substitution that replaces nothing; a derived simplifier calling <c>base.FinalizeExpression</c> receives a copy distinct
    /// from the prepared node (PR #582). The same derived simplifier on a call holding an extension node receives the original
    /// call, and <c>base.FinalizeExpression</c> returns it: that node is the one exception.
    /// </summary>
    [TestMethod]
    public void OrdinaryCallsAndInvocations_KeepTheirHistoricalRebuild()
    {
        ParameterExpression function = Expression.Parameter(typeof(Func<double, double>), "f");
        (string Name, Expression Tree)[] shapes =
        [
            ("Identity(p)", Expression.Call(IdentityMethod, P)),
            ("h.Scale(p)", Expression.Call(Expression.Parameter(typeof(R4CallHolder), "h"), ScaleMethod, P)),
            ("f(p)", Expression.Invoke(function, P)),
        ];

        foreach ((string shape, Expression tree) in shapes)
        {
            foreach ((string engine, Func<Expression, Expression> run) in Engines())
            {
                Assert.AreNotSame(tree, run(tree), $"{shape}, {engine}: an ordinary node is rebuilt");
            }

            Expression[] children = tree is MethodCallExpression call ? [.. call.Arguments] : [.. ((InvocationExpression)tree).Arguments];
            Assert.AreNotSame(tree, RecordingTransformer.Copy(tree, children), $"{shape}: CopyExpression rebuilds an ordinary node");

            // PR #582's guarantee covers the families it optimized (unary, binary, call, conditional): an invocation is not one.
            if (tree is not MethodCallExpression) continue;
            var observing = new ObservingSimplifier();
            observing.Simplify(tree);
            Assert.AreSame(tree.GetType(), observing.FinalizeInput!.GetType(), $"{shape}: the root reaches FinalizeExpression");
            Assert.AreNotSame(observing.FinalizeInput, observing.BaseFinalizeResult, $"{shape}: a derived simplifier receives a distinct copy");
        }

        var node = new R4StrictExtension();
        MethodCallExpression opaqueCall = Expression.Call(PairMethod, node, P);
        var derived = new ObservingSimplifier();
        Armed(node, () => Assert.AreSame(opaqueCall, derived.Simplify(opaqueCall)));
        Assert.AreEqual(0, node.TotalCalls, DescribeCalls(node));
        Assert.AreSame(opaqueCall, derived.FinalizeInput, "the unchanged call holding an extension node is kept by the preparation");
        Assert.AreSame(opaqueCall, derived.BaseFinalizeResult, "and by base.FinalizeExpression");
    }

    /// <summary>
    /// The other families that pass their children to a validating factory: an invocation of a delegate parameter and a dynamic
    /// operation holding the extension node, unchanged, are returned as is without any call into the armed node (the dynamic
    /// operation already was, since ETC R4); an invocation of an unchanged call holding the node keeps its historical rebuild,
    /// which reads nothing of the node.
    /// </summary>
    [TestMethod]
    public void UnchangedInvocationAndDynamic_AreNotRebuilt_AndTheExtensionIsNeverRead()
    {
        var node = new R4StrictExtension();
        ParameterExpression function = Expression.Parameter(typeof(Func<double, double>), "f");
        MethodCallExpression inner = Expression.Call(IdentityMethod, node);
        InvocationExpression outer = Expression.Invoke(function, inner);
        foreach ((string engine, Func<Expression, Expression> run) in Engines())
        {
            Expression result = null!;
            Armed(node, () => result = run(outer));
            Assert.AreEqual(0, node.TotalCalls, $"f(Identity(ext)), {engine}: {DescribeCalls(node)}");
            Assert.AreSame(inner, ((InvocationExpression)result).Arguments[0], $"{engine}: the inner call is kept");
        }

        (string Name, Expression Tree)[] shapes =
        [
            ("f(ext)", Expression.Invoke(function, node)),
            ("dynamic(ext, p)", Expression.Dynamic(new R4CountingBinder("ext", throwOnBind: true), typeof(object), node, P)),
        ];

        foreach ((string shape, Expression tree) in shapes)
        {
            foreach ((string engine, Func<Expression, Expression> run) in Engines())
            {
                Expression result = null!;
                Armed(node, () => result = run(tree));
                Assert.AreEqual(0, node.TotalCalls, $"{shape}, {engine}: {DescribeCalls(node)}");
                Assert.AreSame(tree, result, $"{shape}, {engine}: an unchanged node is kept");
            }
        }
    }

    /// <summary>
    /// Test B: an instance call keeps its receiver; the receiver is prepared before the arguments, the arguments in order; when
    /// nothing changes the call is kept without reading the extension node.
    /// </summary>
    [TestMethod]
    public void UnchangedInstanceCall_KeepsItsReceiver_AndTheHistoricalOrder()
    {
        var node = new R4StrictExtension();
        ParameterExpression holder = Expression.Parameter(typeof(R4CallHolder), "h");
        MethodCallExpression call = Expression.Call(holder, ScaleMethod, node);

        foreach ((string engine, Func<Expression, Expression> run) in Engines())
        {
            Expression result = null!;
            Armed(node, () => result = run(call));
            Assert.AreEqual(0, node.TotalCalls, $"{engine}: {DescribeCalls(node)}");
            Assert.AreSame(call, result, engine);
        }

        var transformer = new RecordingTransformer();
        Armed(node, () => transformer.Transform(call));
        CollectionAssert.AreEqual(new Expression[] { holder, node }, transformer.Prepared, "the receiver, then the argument");

        // A receiver that really changes (its constant 1.0 is rewritten) rebuilds the call on the new receiver and the same argument.
        MethodCallExpression created = Expression.Call(Expression.Call(CreateMethod, Expression.Constant(1.0)), ScaleMethod, P);
        var rebuilt = (MethodCallExpression)new RecordingTransformer().Transform(created);
        Assert.AreNotSame(created, rebuilt);
        Assert.AreSame(ScaleMethod, rebuilt.Method);
        Assert.AreSame(P, rebuilt.Arguments[0]);
        Assert.AreEqual(2.0, ((ConstantExpression)((MethodCallExpression)rebuilt.Object!).Arguments[0]).Value);
        Assert.AreEqual(6.0, Expression.Lambda<Func<double, double>>(rebuilt, P).Compile()(3.0));
    }

    /// <summary>
    /// Test C: when another argument really changes (<c>p * 1.0</c> simplified to <c>p</c>, <c>1.0</c> rewritten, <c>p</c>
    /// substituted), the call is rebuilt with the transformed argument and the same extension instance; the framework factory
    /// then validates the extension node (it reads its <c>Type</c>, as the factory that built the source tree did). An armed
    /// node therefore makes the operation throw its own exception, unchanged: the transformation is never dropped to avoid that
    /// read, and the same instance works again afterwards.
    /// </summary>
    [TestMethod]
    public void ChangedArgument_IsKept_AndTheRebuildValidatesTheExtension()
    {
        var node = new R4StrictExtension();
        var simplifier = new ExpressionSimplifier();
        (string Name, Func<Expression> Run, Expression Expected)[] cases =
        [
            ("simplified", () => simplifier.Simplify(Expression.Call(PairMethod, node, Expression.Multiply(P, Expression.Constant(1.0)))), P),
            ("rewritten", () => new RecordingTransformer().Transform(Expression.Call(PairMethod, node, Expression.Constant(1.0))), null!),
            ("substituted", () => new RecordingTransformer().Replace(Expression.Call(PairMethod, node, P), P, Expression.Constant(4.0)), null!),
        ];

        foreach ((string name, Func<Expression> run, Expression expected) in cases)
        {
            node.ResetCalls();
            var result = (MethodCallExpression)run();
            Assert.AreSame(node, result.Arguments[0], $"{name}: the extension instance is kept");
            Assert.AreSame(PairMethod, result.Method, name);
            if (expected is not null) Assert.AreSame(expected, result.Arguments[1], name);
            else Assert.IsInstanceOfType<ConstantExpression>(result.Arguments[1], name);
            Assert.AreNotEqual(0, node.TotalCalls, $"{name}: the rebuild validates the extension");
            CollectionAssert.IsSubsetOf(node.Calls.Keys.ToArray(), new[] { nameof(Expression.Type), nameof(Expression.NodeType) }, $"{name}: {DescribeCalls(node)}");
            // The getters are read by the validation helpers of the framework (System.Dynamic.Utils), whatever the JIT inlined above them.
            Assert.IsTrue(node.Stacks.All(stack => stack.Contains("System.Dynamic.Utils.", StringComparison.Ordinal) || stack.Contains("System.Linq.Expressions.", StringComparison.Ordinal)), $"{name}: only framework factories read the node: {DescribeCalls(node)}");

            R4StrictReadException exception = null!;
            Armed(node, () => exception = Assert.ThrowsExactly<R4StrictReadException>(() => run()));
            Assert.AreEqual(1, node.TotalCalls, $"{name}: the first read throws");
            Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth, $"{name}: the operation is closed");
            Assert.AreSame(node, ((MethodCallExpression)run()).Arguments[0], $"{name}: the instance works again");
        }
    }

    /// <summary>
    /// Remaining limit, pinned: the other families are out of scope, so an unchanged binary parent of the extension node
    /// (<c>p + ext</c>) is still rebuilt by its factory, which reads the node's getters (only the framework reads them). An armed
    /// node makes the simplification throw its own exception, unchanged, and the operation is closed.
    /// </summary>
    [TestMethod]
    public void UnchangedBinaryParent_IsStillRebuilt_AndValidatedByTheFramework()
    {
        var node = new R4StrictExtension();
        BinaryExpression sum = Expression.Add(P, node);
        var simplifier = new ExpressionSimplifier();

        node.ResetCalls();
        simplifier.Simplify(sum);
        Assert.AreNotEqual(0, node.TotalCalls, "the binary factory validates the extension");
        CollectionAssert.IsSubsetOf(node.Calls.Keys.ToArray(), new[] { nameof(Expression.Type), nameof(Expression.NodeType) }, DescribeCalls(node));

        Armed(node, () => Assert.ThrowsExactly<R4StrictReadException>(() => simplifier.Simplify(sum)));
        Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
    }

    /// <summary>
    /// Test D: a transformer that overrides <c>PrepareExpression</c> and <c>FinalizeExpression</c> keeps its callbacks: each
    /// child is prepared once, in order, and <c>FinalizeExpression</c> receives the call with its prepared arguments (the very
    /// instances, the extension node included); an unchanged call is the original node.
    /// </summary>
    [TestMethod]
    public void DerivedTransformer_KeepsItsCallbacks_AndThePreparedChildren()
    {
        var node = new R4StrictExtension();
        MethodCallExpression call = Expression.Call(PairMethod, node, P);
        var transformer = new RecordingTransformer();

        Armed(node, () => Assert.AreSame(call, transformer.Transform(call)));

        Assert.AreEqual(0, node.TotalCalls, DescribeCalls(node));
        CollectionAssert.AreEqual(new Expression[] { node, P }, transformer.Prepared, "the arguments, in order (the root enters through Transform)");
        (Expression finalizedCall, Expression[] children) = transformer.Finalized.Single(entry => entry.Node is MethodCallExpression);
        Assert.AreSame(call, finalizedCall);
        CollectionAssert.AreEqual(new Expression[] { node, P }, children);
        Assert.AreEqual(3, transformer.Finalized.Count, "the extension node, the parameter and the call are finalized once each");
    }

    /// <summary>
    /// Test E: a resource limit reached while a call holding an extension node is simplified is raised, not absorbed; the
    /// operation is closed and the same instance works again, without reading the extension node.
    /// </summary>
    [TestMethod]
    public void ResourceLimit_IsRaised_AndTheInstanceWorksAgain()
    {
        var node = new R4StrictExtension();
        Expression tree = Expression.Call(PairMethod, node, Expression.Call(IdentityMethod, Expression.Call(IdentityMethod, P)));
        var limited = new ExpressionSimplifier(ExpressionResourceLimits.Unlimited with { MaxTransformVisits = 2 });

        Armed(node, () => Assert.ThrowsExactly<ExpressionResourceLimitException>(() => limited.Simplify(tree)));
        Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
        Assert.AreEqual(0, node.TotalCalls, DescribeCalls(node));

        Expression small = Expression.Call(IdentityMethod, node);
        Armed(node, () => Assert.AreSame(small, limited.Simplify(small)));
        Assert.AreEqual(0, node.TotalCalls, DescribeCalls(node));
    }
}
