using System;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Tracing of <see cref="ExpressionOptimiser"/> (<see cref="ExpressionTransformTracing"/>): every visited node is a treatment
/// reported for the optimiser, a rewrite reports the rule that produced it, the outcome distinguishes unchanged, rebuilt,
/// rewritten and boundary nodes, and tracing never changes the result nor the work.
/// </summary>
[TestClass]
public class ExpressionOptimiserTracingTests
{
    /// <summary>The parameter <c>x</c>.</summary>
    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");

    /// <summary>The parameter <c>y</c>.</summary>
    private static readonly ParameterExpression Y = Expression.Parameter(typeof(double), "y");

    /// <summary>The <see cref="int"/> parameter <c>i</c>.</summary>
    private static readonly ParameterExpression I = Expression.Parameter(typeof(int), "i");

    /// <summary>The <see cref="bool"/> parameter <c>b</c>.</summary>
    private static readonly ParameterExpression B = Expression.Parameter(typeof(bool), "b");

    /// <summary>Optimises <paramref name="tree"/> inside a tracing scope.</summary>
    /// <param name="tree">The tree.</param>
    /// <param name="detail">The level of detail.</param>
    /// <returns>The result and the collected events.</returns>
    private static (Expression Result, ExpressionTransformEvent[] Events) OptimiseTraced(Expression tree, ExpressionTraceDetail detail = ExpressionTraceDetail.Standard)
    {
        var collector = new ExpressionTraceCollector();
        Expression result;
        using (ExpressionTransformTracing.Begin(collector, detail))
        {
            result = new ExpressionOptimiser().Optimize(tree);
        }

        return (result, collector.GetEvents());
    }

    /// <summary>Optimises <paramref name="tree"/> inside an outer operation and returns the result with the structural visits.</summary>
    /// <param name="tree">The tree.</param>
    /// <returns>The result and the structural visits.</returns>
    private static (Expression Result, long StructuralVisits) OptimiseCounting(Expression tree)
        => ExpressionResourceBudget.Run(ExpressionResourceLimits.Default, null, null, null, tree, static t =>
        {
            Expression result = new ExpressionOptimiser().Optimize(t);
            return (result, ExpressionResourceBudget.StructuralVisits);
        });

    /// <summary>
    /// <c>x * 1</c> is reported as a root treatment with one child treatment per operand, the rule applied after the children
    /// returned, and an exit whose outcome is <see cref="ExpressionTransformOutcome.RuleApplied"/>.
    /// </summary>
    [TestMethod]
    public void Rewrite_IsReportedWithItsRule()
    {
        ConstantExpression one = Expression.Constant(1.0);
        BinaryExpression product = Expression.Multiply(X, one);
        (Expression result, ExpressionTransformEvent[] events) = OptimiseTraced(product);

        Assert.AreSame(X, result);
        CollectionAssert.AreEqual(
            new[]
            {
                ExpressionTransformEventKind.TransformEnter,
                ExpressionTransformEventKind.TransformEnter,
                ExpressionTransformEventKind.TransformExit,
                ExpressionTransformEventKind.TransformEnter,
                ExpressionTransformEventKind.TransformExit,
                ExpressionTransformEventKind.RuleApplied,
                ExpressionTransformEventKind.TransformExit,
            },
            events.Select(e => e.Kind).ToArray());
        CollectionAssert.AreEqual(Enumerable.Range(1, events.Length).Select(i => (long)i).ToArray(), events.Select(e => e.Sequence).ToArray());
        Assert.IsTrue(events.All(e => e.TransformerType == typeof(ExpressionOptimiser)));
        Assert.AreEqual(1, events.Select(e => e.SessionId).Distinct().Count());

        ExpressionTransformEvent root = events[0];
        Assert.AreSame(product, root.Input);
        Assert.AreEqual(ExpressionType.Multiply, root.NodeType);
        Assert.AreEqual(0, root.Depth);
        Assert.AreEqual(0L, root.ParentOperationId);
        Assert.AreEqual(root.OperationId, events[1].ParentOperationId);
        Assert.AreEqual(1, events[1].Depth);
        Assert.AreSame(X, events[1].Input);
        Assert.AreEqual(ExpressionTransformOutcome.Unchanged, events[2].Outcome);
        Assert.AreSame(one, events[3].Input);

        ExpressionTransformEvent applied = events[5];
        Assert.AreEqual(root.OperationId, applied.OperationId);
        Assert.AreSame(product, applied.Input);
        Assert.AreSame(X, applied.Output);
        Assert.AreEqual("RewriteMultiply", applied.Rule!.Name);
        Assert.AreEqual(ExpressionType.Multiply, applied.Rule.DeclaredNodeType);
        Assert.AreSame(typeof(ExpressionOptimiser), applied.Rule.TransformerType);

        ExpressionTransformEvent exit = events[6];
        Assert.AreEqual(root.OperationId, exit.OperationId);
        Assert.AreEqual(ExpressionTransformOutcome.RuleApplied, exit.Outcome);
        Assert.AreSame(applied.Rule, exit.Rule);
        Assert.AreSame(X, exit.Output);
    }

