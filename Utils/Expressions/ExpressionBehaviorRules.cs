using System.Linq.Expressions;

namespace Utils.Expressions;

/// <summary>
/// The behaviors of the intrinsic operators of the expression model (those without an operator method), for
/// <see cref="ExpressionBehaviorAnalysis"/> (S5 P18 Architecture A). Every rule reads node types and static types only.
/// </summary>
/// <remarks>
/// The dispatch switches below list every node type they accept and classify any other one as
/// <see cref="ExpressionBehaviors.UnknownCode"/>, so a node type added to the framework later is never silently trusted.
/// </remarks>
internal static class ExpressionBehaviorRules
{
    /// <summary>
    /// Returns the operator a compound assignment applies before its write (<see cref="ExpressionType.Assign"/> for a plain
    /// assignment), or <see langword="null"/> when <paramref name="nodeType"/> is not an assignment.
    /// </summary>
    /// <param name="nodeType">The node type of a binary node.</param>
    /// <returns>The applied operator, or <see langword="null"/>.</returns>
    internal static ExpressionType? CompoundOperator(ExpressionType nodeType) => nodeType switch
    {
        ExpressionType.Assign => ExpressionType.Assign,
        ExpressionType.AddAssign => ExpressionType.Add,
        ExpressionType.AddAssignChecked => ExpressionType.AddChecked,
        ExpressionType.SubtractAssign => ExpressionType.Subtract,
        ExpressionType.SubtractAssignChecked => ExpressionType.SubtractChecked,
        ExpressionType.MultiplyAssign => ExpressionType.Multiply,
        ExpressionType.MultiplyAssignChecked => ExpressionType.MultiplyChecked,
        ExpressionType.DivideAssign => ExpressionType.Divide,
        ExpressionType.ModuloAssign => ExpressionType.Modulo,
        ExpressionType.PowerAssign => ExpressionType.Power,
        ExpressionType.AndAssign => ExpressionType.And,
        ExpressionType.OrAssign => ExpressionType.Or,
        ExpressionType.ExclusiveOrAssign => ExpressionType.ExclusiveOr,
        ExpressionType.LeftShiftAssign => ExpressionType.LeftShift,
        ExpressionType.RightShiftAssign => ExpressionType.RightShift,
        _ => null,
    };

    /// <summary>
    /// Classifies an intrinsic binary operator, operands excluded: unchecked arithmetic, bitwise, comparison and logical
    /// operators never throw; checked arithmetic overflows on integers only; a division or remainder throws on integers
    /// unless the divisor is a constant that cannot make it throw; an array element is mutable state and may be out of range.
    /// </summary>
    /// <param name="nodeType">The operator (never an assignment or a coalescing).</param>
    /// <param name="type">The type of the node (the operand type for arithmetic, possibly nullable).</param>
    /// <param name="right">The right operand, inspected only by class and constant value.</param>
    /// <returns>The behavior of the operator.</returns>
    internal static ExpressionBehaviors IntrinsicBinary(ExpressionType nodeType, Type type, Expression right)
    {
        Type operand = Nullable.GetUnderlyingType(type) ?? type;
        return nodeType switch
        {
            ExpressionType.Add or ExpressionType.Subtract or ExpressionType.Multiply or ExpressionType.And or ExpressionType.Or
                or ExpressionType.ExclusiveOr or ExpressionType.LeftShift or ExpressionType.RightShift or ExpressionType.Equal
                or ExpressionType.NotEqual or ExpressionType.LessThan or ExpressionType.LessThanOrEqual
                or ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual or ExpressionType.AndAlso
                or ExpressionType.OrElse => ExpressionBehaviors.None,
            ExpressionType.AddChecked or ExpressionType.SubtractChecked or ExpressionType.MultiplyChecked
                => IsBinaryFloatingPoint(operand) ? ExpressionBehaviors.None : ExpressionBehaviors.MayThrowArithmetic,
            ExpressionType.Divide or ExpressionType.Modulo => DivisionBehaviors(operand, right),
            ExpressionType.ArrayIndex => ExpressionBehaviors.ReadsMutableState | ExpressionBehaviors.MayThrowRuntime,
            _ => ExpressionBehaviors.UnknownCode,
        };
    }

