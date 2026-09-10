using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Characterization and regression tests for <see cref="ExpressionTransformer"/>'s handling of
/// <see cref="BinaryExpression"/> parameters. Before this optimization, <c>PrepareBinary</c> eagerly
/// allocated an <c>Expression[2]</c> for every binary node. These tests pin down every externally
/// observable behavior that must be preserved now that the two logical parameters are materialized into
/// a real array only when an <c>Expression[]</c>-shaped rule or
/// <see cref="ExpressionTransformer.FinalizeExpression"/> actually requires one: preparation order, the
/// exact array a rule or <c>FinalizeExpression</c> receives (contents, independence from the prepared
/// node, and — for the <c>Expression[]</c>-shaped rule overload — runtime array type), and which
/// invocation paths never require an array at all. Allocation counts are intentionally verified by a
/// separate benchmark, not by these unit tests, which assert only observable object identity, array
/// contents, and control flow.
/// </summary>
[TestClass]
public class ExpressionTransformerBinaryLazyParametersTests
{
    // ---------------------------------------------------------------------------------------------
    // Test A — positional binary rule observes prepared children.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A transformer whose <see cref="ExpressionTransformer.PrepareExpression"/> override substitutes a
    /// fixed set of original sub-expressions with known replacement instances, so a rule's captured
    /// arguments can be checked against those replacements by reference.
    /// </summary>
    private sealed class PreparedChildrenObservingTransformer : ExpressionTransformer
    {
        private readonly Dictionary<Expression, Expression> _replacements;

        /// <summary>Initializes the transformer with a reference-keyed substitution map.</summary>
        /// <param name="replacements">Maps an original sub-expression to the instance it must be replaced with.</param>
        public PreparedChildrenObservingTransformer(Dictionary<Expression, Expression> replacements)
        {
            _replacements = replacements;
        }

        /// <summary>The rebuilt binary node the rule was invoked with.</summary>
        public BinaryExpression? CapturedNode { get; private set; }

        /// <summary>The <c>left</c> argument the rule was invoked with.</summary>
        public Expression? CapturedLeft { get; private set; }

        /// <summary>The <c>right</c> argument the rule was invoked with.</summary>
        public Expression? CapturedRight { get; private set; }

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <inheritdoc />
        protected override Expression PrepareExpression(Expression e)
            => _replacements.TryGetValue(e, out Expression? replacement) ? replacement : e;

        /// <summary>Matches any <see cref="ExpressionType.AndAlso"/> node and captures its arguments.</summary>
        [ExpressionSignature(ExpressionType.AndAlso)]
        private Expression Rule(BinaryExpression e, Expression left, Expression right)
        {
            CapturedNode = e;
            CapturedLeft = left;
            CapturedRight = right;
            return e;
        }
    }

    /// <summary>
    /// A positional binary rule must observe the already-prepared <c>left</c>/<c>right</c> operands (the
    /// instances <see cref="ExpressionTransformer.PrepareExpression"/> returned), and the rebuilt node's
    /// own <c>Left</c>/<c>Right</c> must be those same prepared instances — not the original operands.
    /// </summary>
    [TestMethod]
    public void PositionalBinaryRule_ObservesPreparedChildren()
    {
        ParameterExpression x = Expression.Parameter(typeof(bool), "x");
        ParameterExpression y = Expression.Parameter(typeof(bool), "y");
        Expression preparedLeft = Expression.Constant(true);
        Expression preparedRight = Expression.Constant(false);

        var replacements = new Dictionary<Expression, Expression>
        {
            [x] = preparedLeft,
            [y] = preparedRight,
        };
        var transformer = new PreparedChildrenObservingTransformer(replacements);
        BinaryExpression andAlso = Expression.AndAlso(x, y);

        transformer.ExposeTransform(andAlso);

        Assert.AreSame(preparedLeft, transformer.CapturedLeft);
        Assert.AreSame(preparedRight, transformer.CapturedRight);
        Assert.IsNotNull(transformer.CapturedNode);
        Assert.AreSame(preparedLeft, transformer.CapturedNode!.Left);
        Assert.AreSame(preparedRight, transformer.CapturedNode!.Right);
        Assert.AreNotSame(andAlso, transformer.CapturedNode, "The rule must observe the rebuilt node, not the original.");
    }

