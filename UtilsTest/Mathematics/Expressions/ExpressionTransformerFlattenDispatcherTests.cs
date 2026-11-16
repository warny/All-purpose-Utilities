using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Tests of the <see cref="FlattenRuleDispatcher"/> a flatten rule may receive: the signature rules its group covers, selected
/// from the plan's buckets and run through the shared dispatch of the traversal (order, matching, constraints, invocation
/// shapes, admission, tracing), the fallback without those rules when every rule of such a group declines, and the validation of
/// the new signature.
/// </summary>
[TestClass]
public class ExpressionTransformerFlattenDispatcherTests
{
    private static readonly ParameterExpression A = Expression.Parameter(typeof(double), "a");
    private static readonly ParameterExpression B = Expression.Parameter(typeof(double), "b");
    private static readonly ParameterExpression C = Expression.Parameter(typeof(double), "c");

    /// <summary>
    /// A transformer with additive, subtractive and multiplicative signature rules that log their calls, and no flatten rule:
    /// the historical reference of the dispatch.
    /// </summary>
    private class RuleTransformer : ExpressionTransformer
    {
        /// <summary>Gets the ordered log of the rule calls and finalizations.</summary>
        public List<string> Log { get; } = [];

        /// <summary>Gets or sets what the first additive rule returns (<see langword="null"/>: decline).</summary>
        public Func<BinaryExpression, Expression?> FirstAdd { get; set; } = static _ => null;

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <inheritdoc/>
        protected override Expression PrepareExpression(Expression e) => TransformCore(e);

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
        {
            Log.Add("finalize " + e);
            return CopyExpression(e, parameters);
        }

        /// <summary>The first additive rule: logs, then applies <see cref="FirstAdd"/>.</summary>
        /// <param name="e">The prepared node.</param>
        /// <param name="left">The prepared left operand.</param>
        /// <param name="right">The prepared right operand.</param>
        /// <returns>The configured result.</returns>
        [ExpressionSignature(ExpressionType.Add)]
        protected Expression? AddFirst(BinaryExpression e, Expression left, Expression right)
        {
            Log.Add("AddFirst " + e);
            return FirstAdd(e);
        }

        /// <summary>The second additive rule, constrained to a constant left operand: logs and folds to <c>right</c>.</summary>
        /// <param name="e">The prepared node.</param>
        /// <param name="left">The prepared left operand, a constant.</param>
        /// <param name="right">The prepared right operand.</param>
        /// <returns><paramref name="right"/>.</returns>
        [ExpressionSignature(ExpressionType.Add)]
        protected Expression AddConstantLeft(BinaryExpression e, [ExpressionSignature(ExpressionType.Constant)] ConstantExpression left, Expression right)
        {
            Log.Add("AddConstantLeft " + e);
            return right;
        }

        /// <summary>A single-node rule on subtractions that logs and returns <see langword="null"/> (a result, in that shape).</summary>
        /// <param name="e">The prepared node.</param>
        /// <returns><see langword="null"/>.</returns>
        [ExpressionSignature(ExpressionType.Subtract)]
        protected Expression? SubtractSingle(BinaryExpression e)
        {
            Log.Add("SubtractSingle " + e);
            return null;
        }

        /// <summary>A multiplicative rule that logs and declines.</summary>
        /// <param name="e">The prepared node.</param>
        /// <param name="left">The prepared left operand.</param>
        /// <param name="right">The prepared right operand.</param>
        /// <returns><see langword="null"/>.</returns>
        [ExpressionSignature(ExpressionType.Multiply)]
        protected Expression? MultiplyRule(BinaryExpression e, Expression left, Expression right)
        {
            Log.Add("MultiplyRule " + e);
            return null;
        }
    }

    /// <summary>The same rules with an additive flatten rule receiving the dispatcher, whose behavior is configurable.</summary>
    private sealed class DispatchingTransformer : RuleTransformer
    {
        /// <summary>Gets or sets what the flatten rule does (<see langword="null"/> result: decline).</summary>
        public Func<DispatchingTransformer, Expression, FlattenedTerm[], FlattenRuleDispatcher, Expression?> Rule { get; set; } = static (_, _, _, _) => null;

        /// <summary>Gets the dispatchers the flatten rule received, in order.</summary>
        public List<FlattenRuleDispatcher> Dispatchers { get; } = [];

