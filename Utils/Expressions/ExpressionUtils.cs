using System.Linq.Expressions;
using Utils.Mathematics.Expressions;
using Utils.Objects;

namespace Utils.Expressions
{
    public static class ExpressionUtils
    {
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

        public static bool Equals(Expression x, Expression y)
        {
            return ExpressionComparer.Default.Equals(x, y);
        }

    }

}
