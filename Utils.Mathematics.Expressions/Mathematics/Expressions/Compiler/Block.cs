using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Utils.Arrays;
using Utils.Lists;

namespace Utils.Mathematics.Expressions.Compiler
{
	public class Block : IExpressionTree
	{
		public IExpressionTree Parent { get; set; }

		public ExpressionTreeList ExpressionTrees { get; }

		public Block() { ExpressionTrees = new ExpressionTreeList(this); }

		public Expression[] CreateExpression(Context context)
		{
			context.Push();

			List<Expression> expressions = new List<Expression>();

			foreach (var expressionTree in ExpressionTrees) {
				var expression = expressionTree.CreateExpression(context);
				expressions.AddRange(expression);
			}

			context.Pop();
			return new[] { Expression.Block(expressions) };
		}
	}
}
