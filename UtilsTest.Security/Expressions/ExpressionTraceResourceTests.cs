using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;
using static UtilsTest.Security.Expressions.ExpressionResourceTestTrees;

namespace UtilsTest.Security.Expressions;

/// <summary>
/// Tracing of the expression engine (<see cref="ExpressionTransformTracing"/>) keeps the resource contract: a stack refusal stays
/// controlled under tracing on a small stack (no tracer runs on the exhausted stack), and a tracer can never run the engine
/// inside the operation it observes.
/// </summary>
[TestClass]
public class ExpressionTraceResourceTests
{
    /// <summary>Upper bound of the worker threads.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <summary>The small worker stack of the stack-guard tests.</summary>
    private const int SmallStack = 256 * 1024;

    /// <summary>A depth far beyond any stack measured in R0.</summary>
    private const int HostileDepth = 100_000;

    /// <summary>
    /// A hostile depth traced on a 256 KiB thread ends with the same controlled Stack refusal as without tracing; the refusal
    /// and every treatment it unwound are delivered after the stack unwound, in order, and the thread keeps working.
    /// </summary>
    [TestMethod]
    public void TracedStackRefusal_OnSmallStack_StaysControlled()
    {
        Expression negate = NegateChain(HostileDepth);
        RunOnThread(() =>
        {
            var collector = new ExpressionTraceCollector(capacity: 1_000_000);
            var failureStackStates = new List<bool>();
            var tracer = new RecordingTracer(collector, failureStackStates);
            using (ExpressionTraceScope scope = ExpressionTransformTracing.Begin(tracer))
            {
                ExpressionResourceLimitException refusal = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpressionSimplifier(ExpressionResourceLimits.Unlimited).Simplify(negate));
                Assert.AreEqual(ExpressionResourceLimitKind.Stack, refusal.Kind);
                Assert.IsInstanceOfType<InsufficientExecutionStackException>(refusal.InnerException);
                Assert.IsNull(scope.TracerException);
            }

            Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
            ExpressionTransformEvent[] events = collector.GetEvents();
            Assert.AreEqual(0, collector.DroppedEvents);
            CollectionAssert.AreEqual(Enumerable.Range(1, events.Length).Select(i => (long)i).ToArray(), events.Select(e => e.Sequence).ToArray());

            ExpressionTransformEvent limit = events.Single(e => e.Kind == ExpressionTransformEventKind.ResourceLimitReached);
            Assert.AreEqual(ExpressionResourceLimitKind.Stack, limit.ResourceLimit);
            int enters = events.Count(e => e.Kind == ExpressionTransformEventKind.TransformEnter);
            int exits = events.Count(e => e.Kind == ExpressionTransformEventKind.TransformExit);
            Assert.AreEqual(enters - exits, events.Count(e => e.Kind == ExpressionTransformEventKind.TransformFailed));
            Assert.AreEqual(0, events[^1].Depth, "the root treatment fails last");

            // Every failure was delivered once the stack had unwound: the callbacks never ran on the exhausted stack.
            Assert.AreEqual(enters - exits + 1, failureStackStates.Count);
            Assert.IsTrue(failureStackStates.All(static sufficient => sufficient));

            // The same thread keeps working normally after the refusal.
            Assert.IsTrue(ExpressionComparer.Default.Equals(CallChain(10), CallChain(10)));
        }, SmallStack, Timeout);
    }

    /// <summary>
    /// A tracer forwarding to a collector and recording, for every failure event, whether it runs with a sufficient stack (the
    /// failure events of a stack refusal must be delivered after the stack unwound, never on the exhausted stack).
    /// </summary>
    private sealed class RecordingTracer(ExpressionTraceCollector collector, List<bool> failureStackStates) : IExpressionTransformTracer
    {
        /// <inheritdoc/>
        public void OnEvent(in ExpressionTransformEvent traceEvent)
        {
            if (traceEvent.Kind is ExpressionTransformEventKind.TransformFailed or ExpressionTransformEventKind.ResourceLimitReached)
            {
                failureStackStates.Add(System.Runtime.CompilerServices.RuntimeHelpers.TryEnsureSufficientExecutionStack());
            }

            collector.OnEvent(traceEvent);
        }
    }

    /// <summary>
    /// A tracer cannot start any engine operation from its callback (simplification, comparison, hash, transformation,
    /// substitution): every attempt is refused before it starts, the traced operation keeps its result and its work, and the
    /// engine works normally once the callback returned.
    /// </summary>
    [TestMethod]
    public void TracerCallback_CannotRunTheEngine()
    {
        Expression probe = Expression.Add(P, Expression.Constant(0.0));
        Func<object>[] attempts =
        [
            () => new ExpressionSimplifier().Simplify(probe),
            () => ExpressionComparer.Default.Equals(probe, Expression.Add(P, Expression.Constant(0.0))),
            () => ExpressionComparer.Default.GetHashCode(probe),
            () => new RecursiveTransformer(ExpressionResourceLimits.Default).Transform(probe),
            () => new RecursiveTransformer(ExpressionResourceLimits.Default).Replace(probe, P, Expression.Constant(1.0)),
        ];

        Expression tree = EqualCoefficientChain(6);
        (Expression Result, long Visits) untraced = SimplifyCounting(tree);
        for (int index = 0; index < attempts.Length; index++)
        {
            Func<object> attempt = attempts[index];
            var refusals = new List<Exception>();
            var tracer = new AttemptingTracer(attempt, refusals);
            using ExpressionTraceScope scope = ExpressionTransformTracing.Begin(tracer);
            (Expression Result, long Visits) traced = SimplifyCounting(tree);

            Assert.IsInstanceOfType<InvalidOperationException>(scope.TracerException, $"attempt {index}");
            Assert.AreEqual(1, tracer.Calls, "the scope stops tracing after the refused call");
            Assert.AreEqual(untraced.Result.ToString(), traced.Result.ToString());
            Assert.AreEqual(untraced.Visits, traced.Visits);
            Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
        }

        Assert.AreSame(P, new ExpressionSimplifier().Simplify(probe));
    }

    /// <summary>Simplifies inside an outer operation and returns the result with the transformation visits.</summary>
    /// <param name="tree">The tree.</param>
    /// <returns>The result and the visits.</returns>
    private static (Expression Result, long Visits) SimplifyCounting(Expression tree)
        => ExpressionResourceBudget.Run(ExpressionResourceLimits.Default, null, null, null, tree, static e =>
        {
            Expression result = new ExpressionSimplifier().Simplify(e);
            return (result, ExpressionResourceBudget.TransformVisits);
        });

    /// <summary>A tracer that tries to run the engine from its callback.</summary>
    private sealed class AttemptingTracer(Func<object> attempt, List<Exception> refusals) : IExpressionTransformTracer
    {
        /// <summary>Gets the number of calls.</summary>
        public int Calls { get; private set; }

        /// <inheritdoc/>
        public void OnEvent(in ExpressionTransformEvent traceEvent)
        {
            Calls++;
            attempt();
            refusals.Add(new InvalidOperationException("The engine ran inside the callback."));
        }
    }
}
