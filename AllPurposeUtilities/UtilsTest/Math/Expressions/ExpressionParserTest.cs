using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Math.Expressions
{
	[TestClass]
	public class ExpressionParserTest
	{
		[TestMethod]
		public void SimpleExpressionTest1()
		{
			ExpressionParser parser = new ExpressionParser();
			var expression = parser.Parse("x", Expression.Parameter(typeof(double), "x"));
			Assert.IsTrue(ExpressionUtils.Equals(expression, (Expression<Func<double, double>>)(x => x)));
		}


	}
}
