using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>
/// ETC R4, checkpoint B: extension nodes (any user subclass of <see cref="Expression"/>) are strictly opaque. The engine never
/// calls their <c>NodeType</c>/<c>Type</c> getters itself, nor <c>CanReduce</c>, <c>Reduce</c>, <c>VisitChildren</c>, <c>Accept</c>,
/// <c>Equals</c>, <c>GetHashCode</c> or <c>ToString</c>; two distinct instances are never equal nor interchangeable. The hostile
/// fixture (<see cref="R4HostileExtension"/>) throws as soon as the engine itself touches one of these members; a getter read
/// made by a framework factory rebuilding a supported parent is the documented framework validation and is counted apart.
/// </summary>
[TestClass]
[TestCategory("ETC-R4")]
public class ExpressionTreeR4ExtensionTests
{
    /// <summary>The free parameter of the samples.</summary>
    private static readonly ParameterExpression P = Expression.Parameter(typeof(double), "p");

    /// <summary>Builds <c>p * 1.0</c>.</summary>
    /// <returns>A new leaf.</returns>
    private static Expression Leaf() => Expression.Multiply(P, Expression.Constant(1.0));

    /// <summary>Builds a traced child, so that every evaluation of the extension's reduction is recorded.</summary>
    /// <param name="name">The trace name.</param>
    /// <returns>The child.</returns>
    private static Expression Traced(string name) => Expression.Call(R4DynamicRuntime.TraceMethod, Expression.Constant(name), P);

    /// <summary>A derived simplifier overriding nothing.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>A generic transformer recursing through <c>PrepareExpression</c>, rewriting <c>1.0</c> into <c>2.0</c>, finalizing with <c>CopyExpression</c>.</summary>
    private sealed class RecursiveTransformer : ExpressionTransformer
    {
        /// <summary>Rewrites the marker constant.</summary>
        /// <param name="constant">The constant.</param>
        /// <returns>The rewritten or original constant.</returns>
        [ExpressionSignature(ExpressionType.Constant)]
        private Expression Rewrite(ConstantExpression constant) => constant.Value is 1.0 ? Expression.Constant(2.0) : constant;

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <inheritdoc/>
        protected override Expression PrepareExpression(Expression e) => TransformCore(e);

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => CopyExpression(e, parameters);
    }

    /// <summary>
    /// A generic transformer with a wildcard rule and a rule declared on <see cref="ExpressionType.Extension"/> whose first
    /// parameter accepts any node: since ETC R4 neither runs on an extension node (rule dispatch would read its virtual
    /// <c>NodeType</c>), while its <c>FinalizeExpression</c> override still receives it.
    /// </summary>
    private sealed class ExtensionRuleTransformer : ExpressionTransformer
    {
        /// <summary>Gets the number of rule calls on an extension node.</summary>
        public int RuleCalls { get; private set; }

        /// <summary>Gets the extension nodes received by <c>FinalizeExpression</c>.</summary>
        public List<Expression> Finalized { get; } = [];

        /// <summary>A rule declared on extension nodes.</summary>
        /// <param name="node">The node.</param>
        /// <returns>A marker constant.</returns>
        [ExpressionSignature(ExpressionType.Extension)]
        private Expression OnExtension(Expression node)
        {
            RuleCalls++;
            return Expression.Constant(-1.0);
        }

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <inheritdoc/>
        protected override Expression PrepareExpression(Expression e) => TransformCore(e);

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
        {
            if (e is R4HostileExtension) Finalized.Add(e);
            return CopyExpression(e, parameters);
        }
    }

    /// <summary>Exposes the protected members of <see cref="ExpressionTransformer"/>.</summary>
    private sealed class ExposedTransformer : ExpressionTransformer
    {
        /// <summary>Calls the protected static <c>CopyExpression</c>.</summary>
        /// <param name="e">The node to copy.</param>
        /// <param name="parameters">The replacement children.</param>
        /// <returns>The copy.</returns>
        public static Expression Copy(Expression e, params Expression[] parameters) => CopyExpression(e, parameters);

