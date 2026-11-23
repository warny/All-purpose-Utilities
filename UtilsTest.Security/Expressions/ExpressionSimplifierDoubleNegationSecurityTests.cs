using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;
using static UtilsTest.Security.Expressions.ExpressionResourceTestTrees;

namespace UtilsTest.Security.Expressions;

/// <summary>
/// S5A-10: the removal of an ordinary double negation (the finalization of a <c>Negate</c> node and
/// <c>ExpressionSimplifier.BuildNegation</c>) keeps the security invariants of the engine: it classifies with type tests and
/// runtime reflection objects only, never removes a user, impersonated, lifted or checked operator, never reads an opaque operand,
/// stays inside the resource limits, and gives the same results traced, untraced, after a refusal and concurrently.
/// </summary>
[TestClass]
public class ExpressionSimplifierDoubleNegationSecurityTests
{
    /// <summary>A free parameter of type <see cref="decimal"/>.</summary>
    private static readonly ParameterExpression D = Expression.Parameter(typeof(decimal), "d");

    /// <summary>A derived simplifier overriding nothing.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>The policies of S5 P18 Architecture C, from the default to the least conservative one.</summary>
    private static readonly ExpressionSimplifierOptions[] Policies =
    [
        ExpressionSimplifierOptions.Default,
        new() { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations },
        new() { AggressiveSimplification = true },
        new() { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations, AggressiveSimplification = true, ExceptionPreservation = ExpressionExceptionPreservation.None },
    ];

    /// <summary>
    /// An opaque extension node that counts the calls of the members through which code could run inside it (reduction,
    /// visit, formatting); its type and node type are plain values, read by the framework factories that rebuild its parent.
    /// </summary>
    private sealed class OpaqueExtension : Expression
    {
        /// <summary>Gets the number of calls of <see cref="Reduce"/>, <see cref="Accept"/>, <see cref="VisitChildren"/> and <see cref="ToString"/>.</summary>
        public int Calls { get; private set; }

        /// <inheritdoc/>
        public override ExpressionType NodeType => ExpressionType.Extension;

        /// <inheritdoc/>
        public override Type Type => typeof(double);

        /// <inheritdoc/>
        public override bool CanReduce => false;

        /// <inheritdoc/>
        public override Expression Reduce()
        {
            Calls++;
            return this;
        }

        /// <inheritdoc/>
        protected override Expression Accept(ExpressionVisitor visitor)
        {
            Calls++;
            return this;
        }

