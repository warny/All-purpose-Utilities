using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>
/// ETC R0: minimal, named reproducers of the findings registered in
/// <c>Utils/TODO-2026-10-27-etc-expression-tree-coverage-roadmap.md</c>. Every test pins the behavior of
/// <c>master</c> at <c>b5c05dc3</c>, except the ETC-02 (Block, Loop, New), ETC-08, ETC-09, ETC-11 and ETC-13 tests,
/// which ETC R1a replaced by the corrected contract. A test asserting a defect states, in its summary, the corrected contract
/// and the round expected to replace the assertion; that replacement is part of the finding's resolution and is
/// recorded in the roadmap's compatibility register.
/// </summary>
[TestClass]
[TestCategory("ETC-R0")]
public class ExpressionTreeFindingsTests
{
    /// <summary>The free parameter of the reproducers.</summary>
    private static readonly ParameterExpression P = Expression.Parameter(typeof(double), "p");

    /// <summary>Builds the simplifiable leaf <c>p * 1.0</c>.</summary>
    /// <returns>A new leaf.</returns>
    private static Expression Leaf() => Expression.Multiply(P, Expression.Constant(1.0));

    /// <summary>Exposes the protected members of <see cref="ExpressionTransformer"/> without adding any rule.</summary>
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

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => CopyExpression(e, parameters);
    }

    /// <summary>A generic transformer that recurses through <c>PrepareExpression</c> and rewrites the constant <c>1.0</c> into <c>2.0</c>.</summary>
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

    /// <summary>The same rule as <see cref="RecursiveTransformer"/>, but without overriding <c>PrepareExpression</c>.</summary>
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

    /// <summary>A derived simplifier overriding nothing.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>Compiles <paramref name="body"/> as a function of <see cref="P"/> and evaluates it at 3.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="body">The body.</param>
    /// <returns>The value at <c>p = 3</c>.</returns>
    private static T Evaluate<T>(Expression body) => Expression.Lambda<Func<double, T>>(body, P).Compile()(3.0);

    // ------------------------------------------------------------------------------------------
    // ETC-01: incomplete traversal dispatch.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// ETC-01: families reaching <c>PrepareDefault</c> are returned as the very same instance by the exact simplifier,
    /// so a simplifiable descendant is never visited. R1 (New, NewArray, TypeBinary), R2 (Block, Try, Switch, Loop,
    /// Goto, Label), R3 (MemberInit, ListInit, Index) and R4 (Dynamic, Extension) replace this with explicit policies;
    /// R1b, R2 and R3 are done (see <c>ExpressionTreeR1bTests</c>, <c>ExpressionTreeR2Tests</c> and <c>ExpressionTreeR3Tests</c>).
    /// </summary>
    [TestMethod]
    public void ETC01_ExactSimplifier_DefaultDispatchedFamilies_AreNotTraversed()
    {
        string[] passThrough = ["Dynamic", "Extension"];
        foreach (string name in passThrough)
        {
            Expression original = ExpressionNodeCatalog.Get(name).Build();
            Assert.AreSame(original, new ExpressionSimplifier().Simplify(original), name);
        }
    }

    /// <summary>
    /// ETC-01 / compatibility: the generic base transformer only recurses when a subclass overrides
    /// <c>PrepareExpression</c>; a lambda body root is nevertheless always transformed, because <c>PrepareLambda</c>
    /// calls <c>TransformCore</c> on it directly. Every round must keep this asymmetry for existing subclasses.
    /// </summary>
    [TestMethod]
    public void ETC01_NonRecursiveTransformer_TransformsOnlyLambdaBodyRoots()
    {
        var transformer = new NonRecursiveTransformer();

        var negated = (UnaryExpression)transformer.Transform(Expression.Negate(Expression.Constant(1.0)));
        var lambda = (LambdaExpression)transformer.Transform(Expression.Lambda<Func<double>>(Expression.Constant(1.0)));

        Assert.AreEqual(1.0, ((ConstantExpression)negated.Operand).Value);
        Assert.AreEqual(2.0, ((ConstantExpression)lambda.Body).Value);
    }

    // ------------------------------------------------------------------------------------------
    // ETC-02: reconstruction disagrees with traversal and loses metadata.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// ETC-02, resolved for <c>Block</c> by ETC R1a (C-02): an explicit <c>CopyExpression</c> of a block keeps its declared
    /// variables and its result type (R0 pinned the loss of the variables, which made the copy uncompilable).
    /// </summary>
    [TestMethod]
    public void ETC02_CopyExpression_Block_KeepsVariables()
    {
        ParameterExpression local = Expression.Variable(typeof(double), "local");
        BlockExpression block = Expression.Block(typeof(object), [local], Expression.Assign(local, P), Expression.Convert(local, typeof(object)));

        var copy = (BlockExpression)ExposedTransformer.Copy(block, [.. block.Expressions]);

        CollectionAssert.AreEqual(block.Variables.ToArray(), copy.Variables.ToArray());
        Assert.AreEqual(typeof(object), copy.Type);
        Assert.AreEqual(3.0, Expression.Lambda<Func<double, object>>(copy, P).Compile()(3.0));
    }

    /// <summary>ETC-02, resolved for <c>Loop</c> by ETC R1a (C-02): an explicit copy keeps the break and continue labels and the loop type.</summary>
    [TestMethod]
    public void ETC02_CopyExpression_Loop_KeepsLabels()
    {
        LabelTarget breakLabel = Expression.Label(typeof(double), "break");
        LoopExpression loop = Expression.Loop(Expression.Break(breakLabel, P), breakLabel);

        var copy = (LoopExpression)ExposedTransformer.Copy(loop, loop.Body);

        Assert.AreSame(breakLabel, copy.BreakLabel);
        Assert.AreEqual(typeof(double), copy.Type);
        Assert.AreEqual(3.0, Evaluate<double>(copy));
    }

    /// <summary>ETC-02, resolved for <c>New</c> by ETC R1a (C-02): an explicit copy keeps <see cref="NewExpression.Members"/>.</summary>
    [TestMethod]
    public void ETC02_CopyExpression_NewWithMembers_KeepsMembers()
    {
        NewExpression creation = (NewExpression)ExpressionNodeCatalog.Get("New.Members").Build();

        var copy = (NewExpression)ExposedTransformer.Copy(creation, [.. creation.Arguments]);

        Assert.IsNotNull(creation.Members);
        CollectionAssert.AreEqual(creation.Members.ToArray(), copy.Members!.ToArray());
    }

    /// <summary>
    /// ETC-02: for many families <c>CopyExpression</c> ignores the supplied children and returns the original node. ETC R1b
    /// removed <c>MemberAccess</c> from this list: it follows the C-02 arity contract (see
    /// <see cref="ETC02_CopyExpression_MemberAccess_FollowsArityContract"/>); ETC R2 removed <c>Try</c>, <c>Switch</c>,
    /// <c>Goto</c> and <c>Label</c>, which follow it too (see <c>ExpressionTreeR2Tests</c>); ETC R3 removed <c>MemberInit</c>,
    /// <c>ListInit</c> and <c>Index</c> (see <c>ExpressionTreeR3Tests</c>).
    /// </summary>
    [TestMethod]
    public void ETC02_CopyExpression_IgnoresReplacementChildren_ForPassThroughFamilies()
    {
        Expression replacement = Expression.Constant(9.5);
        string[] ignored = ["Dynamic", "Extension", "Lambda"];
        foreach (string name in ignored)
        {
            Expression original = ExpressionNodeCatalog.Get(name).Build();
            Assert.AreSame(original, ExposedTransformer.Copy(original, replacement, replacement, replacement), name);
        }
    }

    /// <summary>
    /// ETC-02, resolved for <c>MemberAccess</c> by ETC R1b (C-02 extended): an empty list keeps the node, the receiver of an
    /// instance member is replaced with the member kept, a static member has no child, and a wrong arity is rejected.
    /// </summary>
    [TestMethod]
    public void ETC02_CopyExpression_MemberAccess_FollowsArityContract()
    {
        ParameterExpression box = Expression.Parameter(typeof(StrongBox<double>), "box");
        MemberExpression instance = Expression.Field(box, nameof(StrongBox<double>.Value));
        MemberExpression staticMember = Expression.Field(null, typeof(double).GetField(nameof(double.Epsilon))!);
        Expression replacement = Expression.New(typeof(StrongBox<double>).GetConstructor([typeof(double)])!, Expression.Constant(4.5));

        Assert.AreSame(instance, ExposedTransformer.Copy(instance));
        Assert.AreSame(staticMember, ExposedTransformer.Copy(staticMember));
        var copy = (MemberExpression)ExposedTransformer.Copy(instance, replacement);
        Assert.AreSame(instance.Member, copy.Member);
        Assert.AreSame(replacement, copy.Expression);
        Assert.AreEqual(4.5, Expression.Lambda<Func<double>>(copy).Compile()());
        Assert.AreSame(instance, ExposedTransformer.Copy(instance, box));
        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(instance, box, box));
        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(staticMember, box));
    }

    // ------------------------------------------------------------------------------------------
    // ETC-03: incomplete parameter substitution.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// ETC-03, resolved by ETC R2: substitution did not descend into a nested lambda, so a free occurrence of the replaced
    /// parameter captured by the lambda survived. It now substitutes free occurrences through binders that do not shadow it
    /// (the shadowing and capture cases are in <c>ExpressionTreeR2SubstitutionTests</c>).
    /// </summary>
    [TestMethod]
    public void ETC03_Substitution_DescendsIntoNestedLambda()
    {
        Expression<Func<double>> nested = Expression.Lambda<Func<double>>(P);

        Expression result = new ExposedTransformer().Replace(Expression.Invoke(nested), P, Expression.Constant(5.0));

        var invocation = (InvocationExpression)result;
        var lambda = (LambdaExpression)invocation.Expression;
        Assert.AreEqual(nested.Type, lambda.Type);
        Assert.AreEqual(5.0, ((ConstantExpression)lambda.Body).Value);
    }

    /// <summary>
    /// ETC-03 / ETC-04, resolved for unary nodes by ETC R1b (C-01): substitution used the historical factories, so a typed
    /// <c>Throw</c> became <see cref="void"/>; it now keeps its type and its substituted operand.
    /// </summary>
    [TestMethod]
    public void ETC03_Substitution_TypedThrow_KeepsItsType()
    {
        UnaryExpression typedThrow = Expression.Throw(Expression.Call(CatalogHelpers.MakeExceptionMethod, P), typeof(double));

        var result = (UnaryExpression)new ExposedTransformer().Replace(typedThrow, P, Expression.Constant(5.0));

        Assert.AreEqual(typeof(double), result.Type);
        Assert.AreEqual(ExpressionType.Throw, result.NodeType);
        Assert.IsInstanceOfType<ConstantExpression>(((MethodCallExpression)result.Operand).Arguments[0]);
    }

    /// <summary>
    /// ETC-03, resolved by ETC R2: substitution ignored blocks and every other non-scalar family; it now enters a block (its
    /// variables hide a redeclared parameter) and keeps its result type.
    /// </summary>
    [TestMethod]
    public void ETC03_Substitution_EntersBlock()
    {
        BlockExpression block = Expression.Block(Expression.Add(P, Expression.Constant(0.5)));

        var result = (BlockExpression)new ExposedTransformer().Replace(block, P, Expression.Constant(5.0));

        Assert.AreEqual(block.Type, result.Type);
        Assert.AreEqual(5.5, Expression.Lambda<Func<double>>(result).Compile()());
    }

    // ------------------------------------------------------------------------------------------
    // ETC-04 / ETC-05: historical unary and lambda reconstruction losses.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// ETC-04, resolved by ETC R1b (C-01): the generic transformer and a derived simplifier dropped an explicit unary operator
    /// method (changing the computed value from 42 to -3); every transformer now keeps it, like the exact simplifier did.
    /// </summary>
    [TestMethod]
    public void ETC04_CustomUnaryMethod_PreservedByEveryTransformer()
    {
        UnaryExpression negate = Expression.Negate(Leaf(), CatalogHelpers.FortyTwoMethod);

        Assert.AreSame(CatalogHelpers.FortyTwoMethod, ((UnaryExpression)new RecursiveTransformer().Transform(negate)).Method);
        Expression derived = new DerivedSimplifier().Simplify(negate);
        Expression exact = new ExpressionSimplifier().Simplify(negate);

        Assert.AreEqual(42.0, Evaluate<double>(negate));
        Assert.AreEqual(42.0, Evaluate<double>(derived));
        Assert.AreEqual(42.0, Evaluate<double>(exact));
    }

    /// <summary>ETC-04, resolved by ETC R1b (C-01): a typed <c>Throw</c> keeps its type through every transformer (it became <see cref="void"/> through the generic transformer and a derived simplifier).</summary>
    [TestMethod]
    public void ETC04_TypedThrow_TypePreservedByEveryTransformer()
    {
        UnaryExpression typedThrow = Expression.Throw(Expression.Call(CatalogHelpers.MakeExceptionMethod, Leaf()), typeof(double));

        Assert.AreEqual(typeof(double), new RecursiveTransformer().Transform(typedThrow).Type);
        Assert.AreEqual(typeof(double), new DerivedSimplifier().Simplify(typedThrow).Type);
        Assert.AreEqual(typeof(double), new ExpressionSimplifier().Simplify(typedThrow).Type);
    }

    /// <summary>
    /// ETC-05, resolved by ETC R1b (C-01): the lambda delegate type, name and tail-call flag, lost by the generic transformer
    /// and a derived simplifier, are now preserved by every transformer.
    /// </summary>
    [TestMethod]
    public void ETC05_LambdaMetadata_PreservedByEveryTransformer()
    {
        var lambda = (LambdaExpression)ExpressionNodeCatalog.Get("Lambda.Metadata").Build();

        LambdaExpression[] results =
        [
            (LambdaExpression)new RecursiveTransformer().Transform(lambda),
            (LambdaExpression)new DerivedSimplifier().Simplify(lambda),
            (LambdaExpression)new ExpressionSimplifier().Simplify(lambda),
        ];
        foreach (LambdaExpression result in results)
        {
            Assert.AreEqual(typeof(CatalogDelegate), result.Type);
            Assert.AreEqual("named", result.Name);
            Assert.IsTrue(result.TailCall);
        }
    }

    // ------------------------------------------------------------------------------------------
    // ETC-06 / ETC-07: limited structural comparison and coarse unsupported hashing.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// ETC-06, resolved for the scalar families by ETC R1c (C-04): two independently built, identical conditionals compared
    /// unequal; they are now structurally equal. A family that is still unsupported (an index access, R3; a block until ETC R2) stays conservatively
    /// unequal, and the same instance is equal to itself.
    /// </summary>
    [TestMethod]
    public void ETC06_Comparer_ScalarFamilies_AreStructurallyEqual()
    {
        Expression first = ExpressionNodeCatalog.Get("Conditional").Build();
        Expression second = ExpressionNodeCatalog.Get("Conditional").Build();
        Expression firstBlock = ExpressionNodeCatalog.Get("Index").Build();
        Expression secondBlock = ExpressionNodeCatalog.Get("Index").Build();

        Assert.IsTrue(ExpressionComparer.Default.Equals(first, second));
        Assert.IsFalse(ExpressionComparer.Default.Equals(firstBlock, secondBlock));
        Assert.IsTrue(ExpressionComparer.Default.Equals(firstBlock, firstBlock));
    }

    /// <summary>
    /// ETC-07, resolved for the scalar families by ETC R1c: unsupported nodes hashed by node type and result type only, so
    /// different conditionals collided. The hash now includes the children, so these two differ, while a still unsupported
    /// family (an index access, R3; a block until ETC R2) keeps the coarse hash.
    /// </summary>
    [TestMethod]
    public void ETC07_Comparer_ScalarFamilies_HashTheirChildren()
    {
        Expression first = Expression.Condition(Expression.GreaterThan(P, Expression.Constant(0.5)), Expression.Constant(1.5), Expression.Constant(2.5));
        Expression second = Expression.Condition(Expression.LessThan(P, Expression.Constant(7.5)), Expression.Constant(3.5), P);
        Expression firstBlock = Expression.ArrayAccess(Expression.NewArrayInit(typeof(double), Expression.Constant(1.5)), Expression.Constant(0));
        Expression secondBlock = Expression.ArrayAccess(Expression.NewArrayInit(typeof(double), P), Expression.Constant(0));

        Assert.IsFalse(ExpressionComparer.Default.Equals(first, second));
        Assert.AreNotEqual(ExpressionComparer.Default.GetHashCode(first), ExpressionComparer.Default.GetHashCode(second));
        Assert.AreEqual(ExpressionComparer.Default.GetHashCode(firstBlock), ExpressionComparer.Default.GetHashCode(secondBlock));
    }

    // ------------------------------------------------------------------------------------------
    // ETC-08: silent corruption through PrepareDefault + CopyExpression.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// ETC-08 (critical), resolved by ETC R1a and completed by ETC R1b: <c>Simplify(new[] { p * 1, p * 1 })</c> returned an
    /// empty array; R1a passed the initializer through unchanged, and R1b traverses its elements, so every transformer keeps
    /// both elements, in order, and the simplifiers turn each into <c>p</c>.
    /// </summary>
    [TestMethod]
    public void ETC08_ExactSimplifier_NewArrayInit_KeepsEveryElement()
    {
        Expression array = Expression.NewArrayInit(typeof(double), Leaf(), Leaf());

        Expression[] results = [new ExpressionSimplifier().Simplify(array), new DerivedSimplifier().Simplify(array), new RecursiveTransformer().Transform(array)];

        double[] expected = [3.0, 3.0];
        CollectionAssert.AreEqual(expected, Evaluate<double[]>(array));
        foreach (Expression result in results)
        {
            Assert.AreEqual(2, ((NewArrayExpression)result).Expressions.Count);
        }

        CollectionAssert.AreEqual(expected, Evaluate<double[]>(results[0]));
        CollectionAssert.AreEqual(expected, Evaluate<double[]>(results[1]));
        Assert.IsTrue(((NewArrayExpression)results[0]).Expressions.All(element => ReferenceEquals(element, P)));
        double[] rewritten = [6.0, 6.0];
        CollectionAssert.AreEqual(rewritten, Evaluate<double[]>(results[2]), "The generic transformer reached the elements (1.0 became 2.0).");
    }

    /// <summary>
    /// ETC-08 (critical), resolved by ETC R1a: a block was replaced by an empty <see cref="void"/> block; it now keeps its
    /// statements, variables and result. R1a kept the block as is; since ETC R2 it is traversed and rebuilt with the same
    /// variables, statement count and type, and the simplifiers' results evaluate like the original.
    /// </summary>
    [TestMethod]
    public void ETC08_ExactSimplifier_Block_KeepsStatementsVariablesAndResult()
    {
        var block = (BlockExpression)ExpressionNodeCatalog.Get("Block").Build();

        Expression[] results = [new ExpressionSimplifier().Simplify(block), new DerivedSimplifier().Simplify(block), new RecursiveTransformer().Transform(block)];

        Assert.AreEqual(typeof(double), block.Type);
        foreach (Expression result in results)
        {
            var rebuilt = (BlockExpression)result;
            Assert.AreEqual(block.Type, rebuilt.Type);
            CollectionAssert.AreEqual(block.Variables.ToArray(), rebuilt.Variables.ToArray());
            Assert.AreEqual(block.Expressions.Count, rebuilt.Expressions.Count);
        }

        Func<Expression, double> evaluate = body => Expression.Lambda<Func<double, double>>(body, ExpressionNodeCatalog.Free).Compile()(3.0);
        Assert.AreEqual(evaluate(block), evaluate(results[0]));
        Assert.AreEqual(evaluate(block), evaluate(results[1]));
    }

    // ------------------------------------------------------------------------------------------
    // ETC-09: valid trees throw in the simplifier and in the public comparer.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// ETC-09, resolved by ETC R1a: type tests, loops, constructions with arguments and array bounds threw from
    /// <c>Simplify</c>. A construction without argument (no child) is returned unchanged; the families traversed since ETC
    /// R1b, and the loop since ETC R2 (it was returned unchanged before), are rebuilt with the same node type, type and
    /// metadata, without throwing.
    /// </summary>
    [TestMethod]
    public void ETC09_ExactSimplifier_DoesNotThrowOnValidTrees()
    {
        Expression[] unchanged = [Expression.New(typeof(DateTime))];
        Expression[] traversed =
        [
            Expression.TypeIs(Expression.Convert(P, typeof(object)), typeof(double)),
            Expression.New(typeof(StrongBox<double>).GetConstructor([typeof(double)])!, P),
            Expression.NewArrayBounds(typeof(double), Expression.Constant(2)),
            ExpressionNodeCatalog.Get("Loop").Build(),
        ];

        foreach (Expression tree in unchanged)
        {
            Assert.AreSame(tree, new ExpressionSimplifier().Simplify(tree), tree.NodeType.ToString());
            Assert.AreSame(tree, new DerivedSimplifier().Simplify(tree), tree.NodeType.ToString());
        }

        foreach (Expression tree in traversed)
        {
            foreach (Expression result in new[] { new ExpressionSimplifier().Simplify(tree), new DerivedSimplifier().Simplify(tree) })
            {
                Assert.AreEqual(tree.NodeType, result.NodeType);
                Assert.AreEqual(tree.Type, result.Type);
                Assert.IsNotNull(Expression.Lambda(result, P, ExpressionNodeCatalog.Free).Compile(), tree.NodeType.ToString());
            }
        }
    }

    /// <summary>
    /// ETC-09, resolved by ETC R1a: the public comparer could not hash such a tree, so it could not enter a
    /// <see cref="HashSet{T}"/>. It now hashes it (coarsely, ETC-07) and the set finds it again; structural equality of two
    /// distinct instances stays conservative until R1c.
    /// </summary>
    [TestMethod]
    public void ETC09_Comparer_HashesValidTree()
    {
        Expression typeTest = Expression.TypeIs(Expression.Constant("text"), typeof(string));

        Assert.IsTrue(ExpressionComparer.Default.Equals(typeTest, typeTest));
        Assert.AreEqual(ExpressionComparer.Default.GetHashCode(typeTest), ExpressionComparer.Default.GetHashCode(typeTest));
        var set = new HashSet<Expression>(ExpressionComparer.Default) { typeTest };
        Assert.IsTrue(set.Contains(typeTest));
    }

    // ------------------------------------------------------------------------------------------
    // ETC-10 to ETC-15: additional findings of the R0 audit.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// ETC-10, resolved by ETC R1b: a member access receiver was never traversed, although the comparer treats member access
    /// as a supported family. The receiver is now simplified (and rewritten by a recursive generic transformer), the member
    /// is kept, and the getter is not invoked by the transformation.
    /// </summary>
    [TestMethod]
    public void ETC10_MemberAccessReceiver_IsTraversed()
    {
        var member = (MemberExpression)ExpressionNodeCatalog.Get("MemberAccess").Build();

        var simplified = (MemberExpression)new ExpressionSimplifier().Simplify(member);
        var rewritten = (MemberExpression)new RecursiveTransformer().Transform(member);

        Assert.AreSame(member.Member, simplified.Member);
        Assert.AreSame(member.Member, rewritten.Member);
        Assert.AreNotSame(member.Expression, simplified.Expression);
        double Run(Expression body) => Expression.Lambda<Func<double, double>>(body, ExpressionNodeCatalog.Free).Compile()(3.0);
        Assert.AreEqual(Run(member), Run(simplified));
        Assert.AreNotSame(member, rewritten);
    }

    /// <summary>
    /// ETC-11 (high), guarded by ETC R1a: inlining an invoked lambda left its parameter unbound when the body held the
    /// parameter in a nested lambda or a member access. An invocation whose parameter sits in a nested lambda is still
    /// kept (capture-avoiding inlining is R2); since ETC R1b, substitution enters member accesses, so the member-access body
    /// is inlined with its parameter replaced. Both results compile and compute the same value.
    /// </summary>
    [TestMethod]
    public void ETC11_InvokeInlining_KeepsInvocationWhenParameterIsNotSubstitutable()
    {
        ParameterExpression q = Expression.Parameter(typeof(double), "q");
        Expression<Func<double, double>> nestedLambda = Expression.Lambda<Func<double, double>>(
            Expression.Invoke(Expression.Invoke(Expression.Lambda<Func<double, Func<double>>>(Expression.Lambda<Func<double>>(q), q), P)), P);
        Expression<Func<double, double>> memberBody = Expression.Lambda<Func<double, double>>(
            Expression.Invoke(Expression.Lambda<Func<double, double>>(Expression.Property(Expression.Call(typeof(TimeSpan).GetMethod(nameof(TimeSpan.FromSeconds), [typeof(double)])!, q), nameof(TimeSpan.TotalSeconds)), q), P), P);

        Assert.AreEqual(3.0, nestedLambda.Compile()(3.0));
        Assert.AreEqual(3.0, memberBody.Compile()(3.0));
        var simplifier = new ExpressionSimplifier();
        var simplifiedNested = (Expression<Func<double, double>>)simplifier.Simplify(nestedLambda);
        var simplifiedMember = (Expression<Func<double, double>>)simplifier.Simplify(memberBody);
        Assert.AreEqual(3.0, simplifiedNested.Compile()(3.0));
        Assert.AreEqual(3.0, simplifiedMember.Compile()(3.0));
        var keptInvocation = (InvocationExpression)((InvocationExpression)simplifiedNested.Body).Expression;
        Assert.AreSame(q, ((LambdaExpression)keptInvocation.Expression).Parameters[0]);
        var inlined = (MemberExpression)simplifiedMember.Body;
        Assert.AreSame(P, ((MethodCallExpression)inlined.Expression!).Arguments[0]);
    }

    /// <summary>ETC-12 (policy): the simplifier rewrites the contents of a quoted lambda, which is data for its consumer.</summary>
    [TestMethod]
    public void ETC12_ExactSimplifier_RewritesQuotedLambda()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        UnaryExpression quote = Expression.Quote(Expression.Lambda<Func<double, double>>(Expression.Multiply(x, Expression.Constant(1.0)), x));

        var simplified = (UnaryExpression)new ExpressionSimplifier().Simplify(quote);

        Assert.AreSame(x, ((LambdaExpression)simplified.Operand).Body);
    }

    /// <summary>
    /// ETC-13, resolved by ETC R1a: a rethrow (a <c>Throw</c> without operand) crashed the generic recursive transformer
    /// and a derived simplifier, and lost its type through substitution. It is now a leaf kept unchanged by every path.
    /// </summary>
    [TestMethod]
    public void ETC13_Rethrow_NullOperand()
    {
        UnaryExpression rethrow = Expression.Rethrow(typeof(double));

        Assert.AreSame(rethrow, new RecursiveTransformer().Transform(rethrow));
        Assert.AreSame(rethrow, new DerivedSimplifier().Simplify(rethrow));
        Assert.AreSame(rethrow, new ExpressionSimplifier().Simplify(rethrow));
        Assert.AreSame(rethrow, new ExposedTransformer().Replace(rethrow, P, Expression.Constant(5.0)));
    }

    /// <summary>
    /// ETC-14, resolved for the scalar families by ETC R1c: the unsupported-node canonical-order key tied every unsupported
    /// term; conditionals and defaults now have their own keys, which order them antisymmetrically, while two still
    /// unsupported families (an index access and a member initializer, R3; a block and a loop until ETC R2) keep tying, conservatively.
    /// </summary>
    [TestMethod]
    public void ETC14_CanonicalOrder_KeysTheScalarFamilies()
    {
        Expression first = ExpressionNodeCatalog.Get("Conditional").Build();
        Expression second = Expression.Default(typeof(double));
        Expression block = ExpressionNodeCatalog.Get("Index").Build();
        Expression loop = ExpressionNodeCatalog.Get("MemberInit").Build();

        int forward = ExpressionCanonicalOrder.Compare(first, second, []);
        Assert.AreNotEqual(0, forward);
        Assert.AreEqual(-Math.Sign(forward), Math.Sign(ExpressionCanonicalOrder.Compare(second, first, [])));
        Assert.AreEqual(0, ExpressionCanonicalOrder.Compare(block, loop, []));
        Assert.AreEqual(0, ExpressionCanonicalOrder.Compare(loop, block, []));
    }

    /// <summary>
    /// ETC-15 (high), resolved by ETC R1b for the assignment and increment reproducers (C-06): the simplifier merged
    /// (<c>++v + ++v</c> gave 2 instead of 3), dropped (<c>(v = 5) * 0 + v</c> gave 0 instead of 5) and reordered
    /// (<c>++v + v</c> gave 1 instead of 2) side-effecting operands. Such operands are no longer rewritten, by the exact or a
    /// derived simplifier: every result computes the source value. The full matrix lives in <c>ExpressionTreeR1bEffectTests</c>.
    /// </summary>
    [TestMethod]
    public void ETC15_Simplifier_PreservesSideEffectingAssignments()
    {
        ParameterExpression v = Expression.Variable(typeof(double), "v");
        double Run(Expression body)
            => Expression.Lambda<Func<double>>(Expression.Block([v], Expression.Assign(v, Expression.Constant(0.0)), body)).Compile()();

        Expression twice = Expression.Add(Expression.PreIncrementAssign(v), Expression.PreIncrementAssign(v));
        Expression dropped = Expression.Add(Expression.Multiply(Expression.Assign(v, Expression.Constant(5.0)), Expression.Constant(0.0)), v);
        Expression reordered = Expression.Add(Expression.PreIncrementAssign(v), v);

        Assert.AreEqual(3.0, Run(twice));
        Assert.AreEqual(5.0, Run(dropped));
        Assert.AreEqual(2.0, Run(reordered));
        foreach (ExpressionSimplifier simplifier in new[] { new ExpressionSimplifier(), new DerivedSimplifier() })
        {
            Assert.AreEqual(3.0, Run(simplifier.Simplify(twice)));
            Assert.AreEqual(5.0, Run(simplifier.Simplify(dropped)));
            Assert.AreEqual(2.0, Run(simplifier.Simplify(reordered)));
        }
    }
}
