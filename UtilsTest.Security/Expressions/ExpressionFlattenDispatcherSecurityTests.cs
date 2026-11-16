using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using static UtilsTest.Security.Expressions.ExpressionResourceTestTrees;

namespace UtilsTest.Security.Expressions;

/// <summary>
/// Security invariants of the <see cref="FlattenRuleDispatcher"/>: each dispatch is counted (refused exactly one visit above the
/// budget), checks the stack, reads nothing of an extension node or of an impersonating reflection object beyond what the
/// rules read, leaves no state after an exception, is bound to its own instance (concurrent calls and instances sharing one
/// plan), and a dispatching loop ends in a controlled refusal.
/// </summary>
[TestClass]
public class ExpressionFlattenDispatcherSecurityTests
{
    /// <summary>The stack size of the worker threads.</summary>
    private const int SmallStack = 256 * 1024;

    /// <summary>Limits without any bound.</summary>
    private static readonly ExpressionResourceLimits NoLimits = ExpressionResourceLimits.Unlimited;

    /// <summary>A second free parameter.</summary>
    private static readonly ParameterExpression Q = Expression.Parameter(typeof(double), "q");

    /// <summary>A transformer whose additive flatten rule dispatches through its group's dispatcher.</summary>
    private sealed class DispatchProbe : ExpressionTransformer
    {
        /// <summary>Initializes the probe.</summary>
        /// <param name="limits">The resource limits.</param>
        public DispatchProbe(ExpressionResourceLimits limits)
            : base(limits)
        {
        }

        /// <summary>Gets or sets the flatten rule's body.</summary>
        public Func<DispatchProbe, Expression, FlattenRuleDispatcher, Expression?> Rule { get; set; } = static (self, source, dispatcher) => dispatcher(source, out Expression? r) ? r : source;

        /// <summary>Gets or sets the additive signature rule's body (<see langword="null"/> result: decline).</summary>
        public Func<DispatchProbe, BinaryExpression, Expression?> AddRule { get; set; } = static (_, _) => null;

        /// <summary>Gets the number of additive signature rule calls on this instance.</summary>
        public int AddCalls;

        /// <summary>Gets the dispatchers received.</summary>
        public List<FlattenRuleDispatcher> Dispatchers { get; } = [];

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <summary>Exposes <see cref="ExpressionTransformer.TransformCore(Expression)"/>.</summary>
        /// <param name="e">The node.</param>
        /// <returns>Its transformation.</returns>
        public Expression Core(Expression e) => TransformCore(e);

        /// <inheritdoc/>
        protected override Expression PrepareExpression(Expression e) => TransformCore(e);

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => CopyExpression(e, parameters);

        /// <summary>The additive signature rule.</summary>
        /// <param name="e">The node.</param>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>The configured result.</returns>
        [ExpressionSignature(ExpressionType.Add)]
        private Expression? AddSignature(BinaryExpression e, Expression left, Expression right)
        {
            Interlocked.Increment(ref AddCalls);
            return AddRule(this, e);
        }

        /// <summary>The additive flatten rule.</summary>
        /// <param name="source">The root.</param>
        /// <param name="terms">The terms.</param>
        /// <param name="dispatcher">The dispatcher.</param>
        /// <returns>The configured result.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract)]
        private Expression? Additions(Expression source, FlattenedTerm[] terms, FlattenRuleDispatcher dispatcher)
        {
            lock (Dispatchers) Dispatchers.Add(dispatcher);
            return Rule(this, source, dispatcher);
        }
    }

    /// <summary>Transforms <paramref name="e"/> inside an outer operation and returns the visit counters of the whole call.</summary>
    /// <param name="transformer">The transformer.</param>
    /// <param name="e">The expression.</param>
    /// <returns>The transformation and structural visits.</returns>
    private static (long Transform, long Structural) Count(ExpressionTransformer transformer, Expression e)
        => ExpressionResourceBudget.Run(NoLimits, null, null, null, (transformer, e), static state =>
        {
            state.transformer.Transform(state.e);
            return (ExpressionResourceBudget.TransformVisits, ExpressionResourceBudget.StructuralVisits);
        });

