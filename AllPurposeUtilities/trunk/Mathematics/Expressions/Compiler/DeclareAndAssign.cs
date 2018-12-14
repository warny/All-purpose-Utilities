using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Utils.Lists;

namespace Utils.Mathematics.Expressions.Compiler
{
	public class DeclareAndAssign : IExpressionTree
	{
		public IExpressionTree Parent { get; set; }

		public string TypeName { get; set; }
		public string VariableName { get; set; }
		public IExpressionTree RightExpression { get; set; }

		public Expression[] CreateExpression(ParameterExpression[] variables, IndexedList<string, LabelTarget> labels, out ParameterExpression[] declaredVariables)
		{
			var right = RightExpression.CreateExpression(variables, labels, out declaredVariables);
			Type t;
			if (TypeName == "var")
			{
				t = right[0].Type;
			}
			else
			{
				t = Type.GetType(TypeName);
			}
			
			var variable = Expression.Parameter(t, VariableName);
			declaredVariables = declaredVariables.Append(variable).ToArray();
			return new Expression[] {
				variable,
				Expression.Assign(variable, right.ToExpression())
			};

		}
	}
}
