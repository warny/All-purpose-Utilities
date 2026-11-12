using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Tests of the <see cref="FlattenAttribute"/> infrastructure of <see cref="ExpressionTransformer"/>: discovery and validation
/// of the rules, extraction of additive and multiplicative chains from the raw tree, faithful reconstruction, invocation,
/// priority, the historical fallback when every rule declines, and the crossing boundaries.
/// </summary>
[TestClass]
public class ExpressionTransformerFlattenTests
{
    private static readonly ParameterExpression A = Expression.Parameter(typeof(double), "a");
    private static readonly ParameterExpression B = Expression.Parameter(typeof(double), "b");
    private static readonly ParameterExpression C = Expression.Parameter(typeof(double), "c");
    private static readonly ParameterExpression D = Expression.Parameter(typeof(double), "d");

    /// <summary>
    /// A transformer that prepares every child through <see cref="ExpressionTransformer.TransformCore(Expression)"/> and copies
    /// every node, logging each preparation, each signature rule and each finalization, so that the historical order of the
    /// engine's calls can be compared with a flatten-enabled subclass.
    /// </summary>
    private class LoggingTransformer : ExpressionTransformer
    {
        /// <summary>Initializes the transformer with explicit resource limits (the defaults when <see langword="null"/>).</summary>
        /// <param name="limits">The resource limits.</param>
        public LoggingTransformer(ExpressionResourceLimits? limits = null)
            : base(limits ?? ExpressionResourceLimits.Default)
        {
        }

        /// <summary>Gets the ordered log of the engine's calls.</summary>
        public List<string> Log { get; } = [];

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <summary>Exposes <see cref="ExpressionTransformer.RebuildFlattenedChain"/> to the tests.</summary>
        /// <param name="source">The root of a chain.</param>
        /// <param name="operands">The new operands.</param>
        /// <returns>The rebuilt chain.</returns>
        public Expression Rebuild(Expression source, IReadOnlyList<Expression> operands) => RebuildFlattenedChain(source, operands);

        /// <inheritdoc/>
        protected override Expression PrepareExpression(Expression e)
        {
            Log.Add("prepare " + e);
            return TransformCore(e);
        }

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
        {
            Log.Add("finalize " + e);
            return CopyExpression(e, parameters);
        }

        /// <summary>A positional signature rule on additions that logs and declines (so the node is finalized).</summary>
        /// <param name="e">The prepared node.</param>
        /// <param name="left">The prepared left operand.</param>
        /// <param name="right">The prepared right operand.</param>
        /// <returns><see langword="null"/>.</returns>
        [ExpressionSignature(ExpressionType.Add)]
        protected Expression? ClassicalAdd(BinaryExpression e, Expression left, Expression right)
        {
            Log.Add("classical " + e);
            return null;
        }
    }

    /// <summary>A flatten-enabled subclass whose additive and multiplicative rules apply a configurable behavior.</summary>
    private sealed class FlatteningTransformer : LoggingTransformer
    {
        /// <summary>Initializes the transformer with explicit resource limits (the defaults when <see langword="null"/>).</summary>
        /// <param name="limits">The resource limits.</param>
        public FlatteningTransformer(ExpressionResourceLimits? limits = null)
            : base(limits)
        {
        }

        /// <summary>Gets or sets what the additive rule returns (<see langword="null"/>: decline).</summary>
        public Func<FlatteningTransformer, Expression, FlattenedTerm[], Expression?> Additive { get; set; } = static (_, _, _) => null;

        /// <summary>Gets or sets what the multiplicative rule returns (<see langword="null"/>: decline).</summary>
        public Func<FlatteningTransformer, Expression, FlattenedTerm[], Expression?> Multiplicative { get; set; } = static (_, _, _) => null;

        /// <summary>Gets the chains each rule received: the source and a copy of the terms.</summary>
        public List<(Expression Source, FlattenedTerm[] Terms)> Received { get; } = [];

        /// <summary>The additive flatten rule.</summary>
        /// <param name="source">The root of the chain.</param>
        /// <param name="terms">The terms.</param>
        /// <returns>The configured result.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract, ExpressionType.Negate)]
        private Expression? Additions(Expression source, FlattenedTerm[] terms)
        {
            Log.Add("flatten " + source);
            Received.Add((source, (FlattenedTerm[])terms.Clone()));
            return Additive(this, source, terms);
        }

        /// <summary>The multiplicative flatten rule.</summary>
        /// <param name="source">The root of the chain.</param>
        /// <param name="terms">The terms.</param>
        /// <returns>The configured result.</returns>
        [Flatten(ExpressionType.Multiply, ExpressionType.Divide)]
        private Expression? Multiplications(Expression source, FlattenedTerm[] terms)
        {
            Log.Add("flatten " + source);
            Received.Add((source, (FlattenedTerm[])terms.Clone()));
            return Multiplicative(this, source, terms);
        }
    }

