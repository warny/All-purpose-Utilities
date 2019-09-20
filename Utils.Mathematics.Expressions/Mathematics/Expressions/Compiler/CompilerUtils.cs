using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Mathematics.Expressions.Compiler
{
	public static class CompilerUtils
	{
		public static Expression ToExpression(this IEnumerable<Expression> expressions)
		{
			return ToExpression(expressions?.ToArray());
		}

		public static Expression ToExpression(params Expression[] expressions) {
			if (expressions == null || expressions.Length == 0) return null;
			if (expressions.Length == 1)
				return expressions[0];
			else
				return Expression.Block(expressions);
		}


	}
}
