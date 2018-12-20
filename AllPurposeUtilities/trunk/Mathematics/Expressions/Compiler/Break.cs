using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Utils.Lists;

namespace Utils.Mathematics.Expressions.Compiler
{
	public class Break : IExpressionTree
	{
		public IExpressionTree Parent { get; set; }

		public Expression[] CreateExpression(ParameterExpression[] variables, IndexedList<string, LabelTarget> labels, out ParameterExpression[] declaredVariables)
		{
			declaredVariables = null;
			for (IExpressionTree ancestor = Parent; Parent != null; ancestor = ancestor.Parent) {
				if (ancestor is IBreakableContinuableTree breakableContinuableTree) {
					return new[] { Expression.Break(breakableContinuableTree.BreakLabel) };
				}
			}
			throw new CompilerException("break inattendu", "break");
		}
	}
}
