using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Tests of the declarative model of <see cref="FlattenAttribute"/> and <see cref="FlattenUnaryAttribute"/>: the extraction
/// only applies the declared couples (direct operation, inverse operation) and unary operators with their bindings, so changing
/// the declarations alone changes the terms. Each probe differs from the others by its attributes only; a hard-coded knowledge
/// of the historical additive and multiplicative families makes these tests fail.
/// </summary>
[TestClass]
public class ExpressionTransformerFlattenDeclarationTests
{
    private static readonly ParameterExpression A = Expression.Parameter(typeof(int), "a");
    private static readonly ParameterExpression B = Expression.Parameter(typeof(int), "b");
    private static readonly ParameterExpression C = Expression.Parameter(typeof(int), "c");
    private static readonly ParameterExpression D = Expression.Parameter(typeof(int), "d");

    /// <summary>
    /// The base of the probes: it records the terms of every chain its rules receive and declines (or applies
    /// <see cref="Accept"/>), and copies every node otherwise.
    /// </summary>
    private abstract class Probe : ExpressionTransformer
    {
        /// <summary>Gets the chains received, rendered as <c>Operation:operand</c> terms.</summary>
        public List<string> Received { get; } = [];

        /// <summary>Gets or sets what the rules return (<see langword="null"/>: decline).</summary>
        public Func<Probe, Expression, FlattenedTerm[], Expression?> Accept { get; set; } = static (_, _, _) => null;

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <summary>Exposes <see cref="ExpressionTransformer.RebuildFlattenedChain"/> to the tests.</summary>
        /// <param name="source">The root of a chain.</param>
        /// <param name="operands">The new operands.</param>
        /// <returns>The rebuilt chain.</returns>
        public Expression Rebuild(Expression source, IReadOnlyList<Expression> operands) => RebuildFlattenedChain(source, operands);

        /// <inheritdoc/>
        protected override Expression PrepareExpression(Expression e) => TransformCore(e);

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => CopyExpression(e, parameters);

        /// <summary>Records a chain and applies <see cref="Accept"/>.</summary>
        /// <param name="source">The root of the chain.</param>
        /// <param name="terms">The terms.</param>
        /// <returns>The result of <see cref="Accept"/>.</returns>
        protected Expression? Record(Expression source, FlattenedTerm[] terms)
        {
            Received.Add(Render(terms));
            return Accept(this, source, terms);
        }
    }

    /// <summary>Renders terms as <c>Operation:operand</c>, whatever the operations.</summary>
    /// <param name="terms">The terms.</param>
    /// <returns>The rendering.</returns>
    private static string Render(IEnumerable<FlattenedTerm> terms)
        => string.Join(" ", terms.Select(static term => $"{term.Operation}:{term.Operand}"));

    /// <summary><c>Add</c> direct, <c>Subtract</c> inverse.</summary>
    private sealed class AddSubtract : Probe
    {
        /// <summary>The rule.</summary>
        /// <param name="source">The root.</param>
        /// <param name="terms">The terms.</param>
        /// <returns>See <see cref="Probe.Record"/>.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract)]
        private Expression? R(Expression source, FlattenedTerm[] terms) => Record(source, terms);
    }

    /// <summary>The same operators with swapped roles: <c>Subtract</c> direct, <c>Add</c> inverse.</summary>
    private sealed class SubtractAdd : Probe
    {
        /// <summary>The rule.</summary>
        /// <param name="source">The root.</param>
        /// <param name="terms">The terms.</param>
        /// <returns>See <see cref="Probe.Record"/>.</returns>
        [Flatten(ExpressionType.Subtract, ExpressionType.Add)]
        private Expression? R(Expression source, FlattenedTerm[] terms) => Record(source, terms);
    }

    /// <summary><c>Or</c> direct, <c>ExclusiveOr</c> inverse.</summary>
    private sealed class OrExclusiveOr : Probe
    {
        /// <summary>The rule.</summary>
        /// <param name="source">The root.</param>
        /// <param name="terms">The terms.</param>
        /// <returns>See <see cref="Probe.Record"/>.</returns>
        [Flatten(ExpressionType.Or, ExpressionType.ExclusiveOr)]
        private Expression? R(Expression source, FlattenedTerm[] terms) => Record(source, terms);
    }

    /// <summary><c>And</c> direct, <c>Or</c> inverse (the same <c>Or</c> as <see cref="OrExclusiveOr"/>, in the other role).</summary>
    private sealed class AndOr : Probe
    {
        /// <summary>The rule.</summary>
        /// <param name="source">The root.</param>
        /// <param name="terms">The terms.</param>
        /// <returns>See <see cref="Probe.Record"/>.</returns>
        [Flatten(ExpressionType.And, ExpressionType.Or)]
        private Expression? R(Expression source, FlattenedTerm[] terms) => Record(source, terms);
    }

    /// <summary><see cref="AndOr"/> with <c>OnesComplement</c> as a unary inverter.</summary>
    private sealed class AndOrComplement : Probe
    {
        /// <summary>The rule.</summary>
        /// <param name="source">The root.</param>
        /// <param name="terms">The terms.</param>
        /// <returns>See <see cref="Probe.Record"/>.</returns>
        [Flatten(ExpressionType.And, ExpressionType.Or, ExpressionType.OnesComplement)]
        private Expression? R(Expression source, FlattenedTerm[] terms) => Record(source, terms);
    }

    /// <summary><c>Multiply</c> and <c>Divide</c> without inverter (the historical multiplicative declaration).</summary>
    private sealed class MultiplyDivide : Probe
    {
        /// <summary>The rule.</summary>
        /// <param name="source">The root.</param>
        /// <param name="terms">The terms.</param>
        /// <returns>See <see cref="Probe.Record"/>.</returns>
        [Flatten(ExpressionType.Multiply, ExpressionType.Divide)]
        private Expression? R(Expression source, FlattenedTerm[] terms) => Record(source, terms);
    }