    // ---------------------------------------------------------------------------------------------
    // Test B — preparation order.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A transformer recording a single, strictly ordered sequence of events — preparation of the known
    /// <c>Left</c>/<c>Right</c> instances and invocation of the rule — into one list, so a test can assert
    /// their exact relative order instead of only two separately-checked facts (which cannot by
    /// themselves rule out an interleaving such as "prepare Left, run rule, prepare Right"). Optionally
    /// throws when a specific instance is encountered, to simulate a failure while preparing an operand.
    /// </summary>
    private sealed class PreparationOrderTransformer : ExpressionTransformer
    {
        private readonly Expression _leftExpression;
        private readonly Expression _rightExpression;
        private readonly Expression? _throwOn;

        /// <summary>Initializes the transformer with the known Left/Right instances to recognize, optionally simulating a failure while preparing <paramref name="throwOn"/>.</summary>
        /// <param name="leftExpression">The exact instance expected as the binary node's <c>Left</c> operand.</param>
        /// <param name="rightExpression">The exact instance expected as the binary node's <c>Right</c> operand.</param>
        /// <param name="throwOn">The exact sub-expression instance to throw for, or <see langword="null"/> to never throw.</param>
        public PreparationOrderTransformer(Expression leftExpression, Expression rightExpression, Expression? throwOn)
        {
            _leftExpression = leftExpression;
            _rightExpression = rightExpression;
            _throwOn = throwOn;
        }

        /// <summary>
        /// The single, strictly ordered sequence of events observed: <c>"Left"</c> and <c>"Right"</c> when
        /// the corresponding known instance is prepared, and <c>"Rule"</c> when the rule runs.
        /// </summary>
        public List<string> Events { get; } = new();

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <inheritdoc />
        protected override Expression PrepareExpression(Expression e)
        {
            if (_throwOn is not null && ReferenceEquals(e, _throwOn))
            {
                throw new InvalidOperationException("Simulated failure while preparing an operand.");
            }

            if (ReferenceEquals(e, _leftExpression))
            {
                Events.Add("Left");
            }
            else if (ReferenceEquals(e, _rightExpression))
            {
                Events.Add("Right");
            }

            return e;
        }

        /// <summary>Matches any <see cref="ExpressionType.AndAlso"/> node.</summary>
        [ExpressionSignature(ExpressionType.AndAlso)]
        private Expression Rule(BinaryExpression e, Expression left, Expression right)
        {
            Events.Add("Rule");
            return e;
        }
    }

    /// <summary>
    /// <c>Left</c> must be prepared, then <c>Right</c>, then the rule must run — strictly in that order.
    /// Asserting the three events as one ordered sequence (rather than checking preparation order and
    /// rule invocation separately) is what actually rules out a faulty interleaving such as
    /// "prepare Left, run rule, prepare Right", which would still satisfy two independent checks.
    /// </summary>
    [TestMethod]
    public void BinaryPreparation_PreparesLeftThenRightThenRunsRule()
    {
        ParameterExpression x = Expression.Parameter(typeof(bool), "x");
        ParameterExpression y = Expression.Parameter(typeof(bool), "y");
        var transformer = new PreparationOrderTransformer(x, y, throwOn: null);
        BinaryExpression andAlso = Expression.AndAlso(x, y);

        transformer.ExposeTransform(andAlso);

        CollectionAssert.AreEqual(new[] { "Left", "Right", "Rule" }, transformer.Events,
            "PrepareBinary must prepare Left, then Right, then invoke the rule — strictly in that order.");
    }

    /// <summary>
    /// When preparing <c>Left</c> throws, <c>Right</c> must never be prepared and the rule must never run.
    /// </summary>
    [TestMethod]
    public void BinaryPreparation_LeftThrows_RightIsNeverPreparedAndRuleNeverRuns()
    {
        ParameterExpression x = Expression.Parameter(typeof(bool), "x");
        ParameterExpression y = Expression.Parameter(typeof(bool), "y");
        var transformer = new PreparationOrderTransformer(x, y, throwOn: x);
        BinaryExpression andAlso = Expression.AndAlso(x, y);

        Assert.ThrowsExactly<InvalidOperationException>(() => transformer.ExposeTransform(andAlso));

        CollectionAssert.AreEqual(System.Array.Empty<string>(), transformer.Events,
            "Right must never be prepared and the rule must never run when Left throws.");
    }

