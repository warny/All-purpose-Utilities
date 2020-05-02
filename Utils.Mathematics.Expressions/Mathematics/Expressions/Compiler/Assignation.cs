using System;
using System.Linq.Expressions;

namespace Utils.Mathematics.Expressions.Compiler
{
	public class Assignation : ExpressionTreeWithRight
	{
		public string VariableName { get; set; }
		public Func<Expression, Expression, Expression> Operator { get; set; }

		public override Expression[] CreateExpression(Context context)
		{
			var variable = context.Variables[VariableName];
			if (variable == null) throw new CompilerException("Objet non déclaré", VariableName);

			return new Expression[] { Operator(
				variable,
				Right.CreateExpression(context).ToExpression())
			};
		}
	}
}