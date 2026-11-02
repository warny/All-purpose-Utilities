using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>
/// ETC R4, checkpoint A: traversal, faithful reconstruction (C-02), capture-avoiding substitution, structural equality, hashing
/// and canonical keys of <see cref="DynamicExpression"/>. The binder is metadata: it is kept by identity, compared and hashed by
/// identity, and never called (every test binder counts its calls, and most of them throw when bound).
/// </summary>
[TestClass]
[TestCategory("ETC-R4")]
public class ExpressionTreeR4DynamicTests
{
    /// <summary>The free parameter of the samples.</summary>
    private static readonly ParameterExpression P = Expression.Parameter(typeof(double), "p");

    /// <summary>A second free parameter.</summary>
    private static readonly ParameterExpression Q = Expression.Parameter(typeof(double), "q");

    /// <summary>Builds the simplifiable leaf <c>p * 1.0</c>.</summary>
    /// <returns>A new leaf.</returns>
    private static Expression Leaf() => Expression.Multiply(P, Expression.Constant(1.0));

    /// <summary>Builds <c>parameter * 1.0</c>.</summary>
    /// <param name="parameter">The parameter.</param>
    /// <returns>A new leaf.</returns>
    private static Expression Leaf(ParameterExpression parameter) => Expression.Multiply(parameter, Expression.Constant(1.0));

    /// <summary>Builds a dynamic operation returning <see cref="object"/> over <paramref name="arguments"/>.</summary>
    /// <param name="binder">The binder.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The node.</returns>
    private static DynamicExpression Dynamic(CallSiteBinder binder, params Expression[] arguments)
        => Expression.Dynamic(binder, typeof(object), arguments);

    /// <summary>Builds a dynamic operation returning <see cref="double"/> over <paramref name="arguments"/>.</summary>
    /// <param name="binder">The binder.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The node.</returns>
    private static DynamicExpression DoubleDynamic(CallSiteBinder binder, params Expression[] arguments)
        => Expression.Dynamic(binder, typeof(double), arguments);

    /// <summary>A derived simplifier overriding nothing.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>A derived simplifier recording every <c>FinalizeExpression</c> call, then deferring to the base.</summary>
    private sealed class RecordingSimplifier : ExpressionSimplifier
    {
        /// <summary>Gets the child lists received for dynamic operations.</summary>
        public List<Expression[]> DynamicChildren { get; } = [];

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
        {
            if (e is DynamicExpression) DynamicChildren.Add(parameters);
            return base.FinalizeExpression(e, parameters);
        }
    }

    /// <summary>A generic transformer recursing through <c>PrepareExpression</c> and rewriting the constant <c>1.0</c> into <c>2.0</c>.</summary>
    private sealed class RecursiveTransformer : ExpressionTransformer
    {
        /// <summary>Rewrites the marker constant.</summary>
        /// <param name="constant">The constant.</param>
        /// <returns>The rewritten or original constant.</returns>
        [ExpressionSignature(ExpressionType.Constant)]
        private Expression Rewrite(ConstantExpression constant) => constant.Value is 1.0 ? Expression.Constant(2.0) : constant;

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <inheritdoc/>
        protected override Expression PrepareExpression(Expression e) => TransformCore(e);

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => CopyExpression(e, parameters);
    }

    /// <summary>A generic transformer whose rule receives the positional children of every dynamic operation.</summary>
    private sealed class DynamicRuleTransformer : ExpressionTransformer
    {
        /// <summary>Gets the child lists received by the rule.</summary>
        public List<Expression[]> Received { get; } = [];

        /// <summary>Records the children of a dynamic operation and keeps the node.</summary>
        /// <param name="node">The prepared node.</param>
        /// <param name="children">Its positional children.</param>
        /// <returns><paramref name="node"/>.</returns>
        [ExpressionSignature(ExpressionType.Dynamic)]
        private Expression Record(DynamicExpression node, Expression[] children)
        {
            Received.Add(children);
            return node;
        }

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <inheritdoc/>
        protected override Expression PrepareExpression(Expression e) => TransformCore(e);

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => CopyExpression(e, parameters);
    }