    /// <summary>
    /// Each dispatch costs exactly one transformation visit, counted before any rule runs: a budget of exactly the measured
    /// cost is accepted, one visit less is refused, and nothing is left behind (a later call on the same instance is normal).
    /// </summary>
    [TestMethod]
    public void Dispatch_IsCountedOnce_AndRefusedOneVisitAboveTheBudget()
    {
        Expression chain = Expression.Add(P, Q);
        Func<DispatchProbe, Expression, FlattenRuleDispatcher, Expression?> twice = static (self, source, dispatcher) =>
        {
            dispatcher(source, out _);
            dispatcher(source, out _);
            return source;
        };
        (long once, _) = Count(new DispatchProbe(NoLimits), chain);
        (long withTwo, _) = Count(new DispatchProbe(NoLimits) { Rule = twice }, chain);
        Assert.AreEqual(2, once, "one visit for the chain, one for the dispatch");
        Assert.AreEqual(3, withTwo);

        var exact = new DispatchProbe(NoLimits with { MaxTransformVisits = withTwo }) { Rule = twice };
        exact.Transform(chain);
        Assert.AreEqual(2, exact.AddCalls);

        var tooSmall = new DispatchProbe(NoLimits with { MaxTransformVisits = withTwo - 1 }) { Rule = twice };
        Assert.ThrowsExactly<ExpressionResourceLimitException>(() => tooSmall.Transform(chain));
        Assert.AreEqual(1, tooSmall.AddCalls, "the second dispatch is refused before its rule runs");
        Assert.IsFalse(ExpressionResourceBudget.IsActive);

        tooSmall.Rule = static (_, source, _) => source;
        Assert.AreSame(chain, tooSmall.Transform(chain));
    }

    /// <summary>
    /// An exception of a dispatched rule propagates with the invocation contract of the traversal (wrapped in a
    /// <see cref="TargetInvocationException"/>, then by the flatten rule's own invocation) and leaves no state: no operation,
    /// no admission flag, no fallback link; the same instance then transforms normally.
    /// </summary>
    [TestMethod]
    public void RuleException_LeavesNoState()
    {
        var probe = new DispatchProbe(NoLimits) { AddRule = static (_, _) => throw new InvalidOperationException("rule") };
        Expression chain = Expression.Add(Expression.Add(P, Q), P);

        TargetInvocationException outer = Assert.ThrowsExactly<TargetInvocationException>(() => probe.Transform(chain));
        Assert.IsInstanceOfType<TargetInvocationException>(outer.InnerException);
        Assert.IsInstanceOfType<InvalidOperationException>(outer.InnerException!.InnerException);
        Assert.IsFalse(ExpressionResourceBudget.IsActive);

        probe.AddRule = static (_, _) => null;
        probe.Rule = static (_, _, _) => null;
        probe.AddCalls = 0;
        Expression result = probe.Transform(chain);
        Assert.AreEqual(chain.ToString(), result.ToString());
        Assert.AreEqual(0, probe.AddCalls, "the declined chain is finalized without its covered rules, as after a normal call");
    }

    /// <summary>
    /// Extension nodes (ETC R4) are rejected by type tests before any member is read; as an operand of a dispatched node, an
    /// armed extension node is never read by the dispatch itself.
    /// </summary>
    [TestMethod]
    public void Dispatch_NeverReadsExtensionNodes()
    {
        var extension = new ArmedExtension();
        Expression holder = Expression.Add(P, extension);
        Exception? rejected = null;
        bool appliedOnOperand = true;
        var probe = new DispatchProbe(NoLimits)
        {
            Rule = (self, source, dispatcher) =>
            {
                extension.Armed = true;
                try
                {
                    try
                    {
                        dispatcher(extension, out _);
                    }
                    catch (ArgumentException exception)
                    {
                        rejected = exception;
                    }

                    appliedOnOperand = dispatcher(holder, out _);
                }
                finally
                {
                    extension.Armed = false;
                }

                return source;
            },
        };

        probe.Transform(Expression.Add(P, Q));

        Assert.IsNotNull(rejected);
        Assert.IsFalse(appliedOnOperand);
        Assert.AreEqual(0, extension.Calls);
        Assert.AreEqual(1, probe.AddCalls, "the rule ran on the node holding the extension operand");
    }

    /// <summary>An extension node whose every member throws once armed.</summary>
    private sealed class ArmedExtension : Expression
    {
        /// <summary>Gets or sets whether the members throw.</summary>
        public bool Armed { get; set; }

        /// <summary>Gets the number of calls made while armed.</summary>
        public int Calls { get; private set; }

        /// <summary>Counts a call and throws when armed.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="value">The value returned while not armed.</param>
        /// <returns><paramref name="value"/>.</returns>
        private T Hit<T>(T value)
        {
            if (!Armed) return value;
            Calls++;
            throw new InvalidOperationException("A member of an extension node was called.");
        }

        /// <inheritdoc/>
        public override ExpressionType NodeType => Hit(ExpressionType.Extension);

        /// <inheritdoc/>
        public override Type Type => Hit(typeof(double));

        /// <inheritdoc/>
        public override bool CanReduce => Hit(false);

        /// <inheritdoc/>
        public override string ToString() => Hit("extension");
    }