    // ---------------------------------------------------------------------------------------------
    // Test C / D — ExpressionArray-shaped binary rule.
    // ---------------------------------------------------------------------------------------------

    /// <summary>A transformer whose rule uses the special <c>Expression[]</c> second-parameter shape for a binary node.</summary>
    private sealed class BinaryExpressionArrayRuleTransformer : ExpressionTransformer
    {
        /// <summary>The rebuilt binary node the rule was invoked with.</summary>
        public BinaryExpression? CapturedNode { get; private set; }

        /// <summary>The sub-expression array the rule was invoked with.</summary>
        public Expression[]? CapturedParameters { get; private set; }

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>Matches any <see cref="ExpressionType.AndAlso"/> node and captures the full sub-expression array.</summary>
        [ExpressionSignature(ExpressionType.AndAlso)]
        private Expression Rule(BinaryExpression e, Expression[] parameters)
        {
            CapturedNode = e;
            CapturedParameters = parameters;
            return e;
        }
    }

    /// <summary>
    /// An <c>Expression[]</c>-shaped rule matching a binary node must receive a real, exactly-two-element
    /// <see cref="Expression"/>[] containing the prepared <c>Left</c>/<c>Right</c>, in that order.
    /// </summary>
    [TestMethod]
    public void BinaryExpressionArrayRule_ReceivesRealTwoElementArray()
    {
        ParameterExpression x = Expression.Parameter(typeof(bool), "x");
        ParameterExpression y = Expression.Parameter(typeof(bool), "y");
        var transformer = new BinaryExpressionArrayRuleTransformer();
        BinaryExpression andAlso = Expression.AndAlso(x, y);

        transformer.ExposeTransform(andAlso);

        Assert.IsNotNull(transformer.CapturedParameters);
        Assert.AreEqual(typeof(Expression[]), transformer.CapturedParameters!.GetType());
        Assert.AreEqual(2, transformer.CapturedParameters!.Length);
        Assert.IsNotNull(transformer.CapturedNode);
        Assert.AreSame(transformer.CapturedNode!.Left, transformer.CapturedParameters![0]);
        Assert.AreSame(transformer.CapturedNode!.Right, transformer.CapturedParameters![1]);
    }

    /// <summary>A transformer whose <c>Expression[]</c>-shaped rule mutates the array it receives.</summary>
    private sealed class BinaryExpressionArrayMutatingRuleTransformer : ExpressionTransformer
    {
        /// <summary>The rebuilt binary node the rule was invoked with.</summary>
        public BinaryExpression? CapturedNode { get; private set; }

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>Matches any <see cref="ExpressionType.AndAlso"/> node and mutates slot 0 of its sub-expression array.</summary>
        [ExpressionSignature(ExpressionType.AndAlso)]
        private Expression Rule(BinaryExpression e, Expression[] parameters)
        {
            CapturedNode = e;
            parameters[0] = Expression.Constant(true);
            return e;
        }
    }

    /// <summary>
    /// The <c>Expression[]</c> a rule receives is separate storage from the prepared
    /// <see cref="BinaryExpression"/>: mutating a slot in the array must not change the node's
    /// <c>Left</c>/<c>Right</c>.
    /// </summary>
    [TestMethod]
    public void BinaryExpressionArrayRule_MutatingArray_DoesNotAffectPreparedNode()
    {
        ParameterExpression x = Expression.Parameter(typeof(bool), "x");
        ParameterExpression y = Expression.Parameter(typeof(bool), "y");
        var transformer = new BinaryExpressionArrayMutatingRuleTransformer();
        BinaryExpression andAlso = Expression.AndAlso(x, y);

        transformer.ExposeTransform(andAlso);

        Assert.IsNotNull(transformer.CapturedNode);
        Assert.AreSame(x, transformer.CapturedNode!.Left,
            "Mutating the materialized array must not affect the prepared BinaryExpression's Left.");
    }

    // ---------------------------------------------------------------------------------------------
    // Test E — binary ExpressionArray rule returning null remains applied.
    // ---------------------------------------------------------------------------------------------

