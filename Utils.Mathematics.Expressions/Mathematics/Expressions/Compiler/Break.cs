using System.Linq.Expressions;

namespace Utils.Mathematics.Expressions.Compiler
{
	public class Break : IExpressionTree
	{
		public IExpressionTree Parent { get; set; }

		public Expression[] CreateExpression(Context context)
		{
			for (IExpressionTree ancestor = Parent; Parent != null; ancestor = ancestor.Parent)
			{
				if (ancestor is IBreakableContinuableTree breakableContinuableTree)
				{
					return new[] { Expression.Break(breakableContinuableTree.BreakLabel) };
				}
			}
			throw new CompilerException("break inattendu", "break");
		}
	}
}