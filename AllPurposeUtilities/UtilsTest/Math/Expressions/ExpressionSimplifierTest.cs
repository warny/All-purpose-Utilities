using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Math.Expressions
{
	[TestClass]
	public class ExpressionSimplifierTest
	{
		[TestMethod]
		public void SimpleExpressionTest1()
		{
			var simplifier = new ExpressionSimplifier();

			Expression<Func<double, double>> expression = x => System.Math.Pow(System.Math.Cos(x), 2) + System.Math.Pow(System.Math.Sin(x), 2);
			Expression<Func<double, double>> Target = x => 1;

			var simplified = simplifier.Simplify(expression);
			Assert.IsTrue(ExpressionUtils.Equals(expression, Target));
		}
	}
}
