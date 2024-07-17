using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions
{
    [TestClass]
	public class ExpressionSimplifierTests
	{
		[TestMethod]
		public void SimpleExpressionTest1()
		{
			(Expression<Func<double, double>> Expression, Expression<Func<double, double>> Expected)[] Test = [
				(x => Math.Pow(Math.Cos(x), 2) + Math.Pow(Math.Sin(x), 2), x => 1),
				(x => Math.Sin(x) / Math.Cos(x), x => Math.Tan(x)),
				(x => Math.Cos(x) / Math.Sin(x), x => 1 / Math.Tan(x)),
				(x => Math.Cos(x) * Math.Tan(x), x => Math.Sin(x)),
				(x => Math.Tan(x) * Math.Cos(x), x => Math.Sin(x)),
				(x => Math.Sin(x) / Math.Tan(x), x => Math.Cos(x)),
			];

			var simplifier = new ExpressionSimplifier();
			foreach (var test in Test)
			{
				var simplified = simplifier.Simplify(test.Expression);
				Assert.AreEqual(test.Expected, simplified, ExpressionComparer.Default);
			}
		}

	}
}
