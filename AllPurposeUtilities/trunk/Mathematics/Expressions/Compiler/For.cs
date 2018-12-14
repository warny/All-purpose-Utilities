using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Utils.Lists;

namespace Utils.Mathematics.Expressions.Compiler
{
	public class For : IBreakableContinuableTree
	{
		public IExpressionTree Parent { get; set; }
		public LabelTarget ContinueLabel { get; }
		public LabelTarget BreakLabel { get; }

		public IExpressionTree Initializer { get; set; }
		public IExpressionTree Test { get; set; }
		public IExpressionTree Stepper { get; set; }
		public IExpressionTree Body { get; set; }

		public For()
		{
			ContinueLabel = Expression.Label();
			BreakLabel = Expression.Label();
		}


		public Expression[] CreateExpression(ParameterExpression[] variables, IndexedList<string, LabelTarget> labels, out ParameterExpression[] declaredVariables)
		{
			declaredVariables = null;
			var initializerExpression = Initializer.CreateExpression(variables, labels, out var initializerVariables)[0];
			variables = variables.Union(initializerVariables).ToArray();

			var testExpression = Test.CreateExpression(variables, labels, out var testVariables)[0];
			var stepperExpression = Stepper.CreateExpression(variables, labels, out var stepperVariables);

			var loopExpression = 
				Expression.Block(
					new Expression[] {
						Expression.IfThen(
							Expression.Negate(testExpression),
							Expression.Break(BreakLabel)
					) }
					.Concat(Body.CreateExpression(variables, labels, out var bodyVariables))
					.Concat(new Expression[] { Expression.Label(ContinueLabel) })
					.Concat(stepperExpression)
				);
			return new Expression[] {
				initializerExpression,
				Expression.Loop(loopExpression, BreakLabel, ContinueLabel),
				Expression.Label(BreakLabel)
			};

		}
	}
}