    /// <summary>A plain addition, impersonated as a user-defined operator method.</summary>
    /// <param name="x">The left operand.</param>
    /// <param name="y">The right operand.</param>
    /// <returns>The sum.</returns>
    public static double Plus(double x, double y) => x + y;

    /// <summary>
    /// S5 A-10: a dispatched node whose operator method is an impersonating reflection object is matched and dispatched without
    /// reading any member of that object (the dispatch does not rebuild the node); only the rules may read it.
    /// </summary>
    [TestMethod]
    public void Dispatch_NeverReadsImpersonatingReflectionObjects()
    {
        var method = new ImpersonatingMethod(typeof(ExpressionFlattenDispatcherSecurityTests).GetMethod(nameof(Plus))!);
        Expression userOperator = Expression.Add(P, Q, method);
        var probe = new DispatchProbe(NoLimits)
        {
            Rule = (self, source, dispatcher) =>
            {
                method.Trap.Reset();
                method.Trap.Armed = true;
                try
                {
                    dispatcher(userOperator, out _);
                }
                finally
                {
                    // The framework keeps the method in its parameter cache: left armed, it would throw in later tests.
                    method.Trap.Armed = false;
                }

                return source;
            },
        };

        probe.Transform(Expression.Add(P, Q));

        Assert.AreEqual(1, probe.AddCalls);
        Assert.AreEqual(0, method.Trap.ArmedCalls + method.Trap.TypeReads);
    }

    /// <summary>
    /// Shared DAG: a node object crossed twice by one chain is dispatched once per occurrence, each dispatch counted, with the
    /// same rule result.
    /// </summary>
    [TestMethod]
    public void SharedNode_IsDispatchedPerOccurrence()
    {
        Expression shared = Expression.Add(P, Q);
        Expression chain = Expression.Add(shared, shared);
        var results = new List<Expression?>();
        var probe = new DispatchProbe(NoLimits)
        {
            AddRule = static (_, e) => Expression.Constant(1.0),
            Rule = (self, source, dispatcher) =>
            {
                foreach (Expression occurrence in new[] { ((BinaryExpression)source).Left, ((BinaryExpression)source).Right })
                {
                    dispatcher(occurrence, out Expression? r);
                    results.Add(r);
                }

                return source;
            },
        };

        (long transform, _) = Count(probe, chain);

        Assert.AreEqual(3, transform, "the chain, then one dispatch per occurrence");
        Assert.AreEqual(2, probe.AddCalls);
        Assert.AreEqual(2, results.Count);
        Assert.IsTrue(results.All(static r => r is ConstantExpression { Value: 1.0 }));
    }

    /// <summary>
    /// Concurrent operations on one instance, and instances sharing one plan, each use the dispatcher of their own instance:
    /// every rule call lands on the instance whose operation ran it, and each instance keeps a single dispatcher per group.
    /// </summary>
    [TestMethod]
    public void ConcurrentCallsAndSharedPlans_StayBoundToTheirInstance()
    {
        var first = new DispatchProbe(NoLimits);
        var second = new DispatchProbe(NoLimits);
        const int Iterations = 400;

        Parallel.For(0, Iterations, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
        {
            DispatchProbe probe = i % 2 == 0 ? first : second;
            Expression output = probe.Transform(Expression.Add(P, Expression.Constant((double)i)));
            Assert.AreEqual($"(p + {i})", output.ToString());
        });

        Assert.AreEqual(Iterations / 2, first.AddCalls);
        Assert.AreEqual(Iterations / 2, second.AddCalls);
        Assert.AreEqual(1, first.Dispatchers.Distinct().Count(), "a benign race keeps one dispatcher per group and instance");
        Assert.AreEqual(1, second.Dispatchers.Distinct().Count());
        Assert.AreNotSame(first.Dispatchers[0], second.Dispatchers[0]);
    }

    /// <summary>
    /// A rule that re-enters the traversal from every dispatch (dispatch, rule, new chain, dispatch, …) ends in a controlled
    /// refusal on a small stack: the stack guard or the visit budget, never a process crash, and no state is left.
    /// </summary>
    [TestMethod]
    public void DispatchingLoop_IsAControlledRefusal_OnASmallStack()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var probe = new DispatchProbe(ExpressionResourceLimits.Default)
            {
                AddRule = static (self, e) => self.Core(Expression.Add(e.Left, e.Right)),
            };
            try
            {
                probe.Transform(Expression.Add(P, Q));
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            if (ExpressionResourceBudget.IsActive) failure = new InvalidOperationException("an operation leaked");
        }, SmallStack);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(120)));

        Assert.IsNotNull(failure);
        Assert.IsTrue(
            failure is ExpressionResourceLimitException || failure is InsufficientExecutionStackException,
            failure.GetType().FullName + ": " + failure.Message);
    }
}
