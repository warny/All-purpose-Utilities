using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Utils.Lists;

namespace Utils.Mathematics.Expressions.Compiler
{
	public class ComputeOperator : IExpressionTree
	{
		private IExpressionTree left;
		private IExpressionTree right;

		public IExpressionTree Parent { get; set; }

		public IExpressionTree Left
		{
			get => left;
			set {
				left = value;
				left.Parent = this;
			}
		}
		public IExpressionTree Right
		{
			get => right;
			set {
				right = value;
				right.Parent = this;
			}
		}

		public Func<Expression, Expression, Expression> Operator { get; set; }

		public Expression[] CreateExpression(Context context)
		{
			Expression leftExpression = Left.CreateExpression(context).ToExpression();
			Expression rightExpression = Right.CreateExpression(context).ToExpression();

			return new Expression[] {
				Operator(leftExpression, rightExpression)
			};
		}
	}
}
