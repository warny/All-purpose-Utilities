using System;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>A delegate with a by-reference parameter, used to check that <see cref="ParameterExpression.IsByRef"/> is part of the C-02 lambda contract.</summary>
/// <param name="value">The by-reference value.</param>
/// <returns>The computed value.</returns>
internal delegate double R4ByRefDelegate(ref double value);

/// <summary>
/// ETC R4, checkpoint C (C-02 completed): <c>CopyExpression</c> of a lambda. Its positional children are its parameter
/// declarations, in order — exactly what <c>PrepareLambda</c> passes to the rules and to <c>FinalizeExpression</c>; the body is
/// transformed separately and is never a child. An empty list or the same declarations keep the node; other parameters of the
/// same types and by-reference flags alpha-rename the lambda (capture-avoiding, delegate type, name and tail call kept); any
/// other list is refused with <see cref="ArgumentException"/>.
/// </summary>
[TestClass]
[TestCategory("ETC-R4")]
public class ExpressionTreeR4LambdaCopyTests
{
    /// <summary>A free parameter.</summary>
    private static readonly ParameterExpression P = Expression.Parameter(typeof(double), "p");

    /// <summary>The <see cref="QuoteHost.Apply"/> method.</summary>
    private static readonly System.Reflection.MethodInfo ApplyMethod = typeof(QuoteHost).GetMethod(nameof(QuoteHost.Apply))!;

