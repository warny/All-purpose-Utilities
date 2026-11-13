using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using static UtilsTest.Security.Expressions.ExpressionResourceTestTrees;

namespace UtilsTest.Security.Expressions;

/// <summary>
/// Security invariants of the <see cref="FlattenAttribute"/> infrastructure: the extraction of a chain is counted in the
/// resource budgets and refused just above a limit, it never recurses (a chain of any depth is extracted on a 256 KiB stack)
/// while a declined chain keeps the controlled stack refusal of the historical traversal, the shape limits still apply before
/// any rule, no state leaks after a refusal, and extraction alone never runs user code (extension nodes, impersonating
/// reflection objects).
/// </summary>
[TestClass]
public class ExpressionFlattenResourceTests
{
    /// <summary>The stack size of the worker threads.</summary>
    private const int SmallStack = 256 * 1024;

    /// <summary>The maximum duration of a worker thread.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(120);

    /// <summary>Limits without any bound.</summary>
    private static readonly ExpressionResourceLimits NoLimits = ExpressionResourceLimits.Unlimited;

    /// <summary>A second free parameter.</summary>
    private static readonly ParameterExpression Q = Expression.Parameter(typeof(double), "q");

    /// <summary>
    /// A transformer that recurses into every child and copies every node, with an additive and a multiplicative flatten rule
    /// whose result is configurable (<see langword="null"/>: decline).
    /// </summary>
    private sealed class FlattenProbe : ExpressionTransformer
    {
        /// <summary>Initializes the probe.</summary>
        /// <param name="limits">The resource limits.</param>
        public FlattenProbe(ExpressionResourceLimits limits)
            : base(limits)
        {
        }

        /// <summary>Gets or sets the result of the flatten rules.</summary>
        public Func<FlattenProbe, Expression, FlattenedTerm[], Expression?> Rule { get; set; } = static (_, _, _) => null;

        /// <summary>Gets the number of terms of each chain the rules received.</summary>
        public List<int> Received { get; } = [];

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <summary>Exposes the faithful reconstruction.</summary>
        /// <param name="source">The root of a chain.</param>
        /// <param name="operands">The new operands.</param>
        /// <returns>The rebuilt chain.</returns>
        public Expression Rebuild(Expression source, IReadOnlyList<Expression> operands) => RebuildFlattenedChain(source, operands);

        /// <inheritdoc/>
        protected override Expression PrepareExpression(Expression e) => TransformCore(e);

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => CopyExpression(e, parameters);

        /// <summary>The additive flatten rule.</summary>
        /// <param name="source">The root of the chain.</param>
        /// <param name="terms">The terms.</param>
        /// <returns>The configured result.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract, ExpressionType.Negate)]
        private Expression? Additions(Expression source, FlattenedTerm[] terms)
        {
            Received.Add(terms.Length);
            return Rule(this, source, terms);
        }

        /// <summary>The multiplicative flatten rule.</summary>
        /// <param name="source">The root of the chain.</param>
        /// <param name="terms">The terms.</param>
        /// <returns>The configured result.</returns>
        [Flatten(ExpressionType.Multiply, ExpressionType.Divide)]
        private Expression? Multiplications(Expression source, FlattenedTerm[] terms)
        {
            Received.Add(terms.Length);
            return Rule(this, source, terms);
        }
    }

    /// <summary>Accepts every chain with a constant.</summary>
    private static readonly Func<FlattenProbe, Expression, FlattenedTerm[], Expression?> Accept = static (_, _, _) => Expression.Constant(0.0);

