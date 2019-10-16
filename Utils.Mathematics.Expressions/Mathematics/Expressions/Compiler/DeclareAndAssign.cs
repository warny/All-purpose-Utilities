using System;
using System.Linq.Expressions;

namespace Utils.Mathematics.Expressions.Compiler
{
	public class DeclareAndAssign : Assignation
	{
		public string TypeName { get; set; }

		public override Expression[] CreateExpression(Context context)
		{
			var right = Right.CreateExpression(context);
			Type t;
			if (TypeName == "var")
			{
				t = right[0].Type;
			}
			else
			{
				t = Type.GetType(TypeName);
			}

			var variable = Expression.Variable(t, VariableName);
			context.Variables.Add(variable);
			return new Expression[] {
				Expression.Assign(variable, right.ToExpression())
			};
		}
	}
}