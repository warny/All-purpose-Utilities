namespace Utils.Expressions;

/// <summary>
/// How a unary operator declared for a flatten rule (see <see cref="FlattenAttribute"/> and
/// <see cref="FlattenUnaryAttribute"/>) is attached to the couples of its declaration. In both modes the operator inverts the
/// context of its operand; the binding only decides which chains cross it and which chain it roots.
/// </summary>
public enum UnaryOperatorBinding
{
    /// <summary>
    /// The operator belongs to the first couple of the declaration: it roots a chain of that couple whatever its operand
    /// (<c>-x</c> gives one term), and only the chains of that couple cross it.
    /// </summary>
    FirstGroup,

    /// <summary>
    /// The operator belongs to the couple its operand belongs to, decided by the operand alone: a crossed binary operator of a
    /// couple of the declaration, a <see cref="FirstGroup"/> operator, or another <see cref="OperandGroup"/> operator whose own
    /// operand is decided the same way. An operand that identifies no couple (a leaf, for example) attaches the operator to
    /// none: it roots no chain and is an operand of any chain that reaches it. Two couples are never merged.
    /// </summary>
    OperandGroup,
}
