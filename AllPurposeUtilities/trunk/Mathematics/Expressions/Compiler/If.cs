using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Utils.Lists;

namespace Utils.Mathematics.Expressions.Compiler
{
	public class If : IExpressionTree
	{
		public IExpressionTree Parent { get; set; }
		public IExpressionTree Test { get; set; }
		public IExpressionTree TruePart { get; set; }
		public IExpressionTree FalsePart { get; set; }


		public Expression[] CreateExpression(ParameterExpression[] variables, IndexedList<string, LabelTarget> labels, out ParameterExpression[] declaredVariables)
		{
			var testExpressions = Test.CreateExpression(variables, labels, out declaredVariables);
			variables = variables.Union(declaredVariables).ToArray();
			var truePartExpressions = TruePart.CreateExpression(variables, labels, out var truePartVariables);

			if (FalsePart == null)
			{
				return new [] { Expression.IfThen(testExpressions.ToExpression(), truePartExpressions.ToExpression()) };
			} else {
				var falsePartExpression = FalsePart.CreateExpression(variables, labels, out var falsePartVariables);
				return new [] { Expression.IfThenElse(testExpressions.ToExpression(), truePartExpressions.ToExpression(), falsePartExpression.ToExpression()) };
			}
		}
	}
}
