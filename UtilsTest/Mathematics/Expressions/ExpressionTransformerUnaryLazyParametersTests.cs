using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Characterization and regression tests for <see cref="ExpressionTransformer"/>'s handling of
/// <see cref="UnaryExpression"/> parameters, mirroring
/// <see cref="ExpressionTransformerBinaryLazyParametersTests"/> for the follow-up optimization that
/// removes <c>PrepareUnary</c>'s unconditional <c>Expression[1]</c> allocation. These tests pin down
/// every externally observable behavior that must be preserved once the single logical parameter is
/// materialized into a real array only when an <c>Expression[]</c>-shaped rule or
/// <see cref="ExpressionTransformer.FinalizeExpression"/> actually requires one: preparation order, the
/// exact array a rule or <c>FinalizeExpression</c> receives (contents, independence from the prepared
/// node, and — for the <c>Expression[]</c>-shaped rule overload — runtime array type), which invocation
/// paths never require an array at all, and exact reconstruction fidelity against the historical
/// <see cref="ExpressionTransformer.CopyExpression"/> oracle (including quirks such as dropping an
/// explicit <see cref="UnaryExpression.Method"/> or a typed <c>Throw</c>'s declared
/// <see cref="Expression.Type"/> that this performance change deliberately does not fix). Allocation
/// counts are intentionally verified by a separate benchmark, not by these unit tests, which assert only
/// observable object identity, array contents, and control flow.
/// </summary>
[TestClass]
public class ExpressionTransformerUnaryLazyParametersTests
{
    // ---------------------------------------------------------------------------------------------
    // Test A — positional unary rule observes the prepared operand.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A transformer whose <see cref="ExpressionTransformer.PrepareExpression"/> override substitutes a
    /// known original operand with a known replacement instance, so a rule's captured argument can be
    /// checked against that replacement by reference.
    /// </summary>
    private sealed class PreparedOperandObservingTransformer : ExpressionTransformer
    {
        private readonly Expression _original;
        private readonly Expression _replacement;

        /// <summary>Initializes the transformer with the original operand and its replacement.</summary>
        /// <param name="original">The original operand instance to substitute.</param>
        /// <param name="replacement">The replacement instance <paramref name="original"/> is substituted with.</param>
        public PreparedOperandObservingTransformer(Expression original, Expression replacement)
        {
            _original = original;
            _replacement = replacement;
        }

        /// <summary>The rebuilt unary node the rule was invoked with.</summary>
        public UnaryExpression? CapturedNode { get; private set; }

        /// <summary>The <c>operand</c> argument the rule was invoked with.</summary>
        public Expression? CapturedOperand { get; private set; }

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <inheritdoc />
        protected override Expression PrepareExpression(Expression e)
            => ReferenceEquals(e, _original) ? _replacement : e;

        /// <summary>Matches any <see cref="ExpressionType.Negate"/> node and captures the node and operand it was invoked with.</summary>
        [ExpressionSignature(ExpressionType.Negate)]
        private Expression Rule(UnaryExpression e, Expression operand)
        {
            CapturedNode = e;
            CapturedOperand = operand;
            return e;
        }
    }

    /// <summary>
    /// A positional unary rule must observe the prepared operand (the substitution
    /// <see cref="ExpressionTransformer.PrepareExpression"/> returns), not the original one, and the
    /// rebuilt node's own <c>Operand</c> must be that same prepared instance.
    /// </summary>
    [TestMethod]
    public void PositionalUnaryRule_ObservesPreparedOperand()
    {
        ParameterExpression original = Expression.Parameter(typeof(double), "x");
        ParameterExpression replacement = Expression.Parameter(typeof(double), "replacement");
        var transformer = new PreparedOperandObservingTransformer(original, replacement);
        UnaryExpression negate = Expression.Negate(original);

        transformer.ExposeTransform(negate);

        Assert.AreSame(replacement, transformer.CapturedOperand);
        Assert.IsNotNull(transformer.CapturedNode);
        Assert.AreSame(replacement, transformer.CapturedNode!.Operand);
        Assert.AreNotSame(negate, transformer.CapturedNode, "The rule must observe the rebuilt node, not the original.");
    }

    // ---------------------------------------------------------------------------------------------
    // Test B — preparation order.
    // ---------------------------------------------------------------------------------------------