    /// <summary><c>Multiply</c> and <c>Divide</c> with <c>Negate</c> as a unary inverter, a pairing the historical families never had.</summary>
    private sealed class MultiplyDivideNegate : Probe
    {
        /// <summary>The rule.</summary>
        /// <param name="source">The root.</param>
        /// <param name="terms">The terms.</param>
        /// <returns>See <see cref="Probe.Record"/>.</returns>
        [Flatten(ExpressionType.Multiply, ExpressionType.Divide, ExpressionType.Negate)]
        private Expression? R(Expression source, FlattenedTerm[] terms) => Record(source, terms);
    }

    /// <summary><c>Divide</c> as a direct operation alone.</summary>
    private sealed class DivideAlone : Probe
    {
        /// <summary>The rule.</summary>
        /// <param name="source">The root.</param>
        /// <param name="terms">The terms.</param>
        /// <returns>See <see cref="Probe.Record"/>.</returns>
        [Flatten(ExpressionType.Divide)]
        private Expression? R(Expression source, FlattenedTerm[] terms) => Record(source, terms);
    }

    /// <summary>Two couples, without inverter.</summary>
    private sealed class TwoCouples : Probe
    {
        /// <summary>The rule.</summary>
        /// <param name="source">The root.</param>
        /// <param name="terms">The terms.</param>
        /// <returns>See <see cref="Probe.Record"/>.</returns>
        [Flatten([ExpressionType.Add, ExpressionType.Multiply], [ExpressionType.Subtract, ExpressionType.Divide])]
        private Expression? R(Expression source, FlattenedTerm[] terms) => Record(source, terms);
    }

    /// <summary>Two couples, <c>Negate</c> bound to the couple of its operand.</summary>
    private sealed class TwoCouplesNegateOperand : Probe
    {
        /// <summary>The rule.</summary>
        /// <param name="source">The root.</param>
        /// <param name="terms">The terms.</param>
        /// <returns>See <see cref="Probe.Record"/>.</returns>
        [Flatten([ExpressionType.Add, ExpressionType.Multiply], [ExpressionType.Subtract, ExpressionType.Divide])]
        [FlattenUnary(ExpressionType.Negate, UnaryOperatorBinding.OperandGroup)]
        private Expression? R(Expression source, FlattenedTerm[] terms) => Record(source, terms);
    }

    /// <summary>The same two couples, <c>Negate</c> bound to the first couple (here the multiplicative one).</summary>
    private sealed class TwoCouplesNegateFirst : Probe
    {
        /// <summary>The rule.</summary>
        /// <param name="source">The root.</param>
        /// <param name="terms">The terms.</param>
        /// <returns>See <see cref="Probe.Record"/>.</returns>
        [Flatten([ExpressionType.Multiply, ExpressionType.Add], [ExpressionType.Divide, ExpressionType.Subtract])]
        [FlattenUnary(ExpressionType.Negate, UnaryOperatorBinding.FirstGroup)]
        private Expression? R(Expression source, FlattenedTerm[] terms) => Record(source, terms);
    }

    /// <summary>One couple, <c>Negate</c> bound to the couple of its operand.</summary>
    private sealed class AddSubtractNegateOperand : Probe
    {
        /// <summary>The rule.</summary>
        /// <param name="source">The root.</param>
        /// <param name="terms">The terms.</param>
        /// <returns>See <see cref="Probe.Record"/>.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract)]
        [FlattenUnary(ExpressionType.Negate, UnaryOperatorBinding.OperandGroup)]
        private Expression? R(Expression source, FlattenedTerm[] terms) => Record(source, terms);
    }

    /// <summary>One couple, <c>Negate</c> bound to it by <see cref="FlattenUnaryAttribute"/>, as the simplified constructor does.</summary>
    private sealed class AddSubtractNegateFirst : Probe
    {
        /// <summary>The rule.</summary>
        /// <param name="source">The root.</param>
        /// <param name="terms">The terms.</param>
        /// <returns>See <see cref="Probe.Record"/>.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract)]
        [FlattenUnary(ExpressionType.Negate, UnaryOperatorBinding.FirstGroup)]
        private Expression? R(Expression source, FlattenedTerm[] terms) => Record(source, terms);
    }

    /// <summary>The historical additive declaration, through the simplified constructor (<c>Negate</c> bound to the first couple).</summary>
    private sealed class AddSubtractNegate : Probe
    {
        /// <summary>The rule.</summary>
        /// <param name="source">The root.</param>
        /// <param name="terms">The terms.</param>
        /// <returns>See <see cref="Probe.Record"/>.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract, ExpressionType.Negate)]
        private Expression? R(Expression source, FlattenedTerm[] terms) => Record(source, terms);
    }

    /// <summary>
    /// Two rules of two configurations sharing <c>Negate</c> through its operand, and a prefix of two operators bound through
    /// their operand to different couples: <c>UnaryPlus</c> for the additive one only.
    /// </summary>
    private sealed class SharedOperandUnary : Probe
    {
        /// <summary>The additive rule.</summary>
        /// <param name="source">The root.</param>
        /// <param name="terms">The terms.</param>
        /// <returns>See <see cref="Probe.Record"/>.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract)]
        [FlattenUnary(ExpressionType.Negate, UnaryOperatorBinding.OperandGroup)]
        [FlattenUnary(ExpressionType.UnaryPlus, UnaryOperatorBinding.OperandGroup)]
        private Expression? Additions(Expression source, FlattenedTerm[] terms) => Record(source, terms);

        /// <summary>The multiplicative rule.</summary>
        /// <param name="source">The root.</param>
        /// <param name="terms">The terms.</param>
        /// <returns>See <see cref="Probe.Record"/>.</returns>
        [Flatten(ExpressionType.Multiply, ExpressionType.Divide)]
        [FlattenUnary(ExpressionType.Negate, UnaryOperatorBinding.OperandGroup)]
        private Expression? Products(Expression source, FlattenedTerm[] terms) => Record(source, terms);
    }

