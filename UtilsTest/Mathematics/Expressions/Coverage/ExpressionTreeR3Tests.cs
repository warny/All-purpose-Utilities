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
/// ETC R3, checkpoint A: traversal and faithful reconstruction of the initializer and index families (MemberInit with its
/// MemberAssignment, MemberMemberBinding and MemberListBinding bindings, ListInit with its ElementInit initializers, Index),
/// the positional child layout and preparation order observed by subclasses (C-03, H-1, H-2, H-4), the <c>CopyExpression</c>
/// arity contract of these families (C-02), executable original-versus-transformed equivalence with traced user code, and
/// index accesses kept writable as assignment targets.
/// </summary>
[TestClass]
[TestCategory("ETC-R3")]
public class ExpressionTreeR3Tests
{
    /// <summary>The free parameter of the samples; the executable samples run with <c>p = 1</c>.</summary>
    private static readonly ParameterExpression P = Expression.Parameter(typeof(double), "p");

    /// <summary>The names of the catalog samples of the R3 families.</summary>
    private static readonly string[] R3Samples =
        ["MemberInit", "MemberInit.MemberMemberBinding", "MemberInit.MemberListBinding", "ListInit", "ListInit.ExplicitAddMethod", "Index", "Index.Indexer"];

    /// <summary>Builds the simplifiable leaf <c>p * 1.0</c>.</summary>
    /// <returns>A new leaf.</returns>
    private static Expression Leaf() => Expression.Multiply(P, Expression.Constant(1.0));

    /// <summary>Builds the simplifiable integer leaf <c>(int)(p * 1.0)</c>.</summary>
    /// <returns>A new leaf.</returns>
    private static Expression IntLeaf() => Expression.Convert(Leaf(), typeof(int));

