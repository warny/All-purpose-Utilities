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

		public IExpressionTree Left {
			get => left;
			set
			{
				left.Parent = this;
				left = value;
			}
		}
		public IExpressionTree Right
		{
			get => right;
			set
			{
				right.Parent = this;
				right = value;
			}
		}

		public Func<Expression, Expression, Expression> Operator { get; set; }

		public Expression[] CreateExpression(ParameterExpression[] variables, IndexedList<string, LabelTarget> labels, out ParameterExpression[] declaredVariables)
		{
			Expression leftExpression = Left.CreateExpression(variables, labels, out var leftVariables).ToExpression();
			Expression rightExpression = Right.CreateExpression(variables, labels, out var rightVariables).ToExpression();

			declaredVariables = leftVariables.Union(rightVariables).ToArray();

			return new Expression[] {
				Operator(leftExpression, rightExpression)
			};
		}
	}
}
