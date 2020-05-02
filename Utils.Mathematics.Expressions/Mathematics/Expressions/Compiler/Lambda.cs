using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Utils.Lists;

namespace Utils.Mathematics.Expressions.Compiler
{
	public class Lambda : IExpressionTree
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
			context.Push();
			Type returnType = Type.GetType(ReturnType);

			List<Expression> expressions = new List<Expression>();

			ReturnLabel = Expression.Label(returnType);

			foreach (var expressionTree in ExpressionTrees)
			{
				var expression = expressionTree.CreateExpression(context);
				expressions.AddRange(expression);
			}

			Expression body;
			if (expressions.Count == 1 && expressions[0].Type == returnType)
			{
				body = expressions[0];
			}
			else
			{
				expressions.Add(Expression.Label(ReturnLabel, Expression.Default(returnType)));
				body = Expression.Block(
					returnType,
					context.PeekVariables(),
					expressions.ToArray()
				);
			}

			context.Pop();
			var result = new[] {
				Expression.Lambda(
					body,
					Name,
					context.Variables
				)
			};
			return result;
		}
	}

	public class ReturnValue : IExpressionTree
	{
		public IExpressionTree Parent { get; set; }
		public IExpressionTree Expression { get; set; }

		public Expression[] CreateExpression(Context context)
		{
			var l = this.Parent;
			Lambda lambda;
			while ((lambda = l as Lambda) == null)
			{
				l = l.Parent;
				if (l == null) throw new NullReferenceException();
			}

			var returnExpression = Expression.CreateExpression(context).ToExpression();

			return new[] {
				System.Linq.Expressions.Expression.Return(
					lambda.ReturnLabel,
					returnExpression
				)
			};
		}
	}
}