    /// <summary>Renders terms as <c>+a -b *c /d</c>.</summary>
    /// <param name="terms">The terms.</param>
    /// <returns>The rendering.</returns>
    private static string Render(IEnumerable<FlattenedTerm> terms)
        => string.Join(" ", terms.Select(static term => term.Operation switch
        {
            ExpressionType.Add => "+",
            ExpressionType.Subtract => "-",
            ExpressionType.Multiply => "*",
            ExpressionType.Divide => "/",
            _ => "?",
        } + term.Operand));

    /// <summary>The additive cases of the mission, with their expected terms.</summary>
    /// <returns>The cases.</returns>
    private static IEnumerable<(string Name, Expression Expression, string Terms)> AdditiveCases()
    {
        yield return ("a + b", Expression.Add(A, B), "+a +b");
        yield return ("(a + b) + c", Expression.Add(Expression.Add(A, B), C), "+a +b +c");
        yield return ("a + (b + c)", Expression.Add(A, Expression.Add(B, C)), "+a +b +c");
        yield return ("a - b", Expression.Subtract(A, B), "+a -b");
        yield return ("a - (b - c)", Expression.Subtract(A, Expression.Subtract(B, C)), "+a -b +c");
        yield return ("a - (b + c)", Expression.Subtract(A, Expression.Add(B, C)), "+a -b -c");
        yield return ("-(a + b)", Expression.Negate(Expression.Add(A, B)), "-a -b");
        yield return ("-(-a)", Expression.Negate(Expression.Negate(A)), "+a");
        yield return ("a + (b - (c + d))", Expression.Add(A, Expression.Subtract(B, Expression.Add(C, D))), "+a +b -c -d");
        yield return ("a - (b - (c - d))", Expression.Subtract(A, Expression.Subtract(B, Expression.Subtract(C, D))), "+a -b +c -d");
        yield return ("-(a + b - c)", Expression.Negate(Expression.Subtract(Expression.Add(A, B), C)), "-a -b +c");
    }

    /// <summary>The multiplicative cases of the mission, with their expected terms.</summary>
    /// <returns>The cases.</returns>
    private static IEnumerable<(string Name, Expression Expression, string Terms)> MultiplicativeCases()
    {
        yield return ("a * b", Expression.Multiply(A, B), "*a *b");
        yield return ("(a * b) * c", Expression.Multiply(Expression.Multiply(A, B), C), "*a *b *c");
        yield return ("a * (b * c)", Expression.Multiply(A, Expression.Multiply(B, C)), "*a *b *c");
        yield return ("a / b", Expression.Divide(A, B), "*a /b");
        yield return ("a / (b / c)", Expression.Divide(A, Expression.Divide(B, C)), "*a /b *c");
        yield return ("(a / b) * c", Expression.Multiply(Expression.Divide(A, B), C), "*a /b *c");
        yield return ("(a / b) / (c / d)", Expression.Divide(Expression.Divide(A, B), Expression.Divide(C, D)), "*a /b /c *d");
        yield return ("(a * b) / (c * d)", Expression.Divide(Expression.Multiply(A, B), Expression.Multiply(C, D)), "*a *b /c /d");
        yield return ("a * (b / (c * d))", Expression.Multiply(A, Expression.Divide(B, Expression.Multiply(C, D))), "*a *b /c /d");
    }

    /// <summary>
    /// Every additive and multiplicative case is received as one chain: the expected terms, in source order, with explicit
    /// operations, each operand the very parameter of the source, consecutive source indices, and the original root as source.
    /// The rule that accepts the chain is the only call of the engine: no operand is prepared.
    /// </summary>
    [TestMethod]
    public void Chains_AreReceivedWhole_WithPropagatedOperations_InSourceOrder()
    {
        foreach ((string name, Expression expression, string expected) in AdditiveCases().Concat(MultiplicativeCases()))
        {
            var transformer = new FlatteningTransformer
            {
                Additive = static (_, _, _) => Expression.Constant(0.0),
                Multiplicative = static (_, _, _) => Expression.Constant(0.0),
            };

            Expression result = transformer.Transform(expression);

            Assert.AreEqual(0.0, ((ConstantExpression)result).Value, name);
            Assert.AreEqual(1, transformer.Received.Count, name);
            (Expression source, FlattenedTerm[] terms) = transformer.Received[0];
            Assert.AreSame(expression, source, name);
            Assert.AreEqual(expected, Render(terms), name);
            for (int i = 0; i < terms.Length; i++)
            {
                Assert.AreEqual(i, terms[i].SourceIndex, name);
                Assert.IsTrue(new[] { A, B, C, D }.Any(p => ReferenceEquals(p, terms[i].Operand)), name);
            }

            CollectionAssert.AreEqual(new[] { "flatten " + expression }, transformer.Log, name);
        }
    }

