using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Utils.Lists;

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