        /// <summary>Gets the term counts of the chains the flatten rule received, in order.</summary>
        public List<int> TermCounts { get; } = [];

        /// <summary>Exposes <see cref="ExpressionTransformer.TransformCore(Expression)"/> to a rule body.</summary>
        /// <param name="e">The node.</param>
        /// <returns>Its transformation.</returns>
        public Expression Core(Expression e) => TransformCore(e);

        /// <summary>The additive flatten rule.</summary>
        /// <param name="source">The root of the chain.</param>
        /// <param name="terms">The terms.</param>
        /// <param name="dispatcher">The dispatcher of the group.</param>
        /// <returns>The configured result.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract)]
        private Expression? Additions(Expression source, FlattenedTerm[] terms, FlattenRuleDispatcher dispatcher)
        {
            Log.Add("flatten " + source);
            Dispatchers.Add(dispatcher);
            TermCounts.Add(terms.Length);
            return Rule(this, source, terms, dispatcher);
        }
    }

    /// <summary>
    /// The historical treatment of <paramref name="node"/>'s own rules, from an already prepared node: the reference the
    /// dispatcher must reproduce, obtained by a transformer without flatten rules on the same node.
    /// </summary>
    /// <param name="node">A node whose operands are parameters or constants (so their preparation logs nothing).</param>
    /// <param name="configure">Configures the reference transformer.</param>
    /// <returns>The rule log of the reference, without finalizations.</returns>
    private static List<string> HistoricalRuleLog(Expression node, Action<RuleTransformer>? configure = null)
    {
        var reference = new RuleTransformer();
        configure?.Invoke(reference);
        reference.Transform(node);
        return [.. reference.Log.Where(static line => !line.StartsWith("finalize ", StringComparison.Ordinal))];
    }

    /// <summary>
    /// A single binary operation of the group is a chain of two terms: the flatten rule receives it with the dispatcher, and
    /// the dispatcher applies the covered rules of the node in their historical order, with the historical result.
    /// </summary>
    [TestMethod]
    public void SingleBinaryOperation_GoesThroughTheFlattenRuleAndItsDispatcher()
    {
        Expression source = Expression.Add(A, B);
        var transformer = new DispatchingTransformer
        {
            Rule = static (t, src, terms, dispatcher) =>
            {
                Assert.AreEqual(2, terms.Length);
                return dispatcher(src, out Expression? result) ? result : src;
            },
        };

        Expression output = transformer.Transform(source);

        Assert.AreEqual(1, transformer.TermCounts.Count);
        Assert.AreEqual(2, transformer.TermCounts[0]);
        Assert.AreSame(source, output, "AddFirst declines, AddConstantLeft does not match: no rule applies, the rule returns the source");
        CollectionAssert.AreEqual(new[] { "flatten (a + b)", "AddFirst (a + b)" }, transformer.Log);
        CollectionAssert.AreEqual(HistoricalRuleLog(source), transformer.Log.Skip(1).ToList());
    }

    /// <summary>
    /// The dispatcher is bound to the instance and the group: every chain of the group, short or long, receives the very same
    /// delegate, created once; another instance has its own.
    /// </summary>
    [TestMethod]
    public void Chains_ReceiveTheSameDispatcher_PerInstanceAndGroup()
    {
        var transformer = new DispatchingTransformer { Rule = static (_, src, _, _) => src };
        transformer.Transform(Expression.Add(A, B));
        transformer.Transform(Expression.Subtract(Expression.Add(A, B), C));
        var other = new DispatchingTransformer { Rule = static (_, src, _, _) => src };
        other.Transform(Expression.Add(A, B));

        CollectionAssert.AreEqual(new[] { 2, 3 }, transformer.TermCounts);
        Assert.AreSame(transformer.Dispatchers[0], transformer.Dispatchers[1]);
        Assert.AreNotSame(transformer.Dispatchers[0], other.Dispatchers[0]);
    }