    /// <summary>Two couples, <c>UnaryPlus</c> bound to the first one and <c>Negate</c> to the couple of its operand.</summary>
    private sealed class MixedBindings : Probe
    {
        /// <summary>The rule.</summary>
        /// <param name="source">The root.</param>
        /// <param name="terms">The terms.</param>
        /// <returns>See <see cref="Probe.Record"/>.</returns>
        [Flatten([ExpressionType.Add, ExpressionType.Multiply], [ExpressionType.Subtract, ExpressionType.Divide])]
        [FlattenUnary(ExpressionType.Negate, UnaryOperatorBinding.OperandGroup)]
        [FlattenUnary(ExpressionType.UnaryPlus, UnaryOperatorBinding.FirstGroup)]
        private Expression? R(Expression source, FlattenedTerm[] terms) => Record(source, terms);
    }

    /// <summary>Asserts the chains a fresh probe receives for <paramref name="expression"/>.</summary>
    /// <typeparam name="TProbe">The probe type.</typeparam>
    /// <param name="expression">The expression.</param>
    /// <param name="expected">The expected chains, in the order they are offered.</param>
    private static void AssertChains<TProbe>(Expression expression, params string[] expected)
        where TProbe : Probe, new()
    {
        var probe = new TProbe();
        probe.Transform(expression);
        CollectionAssert.AreEqual(expected, probe.Received, $"{typeof(TProbe).Name}: {expression}");
    }

    /// <summary>
    /// The same node types give different terms when only the roles change: with <c>Subtract</c> direct and <c>Add</c>
    /// inverse, an addition inverts its right operand and a subtraction passes the context to both.
    /// </summary>
    [TestMethod]
    public void SwappedRoles_ChangeTheTerms()
    {
        Expression sum = Expression.Add(A, B);
        Expression difference = Expression.Subtract(A, B);
        Expression nested = Expression.Add(A, Expression.Add(B, C));
        Expression mixed = Expression.Subtract(A, Expression.Subtract(B, C));

        AssertChains<AddSubtract>(sum, "Add:a Add:b");
        AssertChains<SubtractAdd>(sum, "Subtract:a Add:b");
        AssertChains<AddSubtract>(difference, "Add:a Subtract:b");
        AssertChains<SubtractAdd>(difference, "Subtract:a Subtract:b");
        AssertChains<AddSubtract>(nested, "Add:a Add:b Add:c");
        AssertChains<SubtractAdd>(nested, "Subtract:a Add:b Subtract:c");
        AssertChains<AddSubtract>(mixed, "Add:a Subtract:b Add:c");
        AssertChains<SubtractAdd>(mixed, "Subtract:a Subtract:b Subtract:c");
    }

    /// <summary>
    /// Operators the historical families never knew are flattened as declared, and one same binary operator (<c>Or</c>)
    /// passes the context to both operands when it is the direct operation and inverts the right one when it is the inverse.
    /// </summary>
    [TestMethod]
    public void SameOperator_TransmitsOrInverts_AsDeclared()
    {
        AssertChains<OrExclusiveOr>(Expression.Or(A, Expression.Or(B, C)), "Or:a Or:b Or:c");
        AssertChains<AndOr>(Expression.Or(A, Expression.Or(B, C)), "And:a Or:b And:c");
        AssertChains<OrExclusiveOr>(Expression.Or(A, Expression.ExclusiveOr(B, C)), "Or:a Or:b ExclusiveOr:c");
        AssertChains<AndOr>(Expression.And(A, Expression.Or(B, C)), "And:a And:b Or:c");
        AssertChains<AndOr>(Expression.Or(Expression.And(A, B), Expression.And(C, D)), "And:a And:b Or:c Or:d");
    }

    /// <summary>
    /// A unary inverter is crossed only where it is declared: <c>OnesComplement</c> inverts the context of its operand, two
    /// complements cancel out, and <c>Negate</c> declared with <c>Multiply</c> and <c>Divide</c> gives inverse terms, while
    /// the same negation is an operand of the declaration without inverter.
    /// </summary>
    [TestMethod]
    public void UnaryInverters_InvertTheirOperand_OnlyWhereDeclared()
    {
        Expression complement = Expression.OnesComplement(Expression.And(A, B));
        AssertChains<AndOrComplement>(complement, "Or:a Or:b");
        AssertChains<AndOrComplement>(Expression.OnesComplement(Expression.OnesComplement(A)), "And:a");
        AssertChains<AndOrComplement>(Expression.And(A, Expression.OnesComplement(Expression.Or(B, C))), "And:a Or:b And:c");
        AssertChains<AndOr>(complement, "And:a And:b");

        AssertChains<MultiplyDivideNegate>(Expression.Negate(Expression.Multiply(A, B)), "Divide:a Divide:b");
        AssertChains<MultiplyDivideNegate>(Expression.Multiply(A, Expression.Negate(B)), "Multiply:a Divide:b");
        AssertChains<MultiplyDivide>(Expression.Multiply(A, Expression.Negate(B)), "Multiply:a Multiply:-b");
    }

    /// <summary>
    /// Operations a historical family crossed are operands when they are not declared: a negation without inverter, and
    /// <c>Multiply</c> when <c>Divide</c> is declared alone, whose terms then all carry <c>Divide</c>.
    /// </summary>
    [TestMethod]
    public void UndeclaredHistoricalOperations_AreOperands()
    {
        AssertChains<AddSubtract>(Expression.Subtract(A, Expression.Negate(B)), "Add:a Subtract:-b");
        AssertChains<AddSubtract>(Expression.Negate(Expression.Add(A, B)), "Add:a Add:b");
        AssertChains<DivideAlone>(Expression.Divide(A, Expression.Divide(B, C)), "Divide:a Divide:b Divide:c");
        AssertChains<DivideAlone>(Expression.Multiply(A, B));
        AssertChains<DivideAlone>(Expression.Divide(Expression.Multiply(A, B), C), "Divide:(a * b) Divide:c");
    }

