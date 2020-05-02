using System.Collections.Generic;
using System.Linq.Expressions;

namespace Utils.Mathematics.Expressions.Compiler
{
	public class Block : IExpressionTree
	{
		public IExpressionTree Parent { get; set; }

		public ExpressionTreeList ExpressionTrees { get; }

		public Block()
		{
			ExpressionTrees = new ExpressionTreeList(this);
		}

		public Expression[] CreateExpression(Context context)
		{
			context.Push();

			List<Expression> expressions = new List<Expression>();

			foreach (var expressionTree in ExpressionTrees)
			{
				var expression = expressionTree.CreateExpression(context);
				expressions.AddRange(expression);
			}

			context.Pop();
			return new[] { Expression.Block(expressions) };
		}
	}
}