    /// <summary>A derived simplifier overriding nothing.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
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
        protected override Expression PrepareExpression(Expression e) => TransformCore(e);

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => CopyExpression(e, parameters);
    }

    /// <summary>The same rule as <see cref="RecursiveTransformer"/>, without overriding <c>PrepareExpression</c>.</summary>
    private sealed class NonRecursiveTransformer : ExpressionTransformer
    {
        /// <summary>Rewrites the marker constant.</summary>
        /// <param name="constant">The constant.</param>
        /// <returns>The rewritten or original constant.</returns>
        [ExpressionSignature(ExpressionType.Constant)]
        private Expression Rewrite(ConstantExpression constant) => constant.Value is 1.0 ? Expression.Constant(2.0) : constant;

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => CopyExpression(e, parameters);
    }

    /// <summary>
    /// A recursive identity transformer recording every node passed to <c>PrepareExpression</c> and every
    /// <c>FinalizeExpression</c> call with its child list, so the preparation order and the child layout are observable.
    /// </summary>
    private sealed class RecordingTransformer : ExpressionTransformer
    {
        /// <summary>Gets the nodes passed to <c>PrepareExpression</c>, in call order.</summary>
        public List<Expression> Prepared { get; } = [];

        /// <summary>Gets the finalized nodes with their child lists, in call order.</summary>
        public List<(Expression Node, Expression[] Children)> Finalized { get; } = [];

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <inheritdoc/>
        protected override Expression PrepareExpression(Expression e)
        {
            Prepared.Add(e);
            return TransformCore(e);
        }

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
        {
            Finalized.Add((e, parameters));
            return CopyExpression(e, parameters);
        }
    }

    /// <summary>A transformer with positional rules on the R3 families (H-1): each receives the prepared children.</summary>
    private sealed class R3RuleTransformer : ExpressionTransformer
    {
        /// <summary>Gets the node types and child counts the rules received.</summary>
        public List<(ExpressionType NodeType, int Children)> Seen { get; } = [];

        /// <summary>Records a member initialization and keeps it.</summary>
        /// <param name="node">The prepared node.</param>
        /// <param name="children">The prepared children.</param>
        /// <returns>The node.</returns>
        [ExpressionSignature(ExpressionType.MemberInit)]
        private Expression MemberInit(MemberInitExpression node, Expression[] children) => Record(node, children);

        /// <summary>Records a list initialization and keeps it.</summary>
        /// <param name="node">The prepared node.</param>
        /// <param name="children">The prepared children.</param>
        /// <returns>The node.</returns>
        [ExpressionSignature(ExpressionType.ListInit)]
        private Expression ListInit(ListInitExpression node, Expression[] children) => Record(node, children);

        /// <summary>Records an index access and keeps it.</summary>
        /// <param name="node">The prepared node.</param>
        /// <param name="children">The prepared children.</param>
        /// <returns>The node.</returns>
        [ExpressionSignature(ExpressionType.Index)]
        private Expression Index(IndexExpression node, Expression[] children) => Record(node, children);

        /// <summary>Records one rule call.</summary>
        /// <param name="node">The node.</param>
        /// <param name="children">The children.</param>
        /// <returns>The node.</returns>
        private Expression Record(Expression node, Expression[] children)
        {
            Seen.Add((node.NodeType, children.Length));
            return node;
        }

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <inheritdoc/>
        protected override Expression PrepareExpression(Expression e) => TransformCore(e);

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => CopyExpression(e, parameters);
    }

    /// <summary>Exposes the protected static <c>CopyExpression</c>.</summary>
    private sealed class ExposedTransformer : ExpressionTransformer
    {
        /// <summary>Calls <c>CopyExpression</c>.</summary>
        /// <param name="e">The node to copy.</param>
        /// <param name="parameters">The replacement children.</param>
        /// <returns>The copy.</returns>
        public static Expression Copy(Expression e, params Expression[] parameters) => CopyExpression(e, parameters);

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);
    }

    /// <summary>The transformations whose results must behave like the original: the exact simplifier, a derived simplifier and a recursive identity transformer.</summary>
    private static readonly Func<Expression, Expression>[] BehaviorPreservingTransforms =
    [
        tree => new ExpressionSimplifier().Simplify(tree),
        tree => new DerivedSimplifier().Simplify(tree),
        tree => new RecordingTransformer().Transform(tree),
    ];

    /// <summary>Collects every node of a tree, the bindings and initializers included.</summary>
    private sealed class NodeCollector : ExpressionVisitor
    {
        /// <summary>Gets the visited nodes.</summary>
        public List<Expression> Nodes { get; } = [];

        /// <inheritdoc/>
        public override Expression? Visit(Expression? node)
        {
            if (node is not null) Nodes.Add(node);
            return base.Visit(node);
        }
    }

    /// <summary>Gets every node of <paramref name="tree"/>.</summary>
    /// <param name="tree">The tree.</param>
    /// <returns>The nodes, in visiting order.</returns>
    private static List<Expression> NodesOf(Expression tree)
    {
        var collector = new NodeCollector();
        collector.Visit(tree);
        return collector.Nodes;
    }

    /// <summary>
    /// Gets the structural metadata of an R3 node that a rebuild must keep: constructor, then each binding's kind and member
    /// (recursively) or each initializer's <c>Add</c> method and arity; for an index access, the indexer, the receiver
    /// presence and the argument count.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The metadata, compared by <see cref="object.Equals(object?)"/>.</returns>
    private static List<object?> Metadata(Expression node)
    {
        var metadata = new List<object?> { node.NodeType, node.Type };
        switch (node)
        {
            case MemberInitExpression memberInit:
                metadata.Add(memberInit.NewExpression.Constructor);
                AddBindings(metadata, memberInit.Bindings);
                break;
            case ListInitExpression listInit:
                metadata.Add(listInit.NewExpression.Constructor);
                AddInitializers(metadata, listInit.Initializers);
                break;
            case IndexExpression index:
                metadata.Add(index.Indexer);
                metadata.Add(index.Object is null);
                metadata.Add(index.Arguments.Count);
                break;
        }

        return metadata;

        static void AddBindings(List<object?> metadata, IEnumerable<MemberBinding> bindings)
        {
            foreach (MemberBinding binding in bindings)
            {
                metadata.Add(binding.BindingType);
                metadata.Add(binding.Member);
                if (binding is MemberMemberBinding memberBinding) AddBindings(metadata, memberBinding.Bindings);
                if (binding is MemberListBinding listBinding) AddInitializers(metadata, listBinding.Initializers);
            }
        }

        static void AddInitializers(List<object?> metadata, IEnumerable<ElementInit> initializers)
        {
            foreach (ElementInit initializer in initializers)
            {
                metadata.Add(initializer.AddMethod);
                metadata.Add(initializer.Arguments.Count);
            }
        }
    }

    /// <summary>Joins an evaluated value and a state description, in that evaluation order; used as the body of the executable samples.</summary>
    /// <param name="value">The evaluated value.</param>
    /// <param name="state">The state description.</param>
    /// <returns>The joined text.</returns>
    public static string Pair(object? value, string state) => Describe(value) + "|" + state;

    /// <summary>Describes a result value, using the state description of the instrumented fixtures.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The description.</returns>
    private static string Describe(object? value) => value switch
    {
        null => "null",
        R3TracedHolder holder => holder.Describe(),
        R3TracedList list => "[" + list.Describe() + "]",
        R3TracedGrid grid => "{" + grid.Describe() + "}",
        double[] array => string.Join(",", array.Select(R3TraceLog.Text)),
        double[,] matrix => string.Join(",", matrix.Cast<double>().Select(R3TraceLog.Text)),
        double number => R3TraceLog.Text(number),
        _ => value.ToString() ?? string.Empty,
    };

    /// <summary>Compiles <paramref name="body"/> over <see cref="P"/>, runs it with <c>p = 1</c> and records its result, exception and trace.</summary>
    /// <param name="body">The body.</param>
    /// <param name="log">The trace the body writes to.</param>
    /// <returns>The description of the result (or <see langword="null"/>), the exception type (or <see langword="null"/>), and the trace.</returns>
    private static (string? Value, string? Exception, string[] Trace) Run(Expression body, R3TraceLog log)
    {
        Expression result = body.Type == typeof(void)
            ? Expression.Block(body, Expression.Constant(null, typeof(object)))
            : Expression.Convert(body, typeof(object));
        Func<double, object> compiled = Expression.Lambda<Func<double, object>>(result, P).Compile();
        try
        {
            return (Describe(compiled(1.0)), null, log.Entries.ToArray());
        }
        catch (Exception exception)
        {
            return (null, exception.GetType().FullName, log.Entries.ToArray());
        }
    }

    /// <summary>
    /// Asserts that every behavior-preserving transformation of the tree built by <paramref name="build"/> runs no user code
    /// during the transformation, then produces the same result, the same exception and the very same trace of user code
    /// (calls, order and count) as the original.
    /// </summary>
    /// <param name="build">Builds the tree over a fresh trace.</param>
    /// <returns>The transformed trees, in the order of <see cref="BehaviorPreservingTransforms"/>.</returns>
    private static Expression[] AssertSameBehavior(Func<R3TraceLog, Expression> build)
    {
        var originalLog = new R3TraceLog();
        (string? Value, string? Exception, string[] Trace) expected = Run(build(originalLog), originalLog);

        var transformed = new Expression[BehaviorPreservingTransforms.Length];
        for (int i = 0; i < transformed.Length; i++)
        {
            var log = new R3TraceLog();
            Expression tree = build(log);
            transformed[i] = BehaviorPreservingTransforms[i](tree);
            Assert.AreEqual(0, log.Entries.Count, "No constructor, accessor or Add method may run during the transformation: " + string.Join(" ; ", log.Entries));
            CollectionAssert.AreEqual(Metadata(tree), Metadata(transformed[i]), "The R3 metadata must be kept.");

            (string? Value, string? Exception, string[] Trace) actual = Run(transformed[i], log);
            Assert.AreEqual(expected.Exception, actual.Exception, "Same exception.");
            Assert.AreEqual(expected.Value, actual.Value, "Same result.");
            CollectionAssert.AreEqual(expected.Trace, actual.Trace, "Same user calls, in the same order: " + string.Join(" ; ", actual.Trace));
        }

        return transformed;
    }

    /// <summary>Builds <c>new R3TracedHolder(log)</c>.</summary>
    /// <param name="log">The trace.</param>
    /// <returns>The creation.</returns>
    private static NewExpression NewHolder(R3TraceLog log)
        => Expression.New(typeof(R3TracedHolder).GetConstructor([typeof(R3TraceLog)])!, Expression.Constant(log));

    /// <summary>Builds <c>new R3TracedHolder(log, seed)</c>.</summary>
    /// <param name="log">The trace.</param>
    /// <param name="seed">The seed argument.</param>
    /// <returns>The creation.</returns>
    private static NewExpression NewHolder(R3TraceLog log, Expression seed)
        => Expression.New(typeof(R3TracedHolder).GetConstructor([typeof(R3TraceLog), typeof(double)])!, Expression.Constant(log), seed);

    /// <summary>
    /// A trace parameter, for the trees that must contain no constant: <c>CopyExpression</c> historically recreates every
    /// constant, so a tree holding one is never returned as the same instance.
    /// </summary>
    private static readonly ParameterExpression LogParameter = Expression.Parameter(typeof(R3TraceLog), "log");

    /// <summary>Builds <c>new R3TracedHolder(log, seed)</c> over <see cref="LogParameter"/>.</summary>
    /// <param name="seed">The seed argument.</param>
    /// <returns>The creation.</returns>
    private static NewExpression NewHolder(Expression seed)
        => Expression.New(typeof(R3TracedHolder).GetConstructor([typeof(R3TraceLog), typeof(double)])!, LogParameter, seed);

    /// <summary>Builds <c>new R3TracedList(log)</c> over <see cref="LogParameter"/>.</summary>
    /// <returns>The creation.</returns>
    private static NewExpression NewList()
        => Expression.New(typeof(R3TracedList).GetConstructor([typeof(R3TraceLog)])!, LogParameter);

    /// <summary>Builds <c>new R3TracedList(log)</c>.</summary>
    /// <param name="log">The trace.</param>
    /// <returns>The creation.</returns>
    private static NewExpression NewList(R3TraceLog log)
        => Expression.New(typeof(R3TracedList).GetConstructor([typeof(R3TraceLog)])!, Expression.Constant(log));

    /// <summary>Builds an <c>Add</c> initializer of <see cref="R3TracedList"/> for the given arguments, selecting the overload by their types.</summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The initializer.</returns>
    private static ElementInit Add(params Expression[] arguments)
        => Expression.ElementInit(R3TracedList.AddMethod(arguments.Select(argument => argument.Type).ToArray()), arguments);

    // ------------------------------------------------------------------------------------------
    // Traversal and faithful reconstruction (C-03).
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Every R3 catalog sample is traversed by the exact simplifier, a derived simplifier and a generic recursive transformer:
    /// the result is rebuilt (a descendant changed) with the same node type, type, constructor, binding kinds and members,
    /// <c>Add</c> methods and indexer, and the exact simplifier leaves no <c>p * 1</c> behind.
    /// </summary>
    [TestMethod]
    public void CatalogSamples_AreTraversedAndRebuiltFaithfully()
    {
        foreach (string name in R3Samples)
        {
            Expression tree = ExpressionNodeCatalog.Get(name).Build();
            Expression[] results = [new ExpressionSimplifier().Simplify(tree), new DerivedSimplifier().Simplify(tree), new RecursiveTransformer().Transform(tree)];
            foreach (Expression result in results)
            {
                Assert.AreNotSame(tree, result, name);
                CollectionAssert.AreEqual(Metadata(tree), Metadata(result), name);
            }

            Assert.IsFalse(NodesOf(results[0]).OfType<BinaryExpression>().Any(b => b.NodeType == ExpressionType.Multiply), name + ": every p * 1 must be simplified.");
            Assert.IsFalse(NodesOf(results[2]).OfType<ConstantExpression>().Any(c => c.Value is 1.0), name + ": every descendant must be visited.");
        }
    }

    /// <summary>
    /// A transformation that changes nothing (an identity recursive transformer) returns the very same instances: unchanged
    /// bindings, initializers and nodes are reused rather than rebuilt. (The simplifiers rebuild every constant, so they are
    /// not identity transformations here.)
    /// </summary>
    [TestMethod]
    public void UnchangedTrees_AreReturnedAsTheSameInstances()
    {
        ParameterExpression row = Expression.Parameter(typeof(int), "row");
        Expression memberInit = Expression.MemberInit(
            NewHolder(P),
            Expression.Bind(R3TracedHolder.Member(nameof(R3TracedHolder.Value)), P),
            Expression.MemberBind(R3TracedHolder.Member(nameof(R3TracedHolder.Child)), Expression.Bind(R3TracedHolder.Member(nameof(R3TracedHolder.Other)), P)),
            Expression.ListBind(R3TracedHolder.Member(nameof(R3TracedHolder.Items)), Add(P)));
        Expression listInit = Expression.ListInit(NewList(), Add(P), Add(P, P));
        Expression index = Expression.MakeIndex(Expression.Parameter(typeof(R3TracedGrid), "grid"), R3TracedGrid.CellIndexer, [row, row]);

        foreach (Expression tree in new[] { memberInit, listInit, index })
        {
            Assert.AreSame(tree, new RecordingTransformer().Transform(tree), tree.NodeType.ToString());
        }
    }

    /// <summary>
    /// H-2: children are prepared in structural order — the creation, then each binding in declaration order with the nested
    /// bindings and initializers depth first, each initializer's arguments in order; the receiver of an index access, then its
    /// arguments. <c>FinalizeExpression</c> receives the flattened layout documented on the <c>Prepare*</c> methods (H-3).
    /// </summary>
    [TestMethod]
    public void PreparationOrder_AndChildLayout_FollowTheStructure()
    {
        ParameterExpression[] p = Enumerable.Range(0, 8).Select(i => Expression.Parameter(typeof(double), "a" + i)).ToArray();
        Expression memberInit = Expression.MemberInit(
            NewHolder(p[0]),
            Expression.Bind(R3TracedHolder.Member(nameof(R3TracedHolder.Value)), p[1]),
            Expression.MemberBind(
                R3TracedHolder.Member(nameof(R3TracedHolder.Child)),
                Expression.Bind(R3TracedHolder.Member(nameof(R3TracedHolder.Other)), p[2]),
                Expression.ListBind(R3TracedHolder.Member(nameof(R3TracedHolder.Items)), Add(p[3]), Add(p[4], p[5]))),
            Expression.ListBind(R3TracedHolder.Member(nameof(R3TracedHolder.Items)), Add(p[6])),
            Expression.Bind(R3TracedHolder.Member(nameof(R3TracedHolder.Field)), p[7]));

        var transformer = new RecordingTransformer();
        Assert.AreSame(memberInit, transformer.Transform(memberInit));
        CollectionAssert.AreEqual(p, transformer.Prepared.OfType<ParameterExpression>().Where(parameter => parameter != LogParameter).ToArray());
        Expression[] children = transformer.Finalized.Single(f => f.Node.NodeType == ExpressionType.MemberInit).Children;
        Assert.AreEqual(8, children.Length);
        Assert.IsInstanceOfType<NewExpression>(children[0]);
        CollectionAssert.AreEqual(p.Skip(1).ToArray(), children.Skip(1).ToArray());

        Expression listInit = Expression.ListInit(NewList(), Add(p[0]), Add(p[1], p[2]), Add(Expression.Parameter(typeof(string), "x")));
        transformer = new RecordingTransformer();
        transformer.Transform(listInit);
        children = transformer.Finalized.Single(f => f.Node.NodeType == ExpressionType.ListInit).Children;
        Assert.AreEqual(5, children.Length);
        Assert.IsInstanceOfType<NewExpression>(children[0]);
        CollectionAssert.AreEqual(new Expression[] { p[0], p[1], p[2], ((ListInitExpression)listInit).Initializers[2].Arguments[0] }, children.Skip(1).ToArray());

        ParameterExpression grid = Expression.Parameter(typeof(R3TracedGrid), "grid");
        ParameterExpression row = Expression.Parameter(typeof(int), "row");
        ParameterExpression column = Expression.Parameter(typeof(int), "column");
        Expression index = Expression.MakeIndex(grid, R3TracedGrid.CellIndexer, [row, column]);
        transformer = new RecordingTransformer();
        transformer.Transform(index);
        CollectionAssert.AreEqual(new Expression[] { grid, row, column }, transformer.Prepared.ToArray());
        CollectionAssert.AreEqual(new Expression[] { grid, row, column }, transformer.Finalized.Single(f => f.Node.NodeType == ExpressionType.Index).Children);
    }

    /// <summary>H-1: a positional rule declared on an R3 family now receives the flattened prepared children.</summary>
    [TestMethod]
    public void PositionalRules_ReceiveTheFlattenedChildren()
    {
        var log = new R3TraceLog();
        var transformer = new R3RuleTransformer();
        transformer.Transform(Expression.MemberInit(NewHolder(log, P), Expression.Bind(R3TracedHolder.Member(nameof(R3TracedHolder.Value)), P)));
        transformer.Transform(Expression.ListInit(NewList(log), Add(P), Add(P, P)));
        transformer.Transform(Expression.ArrayAccess(Expression.Constant(new double[2, 2]), Expression.Constant(0), Expression.Constant(1)));

        CollectionAssert.AreEqual(
            new[] { (ExpressionType.MemberInit, 2), (ExpressionType.ListInit, 4), (ExpressionType.Index, 3) },
            transformer.Seen.ToArray());
    }

    /// <summary>
    /// H-4: a transformer that does not override <c>PrepareExpression</c> leaves the children of the R3 families as they are
    /// (the direct lambda-body descent is not extended to them), so its result is the original instance.
    /// </summary>
    [TestMethod]
    public void NonRecursiveTransformer_LeavesTheChildrenUntouched()
    {
        foreach (string name in R3Samples)
        {
            Expression tree = ExpressionNodeCatalog.Get(name).Build();
            Assert.AreSame(tree, new NonRecursiveTransformer().Transform(tree), name);
        }
    }

    /// <summary>
    /// Nested shapes: three levels of member bindings, a member binding holding a list binding, and assigned values holding a
    /// block (with a local variable) and a lambda (ETC R2 binders) are traversed, simplified and run exactly like the original.
    /// </summary>
    [TestMethod]
    public void NestedBindings_AndBinderArguments_AreTraversedAndBehaveTheSame()
    {
        Expression[] results = AssertSameBehavior(log =>
        {
            ParameterExpression local = Expression.Variable(typeof(double), "local");
            ParameterExpression x = Expression.Parameter(typeof(double), "x");
            return Expression.MemberInit(
                NewHolder(log, Leaf()),
                Expression.Bind(R3TracedHolder.Member(nameof(R3TracedHolder.Value)), Expression.Block([local], Expression.Assign(local, Leaf()), Expression.Add(local, Leaf()))),
                Expression.Bind(R3TracedHolder.Member(nameof(R3TracedHolder.Callback)), Expression.Lambda<Func<double, double>>(Expression.Multiply(x, Expression.Constant(1.0)), x)),
                Expression.MemberBind(
                    R3TracedHolder.Member(nameof(R3TracedHolder.Child)),
                    Expression.MemberBind(
                        R3TracedHolder.Member(nameof(R3TracedHolder.Child)),
                        Expression.MemberBind(
                            R3TracedHolder.Member(nameof(R3TracedHolder.Child)),
                            Expression.Bind(R3TracedHolder.Member(nameof(R3TracedHolder.Other)), Leaf())),
                        Expression.ListBind(R3TracedHolder.Member(nameof(R3TracedHolder.Items)), Add(Leaf()), Add(Leaf(), Leaf())))));
        });

        Assert.IsFalse(NodesOf(results[0]).OfType<BinaryExpression>().Any(b => b.NodeType == ExpressionType.Multiply), "The exact simplifier must reach every nested argument, the lambda body included.");
    }

    // ------------------------------------------------------------------------------------------
    // Executable behavior with traced user code.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// A member initialization with a traced constructor, setters, a plain field, nested member bindings (traced getters) and
    /// list bindings using overloaded and two-parameter <c>Add</c> methods produces the same object state and the very same
    /// sequence of user calls after transformation; each argument is evaluated once, in the same position of that sequence.
    /// </summary>
    [TestMethod]
    public void MemberInit_KeepsTheExactSequenceOfUserCalls()
    {
        AssertSameBehavior(log => Expression.MemberInit(
            NewHolder(log, R3TraceLog.Tagged(log, "seed", Leaf())),
            Expression.Bind(R3TracedHolder.Member(nameof(R3TracedHolder.Value)), R3TraceLog.Tagged(log, "value", Leaf())),
            Expression.Bind(R3TracedHolder.Member(nameof(R3TracedHolder.Field)), Expression.Add(Leaf(), Expression.Constant(3.0))),
            Expression.MemberBind(
                R3TracedHolder.Member(nameof(R3TracedHolder.Child)),
                Expression.Bind(R3TracedHolder.Member(nameof(R3TracedHolder.Other)), R3TraceLog.Tagged(log, "other", Leaf())),
                Expression.ListBind(R3TracedHolder.Member(nameof(R3TracedHolder.Items)), Add(Leaf()))),
            Expression.ListBind(
                R3TracedHolder.Member(nameof(R3TracedHolder.Items)),
                Add(R3TraceLog.Tagged(log, "a", Leaf())),
                Add(Leaf(), R3TraceLog.Tagged(log, "b", Leaf())),
                Add(Expression.Constant("text")),
                Add(Expression.Constant(5))),
            Expression.Bind(R3TracedHolder.Member(nameof(R3TracedHolder.Other)), Leaf())));
    }

    /// <summary>The same member initialized twice keeps both assignments, in order: neither is dropped nor merged.</summary>
    [TestMethod]
    public void MemberInit_SameMemberTwice_KeepsBothAssignments()
    {
        Expression[] results = AssertSameBehavior(log => Expression.MemberInit(
            NewHolder(log),
            Expression.Bind(R3TracedHolder.Member(nameof(R3TracedHolder.Value)), Leaf()),
            Expression.Bind(R3TracedHolder.Member(nameof(R3TracedHolder.Value)), Expression.Add(Leaf(), Leaf()))));

        foreach (Expression result in results)
        {
            Assert.AreEqual(2, ((MemberInitExpression)result).Bindings.Count);
        }
    }

    /// <summary>
    /// A list initialization keeps every <c>Add</c> call with its exact overload (number, pair, text, code), in order, with
    /// equal arguments never merged.
    /// </summary>
    [TestMethod]
    public void ListInit_KeepsEveryAddCall_WithItsExactOverload()
    {
        Expression[] results = AssertSameBehavior(log => Expression.ListInit(
            NewList(log),
            Add(Leaf()),
            Add(Leaf()),
            Add(R3TraceLog.Tagged(log, "first", Leaf()), R3TraceLog.Tagged(log, "second", Leaf())),
            Add(Expression.Constant("text")),
            Add(IntLeaf())));

        foreach (Expression result in results)
        {
            var listInit = (ListInitExpression)result;
            Assert.AreEqual(5, listInit.Initializers.Count);
            Assert.AreEqual(R3TracedList.AddMethod(typeof(int)), listInit.Initializers[4].AddMethod);
        }
    }

    /// <summary>
    /// An <c>Add</c> method that throws interrupts the initialization at the same point after transformation: the same
    /// exception type, and the trace stops after the same call (later arguments and calls never run).
    /// </summary>
    [TestMethod]
    public void ListInit_ThrowingAdd_StopsAtTheSamePoint()
    {
        AssertSameBehavior(log => Expression.ListInit(
            NewList(log),
            Add(R3TraceLog.Tagged(log, "a", Leaf())),
            Add(Expression.Negate(IntLeaf())),
            Add(R3TraceLog.Tagged(log, "never", Leaf()))));
    }

    /// <summary>An exception thrown by a binding's value (here a failing checked conversion) interrupts the initialization at the same point.</summary>
    [TestMethod]
    public void MemberInit_ThrowingValue_StopsAtTheSamePoint()
    {
        AssertSameBehavior(log => Expression.MemberInit(
            NewHolder(log),
            Expression.Bind(R3TracedHolder.Member(nameof(R3TracedHolder.Value)), Leaf()),
            Expression.Bind(R3TracedHolder.Member(nameof(R3TracedHolder.Other)), Expression.Convert(Expression.ConvertChecked(Expression.Multiply(Leaf(), Expression.Constant(1e300)), typeof(int)), typeof(double))),
            Expression.Bind(R3TracedHolder.Member(nameof(R3TracedHolder.Field)), R3TraceLog.Tagged(log, "never", Leaf()))));
    }

    // ------------------------------------------------------------------------------------------
    // Index accesses as read and write targets.
    // ------------------------------------------------------------------------------------------

    /// <summary>Builds a traced two-argument indexer access whose receiver and indices are tagged, so their evaluation order and count are traced.</summary>
    /// <param name="log">The trace.</param>
    /// <param name="grid">The grid.</param>
    /// <returns>The index access.</returns>
    private static IndexExpression TracedCell(R3TraceLog log, R3TracedGrid grid)
        => Expression.MakeIndex(
            R3TraceLog.Tagged(log, "receiver", Expression.Constant(grid)),
            R3TracedGrid.CellIndexer,
            [R3TraceLog.Tagged(log, "row", IntLeaf()), R3TraceLog.Tagged(log, "column", Expression.Add(IntLeaf(), Expression.Constant(1)))]);

    /// <summary>Wraps <paramref name="operation"/> so its value is followed by the grid's state.</summary>
    /// <param name="operation">The operation.</param>
    /// <param name="state">The object whose state is described afterwards.</param>
    /// <returns>A string-typed body.</returns>
    private static Expression WithState(Expression operation, object state)
        => Expression.Call(
            typeof(ExpressionTreeR3Tests).GetMethod(nameof(Pair))!,
            operation.Type == typeof(void) ? Expression.Block(operation, Expression.Constant(null, typeof(object))) : Expression.Convert(operation, typeof(object)),
            Expression.Call(typeof(ExpressionTreeR3Tests).GetMethod(nameof(Describe), BindingFlags.NonPublic | BindingFlags.Static)!, Expression.Constant(state, typeof(object))));

    /// <summary>
    /// An indexer access stays a valid, writable target for every assignment form (plain, compound, pre- and
    /// post-increment), read, multi-argument and text indexers included: the transformation runs no accessor, and the
    /// receiver and the indices are evaluated once each, in the same order, before the same getter and setter calls.
    /// </summary>
    [TestMethod]
    public void IndexerTargets_StayWritable_AndKeepTheirEvaluationOrder()
    {
        Func<R3TraceLog, R3TracedGrid, Expression>[] operations =
        [
            (log, grid) => TracedCell(log, grid),
            (log, grid) => Expression.Assign(TracedCell(log, grid), R3TraceLog.Tagged(log, "value", Leaf())),
            (log, grid) => Expression.AddAssign(TracedCell(log, grid), R3TraceLog.Tagged(log, "value", Expression.Add(Leaf(), Leaf()))),
            (log, grid) => Expression.MultiplyAssign(TracedCell(log, grid), Leaf()),
            (log, grid) => Expression.PreIncrementAssign(TracedCell(log, grid)),
            (log, grid) => Expression.PostIncrementAssign(TracedCell(log, grid)),
            (log, grid) => Expression.PostDecrementAssign(TracedCell(log, grid)),
            (log, grid) => Expression.SubtractAssign(Expression.Property(Expression.Constant(grid), R3TracedGrid.NameIndexer, R3TraceLog.Tagged(log, "name", Expression.Constant("k"))), Leaf()),
        ];

        foreach (Func<R3TraceLog, R3TracedGrid, Expression> operation in operations)
        {
            Expression[] results = AssertSameBehavior(log =>
            {
                var grid = new R3TracedGrid(log);
                return WithState(operation(log, grid), grid);
            });

            foreach (Expression result in results)
            {
                Expression rebuilt = ((UnaryExpression)((MethodCallExpression)result).Arguments[0]).Operand;
                IndexExpression target = rebuilt switch
                {
                    IndexExpression index => index,
                    BinaryExpression binary => (IndexExpression)binary.Left,
                    UnaryExpression unary => (IndexExpression)unary.Operand,
                    _ => throw new AssertFailedException("Unexpected shape " + rebuilt.NodeType),
                };
                Assert.IsTrue(target.Indexer == R3TracedGrid.CellIndexer || target.Indexer == R3TracedGrid.NameIndexer);
            }
        }
    }

    /// <summary>
    /// A multi-dimensional array element stays a writable target for assignment, compound assignment and increments, and an
    /// out-of-range index keeps throwing the same exception, read or written, after transformation.
    /// </summary>
    [TestMethod]
    public void ArrayTargets_StayWritable_AndKeepTheirExceptions()
    {
        Func<R3TraceLog, double[,], Expression>[] operations =
        [
            (log, array) => Expression.ArrayAccess(Expression.Constant(array), IntLeaf(), R3TraceLog.Tagged(log, "j", IntLeaf())),
            (log, array) => Expression.Assign(Expression.ArrayAccess(Expression.Constant(array), R3TraceLog.Tagged(log, "i", IntLeaf()), R3TraceLog.Tagged(log, "j", IntLeaf())), R3TraceLog.Tagged(log, "v", Expression.Add(Leaf(), Leaf()))),
            (log, array) => Expression.AddAssign(Expression.ArrayAccess(Expression.Constant(array), IntLeaf(), Expression.Constant(0)), Leaf()),
            (log, array) => Expression.PreIncrementAssign(Expression.ArrayAccess(Expression.Constant(array), IntLeaf(), IntLeaf())),
            (log, array) => Expression.PostIncrementAssign(Expression.ArrayAccess(Expression.Constant(array), IntLeaf(), IntLeaf())),
            (log, array) => Expression.ArrayAccess(Expression.Constant(array), Expression.Add(IntLeaf(), Expression.Constant(5)), R3TraceLog.Tagged(log, "j", IntLeaf())),
            (log, array) => Expression.Assign(Expression.ArrayAccess(Expression.Constant(array), R3TraceLog.Tagged(log, "i", Expression.Add(IntLeaf(), Expression.Constant(5))), IntLeaf()), R3TraceLog.Tagged(log, "v", Leaf())),
        ];

        foreach (Func<R3TraceLog, double[,], Expression> operation in operations)
        {
            AssertSameBehavior(log =>
            {
                var array = new double[3, 3];
                return WithState(operation(log, array), array);
            });
        }
    }

    // ------------------------------------------------------------------------------------------
    // C-02: the CopyExpression arity contract of the R3 families.
    // ------------------------------------------------------------------------------------------

    /// <summary>Gets the flattened children of an R3 node, in the documented positional layout.</summary>
    /// <param name="node">The node.</param>
    /// <returns>The children.</returns>
    private static Expression[] ChildrenOf(Expression node)
    {
        var children = new List<Expression>();
        switch (node)
        {
            case MemberInitExpression memberInit:
                children.Add(memberInit.NewExpression);
                AddBindings(memberInit.Bindings);
                break;
            case ListInitExpression listInit:
                children.Add(listInit.NewExpression);
                AddInitializers(listInit.Initializers);
                break;
            case IndexExpression index:
                if (index.Object is not null) children.Add(index.Object);
                children.AddRange(index.Arguments);
                break;
        }

        return children.ToArray();

        void AddBindings(IEnumerable<MemberBinding> bindings)
        {
            foreach (MemberBinding binding in bindings)
            {
                switch (binding)
                {
                    case MemberAssignment assignment: children.Add(assignment.Expression); break;
                    case MemberMemberBinding memberBinding: AddBindings(memberBinding.Bindings); break;
                    case MemberListBinding listBinding: AddInitializers(listBinding.Initializers); break;
                }
            }
        }

        void AddInitializers(IEnumerable<ElementInit> initializers)
        {
            foreach (ElementInit initializer in initializers) children.AddRange(initializer.Arguments);
        }
    }

    /// <summary>
    /// For every R3 catalog sample, an empty child list and the node's own children both return the node itself, a list of
    /// the wrong length is rejected (one too few, one too many), and a list of new children rebuilds the node with the same
    /// metadata (binding kinds and members, <c>Add</c> methods, indexer).
    /// </summary>
    [TestMethod]
    public void CopyExpression_FollowsTheArityContract_ForEverySample()
    {
        foreach (string name in R3Samples)
        {
            Expression node = ExpressionNodeCatalog.Get(name).Build();
            Expression[] children = ChildrenOf(node);

            Assert.AreSame(node, ExposedTransformer.Copy(node), name);
            Assert.AreSame(node, ExposedTransformer.Copy(node, children), name);
            Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(node, children.Skip(1).ToArray()), name);
            Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(node, [.. children, children[^1]]), name);

            Expression[] replaced = children.Select(child => child is NewExpression creation ? creation : new RecursiveTransformer().Transform(child)).ToArray();
            Expression copy = ExposedTransformer.Copy(node, replaced);
            Assert.AreNotSame(node, copy, name);
            CollectionAssert.AreEqual(Metadata(node), Metadata(copy), name);
            CollectionAssert.AreEqual(replaced, ChildrenOf(copy), name);
        }
    }

    /// <summary>
    /// The creation position only accepts a <see cref="NewExpression"/> of the same type, an assigned value must fit its
    /// member, an <c>Add</c> argument must fit the original <c>Add</c> method (no other overload is selected), and an index
    /// access may not change type: each is rejected with <see cref="ArgumentException"/> instead of producing another node.
    /// </summary>
    [TestMethod]
    public void CopyExpression_RejectsIncompatibleChildren()
    {
        var log = new R3TraceLog();
        MemberInitExpression memberInit = Expression.MemberInit(NewHolder(log), Expression.Bind(R3TracedHolder.Member(nameof(R3TracedHolder.Value)), P));
        ListInitExpression listInit = Expression.ListInit(NewList(log), Add(Expression.Constant("text")));
        IndexExpression arrayAccess = Expression.ArrayAccess(Expression.Constant(new double[2]), Expression.Constant(0));

        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(memberInit, Expression.Constant(null, typeof(R3TracedHolder)), P));
        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(memberInit, Expression.New(typeof(object)), P));
        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(memberInit, memberInit.NewExpression, Expression.Constant("text")));
        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(listInit, listInit.NewExpression, P));
        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(listInit, Expression.New(typeof(List<string>)), listInit.Initializers[0].Arguments[0]));
        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(arrayAccess, Expression.Constant(new int[2]), Expression.Constant(0)));
        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(arrayAccess, arrayAccess.Object!, Expression.Constant("0")));
    }

    /// <summary>
    /// Degenerate shapes: a member initialization without binding has the creation as its only child; a list binding without
    /// initializer contributes no child; a static or argument-free access keeps its shape. Each is traversed and rebuilt.
    /// </summary>
    [TestMethod]
    public void EmptyBindingsAndInitializers_FollowTheContract()
    {
        Expression[] results = AssertSameBehavior(log => Expression.MemberInit(NewHolder(log, Leaf())));
        Assert.AreEqual(0, ((MemberInitExpression)results[0]).Bindings.Count);

        results = AssertSameBehavior(log => Expression.MemberInit(
            NewHolder(log, Leaf()),
            Expression.ListBind(R3TracedHolder.Member(nameof(R3TracedHolder.Items))),
            Expression.Bind(R3TracedHolder.Member(nameof(R3TracedHolder.Value)), Leaf())));
        Assert.AreEqual(0, ((MemberListBinding)((MemberInitExpression)results[0]).Bindings[0]).Initializers.Count);

        var log = new R3TraceLog();
        MemberInitExpression empty = Expression.MemberInit(NewHolder(log, P));
        Assert.AreSame(empty, ExposedTransformer.Copy(empty, empty.NewExpression));
        Expression copy = ExposedTransformer.Copy(empty, NewHolder(log, Expression.Constant(2.0)));
        Assert.AreEqual(0, ((MemberInitExpression)copy).Bindings.Count);
        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(empty, empty.NewExpression, P));
    }

    /// <summary>
    /// A list initialization without initializer, when the framework accepts it, has the creation as its only child and is
    /// rebuilt as one.
    /// </summary>
    [TestMethod]
    public void ListInit_WithoutInitializer_FollowsTheContract()
    {
        ListInitExpression empty;
        try
        {
            empty = Expression.ListInit(NewList(), System.Array.Empty<ElementInit>());
        }
        catch (ArgumentException)
        {
            // The framework refuses a list initialization without initializer: the shape cannot occur.
            return;
        }

        Assert.AreSame(empty, ExposedTransformer.Copy(empty, empty.NewExpression));
        Assert.AreSame(empty, new RecordingTransformer().Transform(empty));
        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(empty, empty.NewExpression, P));
    }

    /// <summary>
    /// A <c>PrepareExpression</c> override that replaces the creation of an initialization by another kind of node is
    /// rejected (the creation position only accepts a <see cref="NewExpression"/>, as a variable position only accepts a
    /// parameter), before any binding is prepared.
    /// </summary>
    [TestMethod]
    public void PrepareExpression_ReplacingTheCreation_IsRejected()
    {
        Expression tree = ExpressionNodeCatalog.Get("MemberInit").Build();
        Assert.ThrowsExactly<InvalidCastException>(() => new CreationReplacingTransformer().Transform(tree));
        Assert.ThrowsExactly<InvalidCastException>(() => new CreationReplacingTransformer().Transform(ExpressionNodeCatalog.Get("ListInit").Build()));
    }

    /// <summary>A transformer whose <c>PrepareExpression</c> turns every creation into a default value.</summary>
    private sealed class CreationReplacingTransformer : ExpressionTransformer
    {
        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <inheritdoc/>
        protected override Expression PrepareExpression(Expression e) => e is NewExpression ? Expression.Default(e.Type) : TransformCore(e);

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => CopyExpression(e, parameters);
    }

    /// <summary>A <see cref="MemberBinding"/> subclass that is none of the framework classes, built through the obsolete protected constructor.</summary>
    private sealed class CustomBinding : MemberBinding
    {
#pragma warning disable CS0618 // The only way to build a custom binding.
        /// <summary>Initializes the binding.</summary>
        /// <param name="member">The member.</param>
        public CustomBinding(MemberInfo member) : base(MemberBindingType.Assignment, member)
        {
        }
#pragma warning restore CS0618
    }

    /// <summary>
    /// A member initialization holding a binding of an unknown class cannot be traversed: it is rejected explicitly with
    /// <see cref="NotSupportedException"/> rather than rebuilt without that binding.
    /// </summary>
    [TestMethod]
    public void UnknownBindingClass_IsRejectedExplicitly()
    {
        MemberInitExpression tree;
        try
        {
            tree = Expression.MemberInit(Expression.New(typeof(StrongBox<double>)), new CustomBinding(typeof(StrongBox<double>).GetField(nameof(StrongBox<double>.Value))!));
        }
        catch (ArgumentException)
        {
            // The framework refuses the custom binding: the shape cannot occur.
            return;
        }

        Assert.ThrowsExactly<NotSupportedException>(() => new ExpressionSimplifier().Simplify(tree));
        Assert.ThrowsExactly<NotSupportedException>(() => ExposedTransformer.Copy(tree, tree.NewExpression));
    }
}
