using System;
using System.Linq.Expressions;

namespace Utils.Expressions;

/// <summary>
/// Declares one unary operator of the <see cref="FlattenAttribute"/> rule it marks, and how that operator is attached to the
/// couples of the declaration (see <see cref="UnaryOperatorBinding"/>). The operator inverts the context of its operand. This
/// type only holds data.
/// </summary>
/// <remarks>
/// <para>
/// The attribute is repeatable, one per unary operator, and completes any form of <see cref="FlattenAttribute"/>:
/// <c>[Flatten([Add, Multiply], [Subtract, Divide])]</c> with <c>[FlattenUnary(Negate, UnaryOperatorBinding.OperandGroup)]</c>
/// gives <c>-(a * b)</c> to the multiplicative couple and <c>-(a + b)</c> to the additive one.
/// <c>[Flatten(Add, Subtract)]</c> with <c>[FlattenUnary(Negate, UnaryOperatorBinding.FirstGroup)]</c> is the same declaration
/// as <c>[Flatten(Add, Subtract, Negate)]</c>.
/// </para>
/// <para>
/// The attributes belong to the <see cref="FlattenAttribute"/> of the same method declaration. An override that declares a new
/// <see cref="FlattenAttribute"/> replaces the whole configuration of the overridden method, its unary operators included; an
/// override that declares neither keeps it entirely. A <see cref="FlattenUnaryAttribute"/> on a method declaration without a
/// <see cref="FlattenAttribute"/> is rejected when the dispatch plan is built.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class FlattenUnaryAttribute : Attribute
{
    /// <summary>Declares a unary operator of the flatten rule and its binding.</summary>
    /// <param name="operation">The node type of a <see cref="UnaryExpression"/>, which inverts the context of its operand.</param>
    /// <param name="binding">How the operator is attached to the couples of the declaration.</param>
    public FlattenUnaryAttribute(ExpressionType operation, UnaryOperatorBinding binding)
    {
        Operation = operation;
        Binding = binding;
    }

    /// <summary>Gets the declared unary operator.</summary>
    public ExpressionType Operation { get; }

    /// <summary>Gets how the operator is attached to the couples of the declaration.</summary>
    public UnaryOperatorBinding Binding { get; }
}
