using System;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Security.Expressions;

/// <summary>
/// ETC R4: a call holding an extension node, kept instead of rebuilt when its children are unchanged, stays under the shared
/// resource session. A resource limit reached while such a call is simplified is raised, not absorbed; the operation is closed
/// and the same simplifier works again, without any call into the extension node.
/// </summary>
[TestClass]
public class ExpressionCallRebuildResourceTests
{
    /// <summary>The free parameter of the samples.</summary>
    private static readonly ParameterExpression P = Expression.Parameter(typeof(double), "p");

    /// <summary><see cref="CallTestMethods.Identity"/>.</summary>
    private static readonly MethodInfo IdentityMethod = typeof(CallTestMethods).GetMethod(nameof(CallTestMethods.Identity))!;

    /// <summary><see cref="CallTestMethods.Pair"/>.</summary>
    private static readonly MethodInfo PairMethod = typeof(CallTestMethods).GetMethod(nameof(CallTestMethods.Pair))!;

    /// <summary>The methods called by the samples.</summary>
    public static class CallTestMethods
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
    }

    /// <summary>
    /// A strict extension node: every member a caller could use is counted and, once <see cref="Armed"/>, throws, whoever the
    /// caller is, a framework factory included. It is built while unarmed, so the factories that build the source tree can
    /// validate it, then armed before the engine runs.
    /// </summary>
    private sealed class StrictExtension : Expression
    {
        /// <summary>Gets or sets whether every counted member throws.</summary>
        public bool Armed { get; set; }

        /// <summary>Gets the number of calls made into this node while it was armed.</summary>
        public int ArmedCalls { get; private set; }

        /// <summary>Counts a call while armed and returns the exception to throw, or <see langword="null"/> while unarmed.</summary>
        /// <param name="member">The member that was called.</param>
        /// <returns>The exception to throw, or <see langword="null"/>.</returns>
        private InvalidOperationException? Trap(string member)
        {
            if (!Armed) return null;
            ArmedCalls++;
            return new InvalidOperationException($"{member} was called on an armed strict extension node.");
        }

        /// <inheritdoc/>
        public override ExpressionType NodeType => Trap(nameof(NodeType)) is { } exception ? throw exception : ExpressionType.Extension;

        /// <inheritdoc/>
        public override Type Type => Trap(nameof(Type)) is { } exception ? throw exception : typeof(double);

        /// <inheritdoc/>
        public override bool CanReduce => Trap(nameof(CanReduce)) is { } exception ? throw exception : true;

        /// <inheritdoc/>
        public override Expression Reduce() => Trap(nameof(Reduce)) is { } exception ? throw exception : Constant(3.0);

        /// <inheritdoc/>
        protected override Expression VisitChildren(ExpressionVisitor visitor) => Trap(nameof(VisitChildren)) is { } exception ? throw exception : this;

        /// <inheritdoc/>
        protected override Expression Accept(ExpressionVisitor visitor) => Trap(nameof(Accept)) is { } exception ? throw exception : base.Accept(visitor);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => Trap(nameof(Equals)) is { } exception ? throw exception : ReferenceEquals(this, obj);

        /// <inheritdoc/>
        public override int GetHashCode() => Trap(nameof(GetHashCode)) is { } exception ? throw exception : 0;

        /// <inheritdoc/>
        public override string ToString() => Trap(nameof(ToString)) is { } exception ? throw exception : "strict";
    }

    /// <summary>Arms <paramref name="node"/>, runs <paramref name="action"/>, then disarms it.</summary>
    /// <param name="node">The strict node.</param>
    /// <param name="action">The engine operation.</param>
    private static void Armed(StrictExtension node, Action action)
    {
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

    /// <summary>
    /// A resource limit reached while a call holding an extension node is simplified is raised, not absorbed; the operation is
    /// closed and the same instance works again, without reading the extension node.
    /// </summary>
    [TestMethod]
    public void ResourceLimit_IsRaised_AndTheInstanceWorksAgain()
    {
        var node = new StrictExtension();
        Expression tree = Expression.Call(PairMethod, node, Expression.Call(IdentityMethod, Expression.Call(IdentityMethod, P)));
        Expression small = Expression.Call(IdentityMethod, node);
        var limited = new ExpressionSimplifier(ExpressionResourceLimits.Unlimited with { MaxTransformVisits = 2 });

        Armed(node, () => Assert.ThrowsExactly<ExpressionResourceLimitException>(() => limited.Simplify(tree)));
        Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
        Assert.AreEqual(0, node.ArmedCalls, "the refusal reads nothing of the extension node");

        Armed(node, () => Assert.AreSame(small, limited.Simplify(small)));
        Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
        Assert.AreEqual(0, node.ArmedCalls, "the unchanged call is kept without reading the extension node");
    }
}