    /// <summary>A transformer recording a single ordered event sequence across operand preparation and rule invocation.</summary>
    private sealed class PreparationOrderTransformer : ExpressionTransformer
    {
        private readonly bool _throwOnPrepare;

        /// <summary>Initializes the transformer.</summary>
        /// <param name="throwOnPrepare">Whether <see cref="PrepareExpression"/> must throw instead of recording an event.</param>
        public PreparationOrderTransformer(bool throwOnPrepare = false)
        {
            _throwOnPrepare = throwOnPrepare;
        }

        /// <summary>The chronologically ordered events observed: <c>"Operand"</c> then <c>"Rule"</c>.</summary>
        public List<string> Events { get; } = new();

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <inheritdoc />
        protected override Expression PrepareExpression(Expression e)
        {
            if (_throwOnPrepare)
            {
                throw new InvalidOperationException("operand preparation failure");
            }

            Events.Add("Operand");
            return e;
        }

        /// <summary>Matches any <see cref="ExpressionType.Negate"/> node and records that the rule ran.</summary>
        [ExpressionSignature(ExpressionType.Negate)]
        private Expression Rule(UnaryExpression e, Expression operand)
        {
            Events.Add("Rule");
            return e;
        }
    }

    /// <summary>
    /// <c>PrepareUnary</c> must prepare the operand strictly before invoking the rule. A single ordered
    /// event list — rather than two independently checked facts — is required to actually discriminate
    /// against a faulty implementation that runs the rule before (or interleaved with) preparation.
    /// </summary>
    [TestMethod]
    public void UnaryPreparation_PreparesOperandThenRunsRule()
    {
        var transformer = new PreparationOrderTransformer();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        UnaryExpression negate = Expression.Negate(x);

        transformer.ExposeTransform(negate);

        CollectionAssert.AreEqual(new[] { "Operand", "Rule" }, transformer.Events,
            "PrepareUnary must prepare the operand, then invoke the rule — strictly in that order.");
    }

    /// <summary>
    /// If preparing the operand throws, the rule must never run at all.
    /// </summary>
    [TestMethod]
    public void UnaryPreparation_OperandThrows_RuleNeverRuns()
    {
        var transformer = new PreparationOrderTransformer(throwOnPrepare: true);
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        UnaryExpression negate = Expression.Negate(x);

        Assert.ThrowsExactly<InvalidOperationException>(() => transformer.ExposeTransform(negate));
        CollectionAssert.AreEqual(System.Array.Empty<string>(), transformer.Events,
            "No event should be recorded: operand preparation itself failed before the rule could ever run.");
    }

    // ---------------------------------------------------------------------------------------------
    // Test C — Single-shaped rule observes the rebuilt node.
    // ---------------------------------------------------------------------------------------------

    /// <summary>A transformer whose single-parameter rule observes the rebuilt node produced from a substituted operand.</summary>
    private sealed class SingleRuleObservesRebuiltNodeTransformer : ExpressionTransformer
    {
        private readonly Expression _original;
        private readonly Expression _replacement;

        /// <summary>Initializes the transformer with the original operand and its replacement.</summary>
        /// <param name="original">The original operand instance to substitute.</param>
        /// <param name="replacement">The replacement instance <paramref name="original"/> is substituted with.</param>
        public SingleRuleObservesRebuiltNodeTransformer(Expression original, Expression replacement)
        {
            _original = original;
            _replacement = replacement;
        }

        /// <summary>The node the rule was invoked with.</summary>
        public UnaryExpression? CapturedNode { get; private set; }

        /// <summary>Whether <see cref="FinalizeExpression"/> was invoked.</summary>
        public bool FinalizeInvoked { get; private set; }

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <inheritdoc />
        protected override Expression PrepareExpression(Expression e)
            => ReferenceEquals(e, _original) ? _replacement : e;

        /// <summary>A rule declaring only the node parameter (<see cref="ExpressionTransformer"/>'s <c>InvocationKind.Single</c> shape).</summary>
        [ExpressionSignature(ExpressionType.Negate)]
        private Expression Rule(UnaryExpression e)
        {
            CapturedNode = e;
            return e;
        }

        /// <inheritdoc />
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
        {
            FinalizeInvoked = true;
            return CopyExpression(e, parameters);
        }
    }