    /// <summary>
    /// The faithful reconstruction rebuilds the source grouping, not one derived from the signs: <c>(a / b) * c</c> and
    /// <c>a / (b / c)</c> share their terms but keep their own parentheses; with the original operands the source itself is
    /// returned; operators, result types and node types are kept.
    /// </summary>
    [TestMethod]
    public void Rebuild_KeepsTheSourceGrouping_AndReturnsTheSourceWhenUnchanged()
    {
        ParameterExpression[] renamed = [.. "ABCD".Select(static n => Expression.Parameter(typeof(double), n.ToString()))];
        foreach ((string name, Expression expression, _) in AdditiveCases().Concat(MultiplicativeCases()))
        {
            Expression? rebuilt = null;
            Expression? identity = null;
            var transformer = new FlatteningTransformer();
            Expression? Rebuild(FlatteningTransformer t, Expression source, FlattenedTerm[] terms)
            {
                identity = t.Rebuild(source, [.. terms.Select(static term => term.Operand)]);
                rebuilt = t.Rebuild(source, [.. terms.Select(term => (Expression)renamed[System.Array.IndexOf(new[] { A, B, C, D }, (ParameterExpression)term.Operand)])]);
                return source;
            }

            transformer.Additive = Rebuild;
            transformer.Multiplicative = Rebuild;
            transformer.Transform(expression);

            Assert.AreSame(expression, identity, name);
            Assert.IsNotNull(rebuilt, name);
            Assert.AreEqual(expression.ToString().ToUpperInvariant(), rebuilt.ToString(), name);
            Assert.AreEqual(expression.NodeType, rebuilt.NodeType, name);
            Assert.AreEqual(expression.Type, rebuilt.Type, name);
        }
    }

    /// <summary>The reconstruction rejects a node that is not the root of one of the transformer's chains, and a wrong operand count.</summary>
    [TestMethod]
    public void Rebuild_RejectsANonChainRoot_AndAWrongOperandCount()
    {
        var transformer = new FlatteningTransformer();
        Assert.ThrowsExactly<ArgumentException>(() => transformer.Rebuild(Expression.Power(A, B), [A, B]));
        Assert.ThrowsExactly<ArgumentException>(() => transformer.Rebuild(Expression.Add(A, B), [A]));
        Assert.ThrowsExactly<ArgumentException>(() => new LoggingTransformer().Rebuild(Expression.Add(A, B), [A, B]));
    }

    /// <summary>
    /// When every flatten rule declines, the transformer behaves exactly as without flatten rules: the same preparations, the
    /// same signature rules and finalizations in the same order (the flatten invocation aside), and the same result. The
    /// chain is extracted once, at its root: the fallback never re-extracts its sub-chains.
    /// </summary>
    [TestMethod]
    public void DecliningRules_KeepTheHistoricalOrderOfCalls_AndAreInvokedOncePerChain()
    {
        Expression[] cases =
        [
            Expression.Add(Expression.Add(A, B), Expression.Subtract(C, Expression.Negate(D))),
            Expression.Divide(Expression.Multiply(A, Expression.Divide(B, C)), D),
            Expression.Add(A, Expression.Multiply(B, Expression.Add(C, D))),
            Expression.Call(typeof(Math).GetMethod(nameof(Math.Abs), [typeof(double)])!, Expression.Add(A, Expression.Add(B, C))),
        ];
        int[] expectedChains = [1, 1, 3, 1];

        for (int i = 0; i < cases.Length; i++)
        {
            var historical = new LoggingTransformer();
            var flattening = new FlatteningTransformer();

            Expression expected = historical.Transform(cases[i]);
            Expression actual = flattening.Transform(cases[i]);

            Assert.AreEqual(expected.ToString(), actual.ToString(), cases[i].ToString());
            CollectionAssert.AreEqual(historical.Log, flattening.Log.Where(static line => !line.StartsWith("flatten ", StringComparison.Ordinal)).ToList(), cases[i].ToString());
            Assert.AreEqual(expectedChains[i], flattening.Received.Count, cases[i].ToString());
        }
    }

