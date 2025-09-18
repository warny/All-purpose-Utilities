using System.Linq.Expressions;
using Utils.Mathematics.Expressions;
using Utils.Objects;

namespace Utils.Expressions
{
    /// <summary>
    /// Provides utility helpers for analysing and comparing expression trees.
    /// </summary>
    public static class ExpressionUtils
    {
        /// <summary>
        /// Determines whether the supplied expression is a constant with the specified value.
        /// </summary>
        /// <typeparam name="T">Type of the value to compare against.</typeparam>
        /// <param name="expressionToCheck">Expression that may contain a constant.</param>
        /// <param name="checkValue">Value expected to be held by the constant expression.</param>
        /// <returns><see langword="true"/> when the expression is a constant that matches <paramref name="checkValue"/>.</returns>
        public static bool CheckConstant<T>(Expression expressionToCheck, T checkValue)
        {
            if (expressionToCheck is not ConstantExpression expression)
            {
                return false;
            }
            var value = expression.Value;

            if (value is T val)
            {
                return val.Equals(checkValue);
            }

            if (NumberUtils.IsNumeric(value) && NumberUtils.IsNumeric(checkValue))
            {
                return (decimal)value == (decimal)Convert.ChangeType(checkValue, typeof(double));
            }
            return false;
        }

        /// <summary>
        /// Compares two expressions for structural equality using the default comparer.
        /// </summary>
        /// <param name="x">First expression to compare.</param>
        /// <param name="y">Second expression to compare.</param>
        /// <returns><see langword="true"/> when both expressions are structurally equivalent.</returns>
        public static bool Equals(Expression x, Expression y)
        {
            return ExpressionComparer.Default.Equals(x, y);
        }

    }

}
