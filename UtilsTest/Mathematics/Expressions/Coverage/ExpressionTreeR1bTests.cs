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
/// ETC R1b: traversal and faithful reconstruction of the scalar families <see cref="MemberExpression"/>,
/// <see cref="TypeBinaryExpression"/>, <see cref="NewExpression"/>, <see cref="NewArrayExpression"/> and
/// <see cref="DefaultExpression"/> (C-03), faithful unary and lambda reconstruction for every transformer (C-01), and the
/// scalar substitution of those families.
/// </summary>
[TestClass]
[TestCategory("ETC-R1b")]
public class ExpressionTreeR1bTests
{
    /// <summary>The free parameter of the samples.</summary>
    private static readonly ParameterExpression P = Expression.Parameter(typeof(double), "p");

    /// <summary>Builds the simplifiable leaf <c>p * 1.0</c>.</summary>
    /// <returns>A new leaf.</returns>
    private static Expression Leaf() => Expression.Multiply(P, Expression.Constant(1.0));

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

    /// <summary>A generic transformer whose <c>PrepareExpression</c> is the identity (the base default) and which records what it finalizes.</summary>
    private sealed class IdentityTransformer : ExpressionTransformer
    {
        /// <summary>Gets the finalized node types with their child-list lengths.</summary>
        public List<(ExpressionType NodeType, int Children)> Finalized { get; } = [];

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
        {
            Finalized.Add((e.NodeType, parameters.Length));
            return CopyExpression(e, parameters);
        }
    }

    /// <summary>A recursive transformer recording the nodes passed to <c>PrepareExpression</c>, in order.</summary>
    private sealed class PreparationRecorder : ExpressionTransformer
    {
        /// <summary>Gets the prepared nodes, in call order.</summary>
        public List<Expression> Prepared { get; } = [];

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <inheritdoc/>
        protected override Expression PrepareExpression(Expression e)
        {
            Prepared.Add(e);
            return TransformCore(e);
        }

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => CopyExpression(e, parameters);
    }

    /// <summary>A transformer with positional rules on the newly traversed families (C-03, hazard H-1).</summary>
    private sealed class PositionalRuleTransformer : ExpressionTransformer
    {
        /// <summary>Gets the children received by the rules, per family.</summary>
        public List<(ExpressionType NodeType, Expression[] Children)> Received { get; } = [];

        /// <summary>Records a two-argument creation.</summary>
        /// <param name="creation">The creation.</param>
        /// <param name="first">The first prepared argument.</param>
        /// <param name="second">The second prepared argument.</param>
        /// <returns>The creation, unchanged.</returns>
        [ExpressionSignature(ExpressionType.New)]
        private Expression OnNew(NewExpression creation, Expression first, Expression second)
        {
            Received.Add((ExpressionType.New, [first, second]));
            return creation;
        }

        /// <summary>Replaces a type test by its prepared operand's test against <see cref="string"/>.</summary>
        /// <param name="test">The type test.</param>
        /// <param name="operand">The prepared operand.</param>
        /// <returns>A type test of <paramref name="operand"/> against <see cref="string"/>.</returns>
        [ExpressionSignature(ExpressionType.TypeIs)]
        private Expression OnTypeIs(TypeBinaryExpression test, Expression operand)
        {
            Received.Add((ExpressionType.TypeIs, [operand]));
            return Expression.TypeIs(operand, typeof(string));
        }

        /// <summary>Records an array initializer's prepared elements.</summary>
        /// <param name="array">The array.</param>
        /// <param name="elements">The prepared elements.</param>
        /// <returns>The array, unchanged.</returns>
        [ExpressionSignature(ExpressionType.NewArrayInit)]
        private Expression OnArray(NewArrayExpression array, Expression[] elements)
        {
            Received.Add((ExpressionType.NewArrayInit, elements));
            return array;
        }

        /// <summary>Replaces <c>default(double)</c> by <c>4.5</c>.</summary>
        /// <param name="value">The default value.</param>
        /// <returns>The constant <c>4.5</c> for a <see cref="double"/> default, the node otherwise.</returns>
        [ExpressionSignature(ExpressionType.Default)]
        private Expression OnDefault(DefaultExpression value) => value.Type == typeof(double) ? Expression.Constant(4.5) : value;

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <inheritdoc/>
        protected override Expression PrepareExpression(Expression e) => TransformCore(e);

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => CopyExpression(e, parameters);
    }

