using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using UtilsTest.Mathematics.Expressions.Coverage;

namespace UtilsTest.Security.Expressions;

/// <summary>
/// ETC R4 security invariants applied to <see cref="ExpressionOptimiser"/>: processing a hostile extension node never runs its
/// overridable code (<c>Accept</c>, <c>VisitChildren</c>, <c>CanReduce</c>, <c>Reduce</c>, <c>Equals</c>, <c>GetHashCode</c>,
/// <c>ToString</c>, nor its <c>NodeType</c>/<c>Type</c> getters from the engine itself), traced or not, and a dynamic
/// operation's binder is never called.
/// </summary>
[TestClass]
public class ExpressionOptimiserR4SecurityTests
{
    /// <summary>A <see cref="double"/> parameter.</summary>
    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");

    /// <summary>The members of <see cref="R4HostileExtension"/> the engine must never call.</summary>
    private static readonly string[] ForbiddenMembers = ["Accept", "VisitChildren", "CanReduce", "Reduce", "Equals", "GetHashCode", "ToString"];

    /// <summary>Builds the parents of an armed extension node: the root, an identity, and the operand of every rule family.</summary>
    /// <returns>The parent builders.</returns>
    private static Func<Expression, Expression>[] Parents() =>
    [
        e => e,
        e => Expression.Multiply(e, Expression.Constant(1.0)),
        e => Expression.Add(Expression.Constant(-0.0), e),
        e => Expression.Negate(Expression.Negate(e)),
        e => Expression.Convert(e, typeof(double)),
        e => Expression.Block(Expression.Empty(), e),
        e => Expression.Condition(Expression.Constant(true), e, X),
        e => Expression.Condition(Expression.Constant(false), e, X),
        e => Expression.Lambda<Func<double, double>>(Expression.Add(e, X), X),
        e => Expression.Multiply(Expression.Convert(e, typeof(int)), Expression.Constant(0)),
        e => Expression.Quote(Expression.Lambda<Func<double>>(e)),
    ];

    /// <summary>An armed extension node at the root, under an identity, and as the operand of every rule family is never called by the optimiser.</summary>
    [TestMethod]
    public void HostileExtension_IsNeverCalled()
    {
        foreach (Func<Expression, Expression> parent in Parents())
        {
            AssertNeverCalled(parent, static tree => new ExpressionOptimiser().Optimize(tree));
        }
    }

    /// <summary>
    /// Under a tracing scope (standard and detailed), the treatments and events of the optimiser never call an armed extension
    /// node either: its events carry no node type, and the tracer receives the node by reference only.
    /// </summary>
    [TestMethod]
    public void HostileExtension_IsNeverCalledUnderTracing()
    {
        foreach (ExpressionTraceDetail detail in new[] { ExpressionTraceDetail.Standard, ExpressionTraceDetail.Detailed })
        {
            foreach (Func<Expression, Expression> parent in Parents())
            {
                var collector = new ExpressionTraceCollector();
                AssertNeverCalled(parent, tree =>
                {
                    using (ExpressionTransformTracing.Begin(collector, detail))
                    {
                        return new ExpressionOptimiser().Optimize(tree);
                    }
                });

                foreach (ExpressionTransformEvent traceEvent in collector.GetEvents())
                {
                    if (traceEvent.Input is R4HostileExtension) Assert.IsNull(traceEvent.NodeType);
                }
            }
        }
    }

    /// <summary>Builds a tree around an armed hostile node, optimises it, and asserts that the engine never called the node.</summary>
    /// <param name="parent">Builds the tree around the node.</param>
    /// <param name="optimise">Optimises the tree.</param>
    private static void AssertNeverCalled(Func<Expression, Expression> parent, Func<Expression, Expression> optimise)
    {
        var hostile = new R4HostileExtension(X);
        Expression tree = parent(hostile);
        hostile.ResetCalls();
        hostile.Armed = true;
        Expression optimized;
        try
        {
            optimized = optimise(tree);
        }
        finally
        {
            hostile.Armed = false;
        }

        Assert.AreEqual(0, hostile.EngineCalls, tree.GetType().Name);
        foreach (string member in ForbiddenMembers)
        {
            Assert.IsFalse(hostile.Calls.ContainsKey(member), member + " was called");
        }

        Assert.IsNotNull(optimized);
    }

