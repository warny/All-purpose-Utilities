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
		private Assignation initializer;
		private IExpressionTree test;
		private IExpressionTree stepper;
		private IExpressionTree body;

		public IExpressionTree Parent { get; set; }
		public LabelTarget ContinueLabel { get; }
		public LabelTarget BreakLabel { get; }

		public Assignation Initializer
		{
			get => initializer;
			set {
				initializer = value;
				initializer.Parent = this;
			}
		}
		public IExpressionTree Test
		{
			get => test;
			set {
				test = value;
				test.Parent = this;
			}
		}
		public IExpressionTree Stepper {
			get => stepper;
			set {
				stepper = value;
				stepper.Parent = this;
			}
		}
		public IExpressionTree Body {
			get => body;
			set {
				body = value;
				body.Parent = this;
			}
		}

		public For()
		{
			ContinueLabel = Expression.Label();
			BreakLabel = Expression.Label();
		}


		public Expression[] CreateExpression(Context context)
		{
			var initializerExpression = Initializer.CreateExpression(context);

			var testExpression = Test.CreateExpression(context)[0];
			var stepperExpression = Stepper.CreateExpression(context);

			var loopExpression =
				Expression.Block(
					new Expression[] {
						Expression.IfThen(
							Expression.Not(testExpression),
							Expression.Break(BreakLabel)
					) }
					.Concat(Body.CreateExpression(context))
					.Concat(new Expression[] { Expression.Label(ContinueLabel) })
					.Concat(stepperExpression)
				);
			return
				initializerExpression.Union(
					new Expression[] {
					Expression.Loop(loopExpression, BreakLabel)
				}
				).ToArray();

		}
	}
}
