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
		private IExpressionTree test;
		private IExpressionTree truePart;
		private IExpressionTree falsePart;

		public IExpressionTree Parent { get; set; }
		public IExpressionTree Test
		{
			get => test;
			set {
				test = value;
				test.Parent = this;
			}
		}
		public IExpressionTree TruePart
		{
			get => truePart;
			set {
				truePart = value;
				truePart.Parent = this;
			}
		}
		public IExpressionTree FalsePart
		{
			get => falsePart;
			set {
				falsePart = value;
				falsePart.Parent = this;
			}
		}


		public Expression[] CreateExpression(Context context)
		{
			var testExpressions = Test.CreateExpression(context);
			var truePartExpressions = TruePart.CreateExpression(context);

			if (FalsePart == null) {
				return new[] { Expression.IfThen(testExpressions.ToExpression(), truePartExpressions.ToExpression()) };
			}
			else {
				var falsePartExpression = FalsePart.CreateExpression(context);
				return new[] { Expression.IfThenElse(testExpressions.ToExpression(), truePartExpressions.ToExpression(), falsePartExpression.ToExpression()) };
			}
		}
	}
}