    /// <summary>Exposes the protected static <c>CopyExpression</c>.</summary>
    private sealed class ExposedTransformer : ExpressionTransformer
    {
        /// <summary>Calls the protected static <c>CopyExpression</c>.</summary>
        /// <param name="e">The node to copy.</param>
        /// <param name="parameters">The replacement children.</param>
        /// <returns>The copy.</returns>
        public static Expression Copy(Expression e, params Expression[] parameters) => CopyExpression(e, parameters);

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);
    }

    /// <summary>A derived transformer overriding <c>PrepareExpression</c> and finalizing every node through <c>CopyExpression</c>, recording lambda child lists.</summary>
    private sealed class CopyingTransformer : ExpressionTransformer
    {
        /// <summary>Gets the child lists received for lambdas.</summary>
        public System.Collections.Generic.List<Expression[]> LambdaChildren { get; } = [];

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <inheritdoc/>
        protected override Expression PrepareExpression(Expression e) => TransformCore(e);

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
        {
            if (e is LambdaExpression) LambdaChildren.Add(parameters);
            return CopyExpression(e, parameters);
        }
    }

    /// <summary>A derived simplifier whose <c>FinalizeExpression</c> defers to the base (which copies lambdas through <c>CopyExpression</c>).</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>Builds <c>(x, y) =&gt; x - y</c> with a custom name and tail call, over a named delegate type.</summary>
    /// <returns>The lambda and its declarations.</returns>
    private static (Expression<Func<double, double, double>> Lambda, ParameterExpression X, ParameterExpression Y) Difference()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        return (Expression.Lambda<Func<double, double, double>>(Expression.Subtract(x, y), "difference", true, [x, y]), x, y);
    }

    /// <summary>The child layout is the parameter declarations, as received by <c>FinalizeExpression</c>; the body never is a child.</summary>
    [TestMethod]
    public void ChildLayout_IsTheDeclarations()
    {
        (Expression<Func<double, double, double>> lambda, ParameterExpression x, ParameterExpression y) = Difference();
        var transformer = new CopyingTransformer();

        Expression result = transformer.Transform(lambda);

        Assert.AreEqual(1, transformer.LambdaChildren.Count);
        CollectionAssert.AreEqual(new Expression[] { x, y }, transformer.LambdaChildren[0]);
        var copied = (LambdaExpression)result;
        Assert.AreEqual(lambda.Type, copied.Type);
        Assert.AreEqual("difference", copied.Name);
        Assert.IsTrue(copied.TailCall);
        var simplified = (LambdaExpression)new DerivedSimplifier().Simplify(lambda);
        CollectionAssert.AreEqual(new[] { x, y }, simplified.Parameters.ToArray(), "A derived simplifier copies its lambdas through CopyExpression: the declarations are kept.");
    }

    /// <summary>An empty list and the very same declarations keep the node; so does a parameterless lambda with an empty list.</summary>
    [TestMethod]
    public void SameDeclarations_KeepTheNode()
    {
        (Expression<Func<double, double, double>> lambda, ParameterExpression x, ParameterExpression y) = Difference();
        Expression<Func<double>> parameterless = Expression.Lambda<Func<double>>(P);

        Assert.AreSame(lambda, ExposedTransformer.Copy(lambda));
        Assert.AreSame(lambda, ExposedTransformer.Copy(lambda, x, y));
        Assert.AreSame(parameterless, ExposedTransformer.Copy(parameterless));
    }

    /// <summary>
    /// Fresh parameters of the same types alpha-rename the lambda: every bound occurrence follows its declaration, the delegate
    /// type, name and tail-call flag are kept, and the copy computes the same function; a swap of the two declarations is a
    /// simultaneous renaming.
    /// </summary>
    [TestMethod]
    public void FreshParameters_AlphaRenameTheLambda()
    {
        (Expression<Func<double, double, double>> lambda, ParameterExpression x, ParameterExpression y) = Difference();
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        ParameterExpression b = Expression.Parameter(typeof(double), "b");

        var renamed = (Expression<Func<double, double, double>>)ExposedTransformer.Copy(lambda, a, b);
        var swapped = (Expression<Func<double, double, double>>)ExposedTransformer.Copy(lambda, y, x);

        foreach (Expression<Func<double, double, double>> copy in new[] { renamed, swapped })
        {
            Assert.AreEqual(lambda.Type, copy.Type);
            Assert.AreEqual("difference", copy.Name);
            Assert.IsTrue(copy.TailCall);
            Assert.AreEqual(lambda.Compile()(5.0, 2.0), copy.Compile()(5.0, 2.0));
            Assert.IsTrue(ExpressionComparer.Default.Equals(lambda, copy), "Alpha-equivalent.");
        }

        CollectionAssert.AreEqual(new[] { a, b }, renamed.Parameters.ToArray());
        Assert.AreSame(a, ((BinaryExpression)renamed.Body).Left);
        Assert.AreSame(y, ((BinaryExpression)swapped.Body).Left);
    }

    /// <summary>
    /// Renaming is capture-avoiding inside the body: a nested lambda declaring the new name is renamed itself, labels keep their
    /// targets, and the result computes the same values.
    /// </summary>
    [TestMethod]
    public void Renaming_IsCaptureAvoiding_AndKeepsLabels()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression z = Expression.Parameter(typeof(double), "z");
        LabelTarget done = Expression.Label(typeof(double), "done");
        Expression nested = Expression.Invoke(Expression.Lambda<Func<double, double>>(Expression.Add(x, z), z), Expression.Constant(10.0));
        var lambda = Expression.Lambda<Func<double, double>>(Expression.Block(Expression.Return(done, nested), Expression.Label(done, Expression.Constant(0.0))), x);

        var renamed = (Expression<Func<double, double>>)ExposedTransformer.Copy(lambda, z);

        Assert.AreSame(z, renamed.Parameters[0]);
        Assert.AreEqual(lambda.Compile()(3.0), renamed.Compile()(3.0));
        var block = (BlockExpression)renamed.Body;
        Assert.AreSame(done, ((GotoExpression)block.Expressions[0]).Target);
        var inner = (LambdaExpression)((InvocationExpression)((GotoExpression)block.Expressions[0]).Value!).Expression;
        Assert.AreNotSame(z, inner.Parameters[0], "The nested declaration of z would capture the renamed x: it is renamed.");
    }

    /// <summary>
    /// Invalid lists are refused with <see cref="ArgumentException"/>, never silently ignored: a wrong length, a node that is not
    /// a parameter, another type, another by-reference flag, a duplicated declaration, a new declaration that would capture a
    /// free variable of the body, and a renaming the substitution cannot apply (an occurrence inside a quoted expression).
    /// </summary>
    [TestMethod]
    public void InvalidLists_AreRefused()
    {
        (Expression<Func<double, double, double>> lambda, ParameterExpression x, _) = Difference();
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        ParameterExpression text = Expression.Parameter(typeof(string), "text");
        ParameterExpression byRef = Expression.Parameter(typeof(double).MakeByRefType(), "r");
        ParameterExpression value = Expression.Parameter(typeof(double).MakeByRefType(), "value");
        var byRefLambda = Expression.Lambda<R4ByRefDelegate>(value, value);
        ParameterExpression w = Expression.Parameter(typeof(double), "w");
        var capturing = Expression.Lambda<Func<double, double>>(Expression.Add(w, P), w);
        var quoting = Expression.Lambda<Func<double, double>>(Expression.Call(ApplyMethod, Expression.Quote(Expression.Lambda<Func<double, double>>(w, a)), w), w);

        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(lambda, a));
        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(lambda, a, a, a));
        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(lambda, a, Expression.Constant(1.0)));
        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(lambda, a, text));
        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(lambda, a, byRef));
        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(byRefLambda, Expression.Parameter(typeof(double), "plain")));
        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(lambda, a, a));
        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(capturing, P), "p is free in the body: declaring it would capture it.");
        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(quoting, x), "w is read inside a quote, which substitution never enters.");
        Assert.AreSame(byRefLambda, ExposedTransformer.Copy(byRefLambda, value));
        Assert.AreSame(byRef, ((LambdaExpression)ExposedTransformer.Copy(byRefLambda, byRef)).Parameters[0]);
    }

    /// <summary>A deep lambda body is renamed through the budgeted substitution, so a hostile depth ends with a controlled refusal.</summary>
    [TestMethod]
    public void DeepBodies_AreRenamedFaithfully()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression body = x;
        for (int i = 0; i < 300; i++) body = Expression.Add(body, x);
        var lambda = Expression.Lambda<Func<double, double>>(body, x);

        var renamed = (Expression<Func<double, double>>)ExposedTransformer.Copy(lambda, Expression.Parameter(typeof(double), "y"));

        Assert.AreEqual(lambda.Compile()(2.0), renamed.Compile()(2.0));
    }
}
