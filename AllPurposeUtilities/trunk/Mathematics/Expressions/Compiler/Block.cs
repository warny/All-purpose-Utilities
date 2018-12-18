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

		public List<IExpressionTree> ExpressionTrees { get; set; }

		public Expression[] CreateExpression(ParameterExpression[] variables, IndexedList<string, LabelTarget> labels, out ParameterExpression[] declaredVariables)
		{
			declaredVariables = null;
			List<ParameterExpression> innerVariablesList = new List<ParameterExpression>(variables);
			ParameterExpression[] innerVariables = innerVariablesList.ToArray();

			List<Expression> expressions = new List<Expression>();

			foreach (var expressionTree in ExpressionTrees)
			{
				var expression = expressionTree.CreateExpression(innerVariables, labels, out var newVariables);
				if (!newVariables.IsNullOrEmpty())
				{
					innerVariablesList.AddRange(newVariables);
					innerVariables = innerVariablesList.ToArray();
				}
				expressions.AddRange(expression);
			}
			return new[] { Expression.Block(expressions) };
		}
	}
}
