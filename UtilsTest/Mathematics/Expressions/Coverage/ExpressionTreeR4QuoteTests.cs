using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>
/// ETC R4, checkpoint C (C-05): a quoted expression is a strict boundary of <see cref="ExpressionSimplifier"/> by default
/// (<see cref="ExpressionSimplifier.SimplifyQuotedExpressions"/> is <see langword="false"/>): neither its content nor the
/// <c>Quote</c> node itself is rewritten, no rule runs on it and <c>FinalizeExpression</c> does not receive it. Opting in
/// simplifies the quoted lambda like any lambda (delegate type, name, tail call, bindings and labels kept). The option never
/// changes substitution, which never enters a quoted expression, nor the generic <see cref="ExpressionTransformer"/>, which
/// keeps traversing it.
/// </summary>
[TestClass]
[TestCategory("ETC-R4")]
public class ExpressionTreeR4QuoteTests
{
    /// <summary>The free parameter of the samples.</summary>
    private static readonly ParameterExpression P = Expression.Parameter(typeof(double), "p");

    /// <summary>The <see cref="QuoteHost.Apply"/> method.</summary>
    private static readonly MethodInfo ApplyMethod = typeof(QuoteHost).GetMethod(nameof(QuoteHost.Apply))!;

    /// <summary>A simplifier with the default policy (quotes are boundaries).</summary>
    private static ExpressionSimplifier Disabled() => new();

    /// <summary>A simplifier that simplifies quoted expressions.</summary>
    private static ExpressionSimplifier Enabled() => new() { SimplifyQuotedExpressions = true };

