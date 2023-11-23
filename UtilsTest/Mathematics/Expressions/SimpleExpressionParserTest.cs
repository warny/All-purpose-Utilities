using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq.Expressions;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions
{
	[TestClass]
	public class MathExpressionParserTest
	{
		[TestMethod]
		public void ParseSimpleExpressions()
		{
			var parameters = new ParameterExpression[] {
				Expression.Parameter(typeof(double), "x"),
				Expression.Parameter(typeof(double), "y")
			};

			var tests = new (string Expression, Expression Expected)[] {
				("x+y", (double x, double y) => x + y),
				("x-y", (double x, double y) => x - y),
				("x*y", (double x, double y) => x * y),
				("x/y", (double x, double y) => x / y),
				("x%y", (double x, double y) => x % y),
				("x**y", (double x, double y) => Math.Pow(x, y)),
			};

			foreach (var test in tests)
			{
				var result = ExpressionParser.Parse<Func<double, double, double>>(test.Expression, parameters);
				Assert.AreEqual(test.Expected, result, ExpressionComparer.Default);
			}
		}

		[TestMethod]
		public void ParseGroupingExpressions()
		{
			var parameters = new ParameterExpression[] {
				Expression.Parameter(typeof(double), "x"),
				Expression.Parameter(typeof(double), "y"),
				Expression.Parameter(typeof(double), "z")
			};

			var tests = new (string Expression, Expression Expected)[] {
				("x+y+z", (double x, double y, double z) => x + y + z),
				("x-y*z", (double x, double y, double z) => x - y * z),
				("(x-y)*z", (double x, double y, double z) => (x - y) * z),
				("x*y+z", (double x, double y, double z) => x * y + z),
				("x/(y-z)", (double x, double y, double z) => x / ( y - z )),
			};


			foreach (var test in tests)
			{
                var result = ExpressionParser.Parse<Func<double, double, double, double>>(test.Expression, parameters);
				Assert.AreEqual(test.Expected, result, ExpressionComparer.Default);
			}
		}

		[TestMethod]
		public void ParseFunctionExpressions()
		{
			var parameters = new ParameterExpression[] {
				Expression.Parameter(typeof(double), "x"),
			};

			var tests = new (string Expression, Expression Expected)[] {
				("Cos(x)", (double x) => Math.Cos(x)),
				("Sin(x)", (double x) => Math.Sin(x)),
				("Tan(x)", (double x) => Math.Tan(x)),
			};


			foreach (var test in tests)
			{
                var result = ExpressionParser.Parse<Func<double, double>>(test.Expression, parameters, typeof(Math), false);
				Assert.AreEqual(test.Expected, result, ExpressionComparer.Default);
			}
		}

	}
}
				