    /// <summary>A transformer whose <c>Expression[]</c>-shaped binary rule always matches but returns null.</summary>
    private sealed class BinaryExpressionArrayReturnsNullTransformer : ExpressionTransformer
    {
        /// <summary>Whether the rule was invoked.</summary>
        public bool RuleInvoked { get; private set; }

        /// <summary>Whether <see cref="FinalizeExpression"/> was invoked.</summary>
        public bool FinalizeInvoked { get; private set; }

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>Matches any <see cref="ExpressionType.AndAlso"/> node but always returns null.</summary>
        [ExpressionSignature(ExpressionType.AndAlso)]
        private Expression? Rule(BinaryExpression e, Expression[] parameters)
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
    /// A binary <c>Expression[]</c>-shaped rule returning <see langword="null"/> is considered APPLIED:
    /// <see cref="ExpressionTransformer.Transform(Expression)"/> itself returns <see langword="null"/>
    /// and <see cref="ExpressionTransformer.FinalizeExpression"/> must never run.
    /// </summary>
    [TestMethod]
    public void BinaryExpressionArrayRuleReturningNull_IsConsideredApplied_TransformReturnsNull()
    {
        ParameterExpression x = Expression.Parameter(typeof(bool), "x");
        ParameterExpression y = Expression.Parameter(typeof(bool), "y");
        var transformer = new BinaryExpressionArrayReturnsNullTransformer();
        BinaryExpression andAlso = Expression.AndAlso(x, y);

        Expression result = transformer.ExposeTransform(andAlso);

        Assert.IsTrue(transformer.RuleInvoked);
        Assert.IsNull(result);
        Assert.IsFalse(transformer.FinalizeInvoked,
            "FinalizeExpression must not run once an ExpressionArray rule has been applied, even when it returns null.");
    }

    // ---------------------------------------------------------------------------------------------
    // Test F / G — FinalizeExpression fallback.
    // ---------------------------------------------------------------------------------------------

    /// <summary>A transformer with no rules at all, so every binary node reaches <see cref="FinalizeExpression"/>.</summary>
    private class BinaryFinalizeObservingTransformer : ExpressionTransformer
    {
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
            return CopyExpression(e, parameters);
        }
    }

    /// <summary>
    /// A binary node with no matching rule must reach <see cref="ExpressionTransformer.FinalizeExpression"/>
    /// with a real, exactly-two-element <see cref="Expression"/>[] matching the rebuilt node's
    /// <c>Left</c>/<c>Right</c>.
    /// </summary>
    [TestMethod]
    public void NoMatchingRule_FinalizeExpression_ReceivesRealTwoElementArray()
    {
        ParameterExpression x = Expression.Parameter(typeof(bool), "x");
        ParameterExpression y = Expression.Parameter(typeof(bool), "y");
        var transformer = new BinaryFinalizeObservingTransformer();
        BinaryExpression andAlso = Expression.AndAlso(x, y);

        Expression result = transformer.ExposeTransform(andAlso);

        Assert.IsNotNull(transformer.CapturedFinalizeParameters);
        Assert.AreEqual(typeof(Expression[]), transformer.CapturedFinalizeParameters!.GetType());
        Assert.AreEqual(2, transformer.CapturedFinalizeParameters!.Length);

        var finalizedBinary = transformer.CapturedFinalizeExpression as BinaryExpression;
        Assert.IsNotNull(finalizedBinary);
        Assert.AreNotSame(andAlso, transformer.CapturedFinalizeExpression,
            "Finalize must receive the rebuilt binary node, not the original.");
        Assert.AreSame(finalizedBinary!.Left, transformer.CapturedFinalizeParameters![0]);
        Assert.AreSame(finalizedBinary!.Right, transformer.CapturedFinalizeParameters![1]);

        var resultBinary = result as BinaryExpression;
        Assert.IsNotNull(resultBinary);
        Assert.AreEqual(ExpressionType.AndAlso, resultBinary!.NodeType);
    }

    /// <summary>A transformer with no rules whose <see cref="FinalizeExpression"/> mutates the array it receives.</summary>
    private sealed class BinaryFinalizeMutatingTransformer : ExpressionTransformer
    {
        /// <summary>The expression <see cref="FinalizeExpression"/> was invoked with.</summary>
        public Expression? CapturedFinalizeExpression { get; private set; }

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <inheritdoc />
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
        {
            CapturedFinalizeExpression = e;
            parameters[0] = Expression.Constant(true);
            return CopyExpression(e, parameters);
        }
    }

