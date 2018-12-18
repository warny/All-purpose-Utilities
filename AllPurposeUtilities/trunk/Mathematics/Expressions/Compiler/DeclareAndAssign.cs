using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Utils.Lists;

namespace Utils.Mathematics.Expressions.Compiler
{
	public class DeclareAndAssign : Assignation
	{
		public string TypeName { get; set; }

		public override Expression[] CreateExpression(ParameterExpression[] variables, IndexedList<string, LabelTarget> labels, out ParameterExpression[] declaredVariables)
		{
			var right = Right.CreateExpression(variables, labels, out declaredVariables);
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
