using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Utils.Lists;

namespace Utils.Mathematics.Expressions.Compiler
{
	public interface IExpressionTree
	{
		IExpressionTree Parent { get; set; }
		Expression[] CreateExpression(ParameterExpression[] variables, IndexedList<string, LabelTarget> labels, out ParameterExpression[] declaredVariables);
	}

	public interface IBreakableContinuableTree : IExpressionTree
	{
		LabelTarget ContinueLabel { get; }
		LabelTarget BreakLabel { get; }
	}

	public class CompilerException : Exception
	{
		public CompilerException(string message, string objectName) : base(message + " : " + objectName)
		{
		}
	}
}