    /// <summary>
    /// A <c>Single</c>-shaped rule must still receive the rebuilt unary node whose operand has already
    /// been prepared, and completing through it must never reach <see cref="ExpressionTransformer.FinalizeExpression"/>
    /// — this is a positive allocation scenario: no <c>Expression[1]</c> is ever needed on this path.
    /// </summary>
    [TestMethod]
    public void SingleUnaryRule_ObservesRebuiltNodeWithPreparedOperand_FinalizeNotCalled()
    {
        ParameterExpression original = Expression.Parameter(typeof(double), "x");
        ParameterExpression replacement = Expression.Parameter(typeof(double), "replacement");
        var transformer = new SingleRuleObservesRebuiltNodeTransformer(original, replacement);
        UnaryExpression negate = Expression.Negate(original);

        transformer.ExposeTransform(negate);

        Assert.IsNotNull(transformer.CapturedNode);
        Assert.AreNotSame(negate, transformer.CapturedNode);
        Assert.AreSame(replacement, transformer.CapturedNode!.Operand);
        Assert.IsFalse(transformer.FinalizeInvoked);
    }

    // ---------------------------------------------------------------------------------------------
    // Test D / E — ExpressionArray-shaped rule.
    // ---------------------------------------------------------------------------------------------

    /// <summary>A transformer whose <c>Expression[]</c>-shaped unary rule optionally mutates the array it receives.</summary>
    private sealed class UnaryExpressionArrayRuleTransformer : ExpressionTransformer
    {
        private readonly bool _mutate;

        /// <summary>Initializes the transformer.</summary>
        /// <param name="mutate">Whether the rule must overwrite slot 0 of the array it receives.</param>
        public UnaryExpressionArrayRuleTransformer(bool mutate = false)
        {
            _mutate = mutate;
        }

        /// <summary>The array the rule was invoked with.</summary>
        public Expression[]? CapturedArgs { get; private set; }

        /// <summary>The node the rule was invoked with.</summary>
        public UnaryExpression? CapturedNode { get; private set; }

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>Matches any <see cref="ExpressionType.Negate"/> node and captures (and optionally mutates) the sub-expression array.</summary>
        [ExpressionSignature(ExpressionType.Negate)]
        private Expression Rule(UnaryExpression e, Expression[] parameters)
        {
            CapturedNode = e;
            CapturedArgs = parameters;
            if (_mutate)
            {
                parameters[0] = Expression.Constant(999.0);
            }

            return e;
        }
    }

    /// <summary>
    /// A rule declared with a second parameter of exactly type <c>Expression[]</c> must receive a real,
    /// exactly-one-element array whose sole slot is the rebuilt node's <c>Operand</c>.
    /// </summary>
    [TestMethod]
    public void UnaryExpressionArrayRule_ReceivesRealOneElementArray()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        UnaryExpression negate = Expression.Negate(x);
        var transformer = new UnaryExpressionArrayRuleTransformer();

        transformer.ExposeTransform(negate);