    /// <summary>Exposes the protected members of <see cref="ExpressionTransformer"/>.</summary>
    private sealed class ExposedTransformer : ExpressionTransformer
    {
        /// <summary>Calls the protected static <c>CopyExpression</c>.</summary>
        /// <param name="e">The node to copy.</param>
        /// <param name="parameters">The replacement children.</param>
        /// <returns>The copy.</returns>
        public static Expression Copy(Expression e, params Expression[] parameters) => CopyExpression(e, parameters);

        /// <summary>Calls the protected <c>ReplaceArguments</c>.</summary>
        /// <param name="e">The tree.</param>
        /// <param name="parameter">The parameter to replace.</param>
        /// <param name="replacement">The replacement.</param>
        /// <returns>The substituted tree.</returns>
        public Expression Replace(Expression e, ParameterExpression parameter, Expression replacement) => ReplaceArguments(e, [parameter], [replacement]);

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);
    }

    /// <summary>
    /// Asserts that <paramref name="result"/> is a dynamic operation with the binder instance, the exact delegate type and the
    /// result type of <paramref name="original"/>, and the same number of arguments.
    /// </summary>
    /// <param name="original">The source node.</param>
    /// <param name="result">The transformed node.</param>
    /// <returns><paramref name="result"/> as a dynamic operation.</returns>
    private static DynamicExpression AssertSameMetadata(DynamicExpression original, Expression result)
    {
        var dynamic = (DynamicExpression)result;
        Assert.AreSame(original.Binder, dynamic.Binder);
        Assert.AreEqual(original.DelegateType, dynamic.DelegateType);
        Assert.AreEqual(original.Type, dynamic.Type);
        Assert.AreEqual(original.Arguments.Count, dynamic.Arguments.Count);
        return dynamic;
    }

    // ------------------------------------------------------------------------------------------
    // A1: traversal.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// The arguments are traversed in order by the exact simplifier, a derived simplifier and a generic transformer; the binder
    /// instance, the delegate type and the result type are kept, and the binder (which throws when bound) is never called.
    /// </summary>
    [TestMethod]
    public void Arguments_AreTraversed_AndMetadataKept()
    {
        var binder = new R4CountingBinder("sum", throwOnBind: true);
        DynamicExpression original = Dynamic(binder, Leaf(P), Leaf(Q));

        foreach (Expression result in new[] { new ExpressionSimplifier().Simplify(original), new DerivedSimplifier().Simplify(original) })
        {
            DynamicExpression dynamic = AssertSameMetadata(original, result);
            Assert.AreSame(P, dynamic.Arguments[0]);
            Assert.AreSame(Q, dynamic.Arguments[1]);
        }

        DynamicExpression generic = AssertSameMetadata(original, new RecursiveTransformer().Transform(original));
        Assert.AreEqual(2.0, ((ConstantExpression)((BinaryExpression)generic.Arguments[0]).Right).Value);
        Assert.AreSame(P, ((BinaryExpression)generic.Arguments[0]).Left);
        Assert.AreSame(Q, ((BinaryExpression)generic.Arguments[1]).Left);
        Assert.AreEqual(0, binder.TotalCalls);
    }

    /// <summary>A dynamic operation whose arguments do not change is returned as the very same instance.</summary>
    [TestMethod]
    public void UnchangedArguments_KeepTheInstance()
    {
        var binder = new R4CountingBinder("same", throwOnBind: true);
        DynamicExpression original = Dynamic(binder, P, Q);

        Assert.AreSame(original, new ExpressionSimplifier().Simplify(original));
        Assert.AreSame(original, new DerivedSimplifier().Simplify(original));
        Assert.AreSame(original, new RecursiveTransformer().Transform(original));
        Assert.AreEqual(0, binder.TotalCalls);
    }

    /// <summary>
    /// A custom delegate signature, a <see cref="double"/> result and a <see langword="void"/> result are kept exactly: the
    /// delegate type is never re-inferred from the arguments.
    /// </summary>
    [TestMethod]
    public void DelegateTypes_AreKeptExactly()
    {
        var binder = new R4CountingBinder("typed", throwOnBind: true);
        DynamicExpression custom = Expression.MakeDynamic(typeof(R4DynamicDelegate), binder, Leaf(P), Leaf(Q));
        DynamicExpression typed = DoubleDynamic(binder, Leaf(P));
        DynamicExpression voidResult = Expression.MakeDynamic(typeof(Action<CallSite, double>), binder, Leaf(P));

        foreach (DynamicExpression original in new[] { custom, typed, voidResult })
        {
            AssertSameMetadata(original, new ExpressionSimplifier().Simplify(original));
            AssertSameMetadata(original, new DerivedSimplifier().Simplify(original));
            AssertSameMetadata(original, new RecursiveTransformer().Transform(original));
        }

        Assert.AreEqual(typeof(R4DynamicDelegate), ((DynamicExpression)new ExpressionSimplifier().Simplify(custom)).DelegateType);
        Assert.AreEqual(typeof(void), new ExpressionSimplifier().Simplify(voidResult).Type);
        Assert.AreEqual(0, binder.TotalCalls);
    }

    /// <summary>A dynamic operation without argument is a supported leaf for every transformer.</summary>
    [TestMethod]
    public void ZeroArguments_IsSupported()
    {
        var binder = new R4CountingBinder("zero", throwOnBind: true);
        DynamicExpression original = Expression.MakeDynamic(typeof(Func<CallSite, object>), binder, System.Array.Empty<Expression>());

        Assert.AreSame(original, new ExpressionSimplifier().Simplify(original));
        Assert.AreSame(original, new RecursiveTransformer().Transform(original));
        Assert.AreSame(original, ExposedTransformer.Copy(original));
        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(original, P));
        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(original, Expression.MakeDynamic(typeof(Func<CallSite, object>), binder, System.Array.Empty<Expression>())));
        Assert.AreEqual(0, binder.TotalCalls);
    }

    /// <summary>
    /// The positional children passed to the rules and to <c>FinalizeExpression</c> are the prepared arguments, in order (the
    /// call site has no position).
    /// </summary>
    [TestMethod]
    public void RulesAndFinalize_ReceiveTheArgumentsInOrder()
    {
        var binder = new R4CountingBinder("layout", throwOnBind: true);
        DynamicExpression original = Dynamic(binder, Leaf(P), Leaf(Q), Expression.Constant(3.5));

        var rules = new DynamicRuleTransformer();
        rules.Transform(original);
        Assert.AreEqual(1, rules.Received.Count);
        Assert.AreEqual(3, rules.Received[0].Length);
        Assert.AreSame(P, ((BinaryExpression)rules.Received[0][0]).Left);
        Assert.AreSame(Q, ((BinaryExpression)rules.Received[0][1]).Left);
        Assert.AreEqual(3.5, ((ConstantExpression)rules.Received[0][2]).Value);

        var recording = new RecordingSimplifier();
        recording.Simplify(original);
        Assert.AreEqual(1, recording.DynamicChildren.Count);
        Assert.AreEqual(3, recording.DynamicChildren[0].Length);
        Assert.AreSame(P, recording.DynamicChildren[0][0]);
        Assert.AreSame(Q, recording.DynamicChildren[0][1]);
        Assert.AreEqual(3.5, ((ConstantExpression)recording.DynamicChildren[0][2]).Value, "Constants are recreated by the simplifier; the value and position count.");
        Assert.AreEqual(0, binder.TotalCalls);
    }

    /// <summary>A dynamic operation nested in supported families, and families nested in its arguments, are all traversed.</summary>
    [TestMethod]
    public void NestedDynamicOperations_AreTraversed()
    {
        var binder = new R4CountingBinder("nested", throwOnBind: true);
        Expression inner = DoubleDynamic(binder, Leaf(P));
        Expression tree = Expression.Condition(
            Expression.GreaterThan(Leaf(Q), Expression.Constant(0.0)),
            DoubleDynamic(binder, Expression.Add(inner, Leaf(P))),
            Leaf(P));

        var simplified = (ConditionalExpression)new ExpressionSimplifier().Simplify(tree);

        var outer = (DynamicExpression)simplified.IfTrue;
        var sum = (BinaryExpression)outer.Arguments[0];
        Assert.IsTrue(sum.Left is DynamicExpression || sum.Right is DynamicExpression);
        var nested = (DynamicExpression)(sum.Left as DynamicExpression ?? sum.Right);
        Assert.AreSame(P, nested.Arguments[0]);
        Assert.AreSame(P, simplified.IfFalse);
        Assert.AreEqual(0, binder.TotalCalls);
    }

    /// <summary>Wide and deep dynamic operations are traversed completely.</summary>
    [TestMethod]
    public void LargeAndDeepDynamicOperations_AreTraversed()
    {
        var binder = new R4CountingBinder("large", throwOnBind: true);
        DynamicExpression wide = Dynamic(binder, Enumerable.Range(0, 500).Select(_ => Leaf()).ToArray());
        Expression deep = P;
        for (int i = 0; i < 150; i++)
        {
            deep = DoubleDynamic(binder, Leaf(P), deep);
        }

        DynamicExpression simplifiedWide = AssertSameMetadata(wide, new ExpressionSimplifier().Simplify(wide));
        Assert.IsTrue(simplifiedWide.Arguments.All(argument => ReferenceEquals(argument, P)));

        Expression current = new ExpressionSimplifier().Simplify(deep);
        int depth = 0;
        while (current is DynamicExpression level)
        {
            Assert.AreSame(P, level.Arguments[0]);
            current = level.Arguments[1];
            depth++;
        }

        Assert.AreEqual(150, depth);
        Assert.AreSame(P, current);
        Assert.AreEqual(0, binder.TotalCalls);
    }

    // ------------------------------------------------------------------------------------------
    // A2: CopyExpression (C-02).
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// <c>CopyExpression</c> follows the C-02 arity contract: an empty list or unchanged children keep the node; a list of the
    /// right length rebuilds it with the same binder and delegate type; a wrong length, a <see langword="null"/> or an
    /// argument that does not fit the delegate signature is rejected with <see cref="ArgumentException"/>.
    /// </summary>
    [TestMethod]
    public void CopyExpression_FollowsTheArityContract()
    {
        var binder = new R4CountingBinder("copy", throwOnBind: true);
        DynamicExpression original = Expression.MakeDynamic(typeof(R4DynamicDelegate), binder, P, Q);

        Assert.AreSame(original, ExposedTransformer.Copy(original));
        Assert.AreSame(original, ExposedTransformer.Copy(original, P, Q));

        DynamicExpression swapped = AssertSameMetadata(original, ExposedTransformer.Copy(original, Q, P));
        Assert.AreSame(Q, swapped.Arguments[0]);
        Assert.AreSame(P, swapped.Arguments[1]);

        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(original, P));
        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(original, P, Q, P));
        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(original, P, Expression.Constant("text")));
        Assert.Throws<ArgumentException>(() => ExposedTransformer.Copy(original, P, null!));
        Assert.AreEqual(0, binder.TotalCalls);
    }

    // ------------------------------------------------------------------------------------------
    // A3: substitution.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Substitution replaces the free occurrences inside the arguments, keeps the binder and the delegate type, leaves the
    /// node untouched when nothing is replaced, respects shadowing and alpha-renames a capturing declaration.
    /// </summary>
    [TestMethod]
    public void Substitution_EntersTheArguments_WithTheScopeModel()
    {
        var binder = new R4CountingBinder("subst", throwOnBind: true);
        var transformer = new ExposedTransformer();
        DynamicExpression free = Dynamic(binder, P, Q);

        DynamicExpression replaced = AssertSameMetadata(free, transformer.Replace(free, P, Expression.Constant(5.0)));
        Assert.AreEqual(5.0, ((ConstantExpression)replaced.Arguments[0]).Value);
        Assert.AreSame(Q, replaced.Arguments[1]);
        Assert.AreSame(free, transformer.Replace(free, Expression.Parameter(typeof(double), "other"), Expression.Constant(5.0)));

        // Shadowing: the lambda redeclares p, so its occurrence in the dynamic argument is bound there.
        LambdaExpression shadowing = Expression.Lambda(Dynamic(binder, P), P);
        Assert.AreSame(shadowing, transformer.Replace(shadowing, P, Expression.Constant(5.0)));

        // Capture: replacing q by p under a lambda declaring p renames the declaration and its bound occurrence.
        LambdaExpression capturing = Expression.Lambda(Dynamic(binder, P, Q), P);
        var renamed = (LambdaExpression)transformer.Replace(capturing, Q, P);
        ParameterExpression fresh = renamed.Parameters[0];
        Assert.AreNotSame(P, fresh);
        DynamicExpression body = AssertSameMetadata((DynamicExpression)capturing.Body, renamed.Body);
        Assert.AreSame(fresh, body.Arguments[0]);
        Assert.AreSame(P, body.Arguments[1], "The free p inserted for q must stay free.");
        Assert.AreEqual(0, binder.TotalCalls);
    }

    /// <summary>
    /// Inlining an invocation whose body is a dynamic operation over the invoked parameter substitutes the argument and keeps the
    /// dynamic operation (with its binder); a parameter read after the dynamic operation, whose argument is a variable, keeps the
    /// invocation (the operation runs user code). Both compiled results behave like the source.
    /// </summary>
    [TestMethod]
    public void Inlining_ThroughDynamicArguments_IsConservative()
    {
        var binder = new R4CountingBinder("inline");
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        var inlinable = Expression.Lambda<Func<double, double>>(Expression.Invoke(Expression.Lambda(DoubleDynamic(binder, x), x), P), P);
        var readAfter = Expression.Lambda<Func<double, double>>(
            Expression.Invoke(Expression.Lambda(Expression.Add(DoubleDynamic(binder, Expression.Constant(1.0)), x), x), P),
            P);

        var simplifiedInlinable = (Expression<Func<double, double>>)new ExpressionSimplifier().Simplify(inlinable);
        var simplifiedReadAfter = (Expression<Func<double, double>>)new ExpressionSimplifier().Simplify(readAfter);
        Assert.AreEqual(0, binder.TotalCalls, "Simplification must not bind.");

        DynamicExpression inlined = AssertSameMetadata((DynamicExpression)((LambdaExpression)((InvocationExpression)inlinable.Body).Expression).Body, simplifiedInlinable.Body);
        Assert.AreSame(P, inlined.Arguments[0]);
        Assert.IsInstanceOfType<InvocationExpression>(simplifiedReadAfter.Body);

        AssertSameExecution(inlinable, simplifiedInlinable, 2.5);
        AssertSameExecution(readAfter, simplifiedReadAfter, 2.5);
    }

    // ------------------------------------------------------------------------------------------
    // A4: equality, hash, canonical keys.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// The same binder instance with structurally equal arguments is equal on the raw and public comparers, hashes identically,
    /// works as a <see cref="HashSet{T}"/> and <see cref="Dictionary{TKey, TValue}"/> key and ties in the canonical order; the
    /// binder's own <c>Equals</c>, <c>GetHashCode</c> and <c>ToString</c> are never called.
    /// </summary>
    [TestMethod]
    public void SameBinder_EqualArguments_AreEquivalent()
    {
        var binder = new R4CountingBinder("equal", throwOnBind: true);
        DynamicExpression x = Dynamic(binder, Expression.Add(P, Expression.Constant(1.0)), Q);
        DynamicExpression y = Dynamic(binder, Expression.Add(P, Expression.Constant(1.0)), Q);

        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(x, y));
        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(y, x));
        Assert.AreEqual(ExpressionComparer.StructuralHashRaw(x), ExpressionComparer.StructuralHashRaw(y));
        Assert.IsTrue(ExpressionComparer.Default.Equals(x, y));
        Assert.AreEqual(ExpressionComparer.Default.GetHashCode(x), ExpressionComparer.Default.GetHashCode(y));
        Assert.IsTrue(new HashSet<Expression>(ExpressionComparer.Default) { x }.Contains(y));
        Assert.IsTrue(new Dictionary<Expression, int>(ExpressionComparer.Default) { [y] = 1 }.ContainsKey(x));
        Assert.AreEqual(0, ExpressionCanonicalOrder.Compare(x, y, []));
        Assert.AreEqual(0, binder.TotalCalls);
    }

    /// <summary>
    /// Two binder instances, even equal by their own <c>Equals</c> and of the same class, make two dynamic operations unequal;
    /// the canonical order ties them (no portable order exists between two instances of one class) without that tie being
    /// evidence of equivalence. Binders of different classes are keyed apart. No binder member is called.
    /// </summary>
    [TestMethod]
    public void DifferentBinderInstances_AreDistinct()
    {
        var first = new R4CountingBinder("twin", throwOnBind: true);
        var second = new R4CountingBinder("twin", throwOnBind: true);
        var other = new R4OtherBinder();
        DynamicExpression x = Dynamic(first, P);
        DynamicExpression y = Dynamic(second, P);
        DynamicExpression z = Dynamic(other, P);

        Assert.IsFalse(ExpressionComparer.StructuralEqualsRaw(x, y));
        Assert.IsFalse(ExpressionComparer.Default.Equals(x, y));
        Assert.IsFalse(ExpressionComparer.Default.Equals(y, x));
        Assert.AreEqual(0, ExpressionCanonicalOrder.Compare(x, y, []), "Same binder class: tie (documented limitation).");
        Assert.IsFalse(ExpressionComparer.Default.Equals(x, z));
        int forward = ExpressionCanonicalOrder.Compare(x, z, []);
        Assert.AreNotEqual(0, forward);
        Assert.AreEqual(-Math.Sign(forward), Math.Sign(ExpressionCanonicalOrder.Compare(z, x, [])));
        Assert.AreEqual(0, first.TotalCalls + second.TotalCalls);
    }

    /// <summary>Different delegate types, a changed argument order and a different argument are all distinguished.</summary>
    [TestMethod]
    public void DelegateTypeAndArguments_AreDistinguished()
    {
        var binder = new R4CountingBinder("distinct", throwOnBind: true);
        DynamicExpression baseline = Dynamic(binder, P, Q);
        DynamicExpression otherDelegate = Expression.MakeDynamic(typeof(R4DynamicDelegate), binder, P, Q);
        DynamicExpression swapped = Dynamic(binder, Q, P);
        DynamicExpression otherResult = DoubleDynamic(binder, P, Q);

        foreach ((Expression other, string name, bool keysSeparate) in new[]
        {
            ((Expression)otherDelegate, "delegate type", true),
            (swapped, "argument order", false),
            (otherResult, "result type", true),
        })
        {
            Assert.IsFalse(ExpressionComparer.StructuralEqualsRaw(baseline, other), name);
            Assert.IsFalse(ExpressionComparer.Default.Equals(baseline, other), name);
            Assert.IsFalse(ExpressionComparer.Default.Equals(other, baseline), name);
            if (!keysSeparate) continue;

            int forward = ExpressionCanonicalOrder.Compare(baseline, other, []);
            Assert.AreNotEqual(0, forward, name);
            Assert.AreEqual(-Math.Sign(forward), Math.Sign(ExpressionCanonicalOrder.Compare(other, baseline, [])), name);
        }

        Assert.AreEqual(0, binder.TotalCalls);
    }

    /// <summary>
    /// Bound parameters in the arguments are compared up to alpha-renaming; a bound and a free occurrence, or swapped
    /// declarations, are distinguished; a captured outer variable must be the same binding on both sides.
    /// </summary>
    [TestMethod]
    public void BoundAndFreeParameters_FollowTheScopeModel()
    {
        var binder = new R4CountingBinder("scope", throwOnBind: true);
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        ParameterExpression b = Expression.Parameter(typeof(double), "b");
        ParameterExpression c = Expression.Parameter(typeof(double), "c");
        ParameterExpression d = Expression.Parameter(typeof(double), "d");

        Expression left = Expression.Lambda(Dynamic(binder, a, b, P), a, b);
        Expression renamed = Expression.Lambda(Dynamic(binder, c, d, P), c, d);
        Expression swapped = Expression.Lambda(Dynamic(binder, a, b, P), b, a);
        Expression freeInstead = Expression.Lambda(Dynamic(binder, a, Q, P), a, b);
        Expression otherCapture = Expression.Lambda(Dynamic(binder, a, b, Q), a, b);

        Assert.IsTrue(ExpressionComparer.Default.Equals(left, renamed));
        Assert.AreEqual(ExpressionComparer.Default.GetHashCode(left), ExpressionComparer.Default.GetHashCode(renamed));
        Assert.AreEqual(0, ExpressionCanonicalOrder.Compare(left, renamed, []));
        Assert.IsFalse(ExpressionComparer.Default.Equals(left, swapped));
        Assert.IsFalse(ExpressionComparer.Default.Equals(left, freeInstead));
        Assert.IsFalse(ExpressionComparer.Default.Equals(left, otherCapture));
        Assert.AreEqual(0, binder.TotalCalls);
    }

    // ------------------------------------------------------------------------------------------
    // A5: effects and execution.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// A dynamic operation is observable: <c>d - d</c>, <c>d * 0</c>, <c>d + d</c> and <c>2 * d + 3 * d</c> keep every evaluation,
    /// in order, and argument side effects keep their order; simplification never binds, and the compiled results execute the
    /// same rules, the same number of times, as the source.
    /// </summary>
    [TestMethod]
    public void DynamicOperations_AreNeverCancelledMergedOrReordered()
    {
        var binder = new R4CountingBinder("effect");
        Expression Traced(string name, Expression value) => Expression.Call(R4DynamicRuntime.TraceMethod, Expression.Constant(name), value);
        Expression d = DoubleDynamic(binder, Traced("arg1", Leaf(P)), Traced("arg2", Expression.Constant(2.0)));

        Expression[] bodies =
        [
            Expression.Subtract(d, d),
            Expression.Multiply(d, Expression.Constant(0.0)),
            Expression.Add(d, d),
            Expression.Add(Expression.Multiply(Expression.Constant(2.0), d), Expression.Multiply(Expression.Constant(3.0), d)),
            Expression.Add(DoubleDynamic(binder, Traced("first", P)), DoubleDynamic(new R4CountingBinder("second"), Traced("second", P))),
        ];

        foreach (Expression body in bodies)
        {
            var source = Expression.Lambda<Func<double, double>>(body, P);
            int bindsBefore = binder.BindCalls;
            var simplified = (Expression<Func<double, double>>)new ExpressionSimplifier().Simplify(source);
            Assert.AreEqual(bindsBefore, binder.BindCalls, "Simplification must not bind.");
            AssertSameExecution(source, simplified, 1.5);
        }
    }

    /// <summary>Compiles both lambdas, runs each once on <paramref name="input"/> and compares results and runtime traces.</summary>
    /// <param name="source">The source lambda.</param>
    /// <param name="simplified">The simplified lambda.</param>
    /// <param name="input">The argument.</param>
    private static void AssertSameExecution(Expression<Func<double, double>> source, Expression<Func<double, double>> simplified, double input)
    {
        R4DynamicRuntime.Reset();
        double expected = source.Compile()(input);
        string[] expectedEvents = [.. R4DynamicRuntime.Events];

        R4DynamicRuntime.Reset();
        double actual = simplified.Compile()(input);
        string[] actualEvents = [.. R4DynamicRuntime.Events];

        Assert.AreEqual(expected, actual);
        CollectionAssert.AreEqual(expectedEvents, actualEvents, $"{string.Join(",", expectedEvents)} / {string.Join(",", actualEvents)}");
    }
}