    /// <summary>
    /// The dispatcher only accepts nodes of the operators its group declares, by type tests first: another operator, a
    /// non-operator node or <see langword="null"/> are rejected, and nothing runs.
    /// </summary>
    [TestMethod]
    public void Dispatcher_RejectsNodesOutsideItsGroup()
    {
        Exception? multiply = null, constant = null, missing = null;
        var transformer = new DispatchingTransformer
        {
            Rule = (t, src, terms, dispatcher) =>
            {
                multiply = Capture(() => dispatcher(Expression.Multiply(A, B), out _));
                constant = Capture(() => dispatcher(Expression.Constant(1.0), out _));
                missing = Capture(() => dispatcher(null!, out _));
                return src;
            },
        };

        transformer.Transform(Expression.Add(A, B));

        Assert.IsInstanceOfType<ArgumentException>(multiply);
        Assert.IsInstanceOfType<ArgumentException>(constant);
        Assert.IsInstanceOfType<ArgumentNullException>(missing);
        CollectionAssert.AreEqual(new[] { "flatten (a + b)" }, transformer.Log, "no rule ran on a rejected node");
    }

    /// <summary>Runs <paramref name="action"/> and returns the exception it throws, or <see langword="null"/>.</summary>
    /// <param name="action">The action.</param>
    /// <returns>The exception.</returns>
    private static Exception? Capture(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    /// <summary>
    /// The candidates keep their declaration order and their contracts: a declining rule lets the next one run; a parameter
    /// constraint filters exactly as in the traversal; the first applying rule wins.
    /// </summary>
    [TestMethod]
    public void Dispatcher_KeepsTheOrderTheConstraintsAndTheDecliningContract()
    {
        Expression constantLeft = Expression.Add(Expression.Constant(1.0), B);
        Expression parameterLeft = Expression.Add(A, B);
        foreach (Expression node in new[] { constantLeft, parameterLeft })
        {
            Expression? dispatched = null;
            bool applied = false;
            var transformer = new DispatchingTransformer
            {
                Rule = (t, src, _, dispatcher) =>
                {
                    applied = dispatcher(src, out dispatched);
                    return src;
                },
            };

            transformer.Transform(node);

            Expression historical = new RuleTransformer().Transform(node);
            CollectionAssert.AreEqual(HistoricalRuleLog(node), transformer.Log.Skip(1).ToList(), node.ToString());
            Assert.AreEqual(ReferenceEquals(node, constantLeft), applied, node.ToString());
            if (applied) Assert.AreSame(B, dispatched, "AddConstantLeft folds to its right operand");
            if (applied) Assert.AreSame(historical, dispatched, "the traversal returns the same rule result");
        }

        // The first rule applying wins: AddConstantLeft never runs.
        var first = new DispatchingTransformer
        {
            FirstAdd = static _ => Expression.Constant(42.0),
            Rule = static (_, src, _, dispatcher) => dispatcher(src, out Expression? result) ? result : src,
        };
        Assert.AreEqual(42.0, ((ConstantExpression)first.Transform(constantLeft)).Value);
        CollectionAssert.AreEqual(new[] { "flatten (1 + b)", "AddFirst (1 + b)" }, first.Log);
    }

    /// <summary>
    /// A single-node rule returning <see langword="null"/> produced a result in the traversal (the node becomes
    /// <see langword="null"/>); the dispatcher keeps that contract: it reports a result, and the result is <see langword="null"/>.
    /// </summary>
    [TestMethod]
    public void Dispatcher_KeepsTheNullResultOfASingleNodeRule()
    {
        Expression node = Expression.Subtract(A, B);
        bool applied = false;
        Expression? dispatched = A;
        var transformer = new DispatchingTransformer
        {
            Rule = (_, src, _, dispatcher) =>
            {
                applied = dispatcher(src, out dispatched);
                return src;
            },
        };

        transformer.Transform(node);

        Assert.IsTrue(applied);
        Assert.IsNull(dispatched);
        Assert.IsNull(new RuleTransformer().Transform(node), "the traversal returns the null result of the single-node rule");
    }

    /// <summary>
    /// The dispatcher prepares nothing, extracts no chain and calls no flatten rule: dispatching the root of a long chain runs
    /// the root's rules on it as it is, and the flatten rule is called once.
    /// </summary>
    [TestMethod]
    public void Dispatcher_DoesNotReactivateTheFlattenRule()
    {
        Expression source = Expression.Add(Expression.Add(A, B), C);
        var transformer = new DispatchingTransformer { Rule = static (_, src, _, dispatcher) => dispatcher(src, out Expression? r) ? r : src };

        transformer.Transform(source);

        Assert.AreEqual(1, transformer.TermCounts.Count);
        CollectionAssert.AreEqual(new[] { "flatten ((a + b) + c)", "AddFirst ((a + b) + c)" }, transformer.Log, "the inner (a + b) is neither prepared nor offered");
    }

    /// <summary>
    /// When every rule of a group that receives the dispatcher declines, the chain is finalized node by node without the covered
    /// rules (they ran under the rule's control, never a second time), while an operand outside the chain is transformed as
    /// usual, its own rules included.
    /// </summary>
    [TestMethod]
    public void DecliningDispatcherRule_FinalizesTheChainWithoutTheCoveredRules()
    {
        // ((a + b) - Abs(c + 1)): the call is an operand; its argument c + 1 is a chain of its own.
        MethodInfo abs = typeof(Math).GetMethod(nameof(Math.Abs), [typeof(double)])!;
        Expression inner = Expression.Add(C, Expression.Constant(1.0));
        Expression source = Expression.Subtract(Expression.Add(A, B), Expression.Call(abs, inner));
        var transformer = new DispatchingTransformer();

        Expression output = transformer.Transform(source);

        CollectionAssert.AreEqual(
            new[]
            {
                "flatten ((a + b) - Abs((c + 1)))",
                "flatten (c + 1)",
                "finalize c",
                "finalize 1",
                "finalize (c + 1)",
                "finalize Abs((c + 1))",
                "finalize a",
                "finalize b",
                "finalize (a + b)",
                "finalize ((a + b) - Abs((c + 1)))",
            }.OrderBy(static line => line).ToList(),
            transformer.Log.OrderBy(static line => line).ToList(),
            "no AddFirst, SubtractSingle: the covered rules never run on a declined chain of this group");
        Assert.AreEqual(source.ToString(), output.ToString());
        Assert.AreEqual(2, transformer.TermCounts.Count, "the chain under the call is offered once, as a chain of its own");
    }

    /// <summary>
    /// The fallback only skips the covered rules on the nodes of the declined chain: when the rule re-enters the traversal on
    /// a node of its choice, that node is a new transformation, with its rules.
    /// </summary>
    [TestMethod]
    public void DecliningDispatcherRule_LeavesTheSubExpressionsTheRuleTransformsToTheTraversal()
    {
        Expression source = Expression.Add(Expression.Constant(1.0), B);
        var transformer = new DispatchingTransformer
        {
            Rule = static (t, src, _, _) =>
            {
                // A multiplication of the operands, transformed by the traversal (its own rule runs), then declines.
                t.Core(Expression.Multiply(A, B));
                return null;
            },
        };

        transformer.Transform(source);

        Assert.IsTrue(transformer.Log.Contains("MultiplyRule (a * b)"));
        Assert.IsFalse(transformer.Log.Any(static line => line.StartsWith("Add", StringComparison.Ordinal)), "the declined chain ran no covered rule");
    }

    /// <summary>A rule of the old signature keeps the historical fallback, with the signature rules.</summary>
    private sealed class OldSignatureTransformer : RuleTransformer
    {
        /// <summary>An additive flatten rule of the old shape, which declines.</summary>
        /// <param name="source">The root of the chain.</param>
        /// <param name="terms">The terms.</param>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract)]
        private Expression? Additions(Expression source, FlattenedTerm[] terms)
        {
            Log.Add("flatten " + source);
            return null;
        }
    }

    /// <summary>
    /// The old signatures stay valid and keep their fallback: a declined chain runs the signature rules node by node, as
    /// before the dispatcher existed.
    /// </summary>
    [TestMethod]
    public void OldSignature_KeepsTheHistoricalFallback()
    {
        var transformer = new OldSignatureTransformer();
        transformer.Transform(Expression.Add(Expression.Add(A, B), C));

        CollectionAssert.AreEqual(
            new[] { "flatten ((a + b) + c)", "AddFirst (a + b)", "finalize a", "finalize b", "finalize (a + b)", "AddFirst ((a + b) + c)", "finalize c", "finalize ((a + b) + c)" }.OrderBy(static s => s).ToList(),
            transformer.Log.OrderBy(static s => s).ToList());
    }

    /// <summary>
    /// A rule declaring two couples receives, for each chain, the dispatcher of that chain's group: it accepts that group's
    /// operators only, and the two groups' dispatchers are distinct.
    /// </summary>
    private sealed class TwoCouplesTransformer : RuleTransformer
    {
        /// <summary>Gets the outcome of dispatching an addition and a multiplication, per chain root.</summary>
        public List<(string Root, string Add, string Multiply)> Outcomes { get; } = [];

        /// <summary>Gets the dispatchers received, in order.</summary>
        public List<FlattenRuleDispatcher> Dispatchers { get; } = [];

        /// <summary>The flatten rule of both couples.</summary>
        /// <param name="source">The root of the chain.</param>
        /// <param name="terms">The terms.</param>
        /// <param name="dispatcher">The dispatcher of the chain's group.</param>
        /// <returns>The source.</returns>
        [Flatten([ExpressionType.Add, ExpressionType.Multiply], [ExpressionType.Subtract, ExpressionType.Divide])]
        private Expression? Both(Expression source, FlattenedTerm[] terms, FlattenRuleDispatcher dispatcher)
        {
            Dispatchers.Add(dispatcher);
            Outcomes.Add((source.ToString(), Try(dispatcher, Expression.Add(A, B)), Try(dispatcher, Expression.Multiply(A, B))));
            return source;
        }

        /// <summary>Dispatches <paramref name="node"/> and renders the outcome.</summary>
        /// <param name="dispatcher">The dispatcher.</param>
        /// <param name="node">The node.</param>
        /// <returns><c>applied</c>, <c>declined</c> or the exception type name.</returns>
        private static string Try(FlattenRuleDispatcher dispatcher, Expression node)
        {
            try
            {
                return dispatcher(node, out _) ? "applied" : "declined";
            }
            catch (ArgumentException exception)
            {
                return exception.GetType().Name;
            }
        }
    }

    /// <summary>Each couple of a multi-couple rule gets the dispatcher of its own group.</summary>
    [TestMethod]
    public void MultiCoupleRule_GetsTheDispatcherOfEachChainsGroup()
    {
        var transformer = new TwoCouplesTransformer();
        transformer.Transform(Expression.Add(A, B));
        transformer.Transform(Expression.Multiply(A, B));

        Assert.AreEqual(("(a + b)", "declined", nameof(ArgumentException)), transformer.Outcomes[0]);
        Assert.AreEqual(("(a * b)", nameof(ArgumentException), "declined"), transformer.Outcomes[1]);
        Assert.AreNotSame(transformer.Dispatchers[0], transformer.Dispatchers[1]);
    }

    /// <summary>A custom signature attribute that counts its <c>Match</c> calls and may accept other node types.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    private sealed class CountingSignatureAttribute : ExpressionSignatureAttribute
    {
        /// <summary>The number of <see cref="Match"/> calls.</summary>
        public static int Matches;

        /// <summary>Initializes the attribute on <see cref="ExpressionType.Multiply"/>, while matching additions too.</summary>
        public CountingSignatureAttribute()
            : base(ExpressionType.Multiply)
        {
        }

        /// <inheritdoc/>
        public override bool Match(Expression e)
        {
            Matches++;
            return e.NodeType is ExpressionType.Add or ExpressionType.Multiply;
        }
    }

    /// <summary>A custom parameter attribute that counts its constructions.</summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    private sealed class CountingParameterAttribute : ExpressionSignatureAttribute
    {
        /// <summary>The number of constructions.</summary>
        public static int Constructions;

        /// <summary>Initializes the attribute on parameters.</summary>
        public CountingParameterAttribute()
            : base(ExpressionType.Parameter)
        {
            Constructions++;
        }
    }

    /// <summary>A transformer with a custom method attribute and a custom parameter attribute.</summary>
    private class CustomAttributeTransformer : ExpressionTransformer
    {
        /// <summary>Gets the rule log.</summary>
        public List<string> Log { get; } = [];

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <inheritdoc/>
        protected override Expression PrepareExpression(Expression e) => TransformCore(e);

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => CopyExpression(e, parameters);

        /// <summary>Declared on multiplications, matching additions too (a candidate of every bucket).</summary>
        /// <param name="e">The node.</param>
        /// <param name="left">The left operand, constrained by a custom parameter attribute.</param>
        /// <param name="right">The right operand.</param>
        /// <returns><see langword="null"/>.</returns>
        [CountingSignature]
        protected Expression? Custom(BinaryExpression e, [CountingParameter] Expression left, Expression right)
        {
            Log.Add("Custom " + e);
            return null;
        }
    }

    /// <summary>The same with a flatten rule that dispatches its root.</summary>
    private sealed class CustomAttributeDispatchingTransformer : CustomAttributeTransformer
    {
        /// <summary>The additive flatten rule, which dispatches its root.</summary>
        /// <param name="source">The root.</param>
        /// <param name="terms">The terms.</param>
        /// <param name="dispatcher">The dispatcher.</param>
        /// <returns>The source.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract)]
        private Expression? Additions(Expression source, FlattenedTerm[] terms, FlattenRuleDispatcher dispatcher)
        {
            dispatcher(source, out _);
            return source;
        }
    }

    /// <summary>
    /// A custom method attribute, kept a candidate of every bucket because its <c>Match</c> may accept other node types, is a
    /// candidate of the group's dispatch too: its <c>Match</c> receives the same calls, and a custom parameter attribute is
    /// constructed on demand exactly as often as in the traversal (never cached by the plan).
    /// </summary>
    [TestMethod]
    public void CustomAttributes_ReceiveTheSameCallsAsInTheTraversal()
    {
        Expression node = Expression.Add(A, B);

        CountingSignatureAttribute.Matches = 0;
        CountingParameterAttribute.Constructions = 0;
        var reference = new CustomAttributeTransformer();
        reference.Transform(node);
        int referenceMatches = CountingSignatureAttribute.Matches;
        int referenceConstructions = CountingParameterAttribute.Constructions;

        CountingSignatureAttribute.Matches = 0;
        CountingParameterAttribute.Constructions = 0;
        var dispatching = new CustomAttributeDispatchingTransformer();
        dispatching.Transform(node);

        // The reference also visits a and b (one Match each): the dispatcher sees the root only.
        Assert.AreEqual(3, referenceMatches);
        Assert.AreEqual(1, CountingSignatureAttribute.Matches);
        Assert.IsTrue(referenceConstructions > 0);
        Assert.AreEqual(referenceConstructions, CountingParameterAttribute.Constructions);
        CollectionAssert.AreEqual(reference.Log, dispatching.Log);
    }

    /// <summary>A base transformer with an overridable rule and a rule a subclass hides.</summary>
    private class OverrideBase : ExpressionTransformer
    {
        /// <summary>Gets the rule log.</summary>
        public List<string> Log { get; } = [];

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <inheritdoc/>
        protected override Expression PrepareExpression(Expression e) => TransformCore(e);

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => CopyExpression(e, parameters);

        /// <summary>An overridable rule.</summary>
        /// <param name="e">The node.</param>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns><see langword="null"/>.</returns>
        [ExpressionSignature(ExpressionType.Add)]
        protected virtual Expression? Virtual(BinaryExpression e, Expression left, Expression right)
        {
            Log.Add("base Virtual");
            return null;
        }

        /// <summary>A rule a subclass hides.</summary>
        /// <param name="e">The node.</param>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns><see langword="null"/>.</returns>
        [ExpressionSignature(ExpressionType.Add)]
        protected Expression? Hidden(BinaryExpression e, Expression left, Expression right)
        {
            Log.Add("base Hidden");
            return null;
        }
    }

    /// <summary>The override and the hiding method, without flatten rule: the historical reference.</summary>
    private class OverrideDerived : OverrideBase
    {
        /// <inheritdoc/>
        protected override Expression? Virtual(BinaryExpression e, Expression left, Expression right)
        {
            Log.Add("derived Virtual");
            return null;
        }

        /// <summary>Hides <see cref="OverrideBase.Hidden"/> with a rule of its own.</summary>
        /// <param name="e">The node.</param>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns><see langword="null"/>.</returns>
        [ExpressionSignature(ExpressionType.Add)]
        protected new Expression? Hidden(BinaryExpression e, Expression left, Expression right)
        {
            Log.Add("derived Hidden");
            return null;
        }
    }

    /// <summary><see cref="OverrideDerived"/> with a flatten rule that dispatches its root.</summary>
    private sealed class OverrideDispatching : OverrideDerived
    {
        /// <summary>The additive flatten rule, which dispatches its root.</summary>
        /// <param name="source">The root.</param>
        /// <param name="terms">The terms.</param>
        /// <param name="dispatcher">The dispatcher.</param>
        /// <returns>The source.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract)]
        private Expression? Additions(Expression source, FlattenedTerm[] terms, FlattenRuleDispatcher dispatcher)
        {
            Log.Add("flatten");
            dispatcher(source, out _);
            return source;
        }
    }

    /// <summary>
    /// Overrides and hidden methods are dispatched as in the traversal: the override runs once (never the base body), both the
    /// hidden and the hiding rule run, in the traversal's order.
    /// </summary>
    [TestMethod]
    public void OverridesAndHiddenMethods_AreDispatchedLikeTheTraversal()
    {
        var reference = new OverrideDerived();
        reference.Transform(Expression.Add(A, B));
        var dispatching = new OverrideDispatching();
        dispatching.Transform(Expression.Add(A, B));

        CollectionAssert.AreEquivalent(new[] { "derived Virtual", "derived Hidden", "base Hidden" }, reference.Log);
        Assert.AreEqual("flatten", dispatching.Log[0]);
        CollectionAssert.AreEqual(reference.Log, dispatching.Log.Skip(1).ToList());
    }

    /// <summary>Invalid shapes of the new signature, each rejected when the plan is built.</summary>
    private sealed class DispatcherFirst : ExpressionTransformer
    {
        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <summary>Invalid: the dispatcher without the source.</summary>
        /// <param name="terms">The terms.</param>
        /// <param name="dispatcher">The dispatcher.</param>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Add)]
        private Expression? Rule(FlattenedTerm[] terms, FlattenRuleDispatcher dispatcher) => null;
    }

    /// <summary>Invalid: a third parameter of another type.</summary>
    private sealed class WrongThird : ExpressionTransformer
    {
        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <summary>Invalid.</summary>
        /// <param name="source">The source.</param>
        /// <param name="terms">The terms.</param>
        /// <param name="dispatcher">Not a dispatcher.</param>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Add)]
        private Expression? Rule(Expression source, FlattenedTerm[] terms, Func<Expression, Expression?> dispatcher) => null;
    }

    /// <summary>Invalid: four parameters.</summary>
    private sealed class FourParameters : ExpressionTransformer
    {
        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <summary>Invalid.</summary>
        /// <param name="source">The source.</param>
        /// <param name="terms">The terms.</param>
        /// <param name="dispatcher">The dispatcher.</param>
        /// <param name="extra">An extra parameter.</param>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Add)]
        private Expression? Rule(Expression source, FlattenedTerm[] terms, FlattenRuleDispatcher dispatcher, int extra) => null;
    }

    /// <summary>Invalid: a constraint on the dispatcher parameter.</summary>
    private sealed class ConstrainedDispatcher : ExpressionTransformer
    {
        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <summary>Invalid.</summary>
        /// <param name="source">The source.</param>
        /// <param name="terms">The terms.</param>
        /// <param name="dispatcher">The dispatcher.</param>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Add)]
        private Expression? Rule(Expression source, FlattenedTerm[] terms, [ExpressionSignature(ExpressionType.Add)] FlattenRuleDispatcher dispatcher) => null;
    }

    /// <summary>Every invalid shape fails the construction of the transformer, with a message naming the rule.</summary>
    [TestMethod]
    public void InvalidDispatcherSignatures_FailWhenThePlanIsBuilt()
    {
        foreach (Func<ExpressionTransformer> create in new Func<ExpressionTransformer>[]
        {
            static () => new DispatcherFirst(),
            static () => new WrongThird(),
            static () => new FourParameters(),
            static () => new ConstrainedDispatcher(),
        })
        {
            InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() => create());
            StringAssert.Contains(exception.Message, ".Rule'");
        }
    }

