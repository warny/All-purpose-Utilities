using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>
/// ETC R2, checkpoint C: capture-avoiding substitution of free occurrences (roadmap §5.3) through lambdas, conversion
/// lambdas, blocks and catch handlers, alpha-renaming of capturing declarations, the RuntimeVariables policy (C-07), the
/// Quote boundary (C-05 pending), and the conservative extension of invocation inlining (ETC-11). Every successful
/// substitution is checked for unbound parameters and, when executable, against the meaning of the original
/// (<c>((x) =&gt; body)(replacement)</c>).
/// </summary>
[TestClass]
[TestCategory("ETC-R2")]
public class ExpressionTreeR2SubstitutionTests
{
    /// <summary>The replaced parameter.</summary>
    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");

    /// <summary>An outer parameter used by replacements (and redeclared to provoke captures).</summary>
    private static readonly ParameterExpression Y = Expression.Parameter(typeof(double), "y");

    /// <summary>A delegate type with a by-reference parameter.</summary>
    /// <param name="value">The by-reference value.</param>
    /// <returns>A value.</returns>
    public delegate double RefFunc(ref double value);

    /// <summary>Exposes the protected substitution.</summary>
    private sealed class ExposedTransformer : ExpressionTransformer
    {
        /// <summary>Calls <c>ReplaceArguments</c>.</summary>
        /// <param name="e">The tree.</param>
        /// <param name="parameter">The replaced parameter.</param>
        /// <param name="replacement">The replacement.</param>
        /// <returns>The substituted tree.</returns>
        public Expression Replace(Expression e, ParameterExpression parameter, Expression replacement) => ReplaceArguments(e, [parameter], [replacement]);

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);
    }

    /// <summary>Records observable operations of compiled samples.</summary>
    public sealed class Trace
    {
        /// <summary>The <see cref="Record"/> method.</summary>
        public static readonly MethodInfo RecordMethod = typeof(Trace).GetMethod(nameof(Record))!;

        /// <summary>Gets the recorded operation names, in order.</summary>
        public List<string> Events { get; } = [];

        /// <summary>Records <paramref name="name"/> and returns <paramref name="value"/>.</summary>
        /// <param name="name">The operation name.</param>
        /// <param name="value">The value.</param>
        /// <returns><paramref name="value"/>.</returns>
        public double Record(string name, double value)
        {
            Events.Add(name);
            return value;
        }

        /// <summary>Builds a traced value.</summary>
        /// <param name="name">The operation name.</param>
        /// <param name="value">The value.</param>
        /// <returns>The call expression.</returns>
        public Expression Value(string name, Expression value) => Expression.Call(Expression.Constant(this), RecordMethod, Expression.Constant(name), value);
    }

    /// <summary>Collects the free parameters of a tree (binder-aware: lambda, block and catch declarations).</summary>
    private sealed class FreeParameterCollector : ExpressionVisitor
    {
        /// <summary>The declarations in scope, with their nesting count.</summary>
        private readonly Dictionary<ParameterExpression, int> _declared = new(ReferenceEqualityComparer.Instance);

        /// <summary>Gets the free parameters found.</summary>
        public HashSet<ParameterExpression> Free { get; } = new(ReferenceEqualityComparer.Instance);

        /// <inheritdoc/>
        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (!_declared.ContainsKey(node)) Free.Add(node);
            return node;
        }

        /// <inheritdoc/>
        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            Scoped(node.Parameters, () => Visit(node.Body));
            return node;
        }

        /// <inheritdoc/>
        protected override Expression VisitBlock(BlockExpression node)
        {
            Scoped(node.Variables, () => Visit(node.Expressions));
            return node;
        }

        /// <inheritdoc/>
        protected override CatchBlock VisitCatchBlock(CatchBlock node)
        {
            Scoped(node.Variable is null ? [] : [node.Variable], () =>
            {
                Visit(node.Filter);
                Visit(node.Body);
            });
            return node;
        }

        /// <summary>Visits with <paramref name="declarations"/> in scope.</summary>
        /// <param name="declarations">The declarations.</param>
        /// <param name="visit">The visit.</param>
        private void Scoped(IReadOnlyList<ParameterExpression> declarations, Action visit)
        {
            foreach (ParameterExpression declaration in declarations) _declared[declaration] = _declared.TryGetValue(declaration, out int count) ? count + 1 : 1;
            try
            {
                visit();
            }
            finally
            {
                foreach (ParameterExpression declaration in declarations)
                {
                    if (--_declared[declaration] == 0) _declared.Remove(declaration);
                }
            }
        }
    }

    /// <summary>Gets the free parameters of <paramref name="e"/>.</summary>
    /// <param name="e">The tree.</param>
    /// <returns>The free parameters.</returns>
    private static HashSet<ParameterExpression> FreeParameters(Expression e)
    {
        var collector = new FreeParameterCollector();
        collector.Visit(e);
        return collector.Free;
    }

    /// <summary>
    /// Substitutes <paramref name="replacement"/> for <see cref="X"/> in <paramref name="body"/>, then asserts that
    /// <see cref="X"/> is no longer free, that every free parameter of the result is one of <paramref name="outer"/>, and that
    /// the result computes the same value as <c>((x) =&gt; body)(replacement)</c> for <paramref name="arguments"/>.
    /// </summary>
    /// <param name="body">The tree, of type <see cref="double"/>, free in <see cref="X"/> and <paramref name="outer"/>.</param>
    /// <param name="replacement">The replacement, free in <paramref name="outer"/> only.</param>
    /// <param name="outer">The outer parameters.</param>
    /// <param name="arguments">Their values.</param>
    /// <returns>The substituted tree.</returns>
    private static Expression AssertSubstitutionPreservesMeaning(Expression body, Expression replacement, ParameterExpression[] outer, params object[] arguments)
    {
        Expression result = new ExposedTransformer().Replace(body, X, replacement);

        HashSet<ParameterExpression> free = FreeParameters(result);
        Assert.IsFalse(free.Contains(X), "The replaced parameter must not stay free.");
        foreach (ParameterExpression parameter in free)
        {
            Assert.IsTrue(outer.Contains(parameter), $"Unexpected free parameter '{parameter.Name}': a declaration captured or lost a reference.");
        }

        object? expected = Expression.Lambda(Expression.Invoke(Expression.Lambda(body, X), replacement), outer).Compile().DynamicInvoke(arguments);
        object? actual = Expression.Lambda(result, outer).Compile().DynamicInvoke(arguments);
        Assert.AreEqual(expected, actual);
        return result;
    }

    // ------------------------------------------------------------------------------------------
    // Free substitution and shadowing.
    // ------------------------------------------------------------------------------------------

    /// <summary>A free occurrence in a nested lambda body is substituted; the lambda keeps its parameter, delegate type, name and tail call.</summary>
    [TestMethod]
    public void FreeOccurrence_InLambdaBody_IsSubstituted()
    {
        ParameterExpression z = Expression.Parameter(typeof(double), "z");
        Expression<Func<double, double>> nested = Expression.Lambda<Func<double, double>>(Expression.Add(X, z), "named", true, [z]);
        Expression body = Expression.Invoke(nested, Expression.Constant(1.0));

        var result = (InvocationExpression)AssertSubstitutionPreservesMeaning(body, Expression.Constant(4.0), []);

        var lambda = (LambdaExpression)result.Expression;
        Assert.AreSame(z, lambda.Parameters.Single());
        Assert.AreEqual("named", lambda.Name);
        Assert.IsTrue(lambda.TailCall);
        Assert.AreEqual(typeof(Func<double, double>), lambda.Type);
    }

    /// <summary>A lambda, a block and a catch handler that redeclare the replaced parameter hide it in their scope.</summary>
    [TestMethod]
    public void RedeclaredParameter_IsShadowedByLambdaBlockAndCatch()
    {
        // Lambda: (x => x * 2)(3) + x.
        Expression lambda = Expression.Add(Expression.Invoke(Expression.Lambda<Func<double, double>>(Expression.Multiply(X, Expression.Constant(2.0)), X), Expression.Constant(3.0)), X);
        AssertSubstitutionPreservesMeaning(lambda, Expression.Constant(10.0), []);

        // Block: { double x = 1; x + 1 } + x.
        Expression block = Expression.Add(Expression.Block([X], Expression.Assign(X, Expression.Constant(1.0)), Expression.Add(X, Expression.Constant(1.0))), X);
        Expression substituted = AssertSubstitutionPreservesMeaning(block, Expression.Constant(10.0), []);
        Assert.AreSame(X, ((BlockExpression)((BinaryExpression)substituted).Left).Variables.Single(), "A shadowing declaration is not renamed.");

        // Catch: try { throw } catch (InvalidOperationException x') when x' != null { 1 } with a same-instance catch variable.
        ParameterExpression caught = Expression.Variable(typeof(InvalidOperationException), "x");
        Expression @catch = Expression.TryCatch(
            Expression.Throw(Expression.New(typeof(InvalidOperationException)), typeof(double)),
            Expression.Catch(caught, Expression.Condition(Expression.Equal(caught, Expression.Constant(null, typeof(InvalidOperationException))), Expression.Constant(0.0), Expression.Constant(1.0))));
        var replaced = (TryExpression)new ExposedTransformer().Replace(@catch, caught, Expression.Constant(null, typeof(InvalidOperationException)));
        Assert.AreSame(caught, replaced.Handlers[0].Variable, "Every occurrence is bound by the handler: the declaration is kept.");
        Assert.AreEqual(0, FreeParameters(replaced).Count);
        Assert.AreEqual(1.0, Expression.Lambda<Func<double>>(replaced).Compile()());
    }

    /// <summary>
    /// Capture avoidance in a lambda: substituting <c>x → y</c> under <c>y =&gt; x + y</c> renames the lambda parameter, so the
    /// inserted <c>y</c> still means the outer <c>y</c>.
    /// </summary>
    [TestMethod]
    public void CaptureAvoidance_InLambda_RenamesTheDeclaration()
    {
        Expression body = Expression.Invoke(Expression.Lambda<Func<double, double>>(Expression.Subtract(X, Y), Y), Expression.Constant(1.0));

        var result = (InvocationExpression)AssertSubstitutionPreservesMeaning(body, Y, [Y], 10.0);

        var lambda = (LambdaExpression)result.Expression;
        Assert.AreNotSame(Y, lambda.Parameters.Single(), "The capturing declaration must be renamed.");
        Assert.AreEqual("y", lambda.Parameters.Single().Name);
        Assert.AreEqual(typeof(double), lambda.Parameters.Single().Type);
    }

    /// <summary>Capture avoidance in a block: substituting <c>x → y</c> under <c>{ double y = 1; x - y }</c> renames the block variable.</summary>
    [TestMethod]
    public void CaptureAvoidance_InBlock_RenamesTheVariable()
    {
        Expression body = Expression.Block([Y], Expression.Assign(Y, Expression.Constant(1.0)), Expression.Subtract(X, Y));

        var result = (BlockExpression)AssertSubstitutionPreservesMeaning(body, Y, [Y], 10.0);

        Assert.AreNotSame(Y, result.Variables.Single());
        Assert.AreEqual(typeof(double), result.Type);
    }

    /// <summary>Capture avoidance in a catch handler: the catch variable a replacement mentions is renamed in the filter and body.</summary>
    [TestMethod]
    public void CaptureAvoidance_InCatch_RenamesTheVariable()
    {
        ParameterExpression error = Expression.Parameter(typeof(Exception), "error");
        ParameterExpression replaced = Expression.Parameter(typeof(Exception), "replaced");
        Expression Message(ParameterExpression e) => Expression.Condition(Expression.Equal(e, Expression.Constant(null, typeof(Exception))), Expression.Constant(0.0), Expression.Constant(1.0));
        Expression body = Expression.TryCatch(
            Expression.Throw(Expression.New(typeof(InvalidOperationException)), typeof(double)),
            Expression.Catch(error, Expression.Add(Message(replaced), Message(error)), Expression.Equal(replaced, Expression.Constant(null, typeof(Exception)))));

        var result = (TryExpression)new ExposedTransformer().Replace(body, replaced, error);

        ParameterExpression variable = result.Handlers[0].Variable!;
        Assert.AreNotSame(error, variable, "The capturing catch variable must be renamed.");
        Assert.IsTrue(FreeParameters(result).SetEquals([error]), "The inserted 'error' must stay free (the outer one).");
        object? expected = Expression.Lambda(Expression.Invoke(Expression.Lambda(body, replaced), error), error).Compile().DynamicInvoke([null]);
        object? actual = Expression.Lambda(result, error).Compile().DynamicInvoke([null]);
        Assert.AreEqual(expected, actual);
    }

    /// <summary>Nested binders sharing one parameter instance: each occurrence is resolved by its innermost declaration.</summary>
    [TestMethod]
    public void NestedBindersSharingInstances_ResolveInnermost()
    {
        // { double y = x; { double y = y + 1; y * x } + y } with x → y (outer): both blocks are renamed.
        Expression inner = Expression.Block([Y], Expression.Assign(Y, Expression.Constant(2.0)), Expression.Multiply(Y, X));
        Expression body = Expression.Block([Y], Expression.Assign(Y, X), Expression.Add(inner, Y));

        AssertSubstitutionPreservesMeaning(body, Y, [Y], 3.0);
    }

    /// <summary>A replacement referencing outer variables (an outer parameter and an enclosing lambda's parameter) keeps them free.</summary>
    [TestMethod]
    public void ReplacementReferencingOuterVariables_StaysBoundToThem()
    {
        ParameterExpression z = Expression.Parameter(typeof(double), "z");
        Expression body = Expression.Invoke(Expression.Lambda<Func<double, double>>(Expression.Multiply(X, z), z), Expression.Constant(2.0));

        AssertSubstitutionPreservesMeaning(body, Expression.Add(Y, Expression.Constant(1.0)), [Y], 4.0);
        AssertSubstitutionPreservesMeaning(body, Expression.Multiply(Y, Y), [Y], 3.0);
    }

    /// <summary>The conversion lambda of a coalesce node binds its own parameter; a free occurrence inside it is substituted and a capture is avoided.</summary>
    [TestMethod]
    public void CoalesceConversionLambda_IsSubstitutedAndCaptureAvoiding()
    {
        ParameterExpression nullable = Expression.Parameter(typeof(double?), "n");
        Expression body = Expression.Coalesce(nullable, Expression.Constant(0.0), Expression.Lambda<Func<double, double>>(Expression.Add(Y, X), Y));

        var result = (BinaryExpression)AssertSubstitutionPreservesMeaning(body, Y, [nullable, Y], null!, 5.0);
        Assert.AreNotSame(Y, result.Conversion!.Parameters.Single(), "The conversion parameter captured the replacement: it must be renamed.");

        var plain = (BinaryExpression)new ExposedTransformer().Replace(body, X, Expression.Constant(3.0));
        Assert.AreSame(Y, plain.Conversion!.Parameters.Single(), "Without capture the conversion parameter is kept.");
    }

    // ------------------------------------------------------------------------------------------
    // Control flow, runtime variables, quote.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Free occurrences inside a try body, filter, handler, finally, fault, switch value, test values, case bodies, goto value
    /// and label default value are substituted; label targets keep their identity.
    /// </summary>
    [TestMethod]
    public void ControlFlowPositions_AreSubstituted_KeepingLabelTargets()
    {
        ParameterExpression error = Expression.Variable(typeof(InvalidOperationException), "error");
        LabelTarget exit = Expression.Label(typeof(double), "exit");
        Expression body = Expression.Block(
            Expression.TryCatchFinally(
                Expression.Condition(Expression.GreaterThan(X, Expression.Constant(0.0)), Expression.Throw(Expression.New(typeof(InvalidOperationException)), typeof(double)), X),
                Expression.Block(typeof(void), X),
                Expression.Catch(error, Expression.Add(X, Expression.Constant(100.0)), Expression.GreaterThan(X, Expression.Constant(1.0))),
                Expression.Catch(typeof(Exception), Expression.Negate(X))),
            Expression.TryFault(X, Expression.Block(typeof(void), X)),
            Expression.Switch(X, X, Expression.SwitchCase(Expression.Multiply(X, Expression.Constant(2.0)), X, Expression.Constant(7.0))),
            Expression.IfThen(Expression.LessThan(X, Expression.Constant(0.0)), Expression.Return(exit, Expression.Subtract(X, Expression.Constant(1.0)))),
            Expression.Label(exit, Expression.Add(X, Expression.Constant(0.5))));

        foreach (double value in new[] { -3.0, 0.5, 2.0, 7.0 })
        {
            Expression result = AssertSubstitutionPreservesMeaning(body, Expression.Constant(value), []);
            var block = (BlockExpression)result;
            Assert.AreSame(exit, ((LabelExpression)block.Expressions[^1]).Target);
            Assert.AreSame(exit, ((GotoExpression)((ConditionalExpression)block.Expressions[^2]).IfTrue).Target);
        }
    }

    /// <summary>
    /// RuntimeVariables (C-07): a variable may be replaced by another parameter; replacing it by any other expression throws
    /// <see cref="NotSupportedException"/>; a variable bound by an enclosing block is never replaced.
    /// </summary>
    [TestMethod]
    public void RuntimeVariables_AcceptParametersOnly()
    {
        var transformer = new ExposedTransformer();
        RuntimeVariablesExpression variables = Expression.RuntimeVariables(X);

        var renamed = (RuntimeVariablesExpression)transformer.Replace(variables, X, Y);
        Assert.AreSame(Y, renamed.Variables.Single());
        Assert.ThrowsExactly<NotSupportedException>(() => transformer.Replace(variables, X, Expression.Constant(1.0)));

        Expression bound = Expression.Block([X], Expression.RuntimeVariables(X));
        Assert.AreSame(bound, transformer.Replace(bound, X, Expression.Constant(1.0)), "A bound runtime variable is not a free occurrence.");
    }

    /// <summary>
    /// Quote (C-05 pending): substitution never enters a quoted expression, so a captured outer reference inside it is left
    /// as it is (the quoted tree is not silently rewritten), while occurrences outside the quote are substituted.
    /// </summary>
    [TestMethod]
    public void Quote_IsNotEntered()
    {
        ParameterExpression z = Expression.Parameter(typeof(double), "z");
        UnaryExpression quote = Expression.Quote(Expression.Lambda<Func<double, double>>(Expression.Add(X, z), z));
        Expression body = Expression.Block(quote, X);

        var result = (BlockExpression)new ExposedTransformer().Replace(body, X, Expression.Constant(2.0));

        Assert.AreSame(quote, result.Expressions[0]);
        Assert.AreEqual(2.0, ((ConstantExpression)result.Expressions[1]).Value);
    }

    // ------------------------------------------------------------------------------------------
    // Invocation inlining (ETC-11).
    // ------------------------------------------------------------------------------------------

    /// <summary>Counts the invocations of a lambda left in a tree.</summary>
    /// <param name="expression">The tree.</param>
    /// <returns>The number of invocations whose target is a lambda.</returns>
    private static int CountLambdaInvocations(Expression expression)
    {
        var counter = new InvocationCounter();
        counter.Visit(expression);
        return counter.Count;
    }

    /// <summary>Counts invocations whose target is a lambda.</summary>
    private sealed class InvocationCounter : ExpressionVisitor
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

    /// <summary>
    /// Simplifies <c>(y) =&gt; body</c> with the exact and a derived simplifier, asserts the number of lambda invocations left,
    /// that no parameter is left unbound (compilation), and that the results, exceptions and traces match the original.
    /// </summary>
    /// <param name="build">Builds the body for a trace.</param>
    /// <param name="expectedInvocations">The number of lambda invocations the simplified tree must keep.</param>
    /// <param name="message">The case name.</param>
    private static void AssertInlining(Func<Trace, Expression> build, int expectedInvocations, string message)
    {
        foreach (ExpressionSimplifier simplifier in new[] { new ExpressionSimplifier(), new DerivedSimplifier() })
        {
            foreach (double input in new[] { -1.0, 2.0 })
            {
                var originalTrace = new Trace();
                var simplifiedTrace = new Trace();
                LambdaExpression original = Expression.Lambda(build(originalTrace), Y);
                var simplified = (LambdaExpression)simplifier.Simplify(Expression.Lambda(build(simplifiedTrace), Y));

                Assert.AreEqual(expectedInvocations, CountLambdaInvocations(simplified.Body), $"{message}: invocations left");
                Assert.AreEqual(0, FreeParameters(simplified).Count, $"{message}: unbound parameter");
                (object? Value, Type? Exception) expected = Run(original, input);
                (object? Value, Type? Exception) actual = Run(simplified, input);
                Assert.AreEqual(expected, actual, $"{message}, y = {input}");
                CollectionAssert.AreEqual(originalTrace.Events, simplifiedTrace.Events, $"{message}, y = {input}: trace");
            }
        }
    }

    /// <summary>A derived simplifier overriding nothing.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>Compiles and runs a one-parameter lambda, invoking a returned delegate without parameter once.</summary>
    /// <param name="lambda">The lambda.</param>
    /// <param name="input">Its argument.</param>
    /// <returns>The result (a returned <see cref="Func{TResult}"/> is invoked) and the escaping exception type.</returns>
    private static (object? Value, Type? Exception) Run(LambdaExpression lambda, double input)
    {
        try
        {
            object? value = lambda.Compile().DynamicInvoke(input);
            return (value is Func<double> function ? function() : value, null);
        }
        catch (TargetInvocationException exception)
        {
            return (null, exception.InnerException!.GetType());
        }
    }

    /// <summary>A lambda returning a lambda is inlined for a constant argument, and kept for a variable argument (the closure would read the variable later).</summary>
    [TestMethod]
    public void Inlining_LambdaReturningLambda()
    {
        ParameterExpression q = Expression.Parameter(typeof(double), "q");
        Expression<Func<double, Func<double>>> factory = Expression.Lambda<Func<double, Func<double>>>(Expression.Lambda<Func<double>>(q), q);

        AssertInlining(_ => Expression.Invoke(Expression.Invoke(factory, Expression.Constant(5.0))), 0, "constant argument");
        AssertInlining(_ => Expression.Invoke(Expression.Invoke(factory, Y)), 1, "variable argument");
    }

    /// <summary>A lambda returning a closure over a block local initialized from its parameter is inlined: the parameter is read once, before any effect.</summary>
    [TestMethod]
    public void Inlining_LambdaReturningBlockLocalCapture()
    {
        ParameterExpression q = Expression.Parameter(typeof(double), "q");
        ParameterExpression local = Expression.Variable(typeof(double), "local");
        Expression<Func<double, Func<double>>> factory = Expression.Lambda<Func<double, Func<double>>>(
            Expression.Block([local], Expression.Assign(local, q), Expression.Lambda<Func<double>>(local)),
            q);

        AssertInlining(_ => Expression.Invoke(Expression.Invoke(factory, Y)), 0, "block-local capture");
    }

    /// <summary>
    /// A lambda whose body is a try/catch is inlined, its handler and exception behavior kept: with constant arguments (the
    /// throwing and the normal path), and with a variable argument when the body reads it before any effect. An invocation
    /// placed inside a try/catch is inlined as well. A variable argument read in the other branch of a throwing conditional
    /// keeps the invocation (the conditional is scanned conservatively, one branch after the other).
    /// </summary>
    [TestMethod]
    public void Inlining_LambdaWithTryCatch()
    {
        ParameterExpression q = Expression.Parameter(typeof(double), "q");
        Expression<Func<double, double>> guarded = Expression.Lambda<Func<double, double>>(
            Expression.TryCatch(
                Expression.Condition(Expression.LessThan(q, Expression.Constant(0.0)), Expression.Throw(Expression.New(typeof(InvalidOperationException)), typeof(double)), Expression.Multiply(q, Expression.Constant(2.0))),
                Expression.Catch(typeof(InvalidOperationException), q)),
            q);
        Expression<Func<double, double>> reading = Expression.Lambda<Func<double, double>>(
            Expression.TryCatch(Expression.Multiply(q, Expression.Constant(2.0)), Expression.Catch(typeof(InvalidOperationException), q)),
            q);
        Expression<Func<double, double>> pure = Expression.Lambda<Func<double, double>>(Expression.Add(q, Expression.Constant(1.0)), q);

        AssertInlining(_ => Expression.Invoke(guarded, Expression.Constant(-1.0)), 0, "try/catch body, throwing path");
        AssertInlining(_ => Expression.Invoke(guarded, Expression.Constant(3.0)), 0, "try/catch body, normal path");
        AssertInlining(_ => Expression.Invoke(reading, Y), 0, "try/catch body reading a variable argument");
        AssertInlining(_ => Expression.TryCatch(Expression.Invoke(pure, Y), Expression.Catch(typeof(Exception), Expression.Constant(0.0))), 0, "invocation inside a try/catch");
        AssertInlining(_ => Expression.Invoke(guarded, Y), 1, "variable argument after a throwing branch");
    }

    /// <summary>An unused argument with an effect, and an argument used twice that has an effect, keep the invocation (evaluated exactly once).</summary>
    [TestMethod]
    public void Inlining_EffectfulArguments_KeepTheInvocation()
    {
        ParameterExpression q = Expression.Parameter(typeof(double), "q");
        Expression<Func<double, double>> unused = Expression.Lambda<Func<double, double>>(Expression.Constant(1.0), q);
        Expression<Func<double, double>> twice = Expression.Lambda<Func<double, double>>(Expression.Add(q, q), q);

        AssertInlining(trace => Expression.Invoke(unused, trace.Value("argument", Y)), 1, "unused effectful argument");
        AssertInlining(trace => Expression.Invoke(twice, trace.Value("argument", Y)), 1, "effectful argument used twice");
        AssertInlining(_ => Expression.Invoke(twice, Expression.Constant(4.0)), 0, "constant argument used twice");
    }

    /// <summary>A variable argument read after a write in the body, or used as an assignment target, keeps the invocation.</summary>
    [TestMethod]
    public void Inlining_VariableArgumentAfterWriteOrAsTarget_KeepsTheInvocation()
    {
        ParameterExpression q = Expression.Parameter(typeof(double), "q");
        ParameterExpression v = Expression.Variable(typeof(double), "v");

        AssertInlining(_ => Expression.Block(
            [v],
            Expression.Assign(v, Y),
            Expression.Invoke(Expression.Lambda<Func<double, double>>(Expression.Block(Expression.Assign(v, Expression.Constant(7.0)), q), q), v)), 1, "read after a write");
        AssertInlining(_ => Expression.Block(
            [v],
            Expression.Assign(v, Y),
            Expression.Add(Expression.Invoke(Expression.Lambda<Func<double, double>>(Expression.Assign(q, Expression.Constant(3.0)), q), v), v)), 1, "assignment target");
    }

    /// <summary>A lambda with a by-reference parameter is never inlined.</summary>
    [TestMethod]
    public void Inlining_ByRefParameter_KeepsTheInvocation()
    {
        ParameterExpression q = Expression.Parameter(typeof(double).MakeByRefType(), "q");
        ParameterExpression v = Expression.Variable(typeof(double), "v");
        Expression<RefFunc> byRef = Expression.Lambda<RefFunc>(Expression.Assign(q, Expression.Add(q, Expression.Constant(1.0))), q);

        AssertInlining(_ => Expression.Block([v], Expression.Assign(v, Y), Expression.Invoke(byRef, v), v), 1, "by-reference parameter");
    }

    /// <summary>
    /// Nested invocations with shadowing: the inner lambda redeclares the outer parameter instance, so the inner reference is
    /// bound by the inner lambda; both invocations are inlined with the right values.
    /// </summary>
    [TestMethod]
    public void Inlining_NestedInvocationsWithShadowing()
    {
        ParameterExpression q = Expression.Parameter(typeof(double), "q");
        Expression<Func<double, double>> inner = Expression.Lambda<Func<double, double>>(Expression.Multiply(q, Expression.Constant(2.0)), q);
        Expression<Func<double, double>> outer = Expression.Lambda<Func<double, double>>(Expression.Add(Expression.Invoke(inner, Expression.Constant(3.0)), q), q);

        AssertInlining(_ => Expression.Invoke(outer, Y), 0, "nested invocations with shadowing");
    }

    /// <summary>A block in the inlined body declaring the argument's variable is renamed, so the argument is not captured.</summary>
    [TestMethod]
    public void Inlining_AvoidsCapturingTheArgument()
    {
        ParameterExpression q = Expression.Parameter(typeof(double), "q");
        Expression<Func<double, double>> lambda = Expression.Lambda<Func<double, double>>(
            Expression.Block([Y], Expression.Assign(Y, Expression.Constant(1.0)), Expression.Subtract(q, Y)),
            q);

        AssertInlining(_ => Expression.Invoke(lambda, Y), 0, "argument variable redeclared by a body block");
    }

    /// <summary>
    /// An exception raised before or after the substituted read keeps its order relative to the traced effects (constant
    /// arguments, both paths); with a variable argument read after a traced call, the invocation is kept.
    /// </summary>
    [TestMethod]
    public void Inlining_ExceptionOrdering_IsKept()
    {
        ParameterExpression q = Expression.Parameter(typeof(double), "q");
        Expression Body(Trace trace, Expression argument) => Expression.Invoke(
            Expression.Lambda<Func<double, double>>(
                Expression.Block(
                    trace.Value("before", Expression.Constant(0.0)),
                    Expression.Condition(Expression.LessThan(q, Expression.Constant(0.0)), Expression.Throw(Expression.New(typeof(InvalidOperationException)), typeof(double)), q),
                    trace.Value("after", q)),
                q),
            argument);

        AssertInlining(trace => Body(trace, Expression.Constant(-1.0)), 0, "exception between the traced calls");
        AssertInlining(trace => Body(trace, Expression.Constant(2.0)), 0, "no exception");
        AssertInlining(trace => Body(trace, Y), 1, "variable argument read after a traced call");
    }

    /// <summary>A quoted reference to the parameter keeps the invocation: the substitution never enters a quote (C-05).</summary>
    [TestMethod]
    public void Inlining_QuotedReference_KeepsTheInvocation()
    {
        ParameterExpression q = Expression.Parameter(typeof(double), "q");
        ParameterExpression z = Expression.Parameter(typeof(double), "z");
        Expression<Func<double, double>> lambda = Expression.Lambda<Func<double, double>>(
            Expression.Block(Expression.Quote(Expression.Lambda<Func<double, double>>(Expression.Add(q, z), z)), q),
            q);

        Expression simplified = new ExpressionSimplifier().Simplify(Expression.Lambda(Expression.Invoke(lambda, Expression.Constant(2.0)), Y));

        Assert.AreEqual(1, CountLambdaInvocations(((LambdaExpression)simplified).Body));
    }
}
