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
/// ETC R2, checkpoint A: traversal and faithful reconstruction of the binding and control-flow families (Block, Try with its
/// handlers, Switch with its cases, Loop, Goto, Label), the explicit policies of RuntimeVariables (references that stay
/// parameters) and DebugInfo (leaf), the <c>CopyExpression</c> arity contract of these families (C-02), the preparation order
/// and hook contract observed by subclasses (C-03, H-1, H-2, H-4), and executable original-versus-transformed equivalence.
/// </summary>
[TestClass]
[TestCategory("ETC-R2")]
public class ExpressionTreeR2Tests
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
    /// A recursive transformer recording every node passed to <c>PrepareExpression</c> and every <c>FinalizeExpression</c> call
    /// with its child list, so preparation order and child layout are observable (H-2, H-3).
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

    /// <summary>A transformer with a positional rule on blocks (H-1): it receives the prepared statements as children.</summary>
    private sealed class BlockRuleTransformer : ExpressionTransformer
    {
        /// <summary>Gets the statement counts the rule received.</summary>
        public List<int> Seen { get; } = [];

        /// <summary>Records a block and keeps it.</summary>
        /// <param name="block">The prepared block.</param>
        /// <param name="statements">The prepared statements.</param>
        /// <returns>The block.</returns>
        [ExpressionSignature(ExpressionType.Block)]
        private Expression Block(BlockExpression block, Expression[] statements)
        {
            Seen.Add(statements.Length);
            return block;
        }

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <inheritdoc/>
        protected override Expression PrepareExpression(Expression e) => TransformCore(e);

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => CopyExpression(e, parameters);
    }

    /// <summary>A transformer whose <c>PrepareExpression</c> turns every parameter into a constant, to probe the declaration policy.</summary>
    private sealed class ParameterReplacingTransformer : ExpressionTransformer
    {
        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <inheritdoc/>
        protected override Expression PrepareExpression(Expression e) => e is ParameterExpression ? Expression.Constant(0.0) : TransformCore(e);

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

    /// <summary>Records observable operations of compiled samples, so evaluation count and order can be compared.</summary>
    public sealed class Trace
    {
        /// <summary>The <see cref="Record"/> method.</summary>
        public static readonly MethodInfo RecordMethod = typeof(Trace).GetMethod(nameof(Record))!;

        /// <summary>The <see cref="Fail"/> method.</summary>
        public static readonly MethodInfo FailMethod = typeof(Trace).GetMethod(nameof(Fail))!;

        /// <summary>The <see cref="Accept"/> method.</summary>
        public static readonly MethodInfo AcceptMethod = typeof(Trace).GetMethod(nameof(Accept))!;

        /// <summary>Gets the recorded operation names, in order.</summary>
        public List<string> Events { get; } = [];

        /// <summary>Records <paramref name="name"/> and returns <paramref name="value"/>.</summary>
        /// <param name="name">The operation name.</param>
        /// <param name="value">The value to return.</param>
        /// <returns><paramref name="value"/>.</returns>
        public double Record(string name, double value)
        {
            Events.Add(name);
            return value;
        }

        /// <summary>Records <paramref name="name"/> and throws.</summary>
        /// <param name="name">The operation name.</param>
        /// <returns>Never returns.</returns>
        /// <exception cref="InvalidOperationException">Always.</exception>
        public double Fail(string name)
        {
            Events.Add(name);
            throw new InvalidOperationException(name);
        }

        /// <summary>Records <paramref name="name"/> and returns <paramref name="result"/> (a filter decision).</summary>
        /// <param name="name">The operation name.</param>
        /// <param name="result">The decision.</param>
        /// <returns><paramref name="result"/>.</returns>
        public bool Accept(string name, bool result)
        {
            Events.Add(name);
            return result;
        }

        /// <summary>Builds a traced value.</summary>
        /// <param name="name">The operation name.</param>
        /// <param name="value">The value.</param>
        /// <returns>The call expression.</returns>
        public Expression Value(string name, Expression value) => Expression.Call(Expression.Constant(this), RecordMethod, Expression.Constant(name), value);

        /// <summary>Builds a traced throwing call.</summary>
        /// <param name="name">The operation name.</param>
        /// <returns>The call expression.</returns>
        public Expression Throwing(string name) => Expression.Call(Expression.Constant(this), FailMethod, Expression.Constant(name));

        /// <summary>Builds a traced filter decision.</summary>
        /// <param name="name">The operation name.</param>
        /// <param name="result">The decision.</param>
        /// <returns>The call expression.</returns>
        public Expression Filter(string name, bool result) => Expression.Call(Expression.Constant(this), AcceptMethod, Expression.Constant(name), Expression.Constant(result));
    }

    /// <summary>A comparison method for switches that records its calls through a static trace.</summary>
    /// <param name="value">The switch value.</param>
    /// <param name="test">The test value.</param>
    /// <returns>Whether both values are within 0.5.</returns>
    public static bool NearlyEqual(double value, double test)
    {
        ComparisonCalls?.Add($"{value}~{test}");
        return Math.Abs(value - test) < 0.5;
    }

    /// <summary>The calls recorded by <see cref="NearlyEqual"/> while a test observes them, or <see langword="null"/>.</summary>
    [ThreadStatic]
    private static List<string>? ComparisonCalls;

    /// <summary>The transformers whose results must behave like the original: exact simplifier, derived simplifier, generic recursive transformer without rule.</summary>
    /// <returns>The transformation functions, with their names.</returns>
    private static IEnumerable<(string Name, Func<Expression, Expression> Transform)> SemanticTransformers()
    {
        yield return ("exact simplifier", e => new ExpressionSimplifier().Simplify(e));
        yield return ("derived simplifier", e => new DerivedSimplifier().Simplify(e));
    }

    /// <summary>
    /// Compiles <paramref name="body"/> (a function of <see cref="P"/>) and each transformed version, runs them on several
    /// inputs with a fresh trace each time, and asserts identical results, exception types and traces.
    /// </summary>
    /// <param name="build">Builds the body for a trace.</param>
    private static void AssertSameBehavior(Func<Trace, Expression> build)
    {
        foreach ((string name, Func<Expression, Expression> transform) in SemanticTransformers())
        {
            foreach (double input in new[] { -2.0, 0.0, 0.25, 3.0 })
            {
                var originalTrace = new Trace();
                var transformedTrace = new Trace();
                Expression original = build(originalTrace);
                Expression transformed = transform(build(transformedTrace));
                Assert.AreEqual(original.Type, transformed.Type, name);

                (object? Value, Type? Exception) expected = Run(original, input);
                (object? Value, Type? Exception) actual = Run(transformed, input);
                Assert.AreEqual(expected.Exception, actual.Exception, $"{name}, p = {input}: exception");
                Assert.AreEqual(expected.Value, actual.Value, $"{name}, p = {input}: value");
                CollectionAssert.AreEqual(originalTrace.Events, transformedTrace.Events, $"{name}, p = {input}: trace [{string.Join(", ", originalTrace.Events)}] vs [{string.Join(", ", transformedTrace.Events)}]");
            }
        }
    }

    /// <summary>Compiles and runs <paramref name="body"/> at <paramref name="input"/>.</summary>
    /// <param name="body">A body using <see cref="P"/>.</param>
    /// <param name="input">The value of <see cref="P"/>.</param>
    /// <returns>The boxed result (or <see langword="null"/> for <see cref="void"/>) and the type of the escaping exception, if any.</returns>
    private static (object? Value, Type? Exception) Run(Expression body, double input)
    {
        Delegate compiled = Expression.Lambda(body, P).Compile();
        try
        {
            return (compiled.DynamicInvoke(input), null);
        }
        catch (TargetInvocationException exception)
        {
            return (null, exception.InnerException!.GetType());
        }
    }

    /// <summary>Collects every node of a tree.</summary>
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

    /// <summary>Counts the ordinary <c>p * 1</c> multiplications left in a tree.</summary>
    /// <param name="e">The tree.</param>
    /// <returns>The number of <see cref="ExpressionType.Multiply"/> nodes.</returns>
    private static int CountMultiplications(Expression e)
    {
        var collector = new NodeCollector();
        collector.Visit(e);
        return collector.Nodes.Count(node => node.NodeType == ExpressionType.Multiply);
    }

    // ------------------------------------------------------------------------------------------
    // Traversal and metadata.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// A block is traversed by every transformer kind: its statements are simplified, its variables are the same instances in
    /// the same order, and its explicit result type survives even when it differs from the last statement's type.
    /// </summary>
    [TestMethod]
    public void Block_IsTraversed_KeepingVariablesAndExplicitType()
    {
        ParameterExpression first = Expression.Variable(typeof(double), "first");
        ParameterExpression second = Expression.Variable(typeof(double), "second");
        BlockExpression block = Expression.Block(
            typeof(object),
            [first, second],
            Expression.Assign(first, Leaf()),
            Expression.Assign(second, Expression.Add(first, Leaf())),
            Expression.Convert(second, typeof(object)));

        foreach (Expression result in new[] { new ExpressionSimplifier().Simplify(block), new DerivedSimplifier().Simplify(block), new RecursiveTransformer().Transform(block) })
        {
            var rebuilt = (BlockExpression)result;
            Assert.AreNotSame(block, rebuilt);
            Assert.AreEqual(typeof(object), rebuilt.Type);
            Assert.AreSame(first, rebuilt.Variables[0]);
            Assert.AreSame(second, rebuilt.Variables[1]);
            Assert.AreEqual(3, rebuilt.Expressions.Count);
        }

        Assert.AreEqual(0, CountMultiplications(new ExpressionSimplifier().Simplify(block)));
        Assert.AreEqual(6.0, Expression.Lambda<Func<double, object>>(new ExpressionSimplifier().Simplify(block), P).Compile()(3.0));
    }

    /// <summary>
    /// A try expression is traversed in structural order and rebuilt with the same handler count and order, test types,
    /// catch variables (including a handler without variable), filters, finally and result type; a fault stays a fault.
    /// </summary>
    [TestMethod]
    public void Try_IsTraversed_KeepingHandlersFiltersFinallyAndFault()
    {
        ParameterExpression exception = Expression.Variable(typeof(InvalidOperationException), "ex");
        TryExpression withFinally = Expression.TryCatchFinally(
            Leaf(),
            Expression.Block(typeof(void), Leaf()),
            Expression.Catch(exception, Leaf(), Expression.NotEqual(exception, Expression.Constant(null, typeof(InvalidOperationException)))),
            Expression.Catch(typeof(ArgumentException), Leaf()));
        TryExpression withFault = Expression.TryFault(Leaf(), Expression.Block(typeof(void), Leaf()));

        foreach (Func<Expression, Expression> transform in new Func<Expression, Expression>[] { e => new ExpressionSimplifier().Simplify(e), e => new DerivedSimplifier().Simplify(e), e => new RecursiveTransformer().Transform(e) })
        {
            var rebuilt = (TryExpression)transform(withFinally);
            Assert.AreNotSame(withFinally, rebuilt);
            Assert.AreEqual(withFinally.Type, rebuilt.Type);
            Assert.AreEqual(2, rebuilt.Handlers.Count);
            Assert.AreEqual(typeof(InvalidOperationException), rebuilt.Handlers[0].Test);
            Assert.AreSame(exception, rebuilt.Handlers[0].Variable);
            Assert.IsNotNull(rebuilt.Handlers[0].Filter);
            Assert.AreEqual(typeof(ArgumentException), rebuilt.Handlers[1].Test);
            Assert.IsNull(rebuilt.Handlers[1].Variable);
            Assert.IsNull(rebuilt.Handlers[1].Filter);
            Assert.IsNotNull(rebuilt.Finally);
            Assert.IsNull(rebuilt.Fault);

            var fault = (TryExpression)transform(withFault);
            Assert.IsNotNull(fault.Fault);
            Assert.IsNull(fault.Finally);
            Assert.AreEqual(0, fault.Handlers.Count);
        }

        Assert.AreEqual(0, CountMultiplications(new ExpressionSimplifier().Simplify(withFinally)));
    }

    /// <summary>
    /// A switch is traversed and rebuilt with the same comparison method (and a <see langword="null"/> one), case order,
    /// test-value order (duplicates kept), default body and result type; the comparison method is never run.
    /// </summary>
    [TestMethod]
    public void Switch_IsTraversed_KeepingComparisonCasesAndTestOrder()
    {
        MethodInfo comparison = typeof(ExpressionTreeR2Tests).GetMethod(nameof(NearlyEqual))!;
        SwitchExpression withComparison = Expression.Switch(
            Leaf(),
            Leaf(),
            comparison,
            Expression.SwitchCase(Expression.Constant(10.0), Expression.Multiply(Expression.Constant(2.0), Expression.Constant(1.0)), Expression.Constant(1.0)),
            Expression.SwitchCase(Expression.Constant(20.0), Expression.Constant(1.0)));
        SwitchExpression withoutComparison = Expression.Switch(
            Expression.Convert(Leaf(), typeof(int)),
            Expression.SwitchCase(Expression.Block(typeof(void), Leaf()), Expression.Constant(3), Expression.Constant(3)));

        ComparisonCalls = [];
        try
        {
            foreach (Func<Expression, Expression> transform in new Func<Expression, Expression>[] { e => new ExpressionSimplifier().Simplify(e), e => new DerivedSimplifier().Simplify(e), e => new RecursiveTransformer().Transform(e) })
            {
                var rebuilt = (SwitchExpression)transform(withComparison);
                Assert.AreSame(comparison, rebuilt.Comparison);
                Assert.AreEqual(2, rebuilt.Cases.Count);
                Assert.AreEqual(2, rebuilt.Cases[0].TestValues.Count);
                Assert.AreEqual(1, rebuilt.Cases[1].TestValues.Count);
                Assert.IsNotNull(rebuilt.DefaultBody);
                Assert.AreEqual(withComparison.Type, rebuilt.Type);

                var plain = (SwitchExpression)transform(withoutComparison);
                Assert.AreEqual(withoutComparison.Comparison, plain.Comparison);
                Assert.IsNull(plain.DefaultBody);
                Assert.AreEqual(2, plain.Cases[0].TestValues.Count, "Duplicate test values are neither sorted nor deduplicated.");
            }

            Assert.AreEqual(0, ComparisonCalls.Count, "Transforming must never run the comparison method.");
        }
        finally
        {
            ComparisonCalls = null;
        }

        var simplified = (SwitchExpression)new ExpressionSimplifier().Simplify(withComparison);
        Assert.AreEqual(2.0, ((ConstantExpression)simplified.Cases[0].TestValues[0]).Value, "Test values are simplified in place.");
        Assert.AreEqual(1.0, ((ConstantExpression)simplified.Cases[0].TestValues[1]).Value, "Test-value order is kept.");
    }

    /// <summary>
    /// A loop, a jump and a label are traversed and rebuilt with the very same <see cref="LabelTarget"/> instances, the jump
    /// kind and type, and a label's absent default value.
    /// </summary>
    [TestMethod]
    public void LoopGotoAndLabel_AreTraversed_KeepingLabelTargets()
    {
        LabelTarget breakLabel = Expression.Label(typeof(double), "break");
        LabelTarget continueLabel = Expression.Label("continue");
        LoopExpression loop = Expression.Loop(Expression.Break(breakLabel, Leaf()), breakLabel, continueLabel);
        LabelTarget returnLabel = Expression.Label(typeof(double), "return");
        GotoExpression jump = Expression.Return(returnLabel, Leaf(), typeof(double));
        LabelExpression label = Expression.Label(returnLabel, Leaf());
        LabelTarget voidLabel = Expression.Label("end");
        LabelExpression voidDefinition = Expression.Label(voidLabel);
        GotoExpression voidJump = Expression.Goto(voidLabel);

        foreach (Func<Expression, Expression> transform in new Func<Expression, Expression>[] { e => new ExpressionSimplifier().Simplify(e), e => new DerivedSimplifier().Simplify(e), e => new RecursiveTransformer().Transform(e) })
        {
            var rebuiltLoop = (LoopExpression)transform(loop);
            Assert.AreNotSame(loop, rebuiltLoop);
            Assert.AreSame(breakLabel, rebuiltLoop.BreakLabel);
            Assert.AreSame(continueLabel, rebuiltLoop.ContinueLabel);
            Assert.AreSame(breakLabel, ((GotoExpression)rebuiltLoop.Body).Target);

            var rebuiltJump = (GotoExpression)transform(jump);
            Assert.AreNotSame(jump, rebuiltJump);
            Assert.AreEqual(GotoExpressionKind.Return, rebuiltJump.Kind);
            Assert.AreSame(returnLabel, rebuiltJump.Target);
            Assert.AreEqual(typeof(double), rebuiltJump.Type);

            var rebuiltLabel = (LabelExpression)transform(label);
            Assert.AreNotSame(label, rebuiltLabel);
            Assert.AreSame(returnLabel, rebuiltLabel.Target);

            Assert.AreSame(voidDefinition, transform(voidDefinition), "A label without default value has no child.");
            Assert.AreSame(voidJump, transform(voidJump), "A jump without value has no child.");
        }
    }

    /// <summary>
    /// <see cref="RuntimeVariablesExpression"/> references and <see cref="DebugInfoExpression"/> leaves are kept with their
    /// identity, order and metadata; a runtime variable is never turned into another expression.
    /// </summary>
    [TestMethod]
    public void RuntimeVariablesAndDebugInfo_AreExplicitPolicies()
    {
        ParameterExpression local = Expression.Variable(typeof(double), "local");
        RuntimeVariablesExpression variables = Expression.RuntimeVariables(local, P);
        SymbolDocumentInfo document = Expression.SymbolDocument("r2.cs");
        DebugInfoExpression debugInfo = Expression.DebugInfo(document, 3, 4, 3, 9);

        foreach (Func<Expression, Expression> transform in new Func<Expression, Expression>[] { e => new ExpressionSimplifier().Simplify(e), e => new DerivedSimplifier().Simplify(e), e => new RecursiveTransformer().Transform(e) })
        {
            Assert.AreSame(variables, transform(variables));
            Assert.AreSame(debugInfo, transform(debugInfo));
        }

        Assert.ThrowsExactly<InvalidCastException>(() => new ParameterReplacingTransformer().Transform(variables), "A runtime variable must stay a parameter.");
    }

    /// <summary>
    /// Declaration policy: block and catch variables, like lambda parameters, go through <c>PrepareExpression</c> and must stay
    /// <see cref="ParameterExpression"/>s; a transformer turning them into another expression fails with
    /// <see cref="InvalidCastException"/>, as it already did for lambda parameters.
    /// </summary>
    [TestMethod]
    public void Declarations_MustStayParameters_AsForLambdaParameters()
    {
        ParameterExpression local = Expression.Variable(typeof(double), "local");
        ParameterExpression exception = Expression.Variable(typeof(Exception), "ex");
        var transformer = new ParameterReplacingTransformer();

        Assert.ThrowsExactly<InvalidCastException>(() => transformer.Transform(Expression.Lambda<Func<double, double>>(P, P)));
        Assert.ThrowsExactly<InvalidCastException>(() => transformer.Transform(Expression.Block([local], Expression.Constant(1.0))));
        Assert.ThrowsExactly<InvalidCastException>(() => transformer.Transform(Expression.TryCatch(Expression.Constant(1.0), Expression.Catch(exception, Expression.Constant(2.0)))));
    }

    // ------------------------------------------------------------------------------------------
    // Subclass compatibility (C-03, H-1 to H-4).
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// H-2: the preparation order follows the structure: a block's variables, then its statements; a try's body, then each
    /// handler's variable, filter and body, then the finally; a switch's value, then each case's test values and body, then
    /// the default body.
    /// </summary>
    [TestMethod]
    public void PreparationOrder_FollowsStructuralOrder()
    {
        ParameterExpression local = Expression.Variable(typeof(double), "local");
        Expression s1 = Expression.Constant(1.5);
        Expression s2 = Expression.Constant(2.5);
        BlockExpression block = Expression.Block([local], s1, s2);

        ParameterExpression exception = Expression.Variable(typeof(Exception), "ex");
        Expression body = Expression.Constant(3.5);
        Expression filter = Expression.Constant(true);
        Expression handler = Expression.Constant(4.5);
        Expression @finally = Expression.Constant(5.5);
        TryExpression @try = Expression.TryCatchFinally(body, @finally, Expression.Catch(exception, handler, filter));

        Expression value = Expression.Constant(6);
        Expression t1 = Expression.Constant(7);
        Expression t2 = Expression.Constant(8);
        Expression caseBody = Expression.Constant(9.5);
        Expression defaultBody = Expression.Constant(10.5);
        SwitchExpression @switch = Expression.Switch(value, defaultBody, Expression.SwitchCase(caseBody, t1, t2));

        var recorder = new RecordingTransformer();
        recorder.Transform(block);
        CollectionAssert.AreEqual(new Expression[] { local, s1, s2 }, recorder.Prepared);

        recorder = new RecordingTransformer();
        recorder.Transform(@try);
        CollectionAssert.AreEqual(new Expression[] { body, exception, filter, handler, @finally }, recorder.Prepared);

        recorder = new RecordingTransformer();
        recorder.Transform(@switch);
        CollectionAssert.AreEqual(new Expression[] { value, t1, t2, caseBody, defaultBody }, recorder.Prepared);
    }

    /// <summary>
    /// H-3: <c>FinalizeExpression</c> receives the documented positional layouts: a block's statements; a try's body, filter,
    /// handler body and finally; a switch's value, test values, case body and default body; a loop's body; a jump's value; a
    /// label's default value; the runtime variables.
    /// </summary>
    [TestMethod]
    public void FinalizeExpression_ReceivesPositionalLayouts()
    {
        ParameterExpression exception = Expression.Variable(typeof(Exception), "ex");
        TryExpression @try = Expression.TryCatchFinally(Expression.Constant(1.5), Expression.Constant(2.5), Expression.Catch(exception, Expression.Constant(3.5), Expression.Constant(true)));
        SwitchExpression @switch = Expression.Switch(Expression.Constant(6), Expression.Constant(1.5), Expression.SwitchCase(Expression.Constant(2.5), Expression.Constant(7), Expression.Constant(8)));
        LabelTarget target = Expression.Label(typeof(double), "target");
        RuntimeVariablesExpression variables = Expression.RuntimeVariables(P);

        var recorder = new RecordingTransformer();
        recorder.Transform(@try);
        Expression[] tryChildren = recorder.Finalized.Single(f => f.Node is TryExpression).Children;
        CollectionAssert.AreEqual(new object?[] { 1.5, true, 3.5, 2.5 }, tryChildren.Select(c => ((ConstantExpression)c).Value).ToArray());

        recorder = new RecordingTransformer();
        recorder.Transform(@switch);
        Expression[] switchChildren = recorder.Finalized.Single(f => f.Node is SwitchExpression).Children;
        CollectionAssert.AreEqual(new object?[] { 6, 7, 8, 2.5, 1.5 }, switchChildren.Select(c => ((ConstantExpression)c).Value).ToArray());

        recorder = new RecordingTransformer();
        recorder.Transform(Expression.Block(Expression.Return(target, Expression.Constant(4.5)), Expression.Label(target, Expression.Constant(5.5))));
        Assert.AreEqual(1, recorder.Finalized.Single(f => f.Node is GotoExpression).Children.Length);
        Assert.AreEqual(1, recorder.Finalized.Single(f => f.Node is LabelExpression).Children.Length);
        Assert.AreEqual(2, recorder.Finalized.Single(f => f.Node is BlockExpression).Children.Length);

        recorder = new RecordingTransformer();
        recorder.Transform(variables);
        CollectionAssert.AreEqual(new Expression[] { P }, recorder.Finalized.Single(f => f.Node is RuntimeVariablesExpression).Children);
    }

    /// <summary>H-1: a subclass rule declared on blocks now matches with the prepared statements as positional children.</summary>
    [TestMethod]
    public void SubclassRuleOnBlock_ReceivesPreparedStatements()
    {
        var transformer = new BlockRuleTransformer();

        transformer.Transform(Expression.Block(Expression.Constant(1.0), Expression.Constant(2.0), Expression.Constant(3.0)));

        CollectionAssert.AreEqual(new[] { 3 }, transformer.Seen);
    }

    /// <summary>
    /// H-4: a transformer whose <c>PrepareExpression</c> is the identity still transforms a lambda body root, but block, catch
    /// and switch bodies are prepared through <c>PrepareExpression</c>, so they are left as they are (the lambda-body descent
    /// is not extended to the new binders).
    /// </summary>
    [TestMethod]
    public void NonRecursiveTransformer_DoesNotExtendLambdaBodyDescentToNewBinders()
    {
        var transformer = new NonRecursiveTransformer();

        var lambda = (LambdaExpression)transformer.Transform(Expression.Lambda<Func<double>>(Expression.Constant(1.0)));
        var block = (BlockExpression)transformer.Transform(Expression.Block(Expression.Constant(1.0)));
        var @try = (TryExpression)transformer.Transform(Expression.TryCatch(Expression.Constant(1.0), Expression.Catch(typeof(Exception), Expression.Constant(1.0))));

        Assert.AreEqual(2.0, ((ConstantExpression)lambda.Body).Value);
        Assert.AreEqual(1.0, ((ConstantExpression)block.Expressions[0]).Value);
        Assert.AreEqual(1.0, ((ConstantExpression)@try.Body).Value);
        Assert.AreEqual(1.0, ((ConstantExpression)@try.Handlers[0].Body).Value);
    }

    // ------------------------------------------------------------------------------------------
    // CopyExpression arity contract (C-02 extended).
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// <c>CopyExpression</c> on the R2 families: an empty list keeps the node, the documented layout rebuilds it with its
    /// metadata (label targets, handler test types and variables, comparison method, result type), and any other length is
    /// rejected; a runtime variable cannot be replaced by a non-parameter.
    /// </summary>
    [TestMethod]
    public void CopyExpression_R2Families_FollowArityContract()
    {
        Expression two = Expression.Constant(2.0);
        ParameterExpression exception = Expression.Variable(typeof(InvalidOperationException), "ex");
        TryExpression @try = Expression.TryCatchFinally(Expression.Constant(1.0), Expression.Constant(0.0), Expression.Catch(exception, Expression.Constant(1.0), Expression.Constant(true)));
        MethodInfo comparison = typeof(ExpressionTreeR2Tests).GetMethod(nameof(NearlyEqual))!;
        SwitchExpression @switch = Expression.Switch(Expression.Constant(1.0), Expression.Constant(0.0), comparison, Expression.SwitchCase(Expression.Constant(5.0), Expression.Constant(1.0)));
        LabelTarget target = Expression.Label(typeof(double), "target");
        GotoExpression jump = Expression.Return(target, Expression.Constant(1.0));
        LabelExpression label = Expression.Label(target, Expression.Constant(1.0));
        RuntimeVariablesExpression variables = Expression.RuntimeVariables(P);
        DebugInfoExpression debugInfo = Expression.DebugInfo(Expression.SymbolDocument("r2.cs"), 1, 1, 1, 2);

        foreach (Expression node in new Expression[] { @try, @switch, jump, label, variables, debugInfo })
        {
            Assert.AreSame(node, ExposedTransformer.Copy(node), node.NodeType.ToString());
        }

        var copiedTry = (TryExpression)ExposedTransformer.Copy(@try, two, Expression.Constant(false), two, two);
        Assert.AreSame(exception, copiedTry.Handlers[0].Variable);
        Assert.AreEqual(typeof(InvalidOperationException), copiedTry.Handlers[0].Test);
        Assert.AreSame(two, copiedTry.Body);
        Assert.AreSame(two, copiedTry.Finally);

        var copiedSwitch = (SwitchExpression)ExposedTransformer.Copy(@switch, two, two, two, two);
        Assert.AreSame(comparison, copiedSwitch.Comparison);
        Assert.AreSame(two, copiedSwitch.DefaultBody);

        Assert.AreSame(target, ((GotoExpression)ExposedTransformer.Copy(jump, two)).Target);
        Assert.AreSame(target, ((LabelExpression)ExposedTransformer.Copy(label, two)).Target);
        ParameterExpression other = Expression.Parameter(typeof(double), "other");
        Assert.AreSame(other, ((RuntimeVariablesExpression)ExposedTransformer.Copy(variables, other)).Variables[0]);
        Assert.AreSame(@try, ExposedTransformer.Copy(@try, @try.Body, @try.Handlers[0].Filter!, @try.Handlers[0].Body, @try.Finally!), "Unchanged children keep the node.");

        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(@try, two));
        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(@switch, two, two));
        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(jump, two, two));
        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(label, two, two));
        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(variables, two));
        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(debugInfo, two));
    }

    // ------------------------------------------------------------------------------------------
    // Executable equivalence.
    // ------------------------------------------------------------------------------------------

    /// <summary>A block with locals, writes and traced effects keeps its result, effect count and order.</summary>
    [TestMethod]
    public void Block_Execution_IsPreserved()
    {
        AssertSameBehavior(trace =>
        {
            ParameterExpression local = Expression.Variable(typeof(double), "local");
            return Expression.Block(
                [local],
                Expression.Assign(local, trace.Value("first", Leaf())),
                Expression.AddAssign(local, trace.Value("second", Expression.Multiply(local, Expression.Constant(1.0)))),
                Expression.Add(local, Expression.Constant(0.0)));
        });
    }

    /// <summary>
    /// Try/catch/filter/finally and try/fault keep their exception routing: the filter runs (and rejects or accepts), the
    /// finally and the fault run exactly when they did, and the handler result is the same.
    /// </summary>
    [TestMethod]
    public void Try_Execution_IsPreserved()
    {
        AssertSameBehavior(trace =>
        {
            ParameterExpression exception = Expression.Variable(typeof(InvalidOperationException), "ex");
            Expression body = Expression.Condition(Expression.GreaterThan(Leaf(), Expression.Constant(0.0)), trace.Throwing("body throws"), trace.Value("body", Leaf()));
            return Expression.TryCatchFinally(
                body,
                Expression.Block(typeof(void), trace.Value("finally", Leaf())),
                Expression.Catch(exception, trace.Value("rejected handler", Expression.Constant(-1.0)), trace.Filter("rejecting filter", false)),
                Expression.Catch(Expression.Variable(typeof(InvalidOperationException), "ex2"), trace.Value("handler", Expression.Add(Leaf(), Expression.Constant(100.0))), trace.Filter("accepting filter", true)));
        });

        AssertSameBehavior(trace =>
        {
            Expression body = Expression.Condition(Expression.GreaterThan(Leaf(), Expression.Constant(1.0)), trace.Throwing("body throws"), trace.Value("body", Leaf()));
            return Expression.TryCatch(
                Expression.TryFault(body, Expression.Block(typeof(void), trace.Value("fault", Leaf()))),
                Expression.Catch(typeof(InvalidOperationException), trace.Value("outer handler", Expression.Constant(7.0))));
        });
    }

    /// <summary>A switch with a comparison method keeps its case selection and the order of the traced test evaluations.</summary>
    [TestMethod]
    public void Switch_Execution_IsPreserved()
    {
        MethodInfo comparison = typeof(ExpressionTreeR2Tests).GetMethod(nameof(NearlyEqual))!;
        AssertSameBehavior(trace => Expression.Switch(
            trace.Value("value", Leaf()),
            trace.Value("default", Expression.Constant(-1.0)),
            comparison,
            Expression.SwitchCase(trace.Value("negative", Expression.Constant(1.0)), trace.Value("test -2", Expression.Constant(-2.0))),
            Expression.SwitchCase(trace.Value("zero", Expression.Constant(2.0)), trace.Value("test 0", Expression.Multiply(Expression.Constant(0.0), Expression.Constant(1.0))), trace.Value("test 3", Expression.Constant(3.0)))));
    }

    /// <summary>A loop keeps its iteration count, its exits and the order of its traced evaluations; a forward jump keeps its destination and value.</summary>
    [TestMethod]
    public void LoopAndGoto_Execution_IsPreserved()
    {
        AssertSameBehavior(trace =>
        {
            ParameterExpression counter = Expression.Variable(typeof(double), "counter");
            LabelTarget breakLabel = Expression.Label(typeof(double), "break");
            LabelTarget continueLabel = Expression.Label("continue");
            return Expression.Block(
                [counter],
                Expression.Assign(counter, Expression.Constant(0.0)),
                Expression.Loop(
                    Expression.Block(
                        Expression.IfThen(Expression.GreaterThanOrEqual(counter, Expression.Add(Leaf(), Expression.Constant(3.0))), Expression.Break(breakLabel, trace.Value("exit", counter))),
                        Expression.Assign(counter, trace.Value("step", Expression.Add(counter, Expression.Constant(1.0)))),
                        Expression.Continue(continueLabel)),
                    breakLabel,
                    continueLabel));
        });

        AssertSameBehavior(trace =>
        {
            LabelTarget exit = Expression.Label(typeof(double), "exit");
            return Expression.Block(
                Expression.IfThen(Expression.LessThan(Leaf(), Expression.Constant(0.0)), Expression.Return(exit, trace.Value("early", Expression.Multiply(Leaf(), Expression.Constant(2.0))))),
                trace.Value("skipped when early", Leaf()),
                Expression.Label(exit, trace.Value("fallthrough", Expression.Add(Leaf(), Expression.Constant(0.0)))));
        });
    }

    /// <summary>Runtime variables keep exposing the very same variables after transformation (reads and writes through the box).</summary>
    [TestMethod]
    public void RuntimeVariables_Execution_IsPreserved()
    {
        ParameterExpression local = Expression.Variable(typeof(double), "local");
        BlockExpression block = Expression.Block(
            [local],
            Expression.Assign(local, Leaf()),
            Expression.RuntimeVariables(local));

        foreach ((string name, Func<Expression, Expression> transform) in SemanticTransformers())
        {
            var transformed = (BlockExpression)transform(block);
            Assert.AreSame(local, ((RuntimeVariablesExpression)transformed.Expressions[^1]).Variables[0], name);
            IRuntimeVariables boxes = Expression.Lambda<Func<double, IRuntimeVariables>>(transformed, P).Compile()(4.0);
            Assert.AreEqual(4.0, boxes[0], name);
        }
    }
}