    /// <summary>
    /// The <c>Expression[]</c> <see cref="ExpressionTransformer.FinalizeExpression"/> receives is separate,
    /// mutable storage: mutating a slot must not change the prepared node's <c>Left</c>/<c>Right</c>.
    /// </summary>
    [TestMethod]
    public void NoMatchingRule_MutatingFinalizeArray_DoesNotAffectPreparedNode()
    {
        ParameterExpression x = Expression.Parameter(typeof(bool), "x");
        ParameterExpression y = Expression.Parameter(typeof(bool), "y");
        var transformer = new BinaryFinalizeMutatingTransformer();
        BinaryExpression andAlso = Expression.AndAlso(x, y);

        transformer.ExposeTransform(andAlso);

        var finalizedBinary = (BinaryExpression)transformer.CapturedFinalizeExpression!;
        Assert.AreSame(x, finalizedBinary.Left,
            "Mutating the materialized Finalize array must not affect the prepared BinaryExpression's Left.");
    }

    // ---------------------------------------------------------------------------------------------
    // Test H — positional rule chain: first null, second succeeds.
    // ---------------------------------------------------------------------------------------------

    /// <summary>A transformer with two positional binary rules for the same node type: the first always defers.</summary>
    private sealed class PositionalNullThenSuccessTransformer : ExpressionTransformer
    {
        /// <summary>The <c>left</c> argument the first rule was invoked with.</summary>
        public Expression? FirstLeft { get; private set; }

        /// <summary>The <c>right</c> argument the first rule was invoked with.</summary>
        public Expression? FirstRight { get; private set; }

        /// <summary>The <c>left</c> argument the second rule was invoked with.</summary>
        public Expression? SecondLeft { get; private set; }

        /// <summary>The <c>right</c> argument the second rule was invoked with.</summary>
        public Expression? SecondRight { get; private set; }

        /// <summary>Whether the first rule was invoked.</summary>
        public bool FirstInvoked { get; private set; }

        /// <summary>Whether the second rule was invoked.</summary>
        public bool SecondInvoked { get; private set; }

        /// <summary>Whether <see cref="FinalizeExpression"/> was invoked.</summary>
        public bool FinalizeInvoked { get; private set; }

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>Declared first; always defers via <see langword="null"/>.</summary>
        [ExpressionSignature(ExpressionType.AndAlso)]
        private Expression? First(BinaryExpression e, Expression left, Expression right)
        {
            FirstInvoked = true;
            FirstLeft = left;
            FirstRight = right;
            return null;
        }

