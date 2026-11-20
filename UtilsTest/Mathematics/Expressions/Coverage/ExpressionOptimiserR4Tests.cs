using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>
/// ETC R4 invariants applied to <see cref="ExpressionOptimiser"/>: an extension node is passed through without any call into
/// it (<c>Accept</c>, <c>VisitChildren</c>, <c>CanReduce</c>, <c>Reduce</c>, <c>Equals</c>, <c>GetHashCode</c>,
/// <c>ToString</c>, nor its <c>NodeType</c>/<c>Type</c> getters from the optimiser itself), and a dynamic operation stays a
/// dynamic operation whose binder is never called.
/// </summary>
[TestClass]
public class ExpressionOptimiserR4Tests
{
    /// <summary>A <see cref="double"/> parameter.</summary>
    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");

    /// <summary>An armed extension node at the root, under an identity, and as the operand of every rule family is never called by the optimiser.</summary>
    [TestMethod]
    public void HostileExtension_IsNeverCalled()
    {
        Func<Expression, Expression>[] parents =
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

        foreach (Func<Expression, Expression> parent in parents)
        {
            var hostile = new R4HostileExtension(X);
            Expression tree = parent(hostile);
            hostile.ResetCalls();
            hostile.Armed = true;

            Expression optimized = new ExpressionOptimiser().Optimize(tree);

            hostile.Armed = false;
            Assert.AreEqual(0, hostile.EngineCalls, tree.GetType().Name);
            foreach (string member in new[] { "Accept", "VisitChildren", "CanReduce", "Reduce", "Equals", "GetHashCode", "ToString" })
            {
                Assert.IsFalse(hostile.Calls.ContainsKey(member), member + " was called");
            }

            Assert.IsNotNull(optimized);
        }
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

    /// <summary>The optimised parents keep the extension node itself, by reference, when they are rewritten around it.</summary>
    [TestMethod]
    public void HostileExtension_IsKeptByReference()
    {
        var hostile = new R4HostileExtension(X);
        Assert.AreSame(hostile, new ExpressionOptimiser().Optimize(hostile));

        Expression lambda = Expression.Lambda<Func<double, double>>(Expression.Add(Expression.Multiply(X, Expression.Constant(1.0)), hostile), X);
        var optimized = (LambdaExpression)new ExpressionOptimiser().Optimize(lambda);
        var sum = (BinaryExpression)optimized.Body;
        Assert.AreSame(X, sum.Left);
        Assert.AreSame(hostile, sum.Right);
    }

    /// <summary>
    /// A dynamic operation stays a dynamic operation (the framework visitor would reduce it to a call-site invocation), its
    /// arguments are optimised, and its binder is never called.
    /// </summary>
    [TestMethod]
    public void DynamicOperation_IsKeptAndItsBinderNeverCalled()
    {
        var binder = new R4CountingBinder("add", throwOnBind: true);
        Expression dynamic = Expression.MakeDynamic(typeof(R4DynamicDelegate), binder, Expression.Multiply(X, Expression.Constant(1.0)), Expression.Add(X, Expression.Constant(-0.0)));
        Expression tree = Expression.Lambda<Func<double, object>>(dynamic, X);

        var optimized = (LambdaExpression)new ExpressionOptimiser().Optimize(tree);

        var rewritten = (DynamicExpression)optimized.Body;
        Assert.AreSame(binder, rewritten.Binder);
        Assert.AreSame(typeof(R4DynamicDelegate), rewritten.DelegateType);
        Assert.AreSame(X, rewritten.Arguments[0]);
        Assert.AreSame(X, rewritten.Arguments[1]);
        Assert.AreEqual(0, binder.TotalCalls);
    }

    /// <summary>An unchanged dynamic operation is returned by reference and its compiled form runs exactly like the source.</summary>
    [TestMethod]
    public void UnchangedDynamicOperation_IsReturnedAsIs()
    {
        var binder = new R4CountingBinder("sum");
        Expression dynamic = Expression.MakeDynamic(typeof(Func<CallSite, double, double, object>), binder, X, X);
        Assert.AreSame(dynamic, new ExpressionOptimiser().Optimize(dynamic));

        var tree = Expression.Lambda<Func<double, object>>(dynamic, X);
        var optimized = (Expression<Func<double, object>>)new ExpressionOptimiser().Optimize(tree);
        Assert.AreSame(tree, optimized);
        R4DynamicRuntime.Reset();
        Assert.AreEqual(4.0, optimized.Compile()(2.0));
        CollectionAssert.AreEqual(new[] { "sum:4" }, R4DynamicRuntime.Events);
    }
}
