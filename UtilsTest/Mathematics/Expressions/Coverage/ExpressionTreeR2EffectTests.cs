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
/// ETC R2, checkpoint D: the S4 lexical-scope hooks for blocks and catch handlers (roadmap §5.4), and the ETC-15 boundary on
/// the binding and control-flow families: structural equality of two blocks, tries, switches or loops never authorizes an
/// algebraic rewrite that would drop, repeat, merge or reorder their evaluation (C-06).
/// </summary>
[TestClass]
[TestCategory("ETC-R2")]
public class ExpressionTreeR2EffectTests
{
    /// <summary>The free parameter of the samples.</summary>
    private static readonly ParameterExpression P = Expression.Parameter(typeof(double), "p");

    /// <summary>A derived simplifier overriding nothing.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>
    /// A derived simplifier whose rule records the depth of the ambient lexical scope stack when it meets the marker constant
    /// <c>42.5</c>, and throws on the marker <c>13.5</c>.
    /// </summary>
    private sealed class ScopeProbe : ExpressionSimplifier
    {
        /// <summary>Gets the scope depths recorded at each <c>42.5</c> marker.</summary>
        public List<int> Depths { get; } = [];

        /// <summary>Records the scope depth at a marker, or throws at the failure marker.</summary>
        /// <param name="constant">The constant.</param>
        /// <returns>The constant.</returns>
        /// <exception cref="InvalidOperationException">The constant is the failure marker.</exception>
        [ExpressionSignature(ExpressionType.Constant)]
        private Expression Probe(ConstantExpression constant)
        {
            if (constant.Value is 42.5) Depths.Add(LexicalScopeDepth);
            if (constant.Value is 13.5) throw new InvalidOperationException("marker");
            return constant;
        }
    }

    /// <summary>A class whose constructor counts its calls (an allocation with an observable effect).</summary>
    public sealed class CountingBox
    {
        /// <summary>The constructor taking the value.</summary>
        public static readonly ConstructorInfo Constructor = typeof(CountingBox).GetConstructor([typeof(double)])!;

        /// <summary>The number of constructions on the current thread.</summary>
        [ThreadStatic]
        public static int Constructions;

        /// <summary>The boxed value.</summary>
        public readonly double Value;

        /// <summary>Initializes a box and counts the construction.</summary>
        /// <param name="value">The value.</param>
        public CountingBox(double value)
        {
            Constructions++;
            Value = value;
        }
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

    /// <summary>
    /// Compiles the body built for a fresh trace and its simplification (exact and derived simplifiers), runs them on several
    /// inputs and asserts identical results, exception types, traces and construction counts.
    /// </summary>
    /// <param name="build">Builds the body (a function of <see cref="P"/>) for a trace.</param>
    private static void AssertSameBehavior(Func<Trace, Expression> build)
    {
        foreach (ExpressionSimplifier simplifier in new[] { new ExpressionSimplifier(), new DerivedSimplifier() })
        {
            foreach (double input in new[] { -2.0, 0.0, 3.0 })
            {
                var originalTrace = new Trace();
                var simplifiedTrace = new Trace();
                Expression original = build(originalTrace);
                Expression simplified = simplifier.Simplify(build(simplifiedTrace));

                (object? Value, Type? Exception, int Constructions) expected = Run(original, input);
                (object? Value, Type? Exception, int Constructions) actual = Run(simplified, input);
                Assert.AreEqual(expected, actual, $"p = {input}");
                CollectionAssert.AreEqual(originalTrace.Events, simplifiedTrace.Events, $"p = {input}: [{string.Join(", ", originalTrace.Events)}] vs [{string.Join(", ", simplifiedTrace.Events)}]");
            }
        }
    }

    /// <summary>Compiles and runs <paramref name="body"/> at <paramref name="input"/>, counting the constructions of <see cref="CountingBox"/>.</summary>
    /// <param name="body">A body using <see cref="P"/>.</param>
    /// <param name="input">The value of <see cref="P"/>.</param>
    /// <returns>The result, the escaping exception type, and the number of constructions.</returns>
    private static (object? Value, Type? Exception, int Constructions) Run(Expression body, double input)
    {
        Delegate compiled = Expression.Lambda(body, P).Compile();
        CountingBox.Constructions = 0;
        try
        {
            object? value = compiled.DynamicInvoke(input);
            return (value, null, CountingBox.Constructions);
        }
        catch (TargetInvocationException exception)
        {
            return (null, exception.InnerException!.GetType(), CountingBox.Constructions);
        }
    }

