using System.Linq.Expressions;
using System.Linq;

namespace Utils.Expressions;

/// <summary>
/// Provides a standard optimization pass over expression trees.
/// </summary>
public sealed class ExpressionOptimiser
{
    /// <summary>
    /// Optimizes the provided expression tree.
    /// </summary>
    /// <param name="expression">Expression to optimize.</param>
    /// <returns>An optimized expression tree.</returns>
    public Expression Optimize(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return new StandardOptimiserVisitor().Visit(expression);
    }

    /// <summary>
    /// Implements common local expression rewrites.
    /// </summary>
    private sealed class StandardOptimiserVisitor : ExpressionVisitor
    {
        /// <inheritdoc />
        protected override Expression VisitUnary(UnaryExpression node)
        {
            var operand = Visit(node.Operand);

            if (node.NodeType == ExpressionType.Convert && operand.Type == node.Type)
            {
                return operand;
            }

            if (node.NodeType == ExpressionType.Negate && operand is UnaryExpression innerNegate && innerNegate.NodeType == ExpressionType.Negate)
            {
                return innerNegate.Operand;
            }

            return node.Update(operand);
        }

        /// <inheritdoc />
        protected override Expression VisitBinary(BinaryExpression node)
        {
            var left = Visit(node.Left);
            var right = Visit(node.Right);

            if (node.NodeType == ExpressionType.Add && IsNumericType(node.Type))
            {
                if (IsNumericConstant(right, 0d)) return left;
                if (IsNumericConstant(left, 0d)) return right;
            }

            if (node.NodeType == ExpressionType.Subtract && IsNumericType(node.Type))
            {
                if (IsNumericConstant(right, 0d)) return left;
            }

            if (node.NodeType == ExpressionType.Multiply && IsNumericType(node.Type))
            {
                if (IsNumericConstant(right, 1d)) return left;
                if (IsNumericConstant(left, 1d)) return right;
                if (IsNumericConstant(right, 0d)) return CreateZeroConstant(node.Type);
                if (IsNumericConstant(left, 0d)) return CreateZeroConstant(node.Type);
            }

            if (node.NodeType == ExpressionType.Divide && IsNumericType(node.Type))
            {
                if (IsNumericConstant(right, 1d)) return left;
            }

            if (node.NodeType == ExpressionType.AndAlso)
            {
                if (TryGetBooleanConstant(left, out var leftBool))
                {
                    return leftBool ? right : Expression.Constant(false);
                }

                // Only simplify when the right constant is true: left && true → left.
                // When right is false, left must still be evaluated for side effects.
                if (TryGetBooleanConstant(right, out var rightBool) && rightBool)
                {
                    return left;
                }
            }

            if (node.NodeType == ExpressionType.OrElse)
            {
                if (TryGetBooleanConstant(left, out var leftBool))
                {
                    return leftBool ? Expression.Constant(true) : right;
                }

                // Only simplify when the right constant is false: left || false → left.
                // When right is true, left must still be evaluated for side effects.
                if (TryGetBooleanConstant(right, out var rightBool) && !rightBool)
                {
                    return left;
                }
            }

            return node.Update(left, node.Conversion, right);
        }

        /// <inheritdoc />
        protected override Expression VisitConditional(ConditionalExpression node)
        {
            var test = Visit(node.Test);
            var ifTrue = Visit(node.IfTrue);
            var ifFalse = Visit(node.IfFalse);

            if (TryGetBooleanConstant(test, out var testValue))
            {
                return testValue ? ifTrue : ifFalse;
            }

            return node.Update(test, ifTrue, ifFalse);
        }

        /// <inheritdoc />
        protected override Expression VisitBlock(BlockExpression node)
        {
            var visitedExpressions = node.Expressions
                .Select(Visit)
                .Where(static expression => expression.NodeType != ExpressionType.Default || expression.Type != typeof(void))
                .ToList();

            if (visitedExpressions.Count == 0)
            {
                return Expression.Empty();
            }

            if (visitedExpressions.Count == 1 && node.Variables.Count == 0)
            {
                return visitedExpressions[0];
            }

            return Expression.Block(node.Variables, visitedExpressions);
        }

        /// <summary>
        /// Creates a zero constant expression for the given numeric type, handling nullable types.
        /// </summary>
        /// <param name="type">Target type (may be nullable).</param>
        /// <returns>Constant expression representing zero for the given type.</returns>
        private static ConstantExpression CreateZeroConstant(Type type)
        {
            Type underlying = Nullable.GetUnderlyingType(type) ?? type;
            object zero = Convert.ChangeType(0, underlying);
            return Expression.Constant(zero, type);
        }

        /// <summary>
        /// Determines whether a type is numeric.
        /// </summary>
        /// <param name="type">Type to inspect.</param>
        /// <returns><see langword="true"/> when numeric; otherwise <see langword="false"/>.</returns>
        private static bool IsNumericType(Type type)
        {
            var nonNullableType = Nullable.GetUnderlyingType(type) ?? type;
            return Type.GetTypeCode(nonNullableType) switch
            {
                TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16
                    or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64
                    or TypeCode.Single or TypeCode.Double or TypeCode.Decimal => true,
                _ => false
            };
        }

        /// <summary>
        /// Tries to read a boolean constant from an expression.
        /// </summary>
        /// <param name="expression">Expression to inspect.</param>
        /// <param name="value">Resolved boolean value.</param>
        /// <returns><see langword="true"/> when the expression is a boolean constant.</returns>
        private static bool TryGetBooleanConstant(Expression expression, out bool value)
        {
            if (expression is ConstantExpression constantExpression && constantExpression.Value is bool boolValue)
            {
                value = boolValue;
                return true;
            }

            value = false;
            return false;
        }

        /// <summary>
        /// Determines whether an expression is a numeric constant equal to a specific value.
        /// </summary>
        /// <param name="expression">Expression to inspect.</param>
        /// <param name="expectedValue">Expected numeric value.</param>
        /// <returns><see langword="true"/> when the expression matches the expected value.</returns>
        private static bool IsNumericConstant(Expression expression, double expectedValue)
        {
            if (expression is not ConstantExpression constantExpression || constantExpression.Value is null)
            {
                return false;
            }

            if (!IsNumericType(constantExpression.Type))
            {
                return false;
            }

            double actualValue = Convert.ToDouble(constantExpression.Value);
            return double.Abs(actualValue - expectedValue) < double.Epsilon;
        }
    }
}