    /// <summary>Builds <c>Quote(x => body(x))</c> over a fresh <see cref="double"/> parameter.</summary>
    /// <param name="body">Builds the body.</param>
    /// <param name="name">The lambda name.</param>
    /// <param name="tailCall">The tail-call flag.</param>
    /// <returns>The quote.</returns>
    private static UnaryExpression Quote(Func<ParameterExpression, Expression> body, string? name = null, bool tailCall = false)
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        return Expression.Quote(Expression.Lambda<Func<double, double>>(body(x), name, tailCall, [x]));
    }

    /// <summary>Gets the quoted lambda of <paramref name="quote"/>.</summary>
    /// <param name="quote">A quote node.</param>
    /// <returns>Its lambda.</returns>
    private static LambdaExpression Quoted(Expression quote) => (LambdaExpression)((UnaryExpression)quote).Operand;

    /// <summary>A derived simplifier with a custom rule rewriting every quote, and a recording <c>FinalizeExpression</c>.</summary>
    private sealed class QuoteRewritingSimplifier : ExpressionSimplifier
    {
        /// <summary>Gets the number of times the quote rule ran.</summary>
        public int RuleCalls { get; private set; }

        /// <summary>Gets the quote nodes received by <c>FinalizeExpression</c>.</summary>
        public int FinalizedQuotes { get; private set; }

        /// <summary>Replaces a quote by the quote of a constant lambda.</summary>
        /// <param name="quote">The quote.</param>
        /// <returns>The replacement.</returns>
        [ExpressionSignature(ExpressionType.Quote)]
        private Expression RewriteQuote(UnaryExpression quote)
        {
            RuleCalls++;
            ParameterExpression x = Expression.Parameter(typeof(double), "x");
            return Expression.Quote(Expression.Lambda<Func<double, double>>(Expression.Constant(42.0), x));
        }

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
        {
            if (e.NodeType == ExpressionType.Quote) FinalizedQuotes++;
            return base.FinalizeExpression(e, parameters);
        }
    }

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

    /// <summary>The option defaults to <see langword="false"/> and is fixed at construction.</summary>
    [TestMethod]
    public void Option_DefaultsToFalse_AndIsInitOnly()
    {
        Assert.IsFalse(new ExpressionSimplifier().SimplifyQuotedExpressions);
        Assert.IsTrue(Enabled().SimplifyQuotedExpressions);
        PropertyInfo property = typeof(ExpressionSimplifier).GetProperty(nameof(ExpressionSimplifier.SimplifyQuotedExpressions))!;
        Assert.IsTrue(property.SetMethod!.ReturnParameter.GetRequiredCustomModifiers().Any(modifier => modifier.FullName == "System.Runtime.CompilerServices.IsExternalInit"), "init-only");
    }

    /// <summary>
    /// Disabled (default): <c>Quote(x =&gt; x * 1)</c> and <c>Quote(x =&gt; x + 0)</c> are returned as the very same instance by the
    /// exact and a derived simplifier. Enabled: the quoted body is simplified to <c>x</c>, the node stays a quote of the same
    /// type, and the quoted lambda keeps its delegate type, its name, its tail-call flag and its own parameter.
    /// </summary>
    [TestMethod]
    public void QuotedArithmetic_IsKeptByDefault_AndSimplifiedOnOptIn()
    {
        UnaryExpression[] quotes =
        [
            Quote(x => Expression.Multiply(x, Expression.Constant(1.0))),
            Quote(x => Expression.Add(x, Expression.Constant(0.0)), "named", tailCall: true),
        ];

        foreach (UnaryExpression quote in quotes)
        {
            Assert.AreSame(quote, Disabled().Simplify(quote));
            Assert.AreSame(quote, new DerivedSimplifier().Simplify(quote));

            var simplified = (UnaryExpression)Enabled().Simplify(quote);
            Assert.AreEqual(ExpressionType.Quote, simplified.NodeType);
            Assert.AreEqual(quote.Type, simplified.Type);
            LambdaExpression original = Quoted(quote);
            LambdaExpression lambda = Quoted(simplified);
            Assert.AreEqual(original.Type, lambda.Type);
            Assert.AreEqual(original.Name, lambda.Name);
            Assert.AreEqual(original.TailCall, lambda.TailCall);
            Assert.AreSame(original.Parameters[0], lambda.Parameters[0]);
            Assert.AreSame(lambda.Parameters[0], lambda.Body);
        }
    }

    /// <summary>A nested quote is kept whole by default and simplified at every level on opt-in.</summary>
    [TestMethod]
    public void NestedQuotes_FollowTheOption()
    {
        UnaryExpression inner = Quote(x => Expression.Multiply(x, Expression.Constant(1.0)));
        UnaryExpression outer = Expression.Quote(Expression.Lambda<Func<Expression<Func<double, double>>>>(inner));

        Assert.AreSame(outer, Disabled().Simplify(outer));

        var simplified = (UnaryExpression)Enabled().Simplify(outer);
        var simplifiedInner = (UnaryExpression)Quoted(simplified).Body;
        Assert.AreEqual(ExpressionType.Quote, simplifiedInner.NodeType);
        Assert.AreSame(Quoted(simplifiedInner).Parameters[0], Quoted(simplifiedInner).Body);
    }

    /// <summary>
    /// A quoted lambda capturing an outer parameter, holding a block with a variable and a try/catch with a catch variable,
    /// placed inside a conditional and passed to a method call: kept unchanged by default (the enclosing nodes are still
    /// simplified), simplified on opt-in with every binding valid; both compiled results behave like the source.
    /// </summary>
    [TestMethod]
    public void QuotedBindersAndCaptures_StayValid()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression local = Expression.Variable(typeof(double), "local");
        ParameterExpression error = Expression.Variable(typeof(Exception), "error");
        Expression body = Expression.Block(
            [local],
            Expression.Assign(local, Expression.Multiply(Expression.Add(x, P), Expression.Constant(1.0))),
            Expression.TryCatch(
                Expression.Multiply(local, Expression.Constant(1.0)),
                Expression.Catch(error, Expression.Add(local, Expression.Constant(0.0)))));
        UnaryExpression quote = Expression.Quote(Expression.Lambda<Func<double, double>>(body, x));
        var source = Expression.Lambda<Func<double, double>>(
            Expression.Condition(
                Expression.GreaterThan(Expression.Multiply(P, Expression.Constant(1.0)), Expression.Constant(0.0)),
                Expression.Call(ApplyMethod, quote, P),
                Expression.Constant(-1.0)),
            P);

        var disabled = (Expression<Func<double, double>>)Disabled().Simplify(source);
        var enabled = (Expression<Func<double, double>>)Enabled().Simplify(source);

        var disabledCall = (MethodCallExpression)((ConditionalExpression)disabled.Body).IfTrue;
        Assert.AreSame(quote, disabledCall.Arguments[0], "The quote instance is kept by default.");
        Assert.AreSame(P, ((BinaryExpression)((ConditionalExpression)disabled.Body).Test).Left, "The enclosing nodes are still simplified.");

        var enabledQuote = (UnaryExpression)((MethodCallExpression)((ConditionalExpression)enabled.Body).IfTrue).Arguments[0];
        Assert.AreNotSame(quote, enabledQuote);
        var enabledBlock = (BlockExpression)Quoted(enabledQuote).Body;
        Assert.AreSame(local, enabledBlock.Variables[0]);
        Assert.AreSame(error, ((TryExpression)enabledBlock.Expressions[1]).Handlers[0].Variable);

        foreach (double input in new[] { -2.0, 3.0 })
        {
            double expected = source.Compile()(input);
            Assert.AreEqual(expected, disabled.Compile()(input));
            Assert.AreEqual(expected, enabled.Compile()(input));
        }
    }

    /// <summary>
    /// Observable operations inside a quoted lambda: kept by default; on opt-in, simplified under the strict effect rules
    /// (ETC-15), so the evaluations of the traced calls are kept in number and order.
    /// </summary>
    [TestMethod]
    public void QuotedObservableOperations_KeepTheirEvaluations()
    {
        MethodInfo trace = R4DynamicRuntime.TraceMethod;
        UnaryExpression quote = Quote(x => Expression.Add(
            Expression.Multiply(Expression.Call(trace, Expression.Constant("a"), x), Expression.Constant(0.0)),
            Expression.Subtract(Expression.Call(trace, Expression.Constant("b"), x), Expression.Call(trace, Expression.Constant("b"), x))));
        var source = Expression.Lambda<Func<double, double>>(Expression.Call(ApplyMethod, quote, P), P);

        Assert.AreSame(quote, ((MethodCallExpression)((LambdaExpression)Disabled().Simplify(source)).Body).Arguments[0]);
        var enabled = (Expression<Func<double, double>>)Enabled().Simplify(source);

        R4DynamicRuntime.Reset();
        double expected = source.Compile()(2.0);
        string[] expectedEvents = [.. R4DynamicRuntime.Events];
        R4DynamicRuntime.Reset();
        Assert.AreEqual(expected, enabled.Compile()(2.0));
        CollectionAssert.AreEqual(expectedEvents, R4DynamicRuntime.Events.ToArray());
    }

    /// <summary>
    /// A derived simplifier declaring a rule on <see cref="ExpressionType.Quote"/> and overriding <c>FinalizeExpression</c> (then
    /// deferring to the base): by default neither its rule nor its finalizer sees the quote, which is returned unchanged; on
    /// opt-in the rule runs and its rewrite is used.
    /// </summary>
    [TestMethod]
    public void DerivedQuoteRule_RunsOnlyOnOptIn()
    {
        UnaryExpression quote = Quote(x => Expression.Multiply(x, Expression.Constant(1.0)));
        Expression call = Expression.Call(ApplyMethod, quote, P);

        var byDefault = new QuoteRewritingSimplifier();
        Assert.AreSame(quote, byDefault.Simplify(quote));
        Assert.AreSame(quote, ((MethodCallExpression)byDefault.Simplify(call)).Arguments[0]);
        Assert.AreEqual(0, byDefault.RuleCalls);
        Assert.AreEqual(0, byDefault.FinalizedQuotes);

        var optedIn = new QuoteRewritingSimplifier { SimplifyQuotedExpressions = true };
        var rewritten = (UnaryExpression)optedIn.Simplify(quote);
        Assert.AreEqual(1, optedIn.RuleCalls);
        Assert.AreEqual(42.0, ((ConstantExpression)Quoted(rewritten).Body).Value);
    }

    /// <summary>The generic transformer keeps its historical behavior: it traverses quoted expressions.</summary>
    [TestMethod]
    public void GenericTransformer_StillTraversesQuotes()
    {
        UnaryExpression quote = Quote(x => Expression.Multiply(x, Expression.Constant(1.0)));

        var result = (UnaryExpression)new RecursiveTransformer().Transform(quote);

        Assert.AreEqual(2.0, ((ConstantExpression)((BinaryExpression)Quoted(result).Body).Right).Value);
    }

    /// <summary>
    /// The option never opens substitution through a quote: <c>ReplaceArguments</c> leaves a quoted free occurrence untouched,
    /// and the inlining of an invocation whose body references the invoked parameter inside a quote is refused with and
    /// without the option.
    /// </summary>
    [TestMethod]
    public void Substitution_NeverEntersQuotes_WhateverTheOption()
    {
        Expression quotedFree = Expression.Quote(Expression.Lambda<Func<double>>(P));
        Assert.AreSame(quotedFree, new ExposedTransformer().Replace(quotedFree, P, Expression.Constant(5.0)));

        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression quotedReference = Expression.Quote(Expression.Lambda<Func<double, double>>(Expression.Add(x, y), x));
        var source = Expression.Lambda<Func<double, double>>(
            Expression.Invoke(Expression.Lambda(Expression.Call(ApplyMethod, quotedReference, y), y), P),
            P);

        foreach (ExpressionSimplifier simplifier in new[] { Disabled(), Enabled() })
        {
            var simplified = (Expression<Func<double, double>>)simplifier.Simplify(source);
            Assert.IsInstanceOfType<InvocationExpression>(simplified.Body, "The invocation must be kept: its parameter is read inside a quote.");
            Assert.AreEqual(source.Compile()(1.5), simplified.Compile()(1.5));
        }
    }

    /// <summary>
    /// Structural comparison is unchanged by the policy: two independently built quotes of alpha-equivalent lambdas are equal
    /// and hash alike. Since the public comparer simplifies with the default policy, a quote of <c>x =&gt; x * 1</c> is no longer
    /// equal to a quote of <c>x =&gt; x</c> (it was before R4, because the comparer rewrote quoted data).
    /// </summary>
    [TestMethod]
    public void Comparer_ComparesQuotesStructurally_WithoutRewritingThem()
    {
        UnaryExpression first = Quote(x => Expression.Multiply(x, Expression.Constant(1.0)));
        UnaryExpression second = Quote(x => Expression.Multiply(x, Expression.Constant(1.0)));
        UnaryExpression identity = Quote(x => x);

        Assert.IsTrue(ExpressionComparer.Default.Equals(first, second));
        Assert.AreEqual(ExpressionComparer.Default.GetHashCode(first), ExpressionComparer.Default.GetHashCode(second));
        Assert.IsFalse(ExpressionComparer.Default.Equals(first, identity));
    }

    /// <summary>
    /// One instance per policy, shared by concurrent and repeated calls, always applies its own policy: the option is fixed at
    /// construction, so no call can observe another policy, even while other calls run.
    /// </summary>
    [TestMethod]
    public void SharedInstances_ApplyTheirOwnPolicy_UnderConcurrency()
    {
        ExpressionSimplifier disabled = Disabled();
        ExpressionSimplifier enabled = Enabled();
        UnaryExpression[] quotes = Enumerable.Range(0, 64).Select(_ => Quote(x => Expression.Multiply(x, Expression.Constant(1.0)))).ToArray();

        Parallel.For(0, 512, i =>
        {
            UnaryExpression quote = quotes[i % quotes.Length];
            if (i % 2 == 0)
            {
                Assert.AreSame(quote, disabled.Simplify(quote));
            }
            else
            {
                LambdaExpression lambda = Quoted(enabled.Simplify(quote));
                Assert.AreSame(lambda.Parameters[0], lambda.Body);
            }
        });
    }
}

/// <summary>Receives quoted lambdas in the ETC R4 quote samples.</summary>
public static class QuoteHost
{
    /// <summary>Compiles <paramref name="function"/> and applies it to <paramref name="value"/>.</summary>
    /// <param name="function">The quoted function.</param>
    /// <param name="value">The argument.</param>
    /// <returns>The result.</returns>
    public static double Apply(Expression<Func<double, double>> function, double value) => function.Compile()(value);
}