    /// <summary>Each rule family reports the method implementing it, on the treatment of the rewritten node.</summary>
    [TestMethod]
    public void EveryRuleFamily_ReportsItsRule()
    {
        (string Rule, Expression Tree)[] cases =
        [
            ("RewriteAdd", Expression.Add(X, Expression.Constant(-0.0))),
            ("RewriteAdd", Expression.Add(Expression.Constant(0), I)),
            ("RewriteSubtract", Expression.Subtract(X, Expression.Constant(0.0))),
            ("RewriteMultiply", Expression.Multiply(Expression.Constant(1), I)),
            ("RewriteMultiply", Expression.Multiply(I, Expression.Constant(0))),
            ("RewriteDivide", Expression.Divide(X, Expression.Constant(1.0))),
            ("RewriteConvert", Expression.Convert(X, typeof(double))),
            ("RewriteNegate", Expression.Negate(Expression.Negate(X))),
            ("VisitShortCircuit", Expression.AndAlso(Expression.Constant(false), B)),
            ("VisitShortCircuit", Expression.AndAlso(Expression.Constant(true), B)),
            ("VisitShortCircuit", Expression.OrElse(B, Expression.Constant(false))),
            ("VisitConditional", Expression.Condition(Expression.Constant(true), X, Y)),
            ("VisitBlock", Expression.Block(Expression.Empty(), Expression.Empty(), Expression.Assign(X, Y), X)),
            ("UnwrapBlock", Expression.Block(Expression.Empty(), X)),
        ];

        foreach ((string rule, Expression tree) in cases)
        {
            Expression untraced = new ExpressionOptimiser().Optimize(tree);
            (Expression result, ExpressionTransformEvent[] events) = OptimiseTraced(tree);
            string name = rule + ": " + tree;

            Assert.AreNotSame(tree, result, name);
            Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(untraced, result), name);
            ExpressionTransformEvent applied = events.Single(e => e.Kind == ExpressionTransformEventKind.RuleApplied);
            Assert.AreEqual(rule, applied.Rule!.Name, name);
            Assert.AreSame(tree, applied.Input, name);
            Assert.AreSame(result, applied.Output, name);
            Assert.AreEqual(events[0].OperationId, applied.OperationId, name);
            Assert.AreEqual(ExpressionTransformOutcome.RuleApplied, events[^1].Outcome, name);
            Assert.AreEqual(0, events[^1].Depth, name);
        }
    }

    /// <summary>An unchanged tree reports one balanced treatment per node, every exit unchanged.</summary>
    [TestMethod]
    public void UnchangedTree_ReportsUnchangedTreatments()
    {
        Expression tree = Expression.Lambda<Func<double, double, double>>(Expression.Add(Expression.Multiply(X, Y), Expression.Constant(2.0)), X, Y);
        (Expression result, ExpressionTransformEvent[] events) = OptimiseTraced(tree);

        Assert.AreSame(tree, result);
        ExpressionTransformEvent[] exits = events.Where(e => e.Kind == ExpressionTransformEventKind.TransformExit).ToArray();
        Assert.AreEqual(events.Count(e => e.Kind == ExpressionTransformEventKind.TransformEnter), exits.Length);
        Assert.AreEqual(events.Length, exits.Length * 2, "no event other than the treatments");
        Assert.IsTrue(exits.All(e => e.Outcome == ExpressionTransformOutcome.Unchanged));
        Assert.IsTrue(exits.All(e => ReferenceEquals(e.Input, e.Output)));
    }

    /// <summary>A node rebuilt around a rewritten child, without a rule of its own, reports <see cref="ExpressionTransformOutcome.Rebuilt"/>.</summary>
    [TestMethod]
    public void NodeAroundARewrite_IsRebuilt()
    {
        BinaryExpression sum = Expression.Add(Expression.Multiply(X, Expression.Constant(1.0)), Y);
        (Expression result, ExpressionTransformEvent[] events) = OptimiseTraced(sum);

        var rebuilt = (BinaryExpression)result;
        Assert.AreSame(X, rebuilt.Left);
        Assert.AreSame(Y, rebuilt.Right);
        ExpressionTransformEvent exit = events[^1];
        Assert.AreEqual(ExpressionTransformEventKind.TransformExit, exit.Kind);
        Assert.AreSame(sum, exit.Input);
        Assert.AreSame(result, exit.Output);
        Assert.AreEqual(ExpressionTransformOutcome.Rebuilt, exit.Outcome);
        Assert.IsNull(exit.Rule);
        Assert.AreEqual(1, events.Count(e => e.Kind == ExpressionTransformEventKind.RuleApplied));
    }

    /// <summary>A quote is a boundary: its treatment exits with <see cref="ExpressionTransformOutcome.Boundary"/> and its body is not visited.</summary>
    [TestMethod]
    public void Quote_IsABoundary()
    {
        Expression quote = Expression.Quote(Expression.Lambda<Func<double, double>>(Expression.Multiply(X, Expression.Constant(1.0)), X));
        (Expression result, ExpressionTransformEvent[] events) = OptimiseTraced(quote);

        Assert.AreSame(quote, result);
        Assert.AreEqual(2, events.Length);
        Assert.AreEqual(ExpressionTransformOutcome.Boundary, events[1].Outcome);
    }

    /// <summary>An extension node is a treatment without node type, returned unchanged.</summary>
    [TestMethod]
    public void ExtensionNode_IsReportedWithoutNodeType()
    {
        var extension = new OpaqueNode();
        Expression tree = Expression.Add(extension, Expression.Constant(-0.0));
        (Expression result, ExpressionTransformEvent[] events) = OptimiseTraced(tree);

        Assert.AreSame(tree, result, "an opaque operand blocks the identity");
        ExpressionTransformEvent enter = events.Single(e => e.Kind == ExpressionTransformEventKind.TransformEnter && ReferenceEquals(e.Input, extension));
        Assert.IsNull(enter.NodeType);
        ExpressionTransformEvent exit = events.Single(e => e.Kind == ExpressionTransformEventKind.TransformExit && e.OperationId == enter.OperationId);
        Assert.IsNull(exit.NodeType);
        Assert.AreEqual(ExpressionTransformOutcome.Unchanged, exit.Outcome);
    }

    /// <summary>
    /// Tracing is observational: on a corpus covering every rule family, the traced result is the untraced one and the structural
    /// visits are identical; the detailed level reports exactly the standard events.
    /// </summary>
    [TestMethod]
    public void Tracing_DoesNotChangeResultsOrWork()
    {
        ParameterExpression v = Expression.Variable(typeof(double), "v");
        Expression[] corpus =
        [
            Expression.Add(Expression.Multiply(X, Expression.Constant(1.0)), Expression.Constant(-0.0)),
            Expression.Lambda<Func<int, int>>(Expression.Add(Expression.Multiply(I, Expression.Constant(0)), Expression.Negate(Expression.Negate(I))), I),
            Expression.Block([v], Expression.Empty(), Expression.Assign(v, Expression.Divide(X, Expression.Constant(1.0))), v),
            Expression.Condition(Expression.AndAlso(Expression.Constant(true), B), X, Expression.Condition(Expression.Constant(false), Y, X)),
            Expression.Call(typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!, Expression.Subtract(X, Expression.Constant(0.0))),
            Expression.MemberInit(Expression.New(typeof(System.Runtime.CompilerServices.StrongBox<double>)),
                Expression.Bind(typeof(System.Runtime.CompilerServices.StrongBox<double>).GetField("Value")!, Expression.Multiply(Expression.Constant(1.0), X))),
            Expression.TryCatch(Expression.Convert(X, typeof(double)), Expression.Catch(typeof(Exception), Expression.Block(Expression.Empty(), Y))),
        ];

        foreach (Expression tree in corpus)
        {
            (Expression untraced, long untracedVisits) = OptimiseCounting(tree);
            var collector = new ExpressionTraceCollector();
            (Expression Result, long StructuralVisits) traced;
            using (ExpressionTransformTracing.Begin(collector))
            {
                traced = OptimiseCounting(tree);
            }

            Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(untraced, traced.Result), tree.ToString());
            Assert.AreEqual(untracedVisits, traced.StructuralVisits, tree.ToString());

            ExpressionTransformEvent[] standard = collector.GetEvents();
            ExpressionTransformEvent[] detailed = OptimiseTraced(tree, ExpressionTraceDetail.Detailed).Events;
            CollectionAssert.AreEqual(standard.Select(e => (e.Kind, e.Outcome, e.Depth, e.Rule?.Id)).ToArray(), detailed.Select(e => (e.Kind, e.Outcome, e.Depth, e.Rule?.Id)).ToArray(), tree.ToString());
        }
    }

    /// <summary>Without a tracing scope no state is attached and a scope opened elsewhere receives nothing.</summary>
    [TestMethod]
    public void NoScope_ReportsNothing()
    {
        var collector = new ExpressionTraceCollector();
        using (ExpressionTransformTracing.Begin(collector))
        {
        }

        Assert.AreSame(X, new ExpressionOptimiser().Optimize(Expression.Multiply(X, Expression.Constant(1.0))));
        Assert.AreEqual(0, collector.GetEvents().Length);
    }

    /// <summary>
    /// An optimisation run inside a traced operation joins its trace: its treatments are nested in the same session, under the
    /// enclosing operation's numbering.
    /// </summary>
    [TestMethod]
    public void NestedOptimisation_JoinsTheTraceOfTheRunningOperation()
    {
        var collector = new ExpressionTraceCollector();
        using (ExpressionTransformTracing.Begin(collector))
        {
            ExpressionResourceBudget.Run(ExpressionResourceLimits.Default, null, null, null, 0, static _ =>
            {
                new ExpressionOptimiser().Optimize(Expression.Multiply(X, Expression.Constant(1.0)));
                new ExpressionOptimiser().Optimize(Expression.Add(Y, Expression.Constant(-0.0)));
                return 0;
            });
        }

        ExpressionTransformEvent[] events = collector.GetEvents();
        Assert.AreEqual(1, events.Select(e => e.SessionId).Distinct().Count());
        CollectionAssert.AreEqual(Enumerable.Range(1, events.Length).Select(i => (long)i).ToArray(), events.Select(e => e.Sequence).ToArray());
        Assert.AreEqual(2, events.Count(e => e.Kind == ExpressionTransformEventKind.RuleApplied));
        Assert.AreEqual(2, events.Count(e => e.Kind == ExpressionTransformEventKind.TransformExit && e.Depth == 0));
    }

    /// <summary>A plain extension node of type <see cref="double"/>.</summary>
    private sealed class OpaqueNode : Expression
    {
        /// <inheritdoc/>
        public override ExpressionType NodeType => ExpressionType.Extension;

        /// <inheritdoc/>
        public override Type Type => typeof(double);
    }
}