    /// <summary>Counts the nodes of <paramref name="type"/> in a tree.</summary>
    /// <param name="e">The tree.</param>
    /// <param name="type">The node type.</param>
    /// <returns>The count.</returns>
    private static int Count(Expression e, ExpressionType type)
    {
        var counter = new NodeCounter(type);
        counter.Visit(e);
        return counter.Count;
    }

    /// <summary>Counts the nodes of one node type.</summary>
    private sealed class NodeCounter(ExpressionType type) : ExpressionVisitor
    {
        /// <summary>Gets the number of matching nodes.</summary>
        public int Count { get; private set; }

        /// <inheritdoc/>
        public override Expression? Visit(Expression? node)
        {
            if (node?.NodeType == type) Count++;
            return base.Visit(node);
        }
    }

    // ------------------------------------------------------------------------------------------
    // S4 scope hooks.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// The block and catch hooks feed the ambient lexical scope stack: inside a lambda, a block, a catch handler and a nested
    /// block, a term sees four open scopes; every scope is closed again once the simplification ends.
    /// </summary>
    [TestMethod]
    public void BlockAndCatchScopes_AreVisibleToTheSimplifier()
    {
        var probe = new ScopeProbe();
        ParameterExpression exception = Expression.Variable(typeof(Exception), "e");
        ParameterExpression local = Expression.Variable(typeof(double), "local");
        Expression tree = Expression.Lambda<Func<double, double>>(
            Expression.Block(
                Expression.Constant(42.5),
                Expression.TryCatch(P, Expression.Catch(exception, Expression.Block([local], Expression.Constant(42.5))))),
            P);

        probe.Simplify(tree);

        CollectionAssert.AreEqual(new[] { 2, 4 }, probe.Depths, "lambda + block, then lambda + block + catch + inner block");
        Assert.AreEqual(0, ExpressionSimplifier.LexicalScopeDepth);
    }

    /// <summary>An exception thrown by a rule inside nested block and catch scopes leaves no scope open, and the simplifier stays usable.</summary>
    [TestMethod]
    public void ScopeHooks_AreBalanced_WhenARuleThrows()
    {
        var probe = new ScopeProbe();
        ParameterExpression exception = Expression.Variable(typeof(Exception), "e");
        Expression tree = Expression.Lambda<Func<double>>(
            Expression.Block(Expression.TryCatch(Expression.Constant(1.0), Expression.Catch(exception, Expression.Block(Expression.Constant(13.5))))));

        // A rule exception is surfaced by the reflective rule invocation (historical contract), wrapped once.
        Assert.IsInstanceOfType<InvalidOperationException>(Assert.ThrowsExactly<TargetInvocationException>(() => probe.Simplify(tree)).InnerException);

        Assert.AreEqual(0, ExpressionSimplifier.LexicalScopeDepth);
        Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
        Assert.IsTrue(ExpressionComparer.Default.Equals(probe.Simplify(Expression.Add(P, Expression.Constant(0.0))), P));
    }

    /// <summary>
    /// Block-local terms are canonicalized by binding position (S4): <c>{ u = p; v = 2p; v + u }</c> orders the sum as
    /// <c>u + v</c> whatever the instances, so two alpha-equivalent blocks simplify to structurally equal blocks.
    /// </summary>
    [TestMethod]
    public void BlockLocalTerms_AreCanonicalizedByBindingPosition()
    {
        Expression Build(string first, string second)
        {
            ParameterExpression u = Expression.Variable(typeof(double), first);
            ParameterExpression v = Expression.Variable(typeof(double), second);
            return Expression.Block(
                [u, v],
                Expression.Assign(u, P),
                Expression.Assign(v, Expression.Multiply(Expression.Constant(2.0), P)),
                Expression.Add(v, u));
        }

        var first = (BlockExpression)new ExpressionSimplifier().Simplify(Build("u", "v"));
        var second = (BlockExpression)new ExpressionSimplifier().Simplify(Build("s", "t"));

        var sum = (BinaryExpression)first.Expressions[^1];
        Assert.AreSame(first.Variables[0], sum.Left, "The first declared variable sorts first.");
        Assert.AreSame(first.Variables[1], sum.Right);
        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(first, second));
        Assert.AreEqual(9.0, Expression.Lambda<Func<double, double>>(first, P).Compile()(3.0));
    }

    // ------------------------------------------------------------------------------------------
    // ETC-15 boundary on the R2 families.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Two structurally equal blocks are never merged by the additive rules, whether they hold a user call or only a parameter
    /// read: a block is observable by default (C-06), so each keeps its evaluation.
    /// </summary>
    [TestMethod]
    public void EqualBlocks_AreNeverMerged()
    {
        AssertSameBehavior(trace => Expression.Add(Expression.Block(trace.Value("first", P)), Expression.Block(trace.Value("second", P))));
        AssertSameBehavior(trace => Expression.Add(Expression.Block(trace.Value("same", P)), Expression.Block(trace.Value("same", P))));

        Expression pure = new ExpressionSimplifier().Simplify(Expression.Add(Expression.Block(P), Expression.Block(P)));
        Assert.AreEqual(2, Count(pure, ExpressionType.Block), "Two pure blocks stay two evaluations.");
    }

