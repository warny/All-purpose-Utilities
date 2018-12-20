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
	public class Lambda: IExpressionTree
	{
		public IExpressionTree Parent { get; set; }

		public string Name { get; set; }
		public IndexedList<string, LabelTarget> Labels { get; }
		public ExpressionTreeList ExpressionTrees { get; set; }
		public string ReturnType { get; set; }

		internal LabelTarget ReturnLabel { get; set; }

		public Lambda()
		{
			ExpressionTrees = new ExpressionTreeList(this);
			Labels = new IndexedList<string, LabelTarget>(l => l.Name);
		}

		public Expression[] CreateExpression(ParameterExpression[] variables, IndexedList<string, LabelTarget> labels, out ParameterExpression[] declaredVariables)
		{
			declaredVariables = null;
			List<ParameterExpression> innerVariablesList = new List<ParameterExpression>(variables);
			ParameterExpression[] innerVariables = innerVariablesList.ToArray();

			Type returnType = Type.GetType(ReturnType);

			List<Expression> expressions = new List<Expression>();

			foreach (var expressionTree in ExpressionTrees) {
				var expression = expressionTree.CreateExpression(innerVariables, labels, out var newVariables);
				if (!newVariables.IsNullOrEmpty()) {
					innerVariablesList.AddRange(newVariables);
					innerVariables = innerVariablesList.ToArray();
				}
				expressions.AddRange(expression);
			}
			ReturnLabel = Expression.Label(returnType);

			Expression body;
			if (expressions.Count == 1 && expressions[0].Type == returnType) {
				body = expressions[0];
			}
			else {
				expressions.Add(Expression.Label(ReturnLabel));
				body = Expression.Block(returnType, expressions.ToArray());
			}



			declaredVariables = null;
			return new[] {
				Expression.Lambda(
					body,
					Name,
					variables
				)
			};
		}
	}
}