    /// <summary>
    /// A declined chain whose operand holds another chain: the inner chain is a chain of its own, offered to the rules when
    /// the historical traversal reaches it (and accepted there), while the declined chain's own nodes are never offered again.
    /// </summary>
    [TestMethod]
    public void DeclinedChain_StillOffersTheChainsOfItsOperands()
    {
        MethodInfo abs = typeof(Math).GetMethod(nameof(Math.Abs), [typeof(double)])!;
        Expression inner = Expression.Add(B, C);
        Expression outer = Expression.Add(A, Expression.Subtract(Expression.Call(abs, inner), D));
        var transformer = new FlatteningTransformer
        {
            Additive = (_, source, _) => ReferenceEquals(source, inner) ? Expression.Constant(42.0) : null,
        };

        Expression result = transformer.Transform(outer);

        Assert.AreEqual(2, transformer.Received.Count);
        Assert.AreSame(outer, transformer.Received[0].Source);
        Assert.AreSame(inner, transformer.Received[1].Source);
        Assert.AreEqual("(a + (Abs(42) - d))", result.ToString());
    }

    /// <summary>
    /// Documented limit of the fallback: while a declined chain is processed historically, its crossed nodes are recognized by
    /// reference, so another occurrence of the very same node object under one of its operands (<c>s + Abs(s)</c>) is
    /// processed historically too instead of being offered as a chain; a structurally equal but distinct node is offered.
    /// </summary>
    [TestMethod]
    public void DeclinedChain_SharedCrossedNode_IsNotOfferedAgainDuringTheFallback()
    {
        MethodInfo abs = typeof(Math).GetMethod(nameof(Math.Abs), [typeof(double)])!;
        Expression shared = Expression.Add(A, B);

        var sharing = new FlatteningTransformer();
        sharing.Transform(Expression.Add(shared, Expression.Call(abs, shared)));
        Assert.AreEqual(1, sharing.Received.Count);

        var distinct = new FlatteningTransformer();
        distinct.Transform(Expression.Add(shared, Expression.Call(abs, Expression.Add(A, B))));
        Assert.AreEqual(2, distinct.Received.Count);

        // After the fallback the marks are gone: the shared node is a chain root again.
        sharing.Received.Clear();
        sharing.Transform(shared);
        Assert.AreEqual(1, sharing.Received.Count);
    }

    /// <summary>
    /// An accepting rule receives the raw operands and is responsible for them: transforming each one with
    /// <c>TransformCore</c> prepares it exactly once, in the order the rule chooses, and the faithful rebuild keeps the
    /// grouping.
    /// </summary>
    [TestMethod]
    public void AcceptingRule_OwnsItsOperands()
    {
        Expression expression = Expression.Subtract(Expression.Add(A, B), Expression.Add(C, D));
        var transformer = new FlatteningTransformer
        {
            Additive = static (t, source, terms) =>
                t.Rebuild(source, [.. terms.Select(term => (Expression)Expression.Multiply(term.Operand, Expression.Constant(2.0)))]),
        };

        Expression result = transformer.Transform(expression);

        Assert.AreEqual("(((a * 2) + (b * 2)) - ((c * 2) + (d * 2)))", result.ToString());
        CollectionAssert.AreEqual(new[] { "flatten " + expression }, transformer.Log);
    }

    /// <summary>
    /// The flatten rules of a chain root run before any signature rule of the chain: a signature rule on the root is reached
    /// only through the fallback, after every flatten rule declined.
    /// </summary>
    [TestMethod]
    public void FlattenRules_RunBeforeTheSignatureRulesOfTheChain()
    {
        Expression expression = Expression.Add(A, B);
        var declining = new FlatteningTransformer();
        declining.Transform(expression);
        Assert.AreEqual("flatten " + expression, declining.Log[0]);
        CollectionAssert.Contains(declining.Log, "classical " + expression);

        var accepting = new FlatteningTransformer { Additive = static (_, source, _) => source };
        accepting.Transform(expression);
        CollectionAssert.DoesNotContain(accepting.Log, "classical " + expression);
    }

    /// <summary>A flatten-only transformer with two rules on the same operators, in declaration order.</summary>
    private sealed class TwoRulesTransformer : ExpressionTransformer
    {
        /// <summary>Gets the order of the calls.</summary>
        public List<string> Calls { get; } = [];

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => CopyExpression(e, parameters);

        /// <summary>Declines after overwriting and reordering its array.</summary>
        /// <param name="terms">The terms.</param>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract)]
        private Expression? First(FlattenedTerm[] terms)
        {
            Calls.Add("first " + Render(terms));
            System.Array.Reverse(terms);
            terms[0] = new FlattenedTerm(ExpressionType.Subtract, Expression.Constant(-1.0), 99);
            return null;
        }

