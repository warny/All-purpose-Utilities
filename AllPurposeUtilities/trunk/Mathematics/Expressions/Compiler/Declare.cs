using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Utils.Lists;

namespace Utils.Mathematics.Expressions.Compiler
{
	public class Declare : IExpressionTree
	{
		public string TypeName { get; set; }
		public string VariableName { get; set; }

		public IExpressionTree Parent { get; set; }

		public Expression[] CreateExpression(ParameterExpression[] variables, IndexedList<string, LabelTarget> labels, out ParameterExpression[] declaredVariables)
		{
			Type type = Type.GetType(TypeName);
			var variable = Expression.Parameter(type, VariableName);
			declaredVariables = new [] { variable };
			return new[] { variable };
		}
	}
}
