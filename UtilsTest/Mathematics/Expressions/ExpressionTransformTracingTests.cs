using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;
using UtilsTest.Mathematics.Expressions.Coverage;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Injectable tracing of the expression transformation engine (<see cref="ExpressionTransformTracing"/>): events, hierarchy,
/// rule identity, failures, isolation of scopes and tracers, bounded collection, export and cycle analysis.
/// </summary>
[TestClass]
public class ExpressionTransformTracingTests
{
    /// <summary>The parameter <c>x</c>.</summary>
    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");

    /// <summary>The parameter <c>y</c>.</summary>
    private static readonly ParameterExpression Y = Expression.Parameter(typeof(double), "y");

    /// <summary>Builds a <see cref="double"/> constant.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The constant.</returns>
    private static ConstantExpression C(double value) => Expression.Constant(value);

    /// <summary>Runs <paramref name="action"/> inside a tracing scope with a fresh collector.</summary>
    /// <param name="action">The traced code.</param>
    /// <param name="detail">The level of detail.</param>
    /// <returns>The collected events.</returns>
    private static ExpressionTransformEvent[] Trace(Action action, ExpressionTraceDetail detail = ExpressionTraceDetail.Standard)
    {
        var collector = new ExpressionTraceCollector();
        using (ExpressionTransformTracing.Begin(collector, detail))
        {
            action();
        }

        return collector.GetEvents();
    }

    /// <summary>Simplifies <paramref name="e"/> inside an outer operation and returns the result with the visit counters.</summary>
    /// <param name="simplifier">The simplifier.</param>
    /// <param name="e">The expression.</param>
    /// <returns>The result and the transformation and structural visits.</returns>
    private static (Expression Result, long TransformVisits, long StructuralVisits) SimplifyCounting(ExpressionSimplifier simplifier, Expression e)
        => ExpressionResourceBudget.Run(ExpressionResourceLimits.Default, null, null, null, (simplifier, e), static state =>
        {
            Expression result = state.simplifier.Simplify(state.e);
            return (result, ExpressionResourceBudget.TransformVisits, ExpressionResourceBudget.StructuralVisits);
        });

    /// <summary>A corpus of trees covering rules, canonicalization, lambdas, blocks, calls and nested comparisons.</summary>
    /// <returns>The trees.</returns>
    private static Expression[] Corpus()
    {
        ParameterExpression v = Expression.Variable(typeof(double), "v");
        return
        [
            Expression.Add(Expression.Multiply(X, C(1)), C(0)),
            Expression.Lambda<Func<double, double, double>>(Expression.Subtract(Expression.Add(Expression.Multiply(X, C(2)), Expression.Multiply(Y, C(3))), Expression.Multiply(X, C(2))), X, Y),
            Expression.Add(Expression.Add(Expression.Multiply(C(2), X), Expression.Multiply(C(3), X)), Expression.Multiply(C(4), Y)),
            Expression.Subtract(Expression.Subtract(Expression.Subtract(X, Y), C(2)), Expression.Negate(Y)),
            Expression.Block([v], Expression.Assign(v, Expression.Multiply(X, C(1))), Expression.Add(v, C(0))),
            Expression.Call(typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!, Expression.Add(X, C(0))),
            Expression.Divide(Expression.Multiply(X, Y), Y),
            Expression.Condition(Expression.GreaterThan(X, C(0)), Expression.Add(X, X), Expression.Subtract(Y, Y)),
            Expression.Power(Expression.Multiply(X, C(1)), C(2)),
        ];
    }