        /// <inheritdoc/>
        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            Calls++;
            return this;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            Calls++;
            return "extension";
        }
    }

    /// <summary>Builds <paramref name="levels"/> negations of <paramref name="operand"/>, each with <paramref name="method"/>.</summary>
    /// <param name="operand">The innermost operand.</param>
    /// <param name="levels">The number of negations.</param>
    /// <param name="method">The operator method, or <see langword="null"/> for the ordinary one.</param>
    /// <returns>The tower.</returns>
    private static Expression Negations(Expression operand, int levels, MethodInfo? method = null)
    {
        for (int i = 0; i < levels; i++) operand = method is null ? Expression.Negate(operand) : Expression.Negate(operand, method);
        return operand;
    }

    /// <summary>Counts the negations at the top of <paramref name="e"/>, whatever their method.</summary>
    /// <param name="e">The tree.</param>
    /// <returns>The number of nested <c>Negate</c> nodes from the root.</returns>
    private static int Negations(Expression e)
    {
        int count = 0;
        while (e is UnaryExpression { NodeType: ExpressionType.Negate } unary)
        {
            count++;
            e = unary.Operand;
        }

        return count;
    }

    /// <summary>Counts the nodes of <paramref name="e"/> holding <paramref name="method"/>, by reference.</summary>
    /// <param name="e">The tree.</param>
    /// <param name="method">The operator method.</param>
    /// <returns>The number of nodes holding it.</returns>
    private static int Occurrences(Expression e, MethodInfo method)
    {
        int count = 0;
        while (e is UnaryExpression unary)
        {
            if (ReferenceEquals(unary.Method, method)) count++;
            e = unary.Operand;
        }

        return count;
    }

    /// <summary>
    /// An impersonation of <c>decimal.op_UnaryNegation</c> is user code: a double negation holding it, inner, outer or both,
    /// keeps every impersonated operator and every user evaluation, the real operator alone being removed, and the simplification
    /// never invokes the impersonation, for the exact and a derived simplifier under every policy.
    /// </summary>
    /// <remarks>
    /// The trap is not armed: rebuilding any unary node goes through the framework factory, which reads the metadata of its
    /// method (residual limitation L-1 of <c>docs/expressions/A10-ReflectionProvenance.md</c>, unchanged by S5A-10). The
    /// classification of the double negation itself compares reflection objects by provenance only.
    /// </remarks>
    [TestMethod]
    public void ImpersonatedNegation_IsNeverRemoved_NorInvoked()
    {
        MethodInfo real = typeof(decimal).GetMethod("op_UnaryNegation", BindingFlags.Public | BindingFlags.Static, [typeof(decimal)])!;
        var fake = new ImpersonatingMethod(real);
        (Expression Source, int Kept)[] cases =
        [
            (Expression.Negate(Expression.Negate(D, fake), fake), 2),
            (Expression.Negate(Expression.Negate(D, fake)), 1),
            (Expression.Negate(Expression.Negate(D), fake), 1),
            (Expression.Negate(Expression.Negate(Expression.Negate(Expression.Negate(D), fake))), 1),
            (Expression.Negate(Negations(Expression.Negate(D, fake), 2)), 1),
        ];

        foreach (ExpressionSimplifier simplifier in new ExpressionSimplifier[] { new(), new DerivedSimplifier() })
        {
            foreach ((Expression source, int kept) in cases)
            {
                foreach (ExpressionSimplifierOptions options in Policies)
                {
                    ExpressionSimplifier instance = simplifier is DerivedSimplifier ? new DerivedSimplifier { Options = options } : new ExpressionSimplifier { Options = options };
                    fake.Trap.Reset();
                    Expression result = instance.Simplify(source);
                    Assert.AreEqual(0, fake.Trap.Evaluations, "the impersonation was invoked while simplifying");
                    string label = $"{instance.GetType().Name} {ExpressionTraceFormatter.Format(source)} -> {ExpressionTraceFormatter.Format(result)} under {options}";
                    Assert.AreEqual(kept, Occurrences(result, fake), label);

                    // Only a pair of real operators next to each other is removed: two negations always remain here.
                    Assert.AreEqual(2, Negations(result), label);

                    // Same value. The interpreter evaluates the real decimal negations through the impersonation too (it counts
                    // four evaluations for the one impersonated node of -(-(fake(-d)))), so the evaluations are compared only
                    // when every negation of the source is impersonated.
                    Delegate before = Expression.Lambda(source, D).Compile(preferInterpretation: true);
                    Delegate after = Expression.Lambda(result, D).Compile(preferInterpretation: true);
                    fake.Trap.Reset();
                    object? expected = before.DynamicInvoke(3.5m);
                    int evaluations = fake.Trap.Evaluations;
                    fake.Trap.Reset();
                    Assert.AreEqual(expected, after.DynamicInvoke(3.5m), label);
                    if (kept == Negations(source)) Assert.AreEqual(evaluations, fake.Trap.Evaluations, label);
                }
            }
        }

        // Positive control: the real operator pair is removed.
        Assert.AreSame(D, new ExpressionSimplifier().Simplify(Negations(D, 2)));
    }

    /// <summary>
    /// An opaque extension operand is never reduced, visited nor formatted: <c>-(-e)</c> gives <c>e</c> itself, alone or inside
    /// a sum, and an odd tower keeps one negation of it, under every policy.
    /// </summary>
    [TestMethod]
    public void OpaqueOperand_IsNeverEntered()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        var extension = new OpaqueExtension();
        Expression alone = Negations(extension, 2);
        Expression odd = Negations(extension, 3);
        Expression sum = Expression.Add(x, Negations(extension, 2));
        foreach (ExpressionSimplifierOptions options in Policies)
        {
            var simplifier = new ExpressionSimplifier { Options = options };
            Assert.AreSame(extension, simplifier.Simplify(alone));
            Assert.IsTrue(simplifier.Simplify(odd) is UnaryExpression { NodeType: ExpressionType.Negate } negation && ReferenceEquals(negation.Operand, extension));
            Assert.IsTrue(simplifier.Simplify(sum) is BinaryExpression { NodeType: ExpressionType.Add } add
                && (ReferenceEquals(add.Left, extension) || ReferenceEquals(add.Right, extension)));
        }

        Assert.AreEqual(0, extension.Calls);
    }

    /// <summary>
    /// A tower of negations costs one transformation visit per node, so the visit limit refuses it exactly as on <c>master</c>,
    /// the refusal leaves the instance reusable, and a tower deeper than the depth limit is refused before any work.
    /// </summary>
    [TestMethod]
    public void NegationTower_StaysInsideTheLimits()
    {
        Expression tower = NegateChain(200);
        var tight = new ExpressionSimplifier(new ExpressionResourceLimits { MaxTransformVisits = 150 });
        Assert.ThrowsExactly<ExpressionResourceLimitException>(() => tight.Simplify(tower));
        Assert.AreSame(P, tight.Simplify(NegateChain(100)), "reusable after a refusal");

        var exact = new ExpressionSimplifier(new ExpressionResourceLimits { MaxTransformVisits = 201 });
        Assert.AreSame(P, exact.Simplify(tower), "201 visits are enough for 201 nodes");

        Expression deep = NegateChain(ExpressionResourceLimits.DefaultMaxDepth + 10);
        Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpressionSimplifier().Simplify(deep));
    }

    /// <summary>
    /// A DAG sharing a double negation at every level (2^40 logical nodes, a few distinct nodes per level) is simplified within
    /// the default limits: the logical tree is never expanded.
    /// </summary>
    [TestMethod]
    public void SharedDoubleNegations_StayBounded()
    {
        Expression dag = P;
        for (int i = 0; i < 40; i++)
        {
            Expression level = Negations(dag, 2);
            dag = Expression.Multiply(level, Expression.Constant(1.0));
        }

        Expression result = new ExpressionSimplifier().Simplify(dag);
        Assert.AreSame(P, result);
    }

    /// <summary>The traced simplification of a tower gives the untraced result, and its trace reports finalizations only.</summary>
    [TestMethod]
    public void Tracing_GivesTheUntracedResult()
    {
        Expression tower = NegateChain(64);
        Expression untraced = new ExpressionSimplifier().Simplify(tower);
        var collector = new ExpressionTraceCollector(capacity: 16);
        Expression traced;
        using (ExpressionTransformTracing.Begin(collector)) traced = new ExpressionSimplifier().Simplify(tower);
        Assert.AreSame(P, untraced);
        Assert.AreSame(P, traced);
        Assert.IsFalse(collector.GetEvents().Any(e => e.Kind == ExpressionTransformEventKind.RuleApplied));
    }

    /// <summary>Concurrent simplifications of shared towers and products, on one shared instance, all give the same results.</summary>
    [TestMethod]
    public void ConcurrentSimplifications_AgreeOnSharedTrees()
    {
        var shared = new ExpressionSimplifier();
        Expression[] sources =
        [
            NegateChain(40),
            NegateChain(41),
            Expression.Multiply(Expression.Negate(P), Expression.Negate(P)),
            Expression.Divide(Expression.Negate(P), Expression.Constant(-1.0)),
            Expression.Add(Negations(P, 2), Negations(P, 4)),
        ];
        string[] expected = sources.Select(source => ExpressionTraceFormatter.Format(new ExpressionSimplifier().Simplify(source))).ToArray();
        int failures = 0;
        Parallel.For(0, 400, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
        {
            int index = i % sources.Length;
            if (ExpressionTraceFormatter.Format(shared.Simplify(sources[index])) != expected[index]) Interlocked.Increment(ref failures);
        });

        Assert.AreEqual(0, failures);
        CollectionAssert.AreEqual(new[] { "p", "-p", "p ** 2", "p", "2 * p" }, expected, string.Join(" | ", expected));
    }
}
