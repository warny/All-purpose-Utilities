using System.Linq.Expressions;

namespace Utils.Expressions;

/// <summary>
/// One operand of an arithmetic chain flattened for a <see cref="FlattenAttribute"/> rule. This type only holds data.
/// </summary>
/// <param name="Operation">
/// The operation applied to <paramref name="Operand"/> in the chain, after the signs and inversions of the crossed operators are
/// propagated: <see cref="ExpressionType.Add"/> or <see cref="ExpressionType.Subtract"/> for an additive chain,
/// <see cref="ExpressionType.Multiply"/> or <see cref="ExpressionType.Divide"/> for a multiplicative one. The first operand
/// carries an explicit operation too (<c>-(a + b)</c> gives <c>Subtract a</c>, <c>Subtract b</c>).
/// </param>
/// <param name="Operand">The raw, untransformed operand, as it appears in the source tree (shared nodes keep every occurrence).</param>
/// <param name="SourceIndex">The position of the operand in the chain, from left to right in the source tree, starting at 0.</param>
/// <remarks>
/// The operations describe the chain symbolically. For a multiplicative chain they do not establish that a different grouping
/// evaluates to the same value in .NET (integer truncation, rounding, overflow): <c>(a / b) * c</c> and <c>a / (b / c)</c>
/// give the same terms. A rule that reassociates must prove that the new grouping is valid for the operand types.
/// </remarks>
public readonly record struct FlattenedTerm(ExpressionType Operation, Expression Operand, int SourceIndex);