    /// <summary>Builds a chain of <paramref name="operands"/> operands, iteratively.</summary>
    /// <param name="operands">The number of operands.</param>
    /// <param name="shape">0: left-associated additions; 1: right-associated; 2: alternating additions and subtractions; 3: left-associated divisions.</param>
    /// <returns>The chain.</returns>
    private static Expression BuildChain(int operands, int shape)
    {
        Expression e = P;
        for (int i = 1; i < operands; i++)
        {
            e = shape switch
            {
                0 => Expression.Add(e, Q),
                1 => Expression.Add(Q, e),
                2 => i % 2 == 0 ? Expression.Add(e, Q) : Expression.Subtract(Q, e),
                _ => Expression.Divide(e, Q),
            };
        }

        return e;
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
    /// The extraction counts exactly one structural visit per node of the chain (<c>2n - 1</c> for <c>n</c> operands) and the
    /// chain root one transformation visit: an accepted chain costs one transformation visit in all, and a declined chain
    /// costs the historical traversal plus exactly that extraction, once, whatever its depth.
    /// </summary>
    [TestMethod]
    public void Extraction_IsCounted_OncePerChain()
    {
        foreach (int shape in new[] { 0, 1, 2, 3 })
        {
            foreach (int operands in new[] { 2, 8, 64, 256 })
            {
                Expression chain = BuildChain(operands, shape);
                string label = $"shape {shape}, {operands} operands";

                (long historicalTransform, long historicalStructural) = Count(new RecursiveTransformer(NoLimits), chain);
                (long acceptedTransform, long acceptedStructural) = Count(new FlattenProbe(NoLimits) { Rule = Accept }, chain);
                var declining = new FlattenProbe(NoLimits);
                (long declinedTransform, long declinedStructural) = Count(declining, chain);

                Assert.AreEqual(2L * operands - 1, historicalTransform, label);
                Assert.AreEqual(1, acceptedTransform, label);
                Assert.AreEqual(2L * operands - 1, acceptedStructural - historicalStructural, label);
                Assert.AreEqual(historicalTransform + 1, declinedTransform, label);
                Assert.AreEqual(2L * operands - 1, declinedStructural - historicalStructural, label);
                Assert.AreEqual(1, declining.Received.Count, label);
            }
        }
    }

    /// <summary>
    /// The structural budget refuses the extraction just above the limit, before any rule runs; the transformation budget
    /// refuses the fallback of a declined chain just above the limit. Both refusals are <see cref="ExpressionResourceLimitException"/>
    /// of the right kind, leave no operation open, and the same instance then works again.
    /// </summary>
    [TestMethod]
    public void Budgets_RefuseJustAboveTheLimit_AndLeaveNoState()
    {
        Expression chain = BuildChain(64, 2);
        (_, long structural) = Count(new FlattenProbe(NoLimits) { Rule = Accept }, chain);
        (long transform, _) = Count(new FlattenProbe(NoLimits), chain);

        var exact = new FlattenProbe(NoLimits with { MaxStructuralVisits = structural }) { Rule = Accept };
        exact.Transform(chain);
        Assert.AreEqual(1, exact.Received.Count);

        var tooSmall = new FlattenProbe(NoLimits with { MaxStructuralVisits = structural - 1 }) { Rule = Accept };
        ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => tooSmall.Transform(chain));
        Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, exception.Kind);
        Assert.AreEqual(0, tooSmall.Received.Count, "No rule may run on a chain whose extraction was refused.");
        Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);

        var declinedExact = new FlattenProbe(NoLimits with { MaxTransformVisits = transform });
        declinedExact.Transform(chain);

        var declinedTooSmall = new FlattenProbe(NoLimits with { MaxTransformVisits = transform - 1 });
        exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => declinedTooSmall.Transform(chain));
        Assert.AreEqual(ExpressionResourceLimitKind.Work, exception.Kind);
        Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);

        // The refused instance holds no mark of the refused chain: its root is offered to the rules again.
        declinedTooSmall.Received.Clear();
        declinedTooSmall.Rule = Accept;
        Assert.IsInstanceOfType<ConstantExpression>(ExpressionResourceBudget.Run(NoLimits, null, null, null, (declinedTooSmall, chain), static s => s.declinedTooSmall.Transform(s.chain)));
        Assert.AreEqual(1, declinedTooSmall.Received.Count);
    }

    /// <summary>
    /// The shape limits still apply before any flatten rule: a chain deeper than <see cref="ExpressionResourceLimits.MaxDepth"/>
    /// or larger than <see cref="ExpressionResourceLimits.MaxNodes"/> is refused by the validation, and no rule runs.
    /// </summary>
    [TestMethod]
    public void ShapeLimits_StillRefuseBeforeAnyRule()
    {
        Expression chain = BuildChain(600, 0);

        var deep = new FlattenProbe(ExpressionResourceLimits.Default) { Rule = Accept };
        ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => deep.Transform(chain));
        Assert.AreEqual(ExpressionResourceLimitKind.Depth, exception.Kind);
        Assert.AreEqual(0, deep.Received.Count);

        var large = new FlattenProbe(Shape(maxNodes: 1_000)) { Rule = Accept };
        exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => large.Transform(chain));
        Assert.AreEqual(ExpressionResourceLimitKind.Nodes, exception.Kind);
        Assert.AreEqual(0, large.Received.Count);
    }

    /// <summary>
    /// On a 256 KiB thread and without limits, a chain of 100,000 operands of every shape is extracted, given to its rule as
    /// one chain, and rebuilt faithfully, without any recursion.
    /// </summary>
    [TestMethod]
    public void DeepChain_IsExtractedAndRebuilt_OnASmallStack()
    {
        const int Operands = 100_000;
        foreach (int shape in new[] { 0, 1, 2, 3 })
        {
            Expression chain = BuildChain(Operands, shape);
            Expression negated = Chain(Operands, static e => Expression.Negate(e));
            RunOnThread(() =>
            {
                var probe = new FlattenProbe(NoLimits) { Rule = Accept };
                Assert.IsInstanceOfType<ConstantExpression>(probe.Transform(chain));
                CollectionAssert.AreEqual(new[] { Operands }, probe.Received);

                var rebuilding = new FlattenProbe(NoLimits)
                {
                    Rule = static (t, source, terms) => t.Rebuild(source, [.. terms.Select(static term => (Expression)Expression.Constant(1.0))]),
                };
                Expression rebuilt = rebuilding.Transform(chain);
                Assert.AreEqual(chain.NodeType, rebuilt.NodeType);
                Assert.AreNotSame(chain, rebuilt);

                var negation = new FlattenProbe(NoLimits) { Rule = Accept };
                negation.Transform(negated);
                CollectionAssert.AreEqual(new[] { 1 }, negation.Received);
            }, SmallStack, Timeout);
        }
    }

    /// <summary>
    /// A declined deep chain falls back to the historical recursive traversal, which ends with the controlled stack refusal
    /// on a 256 KiB thread (never a crash). No operation stays open and no mark of the declined chain leaks: the same
    /// instance, on the same thread, is offered the same root again.
    /// </summary>
    [TestMethod]
    public void DeclinedDeepChain_IsAControlledStackRefusal_AndLeaksNoState()
    {
        foreach (int shape in new[] { 0, 1 })
        {
            Expression chain = BuildChain(100_000, shape);
            RunOnThread(() =>
            {
                var probe = new FlattenProbe(NoLimits);
                ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => probe.Transform(chain));
                Assert.AreEqual(ExpressionResourceLimitKind.Stack, exception.Kind);
                Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
                CollectionAssert.AreEqual(new[] { 100_000 }, probe.Received, "The chain is extracted once; its sub-chains are never offered.");

                probe.Received.Clear();
                probe.Rule = Accept;
                Assert.IsInstanceOfType<ConstantExpression>(probe.Transform(chain));
                CollectionAssert.AreEqual(new[] { 100_000 }, probe.Received);
            }, SmallStack, Timeout);
        }
    }

    /// <summary>
    /// A declined chain over a shared DAG (every level adds a node to itself) is extracted once: the fallback does not offer
    /// the shared sub-chains again, so the rule runs once instead of once per logical node.
    /// </summary>
    [TestMethod]
    public void DeclinedSharedDag_IsExtractedOnce()
    {
        Expression dag = SharedDag(12);
        var probe = new FlattenProbe(NoLimits);

        probe.Transform(dag);

        CollectionAssert.AreEqual(new[] { 1 << 12 }, probe.Received);
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
    /// Extraction alone runs no user code: an armed extension node and a call to an impersonating method are operands that
    /// are never read, and an operator whose method is an impersonating reflection object is never crossed nor inspected (no
    /// member of the method is called, not even a type read).
    /// </summary>
    [TestMethod]
    public void Extraction_NeverReadsExtensionNodes_NorImpersonatingReflectionObjects()
    {
        var extension = new ArmedExtension();
        var operatorMethod = new ImpersonatingMethod(typeof(ExpressionFlattenResourceTests).GetMethod(nameof(Plus))!);
        var callMethod = new ImpersonatingMethod(AbsMethod);
        Expression userOperator = Expression.Add(P, Q, operatorMethod);
        Expression call = Expression.Call(callMethod, Q);
        Expression chain = Expression.Subtract(Expression.Add(Expression.Add(P, extension), userOperator), Expression.Negate(call));

        extension.Armed = true;
        operatorMethod.Trap.Reset();
        callMethod.Trap.Reset();
        operatorMethod.Trap.Armed = true;
        callMethod.Trap.Armed = true;

        FlattenedTerm[]? received = null;
        var probe = new FlattenProbe(NoLimits)
        {
            Rule = (_, _, terms) =>
            {
                received = terms;
                return Expression.Constant(0.0);
            },
        };
        try
        {
            probe.Transform(chain);
        }
        finally
        {
            // The framework keeps the impersonating methods in its static parameter cache, where they share the hash code of
            // the real methods: left armed, their Equals would throw in any later test that builds a call to those methods.
            operatorMethod.Trap.Armed = false;
            callMethod.Trap.Armed = false;
        }

        Assert.IsNotNull(received);
        Assert.AreEqual(4, received.Length);
        Assert.AreSame(extension, received[1].Operand);
        Assert.AreSame(userOperator, received[2].Operand);
        Assert.AreSame(call, received[3].Operand);
        Assert.AreEqual(ExpressionType.Add, received[3].Operation, "p + ext + (p + q) - -(call): the call is added back.");
        Assert.AreEqual(0, extension.Calls);
        Assert.AreEqual(0, operatorMethod.Trap.ArmedCalls + operatorMethod.Trap.TypeReads);
        Assert.AreEqual(0, callMethod.Trap.ArmedCalls + callMethod.Trap.TypeReads);
    }

    /// <summary>
    /// <see cref="FlattenAttribute"/> is immutable: it keeps a private copy of its operators, so mutating the array given to
    /// its constructor never changes the declared set, and the published collection is read-only (no mutable alias).
    /// </summary>
    [TestMethod]
    public void Attribute_CopiesItsOperators()
    {
        ExpressionType[] operations = [ExpressionType.Add, ExpressionType.Subtract];
        var attribute = new FlattenAttribute(operations);

        operations[0] = ExpressionType.Power;

        CollectionAssert.AreEqual(new[] { ExpressionType.Add, ExpressionType.Subtract }, attribute.Operations.ToArray());
        Assert.IsNotInstanceOfType<ExpressionType[]>(attribute.Operations);
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<ExpressionType>)attribute.Operations)[0] = ExpressionType.Power);
        Assert.AreEqual(0, new FlattenAttribute(null!).Operations.Count);
    }

    /// <summary>
    /// A chain never crosses a lexical boundary: the body of a lambda (invoked or quoted) is a chain of its own, reached only
    /// through the historical traversal that opens the lambda's scope.
    /// </summary>
    [TestMethod]
    public void Chains_NeverCrossALambda()
    {
        LambdaExpression lambda = Expression.Lambda(Expression.Add(Q, Expression.Add(Q, Q)), Q);
        var probe = new FlattenProbe(NoLimits);

        probe.Transform(Expression.Add(P, Expression.Invoke(lambda, P)));

        CollectionAssert.AreEqual(new[] { 2, 3 }, probe.Received);
    }
}
