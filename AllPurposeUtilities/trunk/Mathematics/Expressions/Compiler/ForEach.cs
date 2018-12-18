using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Utils.Lists;

namespace Utils.Mathematics.Expressions.Compiler
{
	public class ForEach : IExpressionTree, IBreakableContinuableTree
	{
		public IExpressionTree Parent { get; set; }
		public LabelTarget ContinueLabel { get; }
		public LabelTarget BreakLabel { get; }

		public Expression[] CreateExpression(ParameterExpression[] variables, IndexedList<string, LabelTarget> labels, out ParameterExpression[] declaredVariables)
		{
			throw new NotImplementedException();
		}
	}
}