    /// <summary>
    /// Several couples declared by one rule are independent chains: an operator of one couple is an operand of a chain of the
    /// other, offered as a chain of its own when the historical fallback reaches it.
    /// </summary>
    [TestMethod]
    public void SeveralCouples_AreIndependentChains()
    {
        AssertChains<TwoCouples>(Expression.Add(A, Expression.Multiply(B, C)), "Add:a Add:(b * c)", "Multiply:b Multiply:c");
        AssertChains<TwoCouples>(Expression.Divide(A, Expression.Subtract(B, C)), "Multiply:a Divide:(b - c)", "Add:b Subtract:c");
        AssertChains<TwoCouples>(Expression.Subtract(Expression.Divide(A, B), Expression.Multiply(C, D)), "Add:(a / b) Subtract:(c * d)", "Multiply:a Divide:b", "Multiply:c Multiply:d");
    }

    /// <summary>
    /// A unary operator bound with <see cref="UnaryOperatorBinding.FirstGroup"/> belongs to the first couple of its declaration,
    /// whatever that couple is: it roots a chain of it even over a leaf, is crossed by its chains, and is an operand of the
    /// chains of the other couple. The simplified constructor and <see cref="FlattenUnaryAttribute"/> give the same declaration.
    /// </summary>
    [TestMethod]
    public void FirstGroup_BelongsToTheFirstCouple()
    {
        AssertChains<TwoCouplesNegateFirst>(Expression.Negate(B), "Divide:b");
        AssertChains<TwoCouplesNegateFirst>(Expression.Multiply(A, Expression.Negate(B)), "Multiply:a Divide:b");
        AssertChains<TwoCouplesNegateFirst>(Expression.Add(A, Expression.Negate(B)), "Add:a Add:-b", "Divide:b");
        AssertChains<TwoCouplesNegateFirst>(Expression.Negate(Expression.Add(A, B)), "Divide:(a + b)", "Add:a Add:b");

        foreach (Func<Probe> create in new Func<Probe>[] { static () => new AddSubtractNegate(), static () => new AddSubtractNegateFirst() })
        {
            Probe probe = create();
            probe.Transform(Expression.Negate(B));
            probe.Transform(Expression.Add(A, Expression.Negate(B)));
            probe.Transform(Expression.Negate(Expression.Negate(A)));
            probe.Transform(Expression.Negate(Expression.Subtract(A, Expression.Subtract(B, C))));
            CollectionAssert.AreEqual(new[] { "Subtract:b", "Add:a Subtract:b", "Add:a", "Subtract:a Add:b Subtract:c" }, probe.Received, probe.GetType().Name);
        }
    }

    /// <summary>
    /// A unary operator bound with <see cref="UnaryOperatorBinding.OperandGroup"/> belongs to the couple its operand alone
    /// identifies: it is crossed by that couple's chain and roots one, even with several couples declared, while over an
    /// operand that identifies no couple it keeps the historical behavior (no chain, an operand of the enclosing chain). Two
    /// couples are never merged.
    /// </summary>
    [TestMethod]
    public void OperandGroup_IsDecidedByTheOperandAlone()
    {
        AssertChains<TwoCouplesNegateOperand>(Expression.Negate(B));
        AssertChains<TwoCouplesNegateOperand>(Expression.Negate(Expression.Negate(A)));
        AssertChains<TwoCouplesNegateOperand>(Expression.Multiply(A, Expression.Negate(B)), "Multiply:a Multiply:-b");
        AssertChains<TwoCouplesNegateOperand>(Expression.Add(A, Expression.Negate(B)), "Add:a Add:-b");
        AssertChains<TwoCouplesNegateOperand>(Expression.Negate(Expression.Multiply(A, B)), "Divide:a Divide:b");
        AssertChains<TwoCouplesNegateOperand>(Expression.Negate(Expression.Add(A, B)), "Subtract:a Subtract:b");
        AssertChains<TwoCouplesNegateOperand>(Expression.Negate(Expression.Negate(Expression.Multiply(A, B))), "Multiply:a Multiply:b");
        AssertChains<TwoCouplesNegateOperand>(Expression.Multiply(A, Expression.Negate(Expression.Divide(B, C))), "Multiply:a Divide:b Multiply:c");
        AssertChains<TwoCouplesNegateOperand>(Expression.Multiply(A, Expression.Negate(Expression.Add(B, C))), "Multiply:a Multiply:-(b + c)", "Subtract:b Subtract:c");
        AssertChains<AddSubtractNegateOperand>(Expression.Subtract(A, Expression.Negate(Expression.Subtract(B, C))), "Add:a Add:b Subtract:c");
        AssertChains<AddSubtractNegateOperand>(Expression.Add(A, Expression.Negate(B)), "Add:a Add:-b");

        var probe = new TwoCouplesNegateOperand();
        Assert.ThrowsExactly<ArgumentException>(() => probe.Rebuild(Expression.Negate(B), [B]));
        Expression negatedProduct = Expression.Negate(Expression.Multiply(A, B));
        Assert.AreSame(negatedProduct, probe.Rebuild(negatedProduct, [A, B]));
    }

