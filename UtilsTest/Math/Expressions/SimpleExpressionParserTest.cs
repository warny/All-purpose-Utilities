﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Utils.Mathematics.Expressions;
using Utils.Net.DNS.RFC1183;

namespace UtilsTest.Math.Expressions
{
	[TestClass]
	public class SimpleExpressionParserTest
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
				("x^y", (double x, double y) => System.Math.Pow(x, y)),
			};


			SimpleExpressionParser parser = new SimpleExpressionParser();
			ExpressionComparer comparer = new ExpressionComparer();
			foreach (var test in tests)
			{
				var result = parser.ParseExpression(test.Expression, parameters);
				Assert.AreEqual(test.Expected, result, comparer);
			}
		}
	}
}
				