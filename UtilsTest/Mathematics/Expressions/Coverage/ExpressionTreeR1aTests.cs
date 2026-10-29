using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>
/// ETC R1a: corrected contracts for the corruption and exception findings ETC-08, ETC-09, ETC-11 and ETC-13. A family
/// that is not traversed yet must survive unchanged; an invocation is inlined only when every parameter use is
/// substituted and the arguments' evaluation cannot be observed to move, repeat or disappear; a rethrow is a leaf.
/// </summary>
[TestClass]
[TestCategory("ETC-R1a")]
public class ExpressionTreeR1aTests
{
    /// <summary>The free parameter of the samples.</summary>
    private static readonly ParameterExpression P = Expression.Parameter(typeof(double), "p");

    /// <summary>
    /// The families the transformer does not traverse, reaching <c>PrepareDefault</c>. ETC R1b removed TypeIs, TypeEqual,
    /// New, NewArrayInit, NewArrayBounds, MemberAccess and Default, which are traversed since (see <c>ExpressionTreeR1bTests</c>).
    /// </summary>
    private static readonly HashSet<ExpressionType> UntraversedRoots =
    [
        ExpressionType.Block, ExpressionType.Loop,
        ExpressionType.MemberInit, ExpressionType.ListInit, ExpressionType.Index, ExpressionType.Try, ExpressionType.Switch,
        ExpressionType.Goto, ExpressionType.Label, ExpressionType.RuntimeVariables, ExpressionType.DebugInfo,
        ExpressionType.Dynamic, ExpressionType.Extension,
    ];

    /// <summary>
    /// Simplifies <paramref name="tree"/> with the exact simplifier, a derived simplifier and a generic recursive transformer
    /// (ETC R1b): each result keeps the root family and its metadata, evaluates to the same value as the original for the
    /// simplifiers, and shows that the descendants were visited (the generic transformer turns every <c>1.0</c> into
    /// <c>2.0</c>, so a <c>p * 1</c> descendant no longer multiplies by one).
    /// </summary>
    /// <param name="tree">A tree rooted at a family traversed since R1b, holding at least one <c>p * 1</c> descendant.</param>
    /// <returns>The three results, in the order exact, derived, generic.</returns>
    private static Expression[] AssertTraversed(Expression tree)
    {
        Expression[] results = [new ExpressionSimplifier().Simplify(tree), new DerivedSimplifier().Simplify(tree), new RecursiveTransformer().Transform(tree)];
        foreach (Expression result in results)
        {
            Assert.AreEqual(tree.NodeType, result.NodeType);
            Assert.AreEqual(tree.Type, result.Type);
            Assert.AreNotSame(tree, result, "A descendant changed, so the node must have been rebuilt.");
        }

        var collector = new NodeCollector();
        collector.Visit(results[0]);
        Assert.IsFalse(collector.Nodes.OfType<BinaryExpression>().Any(b => b.NodeType == ExpressionType.Multiply), "The exact simplifier must have simplified every p * 1.");
        return results;
    }

    /// <summary>Builds the simplifiable leaf <c>p * 1.0</c>.</summary>
    /// <returns>A new leaf.</returns>
    private static Expression Leaf() => Expression.Multiply(P, Expression.Constant(1.0));

    /// <summary>A derived simplifier overriding nothing.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>A derived simplifier that records every <c>FinalizeExpression</c> call and then defers to the base.</summary>
    private sealed class RecordingSimplifier : ExpressionSimplifier
    {
        /// <summary>Gets the finalized node types with their child-list lengths.</summary>
        public List<(ExpressionType NodeType, int Children)> Finalized { get; } = [];

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
        {
            Finalized.Add((e.NodeType, parameters.Length));
            return base.FinalizeExpression(e, parameters);
        }
    }

