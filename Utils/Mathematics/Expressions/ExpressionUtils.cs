using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Utils.Objects;

namespace Utils.Mathematics.Expressions
{
	public static class ExpressionUtils
	{
		public static bool CheckConstant<T>( Expression expressionToCheck, T checkValue )
		{
			if (!(expressionToCheck is ConstantExpression expression))
			{
				return false;
			}
			var value = expression.Value;

			if (value is T val)
			{
				return val.Equals(checkValue);
			}

			if (NumberUtils.IsNumeric(value) && NumberUtils.IsNumeric(checkValue)) {
				return ((double)value) == ((double)(object)checkValue);
			}
			return false;
		}

		public static bool Equals( Expression x, Expression y )
		{
			return ExpressionComparer.Default.Equals(x, y);
		}

	}

}