        /// <summary>Accepts, returning the rendering of the terms it received.</summary>
        /// <param name="source">The root.</param>
        /// <param name="terms">The terms.</param>
        /// <returns>A constant holding the rendering.</returns>
        [Flatten(ExpressionType.Subtract, ExpressionType.Add)]
        private Expression? Second(Expression source, FlattenedTerm[] terms)
        {
            Calls.Add("second " + Render(terms));
            return Expression.Constant(Render(terms));
        }
    }

    /// <summary>
    /// Several rules of a group are tried in declaration order; a declining rule's mutations of its array never reach the
    /// next rule, which receives the pristine terms (operator sets compare as sets, whatever their declaration order).
    /// </summary>
    [TestMethod]
    public void SeveralRules_AreTriedInOrder_WithAPristineCopyEach()
    {
        var transformer = new TwoRulesTransformer();

        Expression result = transformer.Transform(Expression.Subtract(A, Expression.Add(B, C)));

        CollectionAssert.AreEqual(new[] { "first +a -b -c", "second +a -b -c" }, transformer.Calls);
        Assert.AreEqual("+a -b -c", ((ConstantExpression)result).Value);
    }

    /// <summary>A transformer whose additive rule throws.</summary>
    private sealed class ThrowingTransformer : LoggingTransformer
    {
        /// <summary>Always throws.</summary>
        /// <param name="terms">The terms.</param>
        /// <returns>Never returns.</returns>
        [Flatten(ExpressionType.Add)]
        private Expression? Additions(FlattenedTerm[] terms) => throw new InvalidOperationException("boom");
    }

    /// <summary>
    /// An exception of a flatten rule is wrapped like the exception of a signature rule (a new
    /// <see cref="TargetInvocationException"/>) and no operand was prepared before the rule ran.
    /// </summary>
    [TestMethod]
    public void ThrowingRule_IsWrappedLikeASignatureRule_AndNothingWasPreparedBefore()
    {
        var transformer = new ThrowingTransformer();

        TargetInvocationException exception = Assert.ThrowsExactly<TargetInvocationException>(() => transformer.Transform(Expression.Add(A, B)));

        Assert.IsInstanceOfType<InvalidOperationException>(exception.InnerException);
        Assert.AreEqual("boom", exception.InnerException!.Message);
        Assert.AreEqual(0, transformer.Log.Count);
    }

    /// <summary>A transformer that only flattens additions (not subtractions).</summary>
    private sealed class AdditionsOnlyTransformer : LoggingTransformer
    {
        /// <summary>Gets the chains received.</summary>
        public List<string> Received { get; } = [];

        /// <summary>Records and declines.</summary>
        /// <param name="terms">The terms.</param>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Add)]
        private Expression? Additions(FlattenedTerm[] terms)
        {
            Received.Add(Render(terms));
            return null;
        }
    }

    /// <summary>
    /// A chain only crosses the declared operators: with only <c>Add</c> declared, a subtraction is an operand, so the root
    /// <c>a - (b + c)</c> is not a chain and its operand <c>b + c</c> is one (offered again when the historical fallback of
    /// the enclosing chain reaches it, since it is a chain of its own).
    /// </summary>
    [TestMethod]
    public void UndeclaredOperators_AreOperands()
    {
        var transformer = new AdditionsOnlyTransformer();

        transformer.Transform(Expression.Add(Expression.Subtract(A, Expression.Add(B, C)), D));
        CollectionAssert.AreEqual(new[] { "+(a - (b + c)) +d", "+b +c" }, transformer.Received);

        transformer.Received.Clear();
        transformer.Transform(Expression.Subtract(A, Expression.Add(B, C)));
        CollectionAssert.AreEqual(new[] { "+b +c" }, transformer.Received);
    }

    /// <summary>A user-defined value type with an addition operator.</summary>
    /// <param name="Value">The value.</param>
    private readonly record struct Money(double Value)
    {
        /// <summary>Adds two amounts.</summary>
        /// <param name="left">The left amount.</param>
        /// <param name="right">The right amount.</param>
        /// <returns>The sum.</returns>
        public static Money operator +(Money left, Money right) => new(left.Value + right.Value);
    }

