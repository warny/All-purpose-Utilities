using System.Linq;
using System.Linq.Expressions;

namespace Utils.Mathematics.Expressions.Compiler
{
	public class While : IBreakableContinuableTree
	{
		private IExpressionTree test;
		private IExpressionTree body;

		public IExpressionTree Parent { get; set; }
		public LabelTarget ContinueLabel { get; private set; }
		public LabelTarget BreakLabel { get; private set; }

		public IExpressionTree Test
		{
			get => test;
			set {
				test = value;
				test.Parent = this;
			}
		}

		public IExpressionTree Body
		{
			get => body;
			set {
				body = value;
				body.Parent = this;
			}
		}

		public While()
		{
			ContinueLabel = Expression.Label();
			BreakLabel = Expression.Label();
		}

		public Expression[] CreateExpression(Context context)
		{
			var testExpression = Test.CreateExpression(context);
			var loopExpression =
				Expression.Block(
					new Expression[] {
					Expression.Label(ContinueLabel),
					Expression.IfThen(
						Expression.Negate(testExpression[0]),
						Expression.Break(BreakLabel)
					) }
					.Concat(Body.CreateExpression(context))
				);

			return new Expression[] {
				Expression.Loop(loopExpression, BreakLabel, ContinueLabel),
				Expression.Label(BreakLabel)
			};
		}
	}
}