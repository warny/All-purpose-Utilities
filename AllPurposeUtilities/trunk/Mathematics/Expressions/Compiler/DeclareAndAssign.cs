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

		public override Expression[] CreateExpression(Context context)
		{
			var right = Right.CreateExpression(context);
			Type t;
			if (TypeName == "var") {
				t = right[0].Type;
			}
			else {
				t = Type.GetType(TypeName);
			}

			var variable = Expression.Parameter(t, VariableName);
			context.Variables.Add(variable);
			return new Expression[] {
				variable,
				Expression.Assign(variable, right.ToExpression())
			};

		}
	}
}
