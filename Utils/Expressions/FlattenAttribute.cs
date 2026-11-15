using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq.Expressions;

namespace Utils.Expressions;

/// <summary>
/// Marks a transformation rule of an <see cref="ExpressionTransformer"/> that receives a whole chain of operators at once, as
/// an ordered array of <see cref="FlattenedTerm"/>, instead of being called once per operator node. This type only holds data.
/// </summary>
/// <remarks>
/// <para>
/// The attribute declares one or several <em>couples</em>: a <em>direct</em> binary operation, which passes the context of the
/// chain unchanged to both operands, and an optional <em>inverse</em> binary operation, which passes it unchanged to its left
/// operand and inverts it for its right operand. Unary operators, which invert the context of their operand, are attached to
/// the couples either by the simplified constructor (one operator, bound to <see cref="UnaryOperatorBinding.FirstGroup"/>) or
/// by <see cref="FlattenUnaryAttribute"/>. The context has two values; two inversions cancel out. Each operand of the chain is a
/// term whose operation is the direct operation of the couple when the context reaching it is the initial one, its inverse
/// operation otherwise. The engine applies these declarations and nothing else: it gives no meaning to the operators.
/// </para>
/// <para>
/// <c>[Flatten(ExpressionType.Add, ExpressionType.Subtract, ExpressionType.Negate)]</c> gives <c>+a -b +c</c> for
/// <c>a - (b - c)</c>, <c>-a -b</c> for <c>-(a + b)</c> and <c>-x</c> for <c>-x</c>;
/// <c>[Flatten(ExpressionType.Multiply, ExpressionType.Divide)]</c> gives <c>*a /b *c</c> for <c>a / (b / c)</c>. The array
/// form declares several couples, associated by index, each of which is a chain of its own: their terms are never mixed, and an
/// operator of one couple is an operand of a chain of another.
/// </para>
/// <para>
/// The declared operators are the ones the chain may cross. Any other node, and any declared operator node the transformer does
/// not accept to cross (by default a user-defined, <see langword="decimal"/> or lifted operator), is an operand of the chain.
/// Every declaration is validated once, when the dispatch plan of the transformer type is built: the direct and inverse
/// operations must be node types of <see cref="BinaryExpression"/>, the unary operators node types of
/// <see cref="UnaryExpression"/> (and need an inverse operation), no operator may be declared twice, two rules that share an
/// operator must declare the same couple with the same unary operators and bindings, and no unary operator may root the chains
/// of several couples.
/// </para>
/// <para>
/// A rule has one of two signatures, checked once when the transformer's dispatch plan is built:
/// <c>Expression? Rule(FlattenedTerm[] terms)</c> or <c>Expression? Rule(Expression source, FlattenedTerm[] terms)</c>, where
/// <c>source</c> is the original, untransformed root of the chain. The operands are the raw, untransformed sub-expressions:
/// a rule that accepts the chain is responsible for transforming them. A <see langword="null"/> result declines the chain;
/// when every candidate declines, the transformer processes the chain node by node exactly as it does without this attribute.
/// See <c>docs/expressions/FlattenArchitecture.md</c> for the priority, fallback and budget rules.
/// </para>
/// <para>
/// The attribute is a request for a representation, not a permission: declaring an inversion establishes neither
/// associativity, commutativity nor the existence of an inverse, and never allows a rule to drop, repeat, merge or move an
/// evaluation. Those decisions remain the rule's own responsibility.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class FlattenAttribute : Attribute
{
    /// <summary>Declares a couple made of a direct operation only: every term carries <paramref name="direct"/>.</summary>
    /// <param name="direct">The binary operation that passes the context to both operands.</param>
    public FlattenAttribute(ExpressionType direct)
        : this([direct], [])
    {
    }

    /// <summary>Declares a couple made of a direct operation and its inverse.</summary>
    /// <param name="direct">The binary operation that passes the context to both operands.</param>
    /// <param name="inverse">The binary operation that passes the context to its left operand and inverts it for its right one.</param>
    public FlattenAttribute(ExpressionType direct, ExpressionType inverse)
        : this([direct], [inverse])
    {
    }

    /// <summary>
    /// Declares a couple made of a direct operation and its inverse, and one unary operator bound to it
    /// (<see cref="UnaryOperatorBinding.FirstGroup"/>).
    /// </summary>
    /// <param name="direct">The binary operation that passes the context to both operands.</param>
    /// <param name="inverse">The binary operation that passes the context to its left operand and inverts it for its right one.</param>
    /// <param name="unaryOperator">The unary operation that inverts the context of its operand.</param>
    public FlattenAttribute(ExpressionType direct, ExpressionType inverse, ExpressionType unaryOperator)
        : this([direct], [inverse])
    {
        UnaryOperator = unaryOperator;
        Operations = new ReadOnlyCollection<ExpressionType>([direct, inverse, unaryOperator]);
    }

    /// <summary>
    /// Declares several independent couples, <c>(direct[i], inverse[i])</c> for each index <c>i</c>. Their unary operators are
    /// declared with <see cref="FlattenUnaryAttribute"/>.
    /// </summary>
    /// <param name="direct">The direct operation of each couple.</param>
    /// <param name="inverse">The inverse operation of each couple, at the same index as its direct operation; empty for direct operations alone.</param>
    public FlattenAttribute(ExpressionType[] direct, ExpressionType[] inverse)
    {
        // Private copies: the caller's arrays (or the compiler-generated ones) can never alter the declaration afterwards.
        DirectOperations = Copy(direct);
        InverseOperations = Copy(inverse);

        // Every declared operator, couple by couple (the trace identity of the rule, completed by its unary operators).
        var operations = new List<ExpressionType>(DirectOperations.Count + InverseOperations.Count);
        for (int i = 0; i < Math.Max(DirectOperations.Count, InverseOperations.Count); i++)
        {
            if (i < DirectOperations.Count) operations.Add(DirectOperations[i]);
            if (i < InverseOperations.Count) operations.Add(InverseOperations[i]);
        }

        Operations = operations.AsReadOnly();
    }

    /// <summary>Gets the direct operation of each couple, in declaration order.</summary>
    public IReadOnlyList<ExpressionType> DirectOperations { get; }

    /// <summary>Gets the inverse operation of each couple, at the index of its direct operation; empty for direct operations alone.</summary>
    public IReadOnlyList<ExpressionType> InverseOperations { get; }

    /// <summary>
    /// Gets the unary operator of the simplified constructor, bound to <see cref="UnaryOperatorBinding.FirstGroup"/>, or
    /// <see langword="null"/>.
    /// </summary>
    public ExpressionType? UnaryOperator { get; }

    /// <summary>
    /// Gets the operators of this attribute: the direct then the inverse operation of each couple, in index order, then
    /// <see cref="UnaryOperator"/> when there is one.
    /// </summary>
    public IReadOnlyList<ExpressionType> Operations { get; }

    /// <summary>Copies a declared array into a read-only list (<see langword="null"/> gives an empty one).</summary>
    /// <param name="operations">The declared array.</param>
    /// <returns>A read-only copy.</returns>
    private static ReadOnlyCollection<ExpressionType> Copy(ExpressionType[]? operations)
        => new((ExpressionType[])(operations ?? []).Clone());
}