    /// <summary>
    /// Configurations of several rules may share a unary operator bound through its operand: each node goes to the rule whose
    /// couple its operand identifies, and a prefix of such operators belongs to a couple only when every one of them declares
    /// it. A prefix mixing both bindings follows the operator bound to its couple.
    /// </summary>
    [TestMethod]
    public void OperandGroup_SharedByConfigurations_DispatchesDeterministically()
    {
        AssertChains<SharedOperandUnary>(Expression.Negate(Expression.Multiply(A, B)), "Divide:a Divide:b");
        AssertChains<SharedOperandUnary>(Expression.Negate(Expression.Add(A, B)), "Subtract:a Subtract:b");
        AssertChains<SharedOperandUnary>(Expression.Multiply(A, Expression.Negate(Expression.Add(B, C))), "Multiply:a Multiply:-(b + c)", "Subtract:b Subtract:c");
        AssertChains<SharedOperandUnary>(Expression.Negate(Expression.UnaryPlus(Expression.Add(A, B))), "Add:a Add:b");
        AssertChains<SharedOperandUnary>(Expression.Negate(Expression.UnaryPlus(Expression.Multiply(A, B))), "Multiply:a Multiply:b");

        AssertChains<MixedBindings>(Expression.UnaryPlus(B), "Subtract:b");
        AssertChains<MixedBindings>(Expression.Negate(Expression.UnaryPlus(B)), "Add:b");
        AssertChains<MixedBindings>(Expression.Negate(Expression.UnaryPlus(Expression.Multiply(A, B))), "Add:(a * b)", "Multiply:a Multiply:b");
    }

    /// <summary>The base of the inheritance probes: two couples and <c>Negate</c> bound to the couple of its operand.</summary>
    private abstract class InheritedConfiguration : Probe
    {
        /// <summary>The rule.</summary>
        /// <param name="source">The root.</param>
        /// <param name="terms">The terms.</param>
        /// <returns>See <see cref="Probe.Record"/>.</returns>
        [Flatten([ExpressionType.Add, ExpressionType.Multiply], [ExpressionType.Subtract, ExpressionType.Divide])]
        [FlattenUnary(ExpressionType.Negate, UnaryOperatorBinding.OperandGroup)]
        protected virtual Expression? R(Expression source, FlattenedTerm[] terms) => Record(source, terms);
    }

    /// <summary>An override that declares nothing: the whole configuration is inherited.</summary>
    private sealed class InheritsTheConfiguration : InheritedConfiguration
    {
        /// <inheritdoc/>
        protected override Expression? R(Expression source, FlattenedTerm[] terms) => base.R(source, terms);
    }

    /// <summary>An intermediate override that declares nothing.</summary>
    private class Intermediate : InheritedConfiguration
    {
        /// <inheritdoc/>
        protected override Expression? R(Expression source, FlattenedTerm[] terms) => base.R(source, terms);
    }

    /// <summary>An override of <see cref="Intermediate"/> that redeclares the couple without unary operator.</summary>
    private sealed class RedeclaresTheCouple : Intermediate
    {
        /// <inheritdoc/>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract)]
        protected override Expression? R(Expression source, FlattenedTerm[] terms) => base.R(source, terms);
    }

    /// <summary>An override that redeclares the couple with its own unary operator.</summary>
    private sealed class RedeclaresWithItsOwnUnary : InheritedConfiguration
    {
        /// <inheritdoc/>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract)]
        [FlattenUnary(ExpressionType.UnaryPlus, UnaryOperatorBinding.FirstGroup)]
        protected override Expression? R(Expression source, FlattenedTerm[] terms) => base.R(source, terms);
    }

    /// <summary>
    /// The unary operators belong to the effective <see cref="FlattenAttribute"/>: an override that declares nothing keeps the
    /// whole configuration, one that declares a new <see cref="FlattenAttribute"/> replaces it entirely, and the operators of
    /// the overridden declaration are never merged into the new one.
    /// </summary>
    [TestMethod]
    public void InheritedDeclarations_AreBoundToTheEffectiveFlatten()
    {
        AssertChains<InheritsTheConfiguration>(Expression.Negate(Expression.Multiply(A, B)), "Divide:a Divide:b");
        AssertChains<InheritsTheConfiguration>(Expression.Negate(B));

        AssertChains<RedeclaresTheCouple>(Expression.Negate(Expression.Add(A, B)), "Add:a Add:b");
        AssertChains<RedeclaresTheCouple>(Expression.Multiply(A, B));

        AssertChains<RedeclaresWithItsOwnUnary>(Expression.UnaryPlus(Expression.Add(A, B)), "Subtract:a Subtract:b");
        AssertChains<RedeclaresWithItsOwnUnary>(Expression.Negate(Expression.Add(A, B)), "Add:a Add:b");
        AssertChains<RedeclaresWithItsOwnUnary>(Expression.UnaryPlus(B), "Subtract:b");
    }

    /// <summary>
    /// The faithful reconstruction keeps the source operators, grouping and types for any declared composition, and returns
    /// the source itself with the original operands.
    /// </summary>
    [TestMethod]
    public void Rebuild_IsFaithful_ForAnyDeclaredComposition()
    {
        ParameterExpression[] originals = [A, B, C, D];
        ParameterExpression[] renamed = [.. "ABCD".Select(static n => Expression.Parameter(typeof(int), n.ToString()))];
        (Func<Probe> Create, Expression Expression)[] cases =
        [
            (static () => new SubtractAdd(), Expression.Add(A, Expression.Subtract(B, Expression.Add(C, D)))),
            (static () => new AndOrComplement(), Expression.OnesComplement(Expression.Or(Expression.And(A, B), Expression.OnesComplement(C)))),
            (static () => new MultiplyDivideNegate(), Expression.Divide(Expression.Negate(A), Expression.Multiply(B, Expression.Negate(C)))),
            (static () => new TwoCouplesNegateOperand(), Expression.Multiply(A, Expression.Negate(Expression.Divide(B, C)))),
            (static () => new TwoCouplesNegateOperand(), Expression.Negate(Expression.Negate(Expression.Add(A, Expression.Subtract(B, C))))),
            (static () => new MixedBindings(), Expression.Negate(Expression.UnaryPlus(Expression.Subtract(A, B)))),
        ];

        foreach ((Func<Probe> create, Expression expression) in cases)
        {
            Expression? identity = null;
            Expression? rebuilt = null;
            Probe probe = create();
            probe.Accept = (p, source, terms) =>
            {
                identity = p.Rebuild(source, [.. terms.Select(static term => term.Operand)]);
                rebuilt = p.Rebuild(source, [.. terms.Select(term => (Expression)renamed[System.Array.IndexOf(originals, (ParameterExpression)term.Operand)])]);
                return source;
            };

            probe.Transform(expression);

            Assert.AreSame(expression, identity, expression.ToString());
            Assert.IsNotNull(rebuilt, expression.ToString());
            Assert.AreEqual(expression.ToString().ToUpperInvariant(), rebuilt.ToString(), expression.ToString());
            Assert.AreEqual(expression.Type, rebuilt.Type, expression.ToString());
        }
    }

