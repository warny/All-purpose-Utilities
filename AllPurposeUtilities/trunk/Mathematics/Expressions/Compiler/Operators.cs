using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Utils.Lists;

namespace Utils.Mathematics.Expressions.Compiler
{
	public class BinaryOperator : ExpressionTreeWithLeftAndRight
	{
		public Func<Expression, Expression, Expression> Operator { get; set; }

		public override Expression[] CreateExpression(Context context)
		{
			Expression leftExpression = Left.CreateExpression(context).ToExpression();
			Expression rightExpression = Right.CreateExpression(context).ToExpression();

			return new Expression[] {
				Operator(leftExpression, rightExpression)
			};
		}
	}

	public class UnaryOperator : ExpressionTreeWithUnary
	{
		public Func<Expression, Expression> Operator { get; set; }

		public override Expression[] CreateExpression(Context context)
		{
			Expression expression = Expression.CreateExpression(context).ToExpression();

			return new Expression[] {
				Operator(expression)
			};
		}
	}

}
