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

		public Expression[] CreateExpression(Context context)
		{
			Type type = Type.GetType(TypeName);
			var variable = Expression.Variable(type, VariableName);
			context.Variables.Add(variable);
			return new Expression[] { };
		}
	}
}
