using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Utils.Lists;
using Utils.Objects;

namespace Utils.Mathematics.Expressions.Compiler
{
	public class Constant : IExpressionTree
	{
		public IExpressionTree Parent { get; set; }

		public string Value { get; set; }

		public Expression[] CreateExpression(ParameterExpression[] variables, IndexedList<string, LabelTarget> labels, out ParameterExpression[] declaredVariables)
		{
			if (Value.StartsWith("\"")) {
				Expression.Constant(Value.Substring(1, Value.Length - 2));
			} else if (Value.StartsWith("$\"")) {

			}
		}
	}
}