        /// <summary>Calls the protected <c>ReplaceArguments</c>.</summary>
        /// <param name="e">The tree.</param>
        /// <param name="parameter">The parameter to replace.</param>
        /// <param name="replacement">The replacement.</param>
        /// <returns>The substituted tree.</returns>
        public Expression Replace(Expression e, ParameterExpression parameter, Expression replacement) => ReplaceArguments(e, [parameter], [replacement]);

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);
    }

    /// <summary>Builds the hostile nodes of a test (one reducible, one irreducible), not yet armed.</summary>
    /// <returns>The two nodes.</returns>
    private static R4HostileExtension[] Hostiles() => [new R4HostileExtension(Traced("first")), new R4HostileExtension(Traced("second"), reducible: false)];

    /// <summary>Arms <paramref name="nodes"/>, runs <paramref name="action"/>, then disarms them and asserts that the engine made no call.</summary>
    /// <param name="nodes">The hostile nodes.</param>
    /// <param name="action">The engine operation.</param>
    /// <param name="message">The case name.</param>
    private static void AssertNoEngineCall(IEnumerable<R4HostileExtension> nodes, Action action, string message)
    {
        R4HostileExtension[] armed = nodes.ToArray();
        foreach (R4HostileExtension node in armed)
        {
            node.ResetCalls();
            node.Armed = true;
        }

        try
        {
            action();
        }
        finally
        {
            foreach (R4HostileExtension node in armed) node.Armed = false;
        }

        foreach (R4HostileExtension node in armed)
        {
            Assert.AreEqual(0, node.EngineCalls, $"{message}: engine calls {string.Join(",", node.Calls.Select(call => $"{call.Key}={call.Value}"))}");
        }
    }

    /// <summary>
    /// An extension root is returned unchanged by every transformer without any call into it (getters, reduction, visitor,
    /// identity members), for reducible and irreducible nodes, on repeated calls.
    /// </summary>
    [TestMethod]
    public void ExtensionRoot_IsPassedThrough_WithoutAnyCall()
    {
        foreach (R4HostileExtension node in Hostiles())
        {
            AssertNoEngineCall([node], () =>
            {
                for (int i = 0; i < 3; i++)
                {
                    Assert.AreSame(node, new ExpressionSimplifier().Simplify(node));
                    Assert.AreSame(node, new DerivedSimplifier().Simplify(node));
                    Assert.AreSame(node, new RecursiveTransformer().Transform(node));
                }
            }, "root");
        }
    }

    /// <summary>
    /// Builds the shapes holding <paramref name="node"/> under supported families, with their names. The conditional and the
    /// block are built with an explicit type that differs from their branches (the factories store a type only then): without it, the framework's own <see cref="ConditionalExpression.Type"/> and
    /// <see cref="BlockExpression.Type"/> getters return the type of a child (here the extension node), through a tail call
    /// that hides the framework frame, so reading the type of the supported parent reads the child's type.
    /// </summary>
    /// <param name="node">The extension node.</param>
    /// <returns>The shapes.</returns>
    private static (string Name, Expression Tree)[] Shapes(Expression node)
    {
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        return
        [
            ("x + ext", Expression.Add(P, node)),
            ("ext + x", Expression.Add(node, P)),
            ("ext * 2", Expression.Multiply(node, Expression.Constant(2.0))),
            ("2 * ext", Expression.Multiply(Expression.Constant(2.0), node)),
            ("ext - ext", Expression.Subtract(node, node)),
            ("ext + ext", Expression.Add(node, node)),
            ("ext * 0", Expression.Multiply(node, Expression.Constant(0.0))),
            ("-ext", Expression.Negate(node)),
            ("Sin(ext)", Expression.Call(typeof(Math).GetMethod(nameof(Math.Sin))!, node)),
            ("NaN + ext", Expression.Add(Expression.Constant(double.NaN), node)),
            ("p * 1 + ext", Expression.Add(Leaf(), node)),
            ("cond", Expression.Condition(Expression.GreaterThan(P, Expression.Constant(0.0)), node, Leaf(), typeof(void))),
            ("lambda", Expression.Lambda<Func<double, double>>(Expression.Add(node, y), y)),
            ("invoke", Expression.Invoke(Expression.Lambda(Expression.Add(y, node), y), P)),
            ("invoke-body", Expression.Invoke(Expression.Lambda(node, y), P)),
            ("array", Expression.NewArrayInit(typeof(double), node, Leaf())),
            ("block", Expression.Block(typeof(void), node)),
            ("dynamic", Expression.Dynamic(new R4CountingBinder("ext", throwOnBind: true), typeof(object), node, Leaf())),
        ];
    }

    /// <summary>
    /// Under every supported family, the exact simplifier, a derived simplifier, a generic transformer, substitution, the raw
    /// and public comparers, hashing and canonical keys never call into an extension node (only framework factories rebuilding
    /// a supported parent validate it), and the node instance is kept, never reduced.
    /// </summary>
    [TestMethod]
    public void ExtensionUnderSupportedFamilies_IsNeverInspectedByTheEngine()
    {
        foreach (R4HostileExtension node in Hostiles())
        {
            foreach ((string name, Expression tree) in Shapes(node))
            {
                (string Name, Expression Tree) twin = Shapes(node).Single(shape => shape.Name == name);
                AssertNoEngineCall([node], () =>
                {
                    foreach (Expression result in new[] { new ExpressionSimplifier().Simplify(tree), new DerivedSimplifier().Simplify(tree), new RecursiveTransformer().Transform(tree) })
                    {
                        Assert.IsTrue(Contains(result, node), $"{name}: the extension instance must be kept");
                    }

                    new ExposedTransformer().Replace(tree, P, Expression.Constant(4.0));
                    ExpressionComparer.Default.Equals(tree, twin.Tree);
                    ExpressionComparer.Default.GetHashCode(tree);
                    ExpressionComparer.StructuralEqualsRaw(tree, twin.Tree);
                    ExpressionComparer.StructuralHashRaw(tree);
                    ExpressionCanonicalOrder.Compare(tree, twin.Tree, []);
                }, name);
            }
        }
    }

    /// <summary>Whether <paramref name="tree"/> holds <paramref name="node"/>, found by a walk that never enters an extension node.</summary>
    /// <param name="tree">The tree.</param>
    /// <param name="node">The node.</param>
    /// <returns><see langword="true"/> if the instance is present.</returns>
    private static bool Contains(Expression tree, Expression node)
    {
        var finder = new InstanceFinder(node);
        finder.Visit(tree);
        return finder.Found;
    }

    /// <summary>A read-only visitor looking for one instance, stopping at extension nodes without visiting them.</summary>
    private sealed class InstanceFinder(Expression target) : ExpressionVisitor
    {
        /// <summary>Gets whether the instance was found.</summary>
        public bool Found { get; private set; }

        /// <inheritdoc/>
        public override Expression? Visit(Expression? node)
        {
            if (ReferenceEquals(node, target)) Found = true;
            return node is R4HostileExtension ? node : base.Visit(node);
        }
    }

    /// <summary>
    /// The rules of a generic transformer never run on an extension node (rule dispatch would read its virtual
    /// <c>NodeType</c>), even a rule declared on <see cref="ExpressionType.Extension"/>; its <c>FinalizeExpression</c> override
    /// still receives the node with an empty child list, and <c>CopyExpression</c> keeps it.
    /// </summary>
    [TestMethod]
    public void GenericTransformerRules_NeverRunOnExtensionNodes()
    {
        R4HostileExtension node = Hostiles()[0];
        var transformer = new ExtensionRuleTransformer();

        AssertNoEngineCall([node], () => Assert.AreSame(node, transformer.Transform(node)), "rules");

        Assert.AreEqual(0, transformer.RuleCalls);
        CollectionAssert.AreEqual(new Expression[] { node }, transformer.Finalized);
    }

    /// <summary>
    /// <c>CopyExpression</c> of an extension node keeps it for an empty child list and refuses any other list with
    /// <see cref="ArgumentException"/> (the engine cannot rebuild an opaque node: C-02), without calling into it.
    /// </summary>
    [TestMethod]
    public void CopyExpression_OfExtension_FollowsTheArityContract()
    {
        R4HostileExtension node = Hostiles()[1];

        AssertNoEngineCall([node], () =>
        {
            Assert.AreSame(node, ExposedTransformer.Copy(node));
            Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(node, P));
        }, "copy");
    }

    /// <summary>
    /// Two distinct extension instances are never equal (even when their own <c>Equals</c> says so), the same instance is equal
    /// to itself and hashes consistently, and their keys tie without ever making them interchangeable: compiled results evaluate
    /// every occurrence, as the source does.
    /// </summary>
    [TestMethod]
    public void DistinctExtensions_AreNeverInterchangeable()
    {
        R4HostileExtension first = new(Traced("first"));
        R4HostileExtension second = new(Traced("second"));
        R4HostileExtension[] nodes = [first, second];

        AssertNoEngineCall(nodes, () =>
        {
            Assert.IsFalse(ExpressionComparer.StructuralEqualsRaw(first, second));
            Assert.IsFalse(ExpressionComparer.Default.Equals(first, second));
            Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(first, first));
            Assert.AreEqual(ExpressionComparer.StructuralHashRaw(first), ExpressionComparer.StructuralHashRaw(first));
            Assert.IsTrue(ExpressionComparer.Default.Equals(Expression.Add(P, first), Expression.Add(P, first)), "Same instance under the same free binding.");
            Assert.AreEqual(ExpressionComparer.Default.GetHashCode(Expression.Add(P, first)), ExpressionComparer.Default.GetHashCode(Expression.Add(P, first)));
            Assert.IsFalse(ExpressionComparer.Default.Equals(Expression.Add(P, first), Expression.Add(P, second)));
            Assert.AreEqual(0, ExpressionCanonicalOrder.Compare(first, second, []), "Opaque keys tie (no invented order).");

            var set = new HashSet<Expression>(ExpressionComparer.Default) { first, second, Expression.Add(P, first) };
            Assert.AreEqual(3, set.Count);
            Assert.IsTrue(set.Contains(Expression.Add(P, first)));
        }, "equality");

        Expression[] bodies =
        [
            Expression.Subtract(first, second),
            Expression.Subtract(first, first),
            Expression.Add(first, second),
            Expression.Add(first, first),
            Expression.Multiply(first, Expression.Constant(0.0)),
            Expression.Add(Expression.Multiply(Expression.Constant(2.0), first), Expression.Multiply(Expression.Constant(3.0), second)),
        ];

        foreach (Expression body in bodies)
        {
            var source = Expression.Lambda<Func<double, double>>(body, P);
            Expression<Func<double, double>>? simplified = null;
            AssertNoEngineCall(nodes, () => simplified = (Expression<Func<double, double>>)new ExpressionSimplifier().Simplify(source), "simplify");

            R4DynamicRuntime.Reset();
            double expected = source.Compile()(1.5);
            string[] expectedEvents = [.. R4DynamicRuntime.Events];
            R4DynamicRuntime.Reset();
            double actual = simplified!.Compile()(1.5);
            Assert.AreEqual(expected, actual);
            CollectionAssert.AreEqual(expectedEvents, R4DynamicRuntime.Events.ToArray());
        }
    }

    /// <summary>
    /// Around an extension node, the binding context still decides: the same instance under binders that bind the same
    /// declarations is equal, under different or swapped declarations it is not (the node may reference them).
    /// </summary>
    [TestMethod]
    public void ExtensionUnderBinders_KeepsBindingCorrectness()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        ParameterExpression b = Expression.Parameter(typeof(double), "b");
        R4HostileExtension node = new(Expression.Subtract(a, b));

        AssertNoEngineCall([node], () =>
        {
            Expression sameDeclarations = Expression.Lambda(Expression.Add(node, a), a, b);
            Expression sameDeclarationsTwin = Expression.Lambda(Expression.Add(node, a), a, b);
            Expression swapped = Expression.Lambda(Expression.Add(node, b), b, a);
            Expression renamed = Expression.Lambda(Expression.Add(node, Expression.Parameter(typeof(double), "c")), Expression.Parameter(typeof(double), "c"), b);

            Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(sameDeclarations, sameDeclarationsTwin));
            Assert.AreEqual(ExpressionComparer.StructuralHashRaw(sameDeclarations), ExpressionComparer.StructuralHashRaw(sameDeclarationsTwin));
            Assert.IsFalse(ExpressionComparer.StructuralEqualsRaw(sameDeclarations, swapped));
            Assert.IsFalse(ExpressionComparer.StructuralEqualsRaw(sameDeclarations, renamed));
        }, "binders");
    }

    /// <summary>
    /// Inlining never enters an extension node: an invocation whose body holds one keeps the invocation (it may reference the
    /// parameter), without any call into the node; substitution never enters it either, and a capture check that meets one
    /// renames conservatively.
    /// </summary>
    [TestMethod]
    public void InliningAndSubstitution_TreatExtensionsConservatively()
    {
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        R4HostileExtension node = Hostiles()[0];
        Expression bodyHolder = Expression.Invoke(Expression.Lambda(Expression.Add(y, node), y), P);
        Expression bodyRoot = Expression.Invoke(Expression.Lambda(node, y), P);
        LambdaExpression capturing = Expression.Lambda(Expression.Add(node, P), y);

        AssertNoEngineCall([node], () =>
        {
            Assert.IsInstanceOfType<InvocationExpression>(new ExpressionSimplifier().Simplify(bodyHolder));
            Assert.IsInstanceOfType<InvocationExpression>(new ExpressionSimplifier().Simplify(bodyRoot));

            var renamed = (LambdaExpression)new ExposedTransformer().Replace(capturing, P, y);
            Assert.AreNotSame(y, renamed.Parameters[0], "The opaque node may mention y: the declaration is renamed.");
            Assert.IsTrue(Contains(renamed, node));
        }, "inlining");
    }
}