    /// <summary>A generic transformer recursing through <c>PrepareExpression</c> and rewriting the constant <c>1.0</c> into <c>2.0</c>.</summary>
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
        protected override Expression PrepareExpression(Expression e)
        {
            ArgumentNullException.ThrowIfNull(e);
            return TransformCore(e);
        }

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => CopyExpression(e, parameters);
    }

    /// <summary>
    /// A generic transformer whose <c>FinalizeExpression</c> override replaces every block by a marker constant and
    /// whose rule replaces every type test by <see langword="true"/>: both must still be honored for untraversed nodes.
    /// </summary>
    private sealed class OverridingTransformer : ExpressionTransformer
    {
        /// <summary>Gets the node types and child-list lengths seen by <c>FinalizeExpression</c>.</summary>
        public List<(ExpressionType NodeType, int Children)> Finalized { get; } = [];

        /// <summary>Replaces a type test.</summary>
        /// <param name="typeTest">The type test.</param>
        /// <returns>The constant <see langword="true"/>.</returns>
        [ExpressionSignature(ExpressionType.TypeIs)]
        private Expression ReplaceTypeTest(TypeBinaryExpression typeTest) => Expression.Constant(true);

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <inheritdoc/>
        protected override Expression PrepareExpression(Expression e) => TransformCore(e);

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
        {
            Finalized.Add((e.NodeType, parameters.Length));
            return e.NodeType == ExpressionType.Block ? Expression.Constant(-1.0) : CopyExpression(e, parameters);
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

    /// <summary>Records the calls made by compiled samples, so evaluation count and order can be compared.</summary>
    private sealed class CallLog
    {
        /// <summary>The <see cref="Record"/> method.</summary>
        public static readonly MethodInfo RecordMethod = typeof(CallLog).GetMethod(nameof(Record))!;

        /// <summary>The <see cref="Fail"/> method.</summary>
        public static readonly MethodInfo FailMethod = typeof(CallLog).GetMethod(nameof(Fail))!;

        /// <summary>Gets the recorded call names, in order.</summary>
        public List<string> Calls { get; } = [];

        /// <summary>Records <paramref name="name"/> and returns <paramref name="value"/>.</summary>
        /// <param name="name">The call name.</param>
        /// <param name="value">The value to return.</param>
        /// <returns><paramref name="value"/>.</returns>
        public double Record(string name, double value)
        {
            Calls.Add(name);
            return value;
        }

        /// <summary>Records <paramref name="name"/> and throws.</summary>
        /// <param name="name">The call name.</param>
        /// <returns>Never returns.</returns>
        /// <exception cref="InvalidOperationException">Always.</exception>
        public double Fail(string name)
        {
            Calls.Add(name);
            throw new InvalidOperationException(name);
        }

        /// <summary>Builds a recorded call returning <paramref name="value"/>.</summary>
        /// <param name="name">The call name.</param>
        /// <param name="value">The value returned.</param>
        /// <returns>The call expression.</returns>
        public Expression Call(string name, double value)
            => Expression.Call(Expression.Constant(this), RecordMethod, Expression.Constant(name), Expression.Constant(value));
    }

    /// <summary>A pure custom addition, used as an explicit operator method.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><c>left + right + 10</c>.</returns>
    public static double AddTen(double left, double right) => left + right + 10;

    /// <summary>An order-sensitive combination of two values.</summary>
    /// <param name="tens">The tens.</param>
    /// <param name="units">The units.</param>
    /// <returns><c>tens * 10 + units</c>.</returns>
    public static double Combine(double tens, double units) => tens * 10 + units;

    /// <summary>Counts the invocations of a lambda left in a tree.</summary>
    /// <param name="expression">The tree.</param>
    /// <returns>The number of <see cref="InvocationExpression"/> nodes whose target is a <see cref="LambdaExpression"/>.</returns>
    private static int CountLambdaInvocations(Expression expression)
    {
        var counter = new LambdaInvocationCounter();
        counter.Visit(expression);
        return counter.Count;
    }

    /// <summary>Counts invocations whose target is a lambda.</summary>
    private sealed class LambdaInvocationCounter : ExpressionVisitor
    {
        /// <summary>Gets the number of lambda invocations seen.</summary>
        public int Count { get; private set; }

        /// <inheritdoc/>
        protected override Expression VisitInvocation(InvocationExpression node)
        {
            if (node.Expression is LambdaExpression) Count++;
            return base.VisitInvocation(node);
        }
    }

    /// <summary>Simplifies <paramref name="lambda"/> with <paramref name="simplifier"/> and returns the compiled result.</summary>
    /// <param name="lambda">The lambda.</param>
    /// <param name="simplifier">The simplifier, or the exact built-in one when <see langword="null"/>.</param>
    /// <returns>The simplified lambda.</returns>
    private static Expression<Func<double, double>> Simplify(Expression<Func<double, double>> lambda, ExpressionSimplifier? simplifier = null)
        => (Expression<Func<double, double>>)(simplifier ?? new ExpressionSimplifier()).Simplify(lambda);

    /// <summary>Asserts that the original and simplified lambdas compute the same values on a few inputs.</summary>
    /// <param name="original">The original lambda.</param>
    /// <param name="simplified">The simplified lambda.</param>
    private static void AssertSameValues(Expression<Func<double, double>> original, Expression<Func<double, double>> simplified)
    {
        Func<double, double> expected = original.Compile();
        Func<double, double> actual = simplified.Compile();
        foreach (double input in new[] { -2.0, 0.0, 3.0 })
        {
            Assert.AreEqual(expected(input), actual(input), 1e-12, $"p = {input}");
        }
    }

    // ------------------------------------------------------------------------------------------
    // ETC-08 / ETC-09: untraversed families survive unchanged.
    // ------------------------------------------------------------------------------------------

    /// <summary>Simplifies <paramref name="tree"/> with every transformer kind and asserts that each returns the very same instance.</summary>
    /// <param name="tree">The untraversed tree.</param>
    private static void AssertPassedThrough(Expression tree)
    {
        Assert.AreSame(tree, new ExpressionSimplifier().Simplify(tree), "exact simplifier");
        Assert.AreSame(tree, new DerivedSimplifier().Simplify(tree), "derived simplifier");
        Assert.AreSame(tree, new RecursiveTransformer().Transform(tree), "generic transformer");
    }

    /// <summary>
    /// Array initializers with zero, one and several elements keep every element and still evaluate to the same array. ETC R1b
    /// replaced the R1a pass-through by a traversal: the elements are simplified in place (the empty initializer, which has no
    /// child, is still returned as is).
    /// </summary>
    [TestMethod]
    public void NewArrayInit_ZeroOneAndSeveralElements_AreTraversed()
    {
        Expression empty = Expression.NewArrayInit(typeof(double));
        AssertPassedThrough(empty);

        Expression[] arrays =
        [
            Expression.NewArrayInit(typeof(double), Leaf()),
            Expression.NewArrayInit(typeof(double), Leaf(), Expression.Add(P, Expression.Constant(0.0)), Expression.Constant(4.0)),
        ];

        foreach (Expression array in arrays)
        {
            AssertTraversed(array);
            double[] expected = Expression.Lambda<Func<double, double[]>>(array, P).Compile()(3.0);
            double[] values = Expression.Lambda<Func<double, double[]>>(new ExpressionSimplifier().Simplify(array), P).Compile()(3.0);
            CollectionAssert.AreEqual(expected, values);
        }
    }

    /// <summary>
    /// One- and two-dimensional array creations with bounds keep their rank and element type. ETC R1b traverses the bounds:
    /// a simplifiable bound is simplified, and the creation still allocates the same shape.
    /// </summary>
    [TestMethod]
    public void NewArrayBounds_OneAndTwoDimensions_AreTraversed()
    {
        ParameterExpression n = Expression.Parameter(typeof(int), "n");
        Expression vector = Expression.NewArrayBounds(typeof(double), Expression.Multiply(n, Expression.Constant(1)));
        Expression matrix = Expression.NewArrayBounds(typeof(double), Expression.Constant(2), Expression.Add(n, Expression.Constant(0)));

        foreach (Expression creation in new[] { vector, matrix })
        {
            Expression simplified = new ExpressionSimplifier().Simplify(creation);
            Assert.AreEqual(creation.Type, simplified.Type);
            Assert.AreEqual(creation.NodeType, simplified.NodeType);
            Assert.IsInstanceOfType<ParameterExpression>(((NewArrayExpression)simplified).Expressions[^1], "The bound must have been simplified to n.");
        }

        var created = (double[,])Expression.Lambda<Func<int, object>>(new ExpressionSimplifier().Simplify(matrix), n).Compile()(4);
        Assert.AreEqual(2, created.GetLength(0));
        Assert.AreEqual(4, created.GetLength(1));
    }

    /// <summary>Blocks with statements, declared variables, an explicit result type and side effects survive and still run identically.</summary>
    [TestMethod]
    public void Block_StatementsVariablesTypeAndEffects_AreKept()
    {
        ParameterExpression local = Expression.Variable(typeof(double), "local");
        var log = new CallLog();
        Expression[] blocks =
        [
            Expression.Block(typeof(void), System.Array.Empty<Expression>()),
            Expression.Block(Leaf()),
            Expression.Block([local], Expression.Assign(local, Leaf()), Expression.AddAssign(local, log.Call("effect", 1.0)), local),
            Expression.Block(typeof(object), [local], Expression.Assign(local, P), Expression.Convert(local, typeof(object))),
        ];

        foreach (Expression block in blocks)
        {
            AssertPassedThrough(block);
        }

        Func<double, double> compiled = Expression.Lambda<Func<double, double>>(new ExpressionSimplifier().Simplify(blocks[2]), P).Compile();
        Assert.AreEqual(4.0, compiled(3.0));
        CollectionAssert.AreEqual(new[] { "effect" }, log.Calls);
    }

    /// <summary>
    /// Constructions with arguments, without arguments, with members and value-type defaults keep their constructor and
    /// members. ETC R1b traverses the arguments; a creation without argument has no child and is still returned as is.
    /// </summary>
    [TestMethod]
    public void New_AllShapes_AreTraversed()
    {
        Expression withArgument = Expression.New(typeof(StrongBox<double>).GetConstructor([typeof(double)])!, Leaf());
        var withMembers = (NewExpression)ExpressionNodeCatalog.Get("New.Members").Build();
        Expression[] withoutArgument = [Expression.New(typeof(StrongBox<double>)), Expression.New(typeof(DateTime))];

        foreach (Expression creation in AssertTraversed(withArgument))
        {
            Assert.AreSame(((NewExpression)withArgument).Constructor, ((NewExpression)creation).Constructor);
        }

        foreach (Expression creation in withoutArgument)
        {
            AssertPassedThrough(creation);
        }

        var simplifiedMembers = (NewExpression)new ExpressionSimplifier().Simplify(withMembers);
        CollectionAssert.AreEqual(withMembers.Members!.ToArray(), simplifiedMembers.Members!.ToArray());
        Assert.AreEqual(3.0, Expression.Lambda<Func<double, StrongBox<double>>>(new ExpressionSimplifier().Simplify(withArgument), P).Compile()(3.0).Value);
        Assert.AreEqual(default, Expression.Lambda<Func<DateTime>>(new ExpressionSimplifier().Simplify(withoutArgument[1])).Compile()());
    }

    /// <summary>
    /// Type tests keep their node type and type operand and still evaluate identically. ETC R1b traverses the tested
    /// expression.
    /// </summary>
    [TestMethod]
    public void TypeIsAndTypeEqual_AreTraversed()
    {
        Expression boxed = Expression.Convert(Leaf(), typeof(object));
        Expression[] tests = [Expression.TypeIs(boxed, typeof(double)), Expression.TypeEqual(boxed, typeof(ValueType))];

        foreach (Expression test in tests)
        {
            foreach (Expression result in AssertTraversed(test))
            {
                Assert.AreEqual(((TypeBinaryExpression)test).TypeOperand, ((TypeBinaryExpression)result).TypeOperand);
            }
        }

        Assert.IsTrue(Expression.Lambda<Func<double, bool>>(new ExpressionSimplifier().Simplify(tests[0]), P).Compile()(3.0));
        Assert.IsFalse(Expression.Lambda<Func<double, bool>>(new ExpressionSimplifier().Simplify(tests[1]), P).Compile()(3.0));
    }

    /// <summary>A loop with break and continue labels survives and still runs identically.</summary>
    [TestMethod]
    public void Loop_WithBreakAndContinue_IsKept()
    {
        ParameterExpression counter = Expression.Variable(typeof(double), "counter");
        LabelTarget breakLabel = Expression.Label(typeof(double), "break");
        LabelTarget continueLabel = Expression.Label("continue");
        LoopExpression loop = Expression.Loop(
            Expression.Block(
                Expression.IfThen(Expression.GreaterThanOrEqual(counter, Leaf()), Expression.Break(breakLabel, counter)),
                Expression.PreIncrementAssign(counter),
                Expression.Continue(continueLabel)),
            breakLabel,
            continueLabel);
        Expression program = Expression.Block([counter], Expression.Assign(counter, Expression.Constant(0.0)), loop);

        AssertPassedThrough(loop);
        AssertPassedThrough(program);
        Assert.AreEqual(3.0, Expression.Lambda<Func<double, double>>(new ExpressionSimplifier().Simplify(program), P).Compile()(3.0));
    }

    /// <summary>Every catalog sample rooted at an untraversed family is returned as the very same instance by both simplifiers.</summary>
    [TestMethod]
    public void EveryUntraversedCatalogSample_IsReturnedUnchanged()
    {
        ExpressionNodeSample[] samples = ExpressionNodeCatalog.All.Where(sample => UntraversedRoots.Contains(sample.RootNodeType)).ToArray();
        Assert.IsTrue(samples.Length > 20, $"The catalog must keep covering the untraversed families ({samples.Length} samples).");

        foreach (ExpressionNodeSample sample in samples)
        {
            Expression tree = sample.Build();
            Assert.AreSame(tree, new ExpressionSimplifier().Simplify(tree), sample.Name);
            Assert.AreSame(tree, new DerivedSimplifier().Simplify(tree), sample.Name);
        }
    }

    /// <summary>Repeated simplification of an untraversed tree is stable, and simplifying the result again changes nothing.</summary>
    [TestMethod]
    public void UntraversedTree_RepeatedSimplification_IsStable()
    {
        Expression block = ExpressionNodeCatalog.Get("Block.TypedResult").Build();
        var simplifier = new ExpressionSimplifier();

        Expression first = simplifier.Simplify(block);
        Expression second = simplifier.Simplify(block);

        Assert.AreSame(block, first);
        Assert.AreSame(first, second);
        Assert.AreSame(first, simplifier.Simplify(first));
    }

    /// <summary>
    /// An untraversed node inside a supported one is kept while the supported parts around it are still simplified. ETC R1b
    /// traverses array initializers, so the untraversed node is now a block (an R2 family).
    /// </summary>
    [TestMethod]
    public void UntraversedNode_InsideSupportedNode_IsKeptAndNeighborsSimplified()
    {
        BlockExpression block = Expression.Block(Leaf(), Leaf());
        var original = Expression.Lambda<Func<double, double>>(Expression.Add(Leaf(), block), P);

        Expression<Func<double, double>> simplified = Simplify(original);

        AssertSameValues(original, simplified);
        var collector = new NodeCollector();
        collector.Visit(simplified);
        Assert.IsTrue(collector.Nodes.Contains(block), "The block instance must be kept.");
        Assert.AreEqual(2, collector.Nodes.OfType<BinaryExpression>().Count(b => b.NodeType == ExpressionType.Multiply),
            "Only the two p * 1 kept inside the block may remain: the one outside must still be simplified.");
    }

    /// <summary>Collects every node of a tree.</summary>
    private sealed class NodeCollector : ExpressionVisitor
    {
        /// <summary>Gets the visited nodes.</summary>
        public HashSet<Expression> Nodes { get; } = new(ReferenceEqualityComparer.Instance);

        /// <inheritdoc/>
        public override Expression? Visit(Expression? node)
        {
            if (node is not null) Nodes.Add(node);
            return base.Visit(node);
        }
    }

    /// <summary>
    /// A derived simplifier overriding <c>FinalizeExpression</c> still observes untraversed nodes with an empty child list,
    /// and, since ETC R1b (C-03), observes an array initializer with its prepared elements.
    /// </summary>
    [TestMethod]
    public void DerivedSimplifier_FinalizeOverride_StillObservesUntraversedNodes()
    {
        var simplifier = new RecordingSimplifier();
        Expression block = Expression.Block(Leaf());
        Expression array = Expression.NewArrayInit(typeof(double), Leaf());

        Assert.AreSame(block, simplifier.Simplify(block));
        CollectionAssert.Contains(simplifier.Finalized, (ExpressionType.Block, 0));
        Assert.AreNotSame(array, simplifier.Simplify(array));
        CollectionAssert.Contains(simplifier.Finalized, (ExpressionType.NewArrayInit, 1));
    }

    /// <summary>
    /// A generic transformer keeps its full control over untraversed nodes: its rule still matches a type test, and its
    /// <c>FinalizeExpression</c> override still receives a block (with an empty child list) and its result is used.
    /// </summary>
    [TestMethod]
    public void GenericTransformer_RulesAndFinalizeOverride_StillApplyToUntraversedNodes()
    {
        var transformer = new OverridingTransformer();

        Expression typeTest = transformer.Transform(Expression.TypeIs(Expression.Constant("text"), typeof(int)));
        Expression block = transformer.Transform(Expression.Block(Leaf()));

        Assert.IsTrue((bool)((ConstantExpression)typeTest).Value!);
        Assert.AreEqual(-1.0, ((ConstantExpression)block).Value);
        CollectionAssert.Contains(transformer.Finalized, (ExpressionType.Block, 0));
    }

    /// <summary>Every untraversed catalog sample can be added to, and found in, a <see cref="HashSet{T}"/> using the public comparer.</summary>
    [TestMethod]
    public void Comparer_HashSet_AcceptsEveryUntraversedCatalogSample()
    {
        var set = new HashSet<Expression>(ExpressionComparer.Default);
        foreach (ExpressionNodeSample sample in ExpressionNodeCatalog.All.Where(sample => UntraversedRoots.Contains(sample.RootNodeType)))
        {
            Expression tree = sample.Build();
            set.Add(tree);
            Assert.IsTrue(set.Contains(tree), sample.Name);
        }
    }

    /// <summary>
    /// A large untraversed tree (a block of 2,000 simplifiable statements) is returned unchanged; the same width under an array
    /// initializer, traversed since ETC R1b, is simplified element by element.
    /// </summary>
    [TestMethod]
    public void LargeUntraversedTree_IsReturnedUnchanged()
    {
        Expression block = Expression.Block(Enumerable.Range(0, 2000).Select(_ => Leaf()));
        Expression array = Expression.NewArrayInit(typeof(double), Enumerable.Range(0, 2000).Select(_ => Leaf()));

        Assert.AreSame(block, new ExpressionSimplifier().Simplify(block));
        var simplified = (NewArrayExpression)new ExpressionSimplifier().Simplify(array);
        Assert.AreEqual(2000, simplified.Expressions.Count);
        Assert.IsTrue(simplified.Expressions.All(element => ReferenceEquals(element, P)));
    }

    // ------------------------------------------------------------------------------------------
    // C-02: the CopyExpression arity contract for the families rebuilt by R1a.
    // ------------------------------------------------------------------------------------------

    /// <summary>An empty child list returns the node itself for every family covered by C-02.</summary>
    [TestMethod]
    public void CopyExpression_EmptyChildList_ReturnsUntraversedNode()
    {
        string[] names = ["TypeIs", "TypeEqual", "New", "New.Members", "NewArrayInit", "NewArrayBounds", "Block", "Block.TypedResult", "Loop"];
        foreach (string name in names)
        {
            Expression node = ExpressionNodeCatalog.Get(name).Build();
            Assert.AreSame(node, ExposedTransformer.Copy(node), name);
        }
    }

    /// <summary>An explicit child list of the right arity rebuilds the node with the new children and its metadata.</summary>
    [TestMethod]
    public void CopyExpression_ExplicitChildren_RebuildFaithfully()
    {
        Expression two = Expression.Constant(2.0);

        var array = (NewArrayExpression)ExposedTransformer.Copy(Expression.NewArrayInit(typeof(double), Leaf(), Leaf()), two, two);
        var matrix = (NewArrayExpression)ExposedTransformer.Copy(Expression.NewArrayBounds(typeof(double), Expression.Constant(1), Expression.Constant(1)), Expression.Constant(2), Expression.Constant(3));
        var typeEqual = (TypeBinaryExpression)ExposedTransformer.Copy(Expression.TypeEqual(Expression.Constant("x", typeof(object)), typeof(string)), Expression.Constant(5, typeof(object)));
        var block = (BlockExpression)ExposedTransformer.Copy(ExpressionNodeCatalog.Get("Block.TypedResult").Build(), [.. ((BlockExpression)ExpressionNodeCatalog.Get("Block.TypedResult").Build()).Expressions]);

        CollectionAssert.AreEqual(new Expression[] { two, two }, array.Expressions.ToArray());
        Assert.AreEqual(typeof(double[,]), matrix.Type);
        Assert.AreEqual(typeof(string), typeEqual.TypeOperand);
        Assert.AreEqual(ExpressionType.TypeEqual, typeEqual.NodeType);
        Assert.AreEqual(ExpressionNodeCatalog.Get("Block.TypedResult").Build().Type, block.Type);
    }

    /// <summary>A child list of another non-zero length is rejected with <see cref="ArgumentException"/> instead of corrupting the node.</summary>
    [TestMethod]
    public void CopyExpression_WrongArity_Throws()
    {
        Expression one = Expression.Constant(1.0);

        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(Expression.NewArrayInit(typeof(double), one, one), one));
        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(Expression.NewArrayBounds(typeof(double), Expression.Constant(1)), Expression.Constant(1), Expression.Constant(2)));
        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(Expression.TypeIs(one, typeof(double)), one, one));
        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(Expression.Block(one, one), one));
        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(ExpressionNodeCatalog.Get("Loop").Build(), one, one));
        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(Expression.New(typeof(StrongBox<double>)), one));
    }

    // ------------------------------------------------------------------------------------------
    // ETC-11: invocation inlining guard.
    // ------------------------------------------------------------------------------------------

    /// <summary>A parameter that the body never reads does not block inlining of an inert argument.</summary>
    [TestMethod]
    public void Invoke_ParameterNotUsed_InertArgument_IsInlined()
    {
        ParameterExpression q = Expression.Parameter(typeof(double), "q");
        var original = Expression.Lambda<Func<double, double>>(Expression.Invoke(Expression.Lambda(Expression.Constant(7.0), q), P), P);

        Expression<Func<double, double>> simplified = Simplify(original);

        Assert.AreEqual(7.0, ((ConstantExpression)simplified.Body).Value);
    }

    /// <summary>Parameters read only inside supported nodes, once or several times, are substituted.</summary>
    [TestMethod]
    public void Invoke_ParametersUsedInSupportedNodes_AreInlined()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        ParameterExpression b = Expression.Parameter(typeof(double), "b");
        var once = Expression.Lambda<Func<double, double>>(Expression.Invoke(Expression.Lambda(Expression.Multiply(a, Expression.Constant(2.0)), a), P), P);
        var twice = Expression.Lambda<Func<double, double>>(Expression.Invoke(Expression.Lambda(Expression.Add(Expression.Multiply(a, a), a), a), P), P);
        var several = Expression.Lambda<Func<double, double>>(Expression.Invoke(Expression.Lambda(Expression.Subtract(a, b), a, b), P, Expression.Constant(2.0)), P);

        foreach (Expression<Func<double, double>> original in new[] { once, twice, several })
        {
            Expression<Func<double, double>> simplified = Simplify(original);
            AssertSameValues(original, simplified);
            Assert.AreEqual(0, CountLambdaInvocations(simplified));
        }
    }

    /// <summary>
    /// A parameter used in a constructor argument read through a field: R1a kept the invocation (member access and creation
    /// were untraversed); since ETC R1b substitution enters both families, so the inert argument is substituted and the
    /// invocation inlined, and the result still computes the same values.
    /// </summary>
    [TestMethod]
    public void Invoke_ParameterInConstructorArgument_IsInlinedSinceR1b()
    {
        ParameterExpression q = Expression.Parameter(typeof(double), "q");
        Expression body = Expression.Field(Expression.New(typeof(StrongBox<double>).GetConstructor([typeof(double)])!, q), nameof(StrongBox<double>.Value));
        var original = Expression.Lambda<Func<double, double>>(Expression.Invoke(Expression.Lambda(body, q), P), P);

        Expression<Func<double, double>> simplified = Simplify(original);

        AssertSameValues(original, simplified);
        Assert.AreEqual(0, CountLambdaInvocations(simplified));
    }

    /// <summary>An inner lambda redeclaring the invoked parameter (shadowing) keeps the invocation and the original binding.</summary>
    [TestMethod]
    public void Invoke_ShadowedParameter_IsKept()
    {
        ParameterExpression q = Expression.Parameter(typeof(double), "q");
        Expression inner = Expression.Invoke(Expression.Lambda(Expression.Multiply(q, Expression.Constant(3.0)), q), Expression.Add(q, Expression.Constant(1.0)));
        var original = Expression.Lambda<Func<double, double>>(Expression.Invoke(Expression.Lambda(inner, q), P), P);

        Expression<Func<double, double>> simplified = Simplify(original);

        AssertSameValues(original, simplified);
    }

    /// <summary>Invocations returning delegates, and delegate arguments, keep the right bindings.</summary>
    [TestMethod]
    public void Invoke_DelegatesAsResultsAndArguments_KeepBindings()
    {
        ParameterExpression q = Expression.Parameter(typeof(double), "q");
        ParameterExpression f = Expression.Parameter(typeof(Func<double, double>), "f");
        Expression returned = Expression.Invoke(Expression.Invoke(Expression.Lambda(Expression.Lambda<Func<double>>(q), q), P));
        Expression constantArgument = Expression.Invoke(Expression.Lambda(Expression.Invoke(f, P), f), Expression.Constant(CatalogHelpers.Twice));
        Expression lambdaArgument = Expression.Invoke(Expression.Lambda(Expression.Invoke(f, P), f), Expression.Lambda<Func<double, double>>(Expression.Multiply(q, Expression.Constant(3.0)), q));

        foreach (Expression body in new[] { returned, constantArgument, lambdaArgument })
        {
            var original = Expression.Lambda<Func<double, double>>(body, P);
            AssertSameValues(original, Simplify(original));
        }
    }

    /// <summary>Nested invocations of inert arguments are inlined level by level.</summary>
    [TestMethod]
    public void Invoke_NestedInvocations_AreInlined()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        ParameterExpression b = Expression.Parameter(typeof(double), "b");
        Expression inner = Expression.Invoke(Expression.Lambda(Expression.Multiply(b, Expression.Constant(2.0)), b), Expression.Add(a, Expression.Constant(0.0)));
        var original = Expression.Lambda<Func<double, double>>(Expression.Invoke(Expression.Lambda(inner, a), P), P);

        Expression<Func<double, double>> simplified = Simplify(original);

        AssertSameValues(original, simplified);
        Assert.AreEqual(0, CountLambdaInvocations(simplified));
    }

    /// <summary>A custom-operator argument is not inert: the invocation is kept and the operator still runs.</summary>
    [TestMethod]
    public void Invoke_CustomOperatorArgument_IsKept()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        MethodInfo addTen = typeof(ExpressionTreeR1aTests).GetMethod(nameof(AddTen))!;
        var original = Expression.Lambda<Func<double, double>>(
            Expression.Invoke(Expression.Lambda(Expression.Add(a, a), a), Expression.Add(P, P, addTen)), P);

        Expression<Func<double, double>> simplified = Simplify(original);

        AssertSameValues(original, simplified);
        Assert.AreEqual(1, CountLambdaInvocations(simplified));
    }

    /// <summary>
    /// Side-effecting arguments are evaluated exactly once and in order, even when the body drops, repeats or swaps
    /// their uses: such invocations are kept.
    /// </summary>
    [TestMethod]
    public void Invoke_SideEffectingArguments_KeepCallSequence()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        ParameterExpression b = Expression.Parameter(typeof(double), "b");
        var log = new CallLog();
        Expression[] bodies =
        [
            Expression.Invoke(Expression.Lambda(Expression.Constant(1.0), a), log.Call("unused", 5.0)),
            Expression.Invoke(Expression.Lambda(Expression.Add(a, a), a), log.Call("twice", 5.0)),
            Expression.Invoke(Expression.Lambda(Expression.Subtract(b, a), a, b), log.Call("first", 5.0), log.Call("second", 7.0)),
        ];

        foreach (Expression body in bodies)
        {
            var original = Expression.Lambda<Func<double, double>>(body, P);
            Expression<Func<double, double>> simplified = Simplify(original);

            log.Calls.Clear();
            double expected = original.Compile()(3.0);
            string[] expectedCalls = [.. log.Calls];
            log.Calls.Clear();
            double actual = simplified.Compile()(3.0);

            Assert.AreEqual(expected, actual);
            CollectionAssert.AreEqual(expectedCalls, log.Calls);
        }
    }

    /// <summary>An argument that throws still throws after simplification, even when the body does not use it.</summary>
    [TestMethod]
    public void Invoke_ThrowingUnusedArgument_StillThrows()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        var log = new CallLog();
        Expression failing = Expression.Call(Expression.Constant(log), CallLog.FailMethod, Expression.Constant("boom"));
        var original = Expression.Lambda<Func<double, double>>(Expression.Invoke(Expression.Lambda(Expression.Constant(1.0), a), failing), P);

        Func<double, double> compiled = Simplify(original).Compile();

        Assert.ThrowsExactly<InvalidOperationException>(() => compiled(3.0));
        CollectionAssert.AreEqual(new[] { "boom" }, log.Calls);
    }

    /// <summary>
    /// A parameter argument read after the body may have written the variable keeps the invocation: the parameter still
    /// sees the value it had when the invocation started.
    /// </summary>
    [TestMethod]
    public void Invoke_ParameterArgumentReadAfterWrite_IsKept()
    {
        ParameterExpression q = Expression.Parameter(typeof(double), "q");
        ParameterExpression v = Expression.Variable(typeof(double), "v");
        MethodInfo combine = typeof(ExpressionTreeR1aTests).GetMethod(nameof(Combine))!;
        Expression body = Expression.Call(combine, Expression.Assign(v, Expression.Constant(5.0)), q);
        Expression invocation = Expression.Invoke(Expression.Lambda(body, q), v);
        var original = Expression.Lambda<Func<double, double>>(Expression.Block([v], Expression.Assign(v, P), invocation), P);

        Expression simplifiedInvocation = new ExpressionSimplifier().Simplify(invocation);

        Assert.AreEqual(53.0, original.Compile()(3.0));
        Assert.AreEqual(1, CountLambdaInvocations(simplifiedInvocation));
        var rebuilt = Expression.Lambda<Func<double, double>>(Expression.Block([v], Expression.Assign(v, P), simplifiedInvocation), P);
        Assert.AreEqual(53.0, rebuilt.Compile()(3.0));
    }

    /// <summary>An assignment to the invoked parameter keeps the invocation (a constant cannot become an assignment target).</summary>
    [TestMethod]
    public void Invoke_AssignmentToParameter_IsKept()
    {
        ParameterExpression q = Expression.Parameter(typeof(double), "q");
        Expression body = Expression.Block(Expression.AddAssign(q, Expression.Constant(1.0)), q);
        Expression direct = Expression.Invoke(Expression.Lambda(Expression.PreIncrementAssign(q), q), Expression.Constant(2.0));
        Expression viaBlock = Expression.Invoke(Expression.Lambda(body, q), Expression.Constant(2.0));

        foreach (Expression invocation in new[] { direct, viaBlock })
        {
            Expression simplified = new ExpressionSimplifier().Simplify(invocation);
            Assert.AreEqual(3.0, Expression.Lambda<Func<double>>(simplified).Compile()());
        }
    }

    /// <summary>
    /// A custom unary operator in the body: R1a kept the invocation, since substitution dropped the method (ETC-04). Since
    /// ETC R1b (C-01) substitution rebuilds the operator faithfully, so the exact and a derived simplifier inline the
    /// invocation and keep the operator method.
    /// </summary>
    [TestMethod]
    public void Invoke_BodyWithCustomUnaryOperator_IsInlinedWithItsMethod()
    {
        ParameterExpression q = Expression.Parameter(typeof(double), "q");
        var original = Expression.Lambda<Func<double, double>>(
            Expression.Invoke(Expression.Lambda(Expression.Negate(q, CatalogHelpers.FortyTwoMethod), q), P), P);

        foreach (ExpressionSimplifier simplifier in new[] { new ExpressionSimplifier(), new DerivedSimplifier() })
        {
            Expression<Func<double, double>> simplified = Simplify(original, simplifier);

            AssertSameValues(original, simplified);
            Assert.AreEqual(0, CountLambdaInvocations(simplified));
            Assert.AreSame(CatalogHelpers.FortyTwoMethod, ((UnaryExpression)simplified.Body).Method);
        }
    }

    /// <summary>The ETC-11 reproducers compile and compute the same values through a derived simplifier too.</summary>
    [TestMethod]
    public void Invoke_DerivedSimplifier_DoesNotLeaveUnboundParameters()
    {
        ParameterExpression q = Expression.Parameter(typeof(double), "q");
        var nested = Expression.Lambda<Func<double, double>>(
            Expression.Invoke(Expression.Invoke(Expression.Lambda<Func<double, Func<double>>>(Expression.Lambda<Func<double>>(q), q), P)), P);
        var member = Expression.Lambda<Func<double, double>>(
            Expression.Invoke(Expression.Lambda<Func<double, double>>(Expression.Property(Expression.Call(typeof(TimeSpan).GetMethod(nameof(TimeSpan.FromSeconds), [typeof(double)])!, q), nameof(TimeSpan.TotalSeconds)), q), P), P);

        AssertSameValues(nested, Simplify(nested, new DerivedSimplifier()));
        AssertSameValues(member, Simplify(member, new DerivedSimplifier()));
    }

    /// <summary>A kept invocation is stable: simplifying the result again returns an equivalent tree with the invocation still in place.</summary>
    [TestMethod]
    public void Invoke_KeptInvocation_IsStableUnderRepeatedSimplification()
    {
        ParameterExpression q = Expression.Parameter(typeof(double), "q");
        var original = Expression.Lambda<Func<double, double>>(
            Expression.Invoke(Expression.Invoke(Expression.Lambda<Func<double, Func<double>>>(Expression.Lambda<Func<double>>(q), q), P)), P);

        Expression<Func<double, double>> once = Simplify(original);
        Assert.AreEqual(1, CountLambdaInvocations(once));
        Expression<Func<double, double>> twice = Simplify(once);

        AssertSameValues(original, twice);
        Assert.AreEqual(CountLambdaInvocations(once), CountLambdaInvocations(twice));
    }

    // ------------------------------------------------------------------------------------------
    // ETC-13: rethrow.
    // ------------------------------------------------------------------------------------------

    /// <summary>Typed and void rethrows are kept, with their type, by every transformation path.</summary>
    [TestMethod]
    public void Rethrow_TypedAndVoid_AreKeptByEveryPath()
    {
        foreach (UnaryExpression rethrow in new[] { Expression.Rethrow(typeof(double)), Expression.Rethrow() })
        {
            Assert.AreSame(rethrow, new ExpressionSimplifier().Simplify(rethrow));
            Assert.AreSame(rethrow, new DerivedSimplifier().Simplify(rethrow));
            Assert.AreSame(rethrow, new RecursiveTransformer().Transform(rethrow));
            Assert.AreSame(rethrow, new ExposedTransformer().Replace(rethrow, P, Expression.Constant(5.0)));
            Assert.AreSame(rethrow, ExposedTransformer.Copy(rethrow));
        }
    }

    /// <summary>A typed rethrow nested in a supported node keeps its type through simplification and substitution.</summary>
    [TestMethod]
    public void Rethrow_InsideConditional_KeepsTypeThroughEveryPath()
    {
        Expression conditional = Expression.Condition(Expression.GreaterThan(P, Expression.Constant(0.0)), Expression.Rethrow(typeof(double)), Leaf());

        Expression[] results =
        [
            new ExpressionSimplifier().Simplify(conditional),
            new DerivedSimplifier().Simplify(conditional),
            new RecursiveTransformer().Transform(conditional),
            new ExposedTransformer().Replace(conditional, P, Expression.Constant(5.0)),
        ];

        foreach (Expression result in results)
        {
            var rebuilt = (ConditionalExpression)result;
            Assert.AreEqual(typeof(double), rebuilt.IfTrue.Type);
            Assert.AreEqual(ExpressionType.Throw, rebuilt.IfTrue.NodeType);
            Assert.IsNull(((UnaryExpression)rebuilt.IfTrue).Operand);
        }
    }

    /// <summary>A simplified catch body containing a rethrow still rethrows the caught exception; a whole try expression is kept.</summary>
    [TestMethod]
    public void Rethrow_InCatchBody_StillRethrows()
    {
        Expression thrower = Expression.Throw(Expression.Call(CatalogHelpers.MakeExceptionMethod, P), typeof(double));
        Expression catchBody = Expression.Condition(Expression.GreaterThan(P, Expression.Constant(0.0)), Expression.Rethrow(typeof(double)), Leaf());
        TryExpression original = Expression.TryCatch(thrower, Expression.Catch(typeof(InvalidOperationException), catchBody));

        Expression simplifiedBody = new ExpressionSimplifier().Simplify(catchBody);
        TryExpression rebuilt = Expression.TryCatch(thrower, Expression.Catch(typeof(InvalidOperationException), simplifiedBody));
        Func<double, double> expected = Expression.Lambda<Func<double, double>>(original, P).Compile();
        Func<double, double> actual = Expression.Lambda<Func<double, double>>(rebuilt, P).Compile();

        Assert.AreSame(original, new ExpressionSimplifier().Simplify(original));
        Assert.ThrowsExactly<InvalidOperationException>(() => expected(3.0));
        Assert.ThrowsExactly<InvalidOperationException>(() => actual(3.0));
        Assert.AreEqual(expected(-2.0), actual(-2.0));
    }
}