    /// <summary>
    /// The default boundaries: an operator with a method (user-defined or <see langword="decimal"/>), a lifted (nullable)
    /// operator and a checked one are never crossed, so a chain rooted there does not exist and such a node inside a chain is
    /// an operand, like a conversion, a call, an invocation of a lambda or a power. Integer division is crossed: the terms are
    /// a symbolic view, and the grouping stays available through the faithful reconstruction.
    /// </summary>
    [TestMethod]
    public void DefaultBoundaries_CrossOnlyOrdinaryIntrinsicOperators()
    {
        ParameterExpression m1 = Expression.Parameter(typeof(Money), "m1");
        ParameterExpression m2 = Expression.Parameter(typeof(Money), "m2");
        ParameterExpression d1 = Expression.Parameter(typeof(decimal), "d1");
        ParameterExpression d2 = Expression.Parameter(typeof(decimal), "d2");
        ParameterExpression n1 = Expression.Parameter(typeof(double?), "n1");
        ParameterExpression n2 = Expression.Parameter(typeof(double?), "n2");
        ParameterExpression i1 = Expression.Parameter(typeof(int), "i1");
        ParameterExpression i2 = Expression.Parameter(typeof(int), "i2");
        ParameterExpression i3 = Expression.Parameter(typeof(int), "i3");
        LambdaExpression addB = Expression.Lambda(Expression.Add(B, Expression.Constant(1.0)), B);
        MethodInfo abs = typeof(Math).GetMethod(nameof(Math.Abs), [typeof(double)])!;

        (Expression Expression, string[] Received)[] cases =
        [
            (Expression.Add(m1, m2), []),
            (Expression.Add(d1, d2), []),
            (Expression.Add(n1, n2), []),
            (Expression.AddChecked(A, B), []),
            (Expression.Add(A, Expression.AddChecked(B, C)), ["+a +(b + c)"]),
            (Expression.Add(A, Expression.Convert(i1, typeof(double))), ["+a +Convert(i1, Double)"]),
            (Expression.Add(A, Expression.Call(abs, Expression.Add(B, C))), ["+a +Abs((b + c))", "+b +c"]),
            (Expression.Add(A, Expression.Invoke(addB, C)), ["+a +Invoke(b => (b + 1), c)", "+b +1"]),
            (Expression.Add(A, Expression.Power(B, C)), ["+a +(b ** c)"]),
            (Expression.Divide(i1, Expression.Divide(i2, i3)), ["*i1 /i2 *i3"]),
        ];

        foreach ((Expression expression, string[] expected) in cases)
        {
            var transformer = new FlatteningTransformer();
            transformer.Transform(expression);
            CollectionAssert.AreEqual(expected, transformer.Received.Select(static r => Render(r.Terms)).ToArray(), expression.ToString());
        }
    }

    /// <summary>
    /// A shared sub-tree keeps every occurrence: <c>s + s</c> with <c>s = a + b</c> gives four terms whose operands are the
    /// very shared parameters, and a deep left, right and alternating chain of 4,096 operands is one chain of 4,096 terms.
    /// </summary>
    [TestMethod]
    public void SharedAndDeepChains_KeepEveryOccurrence()
    {
        Expression shared = Expression.Add(A, B);
        var transformer = new FlatteningTransformer { Additive = static (_, source, _) => source };
        transformer.Transform(Expression.Add(shared, shared));
        Assert.AreEqual("+a +b +a +b", Render(transformer.Received[0].Terms));
        Assert.AreSame(A, transformer.Received[0].Terms[2].Operand);

        const int Count = 4096;
        Expression left = A;
        Expression right = A;
        Expression alternating = A;
        for (int i = 1; i < Count; i++)
        {
            left = Expression.Add(left, B);
            right = Expression.Add(B, right);
            alternating = i % 2 == 0 ? Expression.Add(alternating, B) : Expression.Subtract(B, alternating);
        }

        foreach (Expression chain in new[] { left, right, alternating })
        {
            var deep = new FlatteningTransformer(ExpressionResourceLimits.Unlimited) { Additive = static (_, _, _) => Expression.Constant(0.0) };
            deep.Transform(chain);
            Assert.AreEqual(1, deep.Received.Count);
            Assert.AreEqual(Count, deep.Received[0].Terms.Length);
        }
    }

    /// <summary>Base type declaring a virtual flatten rule.</summary>
    private abstract class BaseWithVirtualRule : ExpressionTransformer
    {
        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => CopyExpression(e, parameters);

        /// <summary>The base rule.</summary>
        /// <param name="terms">The terms.</param>
        /// <returns>A constant.</returns>
        [Flatten(ExpressionType.Add)]
        protected virtual Expression? Additions(FlattenedTerm[] terms) => Expression.Constant("base");
    }

    /// <summary>Overrides the rule without repeating the attribute.</summary>
    private sealed class DerivedOverride : BaseWithVirtualRule
    {
        /// <inheritdoc/>
        protected override Expression? Additions(FlattenedTerm[] terms) => Expression.Constant("derived " + terms.Length);
    }

    /// <summary>The attribute is inherited by an override, which is the rule invoked (once).</summary>
    [TestMethod]
    public void InheritedAttribute_MakesTheOverrideARule()
    {
        Assert.AreEqual("derived 3", ((ConstantExpression)new DerivedOverride().Transform(Expression.Add(Expression.Add(A, B), C))).Value);
    }