    /// <summary>A transformer with two independent compositions declared by two rules, one of them a direct operation alone.</summary>
    private sealed class PartialCompositions : Probe
    {
        /// <summary>The additive rule.</summary>
        /// <param name="source">The root.</param>
        /// <param name="terms">The terms.</param>
        /// <returns>See <see cref="Probe.Record"/>.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract)]
        private Expression? Additions(Expression source, FlattenedTerm[] terms) => Record(source, terms);

        /// <summary>The multiplicative rule, without inverse.</summary>
        /// <param name="source">The root.</param>
        /// <param name="terms">The terms.</param>
        /// <returns>See <see cref="Probe.Record"/>.</returns>
        [Flatten(ExpressionType.Multiply)]
        private Expression? Products(Expression source, FlattenedTerm[] terms) => Record(source, terms);
    }

    /// <summary>Partial compositions coexist: a division is an operand of the product chain, which carries only <c>Multiply</c>.</summary>
    [TestMethod]
    public void PartialCompositions_Coexist()
    {
        AssertChains<PartialCompositions>(Expression.Multiply(A, Expression.Divide(B, Expression.Multiply(C, D))), "Multiply:a Multiply:(b / (c * d))", "Multiply:c Multiply:d");
        AssertChains<PartialCompositions>(Expression.Subtract(A, Expression.Multiply(B, C)), "Add:a Subtract:(b * c)", "Multiply:b Multiply:c");
    }

    /// <summary>Two rules of one configuration, whose unary operators are declared differently, share one group.</summary>
    private sealed class SameCompositionTwice : Probe
    {
        /// <summary>The first rule.</summary>
        /// <param name="source">The root.</param>
        /// <param name="terms">The terms.</param>
        /// <returns>See <see cref="Probe.Record"/>.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract, ExpressionType.Negate)]
        [FlattenUnary(ExpressionType.UnaryPlus, UnaryOperatorBinding.FirstGroup)]
        private Expression? First(Expression source, FlattenedTerm[] terms) => Record(source, terms);

        /// <summary>The second rule.</summary>
        /// <param name="source">The root.</param>
        /// <param name="terms">The terms.</param>
        /// <returns>See <see cref="Probe.Record"/>.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract)]
        [FlattenUnary(ExpressionType.UnaryPlus, UnaryOperatorBinding.FirstGroup)]
        [FlattenUnary(ExpressionType.Negate, UnaryOperatorBinding.FirstGroup)]
        private Expression? Second(Expression source, FlattenedTerm[] terms) => Record(source, terms);
    }

    /// <summary>
    /// The rules of one composition are one group, tried in order on one extraction; a unary operator declared as an inverter
    /// inverts, whatever its usual meaning (<c>UnaryPlus</c> here).
    /// </summary>
    [TestMethod]
    public void SameComposition_IsOneGroup_AndInvertersFollowTheirDeclaration()
    {
        AssertChains<SameCompositionTwice>(Expression.UnaryPlus(Expression.Subtract(A, B)), "Subtract:a Add:b", "Subtract:a Add:b");
    }

    /// <summary>A unary node without operand (a rethrow) of a declared inverter is an operand, never crossed.</summary>
    private sealed class ThrowInverter : Probe
    {
        /// <summary>The rule.</summary>
        /// <param name="source">The root.</param>
        /// <param name="terms">The terms.</param>
        /// <returns>See <see cref="Probe.Record"/>.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract, ExpressionType.Throw)]
        private Expression? R(Expression source, FlattenedTerm[] terms) => Record(source, terms);
    }

    /// <summary>See <see cref="ThrowInverter"/>.</summary>
    [TestMethod]
    public void UnaryNodeWithoutOperand_IsAnOperand()
    {
        Expression rethrow = Expression.Rethrow(typeof(int));
        var probe = new ThrowInverter();
        probe.Transform(Expression.Add(A, rethrow));
        CollectionAssert.AreEqual(new[] { "Add:a Add:" + rethrow }, probe.Received);

        probe.Received.Clear();
        probe.Transform(rethrow);
        Assert.AreEqual(0, probe.Received.Count);
    }

    /// <summary>
    /// The trace identity of a rule lists every declared operator, couple by couple, then the unary operator of the simplified
    /// constructor, then those of <see cref="FlattenUnaryAttribute"/> by node type value.
    /// </summary>
    [TestMethod]
    public void TraceIdentity_ListsTheDeclaredOperators()
    {
        var collector = new ExpressionTraceCollector();
        using (ExpressionTransformTracing.Begin(collector, ExpressionTraceDetail.Detailed))
        {
            new TwoCouplesNegateOperand { Accept = static (_, source, _) => source }.Transform(Expression.Add(A, B));
            new MixedBindings { Accept = static (_, source, _) => source }.Transform(Expression.Add(A, B));
            new SameCompositionTwice { Accept = static (_, source, _) => source }.Transform(Expression.Add(A, B));
        }

        ExpressionTransformRuleInfo[] rules = [.. collector.GetEvents().Where(static e => e.Kind == ExpressionTransformEventKind.RuleApplied).Select(static e => e.Rule!)];
        CollectionAssert.AreEqual(
            new[] { ExpressionType.Add, ExpressionType.Subtract, ExpressionType.Multiply, ExpressionType.Divide, ExpressionType.Negate },
            rules[0].FlattenedNodeTypes.ToArray());
        CollectionAssert.AreEqual(
            new[] { ExpressionType.Add, ExpressionType.Subtract, ExpressionType.Multiply, ExpressionType.Divide, ExpressionType.Negate, ExpressionType.UnaryPlus },
            rules[1].FlattenedNodeTypes.ToArray());
        CollectionAssert.AreEqual(
            new[] { ExpressionType.Add, ExpressionType.Subtract, ExpressionType.Negate, ExpressionType.UnaryPlus },
            rules[2].FlattenedNodeTypes.ToArray());
        Assert.AreEqual(ExpressionType.Add, rules[0].DeclaredNodeType);
    }