        /// <summary>Declared second; always wins.</summary>
        [ExpressionSignature(ExpressionType.AndAlso)]
        private Expression Second(BinaryExpression e, Expression left, Expression right)
        {
            SecondInvoked = true;
            SecondLeft = left;
            SecondRight = right;
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
    /// When a first positional binary rule defers via <see langword="null"/> and a second one wins, both
    /// must observe the exact same prepared <c>left</c>/<c>right</c> instances, and
    /// <see cref="ExpressionTransformer.FinalizeExpression"/> must never run — this is the positive
    /// performance scenario where no <see cref="Expression"/>[2] should ever be necessary.
    /// </summary>
    [TestMethod]
    public void PositionalRuleReturningNull_ThenSecondRuleSucceeds_BothObserveSamePreparedOperands()
    {
        ParameterExpression x = Expression.Parameter(typeof(bool), "x");
        ParameterExpression y = Expression.Parameter(typeof(bool), "y");
        var transformer = new PositionalNullThenSuccessTransformer();
        BinaryExpression andAlso = Expression.AndAlso(x, y);

        transformer.ExposeTransform(andAlso);

        Assert.IsTrue(transformer.FirstInvoked);
        Assert.IsTrue(transformer.SecondInvoked);
        Assert.AreSame(transformer.FirstLeft, transformer.SecondLeft);
        Assert.AreSame(transformer.FirstRight, transformer.SecondRight);
        Assert.AreSame(x, transformer.FirstLeft);
        Assert.AreSame(y, transformer.FirstRight);
        Assert.IsFalse(transformer.FinalizeInvoked, "A winning positional rule must short-circuit before Finalize.");
    }

    // ---------------------------------------------------------------------------------------------
    // Test I — fallback after positional rules still receives prepared operands.
    // ---------------------------------------------------------------------------------------------

    /// <summary>A transformer whose single positional binary rule always defers, so Finalize always runs.</summary>
    private sealed class PositionalAlwaysNullThenFinalizeTransformer : ExpressionTransformer
    {
        /// <summary>The <c>left</c> argument the rule was invoked with.</summary>
        public Expression? RuleLeft { get; private set; }

        /// <summary>The <c>right</c> argument the rule was invoked with.</summary>
        public Expression? RuleRight { get; private set; }

        /// <summary>Slot 0 of the array <see cref="FinalizeExpression"/> was invoked with.</summary>
        public Expression? FinalizeLeft { get; private set; }

        /// <summary>Slot 1 of the array <see cref="FinalizeExpression"/> was invoked with.</summary>
        public Expression? FinalizeRight { get; private set; }

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>Always defers via <see langword="null"/>.</summary>
        [ExpressionSignature(ExpressionType.AndAlso)]
        private Expression? Rule(BinaryExpression e, Expression left, Expression right)
        {
            RuleLeft = left;
            RuleRight = right;
            return null;
        }

        /// <inheritdoc />
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
        {
            FinalizeLeft = parameters[0];
            FinalizeRight = parameters[1];
            return CopyExpression(e, parameters);
        }
    }

    /// <summary>
    /// When every positional binary rule defers via <see langword="null"/>,
    /// <see cref="ExpressionTransformer.FinalizeExpression"/> must receive the same logical prepared
    /// <c>left</c>/<c>right</c> the rule itself observed — built directly from the prepared node's
    /// <c>Left</c>/<c>Right</c>, not from the separate reflection invocation argument array.
    /// </summary>
    [TestMethod]
    public void PositionalRuleReturningNull_NoLaterWinner_FinalizeReceivesSamePreparedOperands()
    {
        ParameterExpression x = Expression.Parameter(typeof(bool), "x");
        ParameterExpression y = Expression.Parameter(typeof(bool), "y");
        var transformer = new PositionalAlwaysNullThenFinalizeTransformer();
        BinaryExpression andAlso = Expression.AndAlso(x, y);

        transformer.ExposeTransform(andAlso);

        Assert.AreSame(x, transformer.RuleLeft);
        Assert.AreSame(y, transformer.RuleRight);
        Assert.AreSame(transformer.RuleLeft, transformer.FinalizeLeft);
        Assert.AreSame(transformer.RuleRight, transformer.FinalizeRight);
    }

    // ---------------------------------------------------------------------------------------------
    // Binary metadata preservation.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A lifted nullable binary operator's <see cref="BinaryExpression.IsLiftedToNull"/> metadata must
    /// survive <c>PrepareBinary</c>'s reconstruction, exactly like <see cref="BinaryExpression.Method"/>
    /// (covered by <c>Power_WithExplicitFloatMethod_PreservesMethod</c> in
    /// <c>ExpressionTransformerTests</c>) and <see cref="BinaryExpression.Conversion"/> (covered by
    /// <c>Simplify_CoalesceWithConversionLambda_PreservesConversion</c>).
    /// </summary>
    [TestMethod]
    public void NoMatchingRule_LiftedNullableBinary_PreservesIsLiftedToNull()
    {
        ParameterExpression a = Expression.Parameter(typeof(int?), "a");
        ParameterExpression b = Expression.Parameter(typeof(int?), "b");
        BinaryExpression addLifted = Expression.Add(a, b);
        Assert.IsTrue(addLifted.IsLiftedToNull,
            "Precondition: Expression.Add on two nullable int operands must produce a lifted-to-null node.");

        var transformer = new BinaryFinalizeObservingTransformer();
        Expression result = transformer.ExposeTransform(addLifted);

        var resultBinary = result as BinaryExpression;
        Assert.IsNotNull(resultBinary);
        Assert.IsTrue(resultBinary!.IsLiftedToNull,
            "IsLiftedToNull metadata must survive PrepareBinary's reconstruction via CopyBinaryExpression.");
    }
}