    /// <summary>A transformer with an overloaded name shared by a signature rule and a flatten rule.</summary>
    private sealed class OverloadedTransformer : ExpressionTransformer
    {
        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => CopyExpression(e, parameters);

        /// <summary>The signature rule.</summary>
        /// <param name="e">The node.</param>
        /// <returns>The node.</returns>
        [ExpressionSignature(ExpressionType.Power)]
        private Expression? Rule(BinaryExpression e) => e;

        /// <summary>The flatten rule.</summary>
        /// <param name="terms">The terms.</param>
        /// <returns>The source rebuilt.</returns>
        [Flatten(ExpressionType.Multiply, ExpressionType.Divide)]
        private Expression? Rule(FlattenedTerm[] terms) => Expression.Constant(terms.Length);
    }

    /// <summary>
    /// A detailed trace reports the extraction, the flatten rule as a candidate and as applied, with an identity that lists
    /// its operators and completes the overloaded name with the parameter types.
    /// </summary>
    [TestMethod]
    public void DetailedTrace_ReportsTheExtractionAndTheFlattenRule()
    {
        var collector = new ExpressionTraceCollector();
        Expression expression = Expression.Divide(A, Expression.Multiply(B, C));
        using (ExpressionTransformTracing.Begin(collector, ExpressionTraceDetail.Detailed))
        {
            new OverloadedTransformer().Transform(expression);
        }

        ExpressionTransformEvent[] events = collector.GetEvents();
        ExpressionTransformEvent flattened = events.Single(static e => e.Kind == ExpressionTransformEventKind.ChainFlattened);
        Assert.AreSame(expression, flattened.Input);
        ExpressionTransformEvent applied = events.Single(static e => e.Kind == ExpressionTransformEventKind.RuleApplied);
        Assert.AreEqual("Rule(FlattenedTerm[])", applied.Rule!.DisplayName);
        CollectionAssert.AreEqual(new[] { ExpressionType.Multiply, ExpressionType.Divide }, applied.Rule.FlattenedNodeTypes.ToArray());
        Assert.AreEqual(ExpressionType.Multiply, applied.Rule.DeclaredNodeType);
        Assert.IsTrue(events.Any(static e => e.Kind == ExpressionTransformEventKind.RuleCandidate && e.Rule!.FlattenedNodeTypes.Count == 2));
        Assert.AreEqual(ExpressionTransformOutcome.RuleApplied, events.Last(static e => e.Kind == ExpressionTransformEventKind.TransformExit).Outcome);
    }

    /// <summary>A declined rule is reported as rejected (Declined) and the historical events follow in the same treatment.</summary>
    [TestMethod]
    public void DetailedTrace_ReportsADeclinedRule_ThenTheHistoricalTreatment()
    {
        var collector = new ExpressionTraceCollector();
        using (ExpressionTransformTracing.Begin(collector, ExpressionTraceDetail.Detailed))
        {
            new FlatteningTransformer().Transform(Expression.Add(A, B));
        }

        ExpressionTransformEvent[] events = collector.GetEvents();
        ExpressionTransformEvent rejected = events.First(static e => e.Kind == ExpressionTransformEventKind.RuleRejected);
        Assert.AreEqual(ExpressionRuleRejection.Declined, rejected.Rejection);
        Assert.AreEqual("Additions", rejected.Rule!.Name);
        Assert.IsTrue(events.Any(e => e.Kind == ExpressionTransformEventKind.Finalize && e.OperationId == rejected.OperationId));
    }

    /// <summary>Invalid flatten rules: each type fails its construction with an explicit message.</summary>
    private sealed class NoOperator : FlattenProbe
    {
        /// <summary>The invalid rule of this probe.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten]
        private Expression? R(FlattenedTerm[] terms) => null;
    }

    /// <summary>An operator that cannot be flattened.</summary>
    private sealed class PowerOperator : FlattenProbe
    {
        /// <summary>The invalid rule of this probe.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Power)]
        private Expression? R(FlattenedTerm[] terms) => null;
    }

    /// <summary>Both families in one rule.</summary>
    private sealed class MixedFamilies : FlattenProbe
    {
        /// <summary>The invalid rule of this probe.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Multiply)]
        private Expression? R(FlattenedTerm[] terms) => null;
    }

    /// <summary>Two rules whose sets overlap without being equal.</summary>
    private sealed class OverlappingSets : FlattenProbe
    {
        /// <summary>The invalid rule of this probe.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract)]
        private Expression? R1(FlattenedTerm[] terms) => null;

        /// <summary>The invalid rule of this probe.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Add)]
        private Expression? R2(FlattenedTerm[] terms) => null;
    }

