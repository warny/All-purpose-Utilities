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
    /// Goto, Label), R3 (MemberInit, ListInit, Index) and R4 (Dynamic, Extension) replace this with explicit policies.
    /// </summary>
    [TestMethod]
    public void ETC01_ExactSimplifier_DefaultDispatchedFamilies_AreNotTraversed()
    {
        string[] passThrough = ["MemberInit", "ListInit", "Index", "Try", "Switch", "Goto", "Label", "Dynamic", "Extension"];
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

    /// <summary>ETC-02: for many families <c>CopyExpression</c> ignores the supplied children and returns the original node.</summary>
    [TestMethod]
    public void ETC02_CopyExpression_IgnoresReplacementChildren_ForPassThroughFamilies()
    {
        Expression replacement = Expression.Constant(9.5);
        string[] ignored = ["MemberAccess", "MemberInit", "ListInit", "Index", "Try", "Switch", "Goto", "Label", "Dynamic", "Extension", "Lambda"];
        foreach (string name in ignored)
        {
            Expression original = ExpressionNodeCatalog.Get(name).Build();
            Assert.AreSame(original, ExposedTransformer.Copy(original, replacement, replacement, replacement), name);
        }
    }

    // ------------------------------------------------------------------------------------------
    // ETC-03: incomplete parameter substitution.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// ETC-03: substitution does not descend into a nested lambda, so a free occurrence of the replaced parameter
    /// captured by the lambda survives. R2 substitutes free occurrences through binders that do not shadow it.
    /// </summary>
    [TestMethod]
    public void ETC03_Substitution_DoesNotDescendIntoNestedLambda()
    {
        Expression<Func<double>> nested = Expression.Lambda<Func<double>>(P);

        Expression result = new ExposedTransformer().Replace(Expression.Invoke(nested), P, Expression.Constant(5.0));

        var invocation = (InvocationExpression)result;
        Assert.AreSame(nested, invocation.Expression);
    }

    /// <summary>ETC-03 / ETC-04: substitution rebuilds unary nodes through the historical factories, so a typed <c>Throw</c> becomes <see cref="void"/>.</summary>
    [TestMethod]
    public void ETC03_Substitution_TypedThrow_BecomesVoid()
    {
        UnaryExpression typedThrow = Expression.Throw(Expression.Call(CatalogHelpers.MakeExceptionMethod, P), typeof(double));

        Expression result = new ExposedTransformer().Replace(typedThrow, P, Expression.Constant(5.0));

        Assert.AreEqual(typeof(void), result.Type);
    }

    /// <summary>ETC-03: substitution ignores block variables and every other non-scalar family; R2 defines the binding model first.</summary>
    [TestMethod]
    public void ETC03_Substitution_LeavesBlockUntouched()
    {
        BlockExpression block = Expression.Block(Expression.Add(P, Expression.Constant(0.5)));

        Assert.AreSame(block, new ExposedTransformer().Replace(block, P, Expression.Constant(5.0)));
    }

    // ------------------------------------------------------------------------------------------
    // ETC-04 / ETC-05: historical unary and lambda reconstruction losses.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// ETC-04: the generic transformer and a derived simplifier drop an explicit unary operator method (changing the
    /// computed value from 42 to -3), while the exact built-in simplifier preserves it.
    /// </summary>
    [TestMethod]
    public void ETC04_CustomUnaryMethod_DroppedByGenericAndDerived_PreservedByExact()
    {
        UnaryExpression negate = Expression.Negate(Leaf(), CatalogHelpers.FortyTwoMethod);

        Assert.IsNull(((UnaryExpression)new RecursiveTransformer().Transform(negate)).Method);
        Expression derived = new DerivedSimplifier().Simplify(negate);
        Expression exact = new ExpressionSimplifier().Simplify(negate);

        Assert.AreEqual(42.0, Evaluate<double>(negate));
        Assert.AreEqual(-3.0, Evaluate<double>(derived));
        Assert.AreEqual(42.0, Evaluate<double>(exact));
    }

    /// <summary>ETC-04: a typed <c>Throw</c> becomes <see cref="void"/> through the generic transformer and a derived simplifier.</summary>
    [TestMethod]
    public void ETC04_TypedThrow_TypeDroppedByGenericAndDerived_PreservedByExact()
    {
        UnaryExpression typedThrow = Expression.Throw(Expression.Call(CatalogHelpers.MakeExceptionMethod, Leaf()), typeof(double));

        Assert.AreEqual(typeof(void), new RecursiveTransformer().Transform(typedThrow).Type);
        Assert.AreEqual(typeof(void), new DerivedSimplifier().Simplify(typedThrow).Type);
        Assert.AreEqual(typeof(double), new ExpressionSimplifier().Simplify(typedThrow).Type);
    }

    /// <summary>ETC-05: lambda delegate type, name and tail-call flag are lost by the generic transformer and a derived simplifier, preserved by the exact simplifier.</summary>
    [TestMethod]
    public void ETC05_LambdaMetadata_DroppedByGenericAndDerived_PreservedByExact()
    {
        var lambda = (LambdaExpression)ExpressionNodeCatalog.Get("Lambda.Metadata").Build();

        LambdaExpression[] lossyResults = [(LambdaExpression)new RecursiveTransformer().Transform(lambda), (LambdaExpression)new DerivedSimplifier().Simplify(lambda)];
        foreach (LambdaExpression lossy in lossyResults)
        {
            Assert.AreEqual(typeof(Func<double, double>), lossy.Type);
            Assert.IsNull(lossy.Name);
            Assert.IsFalse(lossy.TailCall);
        }

        var exact = (LambdaExpression)new ExpressionSimplifier().Simplify(lambda);
        Assert.AreEqual(typeof(CatalogDelegate), exact.Type);
        Assert.AreEqual("named", exact.Name);
        Assert.IsTrue(exact.TailCall);
    }

    // ------------------------------------------------------------------------------------------
    // ETC-06 / ETC-07: limited structural comparison and coarse unsupported hashing.
    // ------------------------------------------------------------------------------------------

    /// <summary>ETC-06: two independently built, identical conditionals are conservatively unequal; the same instance is equal to itself.</summary>
    [TestMethod]
    public void ETC06_Comparer_UnsupportedFamily_IsConservativelyUnequal()
    {
        Expression first = ExpressionNodeCatalog.Get("Conditional").Build();
        Expression second = ExpressionNodeCatalog.Get("Conditional").Build();

        Assert.IsFalse(ExpressionComparer.Default.Equals(first, second));
        Assert.IsTrue(ExpressionComparer.Default.Equals(first, first));
    }

    /// <summary>ETC-07: unsupported nodes hash by node type and result type only, so different conditionals collide.</summary>
    [TestMethod]
    public void ETC07_Comparer_UnsupportedFamily_HashIsCoarse()
    {
        Expression first = Expression.Condition(Expression.GreaterThan(P, Expression.Constant(0.5)), Expression.Constant(1.5), Expression.Constant(2.5));
        Expression second = Expression.Condition(Expression.LessThan(P, Expression.Constant(7.5)), Expression.Constant(3.5), P);

        Assert.IsFalse(ExpressionComparer.Default.Equals(first, second));
        Assert.AreEqual(ExpressionComparer.Default.GetHashCode(first), ExpressionComparer.Default.GetHashCode(second));
    }

    // ------------------------------------------------------------------------------------------
    // ETC-08: silent corruption through PrepareDefault + CopyExpression.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// ETC-08 (critical), resolved by ETC R1a: <c>Simplify(new[] { p * 1, p * 1 })</c> returned an empty array; the array
    /// initializer is now passed through unchanged (its elements are traversed by R1b), by the exact simplifier, a
    /// derived simplifier and a generic transformer finalizing with <c>CopyExpression</c>.
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
            Assert.AreSame(array, result);
        }
    }

    /// <summary>
    /// ETC-08 (critical), resolved by ETC R1a: a block was replaced by an empty <see cref="void"/> block; it now keeps its
    /// statements, variables and result.
    /// </summary>
    [TestMethod]
    public void ETC08_ExactSimplifier_Block_KeepsStatementsVariablesAndResult()
    {
        Expression block = ExpressionNodeCatalog.Get("Block").Build();

        Expression[] results = [new ExpressionSimplifier().Simplify(block), new DerivedSimplifier().Simplify(block), new RecursiveTransformer().Transform(block)];

        Assert.AreEqual(typeof(double), block.Type);
        foreach (Expression result in results)
        {
            Assert.AreSame(block, result);
        }
    }

    // ------------------------------------------------------------------------------------------
    // ETC-09: valid trees throw in the simplifier and in the public comparer.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// ETC-09, resolved by ETC R1a: type tests, loops, constructions with arguments and array bounds threw from
    /// <c>Simplify</c>; they are now returned unchanged by the exact and a derived simplifier.
    /// </summary>
    [TestMethod]
    public void ETC09_ExactSimplifier_DoesNotThrowOnValidTrees()
    {
        Expression[] trees =
        [
            Expression.TypeIs(Expression.Convert(P, typeof(object)), typeof(double)),
            ExpressionNodeCatalog.Get("Loop").Build(),
            Expression.New(typeof(StrongBox<double>).GetConstructor([typeof(double)])!, P),
            Expression.New(typeof(DateTime)),
            Expression.NewArrayBounds(typeof(double), Expression.Constant(2)),
        ];

        foreach (Expression tree in trees)
        {
            Assert.AreSame(tree, new ExpressionSimplifier().Simplify(tree), tree.NodeType.ToString());
            Assert.AreSame(tree, new DerivedSimplifier().Simplify(tree), tree.NodeType.ToString());
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

    /// <summary>ETC-10: a member access receiver is never traversed, although the comparer treats member access as a supported family.</summary>
    [TestMethod]
    public void ETC10_MemberAccessReceiver_IsNotTraversed()
    {
        Expression member = ExpressionNodeCatalog.Get("MemberAccess").Build();

        Assert.AreSame(member, new ExpressionSimplifier().Simplify(member));
        Assert.AreSame(member, new RecursiveTransformer().Transform(member));
    }

    /// <summary>
    /// ETC-11 (high), guarded by ETC R1a: inlining an invoked lambda left its parameter unbound when the body held the
    /// parameter in a nested lambda or a member access. Such an invocation is now kept, so the result compiles and
    /// computes the same value; capture-avoiding inlining is R2.
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
        Assert.IsInstanceOfType<InvocationExpression>(simplifiedMember.Body);
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
    /// ETC-14: the unsupported-node canonical-order key ties every unsupported term, so equality support added by a
    /// later round must come with canonical keys, or equal terms reached in different orders will not converge.
    /// </summary>
    [TestMethod]
    public void ETC14_CanonicalOrder_TiesEveryUnsupportedNode()
    {
        Expression first = ExpressionNodeCatalog.Get("Conditional").Build();
        Expression second = Expression.Default(typeof(double));

        Assert.AreEqual(0, ExpressionCanonicalOrder.Compare(first, second, []));
        Assert.AreEqual(0, ExpressionCanonicalOrder.Compare(second, first, []));
    }

    /// <summary>
    /// ETC-15 (high): assignment and increment nodes are inside the "scalar" unary/binary families, and the simplifier
    /// treats them as pure symbolic operands: it merges, cancels and drops them and reorders their evaluation.
    /// </summary>
    [TestMethod]
    public void ETC15_ExactSimplifier_RewritesSideEffectingAssignments()
    {
        ParameterExpression v = Expression.Variable(typeof(double), "v");
        double Run(Expression body)
            => Expression.Lambda<Func<double>>(Expression.Block([v], Expression.Assign(v, Expression.Constant(0.0)), body)).Compile()();
        var simplifier = new ExpressionSimplifier();

        Expression twice = Expression.Add(Expression.PreIncrementAssign(v), Expression.PreIncrementAssign(v));
        Expression dropped = Expression.Add(Expression.Multiply(Expression.Assign(v, Expression.Constant(5.0)), Expression.Constant(0.0)), v);
        Expression reordered = Expression.Add(Expression.PreIncrementAssign(v), v);

        Assert.AreEqual(3.0, Run(twice));
        Assert.AreEqual(2.0, Run(simplifier.Simplify(twice)));
        Assert.AreEqual(5.0, Run(dropped));
        Assert.AreEqual(0.0, Run(simplifier.Simplify(dropped)));
        Assert.AreEqual(2.0, Run(reordered));
        Assert.AreEqual(1.0, Run(simplifier.Simplify(reordered)));
    }
}