    /// <summary>
    /// A dispatcher kept after its rule returned and called outside any operation starts one with its transformer's limits:
    /// the rules run as in the traversal.
    /// </summary>
    [TestMethod]
    public void KeptDispatcher_CalledOutsideAnOperation_StartsOne()
    {
        var transformer = new DispatchingTransformer { Rule = static (_, src, _, _) => src };
        transformer.Transform(Expression.Add(A, B));
        FlattenRuleDispatcher kept = transformer.Dispatchers[0];
        transformer.Log.Clear();

        Assert.IsTrue(kept(Expression.Add(Expression.Constant(2.0), B), out Expression? result));
        Assert.AreSame(B, result);
        CollectionAssert.AreEqual(new[] { "AddFirst (2 + b)", "AddConstantLeft (2 + b)" }, transformer.Log);
    }

    /// <summary>
    /// Inside a tracing scope, each dispatch is a treatment of its own, requested by the running flatten rule, with the rule
    /// events of the traversal.
    /// </summary>
    [TestMethod]
    public void Dispatch_IsATreatmentRequestedByTheFlattenRule()
    {
        var collector = new ExpressionTraceCollector();
        var transformer = new DispatchingTransformer { Rule = static (_, src, _, dispatcher) => dispatcher(src, out Expression? r) ? r : src };
        using (ExpressionTransformTracing.Begin(collector, ExpressionTraceDetail.Detailed))
        {
            transformer.Transform(Expression.Add(Expression.Constant(1.0), B));
        }

        ExpressionTransformEvent[] events = collector.GetEvents();
        ExpressionTransformEvent dispatchEnter = events.Single(static e => e.Kind == ExpressionTransformEventKind.TransformEnter && e.Depth == 1);
        Assert.AreEqual("Additions", dispatchEnter.Rule!.Method.Name, "the dispatch is requested by the flatten rule");
        Assert.IsTrue(events.Any(e => e.Kind == ExpressionTransformEventKind.RuleApplied && e.OperationId == dispatchEnter.OperationId && e.Rule!.Method.Name == "AddConstantLeft"));
        Assert.IsTrue(events.Any(e => e.Kind == ExpressionTransformEventKind.RuleRejected && e.OperationId == dispatchEnter.OperationId && e.Rule!.Method.Name == "AddFirst"));
        ExpressionTransformEvent root = events.Last(static e => e.Kind == ExpressionTransformEventKind.TransformExit);
        Assert.AreEqual(0, root.Depth);
        Assert.AreEqual("Additions", root.Rule!.Method.Name, "the root's applied rule is the flatten rule, not the dispatched one");
    }