    /// <summary>A wrong parameter list.</summary>
    private sealed class WrongParameters : FlattenProbe
    {
        /// <summary>The invalid rule of this probe.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Add)]
        private Expression? R(Expression[] terms) => null;
    }

    /// <summary>A source typed more narrowly than <see cref="Expression"/>.</summary>
    private sealed class NarrowSource : FlattenProbe
    {
        /// <summary>The invalid rule of this probe.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Add)]
        private Expression? R(BinaryExpression source, FlattenedTerm[] terms) => null;
    }

    /// <summary>A rule that does not return an expression.</summary>
    private sealed class WrongReturn : FlattenProbe
    {
        /// <summary>The invalid rule of this probe.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Add)]
        private object? R(FlattenedTerm[] terms) => null;
    }

    /// <summary>A generic rule.</summary>
    private sealed class GenericRule : FlattenProbe
    {
        /// <summary>The invalid rule of this probe.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Add)]
        private Expression? R<T>(FlattenedTerm[] terms) => null;
    }

    /// <summary>A static rule.</summary>
    private sealed class StaticRule : FlattenProbe
    {
        /// <summary>The invalid rule of this probe.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Add)]
        private static Expression? R(FlattenedTerm[] terms) => null;
    }

    /// <summary>A method with both attributes.</summary>
    private sealed class BothAttributes : FlattenProbe
    {
        /// <summary>The invalid rule of this probe.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Add)]
        [ExpressionSignature(ExpressionType.Add)]
        private Expression? R(FlattenedTerm[] terms) => null;
    }

    /// <summary>A parameter constraint on a flatten rule.</summary>
    private sealed class ConstrainedParameter : FlattenProbe
    {
        /// <summary>The invalid rule of this probe.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Add)]
        private Expression? R([ExpressionSignature(ExpressionType.Add)] Expression source, FlattenedTerm[] terms) => null;
    }

    /// <summary>Minimal base of the invalid probes.</summary>
    private abstract class FlattenProbe : ExpressionTransformer
    {
        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);
    }

    /// <summary>Every invalid rule shape fails the construction of its transformer, every time, with a message naming the rule.</summary>
    [TestMethod]
    public void InvalidRules_FailTheConstruction_WithAnExplicitMessage()
    {
        (Func<object> Create, string Reason)[] cases =
        [
            (() => new NoOperator(), "declares no operator"),
            (() => new PowerOperator(), "'Power' cannot be flattened"),
            (() => new MixedFamilies(), "mixes additive"),
            (() => new OverlappingSets(), "overlap"),
            (() => new WrongParameters(), "its parameters must be"),
            (() => new NarrowSource(), "its parameters must be"),
            (() => new WrongReturn(), "must return an Expression"),
            (() => new GenericRule(), "cannot be generic"),
            (() => new StaticRule(), "instance method"),
            (() => new BothAttributes(), "also marked with an ExpressionSignatureAttribute"),
            (() => new ConstrainedParameter(), "cannot carry an ExpressionSignatureAttribute"),
        ];

        foreach ((Func<object> create, string reason) in cases)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() => create());
                StringAssert.Contains(exception.Message, reason);
                StringAssert.Contains(exception.Message, "[Flatten] rule");
            }
        }
    }

    /// <summary>A type built for the first time by concurrent constructions.</summary>
    private sealed class ConcurrentlyBuilt : ExpressionTransformer
    {
        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <summary>Counts the terms.</summary>
        /// <param name="terms">The terms.</param>
        /// <returns>The count.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract, ExpressionType.Negate)]
        private Expression? R(FlattenedTerm[] terms) => Expression.Constant(terms.Length);
    }

    /// <summary>Concurrent first constructions of a type build a working plan for every instance.</summary>
    [TestMethod]
    public void ConcurrentFirstConstructions_AllWork()
    {
        Expression expression = Expression.Subtract(A, Expression.Add(B, Expression.Negate(C)));
        object[] results = new object[64];
        Parallel.For(0, results.Length, i => results[i] = ((ConstantExpression)new ConcurrentlyBuilt().Transform(expression)).Value!);
        Assert.IsTrue(results.All(static r => (int)r == 3));
    }

    /// <summary>The attribute keeps a private copy of its operators.</summary>
    [TestMethod]
    public void Attribute_CopiesItsOperators()
    {
        ExpressionType[] operations = [ExpressionType.Add, ExpressionType.Subtract];
        var attribute = new FlattenAttribute(operations);
        operations[0] = ExpressionType.Power;
        CollectionAssert.AreEqual(new[] { ExpressionType.Add, ExpressionType.Subtract }, attribute.Operations.ToArray());
        Assert.AreEqual(0, new FlattenAttribute(null!).Operations.Count);
    }
}