    /// <summary>A try/finally multiplied by zero, or subtracted from itself, keeps its body and finally evaluations.</summary>
    [TestMethod]
    public void TryFinallyAndFault_AreNeverDroppedOrCancelled()
    {
        Expression Guarded(Trace trace) => Expression.TryFinally(trace.Value("body", P), Expression.Block(typeof(void), trace.Value("finally", P)));

        AssertSameBehavior(trace => Expression.Multiply(Guarded(trace), Expression.Constant(0.0)));
        AssertSameBehavior(trace => Expression.Subtract(Guarded(trace), Guarded(trace)));
        AssertSameBehavior(trace => Expression.Multiply(
            Expression.TryCatch(
                Expression.TryFault(
                    Expression.Condition(Expression.GreaterThan(P, Expression.Constant(0.0)), Expression.Throw(Expression.New(typeof(InvalidOperationException)), typeof(double)), trace.Value("body", P)),
                    Expression.Block(typeof(void), trace.Value("fault", P))),
                Expression.Catch(typeof(InvalidOperationException), trace.Value("handler", Expression.Constant(1.0)))),
            Expression.Constant(0.0)));
    }

    /// <summary>A switch subtracted from itself keeps both evaluations and the order of its traced tests.</summary>
    [TestMethod]
    public void EqualSwitches_AreNeverCancelled()
    {
        Expression Switch(Trace trace) => Expression.Switch(
            trace.Value("value", P),
            trace.Value("default", Expression.Constant(-1.0)),
            Expression.SwitchCase(Expression.Constant(1.0), trace.Value("test 0", Expression.Constant(0.0))),
            Expression.SwitchCase(Expression.Constant(2.0), trace.Value("test 3", Expression.Constant(3.0))));

        AssertSameBehavior(trace => Expression.Subtract(Switch(trace), Switch(trace)));
    }

    /// <summary>A loop subtracted from itself, or multiplied by zero, keeps its iteration count and exits.</summary>
    [TestMethod]
    public void EqualLoops_AreNeverCancelled()
    {
        Expression Loop(Trace trace)
        {
            ParameterExpression counter = Expression.Variable(typeof(double), "counter");
            LabelTarget exit = Expression.Label(typeof(double), "exit");
            return Expression.Block(
                [counter],
                Expression.Assign(counter, Expression.Constant(0.0)),
                Expression.Loop(
                    Expression.Block(
                        Expression.IfThen(Expression.GreaterThanOrEqual(counter, Expression.Constant(3.0)), Expression.Break(exit, counter)),
                        Expression.Assign(counter, trace.Value("step", Expression.Add(counter, Expression.Constant(1.0))))),
                    exit));
        }

        AssertSameBehavior(trace => Expression.Subtract(Loop(trace), Loop(trace)));
        AssertSameBehavior(trace => Expression.Multiply(Loop(trace), Expression.Constant(0.0)));
    }

    /// <summary>A block with a jump to its result label, multiplied by zero, keeps its destination and value behavior.</summary>
    [TestMethod]
    public void JumpsAndLabels_AreNeverDropped()
    {
        Expression Jump(Trace trace)
        {
            LabelTarget exit = Expression.Label(typeof(double), "exit");
            return Expression.Block(
                Expression.IfThen(Expression.LessThan(P, Expression.Constant(0.0)), Expression.Return(exit, trace.Value("early", P))),
                Expression.Label(exit, trace.Value("late", P)));
        }

        AssertSameBehavior(trace => Expression.Multiply(Jump(trace), Expression.Constant(0.0)));
        AssertSameBehavior(trace => Expression.Add(Jump(trace), Jump(trace)));
    }

    /// <summary>Two equal allocations inside blocks are both kept (construction count), and so are two equal user calls.</summary>
    [TestMethod]
    public void AllocationsAndCallsInsideBlocks_AreNeverMerged()
    {
        Expression Box() => Expression.Field(Expression.Block(Expression.New(CountingBox.Constructor, P)), nameof(CountingBox.Value));

        AssertSameBehavior(_ => Expression.Add(Box(), Box()));
        AssertSameBehavior(_ => Expression.Subtract(Box(), Box()));
        AssertSameBehavior(trace => Expression.Subtract(Expression.Block(trace.Value("call", P)), Expression.Block(trace.Value("call", P))));
    }
}
