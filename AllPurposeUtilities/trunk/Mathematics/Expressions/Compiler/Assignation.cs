using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Utils.Lists;

namespace Utils.Mathematics.Expressions.Compiler
{
	public class Assignation : IExpressionTree
	{
		private IExpressionTree right;

		public IExpressionTree Parent { get; set; }
		public string VariableName { get; set; }
		public Func<Expression, Expression, Expression> Operator { get; set; }
		public IExpressionTree Right
		{
			get => right;
			set
			{
				right.Parent = this;
				right = value;
			}
		}


		public virtual Expression[] CreateExpression(ParameterExpression[] variables, IndexedList<string, LabelTarget> labels, out ParameterExpression[] declaredVariables)
		{
			var variable = variables.FirstOrDefault(v => v.Name == VariableName);
			if (variable == null) throw new CompilerException("Objet non déclaré", VariableName);

			return new Expression[] { Operator(
				variable,
				Right.CreateExpression(variables, labels, out declaredVariables).ToExpression())
			};
		}
	}
}