    /// <summary>Minimal base of the invalid probes.</summary>
    private abstract class InvalidProbe : ExpressionTransformer
    {
        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);
    }

    /// <summary>A unary node type as a direct operation.</summary>
    private sealed class UnaryAsDirect : InvalidProbe
    {
        /// <summary>The invalid rule.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Negate, ExpressionType.Subtract)]
        private Expression? R(FlattenedTerm[] terms) => null;
    }

    /// <summary>A binary node type as a unary inverter.</summary>
    private sealed class BinaryAsInverter : InvalidProbe
    {
        /// <summary>The invalid rule.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract, ExpressionType.Multiply)]
        private Expression? R(FlattenedTerm[] terms) => null;
    }

    /// <summary>A node type that is neither binary nor unary, as an inverse operation.</summary>
    private sealed class ConditionalAsInverse : InvalidProbe
    {
        /// <summary>The invalid rule.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Conditional)]
        private Expression? R(FlattenedTerm[] terms) => null;
    }

    /// <summary>A unary operator declared by the simplified constructor and by <see cref="FlattenUnaryAttribute"/>.</summary>
    private sealed class DuplicateInverter : InvalidProbe
    {
        /// <summary>The invalid rule.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract, ExpressionType.Negate)]
        [FlattenUnary(ExpressionType.Negate, UnaryOperatorBinding.OperandGroup)]
        private Expression? R(FlattenedTerm[] terms) => null;
    }

    /// <summary>A unary operator declared twice by <see cref="FlattenUnaryAttribute"/>.</summary>
    private sealed class DuplicateFlattenUnary : InvalidProbe
    {
        /// <summary>The invalid rule.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten([ExpressionType.Add, ExpressionType.Multiply], [ExpressionType.Subtract, ExpressionType.Divide])]
        [FlattenUnary(ExpressionType.Negate, UnaryOperatorBinding.FirstGroup)]
        [FlattenUnary(ExpressionType.Negate, UnaryOperatorBinding.FirstGroup)]
        private Expression? R(FlattenedTerm[] terms) => null;
    }

    /// <summary>A binary node type declared by <see cref="FlattenUnaryAttribute"/>.</summary>
    private sealed class BinaryFlattenUnary : InvalidProbe
    {
        /// <summary>The invalid rule.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract)]
        [FlattenUnary(ExpressionType.Multiply, UnaryOperatorBinding.OperandGroup)]
        private Expression? R(FlattenedTerm[] terms) => null;
    }

    /// <summary>A binding outside <see cref="UnaryOperatorBinding"/>.</summary>
    private sealed class UnknownBinding : InvalidProbe
    {
        /// <summary>The invalid rule.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract)]
        [FlattenUnary(ExpressionType.Negate, (UnaryOperatorBinding)7)]
        private Expression? R(FlattenedTerm[] terms) => null;
    }

    /// <summary>A <see cref="FlattenUnaryAttribute"/> on a method without <see cref="FlattenAttribute"/>.</summary>
    private sealed class OrphanUnary : InvalidProbe
    {
        /// <summary>The invalid method.</summary>
        /// <returns><see langword="null"/>.</returns>
        [FlattenUnary(ExpressionType.Negate, UnaryOperatorBinding.OperandGroup)]
        private Expression? R(FlattenedTerm[] terms) => null;
    }

    /// <summary>A <see cref="FlattenUnaryAttribute"/> on a static method.</summary>
    private sealed class StaticUnary : InvalidProbe
    {
        /// <summary>The invalid method.</summary>
        /// <returns><see langword="null"/>.</returns>
        [FlattenUnary(ExpressionType.Negate, UnaryOperatorBinding.OperandGroup)]
        private static Expression? R(FlattenedTerm[] terms) => null;
    }

    /// <summary>The base of <see cref="OrphanOverride"/>: a valid rule.</summary>
    private abstract class ValidBase : InvalidProbe
    {
        /// <summary>The rule.</summary>
        /// <param name="terms">The terms.</param>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract)]
        protected virtual Expression? R(FlattenedTerm[] terms) => null;
    }

    /// <summary>An override that adds a unary operator without redeclaring <see cref="FlattenAttribute"/>: never merged.</summary>
    private sealed class OrphanOverride : ValidBase
    {
        /// <inheritdoc/>
        [FlattenUnary(ExpressionType.Negate, UnaryOperatorBinding.FirstGroup)]
        protected override Expression? R(FlattenedTerm[] terms) => null;
    }

    /// <summary>One unary operator bound to the first couple of two configurations: the root of <c>-x</c> would be ambiguous.</summary>
    private sealed class AmbiguousFirstGroups : InvalidProbe
    {
        /// <summary>The first rule.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract, ExpressionType.Negate)]
        private Expression? R1(FlattenedTerm[] terms) => null;

        /// <summary>The second rule.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Multiply, ExpressionType.Divide, ExpressionType.Negate)]
        private Expression? R2(FlattenedTerm[] terms) => null;
    }

    /// <summary>One unary operator bound to a couple by itself and to another through its operand.</summary>
    private sealed class AmbiguousMixedBindings : InvalidProbe
    {
        /// <summary>The first rule.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract)]
        [FlattenUnary(ExpressionType.Negate, UnaryOperatorBinding.OperandGroup)]
        private Expression? R1(FlattenedTerm[] terms) => null;

        /// <summary>The second rule.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Multiply, ExpressionType.Divide, ExpressionType.Negate)]
        private Expression? R2(FlattenedTerm[] terms) => null;
    }

    /// <summary><see cref="AmbiguousMixedBindings"/> in the other order, within one declaration of two couples.</summary>
    private sealed class AmbiguousMixedBindingsReversed : InvalidProbe
    {
        /// <summary>The first rule.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Multiply, ExpressionType.Divide, ExpressionType.Negate)]
        private Expression? R1(FlattenedTerm[] terms) => null;

        /// <summary>The second rule.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten([ExpressionType.Add, ExpressionType.And], [ExpressionType.Subtract, ExpressionType.Or])]
        [FlattenUnary(ExpressionType.Negate, UnaryOperatorBinding.OperandGroup)]
        private Expression? R2(FlattenedTerm[] terms) => null;
    }

    /// <summary>The same couple with a unary operator bound differently in two rules.</summary>
    private sealed class BindingConflict : InvalidProbe
    {
        /// <summary>The first rule.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract, ExpressionType.Negate)]
        private Expression? R1(FlattenedTerm[] terms) => null;

        /// <summary>The second rule.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract)]
        [FlattenUnary(ExpressionType.Negate, UnaryOperatorBinding.OperandGroup)]
        private Expression? R2(FlattenedTerm[] terms) => null;
    }

    /// <summary>An operator declared in two couples of one rule.</summary>
    private sealed class OperatorInTwoCouples : InvalidProbe
    {
        /// <summary>The invalid rule.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten([ExpressionType.Add, ExpressionType.Multiply], [ExpressionType.Subtract, ExpressionType.Add])]
        private Expression? R(FlattenedTerm[] terms) => null;
    }

    /// <summary>Arrays of different lengths.</summary>
    private sealed class UnmatchedCouples : InvalidProbe
    {
        /// <summary>The invalid rule.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten([ExpressionType.Add, ExpressionType.Multiply], [ExpressionType.Subtract])]
        private Expression? R(FlattenedTerm[] terms) => null;
    }

    /// <summary>A unary operator without an inverse operation: the inverted terms would have no operation.</summary>
    private sealed class InvertersWithoutInverse : InvalidProbe
    {
        /// <summary>The invalid rule.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Add)]
        [FlattenUnary(ExpressionType.Negate, UnaryOperatorBinding.FirstGroup)]
        private Expression? R(FlattenedTerm[] terms) => null;
    }

    /// <summary>The same operators with incompatible roles in two rules.</summary>
    private sealed class IncompatibleRoles : InvalidProbe
    {
        /// <summary>The first rule.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract)]
        private Expression? R1(FlattenedTerm[] terms) => null;

        /// <summary>The second rule, with the roles swapped.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Subtract, ExpressionType.Add)]
        private Expression? R2(FlattenedTerm[] terms) => null;
    }

    /// <summary>The same couple with and without an inverter in two rules.</summary>
    private sealed class PartialConflict : InvalidProbe
    {
        /// <summary>The first rule.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract, ExpressionType.Negate)]
        private Expression? R1(FlattenedTerm[] terms) => null;

        /// <summary>The second rule, without the inverter.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Add, ExpressionType.Subtract)]
        private Expression? R2(FlattenedTerm[] terms) => null;
    }

    /// <summary>A couple of a two-couple rule conflicts with another rule's composition.</summary>
    private sealed class CoupleConflict : InvalidProbe
    {
        /// <summary>The first rule.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten([ExpressionType.Add, ExpressionType.Multiply], [ExpressionType.Subtract, ExpressionType.Divide])]
        private Expression? R1(FlattenedTerm[] terms) => null;

        /// <summary>The second rule, a direct multiplication alone.</summary>
        /// <returns><see langword="null"/>.</returns>
        [Flatten(ExpressionType.Multiply)]
        private Expression? R2(FlattenedTerm[] terms) => null;
    }

    /// <summary>Every inconsistent declaration fails the construction of its transformer, every time, with an explicit message.</summary>
    [TestMethod]
    public void InconsistentDeclarations_FailTheConstruction()
    {
        (Func<object> Create, string Reason)[] cases =
        [
            (() => new UnaryAsDirect(), "'Negate' is not the node type of a BinaryExpression"),
            (() => new BinaryAsInverter(), "'Multiply' is not the node type of a UnaryExpression"),
            (() => new ConditionalAsInverse(), "'Conditional' is not the node type of a BinaryExpression"),
            (() => new DuplicateInverter(), "'Negate' is declared more than once"),
            (() => new OperatorInTwoCouples(), "'Add' is declared more than once"),
            (() => new UnmatchedCouples(), "2 direct and 1 inverse operations do not form couples"),
            (() => new InvertersWithoutInverse(), "unary operators without an inverse operation"),
            (() => new IncompatibleRoles(), "overlap"),
            (() => new PartialConflict(), "overlap"),
            (() => new CoupleConflict(), "overlap"),
            (() => new DuplicateFlattenUnary(), "'Negate' is declared more than once"),
            (() => new BinaryFlattenUnary(), "'Multiply' is not the node type of a UnaryExpression"),
            (() => new UnknownBinding(), "'7' is not a binding of the unary operator 'Negate'"),
            (() => new OrphanUnary(), "FlattenUnaryAttribute without a FlattenAttribute"),
            (() => new StaticUnary(), "must be an instance method"),
            (() => new OrphanOverride(), "FlattenUnaryAttribute without a FlattenAttribute"),
            (() => new AmbiguousFirstGroups(), "'Negate' could root the chains of several couples"),
            (() => new AmbiguousMixedBindings(), "'Negate' could root the chains of several couples"),
            (() => new AmbiguousMixedBindingsReversed(), "'Negate' could root the chains of several couples"),
            (() => new BindingConflict(), "overlap"),
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
}
