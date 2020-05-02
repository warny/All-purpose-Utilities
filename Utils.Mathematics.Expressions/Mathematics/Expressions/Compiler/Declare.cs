using System;
using System.Linq.Expressions;

namespace Utils.Mathematics.Expressions.Compiler
{
	public class Declare : IExpressionTree
	{
		public string TypeName { get; set; }
		public string VariableName { get; set; }

		public IExpressionTree Parent { get; set; }

		public Expression[] CreateExpression(Context context)
		{
			Type type = Type.GetType(TypeName);
			var variable = Expression.Variable(type, VariableName);
			context.Variables.Add(variable);
			return new Expression[] { };
		}
	}
}