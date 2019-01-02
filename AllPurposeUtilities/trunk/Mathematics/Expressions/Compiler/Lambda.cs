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

		public Expression CreateLambda(ParameterExpression[] parameters)
		{
			Context context = new Context();
			context.Variables.AddRange(parameters);
			context.Labels.AddRange(Labels);

			return CreateExpression(context)[0];
		}

		public Expression[] CreateExpression(Context context)
		{
			Type returnType = Type.GetType(ReturnType);

			List<Expression> expressions = new List<Expression>();

			foreach (var expressionTree in ExpressionTrees) {
				var expression = expressionTree.CreateExpression(context);
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

			return new[] {
				Expression.Lambda(
					body,
					Name,
					context.Variables
				)
			};
		}
	}
}
