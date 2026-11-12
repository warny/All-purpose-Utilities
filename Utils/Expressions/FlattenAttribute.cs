using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq.Expressions;

namespace Utils.Expressions;

/// <summary>
/// Marks a transformation rule of an <see cref="ExpressionTransformer"/> that receives a whole arithmetic chain at once, as an
/// ordered array of <see cref="FlattenedTerm"/>, instead of being called once per binary operator. This type only holds data.
/// </summary>
/// <remarks>
/// <para>
/// Two families of operators can be flattened: the additive family (<see cref="ExpressionType.Add"/>,
/// <see cref="ExpressionType.Subtract"/>, <see cref="ExpressionType.Negate"/>) and the multiplicative family
/// (<see cref="ExpressionType.Multiply"/>, <see cref="ExpressionType.Divide"/>). The declared operators are the ones the chain
/// may cross; any other node (and any operator node the transformer does not consider an ordinary intrinsic operator, such as
/// a user-defined, lifted or checked operator) is an operand of the chain.
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
/// The attribute is a request for a representation, not a permission: it never allows a rule to drop, repeat, merge or move an
/// evaluation. Those decisions remain the rule's own responsibility.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class FlattenAttribute : Attribute
{
    /// <summary>Initializes the attribute with the operators the chain may cross.</summary>
    /// <param name="operations">The operators of one family (see the type remarks); validated when the dispatch plan is built.</param>
    public FlattenAttribute(params ExpressionType[] operations)
    {
        // A private copy: the caller's array (or the compiler-generated one) can never alter the declared set afterwards.
        Operations = new ReadOnlyCollection<ExpressionType>((ExpressionType[])(operations ?? []).Clone());
    }

    /// <summary>Gets the operators the chain may cross, in declaration order.</summary>
    public IReadOnlyList<ExpressionType> Operations { get; }
}