    /// <summary>
    /// Tracing is purely observational: on every tree of the corpus, the result and the visit counters of a traced operation
    /// equal those of the untraced one (no extra transformation, comparison or structural walk).
    /// </summary>
    [TestMethod]
    public void Tracing_DoesNotChangeResultsOrWork()
    {
        var simplifier = new ExpressionSimplifier();
        foreach (Expression e in Corpus())
        {
            var untraced = SimplifyCounting(simplifier, e);
            (Expression Result, long TransformVisits, long StructuralVisits) traced = default;
            ExpressionTransformEvent[] events = Trace(() => traced = SimplifyCounting(simplifier, e));

            Assert.IsTrue(events.Length > 0, ExpressionTraceFormatter.Format(e));
            Assert.AreEqual(untraced.Result.ToString(), traced.Result.ToString());
            Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(untraced.Result, traced.Result));
            Assert.AreEqual(untraced.TransformVisits, traced.TransformVisits, "transform visits");
            Assert.AreEqual(untraced.StructuralVisits, traced.StructuralVisits, "structural visits");
        }
    }

    /// <summary>Without a scope, no operation is traced and no tracing state is attached to the running operation.</summary>
    [TestMethod]
    public void NoScope_NoTracingState()
    {
        Assert.IsNull(ExpressionTransformTracing.Current);
        bool sawTrace = ExpressionResourceBudget.Run(ExpressionResourceLimits.Default, null, null, null, 0, static _ => ExpressionResourceBudget.ActiveTrace is not null);
        Assert.IsFalse(sawTrace);

        var collector = new ExpressionTraceCollector();
        using (ExpressionTransformTracing.Begin(collector))
        {
            Assert.IsTrue(ExpressionResourceBudget.Run(ExpressionResourceLimits.Default, null, null, null, 0, static _ => ExpressionResourceBudget.ActiveTrace is not null));
        }

        // The state is dropped with the operation, and nothing is traced after the scope.
        Assert.IsFalse(ExpressionResourceBudget.Run(ExpressionResourceLimits.Default, null, null, null, 0, static _ => ExpressionResourceBudget.ActiveTrace is not null));
        new ExpressionSimplifier().Simplify(Expression.Add(X, C(0)));
        Assert.AreEqual(0, collector.TotalEvents);
    }

    /// <summary>
    /// The events of one operation form a well-nested hierarchy: contiguous sequence numbers, every exit closes the innermost
    /// open treatment, every enter's parent is the open treatment, and depths follow the nesting.
    /// </summary>
    [TestMethod]
    public void Events_AreOrderedAndWellNested()
    {
        foreach (Expression e in Corpus())
        {
            ExpressionTransformEvent[] events = Trace(() => new ExpressionSimplifier().Simplify(e));
            AssertWellNested(events);
        }
    }

    /// <summary>Checks the sequence numbers and the treatment nesting of a complete trace.</summary>
    /// <param name="events">The events of one or several sessions.</param>
    private static void AssertWellNested(IReadOnlyList<ExpressionTransformEvent> events)
    {
        foreach (IGrouping<long, ExpressionTransformEvent> session in events.GroupBy(static e => e.SessionId))
        {
            ExpressionTransformEvent[] list = session.ToArray();
            for (int i = 0; i < list.Length; i++) Assert.AreEqual(i + 1, list[i].Sequence);

            var open = new Stack<ExpressionTransformEvent>();
            foreach (ExpressionTransformEvent e in list)
            {
                switch (e.Kind)
                {
                    case ExpressionTransformEventKind.TransformEnter:
                        Assert.AreEqual(open.Count == 0 ? 0 : open.Peek().OperationId, e.ParentOperationId);
                        Assert.AreEqual(open.Count, e.Depth);
                        open.Push(e);
                        break;
                    case ExpressionTransformEventKind.TransformExit:
                    case ExpressionTransformEventKind.TransformFailed:
                        Assert.AreEqual(open.Pop().OperationId, e.OperationId);
                        break;
                    case ExpressionTransformEventKind.ResourceLimitReached:
                        break;
                    default:
                        Assert.AreEqual(open.Peek().OperationId, e.OperationId, e.Kind.ToString());
                        break;
                }
            }

            Assert.AreEqual(0, open.Count);
        }
    }

    /// <summary>
    /// A rule application is reported after the rule returned, with the prepared node and the rule's result, and the rule is
    /// identified by the shared metadata of the plan.
    /// </summary>
    [TestMethod]
    public void RuleApplied_CarriesRuleInputAndOutput()
    {
        Expression product = Expression.Multiply(X, C(1));
        ExpressionTransformEvent[] events = Trace(() => new ExpressionSimplifier().Simplify(Expression.Add(product, C(0))));

        ExpressionTransformEvent multiplication = events.Single(e => e.Kind == ExpressionTransformEventKind.RuleApplied && e.Rule!.Name == "MultiplicationWithZeroOrOne");
        Assert.AreSame(X, multiplication.Output);
        Assert.AreEqual(ExpressionType.Multiply, multiplication.NodeType);
        Assert.AreEqual(typeof(ExpressionSimplifier), multiplication.TransformerType);
        Assert.AreEqual(typeof(ExpressionSimplifier), multiplication.Rule!.TransformerType);
        Assert.AreEqual(ExpressionType.Multiply, multiplication.Rule.DeclaredNodeType);
        StringAssert.StartsWith(multiplication.Rule.Id, "Utils.Mathematics.Expressions.ExpressionSimplifier.MultiplicationWithZeroOrOne(");
        Assert.AreEqual(multiplication.Rule.Id, multiplication.Rule.ToString());
        StringAssert.Contains(multiplication.Rule.DisplayName, "(BinaryExpression, Expression, ConstantExpression)", "overloaded rule names carry their parameter types");

        ExpressionTransformEvent exit = events.Single(e => e.Kind == ExpressionTransformEventKind.TransformExit && e.OperationId == multiplication.OperationId);
        Assert.AreEqual(ExpressionTransformOutcome.RuleApplied, exit.Outcome);
        Assert.AreSame(multiplication.Rule, exit.Rule);
        Assert.AreSame(product, exit.Input);
        Assert.IsTrue(exit.Sequence > multiplication.Sequence);

        // One identity per rule and per transformer type, shared by every operation.
        ExpressionTransformEvent[] again = Trace(() => new ExpressionSimplifier().Simplify(Expression.Multiply(Y, C(1))));
        Assert.AreSame(multiplication.Rule, again.First(e => e.Kind == ExpressionTransformEventKind.RuleApplied).Rule);
    }

    /// <summary>
    /// A treatment requested by a rule (the rule calls <c>TransformCore</c> again) is a child of the node the rule runs on, and
    /// its enter event names that rule; a child prepared by the traversal names no rule.
    /// </summary>
    [TestMethod]
    public void NestedTreatment_KeepsTheRequestingRule()
    {
        // 0 - x -> TransformCore(-x) (SubstractionWithZero).
        ExpressionTransformEvent[] events = Trace(() => new ExpressionSimplifier().Simplify(Expression.Subtract(C(0), X)));

        ExpressionTransformEvent root = events.First(e => e.Kind == ExpressionTransformEventKind.TransformEnter);
        ExpressionTransformEvent negate = events.Single(e => e.Kind == ExpressionTransformEventKind.TransformEnter && e.NodeType == ExpressionType.Negate);
        Assert.AreEqual(root.OperationId, negate.ParentOperationId);
        Assert.AreEqual("SubstractionWithZero", negate.Rule!.Name);
        Assert.AreEqual(1, negate.Depth);

        ExpressionTransformEvent child = events.First(e => e.Kind == ExpressionTransformEventKind.TransformEnter && e.ParentOperationId == root.OperationId && e.NodeType == ExpressionType.Constant);
        Assert.IsNull(child.Rule);

        // The rule application of the root is reported after its nested treatment returned.
        ExpressionTransformEvent applied = events.Single(e => e.Kind == ExpressionTransformEventKind.RuleApplied && e.OperationId == root.OperationId);
        ExpressionTransformEvent nestedExit = events.Single(e => e.Kind == ExpressionTransformEventKind.TransformExit && e.OperationId == negate.OperationId);
        Assert.IsTrue(applied.Sequence > nestedExit.Sequence);
    }

    /// <summary>
    /// The outcome of a treatment distinguishes an unchanged node, a structural reconstruction, a canonicalization by the
    /// finalization, a rule result and a boundary.
    /// </summary>
    [TestMethod]
    public void Outcomes_DistinguishReconstructionCanonicalizationAndRules()
    {
        var lambda = Expression.Lambda<Func<double, double>>(Expression.Add(Expression.Multiply(X, C(1)), C(0)), X);
        ExpressionTransformEvent[] events = Trace(() => new ExpressionSimplifier().Simplify(lambda));
        ExpressionTransformEvent Exit(Expression input) => events.Single(e => e.Kind == ExpressionTransformEventKind.TransformExit && ReferenceEquals(e.Input, input));

        Assert.AreEqual(ExpressionTransformOutcome.Rebuilt, Exit(lambda).Outcome, "the lambda is only rebuilt around its new body");
        Assert.AreEqual(ExpressionTransformOutcome.RuleApplied, Exit(lambda.Body).Outcome);
        Assert.IsTrue(events.Where(e => e.Kind == ExpressionTransformEventKind.TransformExit && e.NodeType == ExpressionType.Parameter)
            .All(e => e.Outcome == ExpressionTransformOutcome.Unchanged));

        // (1 + x) + 2 becomes 3 + x through the canonicalization of the finalization (constant aggregation): neither a rule
        // nor a reconstruction. The inner 1 + x is already canonical: its finalization only rebuilds it. The chain is of type
        // short, which the flatten rule of the simplifier declines (docs/expressions/SimplifierFlatten.md): a double chain
        // of this shape is canonicalized by that rule, without any finalization.
        ParameterExpression shortX = Expression.Parameter(typeof(short), "x");
        Expression inner = Expression.Add(Expression.Constant((short)1), shortX);
        Expression sum = Expression.Add(inner, Expression.Constant((short)2));
        ExpressionTransformEvent[] canonical = Trace(() => new ExpressionSimplifier().Simplify(sum));
        ExpressionTransformEvent sumExit = canonical.Single(e => e.Kind == ExpressionTransformEventKind.TransformExit && ReferenceEquals(e.Input, sum));
        ExpressionTransformEvent finalize = canonical.Single(e => e.Kind == ExpressionTransformEventKind.Finalize && e.OperationId == sumExit.OperationId);
        Assert.AreEqual(ExpressionTransformOutcome.Finalized, finalize.Outcome);
        Assert.AreEqual("3 + x", ExpressionTraceFormatter.Format(finalize.Output));
        Assert.AreEqual(ExpressionTransformOutcome.Finalized, sumExit.Outcome);
        Assert.IsNull(sumExit.Rule);
        Assert.AreEqual(ExpressionTransformOutcome.Rebuilt, canonical.Single(e => e.Kind == ExpressionTransformEventKind.TransformExit && ReferenceEquals(e.Input, inner)).Outcome);

        // A quote is a boundary of the default simplifier: returned as is.
        UnaryExpression quote = Expression.Quote(Expression.Lambda<Func<double, double>>(Expression.Add(X, C(0)), X));
        ExpressionTransformEvent[] quoted = Trace(() => new ExpressionSimplifier().Simplify(quote));
        Assert.AreEqual(ExpressionTransformOutcome.Boundary, quoted.Single(e => e.Kind == ExpressionTransformEventKind.TransformExit).Outcome);
    }

    /// <summary>
    /// A rule exception leaves one failure event per treatment it crosses, naming the rule that was running, and the
    /// exception reaching the caller is the very one an untraced operation throws (same type and wrapping).
    /// </summary>
    [TestMethod]
    public void RuleException_IsTracedWithTheRunningRule()
    {
        ParameterExpression i = Expression.Parameter(typeof(int), "i");
        Expression division = Expression.Add(i, Expression.Divide(i, Expression.Constant(0)));
        Exception untraced = Assert.ThrowsExactly<TargetInvocationException>(() => new ExpressionSimplifier().Simplify(division));

        Exception? traced = null;
        ExpressionTransformEvent[] events = Trace(() => traced = Assert.ThrowsExactly<TargetInvocationException>(() => new ExpressionSimplifier().Simplify(division)));
        Assert.AreEqual(untraced.InnerException!.GetType(), traced!.InnerException!.GetType());
        Assert.IsInstanceOfType<DivideByZeroException>(traced.InnerException);

        ExpressionTransformEvent[] failures = events.Where(e => e.Kind == ExpressionTransformEventKind.TransformFailed).ToArray();
        Assert.AreEqual(2, failures.Length, "the division and the enclosing sum");
        Assert.AreEqual("DivideWithZeroOrOne", failures[0].Rule!.Name);
        Assert.AreEqual(ExpressionType.Divide, failures[0].NodeType);
        Assert.IsNull(failures[1].Rule, "the sum was preparing its operands, no rule was running");
        Assert.IsFalse(events.Any(e => e.Kind == ExpressionTransformEventKind.ResourceLimitReached));
        AssertWellNested(events);
    }

    /// <summary>
    /// A budget refusal is reported once, by the treatment it interrupts, before the failures it unwinds; an input refused by
    /// validation is reported without treatment. The refusal reaching the caller is unchanged.
    /// </summary>
    [TestMethod]
    public void ResourceRefusals_AreTraced()
    {
        Expression chain = X;
        for (int k = 0; k < 40; k++) chain = Expression.Add(Expression.Multiply(chain, C(1)), C(0));

        var tight = new ExpressionSimplifier(ExpressionResourceLimits.Default with { MaxTransformVisits = 50 });
        ExpressionResourceLimitException? refusal = null;
        ExpressionTransformEvent[] events = Trace(() => refusal = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => tight.Simplify(chain)));
        Assert.AreEqual(ExpressionResourceLimitKind.Work, refusal!.Kind);

        ExpressionTransformEvent[] limits = events.Where(e => e.Kind == ExpressionTransformEventKind.ResourceLimitReached).ToArray();
        Assert.AreEqual(1, limits.Length);
        Assert.AreEqual(ExpressionResourceLimitKind.Work, limits[0].ResourceLimit);
        Assert.AreSame(refusal, limits[0].Exception);
        int limitIndex = System.Array.IndexOf(events, limits[0]);
        Assert.AreEqual(ExpressionTransformEventKind.TransformFailed, events[limitIndex + 1].Kind);
        Assert.AreEqual(limits[0].OperationId, events[limitIndex + 1].OperationId);
        AssertWellNested(events);

        // Validation refuses the input before any treatment.
        var shallow = new ExpressionSimplifier(ExpressionResourceLimits.Default with { MaxDepth = 3 });
        ExpressionTransformEvent[] validation = Trace(() => Assert.ThrowsExactly<ExpressionResourceLimitException>(() => shallow.Simplify(chain)));
        ExpressionTransformEvent only = validation.Single();
        Assert.AreEqual(ExpressionTransformEventKind.ResourceLimitReached, only.Kind);
        Assert.AreEqual(ExpressionResourceLimitKind.Depth, only.ResourceLimit);
        Assert.AreEqual(0, only.OperationId);
    }

    /// <summary>A transformer whose rule transforms the same form again without end (a strict one-step cycle).</summary>
    private sealed class LoopingTransformer : ExpressionTransformer
    {
        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <summary>Rewrites <c>-x</c> to a new <c>-x</c> and transforms it again.</summary>
        /// <param name="e">The negation.</param>
        /// <returns>Never returns.</returns>
        [ExpressionSignature(ExpressionType.Negate)]
        public Expression Loop(UnaryExpression e) => TransformCore(Expression.Negate(e.Operand));

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => CopyExpression(e, parameters);
    }

    /// <summary>A transformer whose rule grows the form at every step (an endless expansion).</summary>
    private sealed class ExpandingTransformer : ExpressionTransformer
    {
        /// <summary>Initializes the transformer with a large work budget, so that the stack ends the recursion.</summary>
        public ExpandingTransformer() : base(ExpressionResourceLimits.Default with { MaxTransformVisits = 100_000_000 })
        {
        }

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <summary>Rewrites <c>a * b</c> to <c>a * (b + 1)</c> and transforms it again.</summary>
        /// <param name="e">The product.</param>
        /// <returns>Never returns.</returns>
        [ExpressionSignature(ExpressionType.Multiply)]
        public Expression Grow(BinaryExpression e) => TransformCore(Expression.Multiply(e.Left, Expression.Add(e.Right, Expression.Constant(1.0))));

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => CopyExpression(e, parameters);
    }

    /// <summary>
    /// A derived transformer is traced like the simplifier; a stack refusal is reported once with every failure it unwinds,
    /// and the cycle analyzer finds the strict one-step cycle and the re-entered form.
    /// </summary>
    [TestMethod]
    public void DerivedTransformer_StackRefusal_IsAnalyzedAsStrictCycle()
    {
        Expression negation = Expression.Negate(X);
        var collector = new ExpressionTraceCollector(capacity: 1_000_000);
        using (ExpressionTransformTracing.Begin(collector))
        {
            var refusal = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new LoopingTransformer().Transform(negation));
            Assert.AreEqual(ExpressionResourceLimitKind.Stack, refusal.Kind);
        }

        ExpressionTransformEvent[] events = collector.GetEvents();
        Assert.AreEqual(0, collector.DroppedEvents);
        Assert.IsTrue(events.All(e => e.TransformerType is null || e.TransformerType == typeof(LoopingTransformer)));
        ExpressionTransformEvent limit = events.Single(e => e.Kind == ExpressionTransformEventKind.ResourceLimitReached);
        Assert.AreEqual(ExpressionResourceLimitKind.Stack, limit.ResourceLimit);
        Assert.AreEqual(events.Count(e => e.Kind == ExpressionTransformEventKind.TransformEnter), events.Count(e => e.Kind == ExpressionTransformEventKind.TransformFailed));

        // Every negation the refusal unwinds was running its rule (the deepest one may still have been preparing its operand).
        ExpressionTransformEvent[] negations = events.Where(e => e.Kind == ExpressionTransformEventKind.TransformFailed && e.NodeType == ExpressionType.Negate).ToArray();
        Assert.IsTrue(negations.Length > 100);
        Assert.IsTrue(negations.Skip(1).All(e => e.Rule?.Name == "Loop"));
        AssertWellNested(events);

        ExpressionTraceCycleReport report = ExpressionTraceCycleAnalyzer.Analyze(events);
        ExpressionTraceRecurrence recurrence = report.Recurrences.Single();
        Assert.IsTrue(recurrence.Confirmed);
        Assert.AreSame(negation, recurrence.Ancestor.Input);
        Assert.AreEqual(1, recurrence.Descendant.Depth);
        ExpressionTracePeriod period = report.Periods.Single();
        Assert.AreEqual(ExpressionTraceLoopKind.StrictCycle, period.Kind);
        CollectionAssert.AreEqual(new[] { "Loop" }, period.Steps.ToArray());
        Assert.IsTrue(period.Repetitions > 100);
    }

    /// <summary>An endless expansion is classified as such: no form comes back, the forms grow at every period.</summary>
    [TestMethod]
    public void CycleAnalyzer_FindsExpansion()
    {
        var collector = new ExpressionTraceCollector(capacity: 50_000, ExpressionTraceRetention.KeepFirst);
        using (ExpressionTransformTracing.Begin(collector))
        {
            Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpandingTransformer().Transform(Expression.Multiply(X, Y)));
        }

        ExpressionTraceCycleReport report = ExpressionTraceCycleAnalyzer.Analyze(collector.GetEvents());
        Assert.IsFalse(report.HasRecurrence);
        ExpressionTracePeriod period = report.Periods.Single();
        Assert.AreEqual(ExpressionTraceLoopKind.Expansion, period.Kind);
        CollectionAssert.AreEqual(new[] { "Grow" }, period.Steps.ToArray());
        Assert.IsTrue(period.StartSizes.Zip(period.StartSizes.Skip(1)).All(pair => pair.Second > pair.First));
    }

    /// <summary>
    /// Ordinary simplifications show no recurrence and no non-terminating rule sequence, deep inputs included (a deep input is a
    /// traversal, not a rewriting loop).
    /// </summary>
    [TestMethod]
    public void CycleAnalyzer_NoFindingOnTerminatingTraces()
    {
        // The difference chain re-enters the canonical chain at every level (S5 P16): nested, periodic, but shrinking.
        Expression deepSum = X, deepDifference = X;
        for (int k = 0; k < 60; k++) deepSum = Expression.Add(deepSum, Expression.Parameter(typeof(double), "p" + k));
        for (int k = 0; k < 12; k++) deepDifference = Expression.Subtract(deepDifference, Expression.Multiply(C(k + 2), Expression.Parameter(typeof(double), "q" + k)));

        foreach (Expression e in Corpus().Append(deepSum).Append(deepDifference))
        {
            ExpressionTraceCycleReport report = ExpressionTraceCycleAnalyzer.Analyze(Trace(() => new ExpressionSimplifier().Simplify(e)));
            Assert.IsFalse(report.HasRecurrence, ExpressionTraceFormatter.Format(e));
            Assert.IsFalse(report.Periods.Any(p => p.Kind is ExpressionTraceLoopKind.StrictCycle or ExpressionTraceLoopKind.Expansion or ExpressionTraceLoopKind.Drift),
                ExpressionTraceFormatter.Format(e));
        }
    }

    /// <summary>The rule balance reports how each applied rule changed the size of the forms.</summary>
    [TestMethod]
    public void CycleAnalyzer_BalancesRules()
    {
        ExpressionTransformEvent[] events = Trace(() => new ExpressionSimplifier().Simplify(Expression.Add(Expression.Multiply(X, C(1)), C(0))));
        ExpressionTraceCycleReport report = ExpressionTraceCycleAnalyzer.Analyze(events);
        ExpressionTraceRuleBalance balance = report.RuleBalances.Single(b => b.Rule.Name == "MultiplicationWithZeroOrOne");
        Assert.AreEqual(1, balance.Applications);
        Assert.AreEqual(1, balance.Shrank);
        Assert.AreEqual(-2, balance.NetSizeChange);
        StringAssert.Contains(ExpressionTraceCycleAnalyzer.ToText(report), "MultiplicationWithZeroOrOne");
    }

    /// <summary>
    /// The analyzer compares forms strictly: operand order and parameter identity matter, so two mathematically equivalent or
    /// identically named forms are not taken for a recurrence.
    /// </summary>
    [TestMethod]
    public void CycleAnalyzer_UsesStrictStructuralIdentity()
    {
        ParameterExpression otherX = Expression.Parameter(typeof(double), "x");
        var commuting = new SwappingTransformer();
        var collector = new ExpressionTraceCollector(capacity: 200, ExpressionTraceRetention.KeepFirst);
        using (ExpressionTransformTracing.Begin(collector))
        {
            Assert.ThrowsExactly<ExpressionResourceLimitException>(() => commuting.Transform(Expression.Add(X, otherX)));
        }

        // x + x' -> x' + x -> x + x' ...: the strict identity sees the cycle every two steps, never after one.
        ExpressionTraceCycleReport report = ExpressionTraceCycleAnalyzer.Analyze(collector.GetEvents());
        ExpressionTraceRecurrence recurrence = report.Recurrences[0];
        Assert.AreEqual(2, recurrence.Descendant.Depth - recurrence.Ancestor.Depth);
        Assert.AreEqual(ExpressionTraceLoopKind.StrictCycle, report.Periods.Single().Kind);
        Assert.AreEqual(2, report.Periods.Single().Steps.Count);
    }

    /// <summary>A transformer that swaps the operands of a sum and transforms the result again.</summary>
    private sealed class SwappingTransformer : ExpressionTransformer
    {
        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <summary>Rewrites <c>a + b</c> to <c>b + a</c> and transforms it again (the children are prepared first).</summary>
        /// <param name="e">The sum.</param>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Never returns.</returns>
        [ExpressionSignature(ExpressionType.Add)]
        public Expression Swap(BinaryExpression e, ParameterExpression left, ParameterExpression right) => TransformCore(Expression.Add(right, left));

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => CopyExpression(e, parameters);
    }

    /// <summary>The detailed level reports candidates, rejections with their reason, and the lexical scopes.</summary>
    [TestMethod]
    public void DetailedLevel_ReportsCandidatesRejectionsAndScopes()
    {
        // x + x^2: not a linear chain, so the flatten rule of the simplifier declines it and the rules of the addition run
        // (x + 2, used here before that rule existed, is canonicalized by it without any declined rule).
        ParameterExpression v = Expression.Variable(typeof(double), "v");
        var lambda = Expression.Lambda<Func<double, double>>(
            Expression.Block([v], Expression.Assign(v, Expression.Add(X, Expression.Power(X, C(2)))), Expression.Multiply(v, C(3))), X);

        ExpressionTransformEvent[] standard = Trace(() => new ExpressionSimplifier().Simplify(lambda));
        Assert.IsFalse(standard.Any(e => e.Kind is ExpressionTransformEventKind.RuleCandidate or ExpressionTransformEventKind.RuleRejected
            or ExpressionTransformEventKind.ScopeEnter or ExpressionTransformEventKind.ScopeExit));

        ExpressionTransformEvent[] detailed = Trace(() => new ExpressionSimplifier().Simplify(lambda), ExpressionTraceDetail.Detailed);
        Assert.IsTrue(detailed.Any(e => e.Kind == ExpressionTransformEventKind.RuleCandidate));
        Assert.IsTrue(detailed.Any(e => e.Kind == ExpressionTransformEventKind.RuleRejected && e.Rejection == ExpressionRuleRejection.Declined));
        Assert.IsTrue(detailed.Any(e => e.Kind == ExpressionTransformEventKind.RuleRejected && e.Rejection == ExpressionRuleRejection.ParameterMismatch));
        Assert.AreEqual(1, detailed.Count(e => e.Kind == ExpressionTransformEventKind.ScopeEnter && e.ScopeKind == ExpressionTraceScopeKind.Lambda));
        Assert.AreEqual(1, detailed.Count(e => e.Kind == ExpressionTransformEventKind.ScopeExit && e.ScopeKind == ExpressionTraceScopeKind.Lambda));
        Assert.AreEqual(1, detailed.Count(e => e.Kind == ExpressionTransformEventKind.ScopeEnter && e.ScopeKind == ExpressionTraceScopeKind.Block));
        Assert.AreEqual(standard.Count(e => e.Kind == ExpressionTransformEventKind.RuleApplied), detailed.Count(e => e.Kind == ExpressionTransformEventKind.RuleApplied));
        AssertWellNested(detailed);

        // The effect gate (ETC R1b) refuses every rule of a node whose operands are not inert.
        Expression call = Expression.Call(typeof(Console).GetMethod(nameof(Console.Read))!);
        Expression observable = Expression.Multiply(Expression.Convert(call, typeof(double)), C(1));
        ExpressionTransformEvent[] gated = Trace(() => new ExpressionSimplifier().Simplify(observable), ExpressionTraceDetail.Detailed);
        Assert.IsTrue(gated.Any(e => e.Kind == ExpressionTransformEventKind.RuleRejected && e.Rejection == ExpressionRuleRejection.EffectGate));
    }

    /// <summary>
    /// A tracer that throws never affects the operation: the result is the untraced one, the exception is reported on the
    /// scope, and the scope stops tracing.
    /// </summary>
    [TestMethod]
    public void ThrowingTracer_IsDetachedAndReported()
    {
        Expression e = Corpus()[1];
        Expression expected = new ExpressionSimplifier().Simplify(e);
        var tracer = new DelegateTracer(_ => throw new FormatException("tracer failure"));
        Expression result;
        using (ExpressionTraceScope scope = ExpressionTransformTracing.Begin(tracer))
        {
            result = new ExpressionSimplifier().Simplify(e);
            Assert.IsInstanceOfType<FormatException>(scope.TracerException);
            Assert.IsTrue(scope.IsFaulted);
            Assert.AreEqual(1, tracer.Calls, "tracing stops after the first failure");

            new ExpressionSimplifier().Simplify(e);
            Assert.AreEqual(1, tracer.Calls, "a faulted scope no longer traces");
        }

        Assert.AreEqual(expected.ToString(), result.ToString());
    }

    /// <summary>
    /// A tracer cannot run the engine from its callback: the call is refused before it starts (reported as the tracer's
    /// failure), and the traced operation keeps its result and its work.
    /// </summary>
    [TestMethod]
    public void ReentrantTracer_IsRefused()
    {
        var simplifier = new ExpressionSimplifier();
        Expression e = Corpus()[2];
        var untraced = SimplifyCounting(simplifier, e);
        Expression? inner = null;
        var tracer = new DelegateTracer(_ => inner = simplifier.Simplify(Expression.Add(Y, C(0))));
        using ExpressionTraceScope scope = ExpressionTransformTracing.Begin(tracer);
        var traced = SimplifyCounting(simplifier, e);

        Assert.IsNull(inner);
        Assert.IsInstanceOfType<InvalidOperationException>(scope.TracerException);
        Assert.AreEqual(untraced.Result.ToString(), traced.Result.ToString());
        Assert.AreEqual(untraced.TransformVisits, traced.TransformVisits);
        Assert.AreEqual(untraced.StructuralVisits, traced.StructuralVisits);

        // The refusal left nothing behind: the engine runs normally outside the callback.
        Assert.AreSame(Y, simplifier.Simplify(Expression.Add(Y, C(0))));
    }

    /// <summary>A tracer built from a delegate, counting its calls.</summary>
    private sealed class DelegateTracer(Action<ExpressionTransformEvent> onEvent) : IExpressionTransformTracer
    {
        /// <summary>Gets the number of calls.</summary>
        public int Calls { get; private set; }

        /// <inheritdoc/>
        public void OnEvent(in ExpressionTransformEvent traceEvent)
        {
            Calls++;
            onEvent(traceEvent);
        }
    }

    /// <summary>Nested scopes: the inner one replaces the outer one until it is disposed.</summary>
    [TestMethod]
    public void NestedScopes_ReplaceAndRestore()
    {
        var outer = new ExpressionTraceCollector();
        var inner = new ExpressionTraceCollector();
        using (ExpressionTraceScope outerScope = ExpressionTransformTracing.Begin(outer))
        {
            using (ExpressionTraceScope innerScope = ExpressionTransformTracing.Begin(inner))
            {
                Assert.AreSame(innerScope, ExpressionTransformTracing.Current);
                new ExpressionSimplifier().Simplify(Expression.Add(X, C(0)));
            }

            Assert.AreSame(outerScope, ExpressionTransformTracing.Current);
            new ExpressionSimplifier().Simplify(Expression.Add(X, C(0)));
        }

        Assert.IsNull(ExpressionTransformTracing.Current);
        Assert.AreEqual(1, inner.GetEvents().Select(e => e.SessionId).Distinct().Count());
        Assert.AreEqual(1, outer.GetEvents().Select(e => e.SessionId).Distinct().Count());
        Assert.AreNotEqual(inner.GetEvents()[0].SessionId, outer.GetEvents()[0].SessionId);
    }

    /// <summary>
    /// Scopes are isolated per execution context: concurrent operations on other threads are not traced by a scope they did not
    /// open, a scope flows into the tasks started inside it, and one collector shared by concurrent sessions keeps each
    /// session's events complete and well nested.
    /// </summary>
    [TestMethod]
    public async Task Scopes_AreIsolatedAcrossThreadsAndFlowIntoTasks()
    {
        var simplifier = new ExpressionSimplifier();
        Expression[] corpus = Corpus();
        var shared = new ExpressionTraceCollector();
        var foreign = new ExpressionTraceCollector();
        using var stop = new ManualResetEventSlim();

        // An untraced worker running concurrently with the scope below.
        Task untraced = Task.Run(() =>
        {
            while (!stop.IsSet)
            {
                foreach (Expression e in corpus) simplifier.Simplify(e);
            }
        });

        using (ExpressionTransformTracing.Begin(shared))
        {
            Task[] workers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
            {
                foreach (Expression e in corpus) simplifier.Simplify(e);
            })).ToArray();
            await Task.WhenAll(workers);
        }

        // A scope opened on another thread does not see the shared collector's scope.
        await Task.Run(() =>
        {
            using (ExpressionTransformTracing.Begin(foreign))
            {
                simplifier.Simplify(corpus[0]);
            }
        });

        stop.Set();
        await untraced;

        ExpressionTransformEvent[] events = shared.GetEvents();
        Assert.AreEqual(4 * corpus.Length, events.Select(e => e.SessionId).Distinct().Count(), "one session per traced operation");
        AssertWellNested(events);
        Assert.AreEqual(1, foreign.GetEvents().Select(e => e.SessionId).Distinct().Count());
        Assert.IsFalse(foreign.GetEvents().Select(e => e.SessionId).Intersect(events.Select(e => e.SessionId)).Any());
    }

    /// <summary>
    /// The collector is bounded: reaching its capacity never fails the operation; the first or the last events are kept and the
    /// others counted; <see cref="ExpressionTraceCollector.Clear"/> drops everything.
    /// </summary>
    [TestMethod]
    public void Collector_IsBounded()
    {
        Expression e = Corpus()[2];
        ExpressionTransformEvent[] all = Trace(() => new ExpressionSimplifier().Simplify(e));
        Assert.IsTrue(all.Length > 20);

        var first = new ExpressionTraceCollector(10, ExpressionTraceRetention.KeepFirst);
        var last = new ExpressionTraceCollector(10, ExpressionTraceRetention.KeepLast);
        using (ExpressionTransformTracing.Begin(first)) new ExpressionSimplifier().Simplify(e);
        using (ExpressionTransformTracing.Begin(last)) new ExpressionSimplifier().Simplify(e);

        Assert.AreEqual(all.Length, first.TotalEvents);
        Assert.AreEqual(all.Length - 10, first.DroppedEvents);
        CollectionAssert.AreEqual(Enumerable.Range(1, 10).Select(i => (long)i).ToArray(), first.GetEvents().Select(x => x.Sequence).ToArray());
        CollectionAssert.AreEqual(Enumerable.Range(all.Length - 9, 10).Select(i => (long)i).ToArray(), last.GetEvents().Select(x => x.Sequence).ToArray());
        Assert.AreEqual(ExpressionTransformEventKind.TransformExit, last.GetEvents()[^1].Kind);

        last.Clear();
        Assert.AreEqual(0, last.Count);
        Assert.AreEqual(0, last.TotalEvents);
        Assert.AreEqual(0, last.DroppedEvents);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ExpressionTraceCollector(0));
    }

    /// <summary>The text export nests the treatments and shows each rule with its input and output; the JSON export is valid.</summary>
    [TestMethod]
    public void Export_TextAndJson()
    {
        ExpressionTransformEvent[] events = Trace(() => new ExpressionSimplifier().Simplify(Expression.Add(Expression.Multiply(X, C(1)), C(0))));
        string text = ExpressionTraceExporter.ToText(events);
        StringAssert.StartsWith(text, "Session ");
        StringAssert.Contains(text, "RuleApplied MultiplicationWithZeroOrOne");
        StringAssert.Contains(text, "Before: x * 1");
        StringAssert.Contains(text, "After:  x");
        StringAssert.Contains(text, "\n    #", "nested treatments are indented");
        Assert.IsFalse(text.Contains("Enter Parameter"), "unchanged leaves are hidden by default");
        StringAssert.Contains(ExpressionTraceExporter.ToText(events, new ExpressionTraceExportOptions { IncludeUnchangedLeaves = true }), "Enter Parameter");

        using JsonDocument json = JsonDocument.Parse(ExpressionTraceExporter.ToJson(events));
        Assert.AreEqual(events.Length, json.RootElement.GetArrayLength());
        JsonElement applied = json.RootElement.EnumerateArray().First(e => e.GetProperty("kind").GetString() == "RuleApplied");
        Assert.AreEqual("x * 1", applied.GetProperty("input").GetString());
        Assert.AreEqual("x", applied.GetProperty("output").GetString());
    }

    /// <summary>
    /// The formatter is bounded and never runs user code: a non-primitive constant shows only its type, an extension node only
    /// its class, and long or deep trees are cut.
    /// </summary>
    [TestMethod]
    public void Formatter_IsBoundedAndSafe()
    {
        var value = new CountingToString();
        Expression unboxed = Expression.Convert(Expression.Constant(value, typeof(object)), typeof(double));
        Assert.AreEqual("((Double)<CountingToString>) + 1.5", ExpressionTraceFormatter.Format(Expression.Add(unboxed, C(1.5))));
        Assert.AreEqual(0, value.Calls);

        var hostile = new R4HostileExtension(X);
        Expression product = Expression.Multiply(X, hostile);
        hostile.ResetCalls();
        hostile.Armed = true;
        Assert.AreEqual("x * <extension R4HostileExtension>", ExpressionTraceFormatter.Format(product));
        Assert.AreEqual(0, hostile.TotalCalls);

        Expression deep = X;
        for (int k = 0; k < 100; k++) deep = Expression.Add(deep, C(k));
        Assert.IsTrue(ExpressionTraceFormatter.Format(deep, maxLength: 50).Length <= 51);
        StringAssert.Contains(ExpressionTraceFormatter.Format(deep, maxDepth: 3), "…");
        Assert.AreEqual("(x, y) => (x > 0 ? x : -y)", ExpressionTraceFormatter.Format(
            Expression.Lambda(Expression.Condition(Expression.GreaterThan(X, C(0)), X, Expression.Negate(Y)), X, Y)));
    }

    /// <summary>A value whose <see cref="object.ToString"/> is counted.</summary>
    private sealed class CountingToString
    {
        /// <summary>Gets the number of calls.</summary>
        public int Calls { get; private set; }

        /// <inheritdoc/>
        public override string ToString()
        {
            Calls++;
            return "called";
        }
    }

    /// <summary>
    /// Tracing, exporting and analyzing a tree that holds an extension node never inspects the node (ETC R4): its events carry no
    /// node type, and the hostile node records no engine call.
    /// </summary>
    [TestMethod]
    public void ExtensionNodes_AreNeverInspected()
    {
        var hostile = new R4HostileExtension(X);
        Expression e = Expression.Add(Expression.Multiply(Y, C(1)), hostile);
        hostile.Armed = true;
        ExpressionTransformEvent[] events = Trace(() => new ExpressionSimplifier().Simplify(e), ExpressionTraceDetail.Detailed);
        ExpressionTraceExporter.ToText(events, new ExpressionTraceExportOptions { IncludeUnchangedLeaves = true });
        ExpressionTraceExporter.ToJson(events);
        ExpressionTraceCycleAnalyzer.Analyze(events);

        Assert.AreEqual(0, hostile.EngineCalls, string.Join(", ", hostile.Calls.Keys));
        ExpressionTransformEvent enter = events.Single(x => x.Kind == ExpressionTransformEventKind.TransformEnter && ReferenceEquals(x.Input, hostile));
        Assert.IsNull(enter.NodeType);
    }
}
