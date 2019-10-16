using System.Linq.Expressions;

namespace Utils.Mathematics.Expressions.Compiler
{
	public class Continue : IExpressionTree
	{
		public IExpressionTree Parent { get; set; }

		public Expression[] CreateExpression(Context context)
		{
			for (IExpressionTree ancestor = Parent; Parent != null; ancestor = ancestor.Parent)
			{
				if (ancestor is IBreakableContinuableTree breakableContinuableTree)
				{
					return new[] { Expression.Break(breakableContinuableTree.ContinueLabel) };
				}
			}
			throw new CompilerException("break inattendu", "break");
		}
	}
}