    /// <summary>
    /// The shared effect analysis the optimiser consults never reads the type of an extension operand (a conversion, a
    /// <c>TypeAs</c> and a field access of an extension receiver read it before the A10-C audit): such an evaluation is
    /// observable, exactly as before.
    /// </summary>
    [TestMethod]
    public void EffectAnalysis_NeverReadsTheTypeOfAnExtensionOperand()
    {
        var hostile = new R4HostileExtension(X);
        var receiver = new TypedExtension(typeof(StrongBox<double>));
        Expression[] trees =
        [
            Expression.Convert(hostile, typeof(int)),
            Expression.TypeAs(hostile, typeof(object)),
            Expression.Field(receiver, typeof(StrongBox<double>).GetField(nameof(StrongBox<double>.Value))!),
        ];

        foreach (Expression tree in trees)
        {
            hostile.ResetCalls();
            hostile.Armed = true;
            receiver.Armed = true;
            ExpressionEffects effects;
            try
            {
                effects = ExpressionEffectAnalysis.Analyze(tree);
            }
            finally
            {
                hostile.Armed = false;
                receiver.Armed = false;
            }

            Assert.AreEqual(ExpressionEffects.Observable, effects & ExpressionEffects.Observable, tree.NodeType.ToString());
            Assert.AreEqual(0, hostile.EngineCalls, tree.NodeType.ToString());
        }
    }

    /// <summary>
    /// A dynamic operation stays a dynamic operation (the framework visitor would reduce it to a call-site invocation), its
    /// arguments are optimised, and its binder is never called, traced or not.
    /// </summary>
    [TestMethod]
    public void DynamicOperation_IsKeptAndItsBinderNeverCalled()
    {
        var binder = new R4CountingBinder("add", throwOnBind: true);
        Expression dynamic = Expression.MakeDynamic(typeof(R4DynamicDelegate), binder, Expression.Multiply(X, Expression.Constant(1.0)), Expression.Add(X, Expression.Constant(-0.0)));
        Expression tree = Expression.Lambda<Func<double, object>>(dynamic, X);

        var optimized = (LambdaExpression)new ExpressionOptimiser().Optimize(tree);
        LambdaExpression traced;
        using (ExpressionTransformTracing.Begin(new ExpressionTraceCollector(), ExpressionTraceDetail.Detailed))
        {
            traced = (LambdaExpression)new ExpressionOptimiser().Optimize(tree);
        }

        foreach (LambdaExpression result in new[] { optimized, traced })
        {
            var rewritten = (DynamicExpression)result.Body;
            Assert.AreSame(binder, rewritten.Binder);
            Assert.AreSame(typeof(R4DynamicDelegate), rewritten.DelegateType);
            Assert.AreSame(X, rewritten.Arguments[0]);
            Assert.AreSame(X, rewritten.Arguments[1]);
        }

        Assert.AreEqual(0, binder.TotalCalls);
    }

    /// <summary>An extension node of a given type whose <c>Type</c> getter throws once armed.</summary>
    private sealed class TypedExtension : Expression
    {
        /// <summary>The reported type.</summary>
        private readonly Type _type;

        /// <summary>Initializes the node.</summary>
        /// <param name="type">The reported type.</param>
        public TypedExtension(Type type) => _type = type;

        /// <summary>Gets or sets whether reading <see cref="Type"/> or <see cref="NodeType"/> throws.</summary>
        public bool Armed { get; set; }

        /// <inheritdoc/>
        public override Type Type => Armed ? throw new InvalidOperationException("Type was read on an opaque node.") : _type;

        /// <inheritdoc/>
        public override ExpressionType NodeType => Armed ? throw new InvalidOperationException("NodeType was read on an opaque node.") : ExpressionType.Extension;
    }
}