        Assert.IsNotNull(transformer.CapturedArgs);
        Assert.AreEqual(typeof(Expression[]), transformer.CapturedArgs!.GetType());
        Assert.AreEqual(1, transformer.CapturedArgs.Length);
        Assert.IsNotNull(transformer.CapturedNode);
        Assert.AreSame(transformer.CapturedNode!.Operand, transformer.CapturedArgs[0]);
    }

    /// <summary>
    /// The <c>Expression[]</c> a rule receives is separate, mutable storage: mutating a slot must not
    /// change the prepared node's <c>Operand</c>.
    /// </summary>
    [TestMethod]
    public void UnaryExpressionArrayRule_MutatingArray_DoesNotAffectPreparedNode()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        UnaryExpression negate = Expression.Negate(x);
        var transformer = new UnaryExpressionArrayRuleTransformer(mutate: true);

        transformer.ExposeTransform(negate);

        Assert.IsNotNull(transformer.CapturedNode);
        Assert.AreSame(x, transformer.CapturedNode!.Operand,
            "Mutating the materialized ExpressionArray argument must not affect the prepared UnaryExpression's Operand.");
    }

    // ---------------------------------------------------------------------------------------------
    // Test F — ExpressionArray rule returning null.
    // ---------------------------------------------------------------------------------------------

    /// <summary>A transformer whose <c>Expression[]</c>-shaped unary rule always matches but returns null.</summary>
    private sealed class UnaryExpressionArrayReturnsNullTransformer : ExpressionTransformer
    {
        /// <summary>Whether the rule was invoked.</summary>
        public bool RuleInvoked { get; private set; }

        /// <summary>Whether <see cref="FinalizeExpression"/> was invoked.</summary>
        public bool FinalizeInvoked { get; private set; }

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>Matches any <see cref="ExpressionType.Negate"/> node but always returns null.</summary>
        [ExpressionSignature(ExpressionType.Negate)]
        private Expression? Rule(UnaryExpression e, Expression[] parameters)
        {
            RuleInvoked = true;
            return null;
        }

        /// <inheritdoc />
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
        {
            FinalizeInvoked = true;
            return CopyExpression(e, parameters);
        }
    }

    /// <summary>
    /// A unary <c>Expression[]</c>-shaped rule returning <see langword="null"/> is considered APPLIED:
    /// <see cref="ExpressionTransformer.Transform(Expression)"/> itself returns <see langword="null"/>
    /// and <see cref="ExpressionTransformer.FinalizeExpression"/> must never run.
    /// </summary>
    [TestMethod]
    public void UnaryExpressionArrayRuleReturningNull_IsConsideredApplied_TransformReturnsNull()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        UnaryExpression negate = Expression.Negate(x);
        var transformer = new UnaryExpressionArrayReturnsNullTransformer();

        Expression result = transformer.ExposeTransform(negate);

        Assert.IsTrue(transformer.RuleInvoked);
        Assert.IsNull(result);
        Assert.IsFalse(transformer.FinalizeInvoked,
            "FinalizeExpression must not run once an ExpressionArray rule has been applied, even when it returns null.");
    }

    // ---------------------------------------------------------------------------------------------
    // Test G / H — FinalizeExpression fallback.
    // ---------------------------------------------------------------------------------------------

    /// <summary>A transformer with no rules, whose <see cref="FinalizeExpression"/> optionally mutates the array it receives.</summary>
    private sealed class UnaryFinalizeObservingTransformer : ExpressionTransformer
    {
        private readonly bool _mutate;

        /// <summary>Initializes the transformer.</summary>
        /// <param name="mutate">Whether <see cref="FinalizeExpression"/> must overwrite slot 0 of the array it receives.</param>
        public UnaryFinalizeObservingTransformer(bool mutate = false)
        {
            _mutate = mutate;
        }

        /// <summary>The expression <see cref="FinalizeExpression"/> was invoked with.</summary>
        public Expression? CapturedFinalizeExpression { get; private set; }

        /// <summary>The sub-expression array <see cref="FinalizeExpression"/> was invoked with.</summary>
        public Expression[]? CapturedFinalizeParameters { get; private set; }

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <inheritdoc />
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
        {
            CapturedFinalizeExpression = e;
            CapturedFinalizeParameters = parameters;
            if (_mutate && parameters.Length > 0)
            {
                parameters[0] = Expression.Constant(999.0);
            }

            return CopyExpression(e, parameters);
        }
    }

    /// <summary>
    /// A unary node with no matching rule must reach <see cref="ExpressionTransformer.FinalizeExpression"/>
    /// with a real, exactly-one-element <see cref="Expression"/>[] matching the rebuilt node's <c>Operand</c>.
    /// </summary>
    [TestMethod]
    public void NoMatchingRule_FinalizeExpression_ReceivesRealOneElementArray()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        UnaryExpression negate = Expression.Negate(x);
        var transformer = new UnaryFinalizeObservingTransformer();

        Expression result = transformer.ExposeTransform(negate);

        Assert.IsNotNull(transformer.CapturedFinalizeParameters);
        Assert.AreEqual(typeof(Expression[]), transformer.CapturedFinalizeParameters!.GetType());
        Assert.AreEqual(1, transformer.CapturedFinalizeParameters!.Length);

        var finalizedUnary = transformer.CapturedFinalizeExpression as UnaryExpression;
        Assert.IsNotNull(finalizedUnary);
        Assert.AreNotSame(negate, transformer.CapturedFinalizeExpression,
            "Finalize must receive the rebuilt unary node, not the original.");
        Assert.AreSame(finalizedUnary!.Operand, transformer.CapturedFinalizeParameters![0]);

        var resultUnary = result as UnaryExpression;
        Assert.IsNotNull(resultUnary);
        Assert.AreEqual(ExpressionType.Negate, resultUnary!.NodeType);
    }

    /// <summary>
    /// The <c>Expression[]</c> <see cref="ExpressionTransformer.FinalizeExpression"/> receives is separate,
    /// mutable storage: mutating a slot must not change the prepared node's <c>Operand</c>.
    /// </summary>
    [TestMethod]
    public void NoMatchingRule_MutatingFinalizeArray_DoesNotAffectPreparedNode()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        UnaryExpression negate = Expression.Negate(x);
        var transformer = new UnaryFinalizeObservingTransformer(mutate: true);

        transformer.ExposeTransform(negate);

        var finalizedUnary = (UnaryExpression)transformer.CapturedFinalizeExpression!;
        Assert.AreSame(x, finalizedUnary.Operand,
            "Mutating the materialized Finalize array must not affect the prepared UnaryExpression's Operand.");
    }

    // ---------------------------------------------------------------------------------------------
    // Test I — positional rule chain: first null, second succeeds.
    // ---------------------------------------------------------------------------------------------

    /// <summary>A transformer with two positional unary rules for the same node type: the first always defers.</summary>
    private sealed class PositionalNullThenSuccessTransformer : ExpressionTransformer
    {
        /// <summary>The <c>operand</c> argument the first rule was invoked with.</summary>
        public Expression? FirstOperand { get; private set; }

        /// <summary>The <c>operand</c> argument the second rule was invoked with.</summary>
        public Expression? SecondOperand { get; private set; }

        /// <summary>Whether the first rule was invoked.</summary>
        public bool FirstInvoked { get; private set; }

        /// <summary>Whether the second rule was invoked.</summary>
        public bool SecondInvoked { get; private set; }

        /// <summary>Whether <see cref="FinalizeExpression"/> was invoked.</summary>
        public bool FinalizeInvoked { get; private set; }

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>Declared first; always defers via <see langword="null"/>.</summary>
        [ExpressionSignature(ExpressionType.Negate)]
        private Expression? First(UnaryExpression e, Expression operand)
        {
            FirstInvoked = true;
            FirstOperand = operand;
            return null;
        }

        /// <summary>Declared second; always wins.</summary>
        [ExpressionSignature(ExpressionType.Negate)]
        private Expression Second(UnaryExpression e, Expression operand)
        {
            SecondInvoked = true;
            SecondOperand = operand;
            return e;
        }

        /// <inheritdoc />
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
        {
            FinalizeInvoked = true;
            return CopyExpression(e, parameters);
        }
    }

    /// <summary>
    /// When a first positional unary rule defers via <see langword="null"/> and a second one wins, both
    /// must observe the exact same prepared operand instance, and
    /// <see cref="ExpressionTransformer.FinalizeExpression"/> must never run — this is the positive
    /// performance scenario where no <see cref="Expression"/>[1] should ever be necessary.
    /// </summary>
    [TestMethod]
    public void PositionalRuleReturningNull_ThenSecondRuleSucceeds_BothObserveSamePreparedOperand()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        UnaryExpression negate = Expression.Negate(x);
        var transformer = new PositionalNullThenSuccessTransformer();

        transformer.ExposeTransform(negate);

        Assert.IsTrue(transformer.FirstInvoked);
        Assert.IsTrue(transformer.SecondInvoked);
        Assert.AreSame(transformer.FirstOperand, transformer.SecondOperand);
        Assert.AreSame(x, transformer.FirstOperand);
        Assert.IsFalse(transformer.FinalizeInvoked, "A winning positional rule must short-circuit before Finalize.");
    }

    // ---------------------------------------------------------------------------------------------
    // Test J — fallback after positional rules still receives the prepared operand.
    // ---------------------------------------------------------------------------------------------

    /// <summary>A transformer whose single positional unary rule always defers, so Finalize always runs.</summary>
    private sealed class PositionalAlwaysNullThenFinalizeTransformer : ExpressionTransformer
    {
        /// <summary>The <c>operand</c> argument the rule was invoked with.</summary>
        public Expression? RuleOperand { get; private set; }

        /// <summary>Slot 0 of the array <see cref="FinalizeExpression"/> was invoked with.</summary>
        public Expression? FinalizeOperand { get; private set; }

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>Always defers via <see langword="null"/>.</summary>
        [ExpressionSignature(ExpressionType.Negate)]
        private Expression? Rule(UnaryExpression e, Expression operand)
        {
            RuleOperand = operand;
            return null;
        }

        /// <inheritdoc />
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
        {
            FinalizeOperand = parameters[0];
            return CopyExpression(e, parameters);
        }
    }

    /// <summary>
    /// When the sole positional unary rule defers via <see langword="null"/>,
    /// <see cref="ExpressionTransformer.FinalizeExpression"/> must receive the same logical prepared
    /// operand the rule itself observed — built directly from the prepared node's <c>Operand</c>, not
    /// from the separate reflection invocation argument array.
    /// </summary>
    [TestMethod]
    public void PositionalRuleReturningNull_NoLaterWinner_FinalizeReceivesSamePreparedOperand()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        UnaryExpression negate = Expression.Negate(x);
        var transformer = new PositionalAlwaysNullThenFinalizeTransformer();

        transformer.ExposeTransform(negate);

        Assert.AreSame(x, transformer.RuleOperand);
        Assert.AreSame(transformer.RuleOperand, transformer.FinalizeOperand);
    }

    // ---------------------------------------------------------------------------------------------
    // Test K — too many positional parameters.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A positional rule declaring MORE parameters than the node's context supplies (here: 3 parameters
    /// for a 2-slot unary context, with a trailing unmatched <c>extra</c>).
    /// </summary>
    private sealed class UnaryTooManyParametersRuleTransformer : ExpressionTransformer
    {
        /// <summary>Whether <see cref="Rule"/> was ever entered (it should never be, per the characterization below).</summary>
        public bool RuleWasInvoked { get; private set; }

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>Declares 3 parameters (e, operand, extra) though the unary context only supplies 2.</summary>
        [ExpressionSignature(ExpressionType.Negate)]
        private Expression Rule(UnaryExpression e, Expression operand, Expression extra)
        {
            RuleWasInvoked = true;
            return operand;
        }
    }

    /// <summary>
    /// A positional rule declaring more parameters than the unary node's context array supplies must fail
    /// during per-parameter validation with <see cref="IndexOutOfRangeException"/> before the rule body
    /// ever runs — mirroring <c>Transform_PositionalRuleWithMoreParametersThanContext_ThrowsIndexOutOfRangeException</c>
    /// for <see cref="BinaryExpression"/>. Unlike Binary, there is no "too few" equivalent for Unary: a
    /// one-parameter rule is a valid <c>InvocationKind.Single</c> shape, not a malformed positional one.
    /// </summary>
    [TestMethod]
    public void Transform_UnaryPositionalRuleWithMoreParametersThanContext_ThrowsIndexOutOfRangeException()
    {
        var transformer = new UnaryTooManyParametersRuleTransformer();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        UnaryExpression negate = Expression.Negate(x);

        Assert.ThrowsExactly<IndexOutOfRangeException>(() => transformer.ExposeTransform(negate));
        Assert.IsFalse(transformer.RuleWasInvoked, "The rule body must never run: the failure happens during parameter validation.");
    }

    // ---------------------------------------------------------------------------------------------
    // Test L / M / N / O — reconstruction fidelity against the historical CopyExpression oracle.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A transformer with no rules, used purely to observe the exact node <c>PrepareUnary</c> produces:
    /// with no candidate rule for any node type, <see cref="Transform(Expression)"/> always falls through
    /// to <see cref="FinalizeExpression"/>, which here returns the node unchanged (no further copy) so the
    /// captured node is exactly <c>PrepareUnary</c>'s output.
    /// </summary>
    private sealed class UnaryReconstructionOracleTransformer : ExpressionTransformer
    {
        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>Exposes the protected static <see cref="ExpressionTransformer.CopyExpression"/> as the historical reconstruction oracle.</summary>
        /// <param name="e">The original expression to copy.</param>
        /// <param name="parameters">The sub-expressions to insert into the copy.</param>
        /// <returns>The oracle's reconstruction of <paramref name="e"/>.</returns>
        public static Expression ExposeCopy(Expression e, params Expression[] parameters) => CopyExpression(e, parameters);

        /// <inheritdoc />
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => e;
    }

    /// <summary>
    /// Asserts that transforming <paramref name="original"/> (with no matching rule, no
    /// <see cref="ExpressionTransformer.PrepareExpression"/> substitution) produces exactly the same
    /// observable reconstruction as the historical <see cref="ExpressionTransformer.CopyExpression"/>
    /// oracle called directly on <paramref name="original"/>'s own operand: same <c>NodeType</c>, same
    /// <c>Type</c>, same <c>Operand</c> reference, same <c>Method</c>, same <c>IsLifted</c>/<c>IsLiftedToNull</c>.
    /// </summary>
    /// <param name="familyName">A short label for the unary family under test, used in failure messages.</param>
    /// <param name="original">The original unary expression to reconstruct both ways.</param>
    private static void AssertUnaryReconstructionMatchesOracle(string familyName, UnaryExpression original)
    {
        var transformer = new UnaryReconstructionOracleTransformer();
        Expression preparedResult = transformer.ExposeTransform(original);
        Expression expectedResult = UnaryReconstructionOracleTransformer.ExposeCopy(original, original.Operand);

        Assert.AreEqual(expectedResult.NodeType, preparedResult.NodeType, $"[{familyName}] NodeType mismatch.");
        Assert.AreEqual(expectedResult.Type, preparedResult.Type, $"[{familyName}] Type mismatch.");

        var preparedUnary = preparedResult as UnaryExpression;
        var expectedUnary = expectedResult as UnaryExpression;
        Assert.IsNotNull(preparedUnary, $"[{familyName}] prepared result must be a UnaryExpression.");
        Assert.IsNotNull(expectedUnary, $"[{familyName}] oracle result must be a UnaryExpression.");
        Assert.AreSame(original.Operand, preparedUnary!.Operand, $"[{familyName}] Operand must be the prepared operand.");
        Assert.AreEqual(expectedUnary!.Method, preparedUnary.Method, $"[{familyName}] Method mismatch.");
        Assert.AreEqual(expectedUnary.IsLifted, preparedUnary.IsLifted, $"[{familyName}] IsLifted mismatch.");
        Assert.AreEqual(expectedUnary.IsLiftedToNull, preparedUnary.IsLiftedToNull, $"[{familyName}] IsLiftedToNull mismatch.");
    }

    /// <summary>
    /// Every practical <see cref="UnaryExpression"/> family <see cref="ExpressionTransformer.CopyExpression"/>
    /// supports must reconstruct identically through <c>PrepareUnary</c>'s dedicated helper: this is not an
    /// idealized reconstruction, it is byte-for-byte parity with the pre-existing, protected reconstruction
    /// oracle that this performance change must not alter.
    /// </summary>
    [TestMethod]
    public void UnaryReconstruction_MatchesHistoricalCopyExpressionOracle_AcrossAllSupportedFamilies()
    {
        (string Name, UnaryExpression Original)[] families =
        [
            ("ArrayLength", Expression.ArrayLength(Expression.Parameter(typeof(double[]), "arr"))),
            ("Convert", Expression.Convert(Expression.Parameter(typeof(double), "x"), typeof(float))),
            ("ConvertChecked", Expression.ConvertChecked(Expression.Parameter(typeof(long), "x"), typeof(int))),
            ("Negate", Expression.Negate(Expression.Parameter(typeof(double), "x"))),
            ("UnaryPlus", Expression.UnaryPlus(Expression.Parameter(typeof(double), "x"))),
            ("NegateChecked", Expression.NegateChecked(Expression.Parameter(typeof(int), "x"))),
            ("Not", Expression.Not(Expression.Parameter(typeof(bool), "x"))),
            ("Quote", Expression.Quote(Expression.Lambda(Expression.Constant(1.0)))),
            ("TypeAs", Expression.TypeAs(Expression.Parameter(typeof(object), "x"), typeof(string))),
            ("Decrement", Expression.Decrement(Expression.Parameter(typeof(double), "x"))),
            ("Increment", Expression.Increment(Expression.Parameter(typeof(double), "x"))),
            ("Throw", Expression.Throw(Expression.Parameter(typeof(Exception), "ex"))),
            ("Unbox", Expression.Unbox(Expression.Parameter(typeof(object), "x"), typeof(int))),
            ("PreIncrementAssign", Expression.PreIncrementAssign(Expression.Parameter(typeof(double), "x"))),
            ("PreDecrementAssign", Expression.PreDecrementAssign(Expression.Parameter(typeof(double), "x"))),
            ("PostIncrementAssign", Expression.PostIncrementAssign(Expression.Parameter(typeof(double), "x"))),
            ("PostDecrementAssign", Expression.PostDecrementAssign(Expression.Parameter(typeof(double), "x"))),
            ("OnesComplement", Expression.OnesComplement(Expression.Parameter(typeof(int), "x"))),
            ("IsTrue", Expression.IsTrue(Expression.Parameter(typeof(bool), "x"))),
            ("IsFalse", Expression.IsFalse(Expression.Parameter(typeof(bool), "x"))),
        ];

        foreach ((string name, UnaryExpression original) in families)
        {
            AssertUnaryReconstructionMatchesOracle(name, original);
        }
    }

    /// <summary>An explicit unary operator method used to characterize what reconstruction does with <see cref="UnaryExpression.Method"/>.</summary>
    /// <param name="value">The operand value.</param>
    /// <returns>The negated value.</returns>
    private static double ExplicitNegateMethod(double value) => -value;

    /// <summary>
    /// A <see cref="ExpressionType.Negate"/> node built with an explicit <see cref="MethodInfo"/> must
    /// reconstruct identically to the historical oracle — whatever that oracle does with
    /// <see cref="UnaryExpression.Method"/> (it is not assumed here that preservation is correct; see
    /// <see cref="AssertUnaryReconstructionMatchesOracle"/>, which compares against the oracle itself
    /// rather than asserting a hard-coded expectation).
    /// </summary>
    [TestMethod]
    public void ExplicitMethodInfo_Negate_MatchesHistoricalCopyExpressionOracle()
    {
        MethodInfo explicitMethod = typeof(ExpressionTransformerUnaryLazyParametersTests)
            .GetMethod(nameof(ExplicitNegateMethod), BindingFlags.NonPublic | BindingFlags.Static)!;
        UnaryExpression original = Expression.Negate(Expression.Parameter(typeof(double), "x"), explicitMethod);

        AssertUnaryReconstructionMatchesOracle("Negate+ExplicitMethod", original);
    }

    /// <summary>
    /// A typed <c>Throw</c> (<c>Expression.Throw(value, type)</c>) must reconstruct identically to the
    /// historical oracle. The historical <see cref="ExpressionTransformer.CopyExpression"/> branch for
    /// <see cref="ExpressionType.Throw"/> calls <c>Expression.Throw(parameters[0])</c> — the single-argument
    /// overload — without ever passing the original node's declared <see cref="Expression.Type"/>, so a
    /// typed throw's reconstructed <c>Type</c> can differ from the original. This is a pre-existing,
    /// historical quirk this performance change must preserve exactly, not fix.
    /// </summary>
    [TestMethod]
    public void TypedThrow_MatchesHistoricalCopyExpressionOracle()
    {
        Expression exceptionExpression = Expression.New(typeof(InvalidOperationException));
        UnaryExpression typedThrow = Expression.Throw(exceptionExpression, typeof(int));

        AssertUnaryReconstructionMatchesOracle("Throw+ExplicitType", typedThrow);
    }

    /// <summary>
    /// <c>Expression.Rethrow()</c> builds a <see cref="ExpressionType.Throw"/> node whose <c>Operand</c>
    /// is <see langword="null"/> (there is no exception expression to rethrow explicitly). The lazy unary
    /// representation must preserve that null through to <see cref="ExpressionTransformer.FinalizeExpression"/>'s
    /// materialized array rather than substituting a replacement or validating it eagerly.
    /// </summary>
    [TestMethod]
    public void Rethrow_NullOperand_FinalizeReceivesArrayContainingNull()
    {
        UnaryExpression rethrow = Expression.Rethrow();
        var transformer = new UnaryFinalizeObservingTransformer();

        Expression result = transformer.ExposeTransform(rethrow);

        Assert.IsNotNull(transformer.CapturedFinalizeParameters);
        Assert.AreEqual(1, transformer.CapturedFinalizeParameters!.Length);
        Assert.IsNull(transformer.CapturedFinalizeParameters[0]);

        var resultUnary = result as UnaryExpression;
        Assert.IsNotNull(resultUnary);
        Assert.AreEqual(ExpressionType.Throw, resultUnary!.NodeType);
        Assert.IsNull(resultUnary.Operand);
    }
}