    /// <summary>Classifies an intrinsic division or remainder.</summary>
    /// <param name="operand">The operand type, not nullable.</param>
    /// <param name="right">The divisor.</param>
    /// <returns>None for a binary floating-point type or a safe constant divisor, <see cref="ExpressionBehaviors.MayThrowArithmetic"/> otherwise.</returns>
    private static ExpressionBehaviors DivisionBehaviors(Type operand, Expression right)
    {
        if (IsBinaryFloatingPoint(operand)) return ExpressionBehaviors.None;
        bool safe = right is ConstantExpression { Value: { } divisor } && divisor.GetType() == operand && ExpressionEffectAnalysis.IsSafeDivisor(divisor);
        return safe ? ExpressionBehaviors.None : ExpressionBehaviors.MayThrowArithmetic;
    }

    /// <summary>Classifies an intrinsic unary operator, operand excluded.</summary>
    /// <param name="nodeType">The operator (never a quote, a throw or an increment assignment).</param>
    /// <param name="operandType">The static type of the operand (never asked of an extension node).</param>
    /// <param name="type">The type of the node.</param>
    /// <returns>The behavior of the operator.</returns>
    internal static ExpressionBehaviors IntrinsicUnary(ExpressionType nodeType, Type operandType, Type type) => nodeType switch
    {
        ExpressionType.Negate or ExpressionType.UnaryPlus or ExpressionType.Not or ExpressionType.OnesComplement
            or ExpressionType.IsTrue or ExpressionType.IsFalse or ExpressionType.Increment or ExpressionType.Decrement
            => ExpressionBehaviors.None,
        ExpressionType.NegateChecked
            => IsBinaryFloatingPoint(Nullable.GetUnderlyingType(type) ?? type) ? ExpressionBehaviors.None : ExpressionBehaviors.MayThrowArithmetic,
        ExpressionType.Convert => Conversion(operandType, type, isChecked: false),
        ExpressionType.ConvertChecked => Conversion(operandType, type, isChecked: true),
        ExpressionType.ArrayLength or ExpressionType.Unbox => ExpressionBehaviors.MayThrowRuntime,
        ExpressionType.TypeAs => operandType.IsValueType && !type.IsValueType ? ExpressionBehaviors.Allocates : ExpressionBehaviors.None,
        _ => ExpressionBehaviors.UnknownCode,
    };

    /// <summary>
    /// Classifies an intrinsic conversion: between primitive or enumeration types it never throws unless it unwraps a nullable
    /// value or, checked, may overflow an integral target; boxing allocates; unboxing and reference casts that are not
    /// widening may throw.
    /// </summary>
    /// <param name="source">The operand type.</param>
    /// <param name="target">The result type.</param>
    /// <param name="isChecked">Whether the conversion is checked.</param>
    /// <returns>The behavior of the conversion.</returns>
    private static ExpressionBehaviors Conversion(Type source, Type target, bool isChecked)
    {
        if (source == target) return ExpressionBehaviors.None;

        Type? sourceUnderlying = Nullable.GetUnderlyingType(source);
        Type? targetUnderlying = Nullable.GetUnderlyingType(target);
        Type sourceValue = sourceUnderlying ?? source;
        Type targetValue = targetUnderlying ?? target;
        if (IsPrimitiveValue(sourceValue) && IsPrimitiveValue(targetValue))
        {
            ExpressionBehaviors behaviors = sourceUnderlying is not null && targetUnderlying is null
                ? ExpressionBehaviors.MayThrowRuntime
                : ExpressionBehaviors.None;
            if (isChecked && sourceValue != targetValue && !IsBinaryFloatingPoint(targetValue)) behaviors |= ExpressionBehaviors.MayThrowArithmetic;
            return behaviors;
        }

        if (source.IsValueType && !target.IsValueType) return ExpressionBehaviors.Allocates;
        if (!source.IsValueType && !target.IsValueType)
        {
            return target.IsAssignableFrom(source) ? ExpressionBehaviors.None : ExpressionBehaviors.MayThrowRuntime;
        }

        // Wrapping a value into its nullable type is total; unboxing, unwrapping and any other value conversion may throw.
        return targetUnderlying == source ? ExpressionBehaviors.None : ExpressionBehaviors.MayThrowRuntime;
    }

    /// <summary>Whether <paramref name="type"/> is <see cref="double"/> or <see cref="float"/>, whose intrinsic operators never throw.</summary>
    /// <param name="type">The type.</param>
    /// <returns><see langword="true"/> for a binary floating-point primitive.</returns>
    private static bool IsBinaryFloatingPoint(Type type) => type == typeof(double) || type == typeof(float);

    /// <summary>Whether <paramref name="type"/> is a primitive or enumeration type.</summary>
    /// <param name="type">The type.</param>
    /// <returns><see langword="true"/> for such a type.</returns>
    private static bool IsPrimitiveValue(Type type) => type.IsPrimitive || type.IsEnum;
}