    /// <summary>A transformer treating quotes as boundaries, whose group declares <c>Quote</c> as a unary operator.</summary>
    private sealed class QuoteTransformer : ExpressionTransformer
    {
        /// <summary>Gets the rule log.</summary>
        public List<string> Log { get; } = [];

        /// <summary>Gets the outcome of dispatching a quote.</summary>
        public bool? QuoteApplied { get; private set; }

        /// <summary>The quote to dispatch.</summary>
        public UnaryExpression Quote { get; } = Expression.Quote(Expression.Lambda<Func<double>>(Expression.Constant(1.0)));

        /// <inheritdoc/>
        internal override bool TreatsQuotesAsBoundaries => true;

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => CopyExpression(e, parameters);

        /// <summary>A rule on quotes, which must never run on a boundary.</summary>
        /// <param name="e">The quote.</param>
        /// <returns>The quote.</returns>
        [ExpressionSignature(ExpressionType.Quote)]
        private Expression QuoteRule(UnaryExpression e)
        {
            Log.Add("QuoteRule");
            return e;
        }

        /// <summary>The flatten rule, whose group declares <c>Quote</c>.</summary>
        /// <param name="source">The root.</param>
        /// <param name="terms">The terms.</param>
        /// <param name="dispatcher">The dispatcher.</param>
        /// <returns>The source.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract, ExpressionType.Quote)]
        private Expression? Additions(Expression source, FlattenedTerm[] terms, FlattenRuleDispatcher dispatcher)
        {
            QuoteApplied = dispatcher(Quote, out _);
            return source;
        }
    }

    /// <summary>ETC R4 (C-05): a quote the transformer treats as a boundary runs no rule, even handed to the dispatcher.</summary>
    [TestMethod]
    public void Dispatcher_KeepsQuotesBoundaries()
    {
        var transformer = new QuoteTransformer();
        transformer.Transform(Expression.Add(A, B));

        Assert.AreEqual(false, transformer.QuoteApplied);
        Assert.AreEqual(0, transformer.Log.Count);
    }
}