    /// <summary>Exposes the protected substitution of <see cref="ExpressionTransformer"/>.</summary>
    private sealed class ExposedTransformer : ExpressionTransformer
    {
        /// <summary>Calls the protected <c>ReplaceArguments</c>.</summary>
        /// <param name="e">The tree.</param>
        /// <param name="parameter">The parameter to replace.</param>
        /// <param name="replacement">The replacement.</param>
        /// <returns>The substituted tree.</returns>
        public Expression Replace(Expression e, ParameterExpression parameter, Expression replacement) => ReplaceArguments(e, [parameter], [replacement]);

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);
    }

    /// <summary>A class whose getter and constructor count their invocations, to prove the transformation never runs them.</summary>
    public sealed class Observed
    {
        /// <summary>The number of constructions.</summary>
        public static int Constructions;

        /// <summary>The number of getter calls.</summary>
        public static int GetterCalls;

        /// <summary>A plain field.</summary>
        public double Field;

        /// <summary>Initializes an instance, counting the call.</summary>
        /// <param name="field">The initial field value.</param>
        /// <param name="other">An unused second argument.</param>
        public Observed(double field, double other)
        {
            Constructions++;
            Field = field + other;
        }

        /// <summary>Gets <see cref="Field"/>, counting the call.</summary>
        public double Property
        {
            get
            {
                GetterCalls++;
                return Field;
            }
        }
    }

    /// <summary>The <see cref="Observed"/> constructor.</summary>
    private static readonly ConstructorInfo ObservedConstructor = typeof(Observed).GetConstructor([typeof(double), typeof(double)])!;

    /// <summary>Compiles <paramref name="body"/> as a function of <see cref="P"/> and evaluates it.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="body">The body.</param>
    /// <param name="value">The value of <see cref="P"/>.</param>
    /// <returns>The value of the body.</returns>
    private static T Evaluate<T>(Expression body, double value = 3.0) => Expression.Lambda<Func<double, T>>(body, P).Compile()(value);

    /// <summary>Simplifies with the exact and a derived simplifier and rewrites with the recursive transformer.</summary>
    /// <param name="tree">The tree.</param>
    /// <returns>The three results.</returns>
    private static Expression[] AllResults(Expression tree)
        => [new ExpressionSimplifier().Simplify(tree), new DerivedSimplifier().Simplify(tree), new RecursiveTransformer().Transform(tree)];

    // ------------------------------------------------------------------------------------------
    // MemberExpression.
    // ------------------------------------------------------------------------------------------

    /// <summary>An instance member's receiver is traversed and simplified, the member is kept, and the getter is never invoked by the transformation.</summary>
    [TestMethod]
    public void Member_InstanceReceiver_IsTraversedWithoutInvokingTheGetter()
    {
        MemberExpression property = Expression.Property(Expression.New(ObservedConstructor, Leaf(), Expression.Constant(0.0)), nameof(Observed.Property));
        Observed.GetterCalls = 0;
        Observed.Constructions = 0;

        Expression[] results = AllResults(property);

        Assert.AreEqual(0, Observed.GetterCalls, "The transformation must not invoke the getter.");
        Assert.AreEqual(0, Observed.Constructions, "The transformation must not run the constructor.");
        foreach (Expression result in results)
        {
            Assert.AreSame(property.Member, ((MemberExpression)result).Member);
        }

        Assert.AreSame(P, ((NewExpression)((MemberExpression)results[0]).Expression!).Arguments[0]);
        Assert.AreEqual(3.0, Evaluate<double>(results[0]));
        Assert.AreEqual(6.0, Evaluate<double>(results[2]), "The recursive transformer reached the receiver (1.0 became 2.0).");
    }

    /// <summary>A static member has no child: it is a leaf, not an opaque node, and is returned as the same instance.</summary>
    [TestMethod]
    public void Member_Static_IsALeafReturnedUnchanged()
    {
        MemberExpression field = Expression.Field(null, typeof(double).GetField(nameof(double.Epsilon))!);
        var recorder = new IdentityTransformer();

        Assert.AreSame(field, new ExpressionSimplifier().Simplify(field));
        Assert.AreSame(field, recorder.Transform(field));
        CollectionAssert.AreEqual(new[] { (ExpressionType.MemberAccess, 0) }, recorder.Finalized);
    }

    /// <summary>A chain of nested member accesses is traversed at every level and keeps every member.</summary>
    [TestMethod]
    public void Member_NestedChain_IsTraversedAtEveryLevel()
    {
        Expression box = Expression.New(typeof(StrongBox<double>).GetConstructor([typeof(double)])!, Leaf());
        Expression outer = Expression.Field(Expression.New(typeof(StrongBox<StrongBox<double>>).GetConstructor([typeof(StrongBox<double>)])!, box), nameof(StrongBox<StrongBox<double>>.Value));
        MemberExpression nested = Expression.Field(outer, nameof(StrongBox<double>.Value));

        var simplified = (MemberExpression)new ExpressionSimplifier().Simplify(nested);

        Assert.AreSame(nested.Member, simplified.Member);
        Assert.AreSame(((MemberExpression)nested.Expression!).Member, ((MemberExpression)simplified.Expression!).Member);
        Assert.IsFalse(simplified.ToString().Contains("* 1"), simplified.ToString());
        Assert.AreEqual(3.0, Evaluate<double>(simplified));
    }

    // ------------------------------------------------------------------------------------------
    // TypeBinaryExpression.
    // ------------------------------------------------------------------------------------------

    /// <summary>Type tests keep <c>TypeIs</c>/<c>TypeEqual</c> and their type operand, nested or not, and evaluate identically.</summary>
    [TestMethod]
    public void TypeBinary_KeepsNodeTypeAndOperand_NestedOrNot()
    {
        Expression boxed = Expression.Convert(Leaf(), typeof(object));
        TypeBinaryExpression inner = Expression.TypeEqual(boxed, typeof(double));
        TypeBinaryExpression outer = Expression.TypeIs(Expression.Convert(inner, typeof(object)), typeof(bool));

        foreach (Expression result in AllResults(outer))
        {
            var test = (TypeBinaryExpression)result;
            Assert.AreEqual(ExpressionType.TypeIs, test.NodeType);
            Assert.AreEqual(typeof(bool), test.TypeOperand);
            var nestedTest = (TypeBinaryExpression)((UnaryExpression)test.Expression).Operand;
            Assert.AreEqual(ExpressionType.TypeEqual, nestedTest.NodeType);
            Assert.AreEqual(typeof(double), nestedTest.TypeOperand);
        }

        Assert.IsTrue(Evaluate<bool>(new ExpressionSimplifier().Simplify(outer)));
        Assert.IsTrue(Evaluate<bool>(new ExpressionSimplifier().Simplify(inner)));
    }

    // ------------------------------------------------------------------------------------------
    // NewExpression.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// A creation's arguments are prepared in order, its constructor and members are kept, the constructor is not run by
    /// the transformation, and a value-type creation without constructor is returned unchanged.
    /// </summary>
    [TestMethod]
    public void New_ArgumentsInOrder_ConstructorAndMembersKept()
    {
        NewExpression creation = Expression.New(ObservedConstructor, Leaf(), Expression.Add(P, Expression.Constant(0.0)));
        var recorder = new PreparationRecorder();
        Observed.Constructions = 0;

        var simplified = (NewExpression)new ExpressionSimplifier().Simplify(creation);
        recorder.Transform(creation);

        Assert.AreEqual(0, Observed.Constructions);
        Assert.AreSame(ObservedConstructor, simplified.Constructor);
        Assert.AreSame(P, simplified.Arguments[0]);
        Assert.AreSame(P, simplified.Arguments[1]);
        Assert.AreSame(creation.Arguments[0], recorder.Prepared[0], "The first argument is prepared first.");
        Assert.IsTrue(recorder.Prepared.IndexOf(creation.Arguments[1]) > recorder.Prepared.IndexOf(creation.Arguments[0]));
        Assert.AreEqual(6.0, Evaluate<Observed>(simplified).Field);

        var withMembers = (NewExpression)ExpressionNodeCatalog.Get("New.Members").Build();
        var simplifiedMembers = (NewExpression)new ExpressionSimplifier().Simplify(withMembers);
        CollectionAssert.AreEqual(withMembers.Members!.ToArray(), simplifiedMembers.Members!.ToArray());

        NewExpression valueDefault = Expression.New(typeof(DateTime));
        Assert.IsNull(valueDefault.Constructor);
        Assert.AreSame(valueDefault, new ExpressionSimplifier().Simplify(valueDefault));
        Assert.AreSame(valueDefault, new RecursiveTransformer().Transform(valueDefault));
    }

    // ------------------------------------------------------------------------------------------
    // NewArrayExpression and DefaultExpression.
    // ------------------------------------------------------------------------------------------

    /// <summary>Array initializers keep their element type and element order; array bounds keep their rank.</summary>
    [TestMethod]
    public void NewArray_InitAndBounds_KeepTypeOrderAndRank()
    {
        NewArrayExpression init = Expression.NewArrayInit(typeof(object), Expression.Convert(Leaf(), typeof(object)), Expression.Constant("x"), Expression.Default(typeof(object)));
        NewArrayExpression bounds = Expression.NewArrayBounds(typeof(int), Expression.Constant(2), Expression.Convert(Leaf(), typeof(int)), Expression.Constant(1));

        var simplifiedInit = (NewArrayExpression)new ExpressionSimplifier().Simplify(init);
        var simplifiedBounds = (NewArrayExpression)new ExpressionSimplifier().Simplify(bounds);

        Assert.AreEqual(typeof(object[]), simplifiedInit.Type);
        Assert.AreEqual(ExpressionType.NewArrayInit, simplifiedInit.NodeType);
        Assert.AreEqual("x", ((ConstantExpression)simplifiedInit.Expressions[1]).Value);
        Assert.AreEqual(ExpressionType.Default, simplifiedInit.Expressions[2].NodeType);
        Assert.AreEqual(typeof(int[,,]), simplifiedBounds.Type);
        Assert.AreEqual(ExpressionType.NewArrayBounds, simplifiedBounds.NodeType);
        var created = Evaluate<object[]>(simplifiedInit);
        Assert.AreEqual(3.0, created[0]);
        Assert.AreEqual(3, Evaluate<int[,,]>(simplifiedBounds).GetLength(1));
    }

    /// <summary>A default value is an explicit leaf: same instance and type, finalized with no child, and visible to a rule.</summary>
    [TestMethod]
    public void Default_IsAnExplicitLeaf()
    {
        DefaultExpression value = Expression.Default(typeof(double));
        var recorder = new IdentityTransformer();

        Assert.AreSame(value, new ExpressionSimplifier().Simplify(value));
        Assert.AreSame(value, recorder.Transform(value));
        CollectionAssert.AreEqual(new[] { (ExpressionType.Default, 0) }, recorder.Finalized);
        Assert.AreEqual(4.5, ((ConstantExpression)new PositionalRuleTransformer().Transform(value)).Value);
    }

    // ------------------------------------------------------------------------------------------
    // Unchanged children, idempotence and C-03 compatibility.
    // ------------------------------------------------------------------------------------------

    /// <summary>When no child changes, a transformer whose <c>PrepareExpression</c> is the identity returns every new family as the same instance.</summary>
    [TestMethod]
    public void UnchangedChildren_ReturnTheSameInstance()
    {
        Expression[] trees =
        [
            Expression.Field(Expression.New(typeof(StrongBox<double>).GetConstructor([typeof(double)])!, P), nameof(StrongBox<double>.Value)),
            Expression.TypeIs(Expression.Convert(P, typeof(object)), typeof(double)),
            Expression.New(ObservedConstructor, P, P),
            Expression.NewArrayInit(typeof(double), P, P),
            Expression.NewArrayBounds(typeof(double), Expression.Convert(P, typeof(int))),
        ];

        foreach (Expression tree in trees)
        {
            Assert.AreSame(tree, new IdentityTransformer().Transform(tree), tree.NodeType.ToString());
        }
    }

    /// <summary>Simplifying the result again changes nothing, for every newly traversed family.</summary>
    [TestMethod]
    public void Simplification_IsIdempotent()
    {
        Expression[] trees =
        [
            Expression.Property(Expression.New(ObservedConstructor, Leaf(), Expression.Constant(0.0)), nameof(Observed.Property)),
            Expression.TypeEqual(Expression.Convert(Leaf(), typeof(object)), typeof(double)),
            Expression.NewArrayInit(typeof(double), Leaf(), Expression.Multiply(Leaf(), Leaf())),
            Expression.NewArrayBounds(typeof(double), Expression.Convert(Leaf(), typeof(int))),
        ];

        var simplifier = new ExpressionSimplifier();
        foreach (Expression tree in trees)
        {
            Expression once = simplifier.Simplify(tree);
            Assert.AreEqual(once.ToString(), simplifier.Simplify(once).ToString(), tree.NodeType.ToString());
        }
    }

    /// <summary>
    /// C-03: a <c>PrepareExpression</c> override observes every child of the new families, in <see cref="ExpressionVisitor"/>
    /// order (receiver; tested operand; arguments; elements or bounds), deterministically.
    /// </summary>
    [TestMethod]
    public void PrepareExpression_ObservesChildrenInVisitorOrder()
    {
        Expression a = Expression.Constant(1.5);
        Expression b = Expression.Constant(2.5);
        Expression c = Expression.Constant(3.5);
        (Expression Tree, Expression[] Expected)[] cases =
        [
            (Expression.Field(Expression.New(typeof(StrongBox<double>).GetConstructor([typeof(double)])!, a), nameof(StrongBox<double>.Value)), [a]),
            (Expression.TypeIs(Expression.Convert(a, typeof(object)), typeof(double)), [a]),
            (Expression.New(ObservedConstructor, a, b), [a, b]),
            (Expression.NewArrayInit(typeof(double), a, b, c), [a, b, c]),
            (Expression.NewArrayBounds(typeof(double), Expression.Convert(a, typeof(int)), Expression.Convert(b, typeof(int))), [a, b]),
        ];

        foreach ((Expression tree, Expression[] expected) in cases)
        {
            var first = new PreparationRecorder();
            var second = new PreparationRecorder();
            first.Transform(tree);
            second.Transform(tree);

            CollectionAssert.AreEqual(expected, first.Prepared.Where(node => node is ConstantExpression).ToArray(), tree.NodeType.ToString());
            CollectionAssert.AreEqual(first.Prepared, second.Prepared, "The order must be deterministic.");
        }
    }

    /// <summary>C-03 (hazard H-1): custom rules on the new families receive the prepared children positionally or as an array.</summary>
    [TestMethod]
    public void CustomRules_ReceiveThePreparedChildren()
    {
        var transformer = new PositionalRuleTransformer();

        transformer.Transform(Expression.New(ObservedConstructor, Expression.Constant(1.0), P));
        Expression test = transformer.Transform(Expression.TypeIs(Expression.Constant("text"), typeof(int)));
        transformer.Transform(Expression.NewArrayInit(typeof(double), Expression.Constant(1.0), P));

        Assert.AreEqual(1.0, ((ConstantExpression)transformer.Received[0].Children[0]).Value, "The rule receives the prepared argument.");
        Assert.AreSame(P, transformer.Received[0].Children[1]);
        Assert.AreEqual(typeof(string), ((TypeBinaryExpression)test).TypeOperand);
        Assert.AreEqual(2, transformer.Received[2].Children.Length);
    }

    // ------------------------------------------------------------------------------------------
    // C-01: faithful unary and lambda reconstruction for every transformer.
    // ------------------------------------------------------------------------------------------

    /// <summary>A custom unary operator, a custom conversion, a lifted operator and typed/void throws keep their metadata through every transformer and substitution.</summary>
    [TestMethod]
    public void UnaryReconstruction_IsFaithfulForEveryTransformer()
    {
        MethodInfo negate = CatalogHelpers.FortyTwoMethod;
        ParameterExpression n = Expression.Parameter(typeof(double?), "n");
        UnaryExpression[] nodes =
        [
            Expression.Negate(Leaf(), negate),
            Expression.Convert(Leaf(), typeof(double), negate),
            Expression.ConvertChecked(Leaf(), typeof(double), negate),
            Expression.Negate(Expression.Convert(Leaf(), typeof(double?))),
            Expression.Throw(Expression.Call(CatalogHelpers.MakeExceptionMethod, Leaf()), typeof(double)),
            Expression.Throw(Expression.Call(CatalogHelpers.MakeExceptionMethod, Leaf())),
            Expression.Not(Expression.GreaterThan(Leaf(), Expression.Constant(0.0))),
        ];

        foreach (UnaryExpression node in nodes)
        {
            Expression[] results = [.. AllResults(node), new ExposedTransformer().Replace(node, P, Expression.Constant(5.0))];
            foreach (Expression result in results)
            {
                var unary = (UnaryExpression)result;
                Assert.AreEqual(node.NodeType, unary.NodeType, node.ToString());
                Assert.AreEqual(node.Type, unary.Type, node.ToString());
                Assert.AreSame(node.Method, unary.Method, node.ToString());
                Assert.AreEqual(node.IsLifted, unary.IsLifted, node.ToString());
            }
        }

        UnaryExpression rethrow = Expression.Rethrow(typeof(double));
        foreach (Expression result in AllResults(rethrow))
        {
            Assert.AreSame(rethrow, result);
        }
    }

    /// <summary>A quoted lambda keeps its node type and type through every transformer; R1b adds no traversal of the quoted content (C-05 stays open).</summary>
    [TestMethod]
    public void Quote_KeepsItsShape()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        UnaryExpression quote = Expression.Quote(Expression.Lambda<Func<double, double>>(Expression.Add(x, Expression.Constant(1.0)), x));

        foreach (Expression result in AllResults(quote))
        {
            Assert.AreEqual(ExpressionType.Quote, result.NodeType);
            Assert.AreEqual(quote.Type, result.Type);
        }
    }

    /// <summary>Lambda delegate type, name, tail call and parameter identity are kept at every depth by every transformer.</summary>
    [TestMethod]
    public void LambdaReconstruction_IsFaithfulAtEveryDepth()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        LambdaExpression inner = Expression.Lambda(typeof(CatalogDelegate), Expression.Multiply(y, Expression.Constant(1.0)), "inner", true, [y]);
        LambdaExpression outer = Expression.Lambda<Func<double, CatalogDelegate>>(inner, "outer", false, [x]);

        foreach (Expression result in AllResults(outer))
        {
            var rebuiltOuter = (LambdaExpression)result;
            var rebuiltInner = (LambdaExpression)rebuiltOuter.Body;
            Assert.AreEqual(outer.Type, rebuiltOuter.Type);
            Assert.AreEqual("outer", rebuiltOuter.Name);
            Assert.AreSame(x, rebuiltOuter.Parameters[0]);
            Assert.AreEqual(typeof(CatalogDelegate), rebuiltInner.Type);
            Assert.AreEqual("inner", rebuiltInner.Name);
            Assert.IsTrue(rebuiltInner.TailCall);
            Assert.AreSame(y, rebuiltInner.Parameters[0]);
        }
    }

    /// <summary>The lambda scope hooks stay paired when the body throws: the simplifier's ambient scope stack is empty afterwards.</summary>
    [TestMethod]
    public void LambdaScopeHooks_ArePairedWhenTheBodyThrows()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        var lambda = Expression.Lambda<Func<double, double>>(Expression.Add(x, Expression.Constant(new ThrowingEquals(), typeof(object)).ConvertToDouble()), x);

        Exception exception = Assert.Throws<Exception>(() => new ThrowingSimplifier().Simplify(lambda));
        while (exception is TargetInvocationException { InnerException: { } inner }) exception = inner;
        Assert.IsInstanceOfType<InvalidOperationException>(exception);
        Assert.AreEqual(0, ExpressionSimplifier.LexicalScopeDepth);
    }

    /// <summary>A simplifier whose constant rule throws, to interrupt a lambda body.</summary>
    private sealed class ThrowingSimplifier : ExpressionSimplifier
    {
        /// <summary>Throws on the marker constant.</summary>
        /// <param name="constant">The constant.</param>
        /// <returns>Never returns for the marker.</returns>
        [ExpressionSignature(ExpressionType.Constant)]
        private Expression Fail(ConstantExpression constant) => constant.Value is ThrowingEquals ? throw new InvalidOperationException("marker") : constant;
    }

    /// <summary>A marker constant value.</summary>
    private sealed class ThrowingEquals
    {
    }

    // ------------------------------------------------------------------------------------------
    // Scalar substitution.
    // ------------------------------------------------------------------------------------------

    /// <summary>Substitution enters member receivers, type tests, creation arguments and array elements, keeping every piece of metadata.</summary>
    [TestMethod]
    public void Substitution_EntersTheScalarFamilies()
    {
        Expression five = Expression.Constant(5.0);
        var transformer = new ExposedTransformer();

        var member = (MemberExpression)transformer.Replace(Expression.Field(Expression.New(typeof(StrongBox<double>).GetConstructor([typeof(double)])!, P), nameof(StrongBox<double>.Value)), P, five);
        var test = (TypeBinaryExpression)transformer.Replace(Expression.TypeEqual(Expression.Convert(P, typeof(object)), typeof(double)), P, five);
        var creation = (NewExpression)transformer.Replace(ExpressionNodeCatalog.Get("New.Members").Build(), ExpressionNodeCatalog.Free, five);
        var array = (NewArrayExpression)transformer.Replace(Expression.NewArrayBounds(typeof(double), Expression.Convert(P, typeof(int)), Expression.Constant(2)), P, five);

        Assert.AreEqual(5.0, Expression.Lambda<Func<double>>(member).Compile()());
        Assert.AreEqual(ExpressionType.TypeEqual, test.NodeType);
        Assert.IsTrue(Expression.Lambda<Func<bool>>(test).Compile()());
        Assert.IsNotNull(creation.Members);
        Assert.AreEqual(typeof(double[,]), array.Type);
        Assert.AreEqual(5, Expression.Lambda<Func<double[,]>>(array).Compile()().GetLength(0));

        BlockExpression block = Expression.Block(Expression.Field(Expression.New(typeof(StrongBox<double>).GetConstructor([typeof(double)])!, P), nameof(StrongBox<double>.Value)));
        Assert.AreEqual(5.0, Expression.Lambda<Func<double>>(transformer.Replace(block, P, five)).Compile()(), "Blocks are entered since ETC R2.");
    }

    // ------------------------------------------------------------------------------------------
    // Derivation and integration (compatibility).
    // ------------------------------------------------------------------------------------------

    /// <summary>Derivation and integration of ordinary expressions are unchanged, and a member access is still not derivable.</summary>
    [TestMethod]
    public void DerivationAndIntegration_AreUnchanged()
    {
        Expression<Func<double, double>> square = x => x * x + 3 * x;
        var derivative = (Expression<Func<double, double>>)new ExpressionDerivation<double>("x").Derivate(square);
        Assert.AreEqual(2 * 2.0 + 3, derivative.Compile()(2.0), 1e-12);
        Expression<Func<double, double>> one = x => 1.0;
        var integral = (Expression<Func<double, double>>)new ExpressionIntegration<double>("x").Integrate(one);
        Assert.AreEqual(2.0, integral.Compile()(2.0) - integral.Compile()(0.0), 1e-9);

        Expression<Func<double, double>> member = x => new StrongBox<double>(x).Value;
        Assert.ThrowsExactly<NotSupportedException>(() => new ExpressionDerivation<double>("x").Derivate(member));
        Assert.ThrowsExactly<NotSupportedException>(() => new ExpressionIntegration<double>("x").Integrate(member));
    }
}

/// <summary>Test-only helper for <see cref="ExpressionTreeR1bTests"/>.</summary>
internal static class ExpressionTreeR1bTestExtensions
{
    /// <summary>The <see cref="Marker"/> method.</summary>
    private static readonly MethodInfo MarkerMethod = typeof(ExpressionTreeR1bTestExtensions).GetMethod(nameof(Marker))!;

    /// <summary>Returns one for any value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>One.</returns>
    public static double Marker(object value) => 1.0;

    /// <summary>Wraps <paramref name="value"/> in a call returning a <see cref="double"/>.</summary>
    /// <param name="value">An expression of type <see cref="object"/>.</param>
    /// <returns><c>Marker(value)</c>.</returns>
    public static Expression ConvertToDouble(this Expression value) => Expression.Call(MarkerMethod, value);
}
